#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class GTextBuilder
{
    private const string emojiMat = "Assets/GText/output/emoji.mat";

    [MenuItem("Tools/Emoji Build")]
    static void Build()
    {
        Dictionary<string, List<AssetInfo>> dic = new Dictionary<string, List<AssetInfo>>();
        Texture2D[] textures = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);

        // get all select textures
        int totalFrames = 0;
        int size = 0;
        foreach (var texture in textures)
        {
            Match match = Regex.Match(texture.name, "^([a-zA-Z0-9]+)(_([0-9]+))?$");//name_idx; name
            if (!match.Success)
            {
                Debug.LogWarning(texture.name +" 不匹配命名规则，跳过.");
                continue;
            }
            string title = match.Groups[1].Value;
            int index;
            if (!int.TryParse(match.Groups[3].Value, out index))
                index = 1;


            List<AssetInfo> infos;
            if (!dic.TryGetValue(title, out infos))
            {
                infos = new List<AssetInfo>();
                dic.Add(title, infos);
            }
            infos.Add(new AssetInfo(){index = index, texture = texture});

            if (texture.width > size)
                size = texture.width;
            if (texture.height > size)
                size = texture.width;

            totalFrames++;
        }

        // sort frames
        foreach (var info in dic.Values)
        {
            info.Sort(new Comparison<AssetInfo>((a, b) => a.index <= b.index ? 1 : 0));
        }

        // compute atlas size, support n*n only
        int lineCount = 0;
        int texSize = ComputeAtlasSize(totalFrames, ref size, ref lineCount);
        if (texSize < 1)
        {
            Debug.LogError("未能构建合适大小的图集，退出.");
            return;
        }

        // sort keys
        var keys = dic.Keys.ToList();
        keys.Sort(new Comparison<string>((a, b)=> String.Compare(a, b, StringComparison.Ordinal)));

        // build atlas
        List<SpriteInfo> sprites = new List<SpriteInfo>();
        Texture2D atlas = new Texture2D(texSize, texSize);
        int idx = 0;
        foreach (var key in keys)
        {
            sprites.Add(new SpriteInfo(key, dic[key].Count, idx));
            foreach (var assetInfo in dic[key])
            {
                int w = assetInfo.texture.width;
                int h = assetInfo.texture.height;

                int x = idx % lineCount;
                int y = idx / lineCount;

                Color[] colors = assetInfo.texture.GetPixels(0, 0, w, h);
                atlas.SetPixels(x*size, y*size, w, h, colors);

                idx++;
            }
        }

        // build emoji config
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Key\tFrame\tIndex");
        foreach (var spriteInfo in sprites)
        {
            builder.AppendLine(spriteInfo.ToString());
        }

        // select save folder
        string pngPath = EditorUtility.SaveFilePanelInProject("Select Save Path", "emoji", "png", "");
        if(string.IsNullOrEmpty(pngPath))
            return;
        

        byte[] bytes = atlas.EncodeToPNG();
        File.WriteAllBytes(pngPath, bytes);
        File.WriteAllText(pngPath.Replace(".png", ".txt"), builder.ToString());
        AssetDatabase.ImportAsset(pngPath);

        // create material
        Material material = new Material(Shader.Find("UI/EmojiFont"));
        Texture2D emojiTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        material.SetTexture("_EmojiTex", emojiTex);
        material.SetFloat("_EmojiSize", size*1.0f/texSize);
        material.SetFloat("_LineCount", lineCount);
        AssetDatabase.CreateAsset(material, pngPath.Replace(".png", ".mat"));

        AssetDatabase.Refresh();
    }

    [MenuItem("GameObject/UI/GText")]
    static void Create()
    {
        GameObject select = Selection.activeGameObject;
        if (select == null)
            return;
        RectTransform transform = select.GetComponent<RectTransform>();
        if(transform == null)
            return;

        GameObject obj = new GameObject("GText");
        obj.transform.SetParent(transform);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(160, 30);

        obj.AddComponent<CanvasRenderer>();
        GText text = obj.AddComponent<GText>();
        text.text = "New GText";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(emojiMat);
        text.material = material;

        Selection.activeGameObject = obj;

	/*
        Canvas[] canvas = rect.GetComponentsInParent<Canvas>();
        foreach (var cv in canvas)
        {
            cv.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
	*/
    }

    static int ComputeAtlasSize(int count, ref int size, ref int x)
    {
        size = GetWrapSize(size);

        int total = count * size * size;
        for (int i = 5; i < 12; i++)
        {
            int w = (int)Mathf.Pow(2, i);
            if (total <= w * w)
            {
                x = w / size;
                return w;
            }
        }

        return 0;
    }

    static int GetWrapSize(int size)
    {
        //最大图集2048
        for (int i = 0; i < 12; i++)
        {
            int s = (int) Mathf.Pow(2, i);
            if (s >= size)
                return s;
        }

        return 0;
    }
    
    class AssetInfo
    {
        public int index;
        public Texture2D texture;
    }
    class SpriteInfo
    {
        public string title;
        public int frame;
        public int index;

        public SpriteInfo(string title, int frame, int index)
        {
            this.title = title;
            this.frame = frame;
            this.index = index;
        }

        public override string ToString()
        {
            return String.Format(title + "\t" + frame + "\t" + index);
        }
    }
}


#endif
