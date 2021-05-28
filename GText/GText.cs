using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum FontType
{
    Fangzheng,
    Minghei,
}

public class GText : Text, IPointerClickHandler
{
    private static Dictionary<string, EmojiData> m_EmojiData;
    private static Dictionary<string, EmojiData> emojiDatas
    {
        get
        {
            if (m_EmojiData == null)
            {
                m_EmojiData = new Dictionary<string, EmojiData>();
#if UNITY_EDITOR
                var emojiContent = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Test/GText/output/emoji.txt").text;
                string[] lines = emojiContent.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        string[] strs = lines[i].Split('\t');
                        EmojiData info = new EmojiData();
                        info.frame = int.Parse(strs[1]);
                        info.index = int.Parse(strs[2]);
                        m_EmojiData.Add(strs[0], info);
                    }
                }
#endif
            }

            return m_EmojiData;
        }
    }
    [SerializeField] private bool m_UseLocalization;
    [SerializeField] private string m_LocalizationKey;
    [SerializeField] private FontType m_FontType;
    [SerializeField] private bool m_TiltEffect;
    [SerializeField] private float m_TiltAngle;
    private readonly UIVertex[] m_TempVerts = new UIVertex[4];
    private readonly Vector3[] m_TempVecs = new Vector3[4];
    private readonly List<BoxInfo> m_Boxs = new List<BoxInfo>();
    private readonly Dictionary<int, SpriteInfo> m_Sprites = new Dictionary<int, SpriteInfo>();
    private readonly List<Image> m_Images = new List<Image>();
    private readonly List<RectTransform> m_Rects = new List<RectTransform>();
    private readonly List<TextureData> m_Clears = new List<TextureData>();
    [SerializeField] private HrefClickEvent m_HrefClickEvent = new HrefClickEvent();
    private static readonly Regex m_Regex = new Regex("<a\\s([^>]+)>([^<>]*)</a>");
    private static readonly Regex m_Attribute = new Regex("([a-z]+)=([^\\s=]+)");

    /// <summary>Fill Image (Image, url)</summary>
    public Action<Image, string> spriteFillHandler;
    /// <summary>Fill Custom (RectTransform, url)</summary>
    public Action<RectTransform, string> customFillHandler;
    /// <summary>Clear Custom (RectTransform, url)</summary>
    public Action<RectTransform, string> customClearHandler;
    
#if UNITY_EDITOR
    private FontType m_LastFontType;
    private Font m_LastFont;
    private bool m_LastUseLocalization;
    private string m_LastLocalizationKey;
#endif
    
    private string m_OutputText;
    private MatchResult m_MatchResult;
    private StringBuilder m_builder;

    /// <summary>是否使用多语言</summary>
    public bool useLocalization
    {
        get { return this.m_UseLocalization; }
        set
        {
            if(this.m_UseLocalization == value)
                return;

            this.m_UseLocalization = value;
            this.text = GTextUtils.I18n(this.m_LocalizationKey);
            // this.SetVerticesDirty();
            // this.SetLayoutDirty();
        }
    }
    /// <summary>多语言Key</summary>
    public string localizationKey
    {
        get { return this.m_LocalizationKey; }
        set
        {
            if(this.m_LocalizationKey.Equals(value))
                return;

            this.m_LocalizationKey = value;

            if (this.m_UseLocalization)
            {
                this.text = GTextUtils.I18n(value);
                // this.SetVerticesDirty();
                // this.SetLayoutDirty();
            }
        }
    }
    /// <summary>字体类型</summary>
    public FontType fontType
    {
        get { return this.m_FontType; }
        set
        {
            if(this.m_FontType == value)
                return;

            this.m_FontType = value;
            #if UNITY_EDITOR
            this.m_LastFontType = value;
            #endif
            this.CheckFont();
        }
    }
    /// <summary>倾斜排列</summary>
    public bool tiltEffect
    {
        get { return this.m_TiltEffect; }
        set
        {
            if(this.m_TiltEffect == value)
                return;
            this.m_TiltEffect = value;
            this.SetLayoutDirty();
            this.SetVerticesDirty();
        }
    }
    /// <summary>倾斜角度</summary>
    public float tiltAngle
    {
        get { return this.m_TiltAngle; }
        set
        {
            if(this.m_TiltAngle == value)
                return;
            this.m_TiltAngle = value;
            if (this.m_TiltEffect)
            {
                this.SetLayoutDirty();
                this.SetVerticesDirty();
            }
        }
    }
    
    public override float preferredWidth
    {
        get
        {
            var settings = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(this.m_OutputText, settings) / pixelsPerUnit;
        }
    }
    
    public override float preferredHeight
    {
        get
        {
            var settings = GetGenerationSettings(new Vector2(rectTransform.rect.size.x, 0.0f));
            return cachedTextGeneratorForLayout.GetPreferredHeight(this.m_OutputText, settings) / pixelsPerUnit;
        }
    }

    public override Material material
    {
        get
        {
            if (this.m_Material == null)
            {
                
            }

            return this.m_Material;
        }
        set { this.m_Material = value; }
    }

    public override string text
    {
        get
        {
            return this.m_Text;
        }
        set
        {
            this.Parse(value);
            base.text = value;
        }
    }
    
    public HrefClickEvent hrefClick
    {
        get { return this.m_HrefClickEvent; }
    }

    
    protected override void Awake()
    {
        base.Awake();
        this.CheckFont();
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;
        Parse(this.m_Text);
        
        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        // Otherwise we can get issues like Case 619238.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.PopulateWithErrors(this.m_OutputText, settings, gameObject);

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        int vertCount = verts.Count;

        // We have no verts to process just return (case 1037923)
        if (vertCount <= 0)
        {
            toFill.Clear();
            return;
        }

        Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
        roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
        toFill.Clear();
        if (roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                m_TempVerts[tempVertsIndex] = verts[i];
                m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                m_TempVerts[tempVertsIndex].position.x += roundingOffset.x;
                m_TempVerts[tempVertsIndex].position.y += roundingOffset.y;
                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(m_TempVerts);
            }
        }
        else
        {
            Vector3 repairVec = new Vector3(0, fontSize * 0.1f);
            Vector2 uv = Vector2.zero;
            float start = verts[0].position.y;
            for (int i = 0; i < vertCount; ++i)
            {
                int index = i / 4;
                int tempVertIndex = i & 3;

                m_TempVerts[tempVertIndex] = verts[i];
                if (this.m_Sprites.TryGetValue(index, out SpriteInfo info))
                {
                    m_TempVerts[tempVertIndex].position -= repairVec;
                    if (info.type == MatchType.Emoji)
                    {
                        uv.x = info.emoji.index;
                        uv.y = info.emoji.frame;
                        m_TempVerts[tempVertIndex].uv0 += uv * 10;
                    }
                    else
                    {
                        if (tempVertIndex == 3)
                            info.texture.position = m_TempVerts[tempVertIndex].position;
                        m_TempVerts[tempVertIndex].position = m_TempVerts[0].position;
                    }
                }
                
                m_TempVerts[tempVertIndex].position *= unitsPerPixel;
                if (tempVertIndex == 3)
                {
                    if (this.m_TiltEffect)
                    {
                        float offset = Mathf.Tan(this.m_TiltAngle * Mathf.Deg2Rad) * (this.m_TempVerts[0].position.y-start);
                        for (int j = 0; j < 4; j++)
                        {
                            this.m_TempVerts[j].position.x += offset;
                        }
                    }
                    toFill.AddUIVertexQuad(this.m_TempVerts);
                }
            }
            
            ComputeBounds(toFill);
            DrawUnderLine(toFill);
        }

        m_DisableFontTextureRebuiltCallback = false;
        
        StartCoroutine(ShowImages());
    }

    void ComputeBounds(VertexHelper toFill)
    {
        if(this.m_Boxs.Count <= 0)
            return;

        int vertCount = toFill.currentVertCount;
        UIVertex vert = new UIVertex();
        for (int b = 0; b < this.m_Boxs.Count; b++)
        {
            var boxInfo = this.m_Boxs[b];
            if (boxInfo.startIndex >= vertCount)
                continue;
            
            toFill.PopulateUIVertex(ref vert, boxInfo.startIndex);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = boxInfo.startIndex; i < boxInfo.endIndex; i++)
            {
                if(i >= vertCount) break;
                
                toFill.PopulateUIVertex(ref vert, i);
                if (i % 4 == 0 && Mathf.Abs(vert.position.y - pos.y) > fontSize * 0.5f)
                {
                    boxInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    pos = vert.position;
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(vert.position);
                }
            }
            boxInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }
        
    }
    void DrawUnderLine(VertexHelper toFill)
    {
        if(this.m_Boxs.Count <= 0 || cachedTextGenerator.lineCount <= 0)
            return;

        float h = cachedTextGenerator.lines[0].height * 0.1f;//this.m_BoxInfo[0].boxes[0].height * 0.1f;
        
        for (int i = 0; i < this.m_Boxs.Count; i++)
        {
            var info = this.m_Boxs[i];
            if(!info.showLine)
                continue;

            for (int j = 0; j < info.boxes.Count; j++)
            {
                if (info.boxes[j].width <= 0 || info.boxes[j].height <= 0)
                    continue;

                this.m_TempVecs[0] = info.boxes[j].min + new Vector2(0, h*info.linePos);
                this.m_TempVecs[1] = this.m_TempVecs[0] + new Vector3(info.boxes[j].width, 0);
                this.m_TempVecs[2] = this.m_TempVecs[0] + new Vector3(info.boxes[j].width, -h);
                this.m_TempVecs[3] = this.m_TempVecs[0] + new Vector3(0, -h);

                for (int k = 0; k < 4; k++)
                {
                    this.m_TempVerts[k] = UIVertex.simpleVert;
                    this.m_TempVerts[k].color = info.color;
                    this.m_TempVerts[k].position = this.m_TempVecs[k];
                    this.m_TempVerts[k].uv0 = Vector2.down;
                }

                toFill.AddUIVertexQuad(this.m_TempVerts);
            }
        }

    }
    
    private void CheckFont()
    {
        var myFont = GTextUtils.GetFont(this.m_FontType);
        if (myFont != this.font)
        {
            this.font = myFont;
        }
    }

    private StringBuilder CacheBuilder()
    {
        if(this.m_builder == null)
            this.m_builder = new StringBuilder(this.m_Text.Length+30);
        if (this.m_builder.Capacity < this.m_Text.Length)
            this.m_builder.Capacity = this.m_Text.Length + 30;
        this.m_builder.Clear();
        return this.m_builder;
    }
    
    private void Parse(string mText)
    {
        this.m_Boxs.Clear();
        this.m_Sprites.Clear();
        ClearImages();
        
        var matchs = m_Regex.Matches(mText);
        if (matchs.Count <= 0)
        {
            this.m_OutputText = mText;
            return;
        }

        var builder = this.CacheBuilder();
        int textIndex = 0, imgIdx = 0, rectIdx = 0;
        foreach (Match match in matchs)
        {
            this.m_MatchResult.Parse(match, fontSize, color);
            
                switch (this.m_MatchResult.type)
                {
                    case MatchType.Emoji:
                    {
                        if (emojiDatas.TryGetValue(this.m_MatchResult.title, out EmojiData data))
                        {
                            builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                            int temIndex = builder.Length;

                            builder.Append("<quad size=");
                            builder.Append(this.m_MatchResult.height);
                            builder.Append(" width=");
                            builder.Append((this.m_MatchResult.width * 1.0f / this.m_MatchResult.height).ToString("f2"));
                            builder.Append(" />");

                            var info = SpriteInfo.ParseEmoji(this.m_MatchResult, data);
                            this.m_Sprites.Add(temIndex, info);

                            if (this.m_MatchResult.isLink || this.m_MatchResult.underline)
                            {
                                var box = new BoxInfo(this.m_MatchResult);
                                box.startIndex = temIndex * 4;
                                box.endIndex = temIndex * 4 + 3;
                                
                                this.m_Boxs.Add(box);
                            }

                            textIndex = match.Index + match.Length;
                        }
                        break;
                    }
                    case MatchType.None:
                    case MatchType.HyperLink:
                    {
                        if (this.m_MatchResult.type == MatchType.None && !this.m_MatchResult.underline)
                        {
                            Debug.LogWarning($"{match.Value} type:None underline:false");
                        }
                        else
                        {
                            builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                            builder.Append("<color=");
                            builder.Append(this.m_MatchResult.hexColor);
                            builder.Append(">");

                            var info = new BoxInfo(this.m_MatchResult);
                            info.startIndex = builder.Length * 4;
                            builder.Append(this.m_MatchResult.title);
                            info.endIndex = builder.Length * 4 - 1;

                            this.m_Boxs.Add(info);
                            builder.Append("</color>");

                            textIndex = match.Index + match.Length;
                        }
                        break;
                    }
                    case MatchType.Custom:
                    case MatchType.Texture:
                    {
                        builder.Append(mText.Substring(textIndex, match.Index - textIndex));

                        int temIndex = builder.Length;

                        builder.Append("<quad size=");
                        builder.Append(m_MatchResult.height);
                        builder.Append(" width=");
                        builder.Append((m_MatchResult.width * 1.0f / m_MatchResult.height).ToString("f2"));
                        builder.Append(" />");

                        var info = SpriteInfo.ParseTexture(this.m_MatchResult, this.m_MatchResult.type == MatchType.Texture ? imgIdx++: rectIdx++);
                        m_Sprites.Add(temIndex, info);

                        if (this.m_MatchResult.isLink || this.m_MatchResult.underline)
                        {
                            var box = new BoxInfo(this.m_MatchResult);
                            box.startIndex = temIndex * 4;
                            box.endIndex = temIndex * 4 + 3;
                                
                            this.m_Boxs.Add(box);
                        }
                        
                        textIndex = match.Index + match.Length;
                        break;
                    }
                }
        }
        
        builder.Append(mText.Substring(textIndex));
        this.m_OutputText = builder.ToString();
    }

    void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp);

        for (int h = 0; h < this.m_Boxs.Count; h++)
        {
            var hrefInfo = this.m_Boxs[h];
            if(!hrefInfo.isLink) continue;
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    this.m_HrefClickEvent.Invoke(hrefInfo.link);
                    return;
                }
            }
        }
    }
    
    void ClearImages()
    {
        foreach (var emoji in this.m_Sprites.Values)
        {
            if (emoji.type == MatchType.Custom)
                this.m_Clears.Add(emoji.texture);
        }
        
        for (int i = 0; i < m_Images.Count; i++)
        {
            m_Images[i].rectTransform.localScale = Vector3.zero;
            m_Images[i].sprite = null;
        }

        for (int i = 0; i < m_Rects.Count; i++)
        {
            m_Rects[i].localScale = Vector3.zero;
        }
    }
    
    IEnumerator ShowImages()
    {
        yield return null;
        
        if(!Application.isPlaying || !gameObject.activeInHierarchy)
            yield break;

        if (this.customClearHandler != null && this.m_Clears.Count > 0)
        {
            foreach (var data in this.m_Clears)
                this.customClearHandler.Invoke(data.rect, data.url);
        }
        m_Clears.Clear();
        
        for (int i = 0; i < this.m_Sprites.Count; i++)
        {
            var spriteInfo = this.m_Sprites[i];
            if (spriteInfo.type == MatchType.Texture)
            {
                spriteInfo.texture.image = GetImage(spriteInfo.texture, spriteInfo.width, spriteInfo.height);
                
                /* Test Data */
                Debug.Log("测试数据，正式由下方EmojiFillHandler完成");
                Sprite sprite = Resources.Load<Sprite>(spriteInfo.texture.url);
                spriteInfo.texture.image.sprite = sprite;
                /* Test Data */

                this.spriteFillHandler?.Invoke(spriteInfo.texture.image, spriteInfo.texture.url);
            }
            else if (spriteInfo.type == MatchType.Custom)
            {
                spriteInfo.texture.rect = GetRectTransform(spriteInfo.texture, spriteInfo.width, spriteInfo.height);

                /* Test Data */
                Debug.Log("测试数据，正式由下方CustomFillHandler完成");
                UnityEngine.Object prefab = Resources.Load(spriteInfo.texture.url);
                var obj = GameObject.Instantiate(prefab) as GameObject;
                var objRect = obj.transform as RectTransform;
                objRect.SetParent(spriteInfo.texture.rect);
                objRect.localScale = Vector3.one;
                objRect.anchoredPosition = Vector2.zero;
                /* Test Data */

                this.customFillHandler?.Invoke(spriteInfo.texture.rect, spriteInfo.texture.url);
            }
        }
    }

    Image GetImage(TextureData data, int width, int height)
    {
        Image img = null;
        if (this.m_Images.Count > data.index)
            img = this.m_Images[data.index];

        if (img == null)
        {
            GameObject obj = new GameObject("emoji_" + data.index);
            img = obj.AddComponent<Image>();
            obj.transform.SetParent(transform);
            img.rectTransform.pivot = Vector2.zero;
            img.raycastTarget = false;

            if(this.m_Images.Count > data.index)
                this.m_Images[data.index] = img;
            else
                this.m_Images.Add(img);
        }

        img.rectTransform.localScale = Vector3.one;
        img.rectTransform.sizeDelta = new Vector2(width, height);
        img.rectTransform.anchoredPosition = data.position;
        return img;
    }

    RectTransform GetRectTransform(TextureData data, int width, int height)
    {
        RectTransform rect = null;
        if (this.m_Rects.Count > data.index)
            rect = this.m_Rects[data.index];

        if (rect == null)
        {
            GameObject obj = new GameObject("custom_" + data.index);
            rect = obj.AddComponent<RectTransform>();
            obj.transform.SetParent(transform);
            rect.pivot = Vector2.zero;

            if (this.m_Rects.Count > data.index)
                this.m_Rects[data.index] = rect;
            else
                this.m_Rects.Add(rect);
        }
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = data.position;
        return rect;
    }
    
#if UNITY_EDITOR
    
    protected override void OnValidate()
    {
        if (this.m_LastUseLocalization && !this.m_LastLocalizationKey.Equals(this.m_LocalizationKey))
        {
            this.m_Text = GTextUtils.I18n(this.m_LocalizationKey);
            this.m_LastLocalizationKey = this.m_LocalizationKey;
        }
        
        if (this.m_LastUseLocalization != this.m_UseLocalization)
        {
            this.m_LastUseLocalization = this.m_UseLocalization;
            if (this.m_UseLocalization)
                this.m_Text = GTextUtils.I18n(this.m_LocalizationKey);
        }
        
        if (this.m_LastFontType != this.m_FontType || this.m_LastFont != font)
        {
            this.CheckFont();
            this.m_LastFontType = this.m_FontType;
            this.m_LastFont = font;
        }
        base.OnValidate();
    }
    
#endif

    enum MatchType
    {
        None = 0,
        Emoji = 1,
        HyperLink = 2,
        Texture = 4,
        Custom = 8
    }
    
    struct MatchResult
    {
        #region TYPE
    
        private const string EMOJI = "emoji";
        private const string LINK = "link";
        private const string TEXTURE = "trxture";
        private const string CUSTOM = "custom";
        
        #endregion
        
        #region ATTRIBUTE
        
        private const string TYPE = "type";//link, emoji, texture
        private const string COLOR = "color";
        private const string HREF = "href";
        private const string WIDTH = "width";
        private const string HEIGHT = "height";
        private const string URL = "url";
        private const string UNDERLINE = "underline";

        private const int TITLEINDEX = 1;
        private const int ATTRIBUTEINDEX = 2;
        
        #endregion
        
        public MatchType type;
        public string title;
        public string url;
        public int width;
        public int height;
        public string hexColor;
        public Color color;
        public bool isLink;
        public string link;
        public bool underline;
        public int underlinePos;

        void Reset()
        {
            this.type = MatchType.None;
            this.title = String.Empty;
            this.url = String.Empty;
            this.width = 0;
            this.height = 0;
            this.hexColor = String.Empty;
            this.color = Color.clear;
            this.link = String.Empty;
            this.isLink = false;
            this.underline = false;
            this.underlinePos = 1;
        }
        
        public void Parse(Match match, int size, Color color)
        {
            this.Reset();
            this.color = color;
            this.title = match.Groups[ATTRIBUTEINDEX].Value;
            var attrs = m_Attribute.Matches(match.Groups[TITLEINDEX].Value);
            foreach (Match attr in attrs)
            {
                switch (attr.Groups[TITLEINDEX].Value)
                {
                    case TYPE:
                        switch (attr.Groups[ATTRIBUTEINDEX].Value)
                        {
                            case EMOJI:
                                this.type = MatchType.Emoji;
                                break;
                            case LINK:
                                this.type = MatchType.HyperLink;
                                break;
                            case TEXTURE:
                                this.type = MatchType.Texture;
                                break;
                            case CUSTOM:
                                this.type = MatchType.Custom;
                                break;
                        }
                        break;
                    case COLOR:
                        this.hexColor = attr.Groups[ATTRIBUTEINDEX].Value;
                        ColorUtility.TryParseHtmlString(attr.Groups[ATTRIBUTEINDEX].Value, out this.color);
                        break;
                    case HREF:
                        this.link = attr.Groups[ATTRIBUTEINDEX].Value;
                        this.isLink = true;
                        break;
                    case WIDTH:
                        int.TryParse(attr.Groups[ATTRIBUTEINDEX].Value, out this.width);
                        break;
                    case HEIGHT:
                        int.TryParse(attr.Groups[ATTRIBUTEINDEX].Value, out this.height);
                        break;
                    case URL:
                        this.url = attr.Groups[ATTRIBUTEINDEX].Value;
                        break;
                    case UNDERLINE:
                        this.underline = true;
                        if (int.TryParse(attr.Groups[ATTRIBUTEINDEX].Value, out this.underlinePos))
                            this.underlinePos = Mathf.Clamp(this.underlinePos, 0, 9);
                        else
                            this.underlinePos = 1;
                        break;
                }
            }

            if (this.isLink && this.type == MatchType.None)
                this.type = MatchType.HyperLink;

            if (this.height <= 0)
                this.height = size;
            if (this.width <= 0)
                this.width = this.height;
        }

    }
    
    struct BoxInfo
    {
        public bool showLine;
        public bool isLink;
        public int startIndex;
        public int endIndex;
        public int linePos;
        public string link;
        public Color color;
        public List<Rect> boxes;

        public BoxInfo(MatchResult result)
        {
            this.showLine = result.underline;
            this.isLink = result.isLink;
            this.startIndex = 0;
            this.endIndex = 0;
            this.linePos = result.underlinePos;
            this.link = result.link;
            this.color = result.color;
            this.boxes = new List<Rect>();
        }
    }

    struct TextureData
    {
        public int index;
        public Image image;
        public RectTransform rect;
        public Vector3 position;
        public string url;
    }
    
    struct SpriteInfo
    {
        public MatchType type;
        public int width;
        public int height;
        public EmojiData emoji;
        public TextureData texture;

        private static SpriteInfo Parse(MatchResult result)
        {
            var info = new SpriteInfo();
            info.type = result.type;
            info.width = result.width;
            info.height = result.height;
            return info;
        }

        public static SpriteInfo ParseEmoji(MatchResult result, EmojiData emoji)
        {
            var info = Parse(result);
            info.type = MatchType.Emoji;
            info.emoji = emoji;
            return info;
        }

        public static SpriteInfo ParseTexture(MatchResult result, int idx)
        {
            var info = Parse(result);
            info.texture = new TextureData()
            {
                url = result.url,
                index = idx
            };
            return info;
        }
    }

    class EmojiData
    {
        public int index;
        public int frame;
    }
    
    [Serializable]
    public class HrefClickEvent : UnityEvent<string> { }
}
