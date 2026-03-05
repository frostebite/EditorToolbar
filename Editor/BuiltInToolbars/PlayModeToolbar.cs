using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("PlayMode")]
public class PlayModeToolbar : IEditorToolbar
{
    public bool ShouldShow()
    {
        return true;
    }

    public void OnGUI()
    {
        // Enter Play Mode Options Enabled toggle
        EditorGUILayout.BeginHorizontal();
        
        bool optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        bool newOptionsEnabled = EditorGUILayout.Toggle("Enter Play Mode Options:", optionsEnabled, GUILayout.Width(180));
        if (newOptionsEnabled != optionsEnabled)
        {
            EditorSettings.enterPlayModeOptionsEnabled = newOptionsEnabled;
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Enter Play Mode Options checkboxes (only show if enabled)
        if (EditorSettings.enterPlayModeOptionsEnabled)
        {
            // Get current options
            EnterPlayModeOptions currentOptions = EditorSettings.enterPlayModeOptions;
            
            bool domainReloadDisabled = (currentOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            bool sceneReloadDisabled = (currentOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            
            // Domain Reload toggle
            EditorGUILayout.BeginHorizontal();
            bool newDomainReloadDisabled = EditorGUILayout.Toggle("Disable Domain Reload:", domainReloadDisabled, GUILayout.Width(180));
            if (newDomainReloadDisabled != domainReloadDisabled)
            {
                if (newDomainReloadDisabled)
                {
                    EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                }
                else
                {
                    EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableDomainReload;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Scene Reload toggle
            EditorGUILayout.BeginHorizontal();
            bool newSceneReloadDisabled = EditorGUILayout.Toggle("Disable Scene Reload:", sceneReloadDisabled, GUILayout.Width(180));
            if (newSceneReloadDisabled != sceneReloadDisabled)
            {
                if (newSceneReloadDisabled)
                {
                    EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableSceneReload;
                }
                else
                {
                    EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Show current status
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EnterPlayModeOptions updatedOptions = EditorSettings.enterPlayModeOptions;
            bool updatedDomainReloadDisabled = (updatedOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            bool updatedSceneReloadDisabled = (updatedOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            
            string status = "Current: ";
            if (updatedDomainReloadDisabled && updatedSceneReloadDisabled)
            {
                status += "Both reloads disabled (fastest)";
            }
            else if (updatedDomainReloadDisabled)
            {
                status += "Domain reload disabled";
            }
            else if (updatedSceneReloadDisabled)
            {
                status += "Scene reload disabled";
            }
            else
            {
                status += "Both reloads enabled (default)";
            }
            
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode Options are disabled. Both domain and scene reloads will occur when entering play mode.", MessageType.Info);
        }
    }
}

