/*
 * GText, Emoji and Hyper Link Solution for UGUI Text
 * by Garson
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class GText : Text, IPointerClickHandler
{
    static Dictionary<string, SpriteInfo> _emojiData;
    readonly StringBuilder _builder = new StringBuilder();
    readonly Dictionary<int, EmojiInfo> _emojis = new Dictionary<int, EmojiInfo>();
    readonly List<HrefInfo> _hrefs = new List<HrefInfo>();
    readonly List<Image> _images = new List<Image>();
    readonly List<RectTransform> _rects = new List<RectTransform>();
    readonly List<UnderlineInfo> _underlines = new List<UnderlineInfo>();
    readonly UIVertex[] _tempVerts = new UIVertex[4];
    readonly HrefClickEvent _hrefClickEvent = new HrefClickEvent();
    readonly MatchResult _matchResult = new MatchResult();
    static readonly string _regexTag = "\\[([0-9A-Za-z]+)((\\|[0-9]+){0,2})(##[0-9a-f]{6})?(#[^=\\]]+)?(=[^\\]]+)?\\]";
    static readonly string _regexEffect = "<material=u(#[0-9a-f]{6})?>(((?!</material>).)*)</material>";
    string _outputText = "";

    /// <summary>0x02 Fill Image，(Image, link) </summary>
    public Action<Image, string> EmojiFillHandler;
    /// <summary>0x03 Custom Fill (RectTransform, link)</summary>
    public Action<RectTransform, string> CustomFillHandler;
    
    /// <summary>Hyper Link Click Event</summary>
    public HrefClickEvent hrefClick
    {
        get { return _hrefClickEvent; }
    }
    public override float preferredWidth
    {
        get
        {
            var settings = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(_outputText, settings) / pixelsPerUnit;
        }
    }
    public override float preferredHeight
    {
        get
        {
            var settings = GetGenerationSettings(new Vector2(rectTransform.rect.size.x, 0.0f));
            return cachedTextGeneratorForLayout.GetPreferredHeight(_outputText, settings) / pixelsPerUnit;
        }
    }
    public override string text
    {
        get { return m_Text; }

        set
        {
            ParseText(value);
            base.text = value;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // only run in playing mode
        if(!Application.isPlaying)
            return;

        // load data
        if (_emojiData == null)
        {
            _emojiData = new Dictionary<string, SpriteInfo>();
            string emojiContent = Resources.Load<TextAsset>("emoji").text;
            string[] lines = emojiContent.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    string[] strs = lines[i].Split('\t');
                    SpriteInfo info = new SpriteInfo();
                    info.frame = int.Parse(strs[1]);
                    info.index = int.Parse(strs[2]);
                    _emojiData.Add(strs[0], info);
                }
            }
        }
    }
    
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;

        ParseText(m_Text);
        
        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        // Otherwise we can get issues like Case 619238.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate(_outputText, settings);

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        //Last 4 verts are always a new line... (\n)
        int vertCount = verts.Count - 4;

        // We have no verts to process just return (case 1037923)
        if (vertCount <= 0)
        {
            toFill.Clear();
            return;
        }
        
        Vector3 repairVec = new Vector3(0, fontSize * 0.1f);
        Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
        roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
        toFill.Clear();
        if (roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                _tempVerts[tempVertsIndex] = verts[i];
                _tempVerts[tempVertsIndex].position *= unitsPerPixel;
                _tempVerts[tempVertsIndex].position.x += roundingOffset.x;
                _tempVerts[tempVertsIndex].position.y += roundingOffset.y;
                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(_tempVerts);
            }
        }
        else
        {
            Vector2 uv = Vector2.zero;
            for (int i = 0; i < vertCount; ++i)
            {
                EmojiInfo info;
                int index = i / 4;
                int tempVertIndex = i & 3;

                if (_emojis.TryGetValue(index, out info))
                {
                    _tempVerts[tempVertIndex] = verts[i];
                    _tempVerts[tempVertIndex].position -= repairVec;
                    if (info.type == MatchType.Emoji)
                    {
                        uv.x = info.sprite.index;
                        uv.y = info.sprite.frame;
                        _tempVerts[tempVertIndex].uv0 += uv * 10;
                    }
                    else
                    {
                        if (tempVertIndex == 3)
                            info.texture.position = _tempVerts[tempVertIndex].position;
                        _tempVerts[tempVertIndex].position = _tempVerts[0].position;
                    }

                    _tempVerts[tempVertIndex].position *= unitsPerPixel;
                    if (tempVertIndex == 3)
                        toFill.AddUIVertexQuad(_tempVerts);
                }
                else
                {
                    _tempVerts[tempVertIndex] = verts[i];
                    _tempVerts[tempVertIndex].position *= unitsPerPixel;
                    if (tempVertIndex == 3)
                        toFill.AddUIVertexQuad(_tempVerts);
                }
            }
            ComputeBoundsInfo(toFill);
            DrawUnderLine(toFill);
        }

        m_DisableFontTextureRebuiltCallback = false;

        StartCoroutine(ShowImages());
    }

    void ParseText(string mText)
    {
        if (_emojiData == null || !Application.isPlaying)
        {
            _outputText = mText;
            return;
        }

        _builder.Length = 0;
        _emojis.Clear();
        _hrefs.Clear();
        _underlines.Clear();
        ClearImages();

        MatchCollection matches = Regex.Matches(mText, _regexTag);
        if (matches.Count > 0)
        {
            int textIndex = 0;
            int imgIdx = 0;
            int rectIdx = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                _matchResult.Parse(match, fontSize);

                switch (_matchResult.type)
                {
                    case MatchType.Emoji:
                    {
                        SpriteInfo info;
                        if (_emojiData.TryGetValue(_matchResult.title, out info))
                        {
                            _builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                            int temIndex = _builder.Length;

                            _builder.Append("<quad size=");
                            _builder.Append(_matchResult.height);
                            _builder.Append(" width=");
                            _builder.Append((_matchResult.width * 1.0f / _matchResult.height).ToString("f2"));
                            _builder.Append(" />");

                            _emojis.Add(temIndex, new EmojiInfo()
                            {
                                type = MatchType.Emoji,
                                sprite = info,
                                width = _matchResult.width,
                                height = _matchResult.height
                            });

                            if (_matchResult.hasUrl)
                            {
                                var hrefInfo = new HrefInfo()
                                {
                                    show = false,
                                    startIndex = temIndex * 4,
                                    endIndex = temIndex * 4 + 3,
                                    url = _matchResult.url,
                                    color = _matchResult.GetColor(color)
                                };
                                _hrefs.Add(hrefInfo);
                            }

                            textIndex = match.Index + match.Length;
                        }
                        break;
                    }
                    case MatchType.HyperLink:
                    {
                        _builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                        _builder.Append("<color=");
                        _builder.Append(_matchResult.GetHexColor(color));
                        _builder.Append(">");
                            
                        var href = new HrefInfo();
                        href.show = true;
                        href.startIndex = _builder.Length * 4;
                        _builder.Append(_matchResult.link);
                        href.endIndex = _builder.Length * 4 - 1;
                        href.url = _matchResult.url;
                        href.color = _matchResult.GetColor(color);

                        _hrefs.Add(href);
                        _underlines.Add(href);
                        _builder.Append("</color>");

                        textIndex = match.Index + match.Length;
                        break;
                    }
                    case MatchType.CustomFill:
                    case MatchType.Texture:
                    {
                        _builder.Append(mText.Substring(textIndex, match.Index - textIndex));

                        int temIndex = _builder.Length;

                        _builder.Append("<quad size=");
                        _builder.Append(_matchResult.height);
                        _builder.Append(" width=");
                        _builder.Append((_matchResult.width * 1.0f / _matchResult.height).ToString("f2"));
                        _builder.Append(" />");
                        
                        _emojis.Add(temIndex, new EmojiInfo()
                        {
                            type = _matchResult.type,
                            width = _matchResult.width,
                            height = _matchResult.height,
                            texture = new TextureInfo() {link = _matchResult.link, index = _matchResult.type == MatchType.Texture? imgIdx++ : rectIdx++}
                        });
                        if (_matchResult.hasUrl)
                        {
                            var hrefInfo = new HrefInfo()
                            {
                                show = false,
                                startIndex = temIndex * 4,
                                endIndex = temIndex * 4 + 3,
                                url = _matchResult.url,
                                color = _matchResult.GetColor(color)
                            };

                            _hrefs.Add(hrefInfo);
                            //_underlines.Add(hrefInfo);
                        }
                        
                        textIndex = match.Index + match.Length;
                        break;
                    }
                }
            }
            _builder.Append(mText.Substring(textIndex, mText.Length - textIndex));
            _outputText = _builder.ToString();
        }
        else
            _outputText = mText;

        matches = Regex.Matches(_outputText, _regexEffect);
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (match.Success && match.Groups.Count == 4)
            {
                string v1 = match.Groups[1].Value;
                Color lineColor;
                if(!string.IsNullOrEmpty(v1) && ColorUtility.TryParseHtmlString(v1, out lineColor)){ }
                else lineColor = color;

                var underline = new UnderlineInfo()
                {
                    show = true,
                    startIndex = match.Groups[2].Index * 4,
                    endIndex = match.Groups[2].Index * 4 + match.Groups[2].Length * 4 - 1,
                    color = lineColor
                };
                _underlines.Add(underline);
            }
        }
    }

    void ComputeBoundsInfo(VertexHelper toFill)
    {
        UIVertex vert = new UIVertex();
        for (int u = 0; u < _underlines.Count; u++)
        {
            var underline = _underlines[u];
            underline.boxes.Clear();
            if (underline.startIndex >= toFill.currentVertCount)
                continue;

            // Add hyper text vector index to bounds
            toFill.PopulateUIVertex(ref vert, underline.startIndex);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = underline.startIndex, m = underline.endIndex; i < m; i++)
            {
                if (i >= toFill.currentVertCount) break;

                toFill.PopulateUIVertex(ref vert, i);
                pos = vert.position;
                if (pos.x < bounds.min.x)
                {
                    //if in different lines
                    underline.boxes.Add(new Rect(bounds.min, bounds.size));
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(pos); //expand bounds
                }

            }
            //add bound
            underline.boxes.Add(new Rect(bounds.min, bounds.size));
        }
    }

    void DrawUnderLine(VertexHelper toFill)
    {
        if(_underlines.Count <= 0)
            return;

        Vector2 extents = rectTransform.rect.size;
        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate("_", settings);
        IList<UIVertex> uList = cachedTextGenerator.verts;
        float h = uList[2].position.y - uList[1].position.y;
        Vector3[] temVecs = new Vector3[4];
        
        for (int i = 0; i < _underlines.Count; i++)
        {
            var info = _underlines[i];
            if(!info.show)
                continue;

            for (int j = 0; j < info.boxes.Count; j++)
            {
                if (info.boxes[j].width <= 0 || info.boxes[j].height <= 0)
                    continue;

                temVecs[0] = info.boxes[j].min;
                temVecs[1] = temVecs[0] + new Vector3(info.boxes[j].width, 0);
                temVecs[2] = temVecs[0] + new Vector3(info.boxes[j].width, -h);
                temVecs[3] = temVecs[0] + new Vector3(0, -h);

                for (int k = 0; k < 4; k++)
                {
                    _tempVerts[k] = uList[k];
                    _tempVerts[k].color = info.color;
                    _tempVerts[k].position = temVecs[k];
                }

                toFill.AddUIVertexQuad(_tempVerts);
            }
        }

    }

    void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        for (int h = 0; h < _hrefs.Count; h++)
        {
            var hrefInfo = _hrefs[h];
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    _hrefClickEvent.Invoke(hrefInfo.url);
                    return;
                }
            }
        }
    }

    void ClearImages()
    {
        for (int i = 0; i < _images.Count; i++)
        {
            _images[i].rectTransform.localScale = Vector3.zero;
        }

        for (int i = 0; i < _rects.Count; i++)
        {
            _rects[i].localScale = Vector3.zero;
        }
    }

    IEnumerator ShowImages()
    {
        yield return null;
        foreach (var emojiInfo in _emojis.Values)
        {
            if (emojiInfo.type == MatchType.Texture)
            {
                emojiInfo.texture.image = GetImage(emojiInfo.texture, emojiInfo.width, emojiInfo.height);
                
                /* Test Data */
                Debug.Log("测试数据，正式由下方EmojiFillHandler完成");
                Sprite sprite = Resources.Load<Sprite>(emojiInfo.texture.link);
                emojiInfo.texture.image.sprite = sprite;
                /* Test Data */

                if (EmojiFillHandler != null)
                    EmojiFillHandler(emojiInfo.texture.image, emojiInfo.texture.link);
            }
            else if (emojiInfo.type == MatchType.CustomFill)
            {
                emojiInfo.texture.rect = GetRectTransform(emojiInfo.texture, emojiInfo.width, emojiInfo.height);

                /* Test Data */
                Debug.Log("测试数据，正式由下方CustomFillHandler完成");
                UnityEngine.Object prefab = Resources.Load(emojiInfo.texture.link);
                var obj = GameObject.Instantiate(prefab) as GameObject;
                var objRect = obj.transform as RectTransform;
                objRect.SetParent(emojiInfo.texture.rect);
                objRect.localScale = Vector3.one;
                objRect.anchoredPosition = Vector2.zero;
                /* Test Data */

                if (CustomFillHandler != null)
                    CustomFillHandler(emojiInfo.texture.rect, emojiInfo.texture.link);
            }
        }
    }

    Image GetImage(TextureInfo info, int width, int height)
    {
        Image img = null;
        if (_images.Count > info.index)
            img = _images[info.index];

        if (img == null)
        {
            GameObject obj = new GameObject("emoji_" + info.index);
            img = obj.AddComponent<Image>();
            obj.transform.SetParent(transform);
            img.rectTransform.pivot = Vector2.zero;
            img.raycastTarget = false;

            if(_images.Count > info.index)
                _images[info.index] = img;
            else
                _images.Add(img);
        }

        img.rectTransform.localScale = Vector3.one;
        img.rectTransform.sizeDelta = new Vector2(width, height);
        img.rectTransform.anchoredPosition = info.position;
        return img;
    }

    RectTransform GetRectTransform(TextureInfo info, int width, int height)
    {
        RectTransform rect = null;
        if (_rects.Count > info.index)
            rect = _rects[info.index];

        if (rect == null)
        {
            GameObject obj = new GameObject("custom_" + info.index);
            rect = obj.AddComponent<RectTransform>();
            obj.transform.SetParent(transform);
            rect.pivot = Vector2.zero;

            if (_rects.Count > info.index)
                _rects[info.index] = rect;
            else
                _rects.Add(rect);
        }
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = info.position;
        return rect;
    }

    [Serializable]
    public class HrefClickEvent : UnityEvent<string> { }

    class SpriteInfo
    {
        public int index;
        public int frame;
    }

    class TextureInfo
    {
        public int index;
        public Image image;
        public RectTransform rect;
        public Vector3 position;
        public string link;
    }

    class EmojiInfo
    {
        public MatchType type;
        public int width;
        public int height;
        public SpriteInfo sprite;
        public TextureInfo texture;
    }
    
    enum MatchType
    {
        None,
        Emoji,
        HyperLink,
        Texture,
        CustomFill,
    }

    class MatchResult
    {
        public MatchType type;
        public string title;
        public string url;
        public string link;
        public int height;
        public int width;
        private string color;
        private Color _color;
        public bool hasUrl
        {
            get { return !string.IsNullOrEmpty(url); }
        }

        void Reset()
        {
            type = MatchType.None;
            title = String.Empty;
            width = 0;
            height = 0;
            color = string.Empty;
            url = string.Empty;
            link = string.Empty;
        }
        
        public void Parse(Match match, int fontSize)
        {
            Reset();
            if(!match.Success || match.Groups.Count != 7)
                return;
            title = match.Groups[1].Value;
            if (match.Groups[2].Success)
            {
                string v = match.Groups[2].Value;
                string[] sp = v.Split('|');
                height = sp.Length > 1 ? int.Parse(sp[1]) : fontSize;
                width = sp.Length == 3 ? int.Parse(sp[2]) : height;
            }
            else
            {
                height = fontSize;
                width = fontSize;
            }

            if (match.Groups[4].Success)
            {
                color = match.Groups[4].Value.Substring(1);
            }

            if (match.Groups[5].Success)
            {
                url = match.Groups[5].Value.Substring(1);
            }

            if (match.Groups[6].Success)
            {
                link = match.Groups[6].Value.Substring(1);
            }

            if (title.Equals("0x01")) //hyper link
            {
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(link))
                {
                    type = MatchType.HyperLink;
                }
            }
            else if (title.Equals("0x02"))
            {
                if (!string.IsNullOrEmpty(link))
                {
                    type = MatchType.Texture;
                }
            }
            else if (title.Equals("0x03"))
            {
                if (!string.IsNullOrEmpty(link))
                {
                    type = MatchType.CustomFill;
                }
            }

            if (type == MatchType.None)
                type = MatchType.Emoji;
        }

        public Color GetColor(Color fontColor)
        {
            if (string.IsNullOrEmpty(color))
                return fontColor;
            ColorUtility.TryParseHtmlString(color, out _color);
            return _color;
        }

        public string GetHexColor(Color fontColor)
        {
            if (!string.IsNullOrEmpty(color))
                return color;
            return ColorUtility.ToHtmlStringRGBA(fontColor);
        }
    }

    class UnderlineInfo
    {
        public bool show;
        public int startIndex;
        public int endIndex;
        public Color color;
        public readonly List<Rect> boxes = new List<Rect>();
    }

    class HrefInfo : UnderlineInfo
    {
        public string url;
    }

}
