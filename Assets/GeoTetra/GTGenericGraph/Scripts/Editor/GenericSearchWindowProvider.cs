﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace GeoTetra.GTGenericGraph
{
    public class GenericSearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        private EditorWindow _editorWindow;
        private GenericGraphEditorView _genericGraphEditorView;
        private GenericGraphView _graphView;
        private Texture2D m_Icon;
        public GenericPort connectedPort { get; set; }
        public bool nodeNeedsRepositioning { get; set; }
        public SlotReference targetSlotReference { get; private set; }
        public Vector2 targetPosition { get; private set; }

        public void Initialize(EditorWindow editorWindow, 
            GenericGraphEditorView genericGraphEditorView, 
            GenericGraphView graphView)
        {
            _editorWindow = editorWindow;
            _genericGraphEditorView = genericGraphEditorView;
            _graphView = graphView;
            
            // Transparent icon to trick search window into indenting items
            m_Icon = new Texture2D(1, 1);
            m_Icon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            m_Icon.Apply();
        }

        void OnDestroy()
        {
            if (m_Icon != null)
            {
                DestroyImmediate(m_Icon);
                m_Icon = null;
            }
        }

        struct NodeEntry
        {
            public string[] title;
            public NodeDescription NodeDescription;
            public int compatibleSlotId;
        }

        List<int> m_Ids;
        List<GenericPortDescription> m_Slots = new List<GenericPortDescription>();

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            // First build up temporary data structure containing group & title as an array of strings (the last one is the actual title) and associated node type.
            var nodeEntries = new List<NodeEntry>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypesOrNothing())
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(NodeDescription)))
                    {
                        var attrs = type.GetCustomAttributes(typeof(TitleAttribute), false) as TitleAttribute[];
                        if (attrs != null && attrs.Length > 0)
                        {
                            var node = (NodeDescription)Activator.CreateInstance(type);
                            AddEntries(node, attrs[0].title, nodeEntries);
                        }
                    }
                }
            }

            // Sort the entries lexicographically by group then title with the requirement that items always comes before sub-groups in the same group.
            // Example result:
            // - Art/BlendMode
            // - Art/Adjustments/ColorBalance
            // - Art/Adjustments/Contrast
            nodeEntries.Sort((entry1, entry2) =>
            {
                for (var i = 0; i < entry1.title.Length; i++)
                {
                    if (i >= entry2.title.Length)
                        return 1;
                    var value = entry1.title[i].CompareTo(entry2.title[i]);
                    if (value != 0)
                    {
                        // Make sure that leaves go before nodes
                        if (entry1.title.Length != entry2.title.Length && (i == entry1.title.Length - 1 || i == entry2.title.Length - 1))
                            return entry1.title.Length < entry2.title.Length ? -1 : 1;
                        return value;
                    }
                }
                return 0;
            });

            //* Build up the data structure needed by SearchWindow.

            // `groups` contains the current group path we're in.
            var groups = new List<string>();

            // First item in the tree is the title of the window.
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),
            };

            foreach (var nodeEntry in nodeEntries)
            {
                // `createIndex` represents from where we should add new group entries from the current entry's group path.
                var createIndex = int.MaxValue;

                // Compare the group path of the current entry to the current group path.
                for (var i = 0; i < nodeEntry.title.Length - 1; i++)
                {
                    var group = nodeEntry.title[i];
                    if (i >= groups.Count)
                    {
                        // The current group path matches a prefix of the current entry's group path, so we add the
                        // rest of the group path from the currrent entry.
                        createIndex = i;
                        break;
                    }
                    if (groups[i] != group)
                    {
                        // A prefix of the current group path matches a prefix of the current entry's group path,
                        // so we remove everyfrom from the point where it doesn't match anymore, and then add the rest
                        // of the group path from the current entry.
                        groups.RemoveRange(i, groups.Count - i);
                        createIndex = i;
                        break;
                    }
                }

                // Create new group entries as needed.
                // If we don't need to modify the group path, `createIndex` will be `int.MaxValue` and thus the loop won't run.
                for (var i = createIndex; i < nodeEntry.title.Length - 1; i++)
                {
                    var group = nodeEntry.title[i];
                    groups.Add(group);
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(group)) { level = i + 1 });
                }

                // Finally, add the actual entry.
                tree.Add(new SearchTreeEntry(new GUIContent(nodeEntry.title.Last(), m_Icon)) { level = nodeEntry.title.Length, userData = nodeEntry });
            }

            return tree;
        }

        void AddEntries(NodeDescription nodeDescription, string[] title, List<NodeEntry> nodeEntries)
        {
            if (connectedPort == null)
            {
                nodeEntries.Add(new NodeEntry
                {
                    NodeDescription = nodeDescription,
                    title = title,
                    compatibleSlotId = -1
                });
                return;
            }

            var connectedSlot = connectedPort.PortDescription;
            m_Slots.Clear();
            nodeDescription.GetSlots(m_Slots);
            var hasSingleSlot = m_Slots.Count(s => s.isOutputSlot != connectedSlot.isOutputSlot) == 1;
            m_Slots.RemoveAll(slot =>
            {
                var materialSlot = (GenericPortDescription)slot;
                return !materialSlot.IsCompatibleWith(connectedSlot);
            });

            if (hasSingleSlot && m_Slots.Count == 1)
            {
                nodeEntries.Add(new NodeEntry
                {
                    NodeDescription = nodeDescription,
                    title = title,
                    compatibleSlotId = m_Slots.First().id
                });
                return;
            }

            foreach (var slot in m_Slots)
            {
                var entryTitle = new string[title.Length];
                title.CopyTo(entryTitle, 0);
                entryTitle[entryTitle.Length - 1] += ": " + slot.DisplayName;
                nodeEntries.Add(new NodeEntry
                {
                    title = entryTitle,
                    NodeDescription = nodeDescription,
                    compatibleSlotId = slot.id
                });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var nodeEntry = (NodeEntry)entry.userData;
            var nodeEditor = nodeEntry.NodeDescription;
            
            var windowMousePosition = _editorWindow.GetRootVisualContainer().ChangeCoordinatesTo(_editorWindow.GetRootVisualContainer().parent, context.screenMousePosition - _editorWindow.position.position);
            var graphMousePosition = _graphView.contentViewContainer.WorldToLocal(windowMousePosition);
            nodeEditor.Position = new Vector3(graphMousePosition.x, graphMousePosition.y, 0);
            
            _genericGraphEditorView.AddNode(nodeEditor);

//            if (connectedPort != null)
//            {
//                var connectedSlot = connectedPort.slot;
//                var connectedSlotReference = connectedSlot.owner.GetSlotReference(connectedSlot.id);
//                var compatibleSlotReference = node.GetSlotReference(nodeEntry.compatibleSlotId);
//
//                var fromReference = connectedSlot.isOutputSlot ? connectedSlotReference : compatibleSlotReference;
//                var toReference = connectedSlot.isOutputSlot ? compatibleSlotReference : connectedSlotReference;
//                m_Graph.Connect(fromReference, toReference);
//
//                nodeNeedsRepositioning = true;
//                targetSlotReference = compatibleSlotReference;
//                targetPosition = graphMousePosition;
//            }

            return true;
        }
    }
}
