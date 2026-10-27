using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RepairBox : MonoBehaviour
{
    public event Action<bool> RepairBoxInteractStateChangedEvent;

    public int repairBoxCount = 0;
    public float repairAmount = 0.25f;
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

    public void SetRepairBoxCount(float _amount)
    {
        repairBoxCount = (int)_amount;
    }

    public void SetRepairAmount(float _amount)
    {
        repairAmount = _amount;
    }

    private void UpdateInteractState()
    {
        bool currentState = bCanReach && bPhysicalOverlapped && repairBoxCount > 0;
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

        if (repairBoxCount <= 0) return;

        if (character != null)
        {
            character.RepairWeapon(repairAmount);
            repairBoxCount--;
            UpdateInteractState();
            PlayRepairFeedback();
        }
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
