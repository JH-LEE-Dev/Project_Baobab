using UnityEngine;

/// <summary>
/// 로그라이크 월드 HUD(WorldHUD_Plan.md). 섬 둘레 물 위에 놓이는 띠 2개(좌상단 임계치 / 우상단 스태미너)와 숫자 4개(좌상단 웨이브 / 임계치 / 스태미너 + 대쉬 칸 / 위 가운데 남은 벌목 수), 연출 입자 2장이다.
/// 값 · 연출 상태기계와 그리기는 순수 C# 클래스 RoguelikeWorldHudPresenter(+ RoguelikeWorldHudPainter 1배 픽셀 페인터)가 맡고, 이 컴포넌트는 시계를 먹이고
/// 그려진 Color32[] 버퍼를 런타임 텍스처 8장으로 올려 SpriteRenderer(PPU 32 = 640x360 기준 1px)로 보여 주며 숫자 렌더러를 정수 px 옮기기만 한다.
///   띠 2개     고정 Sorting Layer TreeShadow, Order -32768(short.MinValue). Y 정렬을 쓰지 않는다. 나무 · 나무 그림자 · 충격파 · Objects 층 이상(액터 · 이펙트 · 물 장식 포함)은 전부 띠 위에 그려지고,
///              띠 아래에는 물 · 지형(Bridge 이하 층)만 깔린다.
///   숫자 4개   Sorting Layer WorldUI, Order 0: 나무 · 떨어지는 아이템 위. 입자 2장은 같은 층 Order -1(숫자보다 아래).
/// 연출(10장): 켜면 등장(띠가 그려지고 칸이 켜지고 숫자가 들어옴), 끄면 퇴장 0.45초 뒤 렌더러를 끈다. 값이 바뀌면 칸 번쩍임 · 숫자 튐 · 불꽃 · 돌파 훑기 · 손실 펄스 · 목표 달성.
/// 위치: 로그라이크 전투 중 카메라는 섬 중앙에 고정된다(RoguelikeSystem.Flow.cs LockFieldCamera). 그래서 켤 때(SetVisible(true))와 SetWave 때 메인 카메라 위치를 1/32 격자에 맞춰 루트를 옮긴다.
///   화면 좌표 (x, y)의 월드 위치 = 루트 + ((x - 320) / 32, (180 - y) / 32). 카메라 흔들림은 월드와 같이 흔들린다(스냅은 이 두 시점뿐).
/// 시계는 unscaled 시간을 모아 쓴다(한 프레임 최대 1/20초만 진행해 로딩 끊김이 연출을 건너뛰게 하지 않는다). 그래서 일시정지(카드 선택 등) 중에도 연출이 이어진다.
/// 값 입력: 웨이브 / 처치는 UIView_RoguelikeHUD가, 스태미너 · 대쉬는 UI_RoguelikeStaminaBar / UI_RoguelikeDashCells(그림 없는 전달 컴포넌트)가 넣는다.
/// </summary>
public sealed class RoguelikeWorldHud : MonoBehaviour
{
    private const int ElementCount = RoguelikeWorldHudPresenter.ElementCount;
    private const int FirstLabelIndex = RoguelikeWorldHudPresenter.WaveLabelIndex;
    private const float MaxStepSeconds = 1f / 20f;

    // 띠 고정 정렬: 나무 그림자 층의 가장 아래. 나무 · 액터 · 이펙트 · 물 장식은 모두 이 위에 그려진다.
    private const string BandSortingLayerName = "TreeShadow";
    private const int BandSortingOrder = short.MinValue;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike World HUD)
    [SerializeField, Tooltip("재색 시트(Assets/Graphics/Roguelike/hud/world/). Read/Write가 켜져 있어야 한다.")]
    private RoguelikeWorldHudSheets sheets;
    [SerializeField, Tooltip("0 임계치 띠 / 1 스태미너 띠 / 2 웨이브 숫자 / 3 임계치 숫자 / 4 스태미너 숫자 + 대쉬 칸 / 5 남은 벌목 수 숫자 / 6 띠 둘레 입자 / 7 남은 수 둘레 입자")]
    private SpriteRenderer[] renderers = new SpriteRenderer[ElementCount];

    //내부 의존성
    private RoguelikeWorldHudPresenter presenter;
    private readonly Texture2D[] textures = new Texture2D[ElementCount];
    private readonly Sprite[] sprites = new Sprite[ElementCount];
    // 숫자 · 입자 렌더러의 기본 위치(루트 기준)와 지금 적용한 보정(px)
    private readonly Vector3[] basePositions = new Vector3[ElementCount];
    private readonly int[] appliedOffsetsX = new int[ElementCount];
    private readonly int[] appliedOffsetsY = new int[ElementCount];

    //상태 변수
    private double clock;

    public bool IsDashUnlocked => null != presenter && true == presenter.IsDashUnlocked;

    /// <summary>텍스처 · 스프라이트를 만들고 요소를 제자리에 놓는다. 만든 뒤 보이지 않는 상태로 시작한다(SetVisible(true)로 켠다).</summary>
    public void Initialize()
    {
        if (null != presenter) return;

        presenter = new RoguelikeWorldHudPresenter(sheets);

        CreateElement(RoguelikeWorldHudPresenter.ThresholdBandIndex, in RoguelikeWorldHudGeometry.ThresholdBandRect, "WorldHud_ThresholdBand");
        CreateElement(RoguelikeWorldHudPresenter.StaminaBandIndex, in RoguelikeWorldHudGeometry.StaminaBandRect, "WorldHud_StaminaBand");
        CreateElement(RoguelikeWorldHudPresenter.WaveLabelIndex, in RoguelikeWorldHudGeometry.WaveLabelRect, "WorldHud_Wave");
        CreateElement(RoguelikeWorldHudPresenter.ThresholdLabelIndex, in RoguelikeWorldHudGeometry.ThresholdLabelRect, "WorldHud_Threshold");
        CreateElement(RoguelikeWorldHudPresenter.StaminaLabelIndex, in RoguelikeWorldHudGeometry.StaminaLabelRect, "WorldHud_Stamina");
        CreateElement(RoguelikeWorldHudPresenter.KillLabelIndex, in RoguelikeWorldHudGeometry.KillLabelRect, "WorldHud_Kill");
        CreateElement(RoguelikeWorldHudPresenter.FxBandIndex, in RoguelikeWorldHudGeometry.FxBandRect, "WorldHud_FxBand");
        CreateElement(RoguelikeWorldHudPresenter.FxKillIndex, in RoguelikeWorldHudGeometry.FxKillRect, "WorldHud_FxKill");

        // 프리팹 값이 어긋나도 띠가 항상 나무 · 액터 · 이펙트 아래에 오도록 층 · order를 명시한다.
        renderers[RoguelikeWorldHudPresenter.ThresholdBandIndex].sortingLayerName = BandSortingLayerName;
        renderers[RoguelikeWorldHudPresenter.ThresholdBandIndex].sortingOrder = BandSortingOrder;
        renderers[RoguelikeWorldHudPresenter.StaminaBandIndex].sortingLayerName = BandSortingLayerName;
        renderers[RoguelikeWorldHudPresenter.StaminaBandIndex].sortingOrder = BandSortingOrder;

        SetRenderersEnabled(false);
    }

    /// <summary>켜면 위치를 카메라 중심에 스냅하고 등장 연출(10-1)을 시작한다. 이미 보이는 중이면 연출을 다시 시작하지 않는다.</summary>
    public void SetVisible(bool _bVisible)
    {
        if (null == presenter) return;

        if (true == _bVisible)
        {
            SnapToCamera();
            presenter.Show(clock);
            SetRenderersEnabled(true);
            Refresh();
            return;
        }

        presenter.Hide(clock);
    }

    /// <summary>즉시 숨긴다(퇴장 연출 없음). 뷰가 숨겨질 때.</summary>
    public void HideImmediate()
    {
        if (null == presenter) return;

        presenter.HideImmediate();
        SetRenderersEnabled(false);
    }

    /// <summary>런이 시작 · 종료될 때 값과 연출을 처음으로 되돌린다.</summary>
    public void ResetState()
    {
        if (null == presenter) return;

        presenter.ResetState();
        SetRenderersEnabled(false);
    }

    /// <summary>메인 카메라 위치(x, y)를 1/32 격자에 맞춰 루트 위치로 쓴다. 카메라가 없으면 위치는 그대로 둔다.</summary>
    public void SnapToCamera()
    {
        CameraFinder finder = CameraFinder.Instance;
        Camera mainCamera = null != finder ? finder.PPMainCamera : null;

        if (null != mainCamera)
        {
            Vector3 position = mainCamera.transform.position;
            float unit = RoguelikeWorldHudGeometry.PixelsPerUnit;
            transform.position = new Vector3(Mathf.Round(position.x * unit) / unit, Mathf.Round(position.y * unit) / unit, 0f);
        }
    }

    /// <summary>웨이브 표기(좌상단, 현재 번호만). 언리밋이면 무한대 아이콘 + 단계 번호. 위치도 카메라에 다시 맞춘다.</summary>
    public void SetWave(int _wave, bool _bUnlimit, int _unlimitIndex)
    {
        SnapToCamera();
        if (null != presenter) presenter.SetWave(_wave, _bUnlimit, _unlimitIndex);
    }

    /// <summary>처치 수 / 목표 처치 수. 위 가운데에는 남은 수 max(0, 목표 - 처치)만 보인다(11장). 처치로 남은 수가 줄면 숫자가 아래로 튀고, 0이 되면 금색 + 반짝이(10-3).</summary>
    public void SetKills(int _killed, int _target)
    {
        if (null != presenter) presenter.SetKills(_killed, _target);
    }

    /// <summary>
    /// 필드 나무 수 / 임계치 T. 손실 중(나무 수 >= T)이면 초과 토막 채움이 빨강이 되고 숫자 두 개가 경고색이 된다. 스태미너 띠 채움도 손실 중에는 빨강이다.
    /// 나무 수가 늘면 새 칸 번쩍임 + 숫자 튐, 줄면 불꽃(웨이브 클리어 뒤에는 물방울), T를 넘는 순간 돌파 훑기(10-3).
    /// </summary>
    public void SetThreshold(int _trees, int _threshold)
    {
        if (null != presenter) presenter.SetThreshold(_trees, _threshold);
    }

    /// <summary>손실 1초 펄스: 초과 토막 테 + 스태미너 띠 테가 0.133초 흰색, 스태미너 숫자 흔들림.</summary>
    public void PlayPulse()
    {
        if (null != presenter) presenter.PlayPulse();
    }

    /// <summary>스태미너 비율(0~1). 띠 채움 길이를 정한다. 값이 줄어든 순간 PlayStaminaDamaged가 먼저 불렸으면 직전 채움에서 지금 채움까지 잔상을 만든다.</summary>
    public void SetStaminaRatio(float _ratio)
    {
        if (null != presenter) presenter.SetStaminaRatio(_ratio);
    }

    /// <summary>스태미너 숫자 현재 / 최대. 표시는 올림 정수(기획서 3-2)다.</summary>
    public void SetStaminaNumbers(float _current, float _max)
    {
        if (null != presenter) presenter.SetStaminaNumbers(_current, _max);
    }

    /// <summary>스태미너가 한 번에 줄어든 순간(최대치 대비 비율). 다음 SetStaminaRatio가 잔상을 시작한다.</summary>
    public void PlayStaminaDamaged(float _lossRatio)
    {
        if (null != presenter) presenter.PlayStaminaDamaged(_lossRatio);
    }

    /// <summary>스태미너가 한 번에 회복된 순간(최대치 대비 비율). 늘어난 구간을 0.15초 동안 흰색으로 덮는다.</summary>
    public void PlayStaminaRecovered(float _gainRatio)
    {
        if (null != presenter) presenter.PlayStaminaRecovered(_gainRatio);
    }

    /// <summary>강제 경고(임계점 초과 · 한 번 맞으면 죽는 상태): 스태미너 띠 테가 0.5초 주기로 흰 테와 번갈아 칠해진다.</summary>
    public void SetForcedWarning(bool _bWarning)
    {
        if (null != presenter) presenter.SetForcedWarning(_bWarning);
    }

    /// <summary>무적 구간: 스태미너 채움을 무적 3톤으로.</summary>
    public void SetInvincible(bool _bInvincible)
    {
        if (null != presenter) presenter.SetInvincible(_bInvincible);
    }

    /// <summary>대쉬를 쓸 수 있을 때만 대쉬 칸이 보인다.</summary>
    public void SetDashUnlocked(bool _bUnlocked)
    {
        if (null != presenter) presenter.SetDashUnlocked(_bUnlocked);
    }

    /// <summary>표시할 대쉬 칸 수(0~2).</summary>
    public void SetDashMax(int _count)
    {
        if (null != presenter) presenter.SetDashMax(_count);
    }

    /// <summary>대쉬 칸 하나의 충전 비율(0~1).</summary>
    public void SetDashCharge(int _index, float _ratio)
    {
        if (null != presenter) presenter.SetDashCharge(_index, _ratio);
    }

    // // 내부

    private void CreateElement(int _index, in RoguelikeWorldHudRect _rect, string _name)
    {
        Texture2D texture = new Texture2D(_rect.width, _rect.height, TextureFormat.RGBA32, false);
        texture.name = _name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        textures[_index] = texture;

        // 피벗 왼쪽 위(0, 1): 렌더러 위치 = 화면 사각형 왼쪽 위 모서리의 월드 오프셋.
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, _rect.width, _rect.height), new Vector2(0f, 1f), RoguelikeWorldHudGeometry.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = _name;
        sprites[_index] = sprite;

        basePositions[_index] = RoguelikeWorldHudGeometry.ToWorldOffset(_rect.x, _rect.y);

        SpriteRenderer spriteRenderer = renderers[_index];
        spriteRenderer.sprite = sprite;
        spriteRenderer.transform.localPosition = basePositions[_index];
    }

    private void SetRenderersEnabled(bool _bEnabled)
    {
        for (int i = 0; ElementCount > i; i++)
        {
            if (null != renderers[i]) renderers[i].enabled = _bEnabled;
        }
    }

    /// <summary>시계를 이번 시각으로 두고 연출 상태를 계산해, 바뀐 요소만 다시 그려 텍스처로 올리고 숫자 렌더러를 정수 px 옮긴다.</summary>
    private void Refresh()
    {
        presenter.Tick(clock);

        int mask = presenter.PaintChanged();
        for (int i = 0; ElementCount > i; i++)
        {
            if (0 != (mask & (1 << i))) Upload(i);
        }

        for (int i = FirstLabelIndex; ElementCount > i; i++)
        {
            int dx = presenter.GetOffsetX(i);
            int dy = presenter.GetOffsetY(i);
            if (dx == appliedOffsetsX[i] && dy == appliedOffsetsY[i]) continue;

            appliedOffsetsX[i] = dx;
            appliedOffsetsY[i] = dy;
            renderers[i].transform.localPosition = basePositions[i] + new Vector3(dx / (float)RoguelikeWorldHudGeometry.PixelsPerUnit, -dy / (float)RoguelikeWorldHudGeometry.PixelsPerUnit, 0f);
        }
    }

    private void Upload(int _index)
    {
        textures[_index].SetPixelData(presenter.GetBuffer(_index), 0);
        textures[_index].Apply(false);
    }

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (null == presenter || false == presenter.IsVisible) return;

        clock += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
        Refresh();

        // 퇴장이 끝나면 Tick이 보이지 않는 상태로 바꾼다.
        if (false == presenter.IsVisible) SetRenderersEnabled(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; ElementCount > i; i++)
        {
            if (null != sprites[i]) Destroy(sprites[i]);
            if (null != textures[i]) Destroy(textures[i]);
        }
    }
}
