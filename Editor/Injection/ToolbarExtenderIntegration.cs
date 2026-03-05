#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Paps.UnityToolbarExtenderUIToolkit;

namespace EditorToolbar
{
    /// <summary>
    /// Integration with unity-toolbar-extender-ui-toolkit package for Unity 6.3+.
    /// This uses the package's MainToolbarElement attribute API to properly register custom UI elements
    /// without elements being moved to UnsupportedUserElements.
    /// </summary>
    public static class ToolbarExtenderIntegration
    {
        // These handlers are populated by GenericToolbar and used by the toolbar elements
        public static readonly List<Action> LeftToolbarGUI = new List<Action>();
        public static readonly List<Action> RightToolbarGUI = new List<Action>();
    }

    /// <summary>
    /// Left-side toolbar element using Paps UnityToolbarExtender UIToolkit.
    /// Uses IMGUIContainer to render IMGUI-based toolbar UI.
    /// </summary>
    [MainToolbarElement(id: "CustomToolbar/Left")]
    public class LeftToolbarElement : IMGUIContainer
    {
        public void InitializeElement()
        {
            onGUIHandler = () =>
            {
                try
                {
                    if (ToolbarExtenderIntegration.LeftToolbarGUI != null && ToolbarExtenderIntegration.LeftToolbarGUI.Count > 0)
                    {
                        GUILayout.BeginHorizontal();
                        foreach (var handler in ToolbarExtenderIntegration.LeftToolbarGUI)
                        {
                            try
                            {
                                handler?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[LeftToolbarElement] Error in toolbar handler: {ex.Message}\n{ex.StackTrace}");
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LeftToolbarElement] Error in onGUIHandler: {ex.Message}\n{ex.StackTrace}");
                }
            };

            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 100;
            style.minHeight = 20;

            name = "CustomToolbarLeft";
        }
    }

    /// <summary>
    /// Right-side toolbar element using Paps UnityToolbarExtender UIToolkit.
    /// Uses IMGUIContainer to render IMGUI-based toolbar UI.
    /// </summary>
    [MainToolbarElement(id: "CustomToolbar/Right")]
    public class RightToolbarElement : IMGUIContainer
    {
        public void InitializeElement()
        {
            onGUIHandler = () =>
            {
                try
                {
                    if (ToolbarExtenderIntegration.RightToolbarGUI != null && ToolbarExtenderIntegration.RightToolbarGUI.Count > 0)
                    {
                        GUILayout.BeginHorizontal();
                        foreach (var handler in ToolbarExtenderIntegration.RightToolbarGUI)
                        {
                            try
                            {
                                handler?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[RightToolbarElement] Error in toolbar handler: {ex.Message}\n{ex.StackTrace}");
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RightToolbarElement] Error in onGUIHandler: {ex.Message}\n{ex.StackTrace}");
                }
            };

            style.flexGrow = 0;
            style.flexShrink = 0;
            style.minWidth = 100;
            style.minHeight = 20;

            name = "CustomToolbarRight";
        }
    }
}
#endif
