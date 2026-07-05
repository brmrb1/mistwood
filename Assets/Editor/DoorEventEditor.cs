using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DoorEvent))]
public class DoorEventEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("interactSfx"), new GUIContent("交互音效"));

        EditorGUILayout.Space();

        DrawPropertiesExcluding(serializedObject, "m_Script", "interactSfx");

        serializedObject.ApplyModifiedProperties();
    }
}