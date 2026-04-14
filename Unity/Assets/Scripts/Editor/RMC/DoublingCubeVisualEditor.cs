using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DoublingCubeVisual))]
public class DoublingCubeVisualEditor : UnityEditor.Editor
{
    private static readonly int[] DebugValues = { 1, 2, 4, 8, 16, 32, 64 };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var visual = (DoublingCubeVisual)target;
        if (visual == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Quick Apply (Visual-Only)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These buttons rotate/animate only the cube visual and do not modify BackgammonGameController.State.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Debug Value Instant"))
                visual.DebugApplySpecificValue(GetDebugValue(visual), false);
            if (GUILayout.Button("Apply Debug Value Animated"))
                visual.DebugApplySpecificValue(GetDebugValue(visual), true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            for (int i = 0; i < DebugValues.Length; i++)
            {
                int value = DebugValues[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"Set {value} Instant"))
                    visual.DebugApplySpecificValue(value, false);
                if (GUILayout.Button($"Set {value} Animated"))
                    visual.DebugApplySpecificValue(value, true);
                EditorGUILayout.EndHorizontal();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use debug apply buttons.", MessageType.None);
    }

    private static int GetDebugValue(DoublingCubeVisual visual)
    {
        var field = typeof(DoublingCubeVisual).GetField("debugCubeValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(visual) : 2;
    }
}