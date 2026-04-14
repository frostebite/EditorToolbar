#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
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

        internal const string LeftElementId = "EditorToolbar-Left";
        internal const string RightElementId = "EditorToolbar-Right";

        // Name assigned to our injected IMGUIContainer so the fallback can detect it
        internal const string LeftInjectedName = "EditorToolbar-Left-IMGUI";
        internal const string RightInjectedName = "EditorToolbar-Right-IMGUI";
    }

    /// <summary>
    /// Left-side toolbar element using the Paps UnityToolbarExtenderUIToolkit v3.0.0 dual-attribute pattern.
    /// Hosts IMGUI content from GenericToolbar (workspace UI, mode selector, runtime status).
    ///
    /// The Paps ID uses a dash separator ("EditorToolbar-Left") to avoid ambiguity with
    /// USS selector paths, though Q() name matching treats "/" literally.
    /// The Unity dummy attribute ID must match exactly.
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
            name = ToolbarExtenderIntegration.LeftInjectedName;
            style.flexGrow = 1;
            style.flexShrink = 0;
            style.minWidth = 350;
            style.minHeight = 22;
            onGUIHandler = DrawLeftToolbar;
        }

        public void InitializeElement()
        {
            // Re-apply in case Paps calls this after construction
            onGUIHandler = DrawLeftToolbar;
            Debug.Log($"[ToolbarExtenderIntegration] LeftToolbarElement.InitializeElement called. Handler count: {ToolbarExtenderIntegration.LeftToolbarGUI.Count}");
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
    /// See LeftToolbarElement for notes on IDs.
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
            name = ToolbarExtenderIntegration.RightInjectedName;
            style.flexGrow = 1;
            style.flexShrink = 0;
            style.minWidth = 250;
            style.minHeight = 22;
            onGUIHandler = DrawRightToolbar;
        }

        public void InitializeElement()
        {
            // Re-apply in case Paps calls this after construction
            onGUIHandler = DrawRightToolbar;
            Debug.Log($"[ToolbarExtenderIntegration] RightToolbarElement.InitializeElement called. Handler count: {ToolbarExtenderIntegration.RightToolbarGUI.Count}");
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

    /// <summary>
    /// Fallback injector that ensures our IMGUI toolbar content is visible even when Paps
    /// fails to replace the Unity MainToolbarElement dummies.
    ///
    /// Strategy: after a short delay, check the MainToolbarWindow's visual tree for our
    /// element IDs. If Paps has already injected our LeftToolbarElement/RightToolbarElement,
    /// we can detect them by the injected name. If not, we inject IMGUIContainers directly
    /// into the OverlayToolbar containers that Unity created for our dummy elements.
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolbarFallbackInjector
    {
        private static bool _leftInjected;
        private static bool _rightInjected;
        private static int _frameCount;
        private static EditorWindow _toolbarWindow;

        static ToolbarFallbackInjector()
        {
            if (Application.isBatchMode) return;

            _leftInjected = false;
            _rightInjected = false;
            _frameCount = 0;

            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;

            Debug.Log("[ToolbarFallbackInjector] Registered. Will check toolbar injection after Paps has had time to initialize.");
        }

        private static void OnUpdate()
        {
            // Wait a few frames for Paps and the toolbar to fully initialize
            _frameCount++;
            if (_frameCount < 30)
                return;

            // Only check periodically (every 60 frames / ~1 second) to avoid perf impact
            if (_frameCount > 30 && (_frameCount % 60) != 0)
                return;

            if (_leftInjected && _rightInjected)
            {
                // Both injected, but keep checking in case toolbar is recreated (layout change)
                if (_toolbarWindow != null)
                    return;
            }

            try
            {
                var toolbarWindow = FindMainToolbarWindow();
                if (toolbarWindow == null)
                    return;

                var root = toolbarWindow.rootVisualElement;
                if (root == null)
                    return;

                _toolbarWindow = toolbarWindow;

                // Check if Paps already injected our elements
                bool leftHasInjection = HasInjectedElement(root, ToolbarExtenderIntegration.LeftElementId, ToolbarExtenderIntegration.LeftInjectedName);
                bool rightHasInjection = HasInjectedElement(root, ToolbarExtenderIntegration.RightElementId, ToolbarExtenderIntegration.RightInjectedName);

                if (!leftHasInjection && !_leftInjected)
                {
                    if (TryInjectIntoElement(root, ToolbarExtenderIntegration.LeftElementId, ToolbarExtenderIntegration.LeftInjectedName, true))
                    {
                        _leftInjected = true;
                        Debug.Log("[ToolbarFallbackInjector] Injected LEFT toolbar IMGUI content (Paps did not inject).");
                    }
                }
                else if (leftHasInjection)
                {
                    _leftInjected = true;
                }

                if (!rightHasInjection && !_rightInjected)
                {
                    if (TryInjectIntoElement(root, ToolbarExtenderIntegration.RightElementId, ToolbarExtenderIntegration.RightInjectedName, false))
                    {
                        _rightInjected = true;
                        Debug.Log("[ToolbarFallbackInjector] Injected RIGHT toolbar IMGUI content (Paps did not inject).");
                    }
                }
                else if (rightHasInjection)
                {
                    _rightInjected = true;
                }

                if (_leftInjected && _rightInjected && _frameCount < 300)
                {
                    Debug.Log("[ToolbarFallbackInjector] Both toolbar elements are injected. Monitoring for toolbar recreation.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ToolbarFallbackInjector] Error during injection check: {ex.Message}");
            }
        }

        private static EditorWindow FindMainToolbarWindow()
        {
            // Try the type Paps uses
            var toolbarWindowType = typeof(Editor).Assembly.GetType("UnityEditor.MainToolbarWindow");

            // Fallback type names for different Unity versions
            if (toolbarWindowType == null)
                toolbarWindowType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarWindowType == null)
                toolbarWindowType = typeof(Editor).Assembly.GetType("UnityEditor.MainWindow");

            if (toolbarWindowType != null)
            {
                try
                {
                    return EditorWindow.GetWindow(toolbarWindowType);
                }
                catch
                {
                    // GetWindow might throw if type is not a valid EditorWindow
                }
            }

            // Last resort: search all windows for one that has our element IDs
            var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in allWindows)
            {
                try
                {
                    var root = window.rootVisualElement;
                    if (root == null) continue;

                    var leftElement = root.Q(ToolbarExtenderIntegration.LeftElementId);
                    if (leftElement != null)
                        return window;
                }
                catch
                {
                    // Some windows may throw when accessing rootVisualElement
                }
            }

            return null;
        }

        /// <summary>
        /// Check if the container for the given element ID already contains our injected IMGUI element.
        /// </summary>
        private static bool HasInjectedElement(VisualElement root, string elementId, string injectedName)
        {
            var container = root.Q(elementId);
            if (container == null)
                return false;

            // Check recursively for our named element
            return FindDescendant(container, injectedName) != null;
        }

        /// <summary>
        /// Inject an IMGUIContainer into the toolbar element container.
        /// Mirrors what Paps TryReplace does: find the container by ID, find the OverlayToolbar
        /// inside, clear it, and add our IMGUIContainer.
        /// </summary>
        private static bool TryInjectIntoElement(VisualElement root, string elementId, string injectedName, bool isLeft)
        {
            var container = root.Q(elementId);
            if (container == null)
            {
                Debug.Log($"[ToolbarFallbackInjector] Container '{elementId}' not found in visual tree.");
                return false;
            }

            // Paps approach: find OverlayToolbar child and add into it
            var overlayToolbar = container.Q<OverlayToolbar>();
            if (overlayToolbar != null)
            {
                // Clear existing children (the empty dummy content)
                var children = overlayToolbar.Children().ToArray();
                foreach (var child in children)
                    overlayToolbar.Remove(child);

                var imgui = CreateIMGUIContainer(injectedName, isLeft);
                overlayToolbar.Add(imgui);
                return true;
            }

            // Fallback: no OverlayToolbar found, inject directly into the container
            // This handles cases where Unity 6000.4 changed the toolbar structure
            Debug.Log($"[ToolbarFallbackInjector] No OverlayToolbar found in '{elementId}', injecting directly into container.");

            // Clear existing children
            var directChildren = container.Children().ToArray();
            foreach (var child in directChildren)
                container.Remove(child);

            var directImgui = CreateIMGUIContainer(injectedName, isLeft);
            container.Add(directImgui);
            return true;
        }

        private static IMGUIContainer CreateIMGUIContainer(string containerName, bool isLeft)
        {
            var handlers = isLeft
                ? ToolbarExtenderIntegration.LeftToolbarGUI
                : ToolbarExtenderIntegration.RightToolbarGUI;

            var imgui = new IMGUIContainer(() =>
            {
                try
                {
                    GUILayout.BeginHorizontal();
                    foreach (var handler in handlers)
                    {
                        handler?.Invoke();
                    }
                    GUILayout.EndHorizontal();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ToolbarFallbackInjector] Error in {containerName}: {ex.Message}");
                }
            });

            imgui.name = containerName;
            imgui.style.flexGrow = 1;
            imgui.style.flexShrink = 0;
            imgui.style.minWidth = isLeft ? 350 : 250;
            imgui.style.minHeight = 22;

            return imgui;
        }

        private static VisualElement FindDescendant(VisualElement parent, string name)
        {
            if (parent.name == name)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDescendant(parent[i], name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
#endif
