using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창의 웨이브 길(30칸). 칸 Image 30개를 상태별 스프라이트로 바꿔 그린다(색을 물들이지 않는다 - 팻 픽셀 · 중간색 방지).
///   큰 길(스포트라이트): 칸 7x7, 10 · 20 · 30번째 칸은 7x9(위로 2px). 작은 길(순위 줄): 칸 3x5.
///   켜진 칸 = 금(언리밋 비교 막대는 민트), 꺼진 칸 = 파인 홈, 마지막 칸이 30 미만이면 끝 결과색(게임오버 빨강 · 포기 흰색), 막 켜진 칸 = 흰 칸.
/// 칸 위치 · 크기와 바탕 홈은 빌더(RoguelikeLobbyUIBuilder)가 정수 픽셀로 놓는다. 이 컴포넌트는 스프라이트만 바꾼다.
/// </summary>
public class UI_RoguelikeWaveTrail : MonoBehaviour
{
    public const int PipCount = 30;

    public enum EEnd
    {
        None = 0,
        Clear = 1,
        Over = 2,
        Retire = 3,
    }

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image[] pips = new Image[PipCount];
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite goldSprite;
    [SerializeField] private Sprite redSprite;
    [SerializeField] private Sprite whiteSprite;
    [SerializeField] private Sprite mintSprite;
    [SerializeField] private Sprite headSprite;
    [SerializeField, Tooltip("10단위 칸(7x9) 스프라이트. 작은 길은 비워 둔다.")] private Sprite offTallSprite;
    [SerializeField] private Sprite goldTallSprite;
    [SerializeField] private Sprite redTallSprite;
    [SerializeField] private Sprite whiteTallSprite;
    [SerializeField] private Sprite headTallSprite;
    [SerializeField, Tooltip("빈 기록일 때 길 전체를 흐리게 한다.")] private CanvasGroup group;

    /// <summary>
    /// _shown칸까지 켜서 그린다. _end는 마지막 켜진 칸이 30 미만일 때 그 칸의 결과색, _head는 막 켜진 칸 번호(-1이면 없음).
    /// _bBlinkOff가 참이면 게임오버 칸을 잠깐 금색으로 둔다(큰 길의 깜빡임).
    /// </summary>
    public void Render(int _shown, EEnd _end, bool _bMint, int _head, bool _bBlinkOff)
    {
        for (int i = 0; pips.Length > i; i++)
        {
            Image pip = pips[i];
            if (null == pip) continue;

            bool bTall = null != offTallSprite && 9 == i % 10;
            Sprite sprite = PickSprite(i, _shown, _end, _bMint, _head, _bBlinkOff, bTall);
            if (pip.sprite != sprite) pip.sprite = sprite;
        }
    }

    public void SetGhost(bool _bGhost)
    {
        if (null != group) group.alpha = true == _bGhost ? 0.45f : 1f;
    }

    private Sprite PickSprite(int _index, int _shown, EEnd _end, bool _bMint, int _head, bool _bBlinkOff, bool _bTall)
    {
        if (_index == _head) return true == _bTall ? headTallSprite : (null != headSprite ? headSprite : whiteSprite);
        if (_index >= _shown) return true == _bTall ? offTallSprite : offSprite;

        bool bLast = _index == _shown - 1 && PipCount > _shown && false == _bMint;
        if (true == bLast && EEnd.Over == _end && false == _bBlinkOff) return true == _bTall ? redTallSprite : redSprite;
        if (true == bLast && EEnd.Retire == _end) return true == _bTall ? whiteTallSprite : whiteSprite;
        if (true == _bMint) return mintSprite;
        return true == _bTall ? goldTallSprite : goldSprite;
    }
}
