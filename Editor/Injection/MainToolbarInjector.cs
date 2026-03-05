// The MainToolbar-based integration is currently disabled behind a compile-time
// define so we can safely build the project on Unity versions where the exact
// MainToolbarElement API shape is uncertain.
//
// To experiment with the official MainToolbar API again, define
// ENABLE_MAIN_TOOLBAR_INJECTOR in your project scripting symbols and update
// the implementation to match the Unity version in use.
#if ENABLE_MAIN_TOOLBAR_INJECTOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Unity 6.3+ MainToolbar API implementation to avoid UnsupportedUserElements.
/// This uses the official MainToolbar API which prevents elements from being moved to UnsupportedUserElements.
/// 
/// NOTE: This will only work in Unity 6.3+. For older versions, CustomToolbarInjector will be used as fallback.
/// </summary>
public static class MainToolbarInjector
{
    // These handlers are populated by GenericToolbar and are currently unused in the
    // simple button-based MainToolbar integration below. We keep them so the existing
    // GenericToolbar wiring compiles, and can be used later if we move to a richer
    // MainToolbarElement implementation.
    public static readonly List<Action> LeftToolbarGUI = new List<Action>();
    public static readonly List<Action> RightToolbarGUI = new List<Action>();

    /// <summary>
    /// Example left-side main toolbar element using the official MainToolbar API.
    /// For now this is a simple button, similar to Unity's documentation example.
    /// </summary>
    [MainToolbarElement("CustomToolbar/Left", defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateLeftToolbar()
    {
        var icon = EditorGUIUtility.IconContent("Toolbar Plus").image as Texture2D;
        var content = new MainToolbarContent(icon);

        return new MainToolbarButton(content, () =>
        {
            try
            {
                // Show combined profile and scene menu from GenericToolbar,
                // which mirrors the old left-side toolbar behaviour but using
                // supported MainToolbar APIs.
                GenericToolbar.ShowMainToolbarLeftMenu();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainToolbarInjector] Error in left toolbar button: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }

    /// <summary>
    /// Example right-side main toolbar element using the official MainToolbar API.
    /// Also implemented as a simple button for now.
    /// </summary>
    [MainToolbarElement("CustomToolbar/Right", defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement CreateRightToolbar()
    {
        var icon = EditorGUIUtility.IconContent("SettingsIcon").image as Texture2D;
        var content = new MainToolbarContent(icon);

        return new MainToolbarButton(content, () =>
        {
            try
            {
                // Use the existing public API that shows the mode selection menu.
                // This replicates the old "Mode" dropdown behaviour on the right.
                GenericToolbar.ShowModeSelectionMenuPublic();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainToolbarInjector] Error in right toolbar button: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }
}

/// <summary>
/// Additional injector that, once the Unity 6.3+ MainToolbar elements are created,
/// finds the visual elements corresponding to our CustomToolbar/Left and
/// CustomToolbar/Right entries and replaces their content with IMGUI containers
/// that render the full GenericToolbar UI.
///
/// This deliberately uses reflection and deep UI Toolkit traversal and may be
/// considered unsupported by Unity in future versions.
/// </summary>
[InitializeOnLoad]
public static class MainToolbarCustomElementInjector
{
    private static bool _installed;

    static MainToolbarCustomElementInjector()
    {
        // Only run when the MainToolbar API exists (Unity 6.3+)
        var mainToolbarElementType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbars.MainToolbarElement");
        if (mainToolbarElementType == null)
        {
            return;
        }

        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        if (_installed)
            return;

        if (!TryInstall())
            return;

        _installed = true;
        EditorApplication.update -= OnUpdate;
    }

    private static bool TryInstall()
    {
        try
        {
            var unityEditorAssembly = typeof(Editor).Assembly;
            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
            {
                Debug.Log("[MainToolbarCustomElementInjector] UnityEditor.Toolbar type not found.");
                return false;
            }

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0)
            {
                return false;
            }

            var toolbar = toolbars[0];

            // Get root VisualElement from toolbar
            VisualElement root = null;

            var mRootField = toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mRootField != null)
            {
                root = mRootField.GetValue(toolbar) as VisualElement;
            }

            if (root == null)
            {
                var rootProp = toolbarType.GetProperty("rootVisualElement",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (rootProp != null)
                    root = rootProp.GetValue(toolbar) as VisualElement;
            }

            if (root == null)
            {
                Debug.Log("[MainToolbarCustomElementInjector] Could not access toolbar root VisualElement.");
                return false;
            }

            // Preferred path: inject into the parent of #rootVisualContainer,
            // which is closer to the true toolbar layout root and gives us a
            // wide host area for the full IMGUI toolbar.
            var rootContainer = FindElementMatching(root, ve =>
            {
                if (ve == null) return false;
                if (!string.IsNullOrEmpty(ve.name) && ve.name.Contains("rootVisualContainer"))
                    return true;
                return false;
            });

            if (rootContainer != null && rootContainer.parent != null)
            {
                InstallFullToolbarIntoOverlayParent(rootContainer.parent);
                Debug.Log("[MainToolbarCustomElementInjector] Installed full IMGUI toolbar into parent of #rootVisualContainer.");
                return true;
            }

            // Secondary path: try parent of #unity-overlay-canvas if present.
            var overlayCanvas = root.Q<VisualElement>("unity-overlay-canvas");
            if (overlayCanvas != null && overlayCanvas.parent != null)
            {
                InstallFullToolbarIntoOverlayParent(overlayCanvas.parent);
                Debug.Log("[MainToolbarCustomElementInjector] Installed full IMGUI toolbar into parent of #unity-overlay-canvas (fallback).");
                return true;
            }

            // Fallback: try to find elements related to our CustomToolbar/Left and CustomToolbar/Right paths.
            // We search by name, tooltip, and label text containing those identifiers.
            var leftHost = FindElementMatching(root, ve =>
            {
                if (ve == null) return false;
                if (!string.IsNullOrEmpty(ve.name) && ve.name.Contains("CustomToolbar/Left"))
                    return true;
                if (!string.IsNullOrEmpty(ve.tooltip) && ve.tooltip.Contains("CustomToolbar/Left"))
                    return true;

                if (ve is TextElement te && !string.IsNullOrEmpty(te.text) && te.text.Contains("CustomToolbar/Left"))
                    return true;

                return false;
            });

            var rightHost = FindElementMatching(root, ve =>
            {
                if (ve == null) return false;
                if (!string.IsNullOrEmpty(ve.name) && ve.name.Contains("CustomToolbar/Right"))
                    return true;
                if (!string.IsNullOrEmpty(ve.tooltip) && ve.tooltip.Contains("CustomToolbar/Right"))
                    return true;

                if (ve is TextElement te && !string.IsNullOrEmpty(te.text) && te.text.Contains("CustomToolbar/Right"))
                    return true;

                return false;
            });

            if (leftHost == null && rightHost == null)
            {
                // Nothing found yet; keep trying on next update
                return false;
            }

            if (leftHost != null)
            {
                InstallIMGUIIntoHost(leftHost, isLeft: true);
            }

            if (rightHost != null)
            {
                InstallIMGUIIntoHost(rightHost, isLeft: false);
            }

            Debug.Log($"[MainToolbarCustomElementInjector] Installed IMGUI containers into toolbar hosts. Left found: {leftHost != null}, Right found: {rightHost != null}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainToolbarCustomElementInjector] Exception during installation: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private static VisualElement FindElementMatching(VisualElement root, Func<VisualElement, bool> predicate)
    {
        if (root == null || predicate == null)
            return null;

        if (predicate(root))
            return root;

        var childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = root[i] as VisualElement;
            var found = FindElementMatching(child, predicate);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Install a single full-width IMGUI container into the parent of
    /// #unity-overlay-canvas, rendering the entire GenericToolbar (left + right)
    /// in one pass. This more closely matches the old inline toolbar behaviour.
    /// </summary>
    private static void InstallFullToolbarIntoOverlayParent(VisualElement overlayParent)
    {
        if (overlayParent == null)
            return;

        // Do not clear existing children (we must keep the overlay canvas itself).
        // Just add our container alongside, and give it a reasonable minimum width.
        var container = new IMGUIContainer(() =>
        {
            try
            {
                // Render the same content that used to live in the main toolbar:
                // left-side profile + scene + mode, and right-side section tools.

                // Left-style content from OnToolbarGUI
                var onToolbarGui = typeof(GenericToolbar).GetMethod("OnToolbarGUI",
                    BindingFlags.NonPublic | BindingFlags.Static);
                onToolbarGui?.Invoke(null, null);

                // Right-side section content
                var drawSectionGuiMethod = typeof(GenericToolbar).GetMethod("DrawSectionGUI",
                    BindingFlags.NonPublic | BindingFlags.Static);
                drawSectionGuiMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainToolbarCustomElementInjector] Error in full IMGUI toolbar container: {ex.Message}\n{ex.StackTrace}");
            }
        });

        container.name = "CustomToolbarFullIMGUI";
        container.style.flexGrow = 0;
        container.style.flexShrink = 0;
        container.style.minWidth = 500; // give the toolbar a decent horizontal footprint

        // Insert before the overlay canvas if possible, so our toolbar appears
        // in a sensible position relative to overlays.
        var index = overlayParent.IndexOf(overlayParent.Q<VisualElement>("unity-overlay-canvas"));
        if (index >= 0)
            overlayParent.Insert(index, container);
        else
            overlayParent.Add(container);
    }

    private static void InstallIMGUIIntoHost(VisualElement host, bool isLeft)
    {
        if (host == null)
            return;

        // Clear any existing children and make room for our IMGUI container
        host.Clear();
        host.style.flexGrow = 0;
        host.style.flexShrink = 0;
        host.style.minWidth = isLeft ? 350 : 250;

        var container = new IMGUIContainer(() =>
        {
            try
            {
                if (isLeft)
                {
                    // Render the main left-side toolbar UI via discovered workspace providers.
                    // Workspace providers (e.g., MonorepoWorkspaceToolbar) handle project-specific UI
                    // like profile status, scene selector, hub buttons, etc.
                    GenericToolbar.DrawProfileStatus();
                    GenericToolbar.DrawSceneSelector();

                    GUILayout.Label("Mode");

                    // Mode dropdown, same behaviour as in OnToolbarGUI.
                    if (GenericToolbar.HasVisibleSections())
                    {
                        var currentMode = GenericToolbar.GetCurrentModeDisplayName();
                        if (GUILayout.Button(new GUIContent(currentMode, "Select Mode")))
                        {
                            GenericToolbar.ShowModeSelectionMenuPublic();
                        }
                    }
                }
                else
                {
                    // Render the right-side toolbar UI: current mode's custom tools.
                    var drawSectionGuiMethod = typeof(GenericToolbar).GetMethod("DrawSectionGUI",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    drawSectionGuiMethod?.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainToolbarCustomElementInjector] Error in {(isLeft ? "left" : "right")} IMGUI container: {ex.Message}\n{ex.StackTrace}");
            }
        });

        container.name = isLeft ? "CustomToolbarLeftIMGUI" : "CustomToolbarRightIMGUI";
        container.style.flexGrow = 0;
        container.style.flexShrink = 0;

        host.Add(container);
    }
}
#endif
