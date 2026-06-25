using System;
using System.Collections.Generic;
using UnityEngine;

public class LogCutter : MonoBehaviour, ILogCutter, ICutterCH
{
    public event Action CuttingDoneEvent;
    public event Action<ILogItemData> CuttingStartEvent;

    [Header("Data References")]
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;
    [SerializeField] private List<LogItemDurabilityData> logItemDurabilityDatas;

    [Space(10)]
    [Header("Visual & Animation")]
    [SerializeField] private List<Sprite> cuttingAnimationSprites;

    [Space(10)]
    [Header("VFX Settings")]
    [SerializeField] private Transform effectTransform;
    [SerializeField] private ParticleSystem.MinMaxGradient effectColor;

    // 내부 상태 및 컴포넌트 참조
    private LogItem cuttingItem;
    private float totalSpeedMultiplier = 1.0f;
    private bool bIsCutting = false;
    private bool bPowerSupply = false;
    private float bPowerSupplyValue = 5f; // 500퍼센트를 의미
    private float maxDurability = 0f;
    private SpriteRenderer visualSpriteRenderer;
    private float animProgress = 0f;
    private bool isReversing = false;

    // 시각적 효과용 (Squash & Stretch)
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

    private CustomSortable customSortable;
    private ILogItemData logToCut;
    private MapType mapType;
    private VFXComponent vfxComponent;
    private ParticleSystem cuttingEffect;
    private bool wasForward = false;

    // 프로퍼티
    public float timeRemaining
    {
        get
        {
            if (cuttingItem == null || !bIsCutting) return 0f;
            return cuttingItem.durability / GetCurrentSpeed();
        }
    }

    public float elapsedProcessingTime
    {
        get
        {
            if (cuttingItem == null || !bIsCutting) return 0f;
            return (maxDurability - cuttingItem.durability) / GetCurrentSpeed();
        }
    }

    public float totalProcessingTime
    {
        get
        {
            if (cuttingItem == null || !bIsCutting) return 0f;
            return maxDurability / GetCurrentSpeed();
        }
    }

    ILogItemData ILogCutter.logToCut => logToCut;
    bool ILogCutter.bIsCutting => bIsCutting;
    float ILogCutter.elapsedProcessingTime => elapsedProcessingTime;
    float ILogCutter.totalProcessingTime => totalProcessingTime;

    public void Initialize()
    {
        // 자식 오브젝트의 SpriteRenderer Transform 캐싱
        visualSpriteRenderer = GetComponent<SpriteRenderer>();
        if (visualSpriteRenderer != null) visualTransform = visualSpriteRenderer.transform;

        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(visualSpriteRenderer);

        vfxComponent = GetComponent<VFXComponent>();
        vfxComponent.Initialize();
    }

    private void Update()
    {
        UpdateBounce(Time.deltaTime);
        UpdateAnimation(Time.deltaTime);

        if (!bIsCutting || cuttingItem == null)
        {
            UpdateVFXState();
            return;
        }

        float currentSpeed = GetCurrentSpeed();

        // 1초에 1 * currentSpeed 만큼 내구도 감소
        float decreaseAmount = Time.deltaTime * currentSpeed;
        cuttingItem.durability -= decreaseAmount;

        if (cuttingItem.durability <= 0f)
        {
            cuttingItem.durability = 0f;
            bIsCutting = false;
            CuttingDone();
        }

        UpdateVFXState();
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    public void CuttingDone()
    {
        cuttingItem.gameObject.SetActive(true);
        CuttingDoneEvent?.Invoke();
    }

    public void StartCutting(LogItem _item, ILogItemData _itemData)
    {
        if (bIsCutting) return;

        cuttingItem = _item;
        bIsCutting = true;
        isReversing = false; // 되돌아가는 도중에 나무가 들어오면 즉시 정방향으로 전환

        // 내구도 멀티플라이어 적용
        if (logItemDurabilityDatas != null)
        {
            for (int i = 0; i < logItemDurabilityDatas.Count; i++)
            {
                if (logItemDurabilityDatas[i].logState == _item.logState)
                {
                    cuttingItem.durability *= logItemDurabilityDatas[i].durabilityMultiplier;
                    break;
                }
            }
        }

        maxDurability = cuttingItem.durability;
        logToCut = _itemData;
        TriggerBounce();
        CuttingStartEvent?.Invoke(logToCut);

        UpdateVFXState();
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public LogItem GetCuttingLogItem()
    {
        cuttingItem.SetTimberSprite();
        return cuttingItem;
    }

    public void IncreaseCutSpeed(float _amount)
    {
        // _amount는 0보다 큰 수이고 퍼센트 (예: 10.0f는 10% 속도 증가)
        totalSpeedMultiplier += (_amount / 100.0f);
    }

    public CutterSaveData GetSaveData()
    {
        CutterSaveData saveData = new CutterSaveData();
        saveData.bIsCutting = bIsCutting;
        saveData.totalSpeedMultiplier = totalSpeedMultiplier;
        saveData.bPowerSupply = bPowerSupply;

        if (bIsCutting && cuttingItem != null)
        {
            saveData.cuttingItemData = new ItemSaveData
            {
                itemType = cuttingItem.itemType,
                treeType = cuttingItem.treeType,
                logState = cuttingItem.logState,
                durability = cuttingItem.durability,
                color = cuttingItem.color // 컬러 저장
            };
        }

        return saveData;
    }

    public void LoadSaveData(CutterSaveData _data, LogItemPoolingManager _poolingManager)
    {
        totalSpeedMultiplier = _data.totalSpeedMultiplier;
        bIsCutting = _data.bIsCutting;
        bPowerSupply = _data.bPowerSupply;

        if (bIsCutting && _data.cuttingItemData.itemType != ItemType.None)
        {
            LogItemData data = new LogItemData
            {
                itemType = _data.cuttingItemData.itemType,
                treeType = _data.cuttingItemData.treeType,
                logState = _data.cuttingItemData.logState,
                color = _data.cuttingItemData.color // 컬러 복구
            };

            // 스프라이트 복구
            var typeData = logItemTypeDataBase.Get(data.treeType);
            if (typeData != null)
            {
                data.sprite = typeData.sprite;
            }

            cuttingItem = _poolingManager.GetLogItem(data);
            if (cuttingItem != null)
            {
                float baseDurability = cuttingItem.durability;
                if (logItemDurabilityDatas != null)
                {
                    for (int i = 0; i < logItemDurabilityDatas.Count; i++)
                    {
                        if (logItemDurabilityDatas[i].logState == cuttingItem.logState)
                        {
                            baseDurability *= logItemDurabilityDatas[i].durabilityMultiplier;
                            break;
                        }
                    }
                }
                maxDurability = baseDurability;

                cuttingItem.transform.position = transform.position; // 커터 위치로 설정
                cuttingItem.durability = _data.cuttingItemData.durability;

                logToCut = data;
            }

            cuttingItem.gameObject.SetActive(false);
        }
        else
        {
            cuttingItem = null;
            logToCut = null;
            maxDurability = 0f;
        }

        Debug.Log("[LogCutter] Cutter Save Data Loaded.");
    }

    public void SetPowerSupply(bool _bPowerSupply)
    {
        bPowerSupply = _bPowerSupply;
    }

    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;
    }

    private void TriggerBounce()
    {
        bounceTime = 0f;
    }

    private void UpdateBounce(float _deltaTime)
    {
        if (bounceTime >= BOUNCE_DURATION)
        {
            if (visualTransform != null && visualTransform.localScale != Vector3.one)
                visualTransform.localScale = Vector3.one;
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / BOUNCE_DURATION;

        // 진폭을 0.4로 키우고 감쇠를 3f로 늦춰 더 찰진 느낌 부여
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Exp(-t * 1.5f) * 0.2f;

        if (visualTransform != null)
        {
            // X축 확대 시 Y축 축소 (Squash & Stretch)
            visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
        }
    }

    private float GetCurrentSpeed()
    {
        float speed = totalSpeedMultiplier;
        if (bPowerSupply && mapType != MapType.Town) speed *= bPowerSupplyValue;
        return speed;
    }

    private void UpdateVFXState()
    {
        bool isForward = bIsCutting && !isReversing;
        if (isForward != wasForward)
        {
            if (isForward)
            {
                PlayCuttingEffect();
            }
            else
            {
                StopCuttingEffect();
            }
            wasForward = isForward;
        }
    }

    private void PlayCuttingEffect()
    {
        if (vfxComponent != null && effectTransform != null)
        {
            if (cuttingEffect != null)
            {
                vfxComponent.Stop(cuttingEffect, true);
            }
            VFXPlaySettings settings = new VFXPlaySettings("CuttingEffect", effectTransform.position, effectTransform.rotation, effectColor, effectTransform);
            cuttingEffect = vfxComponent.Play(settings);
        }
    }

    private void StopCuttingEffect()
    {
        if (vfxComponent != null && cuttingEffect != null)
        {
            vfxComponent.Stop(cuttingEffect);
            cuttingEffect = null;
        }
    }

    private void UpdateAnimation(float _deltaTime)
    {
        if (cuttingAnimationSprites == null || cuttingAnimationSprites.Count == 0 || visualSpriteRenderer == null) return;

        int totalFrames = cuttingAnimationSprites.Count;

        if (bIsCutting)
        {
            float currentSpeed = GetCurrentSpeed();
            float totalDuration = currentSpeed > 0f ? (maxDurability / currentSpeed) : 0f;

            if (totalDuration > 0f && totalDuration < 3f)
            {
                // 3초 미만일 때는 가공 진행률에 맞춰 2:1 비율로 정/역방향 딱 맞춰 재생
                float progress = 1f - (cuttingItem.durability / maxDurability);
                if (progress < (2f / 3f))
                {
                    animProgress = progress * 1.5f;
                    isReversing = false;
                }
                else
                {
                    animProgress = (1f - progress) * 3f;
                    isReversing = true;
                }
            }
            else
            {
                // 3초 이상일 때는 기존의 정방향 2초, 역방향 1초 로직 유지
                if (!isReversing)
                {
                    animProgress += _deltaTime * 0.5f; // 2초 동안 0 -> 1로 정방향 진행
                    if (animProgress >= 1f)
                    {
                        animProgress = 1f;
                        isReversing = true;
                    }
                }
                else
                {
                    animProgress -= _deltaTime; // 1초 동안 1 -> 0으로 역방향 진행
                    if (animProgress <= 0f)
                    {
                        animProgress = 0f;
                        isReversing = false;
                    }
                }
            }
        }
        else
        {
            // 컷팅이 끝났거나 나무가 없을 경우 0프레임으로 되돌아가기
            if (animProgress > 0f)
            {
                isReversing = true;
                animProgress -= _deltaTime;
                if (animProgress <= 0f)
                {
                    animProgress = 0f;
                    isReversing = false;
                }
            }
        }

        int frameIndex = Mathf.Clamp(Mathf.FloorToInt(animProgress * (totalFrames - 1)), 0, totalFrames - 1);
        visualSpriteRenderer.sprite = cuttingAnimationSprites[frameIndex];
    }

    private void OnDisable()
    {
        StopCuttingEffect();
        wasForward = false;
    }
}
