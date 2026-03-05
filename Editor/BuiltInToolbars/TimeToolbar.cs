using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("Time")]
public class TimeToolbar : IEditorToolbar
{
    public bool ShouldShow()
    {
        return EditorApplication.isPlaying;
    }

    public void OnGUI()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Not Running", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("Time Scale", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        // Current time scale display
        float currentTimeScale = Time.timeScale;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current:", GUILayout.Width(60));
        EditorGUILayout.LabelField($"{currentTimeScale:F2}x", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Reset button
        if (GUILayout.Button("Reset to 1.0", EditorStyles.miniButton, GUILayout.Height(20)))
        {
            Time.timeScale = 1f;
            Debug.Log("[TimeToolbar] Reset time scale to 1.0");
        }

        EditorGUILayout.Space(3);

        // Speed up buttons
        EditorGUILayout.LabelField("Speed Up:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+5%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 1.05f;
            Debug.Log($"[TimeToolbar] Increased time scale by 5%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("+10%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 1.10f;
            Debug.Log($"[TimeToolbar] Increased time scale by 10%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("+25%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 1.25f;
            Debug.Log($"[TimeToolbar] Increased time scale by 25%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("+50%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 1.50f;
            Debug.Log($"[TimeToolbar] Increased time scale by 50%: {Time.timeScale:F2}");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Slow down buttons
        EditorGUILayout.LabelField("Slow Down:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("-5%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 0.95f;
            Debug.Log($"[TimeToolbar] Decreased time scale by 5%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("-10%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 0.90f;
            Debug.Log($"[TimeToolbar] Decreased time scale by 10%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("-25%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 0.75f;
            Debug.Log($"[TimeToolbar] Decreased time scale by 25%: {Time.timeScale:F2}");
        }
        if (GUILayout.Button("-50%", EditorStyles.miniButton, GUILayout.Height(18)))
        {
            Time.timeScale = currentTimeScale * 0.50f;
            Debug.Log($"[TimeToolbar] Decreased time scale by 50%: {Time.timeScale:F2}");
        }
        EditorGUILayout.EndHorizontal();
    }
}

