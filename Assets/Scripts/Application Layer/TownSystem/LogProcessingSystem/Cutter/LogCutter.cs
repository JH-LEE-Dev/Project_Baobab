using System;
using UnityEngine;

public class LogCutter : MonoBehaviour, ILogCutter, ICutterCH
{
    public event Action CuttingDoneEvent;
    public event Action<ILogItemData> CuttingStartEvent;

    private LogItem cuttingItem;

    // 외부 의존성
    [SerializeField] private float cuttingDuration = 5.0f;
    private float baseCuttingDuration;
    private float totalSpeedMultiplier = 1.0f;

    // 내부 상태
    private Animator anim;
    private readonly int startHash = Animator.StringToHash("bStart");
    private float cuttingTimer = 0f;
    private bool bIsCutting = false;

    public float timeRemaining => cuttingTimer;

    ILogItemData ILogCutter.logToCut => logToCut;

    private ILogItemData logToCut;

    public void Initialize()
    {
        anim = GetComponent<Animator>();
        baseCuttingDuration = cuttingDuration;
    }

    private void Update()
    {
        if (!bIsCutting) return;

        cuttingTimer -= Time.deltaTime;
        if (cuttingTimer <= 0f)
        {
            bIsCutting = false;
            CuttingDone();
        }
    }

    public void CuttingDone()
    {
        anim.SetBool(startHash, false);
        CuttingDoneEvent?.Invoke();
    }

    public void StartCutting(LogItem _item,ILogItemData _itemData)
    {
        if (bIsCutting) return;

        cuttingItem = _item;
        bIsCutting = true;
        cuttingTimer = cuttingDuration;
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
        
        // 속도 증가 비율에 따라 Duration 감소 (Duration = Base / Multiplier)
        cuttingDuration = baseCuttingDuration / totalSpeedMultiplier;
    }
}
