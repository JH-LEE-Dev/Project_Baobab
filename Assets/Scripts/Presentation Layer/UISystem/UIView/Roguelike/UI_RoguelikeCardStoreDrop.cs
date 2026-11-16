using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 보관 연출: 일반 카드는 슬리브(비닐 덮개)가 씌워진 뒤, 레어 카드는 케이스째, 카드가 아래로 뚝 떨어져 화면 밖(보관함 쪽)으로 사라진다. 모두 unscaled 시간.
/// UI_RoguelikeAcrylicCase와 같은 구조다: 시간에서 바로 계산하는 Evaluate + 자체 Update, 트윈 라이브러리 · LINQ · 람다 없음(호출당 힙 할당 없음).
///
/// [타임라인 - Play 시점부터 센다]
///   ① 슬리브 (일반만) 0.18  슬리브가 카드 위 34px에서 내려와 씌워진다: 위치 34 x (1 - easeOut(p / 0.8)), 알파 min(1, p x 3), p 0.8 이후 카드 · 슬리브가 1px 눌린다.
///   ② 아래로 뚝       0.30  p < 0.3: 위로 6 x easeOut(p / 0.3)px. 이후 -6 + 250 x ((p - 0.3) / 0.7)^2.2px 아래로(화면 밖). 레어는 케이스(받침 · 덮개)가 카드와 같이 떨어진다.
///                           ② 시작 순간 onDropStart 1회(화면이 딤을 걷기 시작), ② 끝에 onDone 1회(화면이 닫힌다).
///
/// [좌표] 슬롯(카드)과 슬리브는 같은 Content 좌표계다(앵커 가운데, 피벗 왼쪽 아래). 슬리브 54x86의 왼쪽 아래 = 슬롯 왼쪽 아래 + (-2, -2). 모든 위치는 정수 픽셀이다.
///        낙하 중 카드 위치는 이 컴포넌트가 슬롯의 anchoredPosition을 직접 옮긴다(슬롯의 PlayMove는 쓰지 않는다). 끝난 뒤에도 ResetDrop이 불릴 때까지 낙하 위치에 머문다.
/// </summary>
public class UI_RoguelikeCardStoreDrop : MonoBehaviour
{
    public const float SleeveSeconds = 0.18f;
    public const float DropSeconds = 0.3f;

    public const int SleeveWidth = 54;
    public const int SleeveHeight = 86;

    // 슬리브 왼쪽 아래가 카드(슬롯) 왼쪽 아래보다 밖으로 나온 거리.
    private const int SleeveMargin = 2;
    private const int SleeveStartHeightPixels = 34;
    private const float SleeveSettleRatio = 0.8f;
    private const float SleeveFadeInRate = 3f;
    private const int SleevePressPixels = 1;
    private const int RisePixels = 6;
    private const float RiseRatio = 0.3f;
    private const float FallPixels = 250f;
    private const float FallExponent = 2.2f;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform sleeveRoot;
    [SerializeField] private Image sleeveImage;

    //상태 변수
    private RectTransform cardRect;
    private UI_RoguelikeAcrylicCase caseRef;
    private Vector2 originPosition;
    private Action onDropStart;
    private Action onDone;
    private bool bPlaying;
    private bool bSleeve;
    private bool bDropStarted;
    private float time;
    private float appliedAlpha = -1f;

    /// <summary>슬리브를 숨긴 채로 준비한다(화면이 만들어질 때 한 번).</summary>
    public void Initialize()
    {
        ResetDrop();
    }

    /// <summary>
    /// 연출을 시작한다. _cardRect는 고른 슬롯, _case는 레어면 케이스(같이 떨어진다)이고 일반이면 null, _bSleeve는 일반 카드만 true(슬리브 단계를 거친다).
    /// _onDropStart는 ② 시작 순간 1회(슬리브가 없으면 Play 안에서 바로 불린다), _onDone은 ② 끝 1회.
    /// </summary>
    public void Play(RectTransform _cardRect, UI_RoguelikeAcrylicCase _case, bool _bSleeve, Action _onDropStart, Action _onDone)
    {
        ResetDrop();
        if (null == _cardRect) return;

        cardRect = _cardRect;
        caseRef = _case;
        originPosition = _cardRect.anchoredPosition;
        onDropStart = _onDropStart;
        onDone = _onDone;
        bSleeve = _bSleeve && null != sleeveRoot && null != sleeveImage;
        bDropStarted = false;
        bPlaying = true;
        time = 0f;

        if (true == bSleeve) sleeveRoot.gameObject.SetActive(true);
        Evaluate(0f);
    }

    /// <summary>연출을 끊고 슬리브를 숨기고 카드(슬롯) · 케이스를 Play 때 자리로 되돌린다(콜백은 부르지 않는다). 연출이 끝난 뒤에도 불러 낙하 위치를 되돌린다.</summary>
    public void ResetDrop()
    {
        bPlaying = false;
        bDropStarted = false;
        onDropStart = null;
        onDone = null;
        time = 0f;
        appliedAlpha = -1f;

        if (null != cardRect)
        {
            cardRect.anchoredPosition = originPosition;
            cardRect = null;
        }

        if (null != caseRef)
        {
            caseRef.SetCarryOffset(0);
            caseRef = null;
        }

        if (null != sleeveRoot) sleeveRoot.gameObject.SetActive(false);
    }

    // // 진행

    private float GetSleeveSeconds()
    {
        return true == bSleeve ? SleeveSeconds : 0f;
    }

    /// <summary>_time초 시점의 모습을 그린다(시간에서 바로 계산하므로 프레임이 튀어도 곡선이 어긋나지 않는다).</summary>
    private void Evaluate(float _time)
    {
        float sleeveSeconds = GetSleeveSeconds();
        float sleeveAlpha = 1f;
        int sleeveRise = 0;
        int press = 0;
        int lift = 0;

        if (_time < sleeveSeconds)
        {
            float p = _time / sleeveSeconds;
            sleeveAlpha = Mathf.Min(1f, p * SleeveFadeInRate);
            sleeveRise = Mathf.RoundToInt(SleeveStartHeightPixels * (1f - EaseOut(Mathf.Min(1f, p / SleeveSettleRatio))));
            if (SleeveSettleRatio <= p) press = SleevePressPixels;
        }
        else
        {
            float p = Mathf.Clamp01((_time - sleeveSeconds) / DropSeconds);
            if (p < RiseRatio)
            {
                lift = Mathf.RoundToInt(RisePixels * EaseOut(p / RiseRatio));
            }
            else
            {
                lift = RisePixels - Mathf.RoundToInt(FallPixels * Mathf.Pow((p - RiseRatio) / (1f - RiseRatio), FallExponent));
            }

            if (false == bDropStarted)
            {
                bDropStarted = true;
                Action callback = onDropStart;
                onDropStart = null;
                callback?.Invoke();
            }
        }

        int offsetY = lift - press;
        if (null != cardRect) cardRect.anchoredPosition = new Vector2(originPosition.x, originPosition.y + offsetY);
        if (null != caseRef) caseRef.SetCarryOffset(-offsetY);

        if (true == bSleeve)
        {
            sleeveRoot.anchoredPosition = new Vector2(originPosition.x - SleeveMargin, originPosition.y - SleeveMargin + offsetY + sleeveRise);

            if (appliedAlpha != sleeveAlpha)
            {
                appliedAlpha = sleeveAlpha;
                sleeveImage.color = new Color(1f, 1f, 1f, sleeveAlpha);
            }
        }
    }

    private void Finish()
    {
        Action callback = onDone;
        onDone = null;
        bPlaying = false;

        callback?.Invoke();
    }

    private static float EaseOut(float _t)
    {
        return 1f - (1f - _t) * (1f - _t);
    }

    // // 유니티 라이프사이클

    private void Update()
    {
        if (false == bPlaying) return;

        time += Time.unscaledDeltaTime;
        float total = GetSleeveSeconds() + DropSeconds;
        Evaluate(Mathf.Min(time, total));

        if (total <= time) Finish();
    }

    private void OnDestroy()
    {
        onDropStart = null;
        onDone = null;
    }
}
