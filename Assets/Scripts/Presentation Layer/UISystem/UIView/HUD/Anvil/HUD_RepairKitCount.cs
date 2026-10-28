using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수리대(모루) 옆에 떠 있는 월드 HUD. 수리킷 아이콘(흰 외곽선은 UIEffect)과 남은 사용 횟수를 보여 주고,
/// 사용할 때 짧게 뽀잉하는 연출을 재생한다. 숫자는 버프 HUD의 작은 숫자 폰트(HUD_BuffNumberFont)를 그대로 쓴다.
/// 평소에는 숨겨져 있다가 RepairBox가 Show/Hide를 호출할 때만 나타나고 사라진다.
/// 노출 중에는 캐릭터가 있는 쪽의 반대편으로 좌우 위치를 보간해 옮겨, 캐릭터가 HUD에 가려지지 않게 한다(원목보관함 UI_Storage와 같은 규칙).
/// 연출은 DOTween 트윈을 만들지 않고 Update에서 DOVirtual.EasedValue로 계산하며(GC 없음), 배율은 정수 도트 폭으로 스냅한다.
/// </summary>
public class HUD_RepairKitCount : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [Tooltip("월드 캔버스 설정(카메라, 소팅 레이어)을 맡는 컴포넌트")]
    [SerializeField] private WorldCanvasEnabler canvasEnabler;
    [Tooltip("뽀잉/등장/퇴장 연출 대상. 피벗은 가운데, 크기는 아이콘과 같은 16x16.")]
    [SerializeField] private RectTransform visualRoot;
    [Tooltip("숫자를 CPU로 합성해 그리는 RawImage")]
    [SerializeField] private RawImage numberImage;
    [Tooltip("숫자 스프라이트 0~9 (버프 HUD와 같은 SmallCurrencyNum 시트)")]
    [SerializeField] private Sprite[] digitSprites;

    [Header("Layout")]
    [Tooltip("숫자의 오른쪽 끝 / 아래쪽 끝 위치(아이콘 왼쪽 아래 기준 px). 아이콘 오른쪽 아래에 겹치도록 둔다.")]
    [SerializeField] private Vector2 numberAnchor = new Vector2(19f, -2f);

    [Header("Pop")]
    [Tooltip("뽀잉: popUpDuration 동안 popScale까지 커지고, popDownDuration 동안 1로 돌아온다(합계 0.2초 이내).")]
    [SerializeField] private float popUpDuration = 0.06f;
    [SerializeField] private float popDownDuration = 0.14f;
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private Ease popUpEase = Ease.OutQuad;
    [SerializeField] private Ease popDownEase = Ease.OutBack;
    [Tooltip("돌아올 때 1보다 살짝 작아졌다 돌아오는 정도(OutBack overshoot)")]
    [SerializeField] private float popDownOvershoot = 2.5f;
    [Tooltip("뽀잉 중 숫자가 위로 튀는 높이(px). 정수 도트로 반올림해 적용한다.")]
    [SerializeField] private float numberKickHeight = 2f;

    [Header("Show / Hide")]
    [Tooltip("등장: 이 시간 동안 1까지 커진다. 퇴장: 이 시간 동안 0까지 줄어든 뒤 숨는다. 도중에 반대 요청이 오면 현재 크기에서 이어서 전환한다.")]
    [SerializeField] private float showDuration = 0.2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private float showOvershoot = 2.2f;
    [SerializeField] private float hideDuration = 0.12f;
    [SerializeField] private Ease hideEase = Ease.InBack;
    [SerializeField] private float hideOvershoot = 1.7f;

    [Header("Side Move")]
    [Tooltip("모루 중심에서 HUD 중심까지의 좌우 거리(월드 유닛). 캐릭터 반대편으로 +/- 적용한다.")]
    [SerializeField] private float sideOffsetX = 0.95f;
    [Tooltip("캐릭터와 모루의 X 차이가 이 값보다 작으면 좌우 판정을 바꾸지 않는다(떨림 방지).")]
    [SerializeField] private float sideDeadzone = 0.15f;
    [SerializeField] private float sideMoveDuration = 0.3f;
    [SerializeField] private Ease sideMoveEase = Ease.OutQuad;

    // 연출 상수
    private const float IconSize = 16f;
    private const float PixelsPerUnit = 32f;
    private const int NumberTexWidth = 24;

    // 내부 의존성
    private HUD_BuffNumberFont font;
    private Texture2D numberTexture;
    private Color32[] numberBuffer;
    private RectTransform numberRect;
    private Transform anchor;
    private Transform player;
    private Vector3 baseLocalPos;

    // 상태 변수
    private bool bResourceReady = false;
    private bool bVisible = false;
    private int numberShown = -1;
    private int numberWidth = 0;
    private int kickShown = 0;

    private float popAge = -1f;
    private float popMul = 1f;
    private float popKick = 0f;

    private bool bFading = false;
    private float fadeAge = 0f;
    private float fadeFrom = 0f;
    private float fadeTo = 0f;
    private float baseScale = 0f;

    private bool bSideMoving = false;
    private float sideAge = 0f;
    private float sideFrom = 0f;
    private float sideTo = 0f;
    private float sideX = 0f;

    // 월드 캔버스를 초기화한다(카메라가 준비된 뒤에 호출). _player는 좌우 위치 판정에 쓰는 캐릭터. 여러 번 불러도 안전하다.
    public void Initialize(Transform _player)
    {
        player = _player;
        EnsureResource();

        if (null != canvasEnabler)
        {
            canvasEnabler.Initialize();
        }
    }

    // 남은 횟수를 표시한다. _bPlayPop이 true면 숫자를 바꾸면서 뽀잉 연출을 재생한다(보이는 동안에만).
    public void SetCount(int _count, bool _bPlayPop)
    {
        if (false == EnsureResource())
        {
            return;
        }

        RefreshNumber(Mathf.Clamp(_count, 0, 999));

        if (true == _bPlayPop && true == bVisible)
        {
            popAge = 0f;
            TickPop(0f);
            ApplyVisual();
        }
    }

    // 캐릭터가 다가와 수리 가능한 위치가 되면 부른다. 지금 캐릭터 반대편에 바로 놓고 등장한다.
    public void Show()
    {
        if (true == bVisible)
        {
            return;
        }

        bVisible = true;
        EnsureResource();
        visualRoot.gameObject.SetActive(true);
        SnapSideToPlayer();
        BeginFade(1f);
        ApplyVisual();
    }

    // 수리 가능한 위치를 벗어나면 부른다. 줄어든 뒤 숨는다.
    public void Hide()
    {
        if (false == bVisible)
        {
            return;
        }

        bVisible = false;
        bSideMoving = false;
        BeginFade(0f);
    }

    private bool EnsureResource()
    {
        if (true == bResourceReady)
        {
            return true;
        }

        font = new HUD_BuffNumberFont();

        if (false == font.Build(digitSprites))
        {
            Debug.LogWarning("[HUD_RepairKitCount] 숫자 스프라이트(digitSprites 0~9)를 읽지 못했습니다.");
            return false;
        }

        numberRect = numberImage.rectTransform;
        numberTexture = new Texture2D(NumberTexWidth, HUD_BuffNumberFont.GlyphHeight, TextureFormat.RGBA32, false);
        numberTexture.filterMode = FilterMode.Point;
        numberTexture.wrapMode = TextureWrapMode.Clamp;
        numberTexture.name = "RepairKitNumber";
        numberBuffer = new Color32[NumberTexWidth * HUD_BuffNumberFont.GlyphHeight];
        numberImage.texture = numberTexture;
        bResourceReady = true;
        return true;
    }

    private void RefreshNumber(int _n)
    {
        if (_n == numberShown)
        {
            return;
        }

        numberWidth = font.Compose(_n, false, numberBuffer, NumberTexWidth);
        numberTexture.SetPixels32(numberBuffer);
        numberTexture.Apply(false, false);
        numberImage.uvRect = new Rect(0f, 0f, (float)numberWidth / NumberTexWidth, 1f);
        numberRect.sizeDelta = new Vector2(numberWidth, HUD_BuffNumberFont.GlyphHeight);
        numberShown = _n;
        PlaceNumber(kickShown);
    }

    private void PlaceNumber(int _kick)
    {
        kickShown = _kick;
        numberRect.anchoredPosition = new Vector2(numberAnchor.x - numberWidth, numberAnchor.y + _kick);
    }

    // 칸 폭이 정수 도트가 되도록 배율을 스냅한다.
    private static float SnapScale(float _scale)
    {
        float _px = Mathf.Max(1f, Mathf.Round(_scale * IconSize));
        return _px / IconSize;
    }

    // ===== 등장 / 퇴장 =====

    // 현재 크기(baseScale)에서 _to(1=등장, 0=퇴장)로 이어서 전환을 시작한다.
    private void BeginFade(float _to)
    {
        fadeFrom = baseScale;
        fadeTo = _to;
        fadeAge = 0f;
        bFading = true;
    }

    private void TickFade(float _dt)
    {
        fadeAge += _dt;

        bool _bShow = 1f == fadeTo;
        float _duration = true == _bShow ? showDuration : hideDuration;
        float _t = 0f < _duration ? Mathf.Clamp01(fadeAge / _duration) : 1f;

        if (_t < 1f)
        {
            baseScale = true == _bShow
                ? DOVirtual.EasedValue(fadeFrom, fadeTo, _t, showEase, showOvershoot)
                : DOVirtual.EasedValue(fadeFrom, fadeTo, _t, hideEase, hideOvershoot);
            return;
        }

        baseScale = fadeTo;
        bFading = false;

        if (false == _bShow)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    // ===== 뽀잉 =====

    // _dt만큼 진행해 popMul(배율 곱)과 popKick(숫자 높이)을 갱신한다. 끝나면 원래 값으로 돌려놓는다.
    private void TickPop(float _dt)
    {
        if (popAge < 0f)
        {
            return;
        }

        popAge += _dt;

        if (popUpDuration + popDownDuration <= popAge)
        {
            popAge = -1f;
            popMul = 1f;
            popKick = 0f;
            return;
        }

        if (popAge < popUpDuration)
        {
            float _t = Mathf.Clamp01(popAge / popUpDuration);
            popMul = DOVirtual.EasedValue(1f, popScale, _t, popUpEase);
            popKick = DOVirtual.EasedValue(0f, numberKickHeight, _t, popUpEase);
        }
        else
        {
            float _t = Mathf.Clamp01((popAge - popUpDuration) / popDownDuration);
            popMul = DOVirtual.EasedValue(popScale, 1f, _t, popDownEase, popDownOvershoot);
            popKick = DOVirtual.EasedValue(numberKickHeight, 0f, _t, popDownEase, popDownOvershoot);
        }
    }

    private void ApplyVisual()
    {
        float _snapped = SnapScale(baseScale * popMul);
        visualRoot.localScale = new Vector3(_snapped, _snapped, 1f);

        if (true == bResourceReady)
        {
            PlaceNumber(Mathf.RoundToInt(popKick));
        }
    }

    // ===== 좌우 위치 =====

    // 캐릭터가 모루보다 왼쪽이면 HUD는 오른쪽(+), 오른쪽이면 왼쪽(-)에 둔다. 데드존 안이거나 대상이 없으면 false를 돌려 이전 판정을 유지한다.
    private bool TryGetSideTarget(out float _target)
    {
        _target = sideTo;

        if (null == player || null == anchor)
        {
            return false;
        }

        float _deltaX = player.position.x - anchor.position.x;

        if (Mathf.Abs(_deltaX) < sideDeadzone)
        {
            return false;
        }

        _target = _deltaX < 0f ? sideOffsetX : -sideOffsetX;
        return true;
    }

    // 보간 없이 지금 캐릭터 반대편에 바로 놓는다(등장 시작용).
    private void SnapSideToPlayer()
    {
        if (true == TryGetSideTarget(out float _target))
        {
            sideTo = _target;
        }
        else if (0f == sideTo)
        {
            sideTo = sideOffsetX;
        }

        sideX = sideTo;
        bSideMoving = false;
        ApplySidePosition();
    }

    private void UpdateSide(float _dt)
    {
        if (true == TryGetSideTarget(out float _target) && false == Mathf.Approximately(_target, sideTo))
        {
            sideFrom = sideX;
            sideTo = _target;
            sideAge = 0f;
            bSideMoving = true;
        }

        if (false == bSideMoving)
        {
            return;
        }

        sideAge += _dt;
        float _t = 0f < sideMoveDuration ? Mathf.Clamp01(sideAge / sideMoveDuration) : 1f;
        sideX = DOVirtual.EasedValue(sideFrom, sideTo, _t, sideMoveEase);

        if (1f <= _t)
        {
            sideX = sideTo;
            bSideMoving = false;
        }

        ApplySidePosition();
    }

    // 1픽셀(1/32 유닛) 단위로 스냅해 픽셀이 뭉개지지 않게 한다.
    private void ApplySidePosition()
    {
        float _x = Mathf.Round(sideX * PixelsPerUnit) / PixelsPerUnit;
        transform.localPosition = new Vector3(_x, baseLocalPos.y, baseLocalPos.z);
    }

    // 꺼질 때(원정 종료 등) 연출 도중 상태가 남지 않도록 숨긴 상태로 되돌린다.
    private void ResetState()
    {
        bVisible = false;
        bFading = false;
        bSideMoving = false;
        popAge = -1f;
        popMul = 1f;
        popKick = 0f;
        baseScale = 0f;
        visualRoot.localScale = Vector3.one;
        visualRoot.gameObject.SetActive(false);

        if (true == bResourceReady)
        {
            PlaceNumber(0);
        }
    }

    private void Awake()
    {
        anchor = transform.parent;
        baseLocalPos = transform.localPosition;
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        if (true == bVisible)
        {
            UpdateSide(Time.deltaTime);
        }

        if (false == bFading && popAge < 0f)
        {
            return;
        }

        if (true == bFading)
        {
            TickFade(Time.deltaTime);
        }

        TickPop(Time.deltaTime);
        ApplyVisual();
    }

    private void OnDestroy()
    {
        if (null != numberTexture)
        {
            Destroy(numberTexture);
        }
    }
}
