using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScreenNotificationController))]
public class ScreenNotificationControllerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ctrl = (ScreenNotificationController)target;
        if (ctrl == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview (Play Mode)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to run preview (fades + optional MMF).", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Play preview"))
                ctrl.PlayDebugPreview();
        }
    }
}
