using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorToolbar
{
/// <summary>
/// Custom toolbar injector for Unity Editor toolbar.
/// 
/// NOTE: In Unity 6, Unity automatically moves custom toolbar elements into "UnsupportedElements"
/// which can make them cramped. Users can right-click the toolbar and select 
/// "Unsupported User Elements" to unhide/show these elements.
/// 
/// For a more robust solution, consider migrating to Unity 6's official Toolbars API:
/// https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Toolbars.MainToolbar.html
/// </summary>
[InitializeOnLoad]
public static class CustomToolbarInjector
{
    private const string CustomLeftContainerName = "CustomToolbarLeft";
    private const string CustomRightContainerName = "CustomToolbarRight";
    
    private static bool _installed;
    private static ScriptableObject _currentToolbar;
    
    public static readonly List<Action> LeftToolbarGUI = new List<Action>();
    public static readonly List<Action> RightToolbarGUI = new List<Action>();
    
    static CustomToolbarInjector()
    {
        // Skip in batch mode (CI builds) — no toolbar to inject into
        if (Application.isBatchMode)
            return;

        // If the official MainToolbar API is available (Unity 6.3+), we prefer that path
        // via MainToolbarInjector and disable this legacy injector to avoid creating
        // elements that Unity moves into UnsupportedUserElements.
        var mainToolbarElementType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbars.MainToolbarElement");
        if (mainToolbarElementType != null)
        {
            Debug.Log("[CustomToolbarInjector] MainToolbar API detected - disabling legacy injector (toolbar elements use Paps dual-attribute pattern via ToolbarExtenderIntegration).");
            return;
        }

        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
        Debug.Log($"[CustomToolbarInjector] Initialized - LeftToolbarGUI handlers: {LeftToolbarGUI.Count}, RightToolbarGUI handlers: {RightToolbarGUI.Count}");
    }
    
    private static void OnUpdate()
    {
        // Toolbar gets recreated when layout changes, so we need to reinstall
        if (_currentToolbar == null || !_installed)
        {
            if (TryInstall())
            {
                _installed = true;
            }
        }
        else
        {
            // Verify toolbar still exists
            var unityEditorAssembly = typeof(Editor).Assembly;
            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
            if (toolbarType != null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                bool toolbarStillExists = false;
                foreach (var toolbar in toolbars)
                {
                    if (toolbar == _currentToolbar)
                    {
                        toolbarStillExists = true;
                        break;
                    }
                }
                
                if (!toolbarStillExists)
                {
                    // Toolbar was recreated, need to reinstall
                    _currentToolbar = null;
                    _installed = false;
                }
            }
        }
    }
    
    private static bool TryInstall()
    {
        var unityEditorAssembly = typeof(Editor).Assembly;
        bool installedAny = false;
        
        // Strategy 1: Search from ContainerWindow root (like status bar injector)
        // This is where #overlay-toolbar__top is located in Unity 6
        var containerWindowType = unityEditorAssembly.GetType("UnityEditor.ContainerWindow");
        if (containerWindowType != null)
        {
            var containerWindows = Resources.FindObjectsOfTypeAll(containerWindowType);
            Debug.Log($"[CustomToolbarInjector] Found {containerWindows.Length} ContainerWindow(s)");
            
            bool strategy1FoundOverlay = false;
            foreach (var containerWindow in containerWindows)
            {
                VisualElement root = null;
                
                // Try property first
                var rootVisualElementProp = containerWindowType.GetProperty("rootVisualElement", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rootVisualElementProp != null)
                {
                    root = rootVisualElementProp.GetValue(containerWindow, null) as VisualElement;
                }
                
                // Try field if property didn't work
                if (root == null)
                {
                    var rootVisualElementField = containerWindowType.GetField("rootVisualElement", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rootVisualElementField != null)
                    {
                        root = rootVisualElementField.GetValue(containerWindow) as VisualElement;
                    }
                }
                
                // Try alternative property names
                if (root == null)
                {
                    var mRootProp = containerWindowType.GetProperty("m_Root", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mRootProp != null)
                    {
                        root = mRootProp.GetValue(containerWindow, null) as VisualElement;
                    }
                }
                
                if (root == null) continue;
                
                Debug.Log($"[CustomToolbarInjector] Checking ContainerWindow root: {root.name} (childCount: {root.childCount})");
                
                // Look for #overlay-toolbar__top in ContainerWindow root (using # selector)
                var overlayToolbarTop = root.Q<VisualElement>("#overlay-toolbar__top");
                
                // Also try searching by name without #
                if (overlayToolbarTop == null)
                {
                    overlayToolbarTop = root.Q<VisualElement>(name: "overlay-toolbar__top");
                }
                
                // Also try searching all children recursively
                if (overlayToolbarTop == null)
                {
                    var allRootElements = new List<VisualElement>();
                    CollectChildren(root, allRootElements);
                    overlayToolbarTop = allRootElements.Find(e => 
                        e.name == "overlay-toolbar__top" || 
                        e.name.Contains("overlay-toolbar") ||
                        (e.name.Contains("toolbar") && e.name.Contains("top")));
                    
                    if (overlayToolbarTop != null)
                    {
                        Debug.Log($"[CustomToolbarInjector] Found overlay toolbar by recursive search: {overlayToolbarTop.name}");
                    }
                }
                
                if (overlayToolbarTop != null)
                {
                    strategy1FoundOverlay = true;
                    Debug.Log($"[CustomToolbarInjector] Found #overlay-toolbar__top in ContainerWindow (name: {overlayToolbarTop.name}, childCount: {overlayToolbarTop.childCount}), attempting installation...");
                    if (InstallIntoOverlayToolbarTop(overlayToolbarTop))
                    {
                        Debug.Log("[CustomToolbarInjector] Successfully installed into #overlay-toolbar__top");
                        installedAny = true;
                        // Store toolbar reference for tracking
                        var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                        if (toolbarType != null)
                        {
                            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                            if (toolbars != null && toolbars.Length > 0)
                            {
                                _currentToolbar = (ScriptableObject)toolbars[0];
                            }
                        }
                        break;
                    }
                    else
                    {
                        Debug.LogWarning("[CustomToolbarInjector] Found #overlay-toolbar__top but InstallIntoOverlayToolbarTop returned false");
                    }
                }
                else
                {
                    // Try searching deeper - maybe it's nested
                    var allRootElements2 = new List<VisualElement>();
                    CollectChildren(root, allRootElements2);
                    var foundOverlay = allRootElements2.Find(e => e.name == "overlay-toolbar__top" || e.name.Contains("overlay-toolbar"));
                    if (foundOverlay != null)
                    {
                        Debug.Log($"[CustomToolbarInjector] Found overlay-toolbar element by name search: {foundOverlay.name} (childCount: {foundOverlay.childCount})");
                    }
                    else
                    {
                        Debug.Log($"[CustomToolbarInjector] #overlay-toolbar__top not found in this ContainerWindow root (searched {allRootElements2.Count} elements)");
                    }
                }
            }
            
            Debug.Log($"[CustomToolbarInjector] Strategy 1 complete - installedAny: {installedAny}, foundOverlay: {strategy1FoundOverlay}");
        }
        else
        {
            Debug.LogWarning("[CustomToolbarInjector] ContainerWindow type not found");
        }
        
        // Strategy 1.5: Try to find toolbar in ContainerWindow with better space (not in UnsupportedElements)
        Debug.Log($"[CustomToolbarInjector] Strategy 1.5 check - installedAny: {installedAny}, containerWindowType: {containerWindowType != null}");
        if (!installedAny && containerWindowType != null)
        {
            Debug.Log("[CustomToolbarInjector] Strategy 1.5: Searching ContainerWindows for #overlay-toolbar__top...");
            var containerWindows = Resources.FindObjectsOfTypeAll(containerWindowType);
            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found {containerWindows.Length} ContainerWindow(s) to search");
            foreach (var containerWindow in containerWindows)
            {
                VisualElement root = null;
                
                // Try property first (exactly like status bar injector)
                var rootVisualElementProp = containerWindowType.GetProperty("rootVisualElement", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rootVisualElementProp != null)
                {
                    try
                    {
                        root = rootVisualElementProp.GetValue(containerWindow, null) as VisualElement;
                        if (root != null)
                        {
                            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Successfully accessed rootVisualElement property! root.name: '{root.name}', root.childCount: {root.childCount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Exception accessing rootVisualElement property: {ex.Message}");
                    }
                }
                
                // Unity 6 fallback: Try rootView -> rootVisualElement
                if (root == null)
                {
                    var rootViewProp = containerWindowType.GetProperty("rootView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rootViewProp != null)
                    {
                        try
                        {
                            var rootView = rootViewProp.GetValue(containerWindow, null);
                            if (rootView != null)
                            {
                                Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found rootView (type: {rootView.GetType().Name}), trying to get rootVisualElement from it...");
                                var viewType = rootView.GetType();
                                var viewRootProp = viewType.GetProperty("rootVisualElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (viewRootProp != null)
                                {
                                    root = viewRootProp.GetValue(rootView, null) as VisualElement;
                                    if (root != null)
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Successfully accessed rootVisualElement from rootView! root.name: '{root.name}', root.childCount: {root.childCount}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Exception accessing rootView -> rootVisualElement: {ex.Message}");
                        }
                    }
                }
                
                // Unity 6 fallback: Try rootSplitView -> rootVisualElement
                if (root == null)
                {
                    var rootSplitViewProp = containerWindowType.GetProperty("rootSplitView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rootSplitViewProp != null)
                    {
                        try
                        {
                            var rootSplitView = rootSplitViewProp.GetValue(containerWindow, null);
                            if (rootSplitView != null)
                            {
                                Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found rootSplitView (type: {rootSplitView.GetType().Name}), trying to get rootVisualElement from it...");
                                var splitViewType = rootSplitView.GetType();
                                var splitViewRootProp = splitViewType.GetProperty("rootVisualElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (splitViewRootProp != null)
                                {
                                    root = splitViewRootProp.GetValue(rootSplitView, null) as VisualElement;
                                    if (root != null)
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Successfully accessed rootVisualElement from rootSplitView! root.name: '{root.name}', root.childCount: {root.childCount}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Exception accessing rootSplitView -> rootVisualElement: {ex.Message}");
                        }
                    }
                }
                
                // Try field if property didn't work
                if (root == null)
                {
                    var rootVisualElementField = containerWindowType.GetField("rootVisualElement", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rootVisualElementField != null)
                    {
                        try
                        {
                            root = rootVisualElementField.GetValue(containerWindow) as VisualElement;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Exception accessing rootVisualElement field: {ex.Message}");
                        }
                    }
                }
                
                // Try alternative property names
                if (root == null)
                {
                    var mRootProp = containerWindowType.GetProperty("m_Root", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mRootProp != null)
                    {
                        try
                        {
                            root = mRootProp.GetValue(containerWindow, null) as VisualElement;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Exception accessing m_Root property: {ex.Message}");
                        }
                    }
                }
                
                if (root == null)
                {
                    Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Could not access root VisualElement from ContainerWindow (tried property, field, and m_Root). ContainerWindow type: {containerWindow?.GetType()?.Name ?? "NULL"}, actual type: {containerWindow?.GetType()?.FullName ?? "NULL"}");
                    continue;
                }
                
                Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Successfully accessed ContainerWindow root! root.name: '{root.name}', root.childCount: {root.childCount}");
                
                // Try Unity 6's official MainToolbar API first
                try
                {
                    var mainToolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbars.MainToolbar");
                    if (mainToolbarType != null)
                    {
                        Debug.Log("[CustomToolbarInjector] Unity 6 MainToolbar API found - but using direct injection for now (official API requires refactor)");
                    }
                }
                catch
                {
                    // MainToolbar API not available or different version
                }
                
                Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Checking ContainerWindow root: {root.name} (childCount: {root.childCount})");
                
                // Look for #overlay-toolbar__top first (the main toolbar container)
                var overlayToolbarTop = root.Q<VisualElement>("#overlay-toolbar__top");
                Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Direct Q() query for #overlay-toolbar__top: {(overlayToolbarTop != null ? "FOUND" : "NOT FOUND")}");
                
                // Also try searching recursively if direct query doesn't work
                if (overlayToolbarTop == null)
                {
                    Debug.Log("[CustomToolbarInjector] Strategy 1.5: #overlay-toolbar__top not found via Q(), trying recursive search...");
                    var allStrategy15Elements = new List<VisualElement>();
                    CollectChildren(root, allStrategy15Elements);
                    Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Collected {allStrategy15Elements.Count} elements recursively");
                    
                    // Log some element names to see what we're finding
                    var toolbarRelated = allStrategy15Elements.Where(e => 
                        e.name.Contains("toolbar") || e.name.Contains("overlay") || e.name.Contains("Toolbar")).Take(10).ToList();
                    if (toolbarRelated.Count > 0)
                    {
                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found {toolbarRelated.Count} toolbar-related elements (showing first 10): {string.Join(", ", toolbarRelated.Select(e => e.name))}");
                    }
                    
                    overlayToolbarTop = allStrategy15Elements.Find(e => 
                        e.name == "overlay-toolbar__top" || 
                        (e.name.Contains("overlay") && e.name.Contains("toolbar") && e.name.Contains("top")));
                    
                    if (overlayToolbarTop != null)
                    {
                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found #overlay-toolbar__top via recursive search: {overlayToolbarTop.name} (childCount: {overlayToolbarTop.childCount})");
                    }
                    else
                    {
                        Debug.Log("[CustomToolbarInjector] Strategy 1.5: #overlay-toolbar__top not found via recursive search either");
                    }
                }
                
                if (overlayToolbarTop != null)
                {
                    Debug.Log($"[CustomToolbarInjector] Found #overlay-toolbar__top (childCount: {overlayToolbarTop.childCount}), searching for toolbar zones NOT in UnsupportedUserElements...");
                    
                    // Find all toolbar zones and filter out those in UnsupportedUserElements
                    var allLeftZones = overlayToolbarTop.Query<VisualElement>(name: "ToolbarZoneLeftAlign").ToList();
                    var allRightZones = overlayToolbarTop.Query<VisualElement>(name: "ToolbarZoneRightAlign").ToList();
                    
                    Debug.Log($"[CustomToolbarInjector] Found {allLeftZones.Count} ToolbarZoneLeftAlign and {allRightZones.Count} ToolbarZoneRightAlign in #overlay-toolbar__top");
                    
                    VisualElement overlayLeftZone = null;
                    VisualElement overlayRightZone = null;
                    
                    // Find left zone NOT in UnsupportedUserElements
                    foreach (var leftZone in allLeftZones)
                    {
                        bool inUnsupported = IsInsideUnsupportedUserElements(leftZone);
                        Debug.Log($"[CustomToolbarInjector] ToolbarZoneLeftAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(leftZone)}");
                        if (!inUnsupported)
                        {
                            overlayLeftZone = leftZone;
                            Debug.Log($"[CustomToolbarInjector] SELECTED ToolbarZoneLeftAlign NOT in UnsupportedUserElements");
                            break;
                        }
                    }
                    
                    // Find right zone NOT in UnsupportedUserElements
                    foreach (var rightZone in allRightZones)
                    {
                        bool inUnsupported = IsInsideUnsupportedUserElements(rightZone);
                        Debug.Log($"[CustomToolbarInjector] ToolbarZoneRightAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(rightZone)}");
                        if (!inUnsupported)
                        {
                            overlayRightZone = rightZone;
                            Debug.Log($"[CustomToolbarInjector] SELECTED ToolbarZoneRightAlign NOT in UnsupportedUserElements");
                            break;
                        }
                    }
                    
                    if (overlayLeftZone != null && overlayRightZone != null)
                    {
                        Debug.Log("[CustomToolbarInjector] Installing into toolbar zones NOT in UnsupportedUserElements");
                        if (InstallIntoZones(overlayLeftZone, overlayRightZone))
                        {
                            installedAny = true;
                            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                            if (toolbarType != null)
                            {
                                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                                if (toolbars != null && toolbars.Length > 0)
                                {
                                    _currentToolbar = (ScriptableObject)toolbars[0];
                                }
                            }
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[CustomToolbarInjector] All toolbar zones are in UnsupportedUserElements (found {allLeftZones.Count} left, {allRightZones.Count} right zones). Trying ContainerSection approach...");
                        
                        // Try to find ContainerSection elements that are NOT in UnsupportedUserElements
                        var allContainerSections = overlayToolbarTop.Query<VisualElement>(className: "unity-overlay-container__spacing-container").ToList();
                        Debug.Log($"[CustomToolbarInjector] Found {allContainerSections.Count} ContainerSection elements in #overlay-toolbar__top");
                        
                        VisualElement leftContainerSection = null;
                        VisualElement rightContainerSection = null;
                        
                        foreach (var section in allContainerSections)
                        {
                            bool inUnsupported = IsInsideUnsupportedUserElements(section);
                            Debug.Log($"[CustomToolbarInjector] ContainerSection - inUnsupported: {inUnsupported}, name: {section.name}, parent: {GetParentChain(section)}");
                            
                            if (!inUnsupported)
                            {
                                // Use first non-unsupported section for left, last for right
                                if (leftContainerSection == null)
                                {
                                    leftContainerSection = section;
                                }
                                rightContainerSection = section; // Keep updating to get the last one
                            }
                        }
                        
                        if (leftContainerSection != null && rightContainerSection != null && leftContainerSection != rightContainerSection)
                        {
                            Debug.Log("[CustomToolbarInjector] Installing into ContainerSection elements NOT in UnsupportedUserElements");
                            if (InstallIntoZones(leftContainerSection, rightContainerSection))
                            {
                                installedAny = true;
                                var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                                if (toolbarType != null)
                                {
                                    var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                                    if (toolbars != null && toolbars.Length > 0)
                                    {
                                        _currentToolbar = (ScriptableObject)toolbars[0];
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log("[CustomToolbarInjector] #overlay-toolbar__top not found in this ContainerWindow");
                }
                
                // Look for DockArea - it has more space (this is the key - search from ContainerWindow root)
                var dockArea = root.Q<VisualElement>("#DockArea");
                if (dockArea != null && !installedAny)
                {
                    Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found #DockArea from ContainerWindow root (childCount: {dockArea.childCount}), checking for toolbar zones within...");
                    
                    // Look for toolbar elements within DockArea that aren't in UnsupportedElements
                    var dockLeftZones = dockArea.Query<VisualElement>(name: "ToolbarZoneLeftAlign").ToList();
                    var dockRightZones = dockArea.Query<VisualElement>(name: "ToolbarZoneRightAlign").ToList();
                    
                    Debug.Log($"[CustomToolbarInjector] Strategy 1.5: Found {dockLeftZones.Count} ToolbarZoneLeftAlign and {dockRightZones.Count} ToolbarZoneRightAlign in #DockArea");
                    
                    VisualElement bestDockLeftZone = null;
                    VisualElement bestDockRightZone = null;
                    
                    // Find zones in DockArea NOT in UnsupportedElements
                    foreach (var leftZone in dockLeftZones)
                    {
                        bool inUnsupported = IsInsideUnsupportedUserElements(leftZone);
                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: DockArea ToolbarZoneLeftAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(leftZone)}");
                        if (!inUnsupported)
                        {
                            bestDockLeftZone = leftZone;
                            Debug.Log("[CustomToolbarInjector] Strategy 1.5: SELECTED ToolbarZoneLeftAlign from #DockArea NOT in UnsupportedElements");
                            break;
                        }
                    }
                    
                    foreach (var rightZone in dockRightZones)
                    {
                        bool inUnsupported = IsInsideUnsupportedUserElements(rightZone);
                        Debug.Log($"[CustomToolbarInjector] Strategy 1.5: DockArea ToolbarZoneRightAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(rightZone)}");
                        if (!inUnsupported)
                        {
                            bestDockRightZone = rightZone;
                            Debug.Log("[CustomToolbarInjector] Strategy 1.5: SELECTED ToolbarZoneRightAlign from #DockArea NOT in UnsupportedElements");
                            break;
                        }
                    }
                    
                    if (bestDockLeftZone != null && bestDockRightZone != null)
                    {
                        Debug.Log("[CustomToolbarInjector] Strategy 1.5: Installing into #DockArea ToolbarZoneLeftAlign/RightAlign (BEST - more space!)");
                        if (InstallIntoZones(bestDockLeftZone, bestDockRightZone))
                        {
                            installedAny = true;
                            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                            if (toolbarType != null)
                            {
                                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                                if (toolbars != null && toolbars.Length > 0)
                                {
                                    _currentToolbar = (ScriptableObject)toolbars[0];
                                }
                            }
                            break;
                        }
                    }
                    else if (dockLeftZones.Count > 0 && dockRightZones.Count > 0)
                    {
                        // Fallback: use zones even if they're in UnsupportedElements (better than nothing)
                        Debug.LogWarning("[CustomToolbarInjector] Strategy 1.5: Using DockArea zones even though they're in UnsupportedElements (fallback)");
                        if (InstallIntoZones(dockLeftZones[0], dockRightZones[0]))
                        {
                            installedAny = true;
                            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                            if (toolbarType != null)
                            {
                                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                                if (toolbars != null && toolbars.Length > 0)
                                {
                                    _currentToolbar = (ScriptableObject)toolbars[0];
                                }
                            }
                            break;
                        }
                    }
                }
                
                // Also try to find toolbar elements that are NOT in UnsupportedElements
                Debug.Log($"[CustomToolbarInjector] Searching ContainerWindow root for toolbar zones (total elements to check: collecting...)");
                var allElements = new List<VisualElement>();
                CollectChildren(root, allElements);
                Debug.Log($"[CustomToolbarInjector] Collected {allElements.Count} elements from ContainerWindow root");
                
                // Find ToolbarZoneLeftAlign that's not inside UnsupportedElements
                VisualElement bestLeftZone = null;
                VisualElement bestRightZone = null;
                int leftZonesFound = 0;
                int rightZonesFound = 0;
                
                foreach (var elem in allElements)
                {
                    if (elem.name == "ToolbarZoneLeftAlign")
                    {
                        leftZonesFound++;
                        // Check if it's NOT inside UnsupportedElements
                        var parent = elem.parent;
                        bool inUnsupported = false;
                        string parentChain = "";
                        while (parent != null)
                        {
                            parentChain += parent.name + " -> ";
                            if (parent.name == "UnsupportedElements" || parent.name.Contains("Unsupported"))
                            {
                                inUnsupported = true;
                                break;
                            }
                            parent = parent.parent;
                        }
                        
                        Debug.Log($"[CustomToolbarInjector] ToolbarZoneLeftAlign #{leftZonesFound} - inUnsupported: {inUnsupported}, parent chain: {parentChain}");
                        
                        if (!inUnsupported && bestLeftZone == null)
                        {
                            bestLeftZone = elem;
                            Debug.Log($"[CustomToolbarInjector] SELECTED ToolbarZoneLeftAlign NOT in UnsupportedElements: {elem.name} (parent: {elem.parent?.name})");
                        }
                    }
                    else if (elem.name == "ToolbarZoneRightAlign")
                    {
                        rightZonesFound++;
                        var parent = elem.parent;
                        bool inUnsupported = false;
                        string parentChain = "";
                        while (parent != null)
                        {
                            parentChain += parent.name + " -> ";
                            if (parent.name == "UnsupportedElements" || parent.name.Contains("Unsupported"))
                            {
                                inUnsupported = true;
                                break;
                            }
                            parent = parent.parent;
                        }
                        
                        Debug.Log($"[CustomToolbarInjector] ToolbarZoneRightAlign #{rightZonesFound} - inUnsupported: {inUnsupported}, parent chain: {parentChain}");
                        
                        if (!inUnsupported && bestRightZone == null)
                        {
                            bestRightZone = elem;
                            Debug.Log($"[CustomToolbarInjector] SELECTED ToolbarZoneRightAlign NOT in UnsupportedElements: {elem.name} (parent: {elem.parent?.name})");
                        }
                    }
                }
                
                Debug.Log($"[CustomToolbarInjector] Search complete - found {leftZonesFound} left zones, {rightZonesFound} right zones. Selected: left={bestLeftZone != null}, right={bestRightZone != null}");
                
                if (bestLeftZone != null && bestRightZone != null)
                {
                    Debug.Log("[CustomToolbarInjector] Using ToolbarZoneLeftAlign/RightAlign NOT in UnsupportedElements (better space)");
                    if (InstallIntoZones(bestLeftZone, bestRightZone))
                    {
                        installedAny = true;
                        var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
                        if (toolbarType != null)
                        {
                            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                            if (toolbars != null && toolbars.Length > 0)
                            {
                                _currentToolbar = (ScriptableObject)toolbars[0];
                            }
                        }
                        break;
                    }
                }
                else if (leftZonesFound > 0 || rightZonesFound > 0)
                {
                    Debug.LogWarning($"[CustomToolbarInjector] Found toolbar zones but all are in UnsupportedElements (left: {leftZonesFound}, right: {rightZonesFound})");
                }
            }
        }
        
        // Fallback: Try toolbar-specific root (older Unity versions)
        if (!installedAny)
        {
            Debug.Log("[CustomToolbarInjector] Trying fallback strategies from Toolbar root...");
            var toolbarType = unityEditorAssembly.GetType("UnityEditor.Toolbar");
            if (toolbarType != null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                Debug.Log($"[CustomToolbarInjector] Found {toolbars?.Length ?? 0} Toolbar(s)");
                
                if (toolbars != null && toolbars.Length > 0)
                {
                    _currentToolbar = (ScriptableObject)toolbars[0];
                    VisualElement root = GetToolbarRoot(_currentToolbar, toolbarType, unityEditorAssembly);
                    
                    if (root != null)
                    {
                        Debug.Log($"[CustomToolbarInjector] Got Toolbar root, name: {root.name}, childCount: {root.childCount}");
                        
                        // First, try to find #overlay-toolbar__top from Toolbar root
                        Debug.Log("[CustomToolbarInjector] Searching for #overlay-toolbar__top from Toolbar root...");
                        var overlayToolbarTop = root.Q<VisualElement>("#overlay-toolbar__top");
                        Debug.Log($"[CustomToolbarInjector] Direct Q() query for #overlay-toolbar__top: {(overlayToolbarTop != null ? "FOUND" : "NOT FOUND")}");
                        
                        if (overlayToolbarTop == null)
                        {
                            // Try recursive search
                            Debug.Log("[CustomToolbarInjector] #overlay-toolbar__top not found via Q(), trying recursive search...");
                            var allToolbarElements = new List<VisualElement>();
                            CollectChildren(root, allToolbarElements);
                            Debug.Log($"[CustomToolbarInjector] Collected {allToolbarElements.Count} elements from Toolbar root recursively");
                            overlayToolbarTop = allToolbarElements.Find(e => 
                                e.name == "overlay-toolbar__top" || 
                                (e.name.Contains("overlay") && e.name.Contains("toolbar") && e.name.Contains("top")));
                            Debug.Log($"[CustomToolbarInjector] Recursive search for #overlay-toolbar__top: {(overlayToolbarTop != null ? "FOUND" : "NOT FOUND")}");
                        }
                        
                        if (overlayToolbarTop != null)
                        {
                            Debug.Log($"[CustomToolbarInjector] Found #overlay-toolbar__top from Toolbar root (childCount: {overlayToolbarTop.childCount}), searching for zones NOT in UnsupportedElements...");
                            
                            // Find all toolbar zones and filter out those in UnsupportedUserElements
                            var allLeftZones = overlayToolbarTop.Query<VisualElement>(name: "ToolbarZoneLeftAlign").ToList();
                            var allRightZones = overlayToolbarTop.Query<VisualElement>(name: "ToolbarZoneRightAlign").ToList();
                            
                            Debug.Log($"[CustomToolbarInjector] Found {allLeftZones.Count} ToolbarZoneLeftAlign and {allRightZones.Count} ToolbarZoneRightAlign in #overlay-toolbar__top");
                            
                            VisualElement bestLeftZone = null;
                            VisualElement bestRightZone = null;
                            
                            // Find left zone NOT in UnsupportedUserElements
                            foreach (var leftZone in allLeftZones)
                            {
                                bool inUnsupported = IsInsideUnsupportedUserElements(leftZone);
                                Debug.Log($"[CustomToolbarInjector] ToolbarZoneLeftAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(leftZone)}");
                                if (!inUnsupported)
                                {
                                    bestLeftZone = leftZone;
                                    Debug.Log("[CustomToolbarInjector] SELECTED ToolbarZoneLeftAlign NOT in UnsupportedUserElements");
                                    break;
                                }
                            }
                            
                            // Find right zone NOT in UnsupportedUserElements
                            foreach (var rightZone in allRightZones)
                            {
                                bool inUnsupported = IsInsideUnsupportedUserElements(rightZone);
                                Debug.Log($"[CustomToolbarInjector] ToolbarZoneRightAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(rightZone)}");
                                if (!inUnsupported)
                                {
                                    bestRightZone = rightZone;
                                    Debug.Log("[CustomToolbarInjector] SELECTED ToolbarZoneRightAlign NOT in UnsupportedUserElements");
                                    break;
                                }
                            }
                            
                            if (bestLeftZone != null && bestRightZone != null)
                            {
                                Debug.Log("[CustomToolbarInjector] Using Strategy 2a: ToolbarZoneLeftAlign/RightAlign from #overlay-toolbar__top NOT in UnsupportedElements");
                                installedAny = InstallIntoZones(bestLeftZone, bestRightZone);
                            }
                            else if (allLeftZones.Count > 0 && allRightZones.Count > 0)
                            {
                                Debug.LogWarning($"[CustomToolbarInjector] All toolbar zones in #overlay-toolbar__top are in UnsupportedElements (found {allLeftZones.Count} left, {allRightZones.Count} right zones). Trying ContainerSection approach...");
                                
                                // Try ContainerSection elements
                                var allContainerSections = overlayToolbarTop.Query<VisualElement>(className: "unity-overlay-container__spacing-container").ToList();
                                Debug.Log($"[CustomToolbarInjector] Found {allContainerSections.Count} ContainerSection elements in #overlay-toolbar__top");
                                
                                VisualElement leftContainerSection = null;
                                VisualElement rightContainerSection = null;
                                
                                foreach (var section in allContainerSections)
                                {
                                    bool inUnsupported = IsInsideUnsupportedUserElements(section);
                                    Debug.Log($"[CustomToolbarInjector] ContainerSection - inUnsupported: {inUnsupported}, name: {section.name}, parent: {GetParentChain(section)}");
                                    
                                    if (!inUnsupported)
                                    {
                                        if (leftContainerSection == null)
                                        {
                                            leftContainerSection = section;
                                        }
                                        rightContainerSection = section; // Keep updating to get the last one
                                    }
                                }
                                
                                if (leftContainerSection != null && rightContainerSection != null && leftContainerSection != rightContainerSection)
                                {
                                    Debug.Log("[CustomToolbarInjector] Using Strategy 2b: ContainerSection elements from #overlay-toolbar__top NOT in UnsupportedElements");
                                    installedAny = InstallIntoZones(leftContainerSection, rightContainerSection);
                                }
                            }
                        }
                        
                        // Strategy 2c/2d: Look for ToolbarZoneLeftAlign and ToolbarZoneRightAlign (check if they're in UnsupportedElements)
                        if (!installedAny)
                        {
                            Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: Searching entire Toolbar root for zones NOT in UnsupportedElements...");
                            var allLeftZones = root.Query<VisualElement>(name: "ToolbarZoneLeftAlign").ToList();
                            var allRightZones = root.Query<VisualElement>(name: "ToolbarZoneRightAlign").ToList();
                            
                            Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found {allLeftZones.Count} ToolbarZoneLeftAlign and {allRightZones.Count} ToolbarZoneRightAlign in Toolbar root");
                            
                            VisualElement bestLeftZone = null;
                            VisualElement bestRightZone = null;
                            
                            // First, try to find #DockArea - user said "parent of parent has sibling #DockArea"
                            // Parent chain: ToolbarZone -> ToolbarContainerContent -> (empty) -> rootVisualContainer2
                            // So "parent of parent" is the empty-named element, and #DockArea should be its sibling within rootVisualContainer2
                            VisualElement dockArea = null;
                            
                            if (allLeftZones.Count > 0)
                            {
                                var firstLeftZone = allLeftZones[0];
                                var parent = firstLeftZone?.parent; // ToolbarContainerContent
                                if (parent != null)
                                {
                                    var parentOfParent = parent.parent; // The empty-named element
                                    if (parentOfParent != null)
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found parent of parent '{parentOfParent.name}' (type: {parentOfParent.GetType().Name}) - looking for #DockArea as sibling within '{parentOfParent.parent?.name ?? "NULL"}'");
                                        
                                        var parentOfParentParent = parentOfParent.parent; // Should be rootVisualContainer2
                                        if (parentOfParentParent != null)
                                        {
                                            Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Parent of parent's parent is '{parentOfParentParent.name}' (childCount: {parentOfParentParent.childCount}) - checking for #DockArea as sibling of '{parentOfParent.name}'");
                                            
                                            // Look for #DockArea as a sibling of parentOfParent (check all children of parentOfParentParent)
                                            for (int i = 0; i < parentOfParentParent.childCount; i++)
                                            {
                                                var child = parentOfParentParent[i];
                                                Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Checking child {i}: '{child.name}' (type: {child.GetType().Name})");
                                                
                                                // Skip the parentOfParent itself
                                                if (child == parentOfParent) continue;
                                                
                                                // Check if this child is #DockArea or contains it
                                                if (child.name == "DockArea" || child.name.Contains("DockArea"))
                                                {
                                                    dockArea = child;
                                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found DockArea sibling: '{child.name}'");
                                                    break;
                                                }
                                                
                                                // Try to find #DockArea within this child
                                                var dockAreaById = child.Q<VisualElement>("#DockArea");
                                                if (dockAreaById != null)
                                                {
                                                    dockArea = dockAreaById;
                                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found #DockArea via Q() within child '{child.name}'");
                                                    break;
                                                }
                                                
                                                // Also check by type name
                                                if (child.GetType().Name.Contains("Dock") && (child.name == "" || child.name.Contains("Dock")))
                                                {
                                                    dockArea = child;
                                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found DockArea by type name: '{child.GetType().Name}'");
                                                    break;
                                                }
                                            }
                                            
                                            // Also try Q() query on parentOfParentParent directly
                                            if (dockArea == null)
                                            {
                                                dockArea = parentOfParentParent.Q<VisualElement>("#DockArea");
                                                if (dockArea != null)
                                                {
                                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found #DockArea via Q() query on parent of parent's parent (childCount: {dockArea.childCount})");
                                                }
                                            }
                                            
                                            // Also try searching recursively from parentOfParentParent
                                            if (dockArea == null)
                                            {
                                                var allElements = new List<VisualElement>();
                                                CollectChildren(parentOfParentParent, allElements);
                                                dockArea = allElements.Find(e => 
                                                    (e.name == "DockArea" || e.name.Contains("DockArea")) &&
                                                    e != parentOfParent); // Exclude the parentOfParent itself
                                                if (dockArea != null)
                                                {
                                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found DockArea via recursive search: '{dockArea.name}' (type: {dockArea.GetType().Name})");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Fallback: Search the entire Toolbar root recursively for #DockArea (maybe it's nested deeper)
                            if (dockArea == null)
                            {
                                Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: Fallback - searching entire Toolbar root recursively for #DockArea...");
                                var allRootElements = new List<VisualElement>();
                                CollectChildren(root, allRootElements);
                                Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Collected {allRootElements.Count} elements from Toolbar root recursively");
                                
                                // Debug: Log all element names/types that might be DockArea
                                var dockCandidates = allRootElements.Where(e => 
                                    e.name.Contains("Dock", StringComparison.OrdinalIgnoreCase) ||
                                    e.GetType().Name.Contains("Dock", StringComparison.OrdinalIgnoreCase)).ToList();
                                
                                if (dockCandidates.Count > 0)
                                {
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found {dockCandidates.Count} Dock-related candidates:");
                                    foreach (var candidate in dockCandidates.Take(10)) // Limit to first 10
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d:   - name: '{candidate.name}', type: {candidate.GetType().Name}, parent: {candidate.parent?.name ?? "NULL"}");
                                    }
                                }
                                
                                // Look for elements with DockArea in name or type
                                dockArea = allRootElements.Find(e => 
                                    e.name == "DockArea" || 
                                    e.name.Contains("DockArea") ||
                                    (e.name == "" && e.GetType().Name.Contains("Dock")));
                                
                                if (dockArea != null)
                                {
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found DockArea via recursive search from Toolbar root: '{dockArea.name}' (type: {dockArea.GetType().Name}, parent: {dockArea.parent?.name ?? "NULL"})");
                                }
                                else
                                {
                                    Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: #DockArea NOT found in Toolbar root hierarchy - it may be at ContainerWindow level (which we can't access)");
                                    
                                    // Also try Q() query on root
                                    dockArea = root.Q<VisualElement>("#DockArea");
                                    if (dockArea != null)
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found #DockArea via Q() query on Toolbar root (childCount: {dockArea.childCount})");
                                    }
                                }
                            }
                            
                            // If we found DockArea, search within it for toolbar zones
                            if (dockArea != null)
                            {
                                Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: Searching within #DockArea for toolbar zones...");
                                var dockLeftZones = dockArea.Query<VisualElement>(name: "ToolbarZoneLeftAlign").ToList();
                                var dockRightZones = dockArea.Query<VisualElement>(name: "ToolbarZoneRightAlign").ToList();
                                
                                Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: Found {dockLeftZones.Count} ToolbarZoneLeftAlign and {dockRightZones.Count} ToolbarZoneRightAlign in #DockArea");
                                
                                // Find zones in DockArea NOT in UnsupportedElements
                                foreach (var leftZone in dockLeftZones)
                                {
                                    bool inUnsupported = IsInsideUnsupportedUserElements(leftZone);
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: DockArea ToolbarZoneLeftAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(leftZone)}");
                                    if (!inUnsupported)
                                    {
                                        bestLeftZone = leftZone;
                                        Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: SELECTED ToolbarZoneLeftAlign from #DockArea NOT in UnsupportedElements");
                                        break;
                                    }
                                }
                                
                                foreach (var rightZone in dockRightZones)
                                {
                                    bool inUnsupported = IsInsideUnsupportedUserElements(rightZone);
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: DockArea ToolbarZoneRightAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(rightZone)}");
                                    if (!inUnsupported)
                                    {
                                        bestRightZone = rightZone;
                                        Debug.Log("[CustomToolbarInjector] Strategy 2c/2d: SELECTED ToolbarZoneRightAlign from #DockArea NOT in UnsupportedElements");
                                        break;
                                    }
                                }
                                
                                if (bestLeftZone != null && bestRightZone != null)
                                {
                                    Debug.Log("[CustomToolbarInjector] Using Strategy 2c: ToolbarZoneLeftAlign/RightAlign from #DockArea NOT in UnsupportedElements (BETTER SPACE)");
                                    installedAny = InstallIntoZones(bestLeftZone, bestRightZone);
                                }
                            }
                            
                            // Fallback: Find zones NOT in UnsupportedElements from the original search
                            if (!installedAny)
                            {
                                foreach (var leftZone in allLeftZones)
                                {
                                    bool inUnsupported = IsInsideUnsupportedUserElements(leftZone);
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: ToolbarZoneLeftAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(leftZone)}");
                                    if (!inUnsupported)
                                    {
                                        bestLeftZone = leftZone;
                                        Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: SELECTED ToolbarZoneLeftAlign NOT in UnsupportedElements");
                                        break;
                                    }
                                }
                                
                                foreach (var rightZone in allRightZones)
                                {
                                    bool inUnsupported = IsInsideUnsupportedUserElements(rightZone);
                                    Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: ToolbarZoneRightAlign - inUnsupported: {inUnsupported}, parent chain: {GetParentChain(rightZone)}");
                                    if (!inUnsupported)
                                    {
                                        bestRightZone = rightZone;
                                        Debug.Log($"[CustomToolbarInjector] Strategy 2c/2d: SELECTED ToolbarZoneRightAlign NOT in UnsupportedElements");
                                        break;
                                    }
                                }
                                
                                if (bestLeftZone != null && bestRightZone != null)
                                {
                                    Debug.Log("[CustomToolbarInjector] Using Strategy 2c: ToolbarZoneLeftAlign/RightAlign NOT in UnsupportedElements");
                                    installedAny = InstallIntoZones(bestLeftZone, bestRightZone);
                                }
                                else if (allLeftZones.Count > 0 && allRightZones.Count > 0)
                                {
                                    // Fallback: use zones even if they're in UnsupportedElements (better than nothing)
                                    Debug.LogWarning("[CustomToolbarInjector] Using Strategy 2d: ToolbarZoneLeftAlign/RightAlign (FALLBACK - all zones are in UnsupportedElements)");
                                    installedAny = InstallIntoZones(allLeftZones[0], allRightZones[0]);
                                }
                                else
                                {
                                    Debug.LogWarning("[CustomToolbarInjector] Strategy 2c/2d: No toolbar zones found at all in Toolbar root");
                                }
                            }
                        }
                        else
                        {
                            // Strategy 3: Look for MainToolbarOverlayContainer and find sections
                            var mainContainer = root.Q<VisualElement>("MainToolbarOverlayContainer");
                            if (mainContainer != null)
                            {
                                Debug.Log("[CustomToolbarInjector] Using Strategy 3: MainToolbarOverlayContainer");
                                installedAny = InstallIntoOverlayContainer(mainContainer);
                            }
                            else
                            {
                                // Strategy 4: Find #PlayMode and insert before/after
                                var playModeElement = root.Q<VisualElement>("#PlayMode");
                                if (playModeElement != null)
                                {
                                    Debug.Log("[CustomToolbarInjector] Using Strategy 4: #PlayMode");
                                    installedAny = InstallAroundPlayMode(playModeElement);
                                }
                                else
                                {
                                    // Strategy 5: Find any ContainerSection and try to insert
                                    var containerSections = root.Query<VisualElement>(className: "unity-overlay-container__spacing-container").ToList();
                                    if (containerSections.Count > 0)
                                    {
                                        Debug.Log($"[CustomToolbarInjector] Using Strategy 5: ContainerSection ({containerSections.Count} found)");
                                        installedAny = InstallIntoContainerSections(root, containerSections);
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[CustomToolbarInjector] No injection strategy worked - no suitable elements found");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[CustomToolbarInjector] Could not get Toolbar root");
                    }
                }
            }
        }
        
        if (installedAny)
        {
            Debug.Log("[CustomToolbarInjector] Successfully installed custom toolbar injector");
        }
        else
        {
            Debug.LogWarning("[CustomToolbarInjector] Failed to install - no suitable injection point found");
        }
        
        return installedAny;
    }
    
    private static VisualElement GetToolbarRoot(ScriptableObject toolbar, Type toolbarType, System.Reflection.Assembly unityEditorAssembly)
    {
        // Try Unity 2021.1+ path (m_Root field)
        var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rootField != null)
        {
            var root = rootField.GetValue(toolbar) as VisualElement;
            if (root != null)
            {
                return root;
            }
        }
        
        // Try Unity 2020.x path (GUIView.visualTree)
        var guiViewType = unityEditorAssembly.GetType("UnityEditor.GUIView");
        if (guiViewType != null)
        {
#if UNITY_2020_1_OR_NEWER
            var windowBackendProp = guiViewType.GetProperty("windowBackend",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (windowBackendProp != null)
            {
                var windowBackend = windowBackendProp.GetValue(toolbar);
                if (windowBackend != null)
                {
                    var iWindowBackendType = unityEditorAssembly.GetType("UnityEditor.IWindowBackend");
                    if (iWindowBackendType != null)
                    {
                        var visualTreeProp = iWindowBackendType.GetProperty("visualTree",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (visualTreeProp != null)
                        {
                            return visualTreeProp.GetValue(windowBackend, null) as VisualElement;
                        }
                    }
                }
            }
#else
            var visualTreeProp = guiViewType.GetProperty("visualTree",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (visualTreeProp != null)
            {
                return visualTreeProp.GetValue(toolbar, null) as VisualElement;
            }
#endif
        }
        
        return null;
    }
    
    private static bool InstallIntoOverlayToolbarTop(VisualElement overlayToolbarTop)
    {
        // Check if already installed
        if (overlayToolbarTop.Q<VisualElement>(CustomLeftContainerName) != null &&
            overlayToolbarTop.Q<VisualElement>(CustomRightContainerName) != null)
        {
            return true;
        }
        
        // Find the ContainerSection that holds the actual toolbar row
        // Look for ContainerSection elements within #overlay-toolbar__top
        var containerSections = new List<VisualElement>();
        FindContainerSections(overlayToolbarTop, containerSections);
        
        Debug.Log($"[CustomToolbarInjector] Found {containerSections.Count} ContainerSection(s) in #overlay-toolbar__top");
        
        if (containerSections.Count == 0)
        {
            // If no ContainerSection found, try to find elements with the spacing-container class
            containerSections = overlayToolbarTop.Query<VisualElement>(className: "unity-overlay-container__spacing-container").ToList();
            Debug.Log($"[CustomToolbarInjector] Found {containerSections.Count} spacing-container(s) in #overlay-toolbar__top");
        }
        
        if (containerSections.Count > 0)
        {
            // Find the ContainerSection that actually holds the toolbar row
            // Usually it's the one that contains the play buttons or has multiple children
            VisualElement toolbarRow = null;
            
            foreach (var section in containerSections)
            {
                // Look for a section that contains play mode buttons or has many children (the actual toolbar row)
                var playMode = section.Q<VisualElement>("#PlayMode");
                if (playMode != null)
                {
                    Debug.Log($"[CustomToolbarInjector] Found toolbar row with PlayMode (childCount: {section.childCount})");
                    toolbarRow = section;
                    break;
                }
                else if (section.childCount > 3)
                {
                    Debug.Log($"[CustomToolbarInjector] Found potential toolbar row (childCount: {section.childCount})");
                    if (toolbarRow == null)
                    {
                        toolbarRow = section;
                    }
                }
            }
            
            // If we didn't find a specific row, use the first/last sections for left/right
            if (toolbarRow == null && containerSections.Count >= 2)
            {
                Debug.Log("[CustomToolbarInjector] Installing into first and last ContainerSection");
                // Install into first section (left) and last section (right)
                InstallZone(containerSections[0], CustomLeftContainerName, LeftToolbarGUI);
                InstallZone(containerSections[containerSections.Count - 1], CustomRightContainerName, RightToolbarGUI);
                return true;
            }
            else if (toolbarRow != null)
            {
                Debug.Log("[CustomToolbarInjector] Installing into toolbar row ContainerSection");
                // Install into the toolbar row - left side before play buttons, right side after
                return InstallIntoToolbarRow(toolbarRow);
            }
        }
        
        // Fallback: install directly into overlay-toolbar__top
        Debug.Log("[CustomToolbarInjector] Fallback: installing directly into #overlay-toolbar__top");
        InstallZone(overlayToolbarTop, CustomLeftContainerName, LeftToolbarGUI);
        InstallZone(overlayToolbarTop, CustomRightContainerName, RightToolbarGUI);
        return true;
    }
    
    private static void FindContainerSections(VisualElement parent, List<VisualElement> result)
    {
        if (parent == null)
        {
            return;
        }
        
        // Check if this element is a ContainerSection (usually has specific classes or structure)
        var classes = parent.GetClasses();
        var classList = new List<string>(classes);
        
        if (classList.Contains("unity-overlay-container__spacing-container") ||
            parent.name.Contains("ContainerSection") ||
            parent.GetType().Name.Contains("ContainerSection"))
        {
            result.Add(parent);
        }
        
        // Recursively search children
        for (int i = 0; i < parent.childCount; i++)
        {
            FindContainerSections(parent[i], result);
        }
    }
    
    private static bool InstallIntoToolbarRow(VisualElement toolbarRow)
    {
        // Check if already installed
        if (toolbarRow.Q<VisualElement>(CustomLeftContainerName) != null &&
            toolbarRow.Q<VisualElement>(CustomRightContainerName) != null)
        {
            return true;
        }
        
        // Find PlayMode element to anchor around
        var playModeElement = toolbarRow.Q<VisualElement>("#PlayMode");
        if (playModeElement != null)
        {
            return InstallAroundPlayMode(playModeElement);
        }
        
        // If no PlayMode, install at start (left) and end (right) of row
        var leftContainer = CreateIMGUIContainer(CustomLeftContainerName, LeftToolbarGUI);
        toolbarRow.Insert(0, leftContainer);
        
        var rightContainer = CreateIMGUIContainer(CustomRightContainerName, RightToolbarGUI);
        toolbarRow.Add(rightContainer);
        
        return true;
    }
    
    private static bool InstallIntoZones(VisualElement leftZone, VisualElement rightZone)
    {
        Debug.Log($"[CustomToolbarInjector] InstallIntoZones - leftZone: {leftZone.name} (childCount: {leftZone.childCount}), rightZone: {rightZone.name} (childCount: {rightZone.childCount})");
        
        // Check if already installed
        var existingLeft = leftZone.Q<VisualElement>(CustomLeftContainerName);
        var existingRight = rightZone.Q<VisualElement>(CustomRightContainerName);
        
        if (existingLeft != null && existingRight != null)
        {
            Debug.Log($"[CustomToolbarInjector] Containers already installed. Left container: {existingLeft.name} (visible: {existingLeft.visible}, parent: {existingLeft.parent?.name}), Right container: {existingRight.name} (visible: {existingRight.visible}, parent: {existingRight.parent?.name})");
            
            // Check if handlers are registered
            Debug.Log($"[CustomToolbarInjector] Current handlers - Left: {LeftToolbarGUI.Count}, Right: {RightToolbarGUI.Count}");
            
            // Ensure both containers are visible
            if (!existingLeft.visible)
            {
                Debug.Log("[CustomToolbarInjector] Left container is not visible, setting visible = true");
                existingLeft.visible = true;
            }
            if (!existingRight.visible)
            {
                Debug.Log("[CustomToolbarInjector] Right container is not visible, setting visible = true");
                existingRight.visible = true;
            }
            
            // Force a repaint to see if they render
            existingLeft.MarkDirtyRepaint();
            existingRight.MarkDirtyRepaint();
            
            // Also ensure the parent zones are visible
            if (!leftZone.visible)
            {
                Debug.Log("[CustomToolbarInjector] Left zone is not visible, setting visible = true");
                leftZone.visible = true;
            }
            if (!rightZone.visible)
            {
                Debug.Log("[CustomToolbarInjector] Right zone is not visible, setting visible = true");
                rightZone.visible = true;
            }
            
            return true;
        }
        
        Debug.Log($"[CustomToolbarInjector] Installing containers - existingLeft: {existingLeft != null}, existingRight: {existingRight != null}");
        
        // Install left zone
        InstallZone(leftZone, CustomLeftContainerName, LeftToolbarGUI);
        
        // Install right zone
        InstallZone(rightZone, CustomRightContainerName, RightToolbarGUI);
        
        Debug.Log($"[CustomToolbarInjector] InstallIntoZones complete - leftZone childCount: {leftZone.childCount}, rightZone childCount: {rightZone.childCount}");
        
        return true;
    }
    
    private static bool InstallIntoOverlayContainer(VisualElement mainContainer)
    {
        // Find left and right sections within the overlay container
        // Look for ContainerSection elements or spacing containers
        var allChildren = new List<VisualElement>();
        CollectChildren(mainContainer, allChildren);
        
        // Try to find left and right sections by position or class
        VisualElement leftSection = null;
        VisualElement rightSection = null;
        
        foreach (var child in allChildren)
        {
            var classes = child.GetClasses();
            var classList = new List<string>(classes);
            
            // Look for left-aligned sections
            if (classList.Contains("unity-overlay-container__spacing-container") || 
                child.name.Contains("Left") || 
                child.name.Contains("left"))
            {
                if (leftSection == null)
                {
                    leftSection = child;
                }
            }
            
            // Look for right-aligned sections
            if (classList.Contains("unity-overlay-container__spacing-container") || 
                child.name.Contains("Right") || 
                child.name.Contains("right"))
            {
                if (rightSection == null)
                {
                    rightSection = child;
                }
            }
        }
        
        // If we found sections, install into them
        if (leftSection != null && rightSection != null)
        {
            InstallZone(leftSection, CustomLeftContainerName, LeftToolbarGUI);
            InstallZone(rightSection, CustomRightContainerName, RightToolbarGUI);
            return true;
        }
        
        // Fallback: install into the main container itself
        InstallZone(mainContainer, CustomLeftContainerName, LeftToolbarGUI);
        InstallZone(mainContainer, CustomRightContainerName, RightToolbarGUI);
        return true;
    }
    
    private static bool InstallAroundPlayMode(VisualElement playModeElement)
    {
        var parent = playModeElement.parent;
        if (parent == null)
        {
            return false;
        }
        
        // Check if already installed
        if (parent.Q<VisualElement>(CustomLeftContainerName) != null &&
            parent.Q<VisualElement>(CustomRightContainerName) != null)
        {
            return true;
        }
        
        var playModeIndex = parent.IndexOf(playModeElement);
        
        // Install left container before PlayMode
        var leftContainer = CreateIMGUIContainer(CustomLeftContainerName, LeftToolbarGUI);
        parent.Insert(playModeIndex, leftContainer);
        
        // Install right container after PlayMode
        var rightContainer = CreateIMGUIContainer(CustomRightContainerName, RightToolbarGUI);
        parent.Insert(playModeIndex + 2, rightContainer);
        
        return true;
    }
    
    private static bool InstallIntoContainerSections(VisualElement root, List<VisualElement> containerSections)
    {
        if (containerSections.Count < 2)
        {
            return false;
        }
        
        // Use first section for left, last section for right
        var leftSection = containerSections[0];
        var rightSection = containerSections[containerSections.Count - 1];
        
        InstallZone(leftSection, CustomLeftContainerName, LeftToolbarGUI);
        InstallZone(rightSection, CustomRightContainerName, RightToolbarGUI);
        
        return true;
    }
    
    private static void CollectChildren(VisualElement element, List<VisualElement> result)
    {
        if (element == null)
        {
            return;
        }
        
        result.Add(element);
        
        for (int i = 0; i < element.childCount; i++)
        {
            CollectChildren(element[i], result);
        }
    }
    
    private static bool IsInsideUnsupportedUserElements(VisualElement element)
    {
        if (element == null) return false;
        
        var parent = element.parent;
        while (parent != null)
        {
            if (parent.name == "UnsupportedUserElements" || parent.name == "UnsupportedElements" || parent.name.Contains("Unsupported"))
            {
                return true;
            }
            parent = parent.parent;
        }
        return false;
    }
    
    private static string GetParentChain(VisualElement element)
    {
        if (element == null) return "";
        
        var chain = new List<string>();
        var parent = element.parent;
        while (parent != null && chain.Count < 5) // Limit to 5 levels
        {
            chain.Add(parent.name);
            parent = parent.parent;
        }
        return string.Join(" -> ", chain);
    }
    
    private static void InstallZone(VisualElement zone, string containerName, List<Action> handlers)
    {
        if (zone == null)
        {
            Debug.LogError($"[CustomToolbarInjector] Cannot install {containerName} - zone is null");
            return;
        }
        
        // Check if already installed
        var existing = zone.Q<VisualElement>(containerName);
        if (existing != null)
        {
            Debug.Log($"[CustomToolbarInjector] Container {containerName} already installed in zone {zone.name} (found existing: {existing.name})");
            return;
        }
        
        Debug.Log($"[CustomToolbarInjector] Installing {containerName} into zone: {zone.name} (type: {zone.GetType().Name}, childCount: {zone.childCount}, handlers: {handlers?.Count ?? 0})");
        
        try
        {
            var container = CreateIMGUIContainer(containerName, handlers);
            zone.Add(container);
            Debug.Log($"[CustomToolbarInjector] Successfully added {containerName} to zone (new childCount: {zone.childCount}, container type: {container.GetType().Name})");
            
            // Verify it was actually added by checking childCount and direct access
            // Note: Q() might not find it immediately due to Unity's internal structure
            // But if childCount increased and OnGUI is called, it's working
            var verify = zone.Q<VisualElement>(containerName);
            if (verify == null)
            {
                // Try direct access via children
                VisualElement found = null;
                for (int i = 0; i < zone.childCount; i++)
                {
                    var child = zone[i];
                    if (child != null && child.name == containerName)
                    {
                        found = child;
                        break;
                    }
                }
                
                if (found != null)
                {
                    Debug.Log($"[CustomToolbarInjector] Container {containerName} found via direct child access (Q() didn't work, but it's there)");
                }
                else
                {
                    Debug.LogWarning($"[CustomToolbarInjector] Container {containerName} not found via Q() or direct access, but childCount increased - may be a timing issue");
                }
            }
            else
            {
                Debug.Log($"[CustomToolbarInjector] Verified container {containerName} exists in zone via Q()");
            }
            
            // Check if Unity moved it to UnsupportedElements (Unity 6 does this automatically)
            // Use a delayed callback to check after Unity processes the addition
            EditorApplication.delayCall += () =>
            {
                var container = zone.Q<VisualElement>(containerName);
                if (container == null)
                {
                    // Try to find it in UnsupportedElements
                    var unsupportedElements = zone.Query<VisualElement>(name: "UnsupportedUserElements").ToList();
                    if (unsupportedElements.Count == 0)
                    {
                        // Try searching from root
                        var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
                        if (toolbarType != null)
                        {
                            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                            if (toolbars != null && toolbars.Length > 0)
                            {
                                var toolbar = toolbars[0];
                                var rootProp = toolbarType.GetProperty("rootVisualElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (rootProp != null)
                                {
                                    var root = rootProp.GetValue(toolbar) as VisualElement;
                                    if (root != null)
                                    {
                                        unsupportedElements = root.Query<VisualElement>(name: "UnsupportedUserElements").ToList();
                                    }
                                }
                            }
                        }
                    }
                    
                    foreach (var unsupported in unsupportedElements)
                    {
                        container = unsupported.Q<VisualElement>(containerName);
                        if (container != null)
                        {
                            Debug.LogWarning($"[CustomToolbarInjector] WARNING: Container {containerName} was moved to UnsupportedUserElements by Unity 6. This is expected behavior when using unsupported methods. Parent chain: {GetParentChain(container)}");
                            break;
                        }
                    }
                }
                else
                {
                    // Check if container itself is in UnsupportedElements
                    if (IsInsideUnsupportedUserElements(container))
                    {
                        Debug.LogWarning($"[CustomToolbarInjector] WARNING: Container {containerName} is inside UnsupportedUserElements. Parent chain: {GetParentChain(container)}");
                    }
                    else
                    {
                        Debug.Log($"[CustomToolbarInjector] Container {containerName} is NOT in UnsupportedUserElements (good!)");
                    }
                }
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomToolbarInjector] Exception installing {containerName}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private static VisualElement CreateIMGUIContainer(string containerName, List<Action> handlers)
    {
        // Create container
        var container = new VisualElement
        {
            name = containerName,
            style =
            {
                flexGrow = 1,
                flexDirection = FlexDirection.Row,
                minWidth = 50, // Ensure minimum width
                minHeight = 20 // Ensure minimum height
            }
        };
        
        // Ensure container is visible
        container.visible = true;
        
        // Create IMGUIContainer for OnGUI callbacks
        var callCount = 0;
        var imguiContainer = new IMGUIContainer(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                Debug.Log($"[CustomToolbarInjector] IMGUIContainer {containerName} OnGUI called for first time (handlers: {handlers?.Count ?? 0})");
            }
            
            if (handlers != null && handlers.Count > 0)
            {
                GUILayout.BeginHorizontal();
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CustomToolbarInjector] Error in toolbar handler: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // Draw something to verify the container is rendering
                GUILayout.Label($"[{containerName}]", EditorStyles.miniLabel);
            }
        })
        {
            style = { 
                flexGrow = 1,
                minWidth = 100, // Ensure minimum width so it's visible
                minHeight = 20
            }
        };
        
        // Log when container is created
        Debug.Log($"[CustomToolbarInjector] Created IMGUIContainer for {containerName} with {handlers?.Count ?? 0} handler(s)");
        
        container.Add(imguiContainer);
        return container;
    }
    
    [MenuItem("Tools/Custom Toolbar/Force Reinstall")]
    private static void ForceReinstall()
    {
        _installed = false;
        _currentToolbar = null;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
        Debug.Log("[CustomToolbarInjector] Forcing reinstall...");
    }
    
    [MenuItem("Tools/Custom Toolbar/Test Installation")]
    private static void TestInstallation()
    {
        var installed = TryInstall();
        Debug.Log($"[CustomToolbarInjector] Installation test: {(installed ? "SUCCESS" : "FAILED")}");
    }
}
}
