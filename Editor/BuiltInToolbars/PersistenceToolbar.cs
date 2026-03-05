using System.IO;
using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("Persistence")]
public class PersistenceToolbar : IEditorToolbar
{
    private const string PersistencePathPrefKey = "PersistenceToolbar.LastKnownPath";
    
    public bool ShouldShow()
    {
        return true;
    }

    public void OnGUI()
    {
        // Get persistence data path
        string persistencePath = Application.persistentDataPath;
        
        // Display path (truncated if too long)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Save Path:", GUILayout.Width(70));
        string displayPath = TruncatePath(persistencePath, 50);
        EditorGUILayout.LabelField(displayPath, EditorStyles.miniLabel);
        if (GUILayout.Button(new GUIContent("...", "Click to copy full path"), EditorStyles.miniButton, GUILayout.Width(20)))
        {
            EditorGUIUtility.systemCopyBuffer = persistencePath;
            Debug.Log($"[PersistenceToolbar] Copied path to clipboard: {persistencePath}");
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // Quick Open Folder button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Folder", GUILayout.Height(20)))
        {
            OpenPersistenceFolder();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // Clear Save Data button
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red
        if (GUILayout.Button("Clear Save Data", GUILayout.Height(20)))
        {
            ClearSaveData();
        }
        GUI.backgroundColor = Color.white; // Reset
        EditorGUILayout.EndHorizontal();
    }
    
    private void OpenPersistenceFolder()
    {
        string persistencePath = Application.persistentDataPath;
        
        // Ensure directory exists
        if (!Directory.Exists(persistencePath))
        {
            try
            {
                Directory.CreateDirectory(persistencePath);
                Debug.Log($"[PersistenceToolbar] Created persistence directory: {persistencePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PersistenceToolbar] Failed to create persistence directory: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create persistence directory:\n{ex.Message}", "OK");
                return;
            }
        }
        
        // Open folder in file browser
        try
        {
            EditorUtility.RevealInFinder(persistencePath);
            Debug.Log($"[PersistenceToolbar] Opened persistence folder: {persistencePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PersistenceToolbar] Failed to open folder: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to open persistence folder:\n{ex.Message}", "OK");
        }
    }
    
    private void ClearSaveData()
    {
        string persistencePath = Application.persistentDataPath;
        
        if (!Directory.Exists(persistencePath))
        {
            EditorUtility.DisplayDialog("Clear Save Data", "Persistence folder does not exist. Nothing to clear.", "OK");
            return;
        }
        
        // Count files and directories
        int fileCount = 0;
        int dirCount = 0;
        
        try
        {
            fileCount = Directory.GetFiles(persistencePath, "*", SearchOption.AllDirectories).Length;
            dirCount = Directory.GetDirectories(persistencePath, "*", SearchOption.AllDirectories).Length;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PersistenceToolbar] Failed to count files: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to access persistence folder:\n{ex.Message}", "OK");
            return;
        }
        
        if (fileCount == 0 && dirCount == 0)
        {
            EditorUtility.DisplayDialog("Clear Save Data", "Persistence folder is empty. Nothing to clear.", "OK");
            return;
        }
        
        // Confirm deletion
        bool confirmed = EditorUtility.DisplayDialog(
            "Clear Save Data",
            $"This will delete all files and folders in the persistence directory:\n\n{persistencePath}\n\nFiles: {fileCount}\nFolders: {dirCount}\n\nThis action cannot be undone. Continue?",
            "Yes, Clear All",
            "Cancel"
        );
        
        if (!confirmed)
        {
            return;
        }
        
        // Perform deletion
        try
        {
            string[] files = Directory.GetFiles(persistencePath, "*", SearchOption.AllDirectories);
            string[] directories = Directory.GetDirectories(persistencePath, "*", SearchOption.AllDirectories);
            
            // Delete files first
            int deletedFiles = 0;
            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedFiles++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PersistenceToolbar] Failed to delete file {file}: {ex.Message}");
                }
            }
            
            // Delete directories (in reverse order to delete nested ones first)
            int deletedDirs = 0;
            System.Array.Sort(directories);
            System.Array.Reverse(directories);
            foreach (string directory in directories)
            {
                try
                {
                    Directory.Delete(directory, true);
                    deletedDirs++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PersistenceToolbar] Failed to delete directory {directory}: {ex.Message}");
                }
            }
            
            // Also delete files directly in the root persistence folder
            string[] rootFiles = Directory.GetFiles(persistencePath);
            foreach (string file in rootFiles)
            {
                try
                {
                    File.Delete(file);
                    deletedFiles++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PersistenceToolbar] Failed to delete file {file}: {ex.Message}");
                }
            }
            
            Debug.Log($"[PersistenceToolbar] Cleared save data: {deletedFiles} files, {deletedDirs} directories deleted");
            EditorUtility.DisplayDialog("Clear Save Data", $"Successfully cleared save data:\n{deletedFiles} files\n{deletedDirs} directories", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PersistenceToolbar] Failed to clear save data: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to clear save data:\n{ex.Message}", "OK");
        }
    }
    
    private string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path))
            return "";
        
        if (path.Length <= maxLength)
            return path;
        
        // Show beginning and end of path
        int startLength = maxLength / 2 - 3;
        int endLength = maxLength / 2 - 3;
        
        return path.Substring(0, startLength) + "..." + path.Substring(path.Length - endLength);
    }
}
