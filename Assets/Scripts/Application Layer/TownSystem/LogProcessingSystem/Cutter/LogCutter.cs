using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TreeVFXColorData
{
    public TreeType treeType;
    public ParticleSystem.MinMaxGradient effectColor;
}

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
    [SerializeField] private List<TreeVFXColorData> treeVFXColorDatas;
    private ParticleSystem.MinMaxGradient effectColor;

    // 내부 상태 및 컴포넌트 참조
    private LogItem cuttingItem;
    private float totalSpeedMultiplier = 1.0f;
    private float globalSpeedMultiplier = 1.0f;

    public void SetGlobalSpeedMultiplier(float _mul)
    {
        globalSpeedMultiplier = _mul;
    }
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
    private AudioHandle cuttingSoundHandle = AudioHandle.Invalid;

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
        // 사운드는 여기서 직접 처리하지 않는다. 완료 직후 Update()가 다시 UpdateVFXState()를
        // 호출하면서 정방향->역방향 전환으로 인식되어 자연스럽게 파워다운 사운드로 이어진다.
        CuttingDoneEvent?.Invoke();
    }

    public void StartCutting(LogItem _item, ILogItemData _itemData)
    {
        if (bIsCutting) return;

        Sound.Play(SoundID.ConvayerCutterGetWood, transform.position, GetSoundVolume());

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
        bIsCutting = _data.bIsCutting;

        if (bIsCutting && _data.cuttingItemData.itemType != ItemType.None)
        {
            LogItemData data = new LogItemData
            {
                itemType = _data.cuttingItemData.itemType,
                treeType = _data.cuttingItemData.treeType,
                logState = _data.cuttingItemData.logState,
                color = _data.cuttingItemData.color // 컬러 복구
            };

            // 스프라이트 복구 - 황금/다이아/무지개 원목은 상태별 스프라이트를 써야 한다.
            var typeData = logItemTypeDataBase.Get(data.treeType);
            if (typeData != null)
            {
                data.sprite = typeData.GetSprite(data.logState);
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

                // 가공 재개 전까지 비활성화. SetActive를 null 체크 안으로 넣어, 풀이 아이템을
                // 반환하지 못하는 경우의 NullReferenceException을 방지한다.
                cuttingItem.gameObject.SetActive(false);
            }
            else
            {
                // 풀에서 아이템을 받지 못하면 가공 중 상태를 유지할 수 없으므로 안전하게 비가공 처리.
                bIsCutting = false;
                logToCut = null;
                maxDurability = 0f;
            }
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

    private bool isRestoredFromOtherMap = false;

    public void SetMapType(MapType _mapType)
    {
        MapType prevMap = mapType;
        mapType = _mapType;

        if (prevMap != mapType)
        {
            if (cuttingSoundHandle.IsValid)
            {
                Sound.StopTracked(cuttingSoundHandle);
                cuttingSoundHandle = AudioHandle.Invalid;
            }
            // 맵 상태가 바뀔 때(던전 -> 마을 복귀 등) sound 연출 상태를 리셋하여,
            // 정방향(가공 중)이든 역방향(날 복귀 중)이든 현재 단계에 맞춰 사운드가 100% 올바르게 재개되도록 유도
            wasForward = isReversing;

            if (mapType == MapType.Town && bIsCutting)
            {
                isRestoredFromOtherMap = true;
            }
        }
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
        return speed * globalSpeedMultiplier;
    }

    // 커터 속도가 1배(기본값)일 때는 기존 그대로 0.4초짜리 피치 램프를 쓰고, 거기서 속도가
    // 빨라진 만큼(GetCurrentSpeed()가 1보다 커진 비율만큼) 램프 시간을 반비례로 줄여
    // 피치가 변화하는 속도 자체도 비례해서 빨라지게 한다.
    private const float BASE_PITCH_RAMP_DURATION = 0.4f;

    private float GetPitchRampDuration()
    {
        float currentSpeed = GetCurrentSpeed();
        return currentSpeed > 0f ? BASE_PITCH_RAMP_DURATION / currentSpeed : BASE_PITCH_RAMP_DURATION;
    }

    private float GetSoundVolume()
    {
        return mapType == MapType.Town ? 1f : 0f;
    }

    private void UpdateVFXState()
    {
        bool isForward = bIsCutting && !isReversing;
        if (isForward != wasForward)
        {
            float soundVolume = GetSoundVolume();
            if (isForward)
            {
                PlayCuttingEffect();
                if (isRestoredFromOtherMap)
                {
                    // 이미 가공 중반에 던전에서 마을로 돌아온 경우:
                    // 0 RPM에서 시동이 새로 걸리는 예열음(PowerUp) 대신, 이미 쌩쌩 가동 중인 정상 피치(1.0)로 루프를 재생하여
                    // 카메라 하강 오디오 페이드인 연출과 자연스럽게 이어지게 한다.
                    cuttingSoundHandle = Sound.PlayTracked(SoundID.SawmillCutterLoop, transform.position,
                        soundVolume, true, 1f);
                    isRestoredFromOtherMap = false;
                }
                else
                {
                    // 새로 가공이 시작된 경우: 0.4초 피치 램프(시동 연출) 적용
                    cuttingSoundHandle = Sound.PlayTrackedWithPowerUp(SoundID.SawmillCutterLoop, transform.position,
                        soundVolume, true, GetPitchRampDuration());
                }
            }
            else
            {
                StopCuttingEffect();
                // 날이 되돌아올 때는 루프를 끊고 기존 사운드로 바꾸되, 루프가 지금 올라와 있던
                // 피치 그대로 이어받아 거기서부터 (가공 속도에 비례한 속도로) 서서히 내려가며
                // 꺼지는 느낌(전원이 빠지듯)을 준다.
                float pitchAtReversal = Sound.GetTrackedPitch(cuttingSoundHandle);
                Sound.StopTracked(cuttingSoundHandle);
                cuttingSoundHandle = Sound.PlayTracked(SoundID.Cutter, transform.position, soundVolume, true, pitchAtReversal);
                Sound.StopTrackedWithPowerDown(cuttingSoundHandle, GetPitchRampDuration());
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
            ParticleSystem.MinMaxGradient color = GetVFXColorForCurrentTree();
            VFXPlaySettings settings = new VFXPlaySettings("CuttingEffect", effectTransform.position, effectTransform.rotation, color, effectTransform);
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

    private ParticleSystem.MinMaxGradient GetVFXColorForCurrentTree()
    {
        if (cuttingItem != null && treeVFXColorDatas != null)
        {
            for (int i = 0; i < treeVFXColorDatas.Count; i++)
            {
                if (treeVFXColorDatas[i].treeType == cuttingItem.treeType)
                {
                    return treeVFXColorDatas[i].effectColor;
                }
            }
        }
        return effectColor;
    }

    private void UpdateAnimation(float _deltaTime)
    {
        if (cuttingAnimationSprites == null || cuttingAnimationSprites.Count == 0 || visualSpriteRenderer == null) return;

        int totalFrames = cuttingAnimationSprites.Count;

        if (bIsCutting)
        {
            if (maxDurability > 0f)
            {
                // durability와 관계없이 항상 가공 진행률에 맞춰 2:1 비율로 정/역방향 왕복 1회로 딱 맞춰 재생
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
        // 씬 전환 등으로 비활성화되는 경우는 페이드아웃 없이 즉시 정지한다 (코루틴은 비활성 오브젝트에서 진행되지 않음).
        Sound.StopTracked(cuttingSoundHandle);
        cuttingSoundHandle = AudioHandle.Invalid;
        wasForward = false;
    }
}