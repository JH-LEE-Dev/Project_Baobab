using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레어 카드(에메랄드 · 프리즘 · 루비)를 고르면 카드가 아크릴 케이스에 끼워지는 연출. 기획서 v3.0 10-2 "케이스 연출".
/// 이미 닫힌 케이스 위쪽에 카드가 반쯤 걸친 채 시작해 → 아래로 쏙 끼우고 → 딸깍(반동 · 번쩍 · 입자) → 광택이 지나간다. 모두 unscaled 시간.
///
/// [타임라인 - 이 컴포넌트가 Play 시점부터 센다. 일반 카드의 이동(0.3초)과 같은 시점에 시작한다]
///   ① Enter  0.30  케이스(받침 + 덮개 한 덩어리)가 6px 아래에서 투명 → 불투명으로 떠오른다(ease-out). 카드 이동은 슬롯(PlayMove)이 맡는다 -
///                  호출 쪽(UIView_RoguelikeCardSelect)이 카드를 '최종 위치보다 InsertDepthPixels 위'로 보낸다.
///   ② Ready  0.12  카드를 3px 들어 올린다(ease-out, 힘 모으기).
///   ③ Plunge 0.09  카드가 최종 위치까지 내리꽂힌다(p^2.6).
///   ④ Snap   0.25  30fps 틱 반동: 케이스는 아래로 2·2·1·0·-1·-1·0px, 카드는 거기에 0·-2·-2·-1·0·0·0px(위로 튕김)이 더해진다.
///                  이 순간 효과음, 케이스 모양 흰 번쩍(0.12초), 입자 8개.
///   ⑤ Shine  0.35  광택 띠가 받침 실루엣 안에서만 왼쪽 → 오른쪽(가산 2배 밝기).
///   ⑥ Hold   0.15  정지. 끝나면 onDone(= 화면이 퇴장 연출 뒤 닫는다).
///
/// [레이어] 카드는 고른 슬롯 안에 있어 케이스 한 장으로 앞뒤를 샌드위치할 수 없다. 그래서 둘로 나눠 같은 Content 아래에 둔다.
///   backRoot(Content 맨 뒤 형제)  받침 - 모든 슬롯 뒤.  frontRoot(Content 맨 앞 형제)  덮개 · 번쩍 · 광택(Mask) · 입자 - 모든 슬롯 앞.
///   이 시점에는 고르지 않은 카드가 이미 투명이라 다른 슬롯과 겹칠 일이 없다. 슬롯의 부모·형제 순서는 건드리지 않는다.
/// [정렬] 케이스 56x87의 왼쪽 아래 = 카드(슬롯) 왼쪽 아래 + (-3, -3). 카드 그림은 슬롯 안 (1, 1)~(49, 80)의 48x79라서 받침 안쪽 4px에 선다.
/// [위치] 모든 위치는 정수 픽셀이다(Content 기준 앵커 가운데 좌표계 - 슬롯과 같다). 카드(슬롯) 위치는 Ready부터 이 컴포넌트가 직접 옮기고 끝나면 최종 위치다.
///        중간에 닫히면(ResetCase) 슬롯을 Play 때의 원래 자리로 되돌린다. 트윈 라이브러리를 쓰지 않아 호출당 힙 할당이 없다.
/// </summary>
public class UI_RoguelikeAcrylicCase : MonoBehaviour
{
    /// <summary>카드가 케이스에 반쯤 걸친 채 시작하는 높이(최종 위치보다 위 px). 호출 쪽이 카드 이동 목표를 이 값으로 잡는다.</summary>
    public const int InsertDepthPixels = 40;

    public const int CaseWidth = 56;
    public const int CaseHeight = 87;
    // 케이스 왼쪽 아래가 카드(슬롯) 왼쪽 아래보다 밖으로 나온 거리.
    private const int CaseMargin = 3;

    private const float EnterSeconds = 0.3f;
    private const float ReadySeconds = 0.12f;
    private const float PlungeSeconds = 0.09f;
    private const float SnapSeconds = 0.25f;
    private const float ShineSeconds = 0.35f;
    private const float HoldSeconds = 0.15f;
    private const float ReadyEnd = EnterSeconds + ReadySeconds;
    private const float PlungeEnd = ReadyEnd + PlungeSeconds;
    private const float SnapEnd = PlungeEnd + SnapSeconds;
    private const float ShineEnd = SnapEnd + ShineSeconds;
    private const float TotalSeconds = ShineEnd + HoldSeconds;

    private const int EnterDropPixels = 6;
    private const int ReadyLiftPixels = 3;
    private const float PlungeExponent = 2.6f;
    private const float TicksPerSecond = 30f;
    private const float FlashSeconds = 0.12f;
    private const float FlashFullSeconds = 0.04f;
    private const float FlashPeakAlpha = 0.55f;
    private const int ShineFromX = -93;
    private const int ShineToX = 56;

    // 착! 반동(30fps 틱). 화면 아래 방향이 +. 카드는 케이스 반동에 CardKicks(위로 튕김 = -)가 더해진다.
    private static readonly int[] CaseDips = { 2, 2, 1, 0, -1, -1, 0 };
    private static readonly int[] CardKicks = { 0, -2, -2, -1, 0, 0, 0 };

    //내부 의존성 (프리팹에서 연결)
    [Header("뒤 (받침 - 카드 뒤)")]
    [SerializeField] private RectTransform backRoot;
    [SerializeField] private Image baseImage;

    [Header("앞 (덮개 · 번쩍 · 광택 · 입자 - 카드 앞)")]
    [SerializeField] private RectTransform frontRoot;
    [SerializeField] private Image lidImage;
    [SerializeField] private Image flashImage;
    [Tooltip("받침 실루엣 Mask. 광택 띠가 도는 동안만 켠다.")]
    [SerializeField] private RectTransform shineRoot;
    [SerializeField] private Image shineImage;
    [SerializeField] private UI_RoguelikeAcrylicSparkle sparkle;

    //상태 변수
    private RectTransform cardRect;
    private Vector2 originPosition;
    private Vector2 finalPosition;
    private Action onDone;
    private bool bPlaying;
    private bool bSnapped;
    private float time;
    private float appliedAlpha = -1f;

    /// <summary>케이스를 숨긴 채로 준비한다(화면이 만들어질 때 한 번).</summary>
    public void Initialize()
    {
        ResetCase();
    }

    /// <summary>
    /// 연출을 시작한다. _cardRect는 고른 카드 슬롯, _finalPosition은 카드가 끼워졌을 때의 위치(Content 기준 정수 좌표).
    /// 시작 시점의 카드는 _finalPosition보다 InsertDepthPixels 위로 이동 중이어야 한다(슬롯 PlayMove). 끝나면 _onDone을 한 번 부른다.
    /// </summary>
    public void Play(RectTransform _cardRect, Vector2 _finalPosition, Action _onDone)
    {
        ResetCase();

        cardRect = _cardRect;
        originPosition = _cardRect.anchoredPosition;
        finalPosition = _finalPosition;
        onDone = _onDone;
        bPlaying = true;
        time = 0f;

        backRoot.gameObject.SetActive(true);
        frontRoot.gameObject.SetActive(true);
        Evaluate(0f);
    }

    /// <summary>연출을 끊고 케이스를 숨긴다. 진행 중이었다면 카드(슬롯)를 Play 때의 자리로 되돌린다(완료 콜백은 부르지 않는다).</summary>
    public void ResetCase()
    {
        if (true == bPlaying && null != cardRect) cardRect.anchoredPosition = originPosition;

        bPlaying = false;
        bSnapped = false;
        cardRect = null;
        onDone = null;
        time = 0f;
        appliedAlpha = -1f;

        if (null != flashImage) flashImage.enabled = false;
        if (null != shineRoot) shineRoot.gameObject.SetActive(false);
        if (null != sparkle) sparkle.Clear();
        if (null != backRoot) backRoot.gameObject.SetActive(false);
        if (null != frontRoot) frontRoot.gameObject.SetActive(false);
    }

    // // 진행

    /// <summary>_time초 시점의 모습을 그린다(시간에서 바로 계산하므로 프레임이 튀어도 곡선이 어긋나지 않는다).</summary>
    private void Evaluate(float _time)
    {
        float caseAlpha = 1f;
        int caseDrop = 0;
        int cardUp = 0;
        bool bDriveCard = true;
        float since = -1f;
        int tick = -1;

        if (_time < EnterSeconds)
        {
            float eased = EaseOut(_time / EnterSeconds);
            caseAlpha = eased;
            caseDrop = Mathf.RoundToInt(EnterDropPixels * (1f - eased));
            bDriveCard = false;
        }
        else if (_time < ReadyEnd)
        {
            float eased = EaseOut((_time - EnterSeconds) / ReadySeconds);
            cardUp = InsertDepthPixels + Mathf.RoundToInt(ReadyLiftPixels * eased);
        }
        else if (_time < PlungeEnd)
        {
            float eased = Mathf.Pow((_time - ReadyEnd) / PlungeSeconds, PlungeExponent);
            cardUp = Mathf.RoundToInt((InsertDepthPixels + ReadyLiftPixels) * (1f - eased));
        }
        else
        {
            since = _time - PlungeEnd;
            tick = Mathf.FloorToInt(since * TicksPerSecond);
            if (tick < CaseDips.Length)
            {
                caseDrop = CaseDips[tick];
                cardUp = -(CardKicks[tick] + CaseDips[tick]);
            }

            if (false == bSnapped)
            {
                bSnapped = true;
                // [효과음 지점] 레어 케이스 끼움 - 전용 SoundID 필요(Application Layer)
                Sound.PlayUI(SoundID.AbilityFinalUpgrade);
            }
        }

        if (true == bDriveCard && null != cardRect)
        {
            cardRect.anchoredPosition = new Vector2(finalPosition.x, finalPosition.y + cardUp);
        }

        Vector2 casePosition = new Vector2(finalPosition.x - CaseMargin, finalPosition.y - CaseMargin - caseDrop);
        backRoot.anchoredPosition = casePosition;
        frontRoot.anchoredPosition = casePosition;

        if (appliedAlpha != caseAlpha)
        {
            appliedAlpha = caseAlpha;
            Color tint = new Color(1f, 1f, 1f, caseAlpha);
            baseImage.color = tint;
            lidImage.color = tint;
        }

        ApplyFlash(since);
        ApplyShine(_time);
        sparkle.SetFrame(tick);
    }

    /// <summary>딸깍 직후 케이스(받침) 모양 그대로 흰색 가산 번쩍: 0.04초까지 최대, 이후 0.08초 동안 0으로.</summary>
    private void ApplyFlash(float _since)
    {
        if (0f > _since || _since >= FlashSeconds)
        {
            if (true == flashImage.enabled) flashImage.enabled = false;
            return;
        }

        float alpha = _since < FlashFullSeconds ? 1f : 1f - (_since - FlashFullSeconds) / (FlashSeconds - FlashFullSeconds);
        flashImage.enabled = true;
        flashImage.color = new Color(1f, 1f, 1f, FlashPeakAlpha * alpha);
    }

    /// <summary>광택 띠가 받침 실루엣 안에서 왼쪽 → 오른쪽으로 지나간다(ease-in-out). 그 밖의 시간에는 Mask째 끈다.</summary>
    private void ApplyShine(float _time)
    {
        bool bShine = SnapEnd <= _time && _time < ShineEnd;
        if (shineRoot.gameObject.activeSelf != bShine) shineRoot.gameObject.SetActive(bShine);
        if (false == bShine) return;

        float eased = EaseInOut((_time - SnapEnd) / ShineSeconds);
        shineImage.rectTransform.anchoredPosition = new Vector2(Mathf.RoundToInt(Mathf.Lerp(ShineFromX, ShineToX, eased)), 0f);
    }

    private void Finish()
    {
        Action callback = onDone;
        onDone = null;
        bPlaying = false;
        cardRect = null;

        callback?.Invoke();
    }

    private static float EaseOut(float _t)
    {
        return 1f - (1f - _t) * (1f - _t);
    }

    private static float EaseInOut(float _t)
    {
        return _t < 0.5f ? 2f * _t * _t : 1f - Mathf.Pow(-2f * _t + 2f, 2f) * 0.5f;
    }

    private void Update()
    {
        if (false == bPlaying) return;

        time += Time.unscaledDeltaTime;
        Evaluate(Mathf.Min(time, TotalSeconds));

        if (TotalSeconds <= time) Finish();
    }

    private void OnDestroy()
    {
        onDone = null;
    }
}
