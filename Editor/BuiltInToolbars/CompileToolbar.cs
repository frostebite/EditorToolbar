using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Asset postprocessor to detect asset changes for compile toolbar
/// </summary>
public class CompileToolbarAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        CompileToolbar.NotifyAssetChanges(importedAssets, deletedAssets, movedAssets);
    }
}

[ToolbarSectionAttribute("Compile")]
public class CompileToolbar : IEditorToolbar
{
    /// <summary>
    /// Refresh mode preferences - controls when compilation and asset imports occur.
    /// </summary>
    private enum RefreshMode
    {
        /// <summary>
        /// Auto: Unity's default behavior - automatically recompiles and imports when assets change.
        /// This is Unity's standard behavior.
        /// </summary>
        Auto,
        
        /// <summary>
        /// Manual: Only refresh (recompile and import) when explicitly requested via the Refresh button.
        /// Uses EditorApplication.LockReloadAssemblies() to prevent automatic compilation.
        /// </summary>
        Manual
    }

    private const string RefreshModePrefKey = "CompileToolbar.RefreshMode";
    private RefreshMode _refreshMode;
    private bool _wasCompiling;
    private DateTime _lastRefreshTime;
    private bool _assembliesLocked;
    private bool _assetEditingStopped;
    private bool _hasPendingChanges;
    private HashSet<string> _pendingScriptChanges = new HashSet<string>();
    private HashSet<string> _pendingAssetImports = new HashSet<string>();
    
    // Static reference to track instance (toolbar sections are created by GenericToolbar)
    private static CompileToolbar _instance;
    private static bool _updateSubscribed = false;

    public CompileToolbar()
    {
        _instance = this;
        LoadRefreshMode();
        _wasCompiling = EditorApplication.isCompiling;
        
        // CRITICAL: Clean up any lingering locks from previous session/version
        // This ensures assemblies aren't locked and asset editing is enabled on startup
        CleanupLocks();
        
        // Subscribe to assembly reload events to track compile completion
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        
        // Subscribe to compilation pipeline to intercept unwanted compilations
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        
        // Use EditorApplication.update to track compilation state changes and manage locks
        // Only subscribe once to prevent memory leaks
        if (!_updateSubscribed)
        {
            EditorApplication.update += OnEditorUpdate;
            _updateSubscribed = true;
        }
        
        // Defer applying refresh mode until after initialization completes
        // This prevents hanging the editor during InitializeOnLoad
        EditorApplication.delayCall += () =>
        {
            // Only apply if not currently compiling and instance still exists
            if (!EditorApplication.isCompiling && _instance != null)
            {
                ApplyRefreshMode();
            }
        };
    }
    
    /// <summary>
    /// Cleans up any lingering locks from previous sessions or versions.
    /// This ensures assemblies are unlocked and asset editing is enabled on initialization.
    /// </summary>
    private void CleanupLocks()
    {
        // Note: Unity resets lock states on editor restart, but we clean up explicitly
        // to handle edge cases where the editor was hung or crashed
        
        // Unlock assemblies (Unity uses reference counting, safe to call even if not locked)
        try
        {
            EditorApplication.UnlockReloadAssemblies();
            _assembliesLocked = false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CompileToolbar] Error unlocking assemblies during cleanup: {ex.Message}");
        }
        
        // Ensure asset editing is in normal state (counter = 0)
        // Unity uses reference counting: StartAssetEditing increments, StopAssetEditing decrements
        // Normal state is when counter = 0 (asset editing enabled)
        // We don't need to call StartAssetEditing here - Unity resets to normal state on editor restart
        // Just reset our tracking flag
        _assetEditingStopped = false;
        
        Debug.Log("[CompileToolbar] Cleaned up any lingering locks from previous session - assemblies unlocked, asset editing enabled");
    }
    
    public static void NotifyAssetChanges(string[] importedAssets, string[] deletedAssets, string[] movedAssets)
    {
        if (_instance != null)
        {
            _instance.OnAssetChanged(importedAssets, deletedAssets, movedAssets);
        }
    }

    public bool ShouldShow()
    {
        return true;
    }

    public void OnGUI()
    {
        bool isCompiling = EditorApplication.isCompiling;
        
        // Unified refresh mode selector
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField("Refresh:", GUILayout.Width(70));
        
        var newMode = (RefreshMode)EditorGUILayout.EnumPopup(_refreshMode, GUILayout.Width(120));
        if (newMode != _refreshMode)
        {
            var oldMode = _refreshMode;
            _refreshMode = newMode;
            SaveRefreshMode();
            ApplyRefreshMode();
            
            // When switching from Manual to Auto, trigger refresh if there are pending changes
            if (oldMode == RefreshMode.Manual && _refreshMode == RefreshMode.Auto && _hasPendingChanges)
            {
                Debug.Log("[CompileToolbar] Switching to Auto mode with pending changes - triggering refresh");
                RequestRefresh();
            }
            else if (_refreshMode == RefreshMode.Auto)
            {
                // Clear pending changes when switching to Auto (if no refresh needed)
                _hasPendingChanges = false;
                _pendingScriptChanges.Clear();
                _pendingAssetImports.Clear();
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Status and refresh button row
        EditorGUILayout.BeginHorizontal();
        
        // Status indicator
        string statusText;
        Color statusColor;
        if (isCompiling)
        {
            statusText = "Compiling...";
            statusColor = new Color(1f, 0.7f, 0f); // Orange
        }
        else if (_hasPendingChanges && _refreshMode == RefreshMode.Manual)
        {
            statusText = "Pending";
            statusColor = new Color(1f, 0.5f, 0f); // Orange-red
        }
        else
        {
            statusText = "Ready";
            statusColor = new Color(0.3f, 1f, 0.3f); // Green
        }
        
        var originalColor = GUI.color;
        GUI.color = statusColor;
        EditorGUILayout.LabelField(statusText, GUILayout.Width(80));
        GUI.color = originalColor;
        
        // Unified Refresh button (triggers both recompile and import)
        bool wasEnabled = GUI.enabled;
        GUI.enabled = !isCompiling;
        
        if (GUILayout.Button("Refresh", GUILayout.Width(80)))
        {
            RequestRefresh();
        }
        
        GUI.enabled = wasEnabled;
        
        EditorGUILayout.EndHorizontal();
        
        // Show pending changes info
        if (_hasPendingChanges && _refreshMode == RefreshMode.Manual)
        {
            var scriptCount = _pendingScriptChanges.Count;
            var assetCount = _pendingAssetImports.Count;
            var parts = new List<string>();
            if (scriptCount > 0) parts.Add($"{scriptCount} script(s)");
            if (assetCount > 0) parts.Add($"{assetCount} asset(s)");
            var message = parts.Count > 0 
                ? $"Pending changes detected ({string.Join(", ", parts)}). Click Refresh to apply."
                : "Pending changes detected. Click Refresh to apply.";
            EditorGUILayout.LabelField(message, EditorStyles.miniLabel);
        }
        
        // Last refresh time (if available)
        if (_lastRefreshTime != default(DateTime))
        {
            var timeSinceRefresh = DateTime.Now - _lastRefreshTime;
            string timeText = timeSinceRefresh.TotalSeconds < 60 
                ? $"{timeSinceRefresh.TotalSeconds:F0}s ago" 
                : $"{timeSinceRefresh.TotalMinutes:F1}m ago";
            
            EditorGUILayout.LabelField($"Last refresh: {timeText}", EditorStyles.miniLabel);
        }
    }

    private void RequestRefresh()
    {
        Debug.Log("[CompileToolbar] Requesting refresh (recompile and import)...");
        
        // Unlock assemblies if locked
        if (_assembliesLocked)
        {
            EditorApplication.UnlockReloadAssemblies();
            _assembliesLocked = false;
        }
        
        try
        {
            // Resume asset editing if it was stopped (this processes any batched imports when counter reaches 0)
            if (_assetEditingStopped)
            {
                AssetDatabase.StartAssetEditing();
                _assetEditingStopped = false;
            }
            
            // Force refresh asset database to import any pending assets (while asset editing is enabled)
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            
            // Request script compilation
            CompilationPipeline.RequestScriptCompilation();
            
            _lastRefreshTime = DateTime.Now;
            
            // Clear pending changes
            _hasPendingChanges = false;
            _pendingScriptChanges.Clear();
            _pendingAssetImports.Clear();
            
            // If still in Manual mode, stop asset editing again to prevent future automatic imports
            if (_refreshMode == RefreshMode.Manual)
            {
                AssetDatabase.StopAssetEditing();
                _assetEditingStopped = true;
            }
            
            Debug.Log("[CompileToolbar] Refresh completed (import and recompile requested).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CompileToolbar] Failed to refresh: {ex.Message}");
        }
    }

    private void ApplyRefreshMode()
    {
        // Unlock assemblies first if they were locked
        if (_assembliesLocked)
        {
            EditorApplication.UnlockReloadAssemblies();
            _assembliesLocked = false;
        }
        
        // Resume asset editing if it was stopped
        if (_assetEditingStopped)
        {
            AssetDatabase.StartAssetEditing();
            _assetEditingStopped = false;
        }
        
        // Lock assemblies and stop asset editing for Manual mode
        if (_refreshMode == RefreshMode.Manual)
        {
            EditorApplication.LockReloadAssemblies();
            _assembliesLocked = true;
            
            // Stop asset editing to prevent automatic imports and progress bars
            AssetDatabase.StopAssetEditing();
            _assetEditingStopped = true;
            
            Debug.Log("[CompileToolbar] Manual refresh mode enabled - assemblies locked and asset editing stopped. Use Refresh button to compile and import.");
        }
    }

    private void OnEditorUpdate()
    {
        // Early exit if instance is null (shouldn't happen, but defensive programming)
        if (_instance == null)
            return;
            
        bool isCompiling = EditorApplication.isCompiling;
        
        // In Manual mode, ensure assemblies are always locked and asset editing is stopped (proactive locking)
        // This prevents Unity from starting compilation and showing progress bars when file changes are detected
        if (_refreshMode == RefreshMode.Manual && !isCompiling)
        {
            if (!_assembliesLocked)
            {
                EditorApplication.LockReloadAssemblies();
                _assembliesLocked = true;
            }
            
            if (!_assetEditingStopped)
            {
                AssetDatabase.StopAssetEditing();
                _assetEditingStopped = true;
            }
        }
        
        // Track when compilation finishes
        if (_wasCompiling && !isCompiling)
        {
            if (_lastRefreshTime == default(DateTime))
            {
                _lastRefreshTime = DateTime.Now;
            }
            
            // Re-lock assemblies and stop asset editing if in Manual mode
            if (_refreshMode == RefreshMode.Manual)
            {
                if (!_assembliesLocked)
                {
                    EditorApplication.LockReloadAssemblies();
                    _assembliesLocked = true;
                }
                
                if (!_assetEditingStopped)
                {
                    AssetDatabase.StopAssetEditing();
                    _assetEditingStopped = true;
                }
            }
        }
        
        _wasCompiling = isCompiling;
    }
    
    private void OnCompilationStarted(object obj)
    {
        // If in Manual mode and compilation starts unexpectedly, try to prevent it
        // Note: We can't actually cancel compilation once it starts, but we can log a warning
        // and ensure assemblies remain locked so the reload doesn't happen
        if (_refreshMode == RefreshMode.Manual)
        {
            Debug.LogWarning("[CompileToolbar] Compilation started in Manual mode. This should not happen. Ensuring assemblies remain locked.");
            
            // Ensure lock is in place to prevent reload
            if (!_assembliesLocked)
            {
                EditorApplication.LockReloadAssemblies();
                _assembliesLocked = true;
            }
        }
    }

    private void OnAfterAssemblyReload()
    {
        // Assembly reload happens after compilation finishes
        if (_wasCompiling)
        {
            if (_lastRefreshTime == default(DateTime))
            {
                _lastRefreshTime = DateTime.Now;
            }
            _wasCompiling = false;
        }
        
        // CRITICAL: Defer locking operations until after all OnAfterAssemblyReload callbacks complete
        // Calling LockReloadAssemblies() or StopAssetEditing() directly in this callback can cause Unity to hang
        // because Unity is still processing the assembly reload and waiting for all callbacks to finish
        if (_refreshMode == RefreshMode.Manual)
        {
            EditorApplication.delayCall += () =>
            {
                // Verify we're still in Manual mode after delay (mode might have changed)
                if (_refreshMode != RefreshMode.Manual)
                    return;
                
                if (!_assembliesLocked)
                {
                    EditorApplication.LockReloadAssemblies();
                    _assembliesLocked = true;
                }
                
                if (!_assetEditingStopped)
                {
                    AssetDatabase.StopAssetEditing();
                    _assetEditingStopped = true;
                }
            };
        }
    }
    
    public void OnAssetChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets)
    {
        if (_refreshMode == RefreshMode.Auto)
        {
            // Auto mode - let Unity handle it, clear any pending changes
            _hasPendingChanges = false;
            _pendingScriptChanges.Clear();
            _pendingAssetImports.Clear();
            return;
        }
        
        // Manual mode - track all changes
        // IMPORTANT: Lock assemblies and stop asset editing immediately to prevent Unity from starting compilation/imports
        // This must happen synchronously in the asset postprocessor callback
        // Note: Asset postprocessor is called AFTER Unity has detected changes but BEFORE it processes them
        // Stopping asset editing here prevents the progress bar from showing
        if (!_assembliesLocked)
        {
            EditorApplication.LockReloadAssemblies();
            _assembliesLocked = true;
        }
        
        if (!_assetEditingStopped)
        {
            AssetDatabase.StopAssetEditing();
            _assetEditingStopped = true;
        }
        
        Debug.Log("[CompileToolbar] Locked assemblies and stopped asset editing in response to asset changes (Manual mode).");
        
        // Track asset imports
        foreach (var asset in importedAssets)
        {
            _pendingAssetImports.Add(asset);
        }
        foreach (var asset in movedAssets)
        {
            _pendingAssetImports.Add(asset);
        }
        
        // Check for script changes
        bool hasScriptChanges = false;
        foreach (var asset in importedAssets.Concat(movedAssets))
        {
            if (asset.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                hasScriptChanges = true;
                _pendingScriptChanges.Add(asset);
            }
        }
        
        // Mark as pending if there are any changes
        if (importedAssets.Length > 0 || deletedAssets.Length > 0 || movedAssets.Length > 0)
        {
            _hasPendingChanges = true;
        }
    }

    private void LoadRefreshMode()
    {
        // Delete old preference keys if they exist (migration from old separate recompile/import modes)
        if (EditorPrefs.HasKey(RecompileModePrefKey))
        {
            EditorPrefs.DeleteKey(RecompileModePrefKey);
        }
        if (EditorPrefs.HasKey(ImportModePrefKey))
        {
            EditorPrefs.DeleteKey(ImportModePrefKey);
        }
        
        // Load saved refresh mode preference, defaulting to Auto if not set
        if (EditorPrefs.HasKey(RefreshModePrefKey))
        {
            _refreshMode = (RefreshMode)EditorPrefs.GetInt(RefreshModePrefKey, (int)RefreshMode.Auto);
            Debug.Log($"[CompileToolbar] Loaded RefreshMode preference: {_refreshMode}");
        }
        else
        {
            // Default to Auto mode if no preference exists
            _refreshMode = RefreshMode.Auto;
        }
    }

    private void SaveRefreshMode()
    {
        EditorPrefs.SetInt(RefreshModePrefKey, (int)_refreshMode);
    }
    
    // Keep old pref keys for migration
    private const string RecompileModePrefKey = "CompileToolbar.RecompileMode";
    private const string ImportModePrefKey = "CompileToolbar.ImportMode";
}

