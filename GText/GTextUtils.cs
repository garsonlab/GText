using System;
using System.Collections.Generic;
using UnityEngine;

public static class GTextUtils
{
    static Dictionary<FontType, Font> _fonts = new Dictionary<FontType, Font>();
    public static Func<string, Font> fontLoadHandler;
    public static Func<string, string> languageHandler;

    public static Font GetFont(FontType fontType)
    {
        if (!_fonts.TryGetValue(fontType, out Font font))
        {
            var fontName = fontType.ToString().Replace("_", "-");
            if (fontLoadHandler != null)
                font = fontLoadHandler.Invoke(fontName);
            else
            {
                #if UNITY_EDITOR
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>($"Assets/LoadResources/Font/{fontName}.ttf");
                #endif
            }

            if (font != null)
                _fonts.Add(fontType, font);
        }

        return font;
    }

    public static string I18n(string key)
    {
        if (languageHandler != null)
            return languageHandler.Invoke(key);

#if UNITY_EDITOR
        
#endif
        return key;
    }
    
    public static FontType GetType(Font font)
    {
        string name = font.name.Replace('-', '_');
        FontType type = (FontType)Enum.Parse(typeof(FontType), name);
        return type;
    }
}