using TMPro;
using UnityEngine;

/// <summary>
/// 픽셀 글자의 1px 그림자. 원본 글자(source)를 복제한 어두운 글자가 원본의 문구 · 폰트 · 크기 · 배치를 따라가며 정수 1px 아래에 그려진다.
/// TMP 언더레이(머티리얼)를 쓰지 않는 이유: 언어별 폰트 교체(FontLocalizer)가 머티리얼을 바꾸고, 언더레이는 픽셀 글자에서 번진다.
/// 문구가 바뀔 때만 다시 넣는다(참조 비교라 매 프레임 비용이 거의 없다).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class UI_TextShadowFollower : MonoBehaviour
{
    private static readonly Vector2 ShadowOffset = new Vector2(0f, -1f);

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private TMP_Text source;
    [Tooltip("켜면 원본 문구의 <color=...> · </color> 태그를 지우고 그림자에 넣는다(그림자가 원본의 글자색을 따라 물들지 않게).")]
    [SerializeField] private bool stripColorTags;

    //내부 의존성
    private readonly System.Text.StringBuilder stripBuilder = new System.Text.StringBuilder(64);
    private TMP_Text shadow;
    private RectTransform shadowRect;

    //상태 변수
    private string lastText;
    private TMP_FontAsset lastFont;

    private void Sync()
    {
        if (null == source || null == shadow) return;

        if (false == ReferenceEquals(lastText, source.text))
        {
            lastText = source.text;
            shadow.text = true == stripColorTags ? StripColorTags(lastText) : lastText;
        }

        if (lastFont != source.font)
        {
            lastFont = source.font;
            shadow.font = source.font;
        }

        if (shadow.fontSize != source.fontSize) shadow.fontSize = source.fontSize;
        if (shadow.alignment != source.alignment) shadow.alignment = source.alignment;

        RectTransform from = source.rectTransform;
        shadowRect.anchorMin = from.anchorMin;
        shadowRect.anchorMax = from.anchorMax;
        shadowRect.pivot = from.pivot;
        shadowRect.sizeDelta = from.sizeDelta;
        shadowRect.anchoredPosition = from.anchoredPosition + ShadowOffset;

        bool bActive = source.gameObject.activeSelf;
        if (shadow.enabled != bActive) shadow.enabled = bActive;
    }

    // 문구가 바뀔 때만 불린다. <color=...>와 </color>만 지우고 나머지 태그는 그대로 둔다.
    private string StripColorTags(string _text)
    {
        if (string.IsNullOrEmpty(_text)) return _text;

        stripBuilder.Clear();
        int index = 0;
        while (index < _text.Length)
        {
            if ('<' == _text[index])
            {
                int end = _text.IndexOf('>', index);
                if (0 <= end)
                {
                    int length = end - index + 1;
                    bool bColorTag = (7 <= length && 0 == string.Compare(_text, index, "<color=", 0, 7, System.StringComparison.OrdinalIgnoreCase))
                        || (8 == length && 0 == string.Compare(_text, index, "</color>", 0, 8, System.StringComparison.OrdinalIgnoreCase));
                    if (true == bColorTag)
                    {
                        index = end + 1;
                        continue;
                    }
                }
            }
            stripBuilder.Append(_text[index]);
            index++;
        }
        return stripBuilder.ToString();
    }

    private void Awake()
    {
        shadow = GetComponent<TMP_Text>();
        shadowRect = (RectTransform)transform;
    }

    private void LateUpdate()
    {
        Sync();
    }
}
