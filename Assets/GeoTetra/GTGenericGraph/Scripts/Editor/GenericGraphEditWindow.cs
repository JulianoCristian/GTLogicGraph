﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using Object = UnityEngine.Object;

namespace GeoTetra.GTGenericGraph
{
    public class GenericGraphEditWindow : EditorWindow
    {
        [SerializeField]
        GraphObject m_GraphObject;

        [SerializeField]
        private string m_Selected;

        [NonSerialized]
        bool m_HasError;

        ColorSpace m_ColorSpace;

        GraphObject graphObject
        {
            get { return m_GraphObject; }
            set
            {
                if (m_GraphObject != null)
                    DestroyImmediate(m_GraphObject);
                m_GraphObject = value;
            }
        }

        private GenericGraphEditorView m_GraphEditorView;

        private GenericGraphEditorView graphEditorView
        {
            get { return m_GraphEditorView; }
            set
            {
                if (m_GraphEditorView != null)
                {
                    m_GraphEditorView.RemoveFromHierarchy();
//                    _builderGraphView.Dispose();
                }

                m_GraphEditorView = value;

                if (m_GraphEditorView != null)
                {
//                    _builderGraphView.saveRequested += UpdateAsset;//
//                    _builderGraphView.convertToSubgraphRequested += ToSubGraph;//
//                    _builderGraphView.showInProjectRequested += PingAsset;
                    this.GetRootVisualContainer().Add(m_GraphEditorView);
                }
            }
        }

        public string selectedGuid
        {
            get { return m_Selected; }
            private set { m_Selected = value; }
        }

        [MenuItem("Window/GenericGraph")]
        private static void CreateFromMenu()
        {
            GenericGraphEditWindow window = GetWindow<GenericGraphEditWindow>();
            window.Initialize("temp");
            window.wantsMouseMove = true;
            window.Show();
        }

        private void Initialize(string assetGuid)
        {
            try
            {
                m_ColorSpace = PlayerSettings.colorSpace;

//                var asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(assetGuid));
//                if (asset == null)
//                    return;
//
//                if (!EditorUtility.IsPersistent(asset))
//                    return;
//
//                if (selectedGuid == assetGuid)
//                    return;

//                var path = AssetDatabase.GetAssetPath(asset);
//                var extension = Path.GetExtension(path);
//                Type graphType;
//                switch (extension)
//                {
//                    case ".ShaderGraph":
//                        graphType = typeof(MaterialGraph);
//                        break;
//                    case ".ShaderSubGraph":
//                        graphType = typeof(SubGraph);
//                        break;
//                    default:
//                        return;
//                }

                selectedGuid = assetGuid;

//                var textGraph = File.ReadAllText(path, Encoding.UTF8);
                graphObject = CreateInstance<GraphObject>();
                graphObject.hideFlags = HideFlags.HideAndDontSave;
//                graphObject.graph = JsonUtility.FromJson(textGraph, graphType) as IGraph;
                graphObject.graph = new GenericGraph();
                graphObject.graph.OnEnable();
                graphObject.graph.ValidateGraph();

                graphEditorView =
//                    new GenericGraphEditorView(this, m_GraphObject.graph as AbstractGenericGraph, asset.name)
//                    {
//                        persistenceKey = selectedGuid
//                    };
                new GenericGraphEditorView(this, m_GraphObject.graph as AbstractGenericGraph, "temp")
                {
                    persistenceKey = selectedGuid
                };
                graphEditorView.RegisterCallback<PostLayoutEvent>(OnPostLayout);

//                titleContent = new GUIContent(asset.name);
                titleContent = new GUIContent("temp");

                Repaint();
            }
            catch (Exception)
            {
                m_HasError = true;
                m_GraphEditorView = null;
                graphObject = null;
                throw;
            }
        }

        private void OnDisable()
        {
            graphEditorView = null;
        }

        private void OnDestroy()
        {
            if (graphObject != null)
            {
//                if (graphObject.isDirty && EditorUtility.DisplayDialog("Shader Graph Has Been Modified", "Do you want to save the changes you made in the shader graph?\n\nYour changes will be lost if you don't save them.", "Save", "Don't Save"))
//                    UpdateAsset();
                Undo.ClearUndo(graphObject);
                DestroyImmediate(graphObject);
            }

            graphEditorView = null;
        }
        
        void Update()
        {
            if (m_HasError)
                return;

            if (PlayerSettings.colorSpace != m_ColorSpace)
            {
                graphEditorView = null;
                m_ColorSpace = PlayerSettings.colorSpace;
            }

            try
            {
                if (graphObject == null && selectedGuid != null)
                {
                    var guid = selectedGuid;
                    selectedGuid = null;
                    Initialize(guid);
                }

                if (graphObject == null)
                {
                    Close();
                    return;
                }

                var materialGraph = graphObject.graph as AbstractGenericGraph;
                if (materialGraph == null)
                    return;
                if (graphEditorView == null)
                {
//                    var asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(selectedGuid));
                    graphEditorView = new GenericGraphEditorView(this, materialGraph, "temp") { persistenceKey = selectedGuid };
                    m_ColorSpace = PlayerSettings.colorSpace;
                }

                graphEditorView.HandleGraphChanges();
                graphObject.graph.ClearChanges();
            }
            catch (Exception e)
            {
                m_HasError = true;
                m_GraphEditorView = null;
                graphObject = null;
                Debug.LogException(e);
                throw;
            }
        }

//        private void OnMouseDown(MouseDownEvent evt)
//        {
//            Debug.Log("Mouse Down " + evt);
//        }
//
        private void OnPostLayout(PostLayoutEvent evt)
        {
            Debug.Log("OnGeometryChanged");
            graphEditorView.UnregisterCallback<PostLayoutEvent>(OnPostLayout);
            graphEditorView.GenericGraphView.FrameAll();
        }
    }
}