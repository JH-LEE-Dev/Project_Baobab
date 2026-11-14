using System;
using System.Collections;
using PresentationLayer.VFX;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RepairBox : MonoBehaviour
{
    public event Action<bool> RepairBoxInteractStateChangedEvent;

    public CircleCollider2D col;

    private bool bCanReach = false;
    public bool bCanInteract { get; private set; }

    private int characterLayer;
    private bool bPhysicalOverlapped = false;
    private bool bLastInteractState = false;

    // RepairBox가 Active일 때만 길찾기 상 이동 불가 타일로 등록되는 발밑 ColliderTilemap
    private TilemapFootprintCollider footprintCollider;

    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CustomSortable customSortable;
    [SerializeField] private GameObject outlineStencilObj;
    [SerializeField] private GameObject outlineObj;

    [Header("Repair Feedback")]
    [Tooltip("수리했을 때 모루가 하얗게 번쩍이는 시간")]
    [SerializeField] private float repairFlashDuration = 0.25f;
    [SerializeField] private AnimationCurve repairFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [Tooltip("뽀잉(눌렸다 튀어오르는 스프링) 전체 시간")]
    [SerializeField] private float repairBounceDuration = 0.45f;
    [Tooltip("뽀잉 세기. 처음엔 옆으로 퍼지며 눌리고, 이어서 위로 늘어난다.")]
    [SerializeField] private float repairBounceAmplitude = 0.18f;
    [SerializeField] private float repairBounceFrequency = 22f;
    [SerializeField] private float repairBounceDamping = 6f;
    [Tooltip("뽀잉 기준점(그림 중심 기준 로컬 Y). 모루 받침 바닥에 두어야 땅에 붙은 채로 눌린다.")]
    [SerializeField] private float repairBounceAnchorY = -11f / 32f;

    [Header("Repair VFX (이펙트 담당이 채우는 자리)")]
    [Tooltip("수리 스파크를 재생할 VFXComponent. repairSparkTag로 등록된 이펙트가 없으면 아무것도 재생하지 않는다.")]
    [SerializeField] private VFXComponent repairVfx;
    [SerializeField] private string repairSparkTag = "RepairSpark";
    [Tooltip("스파크가 튀는 위치. 비어 있으면 모루 그림 위치를 쓴다.")]
    [SerializeField] private Transform repairSparkPoint;

    [Header("Count HUD (사용 안 함)")]
    [Tooltip("예전 사용 횟수 HUD. 모루에 사용 횟수가 없어져 띄우지 않는다(연결만 남겨 둔다).")]
    [SerializeField] private HUD_RepairKitCount countHud;

    [Header("Axe Deposit")]
    [Tooltip("도끼를 맡긴 동안 모루를 두드리는(번쩍임·뽀잉·스파크) 주기(초)")]
    [SerializeField] private float hammerInterval = 0.5f;
    [Tooltip("도끼를 맡긴 동안 1초에 회복하는 도끼 내구도(수치). 최대 내구도까지만 찬다. 수리 효율 특성 보너스는 여기에 더해진다.")]
    [SerializeField] private float durabilityPerSecond = 5f;

    [Header("Durability HUD (모루 위 원판)")]
    [Tooltip("화면 도끼 HUD의 원판 게이지 바탕(Gauge.png)")]
    [SerializeField] private Sprite gaugeBackgroundSprite;
    [Tooltip("원판 게이지 테두리(Gauge_Outline.png)")]
    [SerializeField] private Sprite gaugeOutlineSprite;
    [Tooltip("원판 게이지 채움(Gauge_Inside.png). 위에서부터 시계 방향으로 내구도만큼 칠한다.")]
    [SerializeField] private Sprite gaugeFillSprite;
    [SerializeField] private Shader gaugeShader;
    [Tooltip("원판 바닥 가운데 위치(모루 로컬)")]
    [SerializeField] private Vector2 gaugeLocalAnchor = new Vector2(0f, 0.875f);

    [Header("Axe Throw (임시 리소스)")]
    [Tooltip("맡길 때 손 → 모루, 돌려받을 때 모루 → 손으로 날아가는 도끼 그림(임시: 도끼 팔 그림에서 손을 뺀 것). 비어 있으면 날리지 않고 바로 맡기고/돌려받는다.")]
    [SerializeField] private Sprite throwAxeSprite;
    [Tooltip("날아가는 도끼 재질. 손에 든 도끼와 같은 재질(Custom-Sprite-Default-NoSnap)을 넣는다. 비어 있으면 기본 스프라이트 재질.")]
    [SerializeField] private Material throwAxeMaterial;
    [Tooltip("손 ↔ 모루를 날아가는 시간(초). 맡길 때·돌려받을 때 같다.")]
    [SerializeField] private float throwDuration = 0.3f;
    [Tooltip("포물선 꼭대기 높이(유닛)")]
    [SerializeField] private float throwArcHeight = 1f;
    [Tooltip("날아가는 동안 도는 각도(도). 오른쪽으로 날아갈 때 시계 방향, 왼쪽이면 반대로 돈다.")]
    [SerializeField] private float throwSpinDegrees = 540f;
    [Tooltip("도끼가 떨어지는(돌려받을 때 떠나는) 지점(모루 로컬). 기본값은 수리 스파크가 튀는 모루 윗면")]
    [SerializeField] private Vector2 throwLocalTarget = new Vector2(-5f / 32f, 21f / 32f);

    // 수리 효율 특성 보너스(최대 내구도 대비 초당 비율, 0.01 = 1%/s). OffroadVehicleObj.ResetRepairBox가 넣는다.
    private float repairRateBonus = 0f;

    // 도끼를 맡아 두고 있는지. 맡은 동안 다시 상호작용하면 돌려준다.
    private bool bHoldingAxe = false;
    private float hammerTimer = 0f;
    private AnvilRepairGauge repairGauge;

    // 도끼가 날아가는 중인지(맡길 때 손 → 모루, 돌려받을 때 모루 → 손). 날아가는 동안은 두드리지도, 내구도를 채우지도 않는다.
    private bool bAxeInFlight = false;
    // 모루 → 손으로 돌아오는 중인지. 손에 닿는 순간 캐릭터가 도끼를 돌려받는다(그 전까지는 맡긴 상태 그대로).
    private bool bAxeReturning = false;
    private float throwElapsed = 0f;
    private Vector3 throwStartPos;
    private float throwDirection = 1f;
    private SpriteRenderer throwAxeRenderer;
    private const float ThrowPixelsPerUnit = 32f;

    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock flashMPB;
    private Coroutine repairFeedbackCoroutine;
    private Vector3 visualBaseLocalPos;
    private Vector3 visualBaseLocalScale;

    private SpriteRenderer outlineStencilSR;
    private SpriteRenderer outlineSR;
    private Sprite currentSprite;

    private InputManager inputManager;
    private Character character;

    private void Awake()
    {
        characterLayer = LayerMask.NameToLayer("Character");

        footprintCollider = new TilemapFootprintCollider(GetComponentInChildren<Tilemap>(true));

        if (customSortable != null && spriteRenderer != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(spriteRenderer);
        }

        if (outlineStencilObj != null) outlineStencilSR = outlineStencilObj.GetComponent<SpriteRenderer>();
        if (outlineObj != null) outlineSR = outlineObj.GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            visualBaseLocalPos = spriteRenderer.transform.localPosition;
            visualBaseLocalScale = spriteRenderer.transform.localScale;
        }

        if (repairVfx != null) repairVfx.Initialize();

        repairGauge = AnvilRepairGauge.Create(transform, gaugeBackgroundSprite, gaugeOutlineSprite, gaugeFillSprite, gaugeShader);
    }

    private void Update()
    {
        if (false == bHoldingAxe)
        {
            // 맡기기 가능 여부는 도끼 내구도에 따라 바뀌므로(가득 차면 맡길 수 없다), 모루 앞에 있는 동안은 매 프레임 다시 판정한다.
            if (bCanReach && bPhysicalOverlapped) UpdateInteractState();
            return;
        }

        // 귀환 확정·사망·리셋 등으로 캐릭터가 도끼를 이미 돌려받았다면 맡은 상태만 정리한다.
        if (character == null || false == character.bAxeDeposited)
        {
            EndHoldingAxe();
            return;
        }

        if (bAxeInFlight)
        {
            UpdateAxeFlight(Time.deltaTime);
            return;
        }

        if (false == character.IsAxeDurabilityFull)
        {
            float dt = Time.deltaTime;
            float perSecond = durabilityPerSecond + character.AxeMaxDurability * repairRateBonus;
            character.RestoreAxeDurabilityGradually(perSecond * dt);

            if (character.IsAxeDurabilityFull)
            {
                // 다 찬 순간 마지막으로 한 번 두드리고 HUD 도끼에도 수리 연출을 낸다. 이후로는 두드리지 않는다.
                PlayHammerHit(true);
            }
            else
            {
                hammerTimer -= dt;
                if (hammerTimer <= 0f)
                {
                    PlayHammerHit(false);
                    hammerTimer = Mathf.Max(hammerTimer + hammerInterval, 0.01f);
                }
            }
        }

        if (repairGauge != null) repairGauge.SetState(GetGaugeAnchor(), character.AxeDurabilityRatio);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
        {
            customSortable.SetHeight(0f);
            customSortable.ManualLateUpdate();
        }

        if (spriteRenderer != null)
        {
            currentSprite = spriteRenderer.sprite;
            if (outlineStencilSR != null) outlineStencilSR.sprite = currentSprite;
            if (outlineSR != null)
            {
                outlineSR.sprite = currentSprite;
                outlineSR.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }
    }

    public void SetOutlineMaterial()
    {
        if (outlineStencilObj != null) outlineStencilObj.SetActive(true);
    }

    public void ResetMaterial()
    {
        if (outlineStencilObj != null) outlineStencilObj.SetActive(false);
    }

    public void SetCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    public void SetRepairRateBonus(float _bonus)
    {
        repairRateBonus = _bonus;
    }

    private void UpdateInteractState()
    {
        // 모루는 사용 횟수 없이 몇 번이든 쓸 수 있다(나타나는지는 수리 특성 해금이 정한다 - OffroadVehicleObj.ResetRepairBox).
        // 도끼 내구도가 가득 차 있으면 맡길 수 없다(맡겨 둔 도끼를 돌려받는 것은 언제든 가능, 이미 돌아오는 중이면 제외).
        bool currentState = bCanReach && bPhysicalOverlapped && (bHoldingAxe ? false == bAxeReturning : CanDepositAxe());
        if (currentState != bLastInteractState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;

            RepairBoxInteractStateChangedEvent?.Invoke(currentState);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            bPhysicalOverlapped = true;
            UpdateInteractState();
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            if (bPhysicalOverlapped == false)
            {
                bPhysicalOverlapped = true;
                UpdateInteractState();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            bPhysicalOverlapped = false;
            UpdateInteractState();
        }
    }

    public void Initialize(InputManager _inputManager, Transform _characterTransform, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        footprintCollider.SetEnvironmentProvider(_environmentProvider);
        if (_characterTransform != null && _characterTransform.parent != null)
        {
            character = _characterTransform.parent.GetComponent<Character>();
        }

        BindEvents();

        // OnEnable이 environmentProvider가 세팅되기 전(활성 상태로 인스턴스화된 시점)에 먼저 실행됐을 수 있으므로
        // 여기서 다시 한번 등록을 시도한다. Register()는 매번 먼저 Clear()하고 다시 채우므로 중복 호출해도 안전하다.
        if (gameObject.activeInHierarchy)
        {
            footprintCollider.Register();
        }
    }

    private void OnEnable()
    {
        footprintCollider?.Register();

        // 던전 재입장 시 타일맵이 이후 프레임에 걸쳐 다시 초기화/갱신되면서 방금 한 등록이
        // 뒤늦게 걷어차이는 경우를 대비해, 몇 프레임 뒤 한 번 더 재등록해서 안전하게 확정한다.
        StopCoroutine(nameof(ReregisterFootprintDelayed));
        StartCoroutine(nameof(ReregisterFootprintDelayed));
    }

    private IEnumerator ReregisterFootprintDelayed()
    {
        yield return null;
        yield return null;
        footprintCollider?.Register();

        yield return new WaitForSeconds(0.2f);
        footprintCollider?.Register();
    }

    private void OnDisable()
    {
        footprintCollider?.Clear();

        // 맡은 도끼를 든 채 꺼지면 캐릭터에게 돌려준다(귀환 확정 경로는 캐릭터가 이미 돌려받아 아무 일도 없다).
        if (bHoldingAxe)
        {
            if (character != null) character.RetrieveAxe();
            EndHoldingAxe();
        }

        // 연출 도중 꺼지면(원정 종료 등) 다음에 켜질 때 찌그러진 채로 남지 않도록 원래 모습으로 돌린다.
        if (repairFeedbackCoroutine != null)
        {
            StopCoroutine(repairFeedbackCoroutine);
            repairFeedbackCoroutine = null;
        }
        ResetRepairFeedback();
    }

    public void BindEvents()
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
            inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
        }
    }

    public void ReleaseEvents()
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        }
    }

    private void InteractionKeyPressed()
    {
        if (!gameObject.activeInHierarchy) return;

        if (!bCanInteract) return;

        if (character == null) return;

        // 맡아 둔 도끼가 있으면 손으로 날려 보낸다(손에 닿으면 돌려받는다).
        if (bHoldingAxe)
        {
            if (false == bAxeReturning) StartAxeReturn();
            UpdateInteractState();
            return;
        }

        if (false == CanDepositAxe()) return;

        // 손을 떠나는 위치는 도끼 그림이 꺼지기 전에 잡아 둔다.
        Vector3 handPos = character.AxeWorldPosition;

        // 도끼를 맡는다(사용 횟수 제한·소모 없음). 캐릭터는 마을 모습이 되어 공격할 수 없고, 모루는 돌려줄 때까지 두드리며 내구도를 채운다.
        if (false == character.DepositAxe()) return;

        bHoldingAxe = true;

        if (false == StartAxeFlight(handPos, false))
        {
            BeginHammering();
        }

        UpdateInteractState();
    }

    // 도끼가 모루에 닿았을 때(던지기 연출이 없으면 맡긴 즉시): 원판을 띄우고 첫 번째로 두드린다.
    private void BeginHammering()
    {
        if (repairGauge != null) repairGauge.Show(GetGaugeAnchor(), character.AxeDurabilityRatio);

        hammerTimer = hammerInterval;
        PlayHammerHit(false);
    }

    // 돌려받기 시작: 두드리기를 멈추고 원판을 내린 뒤 도끼를 모루(던지는 중이었다면 그 자리)에서 손으로 날린다.
    private void StartAxeReturn()
    {
        Vector3 from = bAxeInFlight && throwAxeRenderer != null ? throwAxeRenderer.transform.position : GetAnvilAxePoint();
        StopAxeFlight();
        if (repairGauge != null) repairGauge.Hide();

        // 마지막 두드림 이후 조금 더 찬 내구도를 HUD에 반영한다.
        character.NotifyAxeRepairTick(false);

        if (false == StartAxeFlight(from, true))
        {
            character.RetrieveAxe();
            EndHoldingAxe();
        }
    }

    // ===== 손 ↔ 모루 도끼 날리기(임시 리소스) =====
    private bool StartAxeFlight(Vector3 _startPos, bool _bReturning)
    {
        if (throwAxeSprite == null || throwDuration <= 0f) return false;

        if (throwAxeRenderer == null)
        {
            GameObject go = new GameObject("ThrownAxe");
            go.transform.SetParent(transform, false);
            throwAxeRenderer = go.AddComponent<SpriteRenderer>();
            if (throwAxeMaterial != null) throwAxeRenderer.sharedMaterial = throwAxeMaterial;
            throwAxeRenderer.sortingLayerName = "FlyingItem";
            throwAxeRenderer.sortingOrder = 4;
        }

        throwAxeRenderer.sprite = throwAxeSprite;
        bAxeReturning = _bReturning;
        throwStartPos = _startPos;
        throwDirection = GetFlightTarget().x >= _startPos.x ? 1f : -1f;
        throwAxeRenderer.flipX = throwDirection < 0f;
        throwElapsed = 0f;
        bAxeInFlight = true;

        throwAxeRenderer.gameObject.SetActive(true);
        PlaceFlyingAxe(0f);
        return true;
    }

    private void UpdateAxeFlight(float _dt)
    {
        throwElapsed += _dt;
        float t = Mathf.Clamp01(throwElapsed / throwDuration);
        PlaceFlyingAxe(t);

        if (t < 1f) return;

        bool bWasReturning = bAxeReturning;
        StopAxeFlight();

        if (bWasReturning)
        {
            // 손에 닿았다: 도끼를 돌려받는다(모습·조작·HUD 도끼가 돌아온다).
            character.RetrieveAxe();
            EndHoldingAxe();
        }
        else
        {
            BeginHammering();
        }
    }

    // 출발점 → 도착점을 잇는 포물선 위에 놓고 날아가는 방향으로 돌린다. 픽셀 격자에 맞춘다.
    // 돌아올 때는 움직이는 손을 매 프레임 따라가고, 손에 닿을 때 손에 든 모습(회전 0)이 되도록 돈다.
    private void PlaceFlyingAxe(float _t)
    {
        Vector3 target = GetFlightTarget();
        Vector3 pos = Vector3.Lerp(throwStartPos, target, _t);
        pos.y += throwArcHeight * 4f * _t * (1f - _t);
        pos.x = Mathf.Round(pos.x * ThrowPixelsPerUnit) / ThrowPixelsPerUnit;
        pos.y = Mathf.Round(pos.y * ThrowPixelsPerUnit) / ThrowPixelsPerUnit;
        pos.z = 0f;

        float spinT = bAxeReturning ? _t - 1f : _t;
        Transform axe = throwAxeRenderer.transform;
        axe.position = pos;
        axe.rotation = Quaternion.Euler(0f, 0f, -throwDirection * throwSpinDegrees * spinT);
    }

    private void StopAxeFlight()
    {
        bAxeInFlight = false;
        bAxeReturning = false;
        if (throwAxeRenderer != null) throwAxeRenderer.gameObject.SetActive(false);
    }

    private Vector3 GetFlightTarget()
    {
        return bAxeReturning && character != null ? character.AxeWorldPosition : GetAnvilAxePoint();
    }

    private Vector3 GetAnvilAxePoint()
    {
        return transform.TransformPoint(throwLocalTarget);
    }

    // 모루를 한 번 두드린다: 기존 수리 연출(번쩍임·뽀잉·스파크) + 게이지 번쩍임 + HUD 도끼 내구도 갱신.
    private void PlayHammerHit(bool _bPlayHudRepairEffect)
    {
        PlayRepairFeedback();
        if (repairGauge != null) repairGauge.PlayHit();
        if (character != null) character.NotifyAxeRepairTick(_bPlayHudRepairEffect);
    }

    private void EndHoldingAxe()
    {
        if (false == bHoldingAxe) return;

        bHoldingAxe = false;
        StopAxeFlight();
        if (repairGauge != null) repairGauge.Hide();

        // 마지막 두드림 이후 조금 더 찬 내구도를 HUD에 반영한다.
        if (character != null) character.NotifyAxeRepairTick(false);

        UpdateInteractState();
    }

    private bool CanDepositAxe()
    {
        return character != null && false == character.IsAxeDurabilityFull;
    }

    private Vector3 GetGaugeAnchor()
    {
        return transform.TransformPoint(gaugeLocalAnchor);
    }

    // 수리 성공 연출: 하얀 번쩍임 + 뽀잉 + 스파크(이펙트가 등록돼 있을 때만)
    private void PlayRepairFeedback()
    {
        if (repairVfx != null)
        {
            Vector3 sparkPos = repairSparkPoint != null ? repairSparkPoint.position
                : (spriteRenderer != null ? spriteRenderer.transform.position : transform.position);
            ParticleSystem sparkEffect = repairVfx.Play(repairSparkTag, sparkPos, Quaternion.identity);

            // 모루 그림 위에 그려지도록 정렬을 맞춘다(그림의 정렬 순서는 CustomSortable이 위치에 따라 정한다).
            if (null != sparkEffect && null != spriteRenderer)
            {
                repairVfx.SetSortingSettings(sparkEffect, spriteRenderer.sortingLayerID, spriteRenderer.sortingOrder + 2);
            }
        }

        if (spriteRenderer == null || !gameObject.activeInHierarchy) return;

        if (repairFeedbackCoroutine != null) StopCoroutine(repairFeedbackCoroutine);
        repairFeedbackCoroutine = StartCoroutine(RepairFeedbackRoutine());
    }

    private IEnumerator RepairFeedbackRoutine()
    {
        if (flashMPB == null) flashMPB = new MaterialPropertyBlock();
        Transform visual = spriteRenderer.transform;

        float total = Mathf.Max(repairFlashDuration, repairBounceDuration);
        float elapsed = 0f;
        while (elapsed < total)
        {
            float flash = repairFlashDuration > 0f && elapsed < repairFlashDuration
                ? repairFlashCurve.Evaluate(elapsed / repairFlashDuration) : 0f;
            spriteRenderer.GetPropertyBlock(flashMPB);
            flashMPB.SetFloat(FlashAmountID, flash);
            spriteRenderer.SetPropertyBlock(flashMPB);

            // 차량 착지 연출과 같은 감쇠 스프링. 받침 바닥(repairBounceAnchorY)은 그 자리에 두고 크기만 출렁인다.
            float t = repairBounceDuration > 0f ? Mathf.Clamp01(elapsed / repairBounceDuration) : 1f;
            float spring = t < 1f ? Mathf.Exp(-repairBounceDamping * t) * Mathf.Sin(repairBounceFrequency * t) : 0f;
            float sx = 1f + spring * repairBounceAmplitude;
            float sy = 1f - spring * repairBounceAmplitude;
            visual.localScale = new Vector3(visualBaseLocalScale.x * sx, visualBaseLocalScale.y * sy, visualBaseLocalScale.z);
            visual.localPosition = visualBaseLocalPos + new Vector3(0f, repairBounceAnchorY * visualBaseLocalScale.y * (1f - sy), 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetRepairFeedback();
        repairFeedbackCoroutine = null;
    }

    private void ResetRepairFeedback()
    {
        if (spriteRenderer == null) return;

        Transform visual = spriteRenderer.transform;
        visual.localScale = visualBaseLocalScale;
        visual.localPosition = visualBaseLocalPos;

        if (flashMPB == null) return;
        spriteRenderer.GetPropertyBlock(flashMPB);
        flashMPB.SetFloat(FlashAmountID, 0f);
        spriteRenderer.SetPropertyBlock(flashMPB);
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }
}
