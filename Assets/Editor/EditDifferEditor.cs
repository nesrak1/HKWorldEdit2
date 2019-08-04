using Assets.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EditDiffer))]
[CanEditMultipleObjects]
public class EditDifferEditor : Editor
{
    SerializedProperty fileId;
    SerializedProperty pathId;
    SerializedProperty origPathId;
    SerializedProperty newAsset;
    GUIStyle wrapStyle;
    GUIContent newAssetLabel;

    void OnEnable()
    {
        fileId = serializedObject.FindProperty("fileId");
        pathId = serializedObject.FindProperty("pathId");
        origPathId = serializedObject.FindProperty("origPathId");
        newAsset = serializedObject.FindProperty("newAsset");
        if (wrapStyle == null)
        {
            wrapStyle = new GUIStyle(EditorStyles.label);
            wrapStyle.wordWrap = true;
        }
        if (newAssetLabel == null)
        {
            newAssetLabel = new GUIContent("New Asset?");
        }
    }

    public void OnSceneGUI()
    {
        if (Event.current != null)
        {
            if (Event.current.commandName == "Delete" || Event.current.commandName == "SoftDelete")
            {
                EditDiffer.usedIds.Remove(((EditDiffer)target).pathId);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("HKEdit Differ Data");
        EditorGUILayout.LabelField("If you copied this gameobject, make sure the pathId is different than the object you copied.", wrapStyle);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(fileId);
        EditorGUILayout.PropertyField(pathId);
        if (origPathId.longValue != pathId.longValue)
        {
            EditorGUILayout.PropertyField(origPathId);
        }
        EditorGUILayout.PropertyField(newAsset, newAssetLabel);
        GUI.enabled = true;
        serializedObject.ApplyModifiedProperties();
    }
}