using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using TextEditor = UnityEditor.UI.TextEditor;

[CustomEditor(typeof(GText))]
public class LabelEditor : GraphicEditor
{
    private SerializedProperty m_UseLocalization;
    private SerializedProperty m_LocalizationKey;
    private SerializedProperty m_Text;
    private SerializedProperty m_FontData;
    private SerializedProperty m_FontType;
    private SerializedProperty m_TiltEffect;
    private SerializedProperty m_TiltAngle;
    private SerializedProperty m_HrefEvent;

    protected override void OnEnable()
    {
        base.OnEnable();
        this.m_UseLocalization = this.serializedObject.FindProperty("m_UseLocalization");
        this.m_LocalizationKey = this.serializedObject.FindProperty("m_LocalizationKey");
        this.m_Text = this.serializedObject.FindProperty("m_Text");
        this.m_FontData = this.serializedObject.FindProperty("m_FontData");
        this.m_FontType = this.serializedObject.FindProperty("m_FontType");
        this.m_TiltEffect = this.serializedObject.FindProperty("m_TiltEffect");
        this.m_TiltAngle = this.serializedObject.FindProperty("m_TiltAngle");
        this.m_HrefEvent = this.serializedObject.FindProperty("m_HrefClickEvent");
    }
    
    
    public override void OnInspectorGUI()
    {
        this.serializedObject.Update();
        EditorGUILayout.PropertyField(this.m_UseLocalization);
        if (this.m_UseLocalization.boolValue)
        {
            EditorGUILayout.PropertyField(this.m_LocalizationKey);
            GUI.enabled = false;
        }
        EditorGUILayout.PropertyField(this.m_Text);
        GUI.enabled = true;
        GUILayout.Space(10);
        EditorGUILayout.PropertyField(this.m_FontType);
        EditorGUILayout.PropertyField(this.m_FontData);
        this.AppearanceControlsGUI();
        this.RaycastControlsGUI();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(this.m_TiltEffect);
        if (this.m_TiltEffect.boolValue)
        {
            float v = EditorGUILayout.Slider(this.m_TiltAngle.floatValue, -90f, 90f);
            if (v != this.m_TiltAngle.floatValue)
                this.m_TiltAngle.floatValue = v;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.PropertyField(this.m_HrefEvent);
        

        // if (!this.m_UseLocalization.boolValue)
        // {
        //     GUILayout.Space(10);
        //     if (GUILayout.Button("Generate Language"))
        //     {
        //         if (LanguageGenerator.HasChinese(this.m_Text.stringValue))
        //         {
        //             var generator = new LanguageGenerator();
        //             this.m_LocalizationKey.stringValue = generator.GetKey("GText", this.m_Text.stringValue);
        //             generator.SaveExcel();
        //             if (!string.IsNullOrEmpty(this.m_LocalizationKey.stringValue))
        //             {
        //                 this.m_UseLocalization.boolValue = true;
        //             }
        //         }
        //         else
        //         {
        //             Debug.LogError("检测到不包含中文无需转换");
        //         }
        //     }
        // }
        
        this.serializedObject.ApplyModifiedProperties();
    }
}


public class EmojiFontShaderEditor : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        
    }
}
