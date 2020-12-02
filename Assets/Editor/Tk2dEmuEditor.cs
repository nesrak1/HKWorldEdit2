using Assets.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Tk2dEmu))]
public class Tk2dEmuEditor : Editor
{
    GUIStyle wrapStyle;

    void OnEnable()
    {
        if (wrapStyle == null)
        {
            wrapStyle = new GUIStyle(EditorStyles.label);
            wrapStyle.wordWrap = true;
        }
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("HKEdit Tk2d Emulator");
        EditorGUILayout.LabelField("This script contains the data normally stored in a tk2dSprite script. The empty mesh component has been filled in with this data.", wrapStyle);
    }
}