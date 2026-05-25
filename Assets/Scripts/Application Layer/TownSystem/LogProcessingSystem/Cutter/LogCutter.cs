using System;
using System.Collections.Generic;
using UnityEngine;

public class LogCutter : MonoBehaviour, ILogCutter, ICutterCH
{
    public event Action CuttingDoneEvent;
    public event Action<ILogItemData> CuttingStartEvent;

    private LogItem cuttingItem;

    // 외부 의존성
    private float totalSpeedMultiplier = 1.0f;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 내부 상태
    private Animator anim;
    private readonly int startHash = Animator.StringToHash("bStart");
    private bool bIsCutting = false;
    private bool bPowerSupply = false;
    private float bPowerSupplyValue = 5f; //500퍼센트를 의미.
    private float maxDurability = 0f;

    // 시각적 효과용 (Squash & Stretch)
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

    [SerializeField] private List<LogItemDurabilityData> logItemDurabilityDatas;

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

    private ILogItemData logToCut;

    private MapType mapType;

    public void Initialize()
    {
        anim = GetComponent<Animator>();

        // 자식 오브젝트의 SpriteRenderer Transform 캐싱
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) visualTransform = sr.transform;
    }

    private void Update()
    {
        UpdateBounce(Time.deltaTime);

        if (!bIsCutting || cuttingItem == null) return;

        float currentSpeed = GetCurrentSpeed();

        // 애니메이션 속도 동기화
        if (anim != null)
        {
            anim.speed = currentSpeed;
        }

        // 1초에 1 * currentSpeed 만큼 내구도 감소
        float decreaseAmount = Time.deltaTime * currentSpeed;
        cuttingItem.durability -= decreaseAmount;

        if (cuttingItem.durability <= 0f)
        {
            cuttingItem.durability = 0f;
            bIsCutting = false;
            CuttingDone();
        }
    }

    public void CuttingDone()
    {
        if (anim != null) anim.speed = 1.0f;
        anim.SetBool(startHash, false);
        cuttingItem.gameObject.SetActive(true);
        CuttingDoneEvent?.Invoke();
    }

    public void StartCutting(LogItem _item, ILogItemData _itemData)
    {
        if (bIsCutting) return;

        cuttingItem = _item;
        bIsCutting = true;
        anim.SetBool(startHash, true);

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
                anim.SetBool(startHash, true);

                logToCut = data;
            }

            cuttingItem.gameObject.SetActive(false);
        }
        else
        {
            cuttingItem = null;
            anim.SetBool(startHash, false);
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
}
