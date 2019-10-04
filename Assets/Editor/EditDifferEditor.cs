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
    SerializedProperty componentIds;
    GUIStyle wrapStyle;
    GUIContent newAssetLabel;
    GUIContent compIdsLabel;

    void OnEnable()
    {
        fileId = serializedObject.FindProperty("fileId");
        pathId = serializedObject.FindProperty("pathId");
        origPathId = serializedObject.FindProperty("origPathId");
        newAsset = serializedObject.FindProperty("newAsset");
        componentIds = serializedObject.FindProperty("componentIds");
        if (wrapStyle == null)
        {
            wrapStyle = new GUIStyle(EditorStyles.label);
            wrapStyle.wordWrap = true;
        }
        if (newAssetLabel == null)
        {
            newAssetLabel = new GUIContent("New Asset?");
        }
        if (compIdsLabel == null)
        {
            compIdsLabel = new GUIContent("Component IDs");
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
            //else if (Event.current.type == EventType.Repaint)
            //{
            //    if (componentIds.arraySize != ((EditDiffer)target).gameObject.GetComponents<Component>().Length - 1)
            //    {
            //        componentIds.DeleteArrayElementAtIndex()
            //    }
            //}
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
        EditorGUILayout.PropertyField(componentIds, compIdsLabel, true);
        GUI.enabled = true;
        serializedObject.ApplyModifiedProperties();
    }
}