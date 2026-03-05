using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

namespace EditorToolbar
{
    [ToolbarSectionAttribute("Layout")]
    public class LayoutToolbar : IEditorToolbar
    {
        private class LayoutInfo
        {
            public string FilePath;
            public string FileName;
            public string SourceLabel; // e.g., "Shared" or a submodule name
            public string FullPath;
        }

        [Serializable]
        private class ToolbarStates
        {
            public bool includeAppStatusLeft = true;
            public bool includeAppStatusRight = true;
            public bool includeHeaderToolbar = true;
            public int appStatusLeftSection = -1;
            public int appStatusRightSection = -1;
            public int headerToolbarSection = -1;
        }

        [Serializable]
        private class RecentLayoutInfo
        {
            public string filePath;
            public string displayName;
        }

        [Serializable]
        private class RecentLayoutsData
        {
            public List<RecentLayoutInfo> recentLayouts = new List<RecentLayoutInfo>();
        }

        private const string SelectedSectionPrefKeyAppStatusBar = "CustomAppStatusBar.SelectedSection";
        private const string SelectedRightSectionPrefKeyAppStatusBar = "CustomAppStatusBar.SelectedRightSection";
        private const string HeaderToolbarSelectionKey = "IntPreferenceKeyToolbarSelection";
        private const string RecentLayoutsPrefKey = "LayoutToolbar.RecentLayouts";
        private const int MaxRecentLayouts = 5;

        private static MethodInfo _saveWindowLayoutMethod;
        private static bool _saveMethodInitialized = false;

        public bool ShouldShow()
        {
            // Show if any Layouts folder exists (shared or in submodules)
            if (Directory.Exists(Path.Combine(Application.dataPath, "Layouts")))
                return true;

            return HasAnySubmoduleLayouts();
        }

        private bool HasAnySubmoduleLayouts()
        {
            foreach (var submoduleBase in FindAllSubmoduleRoots())
            {
                var submodules = Directory.GetDirectories(submoduleBase);
                foreach (var submodule in submodules)
                {
                    var layoutsPath = Path.Combine(submodule, "Layouts");
                    if (Directory.Exists(layoutsPath))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds all directories named "Submodules" under the Assets folder.
        /// This works for any project structure (e.g., _Game/Submodules/, _Engine/Submodules/,
        /// MyProject/Submodules/, etc.) without hardcoding specific path prefixes.
        /// </summary>
        private static List<string> FindAllSubmoduleRoots()
        {
            var results = new List<string>();
            try
            {
                FindSubmoduleDirectories(Application.dataPath, results, maxDepth: 3, currentDepth: 0);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Error scanning for Submodules directories: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Recursively searches for directories named "Submodules" up to a limited depth
        /// to avoid scanning the entire project tree.
        /// </summary>
        private static void FindSubmoduleDirectories(string directory, List<string> results, int maxDepth, int currentDepth)
        {
            if (currentDepth > maxDepth)
                return;

            try
            {
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    if (Path.GetFileName(subDir).Equals("Submodules", System.StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(subDir);
                    }
                    else
                    {
                        FindSubmoduleDirectories(subDir, results, maxDepth, currentDepth + 1);
                    }
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                // Skip directories we cannot access
            }
        }

        private List<LayoutInfo> CollectAllLayouts()
        {
            var layouts = new List<LayoutInfo>();
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            // Add shared layouts
            var sharedLayoutsPath = Path.Combine(Application.dataPath, "Layouts");
            if (Directory.Exists(sharedLayoutsPath))
            {
                var files = Directory.GetFiles(sharedLayoutsPath, "*.wlt", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = GetRelativePath(file, projectRoot);
                    layouts.Add(new LayoutInfo
                    {
                        FilePath = relativePath,
                        FileName = Path.GetFileNameWithoutExtension(file),
                        SourceLabel = "Shared",
                        FullPath = file
                    });
                }
            }

            // Scan all Submodules directories found under Assets
            foreach (var submoduleBase in FindAllSubmoduleRoots())
            {
                // Derive a label from the parent of the Submodules directory
                // e.g., for "Assets/_Game/Submodules" the parent label is "_Game"
                var parentLabel = Path.GetFileName(Path.GetDirectoryName(submoduleBase)) ?? "Unknown";

                var submodules = Directory.GetDirectories(submoduleBase);
                foreach (var submodule in submodules)
                {
                    var layoutsPath = Path.Combine(submodule, "Layouts");
                    if (!Directory.Exists(layoutsPath))
                        continue;

                    var submoduleName = Path.GetFileName(submodule);
                    var files = Directory.GetFiles(layoutsPath, "*.wlt", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        var relativePath = GetRelativePath(file, projectRoot);
                        layouts.Add(new LayoutInfo
                        {
                            FilePath = relativePath,
                            FileName = Path.GetFileNameWithoutExtension(file),
                            SourceLabel = $"{parentLabel}/{submoduleName}",
                            FullPath = file
                        });
                    }
                }
            }

            return layouts.OrderBy(l => l.SourceLabel).ThenBy(l => l.FileName).ToList();
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
                return fullPath;

            var fullPathNormalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var basePathNormalized = Path.GetFullPath(basePath).Replace('\\', '/');

            if (fullPathNormalized.StartsWith(basePathNormalized))
            {
                var relative = fullPathNormalized.Substring(basePathNormalized.Length);
                return relative.TrimStart('/', '\\');
            }

            return fullPath;
        }

        public void OnGUI()
        {
            var layouts = CollectAllLayouts();
            var recentLayouts = GetRecentLayouts();

            EditorGUILayout.BeginHorizontal();

            if (layouts.Count == 0)
            {
                GUILayout.Label("No layouts", EditorStyles.miniLabel);
            }
            else
            {
                string buttonText = layouts.Count == 1 ? "Layout" : $"Layouts ({layouts.Count})";
                if (GUILayout.Button(new GUIContent(buttonText, "Select layout to load")))
                {
                    ShowLayoutSelectionMenu(layouts);
                }
            }

            // Recent layouts dropdown
            if (recentLayouts.Count > 0)
            {
                if (GUILayout.Button(new GUIContent("Recent", "Recent layouts"), GUILayout.Width(60)))
                {
                    ShowRecentLayoutsMenu(recentLayouts);
                }
            }

            // Save Layout button
            if (GUILayout.Button(new GUIContent("Save Layout", "Save current window layout"), GUILayout.Width(80)))
            {
                ShowSaveLayoutMenu();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowLayoutSelectionMenu(List<LayoutInfo> layouts)
        {
            var menu = new GenericMenu();

            // Group by source for better organization
            var grouped = layouts.GroupBy(l => l.SourceLabel);

            foreach (var group in grouped)
            {
                var sourceLabel = group.Key;

                foreach (var layout in group)
                {
                    var menuLabel = $"{sourceLabel}/{layout.FileName}";
                    var capturedPath = layout.FullPath;

                    menu.AddItem(new GUIContent(menuLabel), false, () =>
                    {
                        LoadLayout(capturedPath, menuLabel);
                    });
                }

                // Add separator between groups (except after the last one)
                if (group != grouped.Last())
                {
                    menu.AddSeparator("");
                }
            }

            menu.ShowAsContext();
        }

        private void ShowSaveLayoutMenu()
        {
            var menu = new GenericMenu();

            // Add Shared option
            menu.AddItem(new GUIContent("Shared"), false, () => SaveLayoutToDestination("Shared", "Assets/Layouts"));

            menu.AddSeparator("");

            // Get all submodules from dynamically discovered Submodules directories
            foreach (var submoduleBase in FindAllSubmoduleRoots())
            {
                var parentLabel = Path.GetFileName(Path.GetDirectoryName(submoduleBase)) ?? "Unknown";

                var submodules = Directory.GetDirectories(submoduleBase);
                foreach (var submodule in submodules)
                {
                    var submoduleName = Path.GetFileName(submodule);
                    // Build relative path from the Assets folder
                    var assetsRelative = GetRelativePath(submodule, Application.dataPath);
                    var relativePath = $"Assets/{assetsRelative}/Layouts";

                    var menuLabel = $"{parentLabel}/{submoduleName}";
                    menu.AddItem(new GUIContent(menuLabel), false, () => SaveLayoutToDestination(menuLabel, relativePath));
                }
            }

            menu.ShowAsContext();
        }

        private void SaveLayoutToDestination(string destinationLabel, string relativePath)
        {
            // Show input dialog window with toolbar checkboxes
            EditorLayoutSaveDialog.Show("Save Layout", "Enter layout name:", "NewLayout", (layoutName, includeFlags) =>
            {
                if (string.IsNullOrEmpty(layoutName))
                    return;

                // Ensure directory exists
                var fullPath = Path.Combine(Application.dataPath, relativePath.Replace("Assets/", ""));
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    AssetDatabase.Refresh();
                }

                var layoutPath = Path.Combine(fullPath, $"{layoutName}.wlt");
                var absolutePath = Path.GetFullPath(layoutPath);

                // Save layout using Unity's API
                if (TrySaveWindowLayout(absolutePath))
                {
                    // Capture and save toolbar states to sibling JSON file if any are included
                    if (includeFlags != null && (includeFlags.includeAppStatusLeft || includeFlags.includeAppStatusRight || includeFlags.includeHeaderToolbar))
                    {
                        var toolbarStates = CaptureToolbarStates(includeFlags.includeAppStatusLeft, includeFlags.includeAppStatusRight, includeFlags.includeHeaderToolbar);
                        SaveToolbarStates(layoutPath, toolbarStates);
                    }

                    Debug.Log($"[LayoutToolbar] Saved layout '{layoutName}' to {destinationLabel}");
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to save layout to {destinationLabel}", "OK");
                }
            });
        }

        private bool TrySaveWindowLayout(string path)
        {
            if (!_saveMethodInitialized)
            {
                InitializeSaveMethod();
            }

            if (_saveWindowLayoutMethod != null)
            {
                try
                {
                    _saveWindowLayoutMethod.Invoke(null, new object[] { path });
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LayoutToolbar] Error saving layout: {ex.Message}");
                    return false;
                }
            }

            // Fallback: try EditorUtility.SaveWindowLayout if available (Unity 2021.2+)
            try
            {
                var saveMethod = typeof(EditorUtility).GetMethod("SaveWindowLayout", BindingFlags.Public | BindingFlags.Static);
                if (saveMethod != null)
                {
                    saveMethod.Invoke(null, new object[] { path });
                    return true;
                }
            }
            catch
            {
                // Ignore
            }

            Debug.LogError("[LayoutToolbar] Unable to save layout - SaveWindowLayout method not found");
            return false;
        }

        private void InitializeSaveMethod()
        {
            _saveMethodInitialized = true;

            // Try to get WindowLayout.SaveWindowLayout via reflection
            var windowLayoutType = System.Type.GetType("UnityEditor.WindowLayout,UnityEditor");
            if (windowLayoutType != null)
            {
                _saveWindowLayoutMethod = windowLayoutType.GetMethod("SaveWindowLayout",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public,
                    null,
                    new System.Type[] { typeof(string) },
                    null);
            }
        }

        private void SaveToolbarStates(string layoutPath, ToolbarStates states)
        {
            try
            {
                // Create sibling file with .json extension
                var jsonPath = Path.ChangeExtension(layoutPath, ".json");
                var json = JsonUtility.ToJson(states, true);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Failed to save toolbar states: {ex.Message}");
            }
        }

        private void LoadToolbarStates(string layoutPath)
        {
            try
            {
                // Look for sibling JSON file
                var jsonPath = Path.ChangeExtension(layoutPath, ".json");
                if (!File.Exists(jsonPath))
                    return;

                var json = File.ReadAllText(jsonPath);
                var states = JsonUtility.FromJson<ToolbarStates>(json);

                if (states == null)
                    return;

                // Apply toolbar states only if they were included in the save
                if (states.includeAppStatusLeft && states.appStatusLeftSection >= 0)
                {
                    EditorPrefs.SetInt(SelectedSectionPrefKeyAppStatusBar, states.appStatusLeftSection);
                }

                if (states.includeAppStatusRight && states.appStatusRightSection >= 0)
                {
                    EditorPrefs.SetInt(SelectedRightSectionPrefKeyAppStatusBar, states.appStatusRightSection);
                }

                if (states.includeHeaderToolbar && states.headerToolbarSection >= 0)
                {
                    EditorPrefs.SetInt(HeaderToolbarSelectionKey, states.headerToolbarSection);
                    // Also update GenericToolbar's internal state
                    GenericToolbar.SetSelectedSection(states.headerToolbarSection);
                }

                Debug.Log($"[LayoutToolbar] Loaded toolbar states from {Path.GetFileName(jsonPath)}");

                // Trigger repaint to update UI
                EditorApplication.delayCall += () =>
                {
                    var repaintMethod = typeof(EditorApplication).GetMethod("QueuePlayerLoopUpdate",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    repaintMethod?.Invoke(null, null);
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Failed to load toolbar states: {ex.Message}");
            }
        }

        private ToolbarStates CaptureToolbarStates(bool includeLeft, bool includeRight, bool includeHeader)
        {
            var states = new ToolbarStates
            {
                includeAppStatusLeft = includeLeft,
                includeAppStatusRight = includeRight,
                includeHeaderToolbar = includeHeader
            };

            if (includeLeft)
            {
                states.appStatusLeftSection = EditorPrefs.GetInt(SelectedSectionPrefKeyAppStatusBar, -1);
            }

            if (includeRight)
            {
                states.appStatusRightSection = EditorPrefs.GetInt(SelectedRightSectionPrefKeyAppStatusBar, -1);
            }

            if (includeHeader)
            {
                states.headerToolbarSection = EditorPrefs.GetInt(HeaderToolbarSelectionKey, -1);
            }

            return states;
        }

        private void LoadLayout(string layoutPath, string displayName)
        {
            if (!File.Exists(layoutPath))
            {
                Debug.LogWarning($"[LayoutToolbar] Layout file not found: {layoutPath}");
                return;
            }

            EditorUtility.LoadWindowLayout(layoutPath);
            // Try to load and apply toolbar states (optional - don't fail if file doesn't exist)
            LoadToolbarStates(layoutPath);

            // Add to recent layouts
            AddToRecentLayouts(layoutPath, displayName);
        }

        private void ShowRecentLayoutsMenu(List<RecentLayoutInfo> recentLayouts)
        {
            var menu = new GenericMenu();

            foreach (var recent in recentLayouts)
            {
                var capturedPath = recent.filePath;
                var capturedName = recent.displayName;

                menu.AddItem(new GUIContent(capturedName), false, () =>
                {
                    LoadLayout(capturedPath, capturedName);
                });
            }

            menu.ShowAsContext();
        }

        private List<RecentLayoutInfo> GetRecentLayouts()
        {
            try
            {
                var json = EditorPrefs.GetString(RecentLayoutsPrefKey, "");
                if (string.IsNullOrEmpty(json))
                    return new List<RecentLayoutInfo>();

                var data = JsonUtility.FromJson<RecentLayoutsData>(json);
                if (data == null || data.recentLayouts == null)
                    return new List<RecentLayoutInfo>();

                // Filter out layouts that no longer exist
                var validLayouts = data.recentLayouts.Where(l => File.Exists(l.filePath)).ToList();

                // Update stored list if any were removed
                if (validLayouts.Count != data.recentLayouts.Count)
                {
                    SaveRecentLayouts(validLayouts);
                }

                return validLayouts;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Failed to load recent layouts: {ex.Message}");
                return new List<RecentLayoutInfo>();
            }
        }

        private void AddToRecentLayouts(string layoutPath, string displayName)
        {
            try
            {
                var recentLayouts = GetRecentLayouts();

                // Remove if already exists (will add to top)
                recentLayouts.RemoveAll(l => l.filePath == layoutPath);

                // Add to beginning
                recentLayouts.Insert(0, new RecentLayoutInfo
                {
                    filePath = layoutPath,
                    displayName = displayName
                });

                // Limit to max count
                if (recentLayouts.Count > MaxRecentLayouts)
                {
                    recentLayouts.RemoveRange(MaxRecentLayouts, recentLayouts.Count - MaxRecentLayouts);
                }

                SaveRecentLayouts(recentLayouts);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Failed to add to recent layouts: {ex.Message}");
            }
        }

        private void SaveRecentLayouts(List<RecentLayoutInfo> recentLayouts)
        {
            try
            {
                var data = new RecentLayoutsData { recentLayouts = recentLayouts };
                var json = JsonUtility.ToJson(data);
                EditorPrefs.SetString(RecentLayoutsPrefKey, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LayoutToolbar] Failed to save recent layouts: {ex.Message}");
            }
        }
    }

    // Toolbar include flags for save dialog
    public class ToolbarIncludeFlags
    {
        public bool includeAppStatusLeft;
        public bool includeAppStatusRight;
        public bool includeHeaderToolbar;
    }

    // Enhanced input dialog with toolbar checkboxes
    public class EditorLayoutSaveDialog : EditorWindow
    {
        private string inputText = "";
        private string message = "";
        private System.Action<string, ToolbarIncludeFlags> onComplete;
        private bool focused = false;
        private bool includeAppStatusLeft = true;
        private bool includeAppStatusRight = true;
        private bool includeHeaderToolbar = true;

        public static void Show(string title, string message, string defaultValue, System.Action<string, ToolbarIncludeFlags> onComplete)
        {
            var window = CreateInstance<EditorLayoutSaveDialog>();
            window.titleContent = new GUIContent(title);
            window.message = message;
            window.inputText = defaultValue;
            window.onComplete = onComplete;
            window.minSize = new Vector2(400, 200);
            window.maxSize = new Vector2(400, 200);
            window.position = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 100, 400, 200);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label(message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);

            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);

            if (!focused)
            {
                EditorGUI.FocusTextInControl("InputField");
                focused = true;
            }

            EditorGUILayout.Space(10);
            GUILayout.Label("Include Toolbar States:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            includeAppStatusLeft = EditorGUILayout.Toggle("App Status Toolbar Left", includeAppStatusLeft);
            includeAppStatusRight = EditorGUILayout.Toggle("App Status Toolbar Right", includeAppStatusRight);
            includeHeaderToolbar = EditorGUILayout.Toggle("Header Toolbar", includeHeaderToolbar);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                onComplete?.Invoke(null, null);
                Close();
            }
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                var flags = new ToolbarIncludeFlags
                {
                    includeAppStatusLeft = includeAppStatusLeft,
                    includeAppStatusRight = includeAppStatusRight,
                    includeHeaderToolbar = includeHeaderToolbar
                };
                onComplete?.Invoke(inputText, flags);
                Close();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            // Handle Enter key
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    var flags = new ToolbarIncludeFlags
                    {
                        includeAppStatusLeft = includeAppStatusLeft,
                        includeAppStatusRight = includeAppStatusRight,
                        includeHeaderToolbar = includeHeaderToolbar
                    };
                    onComplete?.Invoke(inputText, flags);
                    Close();
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    onComplete?.Invoke(null, null);
                    Close();
                    Event.current.Use();
                }
            }
        }
    }
}
