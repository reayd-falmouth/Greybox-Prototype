using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScreenNotificationController))]
public class ScreenNotificationControllerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        var ctrl = (ScreenNotificationController)target;
        if (ctrl == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset authoring", EditorStyles.boldLabel);
        if (GUILayout.Button("Save current as preset"))
        {
            Undo.RecordObject(target, "Save notification preset");
            SerializedProperty presetsProp = serializedObject.FindProperty("presets");
            SerializedProperty debugEvent = serializedObject.FindProperty("debugPresetEventType");
            SerializedProperty debugText = serializedObject.FindProperty("debugPreviewText");
            SerializedProperty debugFont = serializedObject.FindProperty("debugFontSize");
            SerializedProperty debugOff = serializedObject.FindProperty("debugLabelOffsetPixels");

            int key = debugEvent.enumValueIndex;
            int foundIndex = -1;
            for (int i = 0; i < presetsProp.arraySize; i++)
            {
                SerializedProperty el = presetsProp.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("eventType").enumValueIndex == key)
                {
                    foundIndex = i;
                    break;
                }
            }

            SerializedProperty slot;
            if (foundIndex >= 0)
            {
                slot = presetsProp.GetArrayElementAtIndex(foundIndex);
            }
            else
            {
                presetsProp.arraySize++;
                slot = presetsProp.GetArrayElementAtIndex(presetsProp.arraySize - 1);
                slot.FindPropertyRelative("eventType").enumValueIndex = key;
                slot.FindPropertyRelative("displayDurationSeconds").floatValue = 0f;
            }

            slot.FindPropertyRelative("message").stringValue = debugText.stringValue;
            slot.FindPropertyRelative("fontSize").intValue = debugFont.intValue;
            slot.FindPropertyRelative("labelOffsetPixels").vector2Value = debugOff.vector2Value;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            Debug.Log(
                $"[Backgammon][Notify] Saved preset for {(DiceFeedbackEventType)debugEvent.enumValueIndex} (message length={debugText.stringValue?.Length ?? 0}).");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview (Play Mode)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to run preview (fades + optional MMF).", MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Play preview"))
                ctrl.PlayDebugPreview();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
