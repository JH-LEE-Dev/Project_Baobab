using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using DG.Tweening.Core;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

public class HUD_Stemina : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Motions")]
    [SerializeField] private string shakeTag = "DangerShake";
    [SerializeField] private string colorTag = "DangerColor";
    [Range(0f, 100f)][SerializeField] private float startPoint = 100f;

    [Tooltip("경고가 켜진 뒤 피로도가 startPoint + 이 값(%p) 위로 회복되면 경고를 끈다. 경계에서 켜졌다 꺼졌다 하지 않도록 두는 여유폭")]
    [Range(0f, 100f)][SerializeField] private float releaseMargin = 5f;

    [Header("Hit Effect Ref")]
    [Tooltip("피격 잔상. Fill 아래에 깔려, 깎인 구간이 잠깐 남았다가 따라온다(HUD_HPBar의 고스트 바와 같은 규칙).")]
    [SerializeField] private Slider ghostSlider;
    [Tooltip("피격/회복 플래시. 게이지 전체를 덮고 레이캐스트는 꺼 둔다. 바 모양의 스프라이트를 쓰고, 색은 flashEffect의 색 치환(Replace)이 입힌다.")]
    [SerializeField] private Image flashOverlay;
    [Tooltip("flashOverlay의 UIEffect. 스프라이트의 원래 색이 아니라 지정한 단색으로 실루엣만 칠한다.")]
    [SerializeField] private UIEffect flashEffect;
    [Tooltip("피격 흔들림을 줄 대상. 비워 두면 이 오브젝트 자신. 저피로도 경고(DangerShake)는 SteminaBar를 흔들므로 그 위 단계를 지정해 합산되게 한다.")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("Recover Effect Ref")]
    [Tooltip("바의 채움 이미지(Fill). 회복 연출의 몸통 영역(스프라이트 알파에서 테두리/하이라이트/그림자 줄을 뺀 안쪽)과 색(스프라이트 평균색 x 현재 틴트)을 여기서 읽어 항상 바와 같은 색 계열로 그린다.")]
    [SerializeField] private Image barFillImage;
    [Tooltip("\"+\" 입자가 그려질 영역(바 전체와 같은 크기). 입자의 앵커는 (0, 0.5), 피벗은 (0, 0)으로 둔다(왼쪽 아래 모서리를 정수 픽셀에 맞추기 위함). SteminaBar의 자식으로 두어 경고 흔들림을 같이 받는다.")]
    [SerializeField] private RectTransform sparkleContainer;
    [SerializeField] private RectTransform[] sparkleRects;
    [SerializeField] private Image[] sparkleImages;
    [Tooltip("3x3 \"+\" 모양 흰색 픽셀 스프라이트. 비워 두면 \"+\"는 1px 점으로 대신한다.")]
    [SerializeField] private Sprite sparklePlusSprite;

    [Header("Invincible Effect Ref")]
    [Tooltip("프리즘 무적 연출을 그리는 RawImage. 바 전체와 같은 크기로 Fill 위, FlashOverlay 아래에 두고 레이캐스트는 끈다. 텍스처는 코드가 만들고 갱신한다.")]
    [SerializeField] private RawImage prismSurface;

    [Header("Hit Effect")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("변화량이 fullEffectRatio 이상일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMaxAlpha = 0.75f;
    [Tooltip("변화량이 minEffectRatio 근처일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMinAlpha = 0.25f;
    [SerializeField] private float damageFlashDuration = 0.12f;
    [SerializeField] private int damageFlashCount = 2;
    [Tooltip("피격 후 잔상이 남아 있다가 따라오기 시작하기까지의 시간(초)")]
    [SerializeField] private float ghostDelay = 0.35f;
    [Tooltip("잔상이 현재 값을 따라잡는 데 걸리는 대략적인 시간(초). 현재 값을 매 프레임 따라가므로 지속 소모 중에도 끊기지 않는다.")]
    [SerializeField] private float ghostFollowDuration = 0.3f;
    [Tooltip("피격 흔들림의 최대 세기(캔버스 픽셀)")]
    [SerializeField] private float shakeStrength = 2f;
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("Recover Effect")]
    [Tooltip("게이지가 회복 전 값에서 새 값까지 차오르는 대략적인 시간(초). 현재 값을 매 프레임 따라가므로 지속 소모가 겹쳐도 끊기지 않는다.")]
    [SerializeField] private float fillUpDuration = 0.35f;
    [Tooltip("연속으로 들어오는 회복의 세기를 누적하는 시간(초). 마지막 회복의 차오름이 끝난 뒤 이 시간 안에 또 들어오면 같은 묶음으로 보고 누적량으로 플래시 세기와 \"+\" 개수를 정한다.")]
    [Range(0f, 1f)][SerializeField] private float gainBundleWindow = 0.25f;
    [Tooltip("이 비율(최대치 대비) 이하의 단일 회복은 무시한다(노이즈 컷). 0이면 0보다 큰 회복은 모두 차오름에 반영한다.")]
    [Range(0f, 0.05f)][SerializeField] private float minGainRatio = 0f;
    [Tooltip("아직 연출하지 않은 회복량이 이 비율(최대치 대비)에 닿을 때마다 플래시, \"+\"를 한 번 띄우고 비운다. 잘게 연속으로 들어오는 회복(소모가 더 빨라 혼잡해지는 경우)의 연출 횟수를 회복량에 비례하게 제한한다. 이보다 작은 회복은 차오름만 보인다. 0이면 회복이 올 때마다 연출한다.")]
    [Range(0f, 1f)][SerializeField] private float sparkleMinRatio = 0.007f;
    [Tooltip("몸통 영역 위쪽에서 제외하는 줄 수. 스프라이트의 위쪽 하이라이트/어두운 줄(2행)을 피한다.")]
    [SerializeField] private int bodyInsetTop = 2;
    [Tooltip("몸통 영역 아래쪽에서 제외하는 줄 수. 스프라이트의 아래쪽 하이라이트 줄과 그림자(3행)를 피한다.")]
    [SerializeField] private int bodyInsetBottom = 3;
    [Tooltip("몸통 영역 좌우에서 제외하는 열 수. 스프라이트의 왼쪽/오른쪽 윤곽(3열)을 피한다.")]
    [SerializeField] private int bodyInsetSide = 3;

    [Header("Recover Effect - Flash")]
    [Tooltip("회복 플래시 색: 바의 현재 색에서 흰색 쪽으로 밝히는 비율. 1이면 순백이다. 피격 플래시(붉은색)와 같은 길이/횟수/알파 규칙을 쓴다.")]
    [Range(0f, 1f)][SerializeField] private float recoverFlashLighten = 0.75f;
    [SerializeField] private float recoverFlashDuration = 0.12f;
    [SerializeField] private int recoverFlashCount = 2;

    [Header("Recover Effect - Plus Particles")]
    [Tooltip("단발 회복에서 늘어난 구간에서 떠오르는 \"+\"의 개수(x: 회복량 최소, y: 회복량 최대)")]
    [SerializeField] private Vector2Int plusCountRange = new Vector2Int(5, 10);
    [Tooltip("\"+\"의 처음 위쪽 속도(캔버스 픽셀/초)")]
    [SerializeField] private Vector2 plusRiseSpeed = new Vector2(14f, 26f);
    [Tooltip("\"+\"가 좌우로 흔들리는 폭(캔버스 픽셀)")]
    [SerializeField] private float plusSway = 1.2f;
    [Tooltip("\"+\" 하나가 머무는 시간(초). 3x3 \"+\"로 떠오르다 끝 무렵 1px 점으로 줄고 깜빡이며 사라진다.")]
    [SerializeField] private Vector2 plusLifetime = new Vector2(0.7f, 1f);
    [Tooltip("\"+\"가 바의 몸통 위쪽 끝에서 더 올라갈 수 있는 최대 높이(캔버스 픽셀). 이보다 높이는 그리지 않는다.")]
    [SerializeField] private float plusRiseLimit = 16f;
    [Tooltip("\"+\" 색: 바의 현재 색에서 흰색 쪽으로 밝히는 비율")]
    [Range(0f, 1f)][SerializeField] private float plusLighten = 0.8f;
    [Tooltip("휴식 구역 안에서 \"+\" 하나가 떠오르는 간격(초). 바가 충분히 찼을 때의 값이며, 채움이 적으면 비율에 맞춰 늘어난다.")]
    [SerializeField] private float restPlusInterval = 0.15f;

    [Header("Invincible Effect")]
    [Tooltip("켜짐 순간 바 전체가 흰색으로 팝 하는 시간(초)")]
    [SerializeField] private float prismPopDuration = 0.07f;
    [Tooltip("켜짐 후 모든 면이 번쩍였다가 가라앉기까지의 전체 시간(초, 팝 포함)")]
    [SerializeField] private float prismSettleDuration = 0.28f;
    [Tooltip("무적이 풀릴 때 프리즘이 면 단위로 녹색으로 돌아가는 데 걸리는 시간(초)")]
    [SerializeField] private float prismReleaseDuration = 1f;
    [Tooltip("면 하나가 풀리는 동안 바 색으로 이행하며 거치는 톤 단계 수")]
    [SerializeField] private int prismReleaseSteps = 4;
    [Tooltip("면 하나가 풀리기 시작해서 완전히 사라지기까지가 전체 풀림 시간에서 차지하는 비율")]
    [Range(0.1f, 0.6f)][SerializeField] private float prismReleaseFadeSpan = 0.35f;
    [Tooltip("풀림 스타일. Basic: 바 색으로 단계 이행 / Flash: 흰 섬광 후 이행 / Rainbow: 이웃 팔레트 색을 거쳐 이행 / Wave: 왼쪽에서 오른쪽으로 섬광이 쓸고 지나감")]
    [SerializeField] private PrismReleaseStyle prismReleaseStyle = PrismReleaseStyle.Wave;
    [Tooltip("프리즘을 다시 그리는 속도(fps). 보간 없이 단계식으로 갱신해 픽셀 느낌을 지킨다.")]
    [SerializeField] private float prismStepRate = 14f;
    [Tooltip("프리즘 면 하나의 크기(캔버스 픽셀)")]
    [SerializeField] private float prismFacetSize = 8f;
    [Tooltip("면이 한 번 번쩍이고 다음에 번쩍이기까지의 주기 범위(초). 면마다 이 안에서 고유한 값을 갖는다.")]
    [SerializeField] private Vector2 prismTwinkleCycle = new Vector2(1.4f, 3.6f);
    [Tooltip("한 번 번쩍이는 시간(초)")]
    [SerializeField] private float prismTwinkleLength = 0.26f;
    [Tooltip("번쩍이는 면의 비율 배수. 1이면 기본, 클수록 많은 면이 번쩍인다.")]
    [Range(0f, 2f)][SerializeField] private float prismTwinkleDensity = 1f;
    [Tooltip("무적 중 회복 때 늘어난 구간의 면이 왼쪽에서 오른쪽으로 번쩍이는 파도의 길이(초)")]
    [SerializeField] private float prismGainWaveDuration = 0.4f;
    [Tooltip("프리즘 면의 색. 파스텔 6색(민트, 하늘, 라벤더, 분홍, 살구, 연노랑).")]
    [SerializeField] private Color[] prismPalette = new Color[]
    {
        new Color(0.70f, 0.96f, 0.80f, 1f),
        new Color(0.66f, 0.92f, 1.00f, 1f),
        new Color(0.78f, 0.74f, 1.00f, 1f),
        new Color(1.00f, 0.72f, 0.88f, 1f),
        new Color(1.00f, 0.80f, 0.72f, 1f),
        new Color(1.00f, 0.95f, 0.70f, 1f)
    };

    [Header("Front Edge Effect")]
    [Tooltip("채움 앞쪽의 젤리 머리 세기(0이면 끔). 앞쪽 바깥에 말랑한 둥근 머리를 얹고, 회복 때 늘어났다 튕기며 안착하고 소모 때 눌린다. 1이면 기본, 작을수록 늘어남, 눌림, 호흡이 줄어든다. 무적 프리즘 중에는 쓰지 않는다.")]
    [Range(0f, 1f)][SerializeField] private float frontEdgeStrength = 1f;

    [Header("Common")]
    [Tooltip("이 비율(최대치 대비)보다 작은 피격(스태미나 감소)은 연출하지 않는다. 회복에는 쓰지 않는다(회복은 Recover Effect의 sparkleMinRatio, gainBundleWindow가 정한다).")]
    [Range(0f, 1f)][SerializeField] private float minEffectRatio = 0.01f;
    [Tooltip("이 비율 이상의 변화는 연출 세기가 최대가 된다.")]
    [Range(0.01f, 1f)][SerializeField] private float fullEffectRatio = 0.2f;

    // 내부 의존성
    private struct PlusState
    {
        public bool bAlive;
        public float x;
        public float y;
        public float vy;
        public float swayPhase;
        public float age;
        public float life;
    }

    // 프리즘 면 하나. 면 배치와 색, 번쩍임/풀림 값은 윤곽 표를 만들 때 한 번만 정한다.
    private struct PrismFace
    {
        public float nx;
        public float ny;
        public float nz;
        public Color color;
        public float centerX;
        public float twinkleKey;
        public float twinklePeriod;
        public float twinklePhase;
        public float releaseThreshold;
        public float releaseWaveKey;
        public int paletteIndex;
    }

    private enum PrismPhase
    {
        Off,
        On,
        Releasing
    }

    private enum PrismReleaseStyle
    {
        Basic,
        Flash,
        Rainbow,
        Wave
    }

    private static readonly float[] PrismTwinkleLevels = { 0.5f, 1f, 0.8f, 0.35f };
    private static readonly float[] PrismGainLevels = { 1f, 0.7f, 0.4f, 0.2f };
    // 광원이 도는 속도(라디안/초). 광원이 돌아 면의 4단계 명암이 하나씩 바뀐다.
    private const float PrismLightSpeed = 0.8f;
    // 면의 가장 어두운 단계 비율과 가장 밝은 단계에서 흰색으로 섞는 비율
    private const float PrismDeepFactor = 0.872f;
    private const float PrismLitMix = 0.256f;
    private const int PrismCellKeyOffset = 512;
    private const int PrismCellKeyStride = 1024;
    // 면마다 미리 계산해 두는 색 변형 수(일반, 윗변, 아랫변, 가장자리)
    private const int PrismColorVariants = 4;
    // 픽셀의 위치 종류 비트(윤곽 표에서 한 번만 계산)
    private const byte PrismKindTop = 1;
    private const byte PrismKindBottom = 2;
    private const byte PrismKindLeft = 4;

    private const int ShakeStepCount = 8;
    // 휴식 "+" 간격 보정 기준 채움 비율. 이보다 덜 찼으면 좁은 구간에 "+"가 뭉치지 않도록 간격을 비율만큼 늘린다.
    private const float RestPlusMinFillRatio = 0.35f;
    // 잔상/차오름이 현재 값과의 차이가 이보다 작으면(160px 바 기준 반 픽셀 미만) 눈에 보이지 않으므로 바로 맞춘다.
    private const float SnapGap = 0.003f;
    // 지수 감쇠의 세기. 클수록 정해진 시간 안에 더 확실히 따라잡는다.
    private const float FollowSharpness = 5f;
    // 격차가 작아져도 끝이 늘어지지 않도록 하는 최소 속도(비율/초)
    private const float GhostMinFollowSpeed = 0.15f;
    // 회복이 올 때 직전 플래시가 이 값(1이 막 시작, 0이 끝) 이하로 줄어 있으면 플래시를 다시 시작한다(같은 순간의 연타는 건너뛴다)
    private const float FlashRestartBelow = 0.5f;
    // 차오름은 일정한 속도로 차오르다가 목표에 가까워질 때만 느려진다. (처음부터 지수 감쇠로 시작하면 첫 프레임에 한꺼번에 튄다)
    private const float FillEaseGap = 0.12f;
    private const float FillEaseMinFactor = 0.35f;
    private const float FillMinSpeed = 0.5f;
    // "+"는 이 간격으로 한 칸씩만 움직인다. (20fps)
    private const float PixelStep = 0.05f;
    private const int MaxStepsPerFrame = 4;
    // "+" 수명의 이 비율을 지나면 깜빡이며 사라진다.
    private const float PlusBlinkStart = 0.7f;
    // "+" 수명의 이 비율까지는 3x3 모양, 이후는 1px 점
    private const float PlusShapeEnd = 0.6f;
    // 시작 위치를 몸통 안에서 찾을 때 시도하는 횟수
    private const int PlacementTries = 6;
    // 한 번에 예약해 둘 수 있는 "+"의 최대 개수
    private const int MaxPlannedPlus = 16;
    // 회복 한 번마다 최소로 더하는 "+" 개수(묶음의 세기가 상한이어도 회복이 올 때마다 "+"가 보이게)
    private const int PlusMinPerBurst = 2;
    // 회복 중 "+"는 보이는 끝(채움 앞쪽 + 젤리 머리 길이)에서 이 칸 수 x 칸 폭 안쪽으로 솟아오른다.
    private const int PlusTipSlots = 4;
    private const float PlusTipSlotWidth = 2f;
    // 젤리 머리: 갱신 간격(30fps), 움직임으로 깨우는 최소 변화량(비율)
    private const float EdgeStepInterval = 1f / 30f;
    private const float EdgeMoveEpsilon = 0.00005f;
    // 스프링 계수(복원력, 감쇠)와 상태별 목표 길이(캔버스 픽셀), 정지 호흡 진폭, 회복 시작/끝에 주는 충격
    // 회복 때 늘어나는 양은 앞쪽 이동 속도에 비례하고(EdgeGainSpeedK, 비율/초당 픽셀) EdgeGainExtraMax를 넘지 않는다.
    // 시작 충격은 이번 묶음의 회복량(10% 이상이면 최대)에 비례한다.
    private const float EdgeSpringK = 200f;
    private const float EdgeSpringC = 16f;
    private const float EdgeRestLen = 2.2f;
    private const float EdgeGainExtraMax = 2f;
    private const float EdgeGainSpeedK = 4f;
    private const float EdgeFastTau = 0.1f;
    private const float EdgeDrainLen = 1.6f;
    private const float EdgeBreath = 0.35f;
    private const float EdgeGainKick = 14f;
    private const float EdgeSettleKick = -6f;
    // 머리가 (다시) 생길 때 0에서 1로 올라오는 시정수(초). 프리즘 해제 직후 갑자기 튀어나와 회복처럼 보이지 않게 한다.
    private const float EdgeAppearTau = 0.6f;
    // 앞쪽 속도 판정(비율/초): 지수 이동평균 시정수, 소모 진입/해제 기준(히스테리시스), 회복으로 보는 기준, 소모 가중치 시정수
    private const float EdgeVelTau = 0.35f;
    private const float EdgeDrainEnter = 0.0006f;
    private const float EdgeDrainExit = 0.0002f;
    private const float EdgeGainVel = 0.01f;
    private const float EdgeDrainTau = 0.4f;
    // 소모 중 표면 물결 진폭, 위/아래 행 길이 차(기울기 흔들림) 진폭(캔버스 픽셀), 픽셀 경계를 넘을 때의 작은 충격
    private const float EdgeRippleAmp = 0.7f;
    private const float EdgeSloshAmp = 0.7f;
    private const float EdgeTickKick = 5f;
    // 머리 모양: 세로 가운데 이 비율까지는 길이가 일정한 평평한 면이고, 바깥 위/아래 구간에서만 둥글게 말린다
    private const float EdgeFlatRatio = 0.62f;
    // 머리 명암(림 라이트): 머리 길이 방향 위치(0 이음매, 1 바깥 끝)가 이 값을 넘는 위쪽 행은 밝게, 아래쪽 행은 어둡게.
    // 이음매 쪽은 바 본체와 같은 색이라 이음매가 보이지 않는다.
    private const float EdgeRimStart = 0.5f;
    private const float EdgeRimOuter = 0.85f;
    private const float EdgeRimBright1 = 0.14f;
    private const float EdgeRimBright2 = 0.28f;
    private const float EdgeShadeStart = 0.55f;
    private const float EdgeShadeMul = 0.82f;
    // 윤곽(스프라이트 알파)을 안쪽으로 인정하는 기준
    private const byte SilhouetteAlphaCut = 127;
    // 윤곽 표 생성에 실패했을 때 다시 시도하는 간격(초)
    private const float SilhouetteRetryInterval = 0.5f;

    private HUD_ScreenBlood screenBloodComponent;
    // 던전에서만 true. 마을에서는 게이지가 숨겨지고 저피로도 경고(게이지 흔들림/붉은 화면)도 판정하지 않는다.
    private bool bActive = false;
    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;

    // 피격/회복 연출 상태
    // displayRatio: 마지막으로 받은 실제 비율. shownRatio: 게이지에 실제로 그려 주는 값(순간 회복 때만 실제 비율보다 늦게 차오른다).
    private float displayRatio = 1f;
    private float shownRatio = 1f;
    private float pendingLossRatio = 0f;
    private float pendingGainRatio = 0f;
    private bool bGhostTrailing = false;
    private float ghostHoldRemain = 0f;
    private bool bFillingUp = false;
    private bool bFillEvent = false;
    private float fillSegmentLow = 0f;
    private float fillSpeed = 0f;

    // 회복 묶음: 한 번의 공격으로 여러 나무가 쓰러지며 잘게 나뉘어 들어오는 회복을 하나의 큰 회복으로 본다.
    // 마지막 회복의 차오름이 끝난 뒤 gainBundleWindow 안에 또 들어오면 같은 묶음이고, 세기와 "+" 목표 개수는 누적량으로 정한다.
    private float bundleGainRatio = 0f;
    private float bundleLingerRemain = 0f;
    private float bundleFlashIntensity = 0f;
    private int bundlePlannedPlus = 0;
    // 아직 연출하지 않은 회복량. sparkleMinRatio에 닿으면 한 번 연출하고 비운다.
    private float unplayedGainRatio = 0f;

    private float flashValue = 0f;
    private Color flashColor = Color.white;
    private float flashPeakAlpha = 0f;
    private float shakeValue = 0f;
    private float currentShakeStrength = 0f;
    private Vector2 appliedShakeOffset = Vector2.zero;

    private PlusState[] pluses;
    private int alivePlusCount = 0;
    private float pixelStepTimer = 0f;

    // 차오르는 동안 앞쪽이 지나갈 때 켜도록 예약해 둔 "+"의 가로 위치(캔버스 픽셀)
    private float[] plannedPlusX = new float[MaxPlannedPlus];
    private int plusTipSlot = 0;
    private int plannedPlusCount = 0;

    // 바 윤곽(Fill 스프라이트 알파)과 그 안쪽 몸통 영역, 기본색. 시작할 때 한 번만 읽는다.
    private bool[] barInside;
    private bool[] barBody;
    private int barW = 0;
    private int barH = 0;
    private int bodyTopRow = 0;
    private Color barBaseColor = new Color(0.66f, 0.82f, 0.36f, 1f);
    private bool bSilhouetteReady = false;
    private float silhouetteRetryAt = 0f;
    // 이번 갱신에서 "+"와 플래시가 쓰는 바의 현재 색(스프라이트 평균색 x Fill 틴트)
    private Color barColorNow = new Color(0.66f, 0.82f, 0.36f, 1f);

    private bool bResting = false;
    private float restPlusTimer = 0f;

    private Tween flashTween;
    private Tween shakeTween;

    // 트윈마다 델리게이트를 새로 만들지 않도록 한 번만 캐싱한다.
    private DOGetter<float> getFlashValue;
    private DOSetter<float> setFlashValue;
    private DOGetter<float> getShakeValue;
    private DOSetter<float> setShakeValue;

    // 스태미나 무적 원인별 상태(SetInvincible 참고)
    private bool bEntryInvincible = false;
    private bool bRestAreaInvincible = false;

    // 프리즘 무적 연출 상태. 게임이 주는 무적 신호(켜짐/꺼짐)는 이 시각 상태를 바꾸는 입력일 뿐이다.
    private PrismPhase prismPhase = PrismPhase.Off;
    private bool bPrismEngagePending = false;
    private float prismEngageTimer = 0f;
    private float prismReleaseTimer = 0f;
    private float prismStepTimer = 0f;
    private float prismTime = 0f;
    private float prismGainTimer = -1f;
    // 단계(14fps)마다 갱신하는 그리기용 시각 값. 채움 앞쪽만 따라갈 때는 이 값을 그대로 써 반짝임이 단계식으로 유지된다.
    private float prismPaintTime = 0f;
    private float prismPaintEngageTimer = 0f;
    private float prismPaintReleaseTimer = 0f;
    private float prismPaintGainTimer = -1f;
    private int prismPaintedFilledPx = -1;
    private float prismGainLowPx = 0f;
    private float prismGainHighPx = 0f;
    private PrismFace[] prismFaces;
    private float[] prismFaceLevel;
    private int[] prismFaceTone;
    private Color32[] prismFaceColors;
    private byte[] prismPixelKind;
    private int[] prismFaceMap;
    private Color32[] prismBuffer;
    private Texture2D prismTexture;

    // 젤리 머리 상태. 프리즘 오버레이 텍스처를 프리즘이 꺼져 있을 때만 빌려 쓴다.
    // 회복/소모는 프레임 변화량이 아니라 시간 보정한 앞쪽 속도의 이동평균(edgeVelocity)으로 판정한다.
    private Color[] barRowColors;
    // edgeMask: 머리 픽셀 여부, edgeShade: 머리 길이 방향 위치(0 이음매, 1 바깥 끝)
    private byte[] edgeMask;
    private float[] edgeShade;
    private int edgeRowLo = 0;
    private int edgeRowHi = 0;
    private bool bEdgeActive = false;
    private bool bEdgeDrawn = false;
    private bool bEdgeGain = false;
    private bool bEdgeDrain = false;
    private int edgePrevFilled = -1;
    private float edgeVelocity = 0f;
    private float edgeFastSpeed = 0f;
    private float edgeAppear = 0f;
    private float edgeDrainWeight = 0f;
    private float edgeRipplePhase = 0f;
    private float edgeSpringLen = EdgeRestLen;
    private float edgeSpringVel = 0f;
    private float edgeStepTimer = 0f;
    private float edgeTime = 0f;
    private float edgePrevShown = 0f;


    // 퍼블릭 초기화 및 제어 메서드

    // 저피로도 경고 중인지(다른 HUD가 경고 틴트에 맞춰 색을 따라갈 때 읽는다)
    public bool IsWarning
    {
        get { return bWarningGauge; }
    }

    // 바 채움의 현재 틴트(경고 중에는 흰색<->붉은색으로 오간다)
    public Color FillTint
    {
        get { return null != barFillImage ? barFillImage.color : Color.white; }
    }

    public void Initialize(HUD_ScreenBlood _screenBlood)
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();

        screenBloodComponent = _screenBlood;

        getFlashValue = GetFlashValue;
        setFlashValue = SetFlashValue;
        getShakeValue = GetShakeValue;
        setShakeValue = SetShakeValue;

        if (null == shakeTarget)
            shakeTarget = transform as RectTransform;

        if (null != ghostSlider)
        {
            ghostSlider.minValue = 0.0f;
            ghostSlider.maxValue = 1.0f;
            ghostSlider.value = 1.0f;
        }

        pluses = new PlusState[GetPlusTotal()];
        BuildBarSilhouette();

        ResetEffects();
    }

    public void UpdateValue(float _ratio)
    {
        // 경고 상태는 현재 비율을 그대로 따라간다(켜짐: startPoint 이하, 꺼짐: startPoint + releaseMargin 초과).
        // 예전엔 한 번 켜지면 던전 진입 때만 풀리는 래치였는데, 그러면 마을에서 켜져 있어도 끌 방법이 없었고
        // 휴식/포션으로 피로도를 회복해도 붉은 화면이 계속 깜빡였다.
        if (true == bActive)
        {
            float _warnRatio = startPoint * 0.01f;

            if (false == bWarningGauge && _warnRatio >= _ratio)
                SetWarning(true);
            else if (true == bWarningGauge && _warnRatio + releaseMargin * 0.01f < _ratio)
                SetWarning(false);
        }

        displayRatio = _ratio;

        // 이벤트(순간 변화)와 값 변경은 같은 호출 안에서 일어나므로, 이벤트는 항상 HUD가 새 값을 보기 전에 도착한다.
        float _prevShown = shownRatio;
        bool _wasFilling = bFillingUp;

        if (0f < pendingGainRatio)
            BeginFillUp(_ratio);

        UpdateShownRatio(_ratio);
        progressBar?.UpdateValue(shownRatio);

        // 채움 앞쪽이 움직이면 앞쪽 끝부분 연출을 깨운다(움직임이 멈추고 입자가 사라지면 스스로 잠든다).
        if (0f < frontEdgeStrength && EdgeMoveEpsilon < Mathf.Abs(shownRatio - _prevShown))
            bEdgeActive = true;

        UpdateGhost(_ratio, _prevShown, _wasFilling);
    }

    /// <summary>
    /// 스태미나가 한 번에 줄어든 순간의 연출(붉은 플래시, 잔상, 흔들림). _lossRatio는 최대치 대비 줄어든 비율이다.
    /// 지속 소모는 호출하지 않는다.
    /// </summary>
    public void PlayDamaged(float _lossRatio)
    {
        if (false == bActive || minEffectRatio > _lossRatio)
            return;

        float _intensity = GetIntensity(_lossRatio);

        // 피격이 항상 우선한다. 프리즘은 풀리는 도중이어도 즉시 걷어, 잔상 바의 붉은색과 섞여 탁해지지 않게 한다.
        StopPrism();

        // 잔상의 시작점은 줄어든 값이 HUD 비율에 반영된 뒤(UpdateValue)에 정한다.
        pendingLossRatio += _lossRatio;

        StartFlash(damageFlashColor, _intensity, damageFlashDuration, Mathf.Max(1, damageFlashCount));
        StartShake(_intensity);
    }

    /// <summary>
    /// 스태미나가 한 번에 회복된 순간의 연출(차오름 + 바 전체 플래시 + "+" 상승). _gainRatio는 최대치 대비 늘어난 비율이다.
    /// 자연 회복처럼 매 프레임 쌓이는 회복은 호출하지 않는다.
    /// 한 호출의 크기로 연출 여부를 정하지 않는다. 한 번의 공격으로 여러 나무가 쓰러지면 0.x%짜리 회복이 여러 번 나뉘어 들어오므로,
    /// 모두 받아서 묶음(gainBundleWindow)으로 누적하고 누적량으로 세기를 정한다(BeginFillUp).
    /// </summary>
    public void PlayRecovered(float _gainRatio)
    {
        if (false == bActive || minGainRatio >= _gainRatio)
            return;

        pendingGainRatio += _gainRatio;
    }

    /// <summary>
    /// 최대치에 막혀 회복량이 일부 또는 전부 버려진 단발 회복(벌목, 포션)의 표시. 채워질 구간이 없어도 약한 플래시와 "+" 몇 개를 띄운다.
    /// 휴식 구역 안에서는 조용한 "+"가 이미 떠오르고 있어 따로 재생하지 않는다.
    /// </summary>
    public void PlayRecoverWasted()
    {
        if (false == bActive || true == bResting || false == bSilhouetteReady)
            return;

        RefreshBarColor();

        float _filledPx = GetBarWidth() * Mathf.Clamp01(displayRatio);

        // 프리즘이 켜져 있으면 평소와 같은 플래시 위에 채움 전체의 면이 파도처럼 번쩍인다.
        if (true == IsPrismShowing)
            StartPrismGainWave(0f, displayRatio);

        StartRecoverFlash(0.3f);

        SpawnPlusNow(3, 4f, _filledPx - 4f);
    }

    /// <summary>
    /// 휴식 구역 안에서 회복 중인지. true인 동안 피로도가 꽉 차 있어도 "+" 하나가 일정 간격으로 조용히 떠오른다.
    /// 매 프레임 불러도 값이 같으면 아무 일도 하지 않는다.
    /// </summary>
    public void SetResting(bool _bResting)
    {
        bool _bNext = true == _bResting && true == bActive;

        if (_bNext == bResting)
            return;

        bResting = _bNext;

        // 들어오자마자 첫 "+"가 나가도록 타이머를 가득 채워 둔다. 나갈 때는 이미 떠 있는 "+"가 수명대로 사라진다.
        restPlusTimer = true == bResting ? restPlusInterval : 0f;
    }

    /// <summary>
    /// 게이지 표시 여부. 던전이면 true, 마을이면 false(UIView_HUD.ChangedActiveStateStemina 참고).
    /// 어느 쪽으로 바뀌든 저피로도 경고는 일단 끈다. 예전엔 던전 진입(true)에서만 끄고 마을 진입(false)에서는
    /// 아무것도 하지 않아, 던전에서 켜진 붉은 화면 깜빡임이 마을까지 이어지면 다음 던전에 들어갈 때까지 남았다.
    /// </summary>
    public void SetActivate(bool _bActive)
    {
        bActive = _bActive;
        progressBar?.SetActivate(_bActive);

        // 숨겨지는 동안 남아 있던 연출이 다음에 나타날 때 이어 보이지 않도록 정리한다.
        ResetEffects();
        SetWarning(false);

        // 던전에서는 정지 중에도 젤리 머리가 호흡하므로, 활성화되면 곧바로 깨운다.
        if (true == bActive && 0f < frontEdgeStrength)
            bEdgeActive = true;

        // 무적 상태도 맵이 바뀔 때마다 비운다. 꺼짐 알림이 어떤 이유로 빠져도 다음 맵까지 연출이 남지 않게 한다.
        // 던전 쪽은 이 호출(DecalreDungeonTypeSignal)이 차량 활성화(ReadyPortal) 직후 같은 프레임에 오므로
        // 휴식 구역 진입 알림(차량의 다음 Update)보다 항상 먼저다. 입장 무적 켜짐은 그보다도 한참 뒤다.
        ResetInvincible();
    }

    /// <summary>
    /// 스태미나가 닳지 않는 무적 구간의 원인별 시작(true) / 종료(false).
    ///   · DungeonEntry: HUD가 올라오기 시작하는 프레임에 켜지고, 실제 소모가 시작되는 프레임에 꺼진다(약 0.7초 간격).
    ///   · RestArea: "휴식" 특성으로 열린 차량 주변 휴식 구역에 들어가고 나갈 때.
    /// 스폰 지점이 휴식 구역 안이라 두 원인은 자주 겹친다. 그래서 원인별로 따로 기억하고, 둘을 합친
    /// 무적 여부가 실제로 바뀔 때만 연출을 켜고 끈다(입장 무적이 끝나도 원 안이면 연출이 이어진다).
    /// </summary>
    public void SetInvincible(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        // 마을에서는 무적 연출을 쓰지 않는다. 마을 차량에도 같은 휴식 구역이 있어 알림은 오지만 기록하지도 않는다.
        if (false == bActive)
            return;

        bool bWasInvincible = IsInvincible;

        if (EStaminaInvincibleSource.DungeonEntry == _source)
            bEntryInvincible = _bInvincible;
        else if (EStaminaInvincibleSource.RestArea == _source)
            bRestAreaInvincible = _bInvincible;

        if (bWasInvincible != IsInvincible)
            PlayInvincibleEffect(IsInvincible);
    }

    private bool IsInvincible => bEntryInvincible || bRestAreaInvincible;

    private void ResetInvincible()
    {
        bool bWasInvincible = IsInvincible;

        bEntryInvincible = false;
        bRestAreaInvincible = false;

        if (true == bWasInvincible)
            PlayInvincibleEffect(false);
    }

    // 켜짐: 프리즘이 "딱!" 하고 켜진다. 꺼짐: 프리즘이 면 단위로 서서히 풀린다.
    private void PlayInvincibleEffect(bool _bOn)
    {
        if (true == _bOn)
            StartPrism();
        else
            BeginPrismRelease();
    }

    // 프리즘 무적 연출

    // 프리즘이 완전히 켜져 있는 동안만 true. 이때의 회복은 평소 플래시에 더해 면의 파도도 함께 보여 준다.
    private bool IsPrismShowing
    {
        get { return PrismPhase.On == prismPhase; }
    }

    private static float PrismFrac(float _value)
    {
        return _value - Mathf.Floor(_value);
    }

    private static void PrismHash(float _x, float _y, out float _hx, out float _hy)
    {
        _hx = PrismFrac(Mathf.Sin(_x * 127.1f + _y * 311.7f) * 43758.5453f);
        _hy = PrismFrac(Mathf.Sin(_x * 269.5f + _y * 183.3f) * 43758.5453f);
    }

    // 윤곽 표 위에 보로노이 면을 한 번만 만든다. 면 배치는 화면에 고정이고 광원만 돈다.
    private void BuildPrismFaces()
    {
        if (null == prismSurface || false == bSilhouetteReady || 0 >= barW || 0 >= barH)
            return;

        float _facet = Mathf.Max(2f, prismFacetSize);
        int _count = barW * barH;
        int _paletteLength = null != prismPalette ? prismPalette.Length : 0;

        prismFaceMap = new int[_count];
        prismBuffer = new Color32[_count];

        List<PrismFace> _list = new List<PrismFace>(64);
        Dictionary<int, int> _ids = new Dictionary<int, int>(64);

        for (int y = 0; y < barH; y++)
        {
            for (int x = 0; x < barW; x++)
            {
                int _index = y * barW + x;

                if (false == barInside[_index])
                {
                    prismFaceMap[_index] = -1;
                    continue;
                }

                float _cx = x / _facet;
                float _cy = y / _facet;
                float _baseX = Mathf.Floor(_cx);
                float _baseY = Mathf.Floor(_cy);
                float _fx = _cx - _baseX;
                float _fy = _cy - _baseY;

                float _bestDist = 8f;
                float _bestOffX = 0f;
                float _bestOffY = 0f;
                float _bestSiteX = 0f;
                float _bestSiteY = 0f;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float _hx;
                        float _hy;
                        PrismHash(_baseX + ox, _baseY + oy, out _hx, out _hy);

                        float _siteX = ox + _hx;
                        float _siteY = oy + _hy;
                        float _dx = _siteX - _fx;
                        float _dy = _siteY - _fy;
                        float _dist = _dx * _dx + _dy * _dy;

                        if (_dist < _bestDist)
                        {
                            _bestDist = _dist;
                            _bestOffX = ox;
                            _bestOffY = oy;
                            _bestSiteX = _siteX;
                            _bestSiteY = _siteY;
                        }
                    }
                }

                float _cellX = _baseX + _bestOffX;
                float _cellY = _baseY + _bestOffY;
                int _key = (Mathf.RoundToInt(_cellX) + PrismCellKeyOffset) * PrismCellKeyStride + (Mathf.RoundToInt(_cellY) + PrismCellKeyOffset);

                int _faceIndex;
                if (false == _ids.TryGetValue(_key, out _faceIndex))
                {
                    _faceIndex = _list.Count;
                    _ids.Add(_key, _faceIndex);

                    float _centerX = (_baseX + _bestSiteX) * _facet;
                    float _centerY = (_baseY + _bestSiteY) * _facet;

                    float _crx;
                    float _cry;
                    PrismHash(_cellX + 17.3f, _cellY + 17.3f, out _crx, out _cry);

                    float _angle = _crx * 6.2831853f;
                    Vector3 _normal = new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle), Mathf.Lerp(0.35f, 1.25f, _cry)).normalized;

                    // 이웃 면은 인접한 색이 되도록 가로 위치를 천천히 따라가되, 면마다 한 칸 안팎으로 흔든다.
                    Color _color = barBaseColor;
                    int _paletteIndex = -1;
                    if (0 < _paletteLength)
                    {
                        float _pos = _centerX / 26f + (_crx - 0.5f) * 1.6f + (_centerY / 18f - 0.5f) * 0.9f;
                        int _i0 = Mathf.FloorToInt(_pos);
                        _paletteIndex = ((_i0 % _paletteLength) + _paletteLength) % _paletteLength;
                        _color = prismPalette[_paletteIndex];
                    }

                    float _srx;
                    float _sry;
                    PrismHash(_cellX + 91.7f, _cellY + 91.7f, out _srx, out _sry);

                    float _period = Mathf.Lerp(prismTwinkleCycle.x, prismTwinkleCycle.y, PrismFrac(_sry * 13.7f + 0.31f));

                    PrismFace _face = new PrismFace();
                    _face.nx = _normal.x;
                    _face.ny = _normal.y;
                    _face.nz = _normal.z;
                    _face.color = _color;
                    _face.centerX = _centerX;
                    _face.twinkleKey = PrismFrac(_srx * 5.3f + 0.17f);
                    _face.twinklePeriod = Mathf.Max(0.1f, _period);
                    _face.twinklePhase = PrismFrac(_srx * 7.1f + _sry * 3.3f) * _face.twinklePeriod;
                    _face.releaseThreshold = 0.12f + 0.78f * PrismFrac(_srx * 9.1f + _sry * 4.3f);
                    // Wave 스타일: 왼쪽에서 오른쪽으로 쓸고 가되 면마다 조금씩 흔든다(0~0.95).
                    _face.releaseWaveKey = Mathf.Clamp01(_centerX / Mathf.Max(1f, barW)) * 0.8f + 0.15f * PrismFrac(_srx * 3.7f + _sry * 8.1f);
                    _face.paletteIndex = _paletteIndex;
                    _list.Add(_face);
                }

                prismFaceMap[_index] = _faceIndex;
            }
        }

        prismFaces = _list.ToArray();
        prismFaceLevel = new float[prismFaces.Length];
        prismFaceTone = new int[prismFaces.Length];
        prismFaceColors = new Color32[prismFaces.Length * PrismColorVariants];

        // 픽셀의 위치 종류(윗변 / 아랫변 / 왼쪽 끝)는 윤곽이 바뀌지 않으므로 한 번만 계산한다.
        prismPixelKind = new byte[_count];

        for (int y = 0; y < barH; y++)
        {
            for (int x = 0; x < barW; x++)
            {
                if (false == barInside[y * barW + x])
                    continue;

                byte _kind = 0;

                if (false == IsInsideBar(x, y + 1))
                    _kind |= PrismKindTop;

                if (false == IsInsideBar(x, y - 1))
                    _kind |= PrismKindBottom;

                if (false == IsInsideBar(x - 1, y))
                    _kind |= PrismKindLeft;

                prismPixelKind[y * barW + x] = _kind;
            }
        }

        if (null == prismTexture || prismTexture.width != barW || prismTexture.height != barH)
        {
            if (null != prismTexture)
                Destroy(prismTexture);

            prismTexture = new Texture2D(barW, barH, TextureFormat.RGBA32, false);
            prismTexture.name = "HUD_Stemina_Prism";
            prismTexture.filterMode = FilterMode.Point;
            prismTexture.wrapMode = TextureWrapMode.Clamp;
        }

        // 새 텍스처의 초기 내용은 불투명 흰색이라, 어떤 경로로든 먼저 보이지 않도록 투명으로 비워 둔다.
        System.Array.Clear(prismBuffer, 0, prismBuffer.Length);
        prismTexture.SetPixelData(prismBuffer, 0);
        prismTexture.Apply(false);

        prismSurface.texture = prismTexture;
        prismSurface.enabled = false;
    }

    private bool IsInsideBar(int _x, int _y)
    {
        return 0 <= _x && _x < barW && 0 <= _y && _y < barH && true == barInside[_y * barW + _x];
    }

    // 켜짐: 풀리는 중이어도 현재 단계에서 곧바로 "딱!"으로 돌아간다.
    private void StartPrism()
    {
        if (null == prismSurface)
            return;

        EnsureSilhouette();

        if (false == bSilhouetteReady || null == prismFaces || 0 == prismFaces.Length)
        {
            bPrismEngagePending = true;
            return;
        }

        bPrismEngagePending = false;
        prismPhase = PrismPhase.On;
        prismEngageTimer = 0f;
        prismReleaseTimer = 0f;
        prismStepTimer = 0f;
        prismGainTimer = -1f;

        prismSurface.enabled = true;
        CapturePrismPaintTimers();
        PaintPrism();
    }

    // 지금의 시각 값을 그리기용으로 복사한다. 단계(14fps)가 바뀔 때만 부른다.
    private void CapturePrismPaintTimers()
    {
        prismPaintTime = prismTime;
        prismPaintEngageTimer = prismEngageTimer;
        prismPaintReleaseTimer = prismReleaseTimer;
        prismPaintGainTimer = prismGainTimer;
    }

    // 꺼짐: 켜져 있었다면 면 단위 풀림을 시작한다. 이미 풀리는 중이거나 꺼져 있으면 아무것도 하지 않는다.
    private void BeginPrismRelease()
    {
        bPrismEngagePending = false;

        if (PrismPhase.On != prismPhase)
            return;

        prismPhase = PrismPhase.Releasing;
        prismReleaseTimer = 0f;
    }

    // 풀림 연출 없이 즉시 끈다(피격, 맵 전환, 풀림 종료).
    private void StopPrism()
    {
        bPrismEngagePending = false;
        prismGainTimer = -1f;

        if (PrismPhase.Off == prismPhase)
            return;

        prismPhase = PrismPhase.Off;

        if (null != prismBuffer && null != prismTexture)
        {
            System.Array.Clear(prismBuffer, 0, prismBuffer.Length);
            prismTexture.SetPixelData(prismBuffer, 0);
            prismTexture.Apply(false);
        }

        if (null != prismSurface)
            prismSurface.enabled = false;
    }

    // 무적 중 회복: 늘어난 구간의 면이 왼쪽에서 오른쪽으로 파도처럼 번쩍인다.
    private void StartPrismGainWave(float _lowRatio, float _highRatio)
    {
        prismGainLowPx = barW * Mathf.Clamp01(_lowRatio);
        prismGainHighPx = barW * Mathf.Clamp01(_highRatio);
        prismGainTimer = 0f;
    }

    private void UpdatePrism(float _dt)
    {
        if (true == bPrismEngagePending && true == IsInvincible)
            StartPrism();

        if (PrismPhase.Off == prismPhase)
            return;

        prismEngageTimer += _dt;
        prismTime += _dt;

        if (0f <= prismGainTimer)
        {
            prismGainTimer += _dt;

            if (prismGainTimer > prismGainWaveDuration + 0.5f)
                prismGainTimer = -1f;
        }

        if (PrismPhase.Releasing == prismPhase)
        {
            prismReleaseTimer += _dt;

            if (prismReleaseTimer >= prismReleaseDuration)
            {
                StopPrism();
                return;
            }
        }

        float _interval = 1f / Mathf.Max(1f, prismStepRate);
        prismStepTimer += _dt;

        if (prismStepTimer >= _interval)
        {
            prismStepTimer -= _interval;

            // 프레임이 크게 끊겼다면 밀린 시간은 버린다.
            if (prismStepTimer > _interval)
                prismStepTimer = 0f;

            CapturePrismPaintTimers();
            PaintPrism();
            return;
        }

        // 단계 사이에도 채움 앞쪽이 움직였다면(차오름, 지속 소모) 같은 시각 값으로 다시 그려 앞쪽 끝을 맞춘다.
        int _filledNow = Mathf.Min(barW, Mathf.FloorToInt(barW * Mathf.Clamp01(shownRatio)));

        if (_filledNow != prismPaintedFilledPx)
            PaintPrism();
    }

    // 지금 시각의 프리즘을 텍스처에 다시 그린다. 채워진 구간 안에서만 칠하고, 풀려 나간 면은 투명으로 둬 아래 Fill이 보이게 한다.
    private void PaintPrism()
    {
        if (null == prismBuffer || null == prismTexture || null == prismFaces || null == prismFaceMap
            || null == prismFaceColors || null == prismPixelKind || null == prismFaceTone)
            return;

        int _filledPx = Mathf.Min(barW, Mathf.FloorToInt(barW * Mathf.Clamp01(shownRatio)));
        prismPaintedFilledPx = _filledPx;

        float _lightAngle = prismPaintTime * PrismLightSpeed;
        float _lx = Mathf.Cos(_lightAngle);
        float _ly = Mathf.Sin(_lightAngle);
        float _lz = 0.75f;
        float _lengthInv = 1f / Mathf.Sqrt(_lx * _lx + _ly * _ly + _lz * _lz);
        _lx *= _lengthInv;
        _ly *= _lengthInv;
        _lz *= _lengthInv;

        bool _bReleasing = PrismPhase.Releasing == prismPhase;
        float _releaseP = true == _bReleasing ? Mathf.Clamp01(prismPaintReleaseTimer / Mathf.Max(0.01f, prismReleaseDuration)) : 0f;
        int _tones = Mathf.Max(1, prismReleaseSteps);
        float _fadeSpan = Mathf.Clamp(prismReleaseFadeSpan, 0.1f, 0.6f);
        bool _bWave = PrismReleaseStyle.Wave == prismReleaseStyle;
        float _density = Mathf.Max(0f, prismTwinkleDensity) * (true == _bReleasing ? 1f - _releaseP : 1f);

        // 켜짐 직후: 팝 구간은 흰색, 이어서 모든 면이 한꺼번에 번쩍였다가 세 단계로 가라앉는다.
        float _engageBoost = 0f;
        float _segment = Mathf.Max(0.04f, prismSettleDuration) * 0.25f;
        bool _bPop = prismPaintEngageTimer < prismPopDuration;

        if (prismPaintEngageTimer < _segment * 4f)
        {
            if (prismPaintEngageTimer < _segment * 2f)
                _engageBoost = 1f;
            else if (prismPaintEngageTimer < _segment * 3f)
                _engageBoost = 0.6f;
            else
                _engageBoost = 0.3f;
        }

        // 면 단위 값(번쩍임 세기, 풀려 나갔는지)을 먼저 정한다. -1이면 풀려서 투명.
        for (int i = 0; i < prismFaces.Length; i++)
        {
            PrismFace _face = prismFaces[i];
            float _level = 0f;

            if (_face.twinkleKey < _density * 0.5f)
            {
                float _tt = Mathf.Repeat(prismPaintTime + _face.twinklePhase, _face.twinklePeriod);

                if (_tt < prismTwinkleLength)
                    _level = PrismTwinkleLevels[Mathf.Min(3, Mathf.FloorToInt(_tt / Mathf.Max(0.01f, prismTwinkleLength) * 4f))];
            }

            if (_level < _engageBoost)
                _level = _engageBoost;

            if (0f <= prismPaintGainTimer && _face.centerX >= prismGainLowPx - 6f && _face.centerX <= prismGainHighPx + 6f)
            {
                float _u = prismPaintGainTimer - Mathf.Max(0f, (_face.centerX - prismGainLowPx) / barW * 0.5f);

                if (0f <= _u && _u < prismGainWaveDuration)
                {
                    float _wave = PrismGainLevels[Mathf.Min(3, Mathf.FloorToInt(_u / Mathf.Max(0.01f, prismGainWaveDuration) * 4f))];

                    if (_level < _wave)
                        _level = _wave;
                }
            }

            // 면마다 풀림 구간(전체의 _fadeSpan 비율)을 갖고, 그 안에서 톤 단계로 바 색에 가까워진 뒤 투명이 된다.
            prismFaceTone[i] = -1;

            if (true == _bReleasing)
            {
                float _key = true == _bWave ? _face.releaseWaveKey : _face.releaseThreshold;
                float _q = (_releaseP - _key * (1f - _fadeSpan)) / _fadeSpan;

                if (1f <= _q)
                    _level = -1f;
                else if (0f < _q)
                    prismFaceTone[i] = Mathf.Min(_tones - 1, Mathf.FloorToInt(_q * _tones));
            }

            prismFaceLevel[i] = _level;
        }

        bool _bWarn = true == bWarningGauge && null != barFillImage;
        Color _warn = true == _bWarn ? barFillImage.color : Color.white;

        // 면마다 색 변형 4종(일반 / 윗변 / 아랫변 / 가장자리)을 한 번만 계산한다. 픽셀 루프는 표 조회만 한다.
        // 계산식과 순서는 픽셀마다 계산하던 이전 방식과 같다.
        for (int i = 0; i < prismFaces.Length; i++)
        {
            float _level = prismFaceLevel[i];

            if (0f > _level)
                continue;

            PrismFace _face = prismFaces[i];

            float _ndl = Mathf.Clamp01(_face.nx * _lx + _face.ny * _ly + _face.nz * _lz);
            float _shade = Mathf.Round(_ndl * 3f) / 3f;
            bool _bLit = 0.8f <= _ndl;

            float _r;
            float _g;
            float _b;
            ShadePrismColor(_face.color, _shade, _bLit, out _r, out _g, out _b);

            if (0f < _level)
            {
                float _mix = 0.6f * _level;
                _r = Mathf.Lerp(_r, 1f, _mix);
                _g = Mathf.Lerp(_g, 1f, _mix);
                _b = Mathf.Lerp(_b, 1f, _mix);
            }

            int _tone = prismFaceTone[i];

            if (0 <= _tone)
                ApplyPrismReleaseTone(in _face, _tone, _tones, _shade, _bLit, ref _r, ref _g, ref _b);

            int _slot = i * PrismColorVariants;

            // 일반
            prismFaceColors[_slot] = PackPrismColor(_r, _g, _b, _bPop);

            // 윗변은 밝게. 저스태미나 경고 중에는 윗변 한 줄만 경고 색(채움 틴트의 현재 색)을 유지한다.
            float _tr = Mathf.Lerp(_r, 1f, 0.45f);
            float _tg = Mathf.Lerp(_g, 1f, 0.45f);
            float _tb = Mathf.Lerp(_b, 1f, 0.45f);

            if (true == _bWarn)
            {
                _tr = Mathf.Lerp(_tr, _warn.r, 0.9f);
                _tg = Mathf.Lerp(_tg, _warn.g, 0.9f);
                _tb = Mathf.Lerp(_tb, _warn.b, 0.9f);
            }

            prismFaceColors[_slot + 1] = PackPrismColor(_tr, _tg, _tb, _bPop);

            // 아랫변은 어둡게
            prismFaceColors[_slot + 2] = PackPrismColor(_r * 0.74f, _g * 0.74f, _b * 0.74f, _bPop);

            // 좌우 끝과 채움 앞쪽은 살짝 어둡게
            prismFaceColors[_slot + 3] = PackPrismColor(_r * 0.86f, _g * 0.86f, _b * 0.86f, _bPop);
        }

        Color32 _clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < barH; y++)
        {
            int _row = y * barW;

            for (int x = 0; x < barW; x++)
            {
                int _index = _row + x;
                int _faceIndex = prismFaceMap[_index];

                if (0 > _faceIndex || x >= _filledPx || 0f > prismFaceLevel[_faceIndex])
                {
                    prismBuffer[_index] = _clear;
                    continue;
                }

                // 우선순위: 윗변 > 아랫변 > (왼쪽 끝 또는 채움 앞쪽) > 일반
                byte _kind = prismPixelKind[_index];
                int _variant = 0;

                if (0 != (_kind & PrismKindTop))
                    _variant = 1;
                else if (0 != (_kind & PrismKindBottom))
                    _variant = 2;
                else if (0 != (_kind & PrismKindLeft) || x == _filledPx - 1)
                    _variant = 3;

                prismBuffer[_index] = prismFaceColors[_faceIndex * PrismColorVariants + _variant];
            }
        }

        prismTexture.SetPixelData(prismBuffer, 0);
        prismTexture.Apply(false);
    }

    // 면 색에 4단계 명암을 입힌다. 광원과 정렬된 면(_bLit)은 한 단계 더 밝다.
    private static void ShadePrismColor(Color _color, float _shade, bool _bLit, out float _r, out float _g, out float _b)
    {
        _r = Mathf.Lerp(_color.r * PrismDeepFactor, Mathf.Lerp(_color.r, 1f, PrismLitMix), _shade);
        _g = Mathf.Lerp(_color.g * PrismDeepFactor, Mathf.Lerp(_color.g, 1f, PrismLitMix), _shade);
        _b = Mathf.Lerp(_color.b * PrismDeepFactor, Mathf.Lerp(_color.b, 1f, PrismLitMix), _shade);

        if (true == _bLit)
        {
            _r = Mathf.Lerp(_r, 1f, 0.12f);
            _g = Mathf.Lerp(_g, 1f, 0.12f);
            _b = Mathf.Lerp(_b, 1f, 0.12f);
        }
    }

    // 면의 팔레트 색에서 _offset칸 뒤의 색. 팔레트가 없으면 면 자신의 색을 쓴다.
    private Color GetPrismPaletteColor(in PrismFace _face, int _offset)
    {
        int _length = null != prismPalette ? prismPalette.Length : 0;

        if (0 >= _length || 0 > _face.paletteIndex)
            return _face.color;

        return prismPalette[(_face.paletteIndex + _offset) % _length];
    }

    // 풀리는 면의 톤 단계(_tone)를 스타일에 맞춰 현재 색에 덧입힌다. 마지막 단계 뒤에는 면이 투명이 되어 아래 Fill이 드러난다.
    private void ApplyPrismReleaseTone(in PrismFace _face, int _tone, int _tones, float _shade, bool _bLit, ref float _r, ref float _g, ref float _b)
    {
        Color _bar = barColorNow;
        float _toBar = 0f;
        float _toWhite = 0f;

        switch (prismReleaseStyle)
        {
            case PrismReleaseStyle.Flash:
                if (0 == _tone)
                    _toWhite = 0.8f;
                else if (1 == _tone)
                    _toWhite = 0.35f;
                else
                    _toBar = 0.7f * (_tone - 1) / Mathf.Max(1, _tones - 2);
                break;

            case PrismReleaseStyle.Wave:
                if (0 == _tone)
                    _toWhite = 0.85f;
                else if (1 == _tone)
                    _toWhite = 0.4f;
                else
                    _toBar = 0.4f + 0.35f * (_tone - 2) / Mathf.Max(1, _tones - 3);
                break;

            case PrismReleaseStyle.Rainbow:
                if (0 < _tone)
                {
                    // 이웃 팔레트 색으로 한 번 더 바뀐 뒤 바 색으로 이행한다.
                    float _pr;
                    float _pg;
                    float _pb;
                    ShadePrismColor(GetPrismPaletteColor(in _face, 1 == _tone ? 1 : 2), _shade, _bLit, out _pr, out _pg, out _pb);
                    _r = _pr;
                    _g = _pg;
                    _b = _pb;

                    if (1 == _tone)
                        _toWhite = 0.2f;
                    else
                        _toBar = 0.7f * (_tone - 1) / Mathf.Max(1, _tones - 2);
                }
                break;

            default:
                _toBar = 0.9f * _tone / _tones;
                break;
        }

        if (0f < _toWhite)
        {
            _r = Mathf.Lerp(_r, 1f, _toWhite);
            _g = Mathf.Lerp(_g, 1f, _toWhite);
            _b = Mathf.Lerp(_b, 1f, _toWhite);
        }

        if (0f < _toBar)
        {
            _r = Mathf.Lerp(_r, _bar.r, _toBar);
            _g = Mathf.Lerp(_g, _bar.g, _toBar);
            _b = Mathf.Lerp(_b, _bar.b, _toBar);
        }
    }

    // 젤리 머리 연출

    // 매 프레임 불러, 던전에 있는 동안 스프링을 적분하고 30fps로 머리를 그린다.
    // 프리즘이 켜져 있으면 프리즘이 오버레이 전체를 쓰므로 아무것도 하지 않는다.
    private void UpdateFrontEdge(float _dt)
    {
        if (PrismPhase.Off != prismPhase)
        {
            bEdgeActive = false;
            bEdgeDrawn = false;
            bEdgeGain = false;
            bEdgeDrain = false;
            edgeVelocity = 0f;
            edgeFastSpeed = 0f;
            edgeAppear = 0f;
            edgeDrainWeight = 0f;
            edgePrevFilled = -1;
            edgePrevShown = Mathf.Clamp01(shownRatio);
            return;
        }

        bool _bAllowed = 0f < frontEdgeStrength && true == bActive && true == bSilhouetteReady
            && null != prismSurface && null != prismTexture && null != prismBuffer
            && null != edgeMask && null != barRowColors && prismBuffer.Length == edgeMask.Length;

        if (false == _bAllowed)
        {
            ClearFrontEdge();
            return;
        }

        bEdgeActive = true;

        float _s = Mathf.Clamp01(frontEdgeStrength);
        float _dtSafe = Mathf.Max(0.0001f, _dt);
        float _shown = Mathf.Clamp01(shownRatio);

        // 앞쪽 속도(비율/초)를 시간 보정해 이동평균한다. 프레임 레이트나 한 프레임의 튐에 판정이 흔들리지 않는다.
        float _vRaw = (_shown - edgePrevShown) / _dtSafe;
        edgePrevShown = _shown;
        edgeVelocity += (_vRaw - edgeVelocity) * (1f - Mathf.Exp(-_dtSafe / EdgeVelTau));

        // 회복의 늘어남을 정하는 빠른 속도(짧은 시정수). 차오름이 감속하면 머리도 함께 줄어 멈출 때 갑자기 수축하지 않는다.
        edgeFastSpeed += (Mathf.Max(0f, _vRaw) - edgeFastSpeed) * (1f - Mathf.Exp(-_dtSafe / EdgeFastTau));

        // 머리 등장: 처음 생길 때 갑자기 튀어나와 회복처럼 보이지 않게 서서히 올린다.
        edgeAppear += (1f - edgeAppear) * (1f - Mathf.Exp(-_dtSafe / EdgeAppearTau));

        bool _bGain = true == bFillingUp || EdgeGainVel < edgeVelocity;

        if (true == _bGain)
            bEdgeDrain = false;
        else if (true == bEdgeDrain)
            bEdgeDrain = -EdgeDrainExit > edgeVelocity;
        else
            bEdgeDrain = -EdgeDrainEnter > edgeVelocity;

        // 충격은 회복이 시작되고 끝날 때만 준다. 소모와 정지 사이는 가중치로 부드럽게 이어진다.
        if (_bGain != bEdgeGain)
        {
            float _kickSize = true == _bGain ? Mathf.Min(1f, bundleGainRatio / 0.1f) : 1f;
            edgeSpringVel += (true == _bGain ? EdgeGainKick : EdgeSettleKick) * _s * _kickSize;
            bEdgeGain = _bGain;
        }

        float _drainTarget = true == bEdgeDrain ? 1f : 0f;
        edgeDrainWeight += (_drainTarget - edgeDrainWeight) * (1f - Mathf.Exp(-_dtSafe / EdgeDrainTau));

        // 소모로 채움이 한 픽셀 줄 때마다 작은 충격과 새 물결 위상을 준다(같은 패턴이 반복되지 않게 크기와 위상을 흔든다).
        int _filledNow = Mathf.Min(barW, Mathf.FloorToInt(barW * _shown));

        if (0 <= edgePrevFilled && _filledNow < edgePrevFilled && 0.2f < edgeDrainWeight)
        {
            edgeSpringVel -= EdgeTickKick * _s * (0.5f + Random.value);
            edgeRipplePhase = Random.value * 6.2832f;
        }

        edgePrevFilled = _filledNow;

        edgeTime += _dt;

        float _rest = EdgeRestLen + EdgeBreath * _s * (1f + 0.8f * edgeDrainWeight) * Mathf.Sin(edgeTime * 2.4f);
        float _target = _rest + (EdgeDrainLen - EdgeRestLen) * _s * edgeDrainWeight;

        if (true == _bGain)
            _target = _rest + Mathf.Min(EdgeGainExtraMax, edgeFastSpeed * EdgeGainSpeedK) * _s;

        float _h = Mathf.Min(_dt, 0.05f) * 0.25f;

        for (int i = 0; i < 4; i++)
        {
            float _a = EdgeSpringK * (_target - edgeSpringLen) - EdgeSpringC * edgeSpringVel;
            edgeSpringVel += _a * _h;
            edgeSpringLen += edgeSpringVel * _h;
        }

        edgeSpringLen = Mathf.Clamp(edgeSpringLen, 0.6f, EdgeRestLen + EdgeGainExtraMax + 0.6f);

        edgeStepTimer += _dt;

        if (edgeStepTimer < EdgeStepInterval)
            return;

        edgeStepTimer = 0f;
        PaintFrontEdge();
    }

    // 오버레이에 그린 내용을 비우고 연출을 잠재운다. 프리즘이 오버레이를 쓰는 중에는 텍스처를 건드리지 않는다.
    private void ClearFrontEdge()
    {
        bEdgeActive = false;
        bEdgeGain = false;
        bEdgeDrain = false;
        edgeStepTimer = 0f;
        edgeVelocity = 0f;
        edgeFastSpeed = 0f;
        edgeAppear = 0f;
        edgeDrainWeight = 0f;
        edgePrevFilled = -1;
        edgePrevShown = Mathf.Clamp01(shownRatio);
        edgeSpringLen = EdgeRestLen;
        edgeSpringVel = 0f;

        ClearEdgePixels();
    }

    // 머리를 그린 픽셀을 지우고 오버레이를 끈다(프리즘이 쓰는 중에는 건드리지 않는다).
    private void ClearEdgePixels()
    {
        if (true == bEdgeDrawn && PrismPhase.Off == prismPhase && null != prismBuffer && null != prismTexture)
        {
            System.Array.Clear(prismBuffer, 0, prismBuffer.Length);
            prismTexture.SetPixelData(prismBuffer, 0);
            prismTexture.Apply(false);

            if (null != prismSurface)
                prismSurface.enabled = false;
        }

        bEdgeDrawn = false;
    }

    // 채움 앞쪽 바깥에 말랑한 둥근 머리를 그린다. 불투명 도트이고, 길이는 스프링 값이다.
    // 앞쪽의 소수 부분을 반길이에 더해 머리 끝이 픽셀 경계에서 점프하지 않게 한다.
    // 위아래 끝까지 이어지는 캡 모양(가운데는 평평, 위/아래 모서리만 둥글게), 소모 중 표면 물결과 기울기 흔들림,
    // 가장자리 림 라이트 명암(이음매는 바 본체와 같은 색), 바 틴트 반영, 바깥 1px 테두리를 더한다.
    private void PaintFrontEdge()
    {
        float _frontPx = barW * Mathf.Clamp01(shownRatio);
        int _filled = Mathf.Min(barW, Mathf.FloorToInt(_frontPx));

        // 비었거나 가득 찼으면 그릴 머리가 없다.
        if (0 >= _filled || barW <= _filled)
        {
            ClearEdgePixels();
            return;
        }

        float _s = Mathf.Clamp01(frontEdgeStrength);
        int _fl = _filled - 1;
        float _len = (Mathf.Max(0.6f, edgeSpringLen) + (_frontPx - _filled)) * edgeAppear;

        // 세로 범위: 바 윤곽이 실제로 차지하는 행 전체. 늘어나도 높이를 줄이지 않아 윗줄/아랫줄이 머리로 이어진다.
        // 소모 중 느린 흔들림은 중심을 옮기지 않고 위/아래 행의 길이 차(기울기)로 준다.
        float _tilt = EdgeSloshAmp * _s * edgeDrainWeight * Mathf.Sin(edgeTime * 1.7f + edgeRipplePhase);
        float _half = (edgeRowHi - edgeRowLo + 1) * 0.5f;
        float _cy = (edgeRowLo + edgeRowHi) * 0.5f;

        float _ripple = EdgeRippleAmp * _s * edgeDrainWeight;
        int _maxX = Mathf.Min(barW - 1, _fl + Mathf.CeilToInt(_len + _ripple * 1.2f + Mathf.Abs(_tilt)) + 1);

        System.Array.Clear(prismBuffer, 0, prismBuffer.Length);

        // 머리 영역의 마스크와 명암을 구한다(바 높이 방향 위쪽이 +y, 빛은 왼쪽 위).
        for (int y = 0; y < barH; y++)
        {
            // 행의 세로 위치(위쪽이 +). 평평한 면 밖에서만 둥글게 말려 길이 비율이 줄어든다.
            float _v = (y - _cy) / _half;
            float _vAbs = Mathf.Abs(_v);
            float _w = EdgeFlatRatio < _vAbs ? Mathf.Min(1f, (_vAbs - EdgeFlatRatio) / (1f - EdgeFlatRatio)) : 0f;
            float _f = Mathf.Sqrt(1f - _w * _w);

            float _ay = _len + _tilt * _v;

            if (0f < _ripple)
            {
                _ay += _ripple * (Mathf.Sin(y * 0.9f - edgeTime * 5.2f + edgeRipplePhase) * 0.7f
                    + Mathf.Sin(y * 0.45f + edgeTime * 2.3f + edgeRipplePhase * 1.7f) * 0.4f);
            }

            _ay = Mathf.Max(0.5f, _ay);

            for (int x = _fl + 1; x <= _maxX + 1 && x < barW; x++)
            {
                int _i = y * barW + x;
                edgeMask[_i] = 0;
                edgeShade[_i] = 0f;

                if (x > _maxX)
                    continue;

                float _u = (x - (_fl + 0.5f)) / _ay;

                if (_u > _f)
                    continue;

                edgeMask[_i] = 1;
                edgeShade[_i] = _u / Mathf.Max(0.0001f, _f);
            }
        }

        // 바 본체(Fill)는 현재 틴트(저피로도 경고의 빨강 펄스 포함)가 곱해져 그려진다. 머리도 같은 틴트를 곱해야 색이 갈라지지 않는다.
        Color _tint = null != barFillImage ? barFillImage.color : Color.white;

        for (int y = 0; y < barH; y++)
        {
            Color _rc = barRowColors[y];
            float _bodyR = _rc.r * _tint.r;
            float _bodyG = _rc.g * _tint.g;
            float _bodyB = _rc.b * _tint.b;
            float _vRow = (y - _cy) / _half;

            for (int x = _fl + 1; x <= _maxX + 1 && x < barW; x++)
            {
                int _i = y * barW + x;

                if (false == barInside[_i])
                    continue;

                float _r;
                float _g;
                float _b;

                if (0 != edgeMask[_i])
                {
                    float _t = edgeShade[_i];

                    _r = _bodyR;
                    _g = _bodyG;
                    _b = _bodyB;

                    if (0.15f < _vRow && EdgeRimStart < _t)
                    {
                        float _m = EdgeRimOuter < _t ? EdgeRimBright2 : EdgeRimBright1;

                        _r = Mathf.Lerp(_r, 1f, _m);
                        _g = Mathf.Lerp(_g, 1f, _m);
                        _b = Mathf.Lerp(_b, 0.92f, _m);
                    }
                    else if (-0.3f > _vRow && EdgeShadeStart < _t)
                    {
                        _r *= EdgeShadeMul;
                        _g *= EdgeShadeMul;
                        _b *= EdgeShadeMul;
                    }
                }
                else
                {
                    // 마스크 바깥에서 마스크와 맞닿은 1px는 어두운 테두리
                    bool _bAdj = (x - 1 > _fl && 0 != edgeMask[_i - 1])
                        || (x + 1 <= _maxX && 0 != edgeMask[_i + 1])
                        || (y > 0 && 0 != edgeMask[_i - barW])
                        || (y + 1 < barH && 0 != edgeMask[_i + barW]);

                    if (false == _bAdj)
                        continue;

                    // 테두리도 바 틴트를 따른다(아주 어두워지지 않게 최소 밝기를 둔다).
                    _r = Mathf.Max(0.09f, 0.23f * _tint.r);
                    _g = Mathf.Max(0.04f, 0.36f * _tint.g);
                    _b = Mathf.Max(0.03f, 0.10f * _tint.b);
                }

                prismBuffer[_i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(_r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(_g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(_b * 255f), 0, 255),
                    255);
            }
        }

        prismTexture.SetPixelData(prismBuffer, 0);
        prismTexture.Apply(false);
        prismSurface.enabled = true;
        bEdgeDrawn = true;
    }

    // 팝 구간이면 흰색 쪽으로 섞은 뒤 바이트 색으로 바꾼다.
    private static Color32 PackPrismColor(float _r, float _g, float _b, bool _bPop)
    {
        if (true == _bPop)
        {
            _r = Mathf.Lerp(_r, 1f, 0.8f);
            _g = Mathf.Lerp(_g, 1f, 0.8f);
            _b = Mathf.Lerp(_b, 1f, 0.8f);
        }

        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(_r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(_g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(_b * 255f), 0, 255),
            255);
    }

    private void SetWarning(bool _bOn)
    {
        bWarningGauge = _bOn;

        if (true == _bOn)
        {
            if (null != motionPlayer)
            {
                shakeMotion = motionPlayer.Play(shakeTag, bReset: true);
                colorMotion = motionPlayer.Play(colorTag, bReset: true);
            }
        }
        else
        {
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
            }
        }

        // 켜기/끄기 모두 HUD_ScreenBlood 쪽이 중복 호출을 걸러주므로 상태 전이 여부와 무관하게 넘긴다.
        if (null != screenBloodComponent)
            screenBloodComponent.PlayBloodEffect(_bOn);
    }

    // 피격/회복 연출

    private float GetIntensity(float _ratio)
    {
        return Mathf.Clamp01(_ratio / Mathf.Max(0.0001f, fullEffectRatio));
    }

    // 순간 회복이 반영되는 첫 프레임: 게이지가 "회복 전 값"에서 차오르기 시작하도록 보이는 값을 되돌려 놓고 연출을 시작한다.
    private void BeginFillUp(float _ratio)
    {
        float _gain = pendingGainRatio;
        pendingGainRatio = 0f;

        float _high = Mathf.Clamp01(_ratio);

        if (false == bFillingUp)
        {
            shownRatio = Mathf.Clamp01(_ratio - _gain);
            fillSegmentLow = shownRatio;
            bFillingUp = true;
            plannedPlusCount = 0;
        }

        // 묶음 누적: 세기는 이번 호출이 아니라 묶음의 누적량으로 정한다.
        // 단발 회복이면 누적량이 이번 회복량과 같아 이전과 같은 값이 나온다.
        bundleGainRatio += _gain;
        bundleLingerRemain = gainBundleWindow;

        float _bundleIntensity = GetIntensity(bundleGainRatio);

        // 연출하지 않은 회복량이 sparkleMinRatio에 닿을 때마다 한 번 연출한다. 큰 회복은 곧바로, 잘게 들어오는 회복은 쌓여서 닿는 시점에 나간다.
        // 닿기 전까지는 차오름만 보인다. "+"는 그동안 쌓인 회복이 늘린 구간 근처에 놓는다.
        unplayedGainRatio += _gain;

        if (unplayedGainRatio >= sparkleMinRatio)
        {
            float _playedGain = unplayedGainRatio;
            unplayedGainRatio = 0f;

            bFillEvent = true;
            PlanBundlePluses(_high - _playedGain, _high);

            RefreshBarColor();

            // 연출 차례마다 플래시를 다시 시작한다. 직전 플래시가 아직 많이 남아 있고 세기도 그대로면(같은 순간의 연타) 건너뛴다.
            if (FlashRestartBelow >= flashValue || _bundleIntensity > bundleFlashIntensity)
            {
                bundleFlashIntensity = _bundleIntensity;

                // 무적 중 회복도 평소와 같은 플래시가 먼저 번쩍이고, 그 위에 늘어난 구간의 면이 파도처럼 번쩍인다.
                if (true == IsPrismShowing)
                    StartPrismGainWave(fillSegmentLow, _high);

                StartRecoverFlash(_bundleIntensity);
            }
        }

        // 남은 거리를 fillUpDuration 안에 채우는 속도. 이미 차오르는 중에 또 회복되면 구간의 시작은 그대로 두고 남은 거리로 다시 계산한다.
        fillSpeed = Mathf.Max(FillMinSpeed, (_high - shownRatio) / Mathf.Max(0.05f, fillUpDuration));
    }

    // 보이는 값: 차오르는 동안은 실제 비율을 향해 일정한 속도로 차오르고(지속 소모가 겹쳐도 끊기지 않음), 아니면 실제 비율과 같다.
    private void UpdateShownRatio(float _ratio)
    {
        if (false == bFillingUp)
        {
            shownRatio = _ratio;
            return;
        }

        // 실제 값이 보이는 값 이하로 내려갔다면(피격, 지속 소모) 더 차오를 곳이 없으니 맞추고 끝낸다.
        if (_ratio <= shownRatio)
        {
            shownRatio = _ratio;
            EndFillUp();
            return;
        }

        float _gap = _ratio - shownRatio;
        float _dt = Time.deltaTime;
        float _ease = Mathf.Lerp(FillEaseMinFactor, 1f, Mathf.Clamp01(_gap / FillEaseGap));
        float _step = fillSpeed * _ease * _dt;

        shownRatio = Mathf.Min(_ratio, shownRatio + _step);

        if (SnapGap >= _ratio - shownRatio)
        {
            shownRatio = _ratio;
            EndFillUp();
            return;
        }
    }

    // 다 차오르면, 큰 회복은 아직 못 떠오른 "+"가 모두 떠오른다. (플래시는 차오름이 시작될 때 이미 시작했다)
    private void EndFillUp()
    {
        bFillingUp = false;
        bundleLingerRemain = gainBundleWindow;

        if (true == bFillEvent)
        {
            RefreshBarColor();

            FlushPlannedPluses();
        }

        bFillEvent = false;
    }

    // 고스트 바: 피격 직후에는 줄어들기 전 값을 잠깐 붙들고(ghostDelay), 이후 "보이는 값"을 매 프레임 따라간다.
    // 따라가는 목표를 피격 순간의 값으로 고정하면, 지속 소모가 계속되는 동안 잔상이 옛 값에서 멈춘 뒤 한 번에 맞춰져
    // 붉은 조각이 툭 사라진다. 회복되면 예전처럼 즉시 맞춘다.
    private void UpdateGhost(float _ratio, float _prevShown, bool _wasFilling)
    {
        if (null == ghostSlider)
        {
            pendingLossRatio = 0f;
            return;
        }

        if (0f < pendingLossRatio)
        {
            // 줄어들기 전 값에서 잔상을 시작한다. 차오르는 중이었다면 그때 "보이던 값"이 기준이라, 아직 안 채워진 곳이 붉게 보이지 않는다.
            float _start = Mathf.Min(1f, _ratio + pendingLossRatio);
            if (true == _wasFilling)
                _start = Mathf.Min(_start, _prevShown);

            if (_start > shownRatio)
                ghostSlider.value = Mathf.Max(ghostSlider.value, _start);

            pendingLossRatio = 0f;
            ghostHoldRemain = ghostDelay;
            bGhostTrailing = true;
        }

        if (false == bGhostTrailing)
        {
            ghostSlider.value = shownRatio;
            return;
        }

        // 보이는 값이 잔상 이상이면(회복, 또는 따라잡음) 잔상을 맞추고 끝낸다.
        float _gap = ghostSlider.value - shownRatio;
        if (SnapGap >= _gap)
        {
            ghostSlider.value = shownRatio;
            bGhostTrailing = false;
            return;
        }

        float _dt = Time.deltaTime;

        if (0f < ghostHoldRemain)
        {
            ghostHoldRemain -= _dt;
            return;
        }

        // 지수 감쇠: 격차가 클 때 빠르고 끝에서 느려지는 OutQuad와 비슷한 움직임. 끝이 너무 길게 늘어지지 않도록 최소 속도를 둔다.
        float _follow = 1f - Mathf.Exp(-_dt * FollowSharpness / Mathf.Max(0.01f, ghostFollowDuration));
        float _step = Mathf.Max(_gap * _follow, GhostMinFollowSpeed * _dt);
        ghostSlider.value = Mathf.Max(shownRatio, ghostSlider.value - _step);
    }

    // 회복 플래시: 피격 플래시와 같은 경로(길이, 횟수, 알파 규칙)를 쓰고 색만 바 색을 밝힌 라임-흰색으로 바꾼다.
    // 프리즘이 켜져 있는 동안은 파스텔 면에 녹색 기운이 돌지 않도록 순백으로 번쩍인다.
    private void StartRecoverFlash(float _intensity)
    {
        Color _color = PrismPhase.Off != prismPhase ? Color.white : Color.Lerp(barColorNow, Color.white, recoverFlashLighten);
        StartFlash(_color, Mathf.Clamp01(_intensity), recoverFlashDuration, Mathf.Max(1, recoverFlashCount));
    }

    // 바 윤곽과 기본색을 Fill 스프라이트에서 한 번 읽는다. 스프라이트가 읽기 불가여도 GPU 복사로 읽는다.
    // 윤곽은 캔버스 픽셀 단위(바 너비 x 바 높이)로 표를 만들고, 그 안쪽에서 위/아래/좌우 윤곽 줄을 뺀 "몸통 영역"을 따로 만든다.
    // 효과는 몸통 영역 안(과 "+"가 떠오르는 바 위쪽 일정 높이)에서만 그린다. 스텐실 마스크는 쓰지 않는다.
    // (렌더 대상에 스텐실이 없으면 마스크가 동작하지 않고, 알파 윤곽만 쓰면 하이라이트/그림자 줄 위에 효과가 올라간다)
    private void BuildBarSilhouette()
    {
        bSilhouetteReady = false;
        silhouetteRetryAt = Time.unscaledTime + SilhouetteRetryInterval;

        if (null == barFillImage || null == barFillImage.sprite || null == sparkleContainer)
        {
            Debug.LogWarning("[HUD_Stemina] 윤곽 표 생성 실패: barFillImage/sprite/sparkleContainer 중 비어 있는 참조가 있습니다. "
                + "barFillImage=" + (null != barFillImage) + " sparkleContainer=" + (null != sparkleContainer));
            return;
        }

        Sprite _sprite = barFillImage.sprite;
        Texture _source = _sprite.texture;
        Rect _rect = _sprite.textureRect;

        int _sw = Mathf.RoundToInt(_rect.width);
        int _sh = Mathf.RoundToInt(_rect.height);

        // 레이아웃이 아직 계산되지 않아 크기가 0이면 스프라이트 원본 크기(캔버스 1픽셀 = 스프라이트 1픽셀)로 대신한다.
        int _w = Mathf.RoundToInt(sparkleContainer.rect.width);
        int _h = Mathf.RoundToInt(sparkleContainer.rect.height);
        if (0 >= _w)
            _w = _sw;
        if (0 >= _h)
            _h = _sh;

        if (null == _source || 0 >= _w || 0 >= _h || 0 >= _sw || 0 >= _sh)
        {
            Debug.LogWarning("[HUD_Stemina] 윤곽 표 생성 실패: 크기가 0입니다. container=" + sparkleContainer.rect.size + " sprite=" + _sw + "x" + _sh);
            return;
        }

        RenderTexture _rt = RenderTexture.GetTemporary(_source.width, _source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture _prev = RenderTexture.active;

        Graphics.Blit(_source, _rt);
        RenderTexture.active = _rt;

        Texture2D _read = new Texture2D(_sw, _sh, TextureFormat.RGBA32, false);
        _read.ReadPixels(new Rect(_rect.x, _rect.y, _sw, _sh), 0, 0);
        _read.Apply();

        RenderTexture.active = _prev;
        RenderTexture.ReleaseTemporary(_rt);

        Color32[] _pixels = _read.GetPixels32();
        Destroy(_read);

        barW = _w;
        barH = _h;
        barInside = new bool[_w * _h];
        barBody = new bool[_w * _h];

        float _r = 0f;
        float _g = 0f;
        float _b = 0f;
        int _opaque = 0;
        float[] _rowR = new float[_h];
        float[] _rowG = new float[_h];
        float[] _rowB = new float[_h];
        int[] _rowN = new int[_h];

        for (int y = 0; y < _h; y++)
        {
            int _sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / _h * _sh), 0, _sh - 1);

            for (int x = 0; x < _w; x++)
            {
                int _sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / _w * _sw), 0, _sw - 1);
                Color32 _c = _pixels[_sy * _sw + _sx];

                if (SilhouetteAlphaCut < _c.a)
                {
                    barInside[y * _w + x] = true;
                    _r += _c.r;
                    _g += _c.g;
                    _b += _c.b;
                    _opaque++;
                    _rowR[y] += _c.r;
                    _rowG[y] += _c.g;
                    _rowB[y] += _c.b;
                    _rowN[y]++;
                }
            }
        }

        if (0 < _opaque)
            barBaseColor = new Color(_r / _opaque / 255f, _g / _opaque / 255f, _b / _opaque / 255f, 1f);

        // 행별 평균색: 앞쪽 끝부분 연출이 바의 명암 행(윗 하이라이트, 아랫 어두운 줄)에 맞춰 색을 입히는 데 쓴다.
        barRowColors = new Color[_h];

        for (int y = 0; y < _h; y++)
        {
            if (0 < _rowN[y])
                barRowColors[y] = new Color(_rowR[y] / _rowN[y] / 255f, _rowG[y] / _rowN[y] / 255f, _rowB[y] / _rowN[y] / 255f, 1f);
            else
                barRowColors[y] = barBaseColor;
        }

        edgeMask = new byte[_w * _h];
        edgeShade = new float[_w * _h];

        // 젤리 머리의 세로 범위: 바 윤곽이 실제로 차지하는 행(중앙 열 기준). 텍스처 높이와 어긋날 수 있다.
        edgeRowLo = _h;
        edgeRowHi = -1;

        for (int y = 0; y < _h; y++)
        {
            if (false == barInside[y * _w + _w / 2])
                continue;

            if (y < edgeRowLo)
                edgeRowLo = y;

            if (y > edgeRowHi)
                edgeRowHi = y;
        }

        if (0 > edgeRowHi)
        {
            edgeRowLo = 0;
            edgeRowHi = _h - 1;
        }

        // 몸통 영역: 윤곽 안쪽에서, 위/아래/좌우로 정해진 폭만큼 윤곽 밖과 닿지 않는 픽셀만 남긴다. (상자 모양 침식)
        int _top = Mathf.Max(0, bodyInsetTop);
        int _bottom = Mathf.Max(0, bodyInsetBottom);
        int _side = Mathf.Max(0, bodyInsetSide);

        bodyTopRow = 0;

        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                if (false == barInside[y * _w + x])
                    continue;

                bool _bBody = true;

                for (int dy = -_bottom; dy <= _top && true == _bBody; dy++)
                {
                    int _yy = y + dy;

                    for (int dx = -_side; dx <= _side; dx++)
                    {
                        int _xx = x + dx;

                        // 바의 좌우 끝(0, 너비-1)을 넘어서는 곳은 윤곽 밖으로 본다. 위/아래도 마찬가지다.
                        if (0 > _xx || _xx >= _w || 0 > _yy || _yy >= _h || false == barInside[_yy * _w + _xx])
                        {
                            _bBody = false;
                            break;
                        }
                    }
                }

                barBody[y * _w + x] = _bBody;

                if (true == _bBody && y > bodyTopRow)
                    bodyTopRow = y;
            }
        }

        barColorNow = barBaseColor;
        bSilhouetteReady = true;

        BuildPrismFaces();
    }

    // 윤곽 표가 아직 없으면(레이아웃 지연 등으로 첫 시도가 실패한 경우) 일정 간격으로 다시 만든다.
    // 표가 없으면 효과가 전부 숨겨지므로, 게임에서 한 번 실패해도 연출이 영구히 사라지지 않게 한다.
    private void EnsureSilhouette()
    {
        if (true == bSilhouetteReady || Time.unscaledTime < silhouetteRetryAt)
            return;

        BuildBarSilhouette();
    }

    // 캔버스 픽셀 (x, y)(바의 왼쪽 아래가 원점)가 몸통 영역인지. 표가 없으면 안쪽이 아닌 것으로 본다.
    private bool IsInsideBody(int _x, int _y)
    {
        if (0 > _x || _x >= barW || 0 > _y || _y >= barH)
            return false;

        if (false == bSilhouetteReady || null == barBody)
            return false;

        return barBody[_y * barW + _x];
    }

    // 바가 지금 화면에 표시되는 색. 스프라이트 평균색에 Fill의 현재 틴트(경고 펄스 포함)를 곱한 값이라 항상 바와 같은 색 계열이다.
    private void RefreshBarColor()
    {
        Color _tint = null != barFillImage ? barFillImage.color : Color.white;

        barColorNow = new Color(barBaseColor.r * _tint.r, barBaseColor.g * _tint.g, barBaseColor.b * _tint.b, 1f);
    }

    // "+" 입자

    private float GetBarWidth()
    {
        // 레이아웃이 아직 계산되지 않아 0이면 윤곽 표를 만들 때 쓴 폭을 대신 쓴다.
        if (null != sparkleContainer && 0f < sparkleContainer.rect.width)
            return sparkleContainer.rect.width;

        return barW;
    }

    private int GetPlusTotal()
    {
        if (null == sparkleRects || null == sparkleImages)
            return 0;

        return Mathf.Min(sparkleRects.Length, sparkleImages.Length);
    }

    private int ClaimPlusSlot(int _from)
    {
        for (int i = _from; i < pluses.Length; i++)
        {
            if (false == pluses[i].bAlive)
                return i;
        }

        return -1;
    }

    // 회복 묶음을 끝낸다. 누적량과 플래시/"+" 기록을 비워 다음 회복이 새 묶음으로 시작하게 한다.
    private void EndBundle()
    {
        bundleGainRatio = 0f;
        bundleLingerRemain = 0f;
        bundleFlashIntensity = 0f;
        bundlePlannedPlus = 0;
        unplayedGainRatio = 0f;
    }

    // 이번 회복(_lowRatio ~ _highRatio)이 늘린 구간에서 "+"가 켜질 타이밍(앞쪽이 지나가는 위치)을 정해 둔다.
    // 실제로 켜지는 가로 위치는 켜지는 순간의 보이는 끝 근처다(SpawnPlusAtTip). 이 위치는 켜는 순서와 간격만 정한다.
    // 개수는 묶음의 누적 세기가 정한 목표에서 이미 예약한 개수를 뺀 만큼이되, 회복마다 최소 PlusMinPerBurst개는 더한다
    // (세기가 상한이어도 회복이 올 때마다 "+"가 보이게). 구간을 칸으로 나눠 칸마다 하나씩 두어 한꺼번에 켜지지 않게 한다.
    // 잘게 들어오는 회복은 차오름이 곧바로 끝나므로, 예약된 "+"는 차오름이 끝나는 시점(FlushPlannedPluses)에 한꺼번에 떠오른다.
    private void PlanBundlePluses(float _lowRatio, float _highRatio)
    {
        int _target = Mathf.RoundToInt(Mathf.Lerp(plusCountRange.x, plusCountRange.y, GetIntensity(bundleGainRatio)));
        int _add = Mathf.Max(_target - bundlePlannedPlus, PlusMinPerBurst);

        float _width = GetBarWidth();
        float _lo = _width * Mathf.Clamp01(_lowRatio);
        float _hi = Mathf.Max(_lo, _width * Mathf.Clamp01(_highRatio) - 2f);

        for (int i = 0; i < _add && plannedPlusCount < MaxPlannedPlus; i++)
        {
            plannedPlusX[plannedPlusCount] = _lo + (_hi - _lo) * (i + 0.25f + 0.5f * Random.value) / _add;
            plannedPlusCount++;
            bundlePlannedPlus++;
        }
    }

    // 차오르는 앞쪽이 지나간 위치의 "+"를 켜고 목록에서 뺀다.
    private void ReleasePlannedPluses(float _frontPx)
    {
        int _write = 0;

        for (int i = 0; i < plannedPlusCount; i++)
        {
            if (plannedPlusX[i] <= _frontPx)
                SpawnPlusAtTip();
            else
                plannedPlusX[_write++] = plannedPlusX[i];
        }

        plannedPlusCount = _write;
    }

    private void FlushPlannedPluses()
    {
        for (int i = 0; i < plannedPlusCount; i++)
            SpawnPlusAtTip();

        plannedPlusCount = 0;
    }

    // 보이는 끝(채움 앞쪽 + 젤리 머리 길이)의 캔버스 픽셀 위치. 머리를 그리지 않으면 채움 앞쪽이다.
    private float GetFrontTipPx()
    {
        float _front = GetBarWidth() * Mathf.Clamp01(shownRatio);
        float _head = true == bEdgeDrawn && 0f < frontEdgeStrength ? edgeSpringLen * edgeAppear : 0f;

        return Mathf.Min(barW, _front + _head);
    }

    // 보이는 끝 근처에서 "+" 하나를 켠다. 칸을 돌려 가며(칸마다 PlusTipSlotWidth) 놓아 한꺼번에 켜져도 서로 붙지 않는다.
    private void SpawnPlusAtTip()
    {
        float _x = GetFrontTipPx() - 2f - plusTipSlot * PlusTipSlotWidth - Random.value * PlusTipSlotWidth;
        plusTipSlot = (plusTipSlot + 1) % PlusTipSlots;

        SpawnPlusAt(Mathf.Max(2f, _x));
    }

    // 구간 [_fromPx, _toPx] 안에 "+" _count개를 한꺼번에 켠다.
    private void SpawnPlusNow(int _count, float _fromPx, float _toPx)
    {
        if (_toPx < _fromPx)
            return;

        for (int i = 0; i < _count; i++)
            SpawnPlusAt(Random.Range(_fromPx, _toPx));
    }

    // 가로 위치 _xPx의 몸통 안쪽 행 하나에서 "+"를 켠다. 몸통 안에서 자리를 못 찾으면 켜지 않는다.
    private void SpawnPlusAt(float _xPx)
    {
        if (null == pluses || 0 == pluses.Length || false == bSilhouetteReady)
            return;

        int _slot = ClaimPlusSlot(0);
        if (0 > _slot)
            return;

        int _px = Mathf.RoundToInt(_xPx);
        int _py = 0;
        bool _bFound = false;

        for (int t = 0; t < PlacementTries; t++)
        {
            _py = Random.Range(0, barH);

            if (true == IsInsideBody(_px, _py))
            {
                _bFound = true;
                break;
            }
        }

        if (false == _bFound)
            return;

        PlusState _p = pluses[_slot];

        _p.bAlive = true;
        _p.age = 0f;
        _p.life = Random.Range(plusLifetime.x, plusLifetime.y);
        _p.x = _px;
        // y는 바 가운데가 0이라, 아래 기준 행에서 높이의 절반을 뺀다.
        _p.y = _py - barH * 0.5f;
        _p.vy = Random.Range(plusRiseSpeed.x, plusRiseSpeed.y);
        _p.swayPhase = Random.Range(0f, 6.2832f);

        pluses[_slot] = _p;
        ApplyPlusVisual(_slot);
        alivePlusCount = CountAlivePluses();
    }

    private int CountAlivePluses()
    {
        int _alive = 0;

        if (null != pluses)
        {
            for (int i = 0; i < pluses.Length; i++)
            {
                if (true == pluses[i].bAlive)
                    _alive++;
            }
        }

        return _alive;
    }

    // "+" 하나를 지금 나이에 맞춰 그린다. 알파는 쓰지 않고 크기 단계와 깜빡임으로만 사라진다.
    // 네 모서리가 모두 (몸통 영역 안) 또는 (바 위쪽 plusRiseLimit 이내)가 아니면 그리지 않는다.
    private void ApplyPlusVisual(int _index)
    {
        PlusState _p = pluses[_index];
        Image _image = sparkleImages[_index];
        RectTransform _rect = sparkleRects[_index];

        if (null == _image || null == _rect)
            return;

        float _t = Mathf.Clamp01(_p.age / Mathf.Max(0.01f, _p.life));

        // 끝 무렵에는 켜졌다 꺼졌다 하며 사라진다.
        if (_t >= PlusBlinkStart && 0 != (Mathf.FloorToInt(_p.age / PixelStep) & 1))
        {
            _image.enabled = false;
            return;
        }

        bool _bShape = _t < PlusShapeEnd && null != sparklePlusSprite;
        int _size = true == _bShape ? 3 : 1;
        Sprite _sprite = true == _bShape ? sparklePlusSprite : null;

        // 피벗이 (0, 0)이라 위치가 곧 왼쪽 아래 모서리다. 입자 중심에서 크기의 절반(내림)을 빼 홀수 크기도 모서리가 정수 픽셀에 놓이게 한다.
        int _half = _size / 2;
        float _swayX = Mathf.Sin(_p.age * 8f + _p.swayPhase) * plusSway;
        int _left = Mathf.RoundToInt(_p.x + _swayX) - _half;
        int _bottomLocal = Mathf.RoundToInt(_p.y) - _half;
        // 윤곽 표는 바의 왼쪽 아래가 원점이므로 높이의 절반을 더해 맞춘다.
        int _bottom = _bottomLocal + Mathf.RoundToInt(barH * 0.5f);

        if (false == IsPlusInside(_left, _bottom, _size))
        {
            _image.enabled = false;
            return;
        }

        _image.sprite = _sprite;
        _rect.sizeDelta = new Vector2(_size, _size);
        _rect.anchoredPosition = new Vector2(_left, _bottomLocal);
        // 프리즘 위에서는 파스텔 면보다 또렷하도록 흰색으로 그린다.
        _image.color = PrismPhase.Off != prismPhase ? Color.white : Color.Lerp(barColorNow, Color.white, plusLighten);
        _image.enabled = true;
    }

    // 실제로 그려지는 칸이 모두 허용 영역 안인지. 3x3 "+"는 가운데와 팔 네 칸(모서리 칸은 투명), 1px 점은 그 한 칸만 본다.
    // 허용 영역: 몸통 영역, 또는 몸통 위쪽 끝 위로 plusRiseLimit 이내이면서 바 가로 폭 안(채워진 구간 안).
    private bool IsPlusInside(int _left, int _bottom, int _size)
    {
        int _right = _left + _size - 1;
        int _targetPx = Mathf.Min(barW, Mathf.FloorToInt(GetBarWidth() * Mathf.Clamp01(Mathf.Max(shownRatio, displayRatio))));

        // 젤리 머리가 채움 앞쪽 바깥으로 뻗은 만큼은 머리 위로 본다(머리가 눌려도 갑자기 사라지지 않게 최대 길이까지).
        if (0f < frontEdgeStrength)
            _targetPx = Mathf.Min(barW, _targetPx + Mathf.CeilToInt((EdgeRestLen + EdgeGainExtraMax) * Mathf.Clamp01(frontEdgeStrength)));

        if (0 > _left || _right >= _targetPx)
            return false;

        if (3 > _size)
            return IsPlusCellOk(_left, _bottom);

        return true == IsPlusCellOk(_left + 1, _bottom)
            && true == IsPlusCellOk(_left, _bottom + 1)
            && true == IsPlusCellOk(_left + 1, _bottom + 1)
            && true == IsPlusCellOk(_left + 2, _bottom + 1)
            && true == IsPlusCellOk(_left + 1, _bottom + 2);
    }

    private bool IsPlusCellOk(int _x, int _y)
    {
        if (true == IsInsideBody(_x, _y))
            return true;

        return _y > bodyTopRow && _y <= bodyTopRow + Mathf.RoundToInt(plusRiseLimit) && 0 <= _x && _x < barW;
    }

    // 한 칸(PixelStep)만큼 "+"를 움직이고 새 "+"를 켠다. 정수 픽셀로 반올림해 계단식으로 움직이는 픽셀 아트의 느낌을 지킨다.
    private void StepPluses()
    {
        if (null == pluses)
            return;

        EnsureSilhouette();

        // 바의 현재 색(경고 펄스 포함)에 동기화한다.
        RefreshBarColor();

        // 차오르는 동안은 앞쪽이 예약된 위치를 지날 때 "+"가 켜진다.
        if (true == bFillingUp && 0 < plannedPlusCount)
            ReleasePlannedPluses(GetBarWidth() * Mathf.Clamp01(shownRatio));

        for (int i = 0; i < pluses.Length; i++)
        {
            PlusState _p = pluses[i];
            if (false == _p.bAlive)
                continue;

            _p.age += PixelStep;

            if (_p.age >= _p.life)
            {
                _p.bAlive = false;
                pluses[i] = _p;

                if (null != sparkleImages[i])
                    sparkleImages[i].enabled = false;

                continue;
            }

            // 바 위쪽 한계에 닿으면 더 올라가지 않고 그 자리에서 깜빡이며 사라진다.
            float _maxY = bodyTopRow + plusRiseLimit - barH * 0.5f;
            _p.y = Mathf.Min(_maxY, _p.y + _p.vy * PixelStep);
            pluses[i] = _p;

            ApplyPlusVisual(i);
        }

        alivePlusCount = CountAlivePluses();
    }

    // 피격/회복 플래시, 흔들림

    private void StartFlash(Color _color, float _intensity, float _duration, int _count)
    {
        if (null == flashOverlay)
            return;

        KillTween(ref flashTween);

        flashColor = _color;
        flashPeakAlpha = Mathf.Lerp(flashMinAlpha, flashMaxAlpha, _intensity);

        if (null != flashEffect)
        {
            flashEffect.colorFilter = ColorFilter.Replace;
            flashEffect.colorIntensity = 1f;
            flashEffect.color = new Color(_color.r, _color.g, _color.b, 1f);
        }

        SetFlashValue(1f);

        // 같은 감소를 Restart 루프로 반복해 "번쩍, 번쩍"처럼 보이게 한다. 값이 매번 1로 되돌아가므로 경계가 또렷하다.
        flashTween = DOTween.To(getFlashValue, setFlashValue, 0f, _duration)
            .SetEase(Ease.OutQuad)
            .SetLoops(_count, LoopType.Restart)
            .SetUpdate(false);
    }

    private void StartShake(float _intensity)
    {
        if (null == shakeTarget)
            return;

        KillTween(ref shakeTween);

        currentShakeStrength = shakeStrength * Mathf.Lerp(0.5f, 1f, _intensity);
        SetShakeValue(1f);

        shakeTween = DOTween.To(getShakeValue, setShakeValue, 0f, shakeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(false);
    }

    private float GetFlashValue()
    {
        return flashValue;
    }

    private void SetFlashValue(float _value)
    {
        flashValue = _value;

        if (null == flashOverlay)
            return;

        // 색 치환(UIEffect)을 쓰면 이미지 색은 알파만 담당하고, 쓰지 않으면 이미지 색 자체를 칠한다.
        Color _color = null != flashEffect ? Color.white : flashColor;
        _color.a = flashPeakAlpha * _value;
        flashOverlay.color = _color;
    }

    private float GetShakeValue()
    {
        return shakeValue;
    }

    // 값이 줄어드는 동안 좌우를 번갈아 흔든다. 부드러운 보간 대신 정수 픽셀 단위의 계단식으로 움직여 픽셀 느낌을 지킨다.
    // 다른 움직임(HUD 이동 등)과 겹쳐도 틀어지지 않도록 절대 위치가 아니라 "이번에 더한 오프셋"만 갈아 끼운다.
    private void SetShakeValue(float _value)
    {
        shakeValue = _value;

        if (null == shakeTarget)
            return;

        Vector2 _offset = Vector2.zero;

        if (0f < _value)
        {
            int _step = Mathf.FloorToInt(_value * ShakeStepCount);
            float _dir = 0 == (_step & 1) ? 1f : -1f;
            _offset.x = Mathf.Round(_dir * currentShakeStrength * _value);
        }

        shakeTarget.anchoredPosition += _offset - appliedShakeOffset;
        appliedShakeOffset = _offset;
    }

    private static void KillTween(ref Tween _tween)
    {
        if (null != _tween)
        {
            _tween.Kill();
            _tween = null;
        }
    }

    // 진행 중인 연출을 모두 끊고 눈에 보이는 상태를 원래대로 돌려 놓는다.
    private void ResetEffects()
    {
        KillTween(ref flashTween);
        KillTween(ref shakeTween);

        bGhostTrailing = false;
        ghostHoldRemain = 0f;
        bFillingUp = false;
        bFillEvent = false;
        pendingLossRatio = 0f;
        pendingGainRatio = 0f;
        shownRatio = displayRatio;
        plannedPlusCount = 0;
        pixelStepTimer = 0f;
        EndBundle();

        if (null != ghostSlider)
            ghostSlider.value = displayRatio;

        if (null != pluses)
        {
            for (int i = 0; i < pluses.Length; i++)
            {
                pluses[i].bAlive = false;

                if (null != sparkleImages[i])
                    sparkleImages[i].enabled = false;
            }
        }

        alivePlusCount = 0;

        bResting = false;
        restPlusTimer = 0f;

        ClearFrontEdge();

        // 맵 전환 등으로 숨겨질 때는 풀림 연출 없이 프리즘도 즉시 걷는다.
        StopPrism();

        SetFlashValue(0f);
        SetShakeValue(0f);
    }

    // 유니티 이벤트 함수

    // "+"는 트윈 없이 정해진 간격(20fps)으로 한 칸씩만 움직인다.
    // 할 일이 없으면 곧바로 돌아간다.
    // 이 클래스의 연출(프리즘, "+", 차오름, 잔상, 플래시/흔들림 트윈)은 모두 scaled time을 쓴다.
    // ESC 일시정지(Time.timeScale = 0) 동안에는 그 자리에서 멈췄다가 풀리면 이어서 진행한다.
    // 저피로도 경고(DangerShake/DangerColor 모션)도 scaled time이라 함께 멈춘다.
    private void Update()
    {
        if (0 >= alivePlusCount && false == bFillingUp && false == bResting && 0 >= plannedPlusCount
            && PrismPhase.Off == prismPhase && false == bPrismEngagePending && 0f >= bundleGainRatio
            && false == bEdgeActive)
        {
            pixelStepTimer = 0f;
            return;
        }

        float _dt = Time.deltaTime;

        // 회복 묶음: 차오름이 끝난 뒤 gainBundleWindow 동안 새 회복이 없으면 묶음을 끝낸다.
        if (0f < bundleGainRatio && false == bFillingUp)
        {
            bundleLingerRemain -= _dt;

            if (0f >= bundleLingerRemain)
                EndBundle();
        }

        UpdatePrism(_dt);
        UpdateFrontEdge(_dt);

        // 휴식 구역 안에서는 일정 간격마다 채워진 구간에서 "+" 하나가 조용히 떠오른다.
        if (true == bResting && false == bFillingUp)
        {
            restPlusTimer += _dt;

            float _interval = restPlusInterval / Mathf.Max(RestPlusMinFillRatio, Mathf.Clamp01(shownRatio));

            if (restPlusTimer >= _interval)
            {
                restPlusTimer = 0f;

                float _filledPx = GetBarWidth() * Mathf.Clamp01(shownRatio);
                if (8f < _filledPx)
                    SpawnPlusAt(Random.Range(4f, _filledPx - 4f));
            }
        }

        pixelStepTimer += _dt;

        int _steps = 0;
        while (PixelStep <= pixelStepTimer && MaxStepsPerFrame > _steps)
        {
            pixelStepTimer -= PixelStep;
            _steps++;

            StepPluses();
        }

        // 프레임이 크게 끊겼다면 밀린 시간은 버린다.
        if (pixelStepTimer > PixelStep)
            pixelStepTimer = 0f;
    }

    private void OnDestroy()
    {
        KillTween(ref flashTween);
        KillTween(ref shakeTween);

        if (null != prismTexture)
        {
            Destroy(prismTexture);
            prismTexture = null;
        }
    }
}
