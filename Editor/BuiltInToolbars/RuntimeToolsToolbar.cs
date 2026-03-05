using UnityEditor;
using UnityEngine;

namespace EditorToolbar
{
    [ToolbarSection("Runtime Tools")]
    public class RuntimeToolsToolbar : IEditorToolbar
    {
        public bool ShouldShow() => EditorApplication.isPlaying;

        public void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                GUILayout.Label("Not Running");
                return;
            }

            if (GUILayout.Button(new GUIContent("Pause", "Pause the game")))
            {
                EditorApplication.isPaused = true;
            }

            if (GUILayout.Button(new GUIContent("Resume", "Resume the game")))
            {
                EditorApplication.isPaused = false;
            }

            GUILayout.Space(5);

            var timeScale = Time.timeScale;
            GUILayout.Label($"Time Scale: {timeScale:F2}");

            if (GUILayout.Button(new GUIContent("Normal Speed", "Set time scale to 1")))
            {
                Time.timeScale = 1f;
            }

            if (GUILayout.Button(new GUIContent("Half Speed", "Set time scale to 0.5")))
            {
                Time.timeScale = 0.5f;
            }

            if (GUILayout.Button(new GUIContent("Double Speed", "Set time scale to 2")))
            {
                Time.timeScale = 2f;
            }
        }
    }
}
