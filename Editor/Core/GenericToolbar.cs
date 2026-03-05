using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EditorToolbar
{
    [InitializeOnLoad]
    public static class GenericToolbar
    {
        private static readonly List<SectionInfo> _sections;
        private static bool prefsLoaded;
        private static int _selectedSection;
        private static List<SectionInfo> _cachedVisibleSections; // Cache visible sections to avoid recalculating every frame
        private static int _lastCachedSectionCount = -1; // Track when to invalidate cache

        // Cached workspace provider methods (discovered via reflection)
        private static readonly List<WorkspaceProviderInfo> _workspaceProviders = new List<WorkspaceProviderInfo>();

        // Cached framework detector methods (discovered via reflection)
        private static readonly List<FrameworkDetectorInfo> _frameworkDetectors = new List<FrameworkDetectorInfo>();

        /// <summary>
        /// Information about a discovered workspace provider.
        /// </summary>
        private class WorkspaceProviderInfo
        {
            public Type Type;
            public int Priority;
            public MethodInfo DrawWorkspaceUI;
            public MethodInfo DrawRuntimeStatus;
            public MethodInfo ShowMainToolbarLeftMenu;
            public MethodInfo DrawSceneSelector;
            public MethodInfo DrawProfileStatus;
        }

        /// <summary>
        /// Information about a discovered framework detector.
        /// </summary>
        private class FrameworkDetectorInfo
        {
            public Type Type;
            public int Priority;
            public MethodInfo DetectFramework;
        }

        static GenericToolbar()
        {
            // Skip toolbar initialization in batch mode (no graphics device available)
            if (Application.isBatchMode)
            {
                _sections = new List<SectionInfo>();
                return;
            }

            List<SectionInfo> sections = null;
            try
            {
                // Discover extensibility points via reflection
                LoadFrameworkDetectors();
                LoadWorkspaceProviders();

                sections = LoadSections();

                // Register handlers with toolbar systems
                // ToolbarExtenderIntegration (Paps plugin) will use these handlers if available,
                // otherwise CustomToolbarInjector will handle them directly
                Debug.Log("[Toolbar] Registering toolbar handlers");

                // Register with Paps plugin integration (if available)
                ToolbarExtenderIntegration.LeftToolbarGUI.Add(OnToolbarGUI);
                ToolbarExtenderIntegration.RightToolbarGUI.Add(DrawSectionGUI);

                // Also register with CustomToolbarInjector as fallback
                CustomToolbarInjector.LeftToolbarGUI.Add(OnToolbarGUI);
                CustomToolbarInjector.RightToolbarGUI.Add(DrawSectionGUI);

                Init();
                Debug.Log($"[Toolbar] Initialized successfully with {sections?.Count ?? 0} sections, {_workspaceProviders.Count} workspace provider(s), {_frameworkDetectors.Count} framework detector(s).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Toolbar] Failed to initialize toolbar: {ex.Message}\n{ex.StackTrace}");
                sections = new List<SectionInfo>();
            }
            finally
            {
                // Ensure _sections is always assigned, even if initialization fails
                _sections = sections ?? new List<SectionInfo>();
            }
        }

        /// <summary>
        /// Discovers classes marked with [ToolbarWorkspaceProvider] and caches their methods.
        /// </summary>
        private static void LoadWorkspaceProviders()
        {
            _workspaceProviders.Clear();

            foreach (var type in TypeCache.GetTypesWithAttribute<ToolbarWorkspaceProviderAttribute>())
            {
                try
                {
                    var attr = type.GetCustomAttribute<ToolbarWorkspaceProviderAttribute>();
                    if (attr == null) continue;

                    var providerInfo = new WorkspaceProviderInfo
                    {
                        Type = type,
                        Priority = attr.Priority,
                        DrawWorkspaceUI = type.GetMethod("DrawWorkspaceUI", BindingFlags.Public | BindingFlags.Static),
                        DrawRuntimeStatus = type.GetMethod("DrawRuntimeStatus", BindingFlags.Public | BindingFlags.Static),
                        ShowMainToolbarLeftMenu = type.GetMethod("ShowMainToolbarLeftMenu", BindingFlags.Public | BindingFlags.Static),
                        DrawSceneSelector = type.GetMethod("DrawSceneSelector", BindingFlags.Public | BindingFlags.Static),
                        DrawProfileStatus = type.GetMethod("DrawProfileStatus", BindingFlags.Public | BindingFlags.Static)
                    };

                    // Must have at least DrawWorkspaceUI to be useful
                    if (providerInfo.DrawWorkspaceUI != null)
                    {
                        _workspaceProviders.Add(providerInfo);
                        Debug.Log($"[Toolbar] Found workspace provider: {type.Name} (Priority: {attr.Priority})");
                    }
                    else
                    {
                        Debug.LogWarning($"[Toolbar] Workspace provider {type.Name} missing DrawWorkspaceUI method, skipping");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Failed to load workspace provider {type.Name}: {ex.Message}");
                }
            }

            // Sort by priority (higher first)
            _workspaceProviders.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Invokes DrawWorkspaceUI on all discovered workspace providers.
        /// </summary>
        private static void InvokeWorkspaceUI()
        {
            foreach (var provider in _workspaceProviders)
            {
                try
                {
                    provider.DrawWorkspaceUI?.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {provider.Type.Name}.DrawWorkspaceUI: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Invokes DrawRuntimeStatus on all discovered workspace providers.
        /// </summary>
        private static void InvokeRuntimeStatus()
        {
            foreach (var provider in _workspaceProviders)
            {
                if (provider.DrawRuntimeStatus == null) continue;
                try
                {
                    provider.DrawRuntimeStatus.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {provider.Type.Name}.DrawRuntimeStatus: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Invokes ShowMainToolbarLeftMenu on the first workspace provider that has it.
        /// </summary>
        private static void InvokeMainToolbarLeftMenu()
        {
            foreach (var provider in _workspaceProviders)
            {
                if (provider.ShowMainToolbarLeftMenu == null) continue;
                try
                {
                    provider.ShowMainToolbarLeftMenu.Invoke(null, null);
                    return; // Only invoke first available
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {provider.Type.Name}.ShowMainToolbarLeftMenu: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Invokes DrawSceneSelector on the first workspace provider that has it.
        /// </summary>
        private static void InvokeSceneSelector()
        {
            foreach (var provider in _workspaceProviders)
            {
                if (provider.DrawSceneSelector == null) continue;
                try
                {
                    provider.DrawSceneSelector.Invoke(null, null);
                    return; // Only invoke first available
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {provider.Type.Name}.DrawSceneSelector: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Invokes DrawProfileStatus on the first workspace provider that has it.
        /// </summary>
        private static void InvokeProfileStatus()
        {
            foreach (var provider in _workspaceProviders)
            {
                if (provider.DrawProfileStatus == null) continue;
                try
                {
                    provider.DrawProfileStatus.Invoke(null, null);
                    return; // Only invoke first available
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {provider.Type.Name}.DrawProfileStatus: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Discovers classes marked with [FrameworkDetector] and caches their methods.
        /// </summary>
        private static void LoadFrameworkDetectors()
        {
            _frameworkDetectors.Clear();

            foreach (var type in TypeCache.GetTypesWithAttribute<FrameworkDetectorAttribute>())
            {
                try
                {
                    var attr = type.GetCustomAttribute<FrameworkDetectorAttribute>();
                    if (attr == null) continue;

                    var detectMethod = type.GetMethod("DetectFramework", BindingFlags.Public | BindingFlags.Static);
                    if (detectMethod == null)
                    {
                        Debug.LogWarning($"[Toolbar] Framework detector {type.Name} missing DetectFramework method, skipping");
                        continue;
                    }

                    // Verify method signature: (Type) -> (string, string)
                    var parameters = detectMethod.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Type))
                    {
                        Debug.LogWarning($"[Toolbar] Framework detector {type.Name}.DetectFramework has wrong signature, expected (Type), skipping");
                        continue;
                    }

                    _frameworkDetectors.Add(new FrameworkDetectorInfo
                    {
                        Type = type,
                        Priority = attr.Priority,
                        DetectFramework = detectMethod
                    });

                    Debug.Log($"[Toolbar] Found framework detector: {type.Name} (Priority: {attr.Priority})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Failed to load framework detector {type.Name}: {ex.Message}");
                }
            }

            // Sort by priority (higher first)
            _frameworkDetectors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Detects framework and module for a type using discovered framework detectors.
        /// Detectors are tried in priority order until one returns a non-empty framework.
        /// </summary>
        private static (string Framework, string Module) DetectFrameworkForType(Type type)
        {
            foreach (var detector in _frameworkDetectors)
            {
                try
                {
                    var result = detector.DetectFramework.Invoke(null, new object[] { type });
                    if (result is ValueTuple<string, string> tuple)
                    {
                        if (!string.IsNullOrEmpty(tuple.Item1))
                        {
                            return tuple;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Toolbar] Error in {detector.Type.Name}.DetectFramework: {ex.Message}");
                }
            }

            // No detector returned a result, use default
            return ("shared", "");
        }

        public class SectionInfo
        {
            public string Name;
            public string Framework;
            public string Module;
            public string DisplayName
            {
                get
                {
                    if (string.IsNullOrEmpty(Module))
                        return $"{Framework} - {Name}";
                    return $"{Framework} {Module} - {Name}";
                }
            }
            public IEditorToolbar Instance;
        }

        private static List<SectionInfo> LoadSections()
        {
            var infos = new List<SectionInfo>();
            foreach (var type in TypeCache.GetTypesWithAttribute<ToolbarSectionAttribute>())
            {
                if (typeof(IEditorToolbar).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        var attr = type.GetCustomAttribute<ToolbarSectionAttribute>();
                        if (attr == null)
                        {
                            Debug.LogWarning($"[Toolbar] ToolbarSectionAttribute not found on type {type.Name}, skipping");
                            continue;
                        }

                        var instance = Activator.CreateInstance(type) as IEditorToolbar;
                        if (instance == null)
                        {
                            Debug.LogWarning($"[Toolbar] Failed to create instance of {type.Name}, skipping");
                            continue;
                        }

                        var (framework, module) = DetectFrameworkForType(type);
                        infos.Add(new SectionInfo { Name = attr.Name, Framework = framework, Module = module, Instance = instance });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Toolbar] Failed to load toolbar section {type.Name}: {ex.Message}\n{ex.StackTrace}");
                        // Continue loading other sections even if one fails
                    }
                }
            }
            // Sort sections by framework, then module, then name for consistent ordering
            return infos.OrderBy(s => s.Framework).ThenBy(s => s.Module).ThenBy(s => s.Name).ToList();
        }

        private static void DrawSectionGUI()
        {
            if (_selectedSection >= 0 && _selectedSection < _sections.Count)
            {
                var section = _sections[_selectedSection];
                if (section != null && section.Instance != null)
                {
                    try
                    {
                        section.Instance.OnGUI();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Toolbar] Error in section '{section.DisplayName}': {ex.Message}\n{ex.StackTrace}");
                        // Show error indicator in toolbar
                        GUILayout.Label($"Error in {section.Name}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        static void OnToolbarGUI()
        {
            try
            {
                GUILayout.FlexibleSpace();

                // Workspace UI (profile, scene, hub, submodules) - discovered via reflection
                InvokeWorkspaceUI();

                // Section Selection
                GUILayout.Label("Mode");

                // Cache visible sections to avoid recalculating every frame
                if (_cachedVisibleSections == null || _lastCachedSectionCount != _sections.Count)
                {
                    _cachedVisibleSections = _sections.Where(s => s != null && s.Instance != null && s.Instance.ShouldShow()).ToList();
                    _lastCachedSectionCount = _sections.Count;
                }

                if (_cachedVisibleSections.Count > 0)
                {
                    var currentSectionName = _selectedSection >= 0 && _selectedSection < _sections.Count
                        ? _sections[_selectedSection].DisplayName
                        : "Select Mode";

                    if (GUILayout.Button(new GUIContent(currentSectionName, "Select Mode")))
                    {
                        ShowModeSelectionMenu(_sections);
                    }
                }

                // Runtime Status (can be extended by submodules) - discovered via reflection
                if (EditorApplication.isPlaying)
                {
                    InvokeRuntimeStatus();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Toolbar] Error in OnToolbarGUI: {ex.Message}\n{ex.StackTrace}");
                // Show minimal error indicator
                GUILayout.Label("Toolbar Error", EditorStyles.miniLabel);
            }
        }

        static void Init()
        {
            if (!prefsLoaded)
            {
                _selectedSection = EditorPrefs.GetInt("IntPreferenceKeyToolbarSelection", 0);
                prefsLoaded = true;
            }
        }

        /// <summary>
        /// Shows a combined profile and scene selection menu, suitable for invoking
        /// from the Unity 6.3+ MainToolbar integration (left toolbar button).
        /// Invokes the first discovered workspace provider's ShowMainToolbarLeftMenu method.
        /// </summary>
        public static void ShowMainToolbarLeftMenu()
        {
            InvokeMainToolbarLeftMenu();
        }

        /// <summary>
        /// Draws the scene selector UI.
        /// Invokes the first discovered workspace provider's DrawSceneSelector method.
        /// </summary>
        public static void DrawSceneSelector()
        {
            InvokeSceneSelector();
        }

        /// <summary>
        /// Draws the profile status UI.
        /// Invokes the first discovered workspace provider's DrawProfileStatus method.
        /// </summary>
        public static void DrawProfileStatus()
        {
            InvokeProfileStatus();
        }

        private static void ShowModeSelectionMenu(List<SectionInfo> allSections)
        {
            var menu = new GenericMenu();

            for (int i = 0; i < allSections.Count; i++)
            {
                var section = allSections[i];

                // Skip sections that shouldn't show
                if (!section.Instance.ShouldShow())
                {
                    continue;
                }

                // Find the original index in _sections
                int originalIndex = _sections.FindIndex(s => s == section);
                if (originalIndex < 0) continue;

                var isSelected = originalIndex == _selectedSection;
                var sectionIndex = originalIndex; // Capture for closure

                menu.AddItem(new GUIContent(section.DisplayName), isSelected, () =>
                {
                    _selectedSection = sectionIndex;
                    EditorPrefs.SetInt("IntPreferenceKeyToolbarSelection", sectionIndex);
                    // Trigger repaint for status bar
                    EditorApplication.delayCall += () => RepaintAllEditorWindows();
                });
            }

            menu.ShowAsContext();
        }

        // Public API for status bar integration
        public static void ShowModeSelectionMenuPublic()
        {
            if (_sections == null || _sections.Count == 0) return;
            ShowModeSelectionMenu(_sections);
        }

        public static string GetCurrentModeDisplayName()
        {
            if (_sections == null || _sections.Count == 0) return "No Mode";
            if (_selectedSection >= 0 && _selectedSection < _sections.Count)
            {
                return _sections[_selectedSection].DisplayName;
            }
            return "Select Mode";
        }

        public static bool HasVisibleSections()
        {
            if (_sections == null || _sections.Count == 0) return false;
            return _sections.Any(s => s.Instance.ShouldShow());
        }

        public static void SetSelectedSection(int sectionIndex)
        {
            if (_sections == null || sectionIndex < 0 || sectionIndex >= _sections.Count) return;

            _selectedSection = sectionIndex;
            EditorPrefs.SetInt("IntPreferenceKeyToolbarSelection", sectionIndex);
            EditorApplication.delayCall += () => RepaintAllEditorWindows();
        }

        public static List<SectionInfo> GetAllSections()
        {
            return _sections ?? new List<SectionInfo>();
        }

        private static void RepaintAllEditorWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows)
            {
                if (window != null)
                {
                    window.Repaint();
                }
            }
        }
    }
}
