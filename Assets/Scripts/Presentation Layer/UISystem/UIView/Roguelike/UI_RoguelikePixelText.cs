using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// 기록 창 값 글자의 기준선을 정수 픽셀에 맞춘다(서브 픽셀 방지 - 계획서 1-2).
/// TMP의 가운데(Midline) 정렬은 폰트의 위 · 아래 높이 평균으로 세로 위치를 정해 기준선이 반 픽셀에 떨어질 수 있다.
/// 그래서 글자는 위쪽 정렬(TopLeft / TopRight)로 두고, 기준선 = 세로 가운데(centerY) + 대문자 높이의 절반(반올림)이 되도록 상자 윗변을 계산한다.
///   위쪽 정렬의 첫 줄 기준선 = 상자 윗변 + 폰트 ascentLine(UI_RoguelikeChallengeRow.MeasureAscent와 같은 계산)이라 윗변이 소수여도 기준선은 정수가 된다.
/// 언어 변경으로 폰트가 바뀌면(LocalizedFontTracker) 다음 프레임에 다시 맞춘다. 가로 위치(anchoredPosition.x)는 건드리지 않는다.
/// </summary>
public class UI_RoguelikePixelText : MonoBehaviour
{
    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private TMP_Text text;
    [SerializeField, Tooltip("글자의 세로 가운데(부모 왼쪽 위 기준, 아래쪽 + px, 정수).")] private float centerY;

    //상태 변수
    private TMP_FontAsset appliedFont;
    private float appliedSize;

    /// <summary>현재 폰트 기준으로 상자 윗변을 다시 계산한다.</summary>
    public void Apply()
    {
        if (null == text || null == text.font) return;

        FaceInfo face = text.font.faceInfo;
        if (0f >= face.pointSize) return;

        float scale = text.fontSize / face.pointSize * face.scale;
        float ascent = face.ascentLine * scale;
        float cap = face.capLine * scale;
        float baseline = centerY + Mathf.Round(cap * 0.5f);

        RectTransform rect = text.rectTransform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(baseline - ascent));
        appliedFont = text.font;
        appliedSize = text.fontSize;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (null == text) return;
        if (text.font != appliedFont || text.fontSize != appliedSize) Apply();
    }
}
