using System;
using System.Collections;
using UnityEngine;

public class LootItem : Item
{
    // 이벤트
    public event Action<LootItem> lootItemAcquiredEvent;
    private LootType lootType;
    public LootType LootType => lootType;

    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    // 상태 변수
    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;
    private bool bDrop = true;

    // 이동 관련 변수 (캐싱)
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;
    private float suckSpeed;
    private const float SuckAccel = 12f;
    private const float MinAcquireDist = 0.2f;

    // 관리용 인덱스
    public int UpdateIndex { get; set; } = -1;

    [SerializeField] private GameObject outlineObj;
    [SerializeField] private SpriteRenderer outlineStencilSR;
    [SerializeField] private SpriteRenderer outlineSR;
    [SerializeField] private Color outlineColor = Color.white;
    private static readonly int OutlineColorPropertyID = Shader.PropertyToID("_OutlineColor");
    private MaterialPropertyBlock mpb;

    [Header("VFX 연출")]
    [SerializeField] private GameObject vfxRoot;
    [SerializeField] private ItemAuraOrbitController vfxOrbit;
    [SerializeField] private ItemAuraEffectController vfxBeam;
    private Coroutine vfxRelayCoroutine;

    [Header("드랍 포물선 연출")]
    [Tooltip("포물선 낙하 구간(좌우 이동 + 높이)의 실제 소요 시간을 이 배율만큼 늘립니다. 1이면 원래 속도, 클수록 낙하 자체가 느려지는 진짜 슬로우모션이 됩니다. Sucking(흡입) 구간에는 영향 없습니다.")]
    [SerializeField, Range(1f, 5f)] private float arcSlowMoTimeScale = 1.5f;

    public void Initialize(LootItemTypeData _lootItemTypeData)
    {
        base.Initialize(_lootItemTypeData.itemType);

        lootType = _lootItemTypeData.lootType;
        state = ItemMoveState.None;
        suckTarget = null;
        sprite = _lootItemTypeData.sprite;
        elapsed = 0;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            visualTransform = spriteRenderer.transform;
        }

        if (outlineStencilSR != null)
            outlineStencilSR.sprite = sprite;
        if (outlineSR != null)
            outlineSR.sprite = sprite;

        // 스텐실 라이터가 아웃라인보다 한 순서 먼저 그려져야 스텐실 마스크 기법이 정상 동작한다(LogItem과 동일).
        if (outlineStencilSR != null && outlineSR != null)
            outlineStencilSR.sortingOrder = outlineSR.sortingOrder - 1;

        ApplyOutlineColor();
    }

    private void ApplyOutlineColor()
    {
        if (outlineSR == null) return;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        outlineSR.GetPropertyBlock(mpb);
        mpb.SetColor(OutlineColorPropertyID, outlineColor);
        outlineSR.SetPropertyBlock(mpb);
    }

    public void IsDropItem(bool _boolean)
    {
        bDrop = _boolean;
    }

    /// <summary>
    /// 범위 판정(OnTriggerEnter2D) 없이, Dropped 상태가 되는 즉시 이 타겟으로 흡입되도록 미리 지정한다.
    /// </summary>
    public override void SetSuckTarget(Transform _target)
    {
        suckTarget = _target;
    }

    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        elapsed = 0f;
        state = ItemMoveState.Launching;

        if (outlineObj != null)
        {
            outlineObj.SetActive(true);
            if (visualTransform != null)
            {
                outlineObj.transform.localPosition = visualTransform.localPosition;
                outlineObj.transform.localRotation = visualTransform.localRotation;
                outlineObj.transform.localScale = visualTransform.localScale;
            }
        }

        PlayVFX();
    }

    public override void ResetItem()
    {
        base.ResetItem();
        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0;

        if (vfxRelayCoroutine != null)
        {
            StopCoroutine(vfxRelayCoroutine);
            vfxRelayCoroutine = null;
        }
        if (vfxRoot != null)
            vfxRoot.SetActive(false);

        if (outlineObj != null)
            outlineObj.SetActive(false);
        if (outlineSR != null)
            outlineSR.SetPropertyBlock(null);
    }

    private void PlayVFX()
    {
        if (vfxRoot == null) return;

        if (vfxRelayCoroutine != null)
            StopCoroutine(vfxRelayCoroutine);

        vfxRelayCoroutine = StartCoroutine(VFXRelayRoutine());
    }

    /// <summary>
    /// 단발성 빔(VFX_Beam)이 터진 뒤, 빔의 수명(BurstDuration) 절반 지점에서
    /// 상시 궤도 오오라(VFX_Orbit)로 자연스럽게 릴레이 전환합니다.
    /// </summary>
    private IEnumerator VFXRelayRoutine()
    {
        vfxRoot.SetActive(true);
        SyncVFXPosition();
        SyncVFXSortingOrder();

        if (vfxOrbit != null)
            vfxOrbit.gameObject.SetActive(false);

        float delay = 0f;
        if (vfxBeam != null)
        {
            vfxBeam.gameObject.SetActive(true);
            vfxBeam.Play();
            delay = vfxBeam.BurstDuration * 0.5f;
        }

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (vfxOrbit != null)
            vfxOrbit.gameObject.SetActive(true);

        vfxRelayCoroutine = null;
    }

    private void SyncVFXPosition()
    {
        if (vfxRoot == null || visualTransform == null) return;

        vfxRoot.transform.localPosition = visualTransform.localPosition;
    }

    /// <summary>
    /// VFX_Beam은 본체 스프라이트보다 항상 1 앞(+1)으로 유지합니다.
    /// VFX_Orbit은 위성 기준 오더를 본체+1로 재기준(rebase)하되, 인스펙터에 세팅된
    /// 트레일/중앙 글로우의 상대 깊이 오프셋(-10/+10 등)은 그대로 보존됩니다.
    /// </summary>
    private void SyncVFXSortingOrder()
    {
        if (spriteRenderer == null) return;

        int order = spriteRenderer.sortingOrder + 1;

        if (vfxBeam != null)
            vfxBeam.SetSortingOrder(order);
        if (vfxOrbit != null)
            vfxOrbit.RebaseSortingOrder(order);
    }

    public void ManualUpdate(float _deltaTime)
    {
        switch (state)
        {
            case ItemMoveState.Launching:
                UpdateLaunching(_deltaTime);
                break;
            case ItemMoveState.Sucking:
                UpdateSucking(_deltaTime);
                break;
            case ItemMoveState.Dropped:
                if (suckTarget != null)
                {
                    StartSucking(suckTarget);
                }
                break;
        }
    }

    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;

        // 포물선 낙하 구간의 실제 소요 시간 자체를 배율만큼 늘려서(= elapsed가 더 천천히 100%에 도달) 진짜 슬로우모션으로 만든다.
        // Sucking(흡입) 상태로 넘어간 뒤에는 이 배율이 적용되지 않는다.
        float effectiveDuration = duration * arcSlowMoTimeScale;
        float t = Mathf.Clamp01(elapsed / effectiveDuration);

        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t);
        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);

            if (outlineObj != null)
                outlineObj.transform.localPosition = visualTransform.localPosition;
            SyncVFXPosition();
            SyncVFXSortingOrder();
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.zero;
                if (outlineObj != null) outlineObj.transform.localPosition = Vector3.zero;
                if (vfxRoot != null) vfxRoot.transform.localPosition = Vector3.zero;
            }

            state = ItemMoveState.Dropped;
            if (suckTarget != null) StartSucking(suckTarget);
        }
    }

    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null)
        {
            state = ItemMoveState.Dropped;
            return;
        }

        Vector3 targetPos = suckTarget.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        if (distance < MinAcquireDist)
        {
            lootItemAcquiredEvent?.Invoke(this);
            return;
        }

        suckSpeed += SuckAccel * _deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, suckSpeed * _deltaTime);

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 5f);

            if (outlineObj != null)
                outlineObj.transform.localPosition = visualTransform.localPosition;
            SyncVFXPosition();
            SyncVFXSortingOrder();
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (state == ItemMoveState.Sucking || !bDrop) return;

        if (_other.CompareTag("ItemSensor"))
        {
            suckTarget = _other.transform;
            if (state == ItemMoveState.Dropped)
            {
                StartSucking(suckTarget);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (!bDrop) return;
            
        if (_other.CompareTag("ItemSensor") && suckTarget == _other.transform)
        {
            suckTarget = null;
        }
    }

    private void StartSucking(Transform _target)
    {
        suckTarget = _target;
        suckSpeed = 0f;
        state = ItemMoveState.Sucking;
    }
}
