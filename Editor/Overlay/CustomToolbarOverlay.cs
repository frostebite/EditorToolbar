using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorToolbar
{
    /// <summary>
    /// Scene overlay that displays the custom toolbar GUI using IMGUIContainer.
    /// Provides access to toolbar functionality directly in the Scene View overlay panel.
    /// </summary>
    [Overlay(typeof(SceneView), "Custom Toolbar Overlay")]
    [Icon("d_ToolHandleLocal")]
    public class CustomToolbarOverlay : Overlay
    {
        private static MethodInfo _onToolbarGUIMethod;
        private static MethodInfo _drawSectionGUIMethod;

        static CustomToolbarOverlay()
        {
            // Cache method info for performance
            var toolbarType = typeof(GenericToolbar);
            _onToolbarGUIMethod = toolbarType.GetMethod("OnToolbarGUI", BindingFlags.NonPublic | BindingFlags.Static);
            _drawSectionGUIMethod = toolbarType.GetMethod("DrawSectionGUI", BindingFlags.NonPublic | BindingFlags.Static);
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "Custom Toolbar Overlay" };

            // Add some styling
            root.style.paddingTop = 5;
            root.style.paddingBottom = 5;
            root.style.paddingLeft = 5;
            root.style.paddingRight = 5;

            // Create container for left toolbar (profile, scene, mode selection)
            var leftContainer = new IMGUIContainer(() =>
            {
                try
                {
                    if (_onToolbarGUIMethod != null)
                    {
                        _onToolbarGUIMethod.Invoke(null, null);
                    }
                    else
                    {
                        Debug.LogWarning("[CustomToolbarOverlay] OnToolbarGUI method not found");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CustomToolbarOverlay] Error rendering toolbar GUI: {ex.Message}\n{ex.StackTrace}");
                }
            })
            {
                style =
                {
                    minHeight = 20,
                    flexGrow = 1
                }
            };

            root.Add(leftContainer);

            // Add separator
            var separator = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
                    marginTop = 5,
                    marginBottom = 5
                }
            };
            root.Add(separator);

            // Create container for right toolbar (section content)
            var rightContainer = new IMGUIContainer(() =>
            {
                try
                {
                    if (_drawSectionGUIMethod != null)
                    {
                        _drawSectionGUIMethod.Invoke(null, null);
                    }
                    else
                    {
                        Debug.LogWarning("[CustomToolbarOverlay] DrawSectionGUI method not found");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CustomToolbarOverlay] Error rendering section GUI: {ex.Message}\n{ex.StackTrace}");
                }
            })
            {
                style =
                {
                    minHeight = 20,
                    flexGrow = 1
                }
            };

            root.Add(rightContainer);

            return root;
        }
    }
}
