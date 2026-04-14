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
    ///
    /// The Paps ID uses a dash separator ("EditorToolbar-Left") because UI Toolkit's Q() method
    /// interprets "/" as a USS selector path separator, which would prevent Paps from finding
    /// the Unity dummy placeholder. The Unity dummy attribute ID must match exactly.
    /// </summary>
    [Paps.UnityToolbarExtenderUIToolkit.MainToolbarElement(id: "EditorToolbar-Left", order: 0, useRecommendedStyles: false)]
    public class LeftToolbarElement : IMGUIContainer
    {
        [UnityEditor.Toolbars.MainToolbarElement("EditorToolbar-Left", defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 0)]
        public static UnityEditor.Toolbars.MainToolbarElement CreateDummy()
        {
            return null;
        }

        public LeftToolbarElement()
        {
            name = "EditorToolbar-Left";
            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 350;
            style.minHeight = 22;
            onGUIHandler = DrawLeftToolbar;
        }

        public void InitializeElement()
        {
            // Re-apply in case Paps calls this after construction
            onGUIHandler = DrawLeftToolbar;
            Debug.Log($"[ToolbarExtenderIntegration] LeftToolbarElement initialized. Handler count: {ToolbarExtenderIntegration.LeftToolbarGUI.Count}");
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
    ///
    /// See LeftToolbarElement for notes on dash-separated IDs.
    /// </summary>
    [Paps.UnityToolbarExtenderUIToolkit.MainToolbarElement(id: "EditorToolbar-Right", order: 1, useRecommendedStyles: false)]
    public class RightToolbarElement : IMGUIContainer
    {
        [UnityEditor.Toolbars.MainToolbarElement("EditorToolbar-Right", defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        public static UnityEditor.Toolbars.MainToolbarElement CreateDummy()
        {
            return null;
        }

        public RightToolbarElement()
        {
            name = "EditorToolbar-Right";
            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 250;
            style.minHeight = 22;
            onGUIHandler = DrawRightToolbar;
        }

        public void InitializeElement()
        {
            // Re-apply in case Paps calls this after construction
            onGUIHandler = DrawRightToolbar;
            Debug.Log($"[ToolbarExtenderIntegration] RightToolbarElement initialized. Handler count: {ToolbarExtenderIntegration.RightToolbarGUI.Count}");
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
