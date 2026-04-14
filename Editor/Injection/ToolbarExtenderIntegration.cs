#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorToolbar
{
    /// <summary>
    /// Toolbar GUI handler registration for custom toolbar elements.
    /// Also contains the Paps v3.0.0 dual-attribute toolbar elements that host IMGUI content.
    /// </summary>
    public static class ToolbarExtenderIntegration
    {
        // These handlers are populated by GenericToolbar and invoked by the toolbar elements below
        public static readonly List<Action> LeftToolbarGUI = new List<Action>();
        public static readonly List<Action> RightToolbarGUI = new List<Action>();
    }

    /// <summary>
    /// Left-side toolbar element using the Paps UnityToolbarExtenderUIToolkit v3.0.0 dual-attribute pattern.
    /// Hosts IMGUI content from GenericToolbar (workspace UI, mode selector, runtime status).
    /// </summary>
    [Paps.UnityToolbarExtenderUIToolkit.MainToolbarElement(id: "EditorToolbar/Left", order: 0, useRecommendedStyles: false)]
    public class LeftToolbarElement : IMGUIContainer
    {
        [UnityEditor.Toolbars.MainToolbarElement("EditorToolbar/Left", defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 0)]
        public static UnityEditor.Toolbars.MainToolbarElement CreateDummy()
        {
            return null;
        }

        public void InitializeElement()
        {
            name = "EditorToolbar-Left";
            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 350;
            style.minHeight = 22;

            onGUIHandler = DrawLeftToolbar;
        }

        private void DrawLeftToolbar()
        {
            try
            {
                GUILayout.BeginHorizontal();
                foreach (var handler in ToolbarExtenderIntegration.LeftToolbarGUI)
                {
                    handler?.Invoke();
                }
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ToolbarExtenderIntegration] Error in left toolbar: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Right-side toolbar element using the Paps UnityToolbarExtenderUIToolkit v3.0.0 dual-attribute pattern.
    /// Hosts IMGUI content from GenericToolbar (section-specific tools for the selected mode).
    /// </summary>
    [Paps.UnityToolbarExtenderUIToolkit.MainToolbarElement(id: "EditorToolbar/Right", order: 1, useRecommendedStyles: false)]
    public class RightToolbarElement : IMGUIContainer
    {
        [UnityEditor.Toolbars.MainToolbarElement("EditorToolbar/Right", defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        public static UnityEditor.Toolbars.MainToolbarElement CreateDummy()
        {
            return null;
        }

        public void InitializeElement()
        {
            name = "EditorToolbar-Right";
            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 250;
            style.minHeight = 22;

            onGUIHandler = DrawRightToolbar;
        }

        private void DrawRightToolbar()
        {
            try
            {
                GUILayout.BeginHorizontal();
                foreach (var handler in ToolbarExtenderIntegration.RightToolbarGUI)
                {
                    handler?.Invoke();
                }
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ToolbarExtenderIntegration] Error in right toolbar: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
