/*
 * GText for Unity By Garson(https://github.com/garsonlab)
 * -------------------------------------------------------------------
 * FileName: GText
 * Date    : 2017/06/24
 * Version : v1.0
 * Describe: A Unity UGUI Text Component Extend, Support Emoji and Hyper Link.
 *              Base On https://github.com/zouchunyi/EmojiText
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UGUI Emoji & Hyper Link Component
/// </summary>
public class GText : Text, IPointerClickHandler
{
    #region Members
    static Dictionary<string, SpriteInfo> EmojiIndex = null;
    Dictionary<int, SpriteInfo> emojis = new Dictionary<int, SpriteInfo>();
    List<HrefInfo> hrefs = new List<HrefInfo>();
    readonly UIVertex[] m_TempVerts = new UIVertex[4];

    public delegate void HrefClickHandler(string arg);
    private event HrefClickHandler m_OnHrefClick = null;//can use toLua directly, so not UnityEvent.
    #endregion

    #region Private
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;

        if (EmojiIndex == null)
            LoadEmojiData();

        //m_Text = ParseTextInTag(m_Text);
        ParseTextTag(m_Text);

        #region Rebuilt
        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate(text, settings);

        Rect inputRect = rectTransform.rect;

        // get the text alignment anchor point for the text in local space
        Vector2 textAnchorPivot = GetTextAnchorPivot(alignment);
        Vector2 refPoint = Vector2.zero;
        refPoint.x = Mathf.Lerp(inputRect.xMin, inputRect.xMax, textAnchorPivot.x);
        refPoint.y = Mathf.Lerp(inputRect.yMin, inputRect.yMax, textAnchorPivot.y);

        // Determine fraction of pixel to offset text mesh.
        Vector2 roundingOffset = PixelAdjustPoint(refPoint) - refPoint;

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        //Last 4 verts are always a new line...
        int vertCount = verts.Count - 4;

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
            float repairDistance = 0;
            float repairDistanceHalf = 0;
            float repairY = 0;
            if (vertCount > 0)
            {
                repairY = verts[3].position.y;
            }
            for (int i = 0; i < vertCount; ++i)
            {
                SpriteInfo info;
                int index = i / 4;
                if (emojis.TryGetValue(index, out info))
                {
                    //compute the distance of '[' and get the distance of emoji 
                    float charDis = (verts[i + 1].position.x - verts[i].position.x) * 3;
                    m_TempVerts[3] = verts[i];//1
                    m_TempVerts[2] = verts[i + 1];//2
                    m_TempVerts[1] = verts[i + 2];//3
                    m_TempVerts[0] = verts[i + 3];//4

                    //the real distance of an emoji
                    m_TempVerts[2].position += new Vector3(charDis, 0, 0);
                    m_TempVerts[1].position += new Vector3(charDis, 0, 0);

                    //make emoji has equal width and height
                    float fixValue = (m_TempVerts[2].position.x - m_TempVerts[3].position.x - (m_TempVerts[2].position.y - m_TempVerts[1].position.y));
                    m_TempVerts[2].position -= new Vector3(fixValue, 0, 0);
                    m_TempVerts[1].position -= new Vector3(fixValue, 0, 0);

                    float curRepairDis = 0;
                    if (verts[i].position.y < repairY)
                    {// to judge current char in the same line or not
                        repairDistance = repairDistanceHalf;
                        repairDistanceHalf = 0;
                        repairY = verts[i + 3].position.y;
                    }
                    curRepairDis = repairDistance;
                    int dot = 0;//repair next line distance
                    for (int j = info.len - 1; j > 0; j--)
                    {
                        if (i + j * 4 + 3 >= verts.Count)//Fixed [AB] tag length is 4 but verts count is 12
                            continue;

                        if (verts[i + j * 4 + 3].position.y >= verts[i + 3].position.y)
                        {
                            repairDistance += verts[i + j * 4 + 1].position.x - m_TempVerts[2].position.x;
                            break;
                        }
                        else
                        {
                            dot = i + 4 * j;

                        }
                    }
                    if (dot > 0)
                    {
                        int nextChar = i + info.len * 4;
                        if (nextChar < verts.Count)
                        {
                            repairDistanceHalf = verts[nextChar].position.x - verts[dot].position.x;
                        }
                    }

                    //repair its distance
                    for (int j = 0; j < 4; j++)
                    {
                        m_TempVerts[j].position -= new Vector3(curRepairDis, 0, 0);
                    }

                    m_TempVerts[0].position *= unitsPerPixel;
                    m_TempVerts[1].position *= unitsPerPixel;
                    m_TempVerts[2].position *= unitsPerPixel;
                    m_TempVerts[3].position *= unitsPerPixel;

                    float pixelOffset = emojis[index].size / 32 / 2;
                    m_TempVerts[0].uv1 = new Vector2(emojis[index].x + pixelOffset, emojis[index].y + pixelOffset);
                    m_TempVerts[1].uv1 = new Vector2(emojis[index].x - pixelOffset + emojis[index].size, emojis[index].y + pixelOffset);
                    m_TempVerts[2].uv1 = new Vector2(emojis[index].x - pixelOffset + emojis[index].size, emojis[index].y - pixelOffset + emojis[index].size);
                    m_TempVerts[3].uv1 = new Vector2(emojis[index].x + pixelOffset, emojis[index].y - pixelOffset + emojis[index].size);

                    toFill.AddUIVertexQuad(m_TempVerts);

                    i += 4 * info.len - 1;
                }
                else
                {
                    int tempVertsIndex = i & 3;
                    if (tempVertsIndex == 0 && verts[i].position.y < repairY)
                    {
                        repairY = verts[i + 3].position.y;
                        repairDistance = repairDistanceHalf;
                        repairDistanceHalf = 0;
                    }
                    m_TempVerts[tempVertsIndex] = verts[i];
                    m_TempVerts[tempVertsIndex].position -= new Vector3(repairDistance, 0, 0);
                    m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                    if (tempVertsIndex == 3)
                        toFill.AddUIVertexQuad(m_TempVerts);
                }
            }


            CalcBoundsInfo(verts, toFill);
        }
        m_DisableFontTextureRebuiltCallback = false;
        #endregion
    }

    /// <summary>
    /// Load emoji data, you can overwrite this segment code base on your project.
    /// </summary>
    void LoadEmojiData()
    {
        EmojiIndex = new Dictionary<string, SpriteInfo>();

        TextAsset emojiContent = Resources.Load<TextAsset>("Emoji");
        if(emojiContent == null)
            return;

        string[] lines = emojiContent.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                string[] strs = lines[i].Split('\t');
                SpriteInfo info = new SpriteInfo();
                info.x = float.Parse(strs[3]);
                info.y = float.Parse(strs[4]);
                info.size = float.Parse(strs[5]);
                info.len = 0;
                EmojiIndex.Add(strs[1], info);
            }
        }
    }

    [Obsolete("Custom href format: [#ShowChars=ClickResult]. Get preferSize worked in next frame.")]
    string ParseTextInTag(string inputText)
    {
        StringBuilder stringBuilder = new StringBuilder();
        StringBuilder hrefBuilder = new StringBuilder();//Distinguish sprite,one sprite equals one char
        int indexText = 0;
        MatchCollection matchs = Regex.Matches(inputText, "\\[(([a-z0-9A-Z]+)|(#(.+?)=(.+?)))\\]");

        for (int i = 0; i < matchs.Count; i++)
        {
            var match = matchs[i];
            var tmpText = inputText.Substring(indexText, matchs[i].Index - indexText);
            stringBuilder.Append(tmpText);
            hrefBuilder.Append(tmpText);
            SpriteInfo info;
            if (EmojiIndex.TryGetValue(match.Value, out info))
            {
                info.len = match.Length;
                emojis.Add(stringBuilder.Length, info);
                stringBuilder.Append(match.Value);
                hrefBuilder.Append("W");
            }
            else
            {
                if (match.Value.Contains("="))
                {
                    stringBuilder.Append("[");
                    hrefBuilder.Append("[");
                    var hrefInfo = new HrefInfo
                    {
                        startIndex = (hrefBuilder.Length - 1) * 4, // hyperLink vecs start index
                        endIndex = (hrefBuilder.Length + match.Groups[4].Length + 1) * 4 + 3,
                        url = match.Groups[5].Value
                    };
                    hrefs.Add(hrefInfo);
                    stringBuilder.Append(match.Groups[4].Value);
                    stringBuilder.Append("]");
                    hrefBuilder.Append(match.Groups[4].Value);
                    hrefBuilder.Append("]");
                }
                else
                {
                    stringBuilder.Append(match.Value);
                    hrefBuilder.Append(match.Value);
                }
            }
            indexText = match.Index + match.Length;
        }
        stringBuilder.Append(inputText.Substring(indexText, inputText.Length - indexText));
        hrefBuilder.Append(inputText.Substring(indexText, inputText.Length - indexText));
        return hrefBuilder.ToString();
    }

    /// <summary>
    /// Parse Text with tag
    /// </summary>
    /// <param name="inputText"></param>
    void ParseTextTag(string inputText)
    {
        hrefs.Clear();//clear cache
        emojis.Clear();

        MatchCollection matchs = Regex.Matches(inputText, "\\[[a-z0-9A-Z]+\\]");
        for (int i = 0; i < matchs.Count; i++)
        {
            var match = matchs[i];
            SpriteInfo info;
            if (EmojiIndex.TryGetValue(match.Value, out info))
            {
                info.len = match.Length;
                emojis.Add(match.Index, info);
            }
        }

        matchs = Regex.Matches(inputText, @"<material=([^>]*)>(.+?)</material>");
        for (int i = 0; i < matchs.Count; i++)
        {
            var match = matchs[i];
            var hrefInfo = new HrefInfo
            {
                startIndex = match.Index * 4, // hyperLink vecs start index
                endIndex = match.Index * 4 + (match.Length - 1) * 4 + 3,
                url = match.Groups[1].Value
            };
            hrefs.Add(hrefInfo);
        }
    }

    // Hyper Bounds
    void CalcBoundsInfo(IList<UIVertex> verts, VertexHelper toFill)
    {
        UIVertex vert = new UIVertex();
        for (int h = 0; h < hrefs.Count; h++)
        {
            var hrefInfo = hrefs[h];
            hrefInfo.boxes.Clear();
            if (hrefInfo.startIndex >= toFill.currentVertCount)
            {
                continue;
            }

            // Add hyper text vector index to bounds
            toFill.PopulateUIVertex(ref vert, hrefInfo.startIndex);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i++)
            {
                if (i >= toFill.currentVertCount)
                {
                    break;
                }

                toFill.PopulateUIVertex(ref vert, i);
                pos = vert.position;
                if (pos.x < bounds.min.x)
                {
                    //if in different lines
                    hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(pos); //expand bounds
                }
            }
            //add bound
            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }
    }
    // Pointer Click
    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        for (int h = 0; h < hrefs.Count; h++)
        {
            var hrefInfo = hrefs[h];
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    if (m_OnHrefClick != null)
                        m_OnHrefClick(hrefInfo.url);
                    return;
                }
            }
        }
    }
    #endregion

    #region Public Methods
    public void AddHrefListener(HrefClickHandler callback)
    {
        m_OnHrefClick -= callback;
        m_OnHrefClick += callback;
    }
    public void RemoveHrefListener(HrefClickHandler callback)
    {
        m_OnHrefClick -= callback;
    }
    public void RemoveHrefListeners()
    {
        m_OnHrefClick = null;
    }
    #endregion

    #region Private Class
    /// <summary>
    /// Sprite Info
    /// </summary>
    class SpriteInfo
    {
        public float x;
        public float y;
        public float size;
        public int len;
    }

    /// <summary>
    /// Hyper Info
    /// </summary>
    class HrefInfo
    {
        public int startIndex;
        public int endIndex;
        public string url;
        public readonly List<Rect> boxes = new List<Rect>();
    }
    #endregion
}
