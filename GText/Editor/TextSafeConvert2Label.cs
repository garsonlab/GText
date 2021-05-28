using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TextSafeConvert2Label : MonoBehaviour
{
    [MenuItem("Garson/LabelSafeConvertor")]
    static void SafeConvert()
    {
         var objs = Selection.gameObjects;

         if(objs == null || objs.Length <= 0)
             return;
         int index = 0;
        List<SerializedObject> cache = new List<SerializedObject>();

        EditorApplication.update = () =>
        {
            var prefab = objs[index++];
            string path = AssetDatabase.GetAssetPath(prefab);

            bool cancel = EditorUtility.DisplayCancelableProgressBar("Localization Label Replacing", path,
                index * 1.0f / objs.Length);
            
            var txts = prefab.GetComponentsInChildren<Text>(true);
            foreach (var txt in txts)
            {
                cache.Add(Convert(txt));
            }
            Apply(cache);

            var labels = prefab.GetComponentsInChildren<GText>(true);
            foreach (var label in labels)
            {
                // var langCom = label.GetComponent<LanguageComponent>();
                // if (langCom != null)
                // {
                //     label.useLocalization = true;
                //     label.localizationKey = langCom.Language;
                //     cache.Add(Trim(langCom));
                // }
                //
                // var langTxt = label.GetComponent<LanguageTextKey>();
                // if (langTxt != null)
                // {
                //     label.useLocalization = true;
                //     label.localizationKey = langTxt.key;
                //     cache.Add(Trim(langTxt));
                // }
            }
            Apply(cache);

            if (index >= objs.Length || cancel)
            {
                EditorApplication.update = null;
                EditorUtility.ClearProgressBar();
                Debug.Log($"Convert Success With {index} Prefab{(index>1? "s": "")}!");
            }
        };
    }

    static SerializedObject Convert(Text text)
    {
        var so = new SerializedObject(text);
        so.Update();

        bool enable = text.enabled;
        Font font = text.font;

        text.enabled = false;
        // Find MonoScript of the specified component.
        foreach (var script in Resources.FindObjectsOfTypeAll<MonoScript>())
        {
            if (script.GetClass() != typeof(GText))
                continue;

            // Set 'm_Script' to convert.
            so.FindProperty("m_Script").objectReferenceValue = script;
            so.ApplyModifiedProperties();
            break;
        }
        
        GText gText = so.targetObject as GText;
        gText.font = null;
        gText.fontType = GTextUtils.GetType(font);
        gText.enabled = enable;

        return so;
    }
    
    
    static void ConvertTo<T>(Object context) where T : MonoBehaviour
    {
        var target = context as MonoBehaviour;
        var so = new SerializedObject(target);
        so.Update();

        bool oldEnable = target.enabled;
        target.enabled = false;

        // Find MonoScript of the specified component.
        foreach (var script in Resources.FindObjectsOfTypeAll<MonoScript>())
        {
            if (script.GetClass() != typeof(T))
                continue;

            // Set 'm_Script' to convert.
            so.FindProperty("m_Script").objectReferenceValue = script;
            so.ApplyModifiedProperties();
            break;
        }

        (so.targetObject as MonoBehaviour).enabled = oldEnable;
    }

    static SerializedObject Trim<T>(T com) where T: MonoBehaviour
    {
        SerializedObject so = new SerializedObject(com.gameObject);
        SerializedProperty prop = so.FindProperty("m_Component");
        // 这里再次找组件，只是为了找到目标组件在GameObject上挂载的位置
        Component[] coms = com.gameObject.GetComponents<Component>();
        for (int i = 0; i < coms.Length; ++i)
        {
            if (coms[i] == com)
            {
                prop.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        return so;
    }
    
    static void Apply(List<SerializedObject> list)
    {
        foreach (var so in list)
        {
            so.ApplyModifiedProperties();
        }
        list.Clear();
    }
}
