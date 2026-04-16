using System;
using UnityEngine;

public class LogCutter : MonoBehaviour, ILogCutter, ICutterCH
{
    public event Action CuttingDoneEvent;
    public event Action<ILogItemData> CuttingStartEvent;

    private LogItem cuttingItem;

    // 외부 의존성
    private float totalSpeedMultiplier = 1.0f;

    // 내부 상태
    private Animator anim;
    private readonly int startHash = Animator.StringToHash("bStart");
    private bool bIsCutting = false;

    public float timeRemaining
    {
        get
        {
            if (cuttingItem == null || !bIsCutting) return 0f;
            // 1초에 1 * Multiplier만큼 깎으므로, 남은 시간 = 남은 내구도 / (1 * Multiplier)
            return cuttingItem.durability / totalSpeedMultiplier;
        }
    }

    ILogItemData ILogCutter.logToCut => logToCut;

    private ILogItemData logToCut;

    public void Initialize()
    {
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!bIsCutting || cuttingItem == null) return;

        // 1초에 1 * totalSpeedMultiplier 만큼 내구도 감소
        float decreaseAmount = Time.deltaTime * totalSpeedMultiplier;
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
        anim.SetBool(startHash, false);
        CuttingDoneEvent?.Invoke();
    }

    public void StartCutting(LogItem _item, ILogItemData _itemData)
    {
        if (bIsCutting) return;

        cuttingItem = _item;
        bIsCutting = true;
        anim.SetBool(startHash, true);

        logToCut = _itemData;
        CuttingStartEvent?.Invoke(logToCut);
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public LogItem GetCuttingLogItem()
    {
        return cuttingItem;
    }

    public void IncreaseCutSpeed(float _amount)
    {
        // _amount는 0보다 큰 수이고 퍼센트 (예: 10.0f는 10% 속도 증가)
        totalSpeedMultiplier += (_amount / 100.0f);
    }
}
