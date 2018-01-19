using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class GTextBuilder
{

    private static readonly Vector2[] AtlasSize = new Vector2[]
    {
        new Vector2(32, 32),
        new Vector2(64, 64),
        new Vector2(128, 128),
        new Vector2(256, 256),
        new Vector2(512, 512),
        new Vector2(1024, 1024),
        new Vector2(2048, 2048)
    };

    class AssetInfor
    {
        public int index;
        public Object obj;
    }

    class SpriteInfo
    {
        public string key;
        public string x;
        public string y;
        public string size;
    }

    static int EmojiSize = 32;


    [MenuItem("Assets/Build Emoji")]
    public static void BuildEmoji()
    {
        List<char> keylist = new List<char>();
        for (int i = 48; i <= 57; i++)
        {
            keylist.Add(System.Convert.ToChar(i)); //0-9
        }
        for (int i = 65; i <= 90; i++)
        {
            keylist.Add(System.Convert.ToChar(i)); //A-Z
        }
        for (int i = 97; i <= 122; i++)
        {
            keylist.Add(System.Convert.ToChar(i)); //a-z
        }


        int totalFrames = 0;
        Dictionary<string, List<AssetInfor>> sourceDic = new Dictionary<string, List<AssetInfor>>();
        Object[] textures = Selection.GetFiltered(typeof(Texture), SelectionMode.DeepAssets);
        //search all emojis and compute they frames.
        for (int i = 0; i < textures.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(textures[i]);
            if (!path.EndsWith(".png"))
                continue;
            string filename = Path.GetFileNameWithoutExtension(path);
            string[] t = filename.Split('_');
            string id = t[0];
            int index = 1;
            if (t.Length > 1)
                if (!int.TryParse(t[1], out index))
                {
                    Debug.LogError(path + " 's Name Error! You Should Name 'name_index'");
                    continue;
                }

            if (!sourceDic.ContainsKey(id))
            {
                sourceDic.Add(id, new List<AssetInfor>());
            }
            sourceDic[id].Add(new AssetInfor() { index = index, obj = textures[i] });
            totalFrames++;
        }
        List<string> keys = new List<string>(sourceDic.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            sourceDic[keys[i]].Sort(
                new Comparison<AssetInfor>((AssetInfor a, AssetInfor b) => a.index <= b.index ? 1 : 0));
        }

        Dictionary<string, SpriteInfo> emojiDic = new Dictionary<string, SpriteInfo>();
        Vector2 texSize = ComputeAtlasSize(totalFrames);
        Texture2D newTex = new Texture2D((int)texSize.x, (int)texSize.y, TextureFormat.ARGB32, false);
        for (int i = 0; i < newTex.width; i++)
        {
            //Set original texture alpha 0
            for (int j = 0; j < newTex.height; j++)
            {
                newTex.SetPixel(i, j, new Color(1, 1, 1, 0));
            }
        }
        int lineCount = (int)texSize.x / EmojiSize;
        Texture2D dataTex = new Texture2D(lineCount, (int)texSize.y / EmojiSize, TextureFormat.ARGB32, false);

        int x = 0, y = 0, keyindex = 0;
        foreach (string key in sourceDic.Keys)
        {
            var list = sourceDic[key];
            for (int i = 0; i < list.Count; i++)
            {
                Texture2D texture = list[i].obj as Texture2D;
                Color[] colors = texture.GetPixels(0, 0, EmojiSize, EmojiSize);
                newTex.SetPixels(x, y, EmojiSize, EmojiSize, colors);

                string t = System.Convert.ToString(sourceDic[key].Count - 1, 2);
                float r = 0, g = 0, b = 0;
                if (t.Length >= 3)
                {
                    r = t[2] == '1' ? 0.5f : 0;
                    g = t[1] == '1' ? 0.5f : 0;
                    b = t[0] == '1' ? 0.5f : 0;
                }
                else if (t.Length >= 2)
                {
                    r = t[1] == '1' ? 0.5f : 0;
                    g = t[0] == '1' ? 0.5f : 0;
                }
                else
                {
                    r = t[0] == '1' ? 0.5f : 0;
                }

                dataTex.SetPixel(x / EmojiSize, y / EmojiSize, new Color(r, g, b, 1));

                if (!emojiDic.ContainsKey(key))
                {
                    SpriteInfo info = new SpriteInfo();
                    if (keyindex < keylist.Count)
                    {
                        info.key = "[" + char.ToString(keylist[keyindex]) + "]";
                    }
                    else
                    {
                        info.key = "[" + char.ToString(keylist[keyindex / keylist.Count]) +
                                   char.ToString(keylist[keyindex % keylist.Count]) + "]";
                    }
                    info.x = (x * 1.0f / texSize.x).ToString();
                    info.y = (y * 1.0f / texSize.y).ToString();
                    info.size = (EmojiSize * 1.0f / texSize.x).ToString();

                    emojiDic.Add(key, info);
                    keyindex++;
                }

                x += EmojiSize;
                if (x >= texSize.x)
                {
                    x = 0;
                    y += EmojiSize;
                }
            }
        }

        string saveFolder = EditorUtility.SaveFolderPanel("Select Folder To Save Data", "Assets/", "");

        byte[] bytes1 = newTex.EncodeToPNG();
        string outputfile1 = saveFolder + "/emoji_tex.png";
        File.WriteAllBytes(outputfile1, bytes1);
        byte[] bytes2 = dataTex.EncodeToPNG();
        string outputfile2 = saveFolder + "/emoji_data.png";
        File.WriteAllBytes(outputfile2, bytes2);

        using (StreamWriter sw = new StreamWriter(saveFolder + "/Emoji.txt", false))
        {
            sw.WriteLine("Name\tKey\tFrames\tX\tY\tSize");
            foreach (string key in emojiDic.Keys)
            {
                sw.WriteLine("{" + key + "}\t" + emojiDic[key].key + "\t" + sourceDic[key].Count + "\t" + emojiDic[key].x + "\t" + emojiDic[key].y + "\t" + emojiDic[key].size);
            }
            sw.Close();
        }
        AssetDatabase.Refresh();
        FormatTexture("Assets/" + saveFolder.Split(new[] { "Assets/" }, StringSplitOptions.None)[1]);

        Material material = new Material(Shader.Find("UI/EmojiFont"));
        Texture2D emojiTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + outputfile1.Split(new[] { "Assets/" }, StringSplitOptions.None)[1]);
        material.SetTexture("_EmojiTex", emojiTex);

        Texture2D emojiData = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + outputfile2.Split(new[] { "Assets/" }, StringSplitOptions.None)[1]);
        material.SetTexture("_EmojiDataTex", emojiData);
        material.SetFloat("_EmojiSize", lineCount);
        AssetDatabase.CreateAsset(material, "Assets/" + saveFolder.Split(new[] { "Assets/" }, StringSplitOptions.None)[1] + "/Emoji.mat");
    }

    private static Vector2 ComputeAtlasSize(int count)
    {
        long total = count * EmojiSize * EmojiSize;
        for (int i = 0; i < AtlasSize.Length; i++)
        {
            if (total <= AtlasSize[i].x * AtlasSize[i].y)
            {
                return AtlasSize[i];
            }
        }
        return Vector2.zero;
    }

    private static void FormatTexture(string OutputPath)
    {
        TextureImporter emojiTex = AssetImporter.GetAtPath(OutputPath + "/emoji_tex.png") as TextureImporter;
        emojiTex.filterMode = FilterMode.Point;
        emojiTex.mipmapEnabled = false;
        emojiTex.sRGBTexture = true;
        emojiTex.alphaSource = TextureImporterAlphaSource.FromInput;
        emojiTex.SaveAndReimport();

        TextureImporter emojiData = AssetImporter.GetAtPath(OutputPath + "/emoji_data.png") as TextureImporter;
        emojiData.filterMode = FilterMode.Point;
        emojiData.mipmapEnabled = false;
        emojiData.sRGBTexture = false;
        emojiData.alphaSource = TextureImporterAlphaSource.None;
        emojiData.SaveAndReimport();
    }

    [MenuItem("GameObject/UI/Text->GText")]
    static void Text2GText()
    {
        UnityEngine.Object select = Selection.activeObject;
        if (select == null)
            return;
        GameObject target = select as GameObject;
        Text text = target.GetComponent<Text>();
        if (text == null)
            return;
        string content = text.text;
        Font font = text.font;
        FontStyle fs = text.fontStyle;
        int fontsize = text.fontSize;
        float lineSpacing = text.lineSpacing;
        Color color = text.color;

        GameObject.DestroyImmediate(text);
        GText gText = target.AddComponent<GText>();
        gText.text = content;
        gText.font = font;
        gText.fontSize = fontsize;
        gText.fontStyle = fs;
        gText.lineSpacing = lineSpacing;
        gText.color = color;
        gText.raycastTarget = true;
        gText.supportRichText = true;


        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/GText/Resources/Emoji.mat");
        if(material == null)
            Debug.LogError("Can not find material Use UI-EmojiFount ");
        else
            gText.material = material;

    }
}
