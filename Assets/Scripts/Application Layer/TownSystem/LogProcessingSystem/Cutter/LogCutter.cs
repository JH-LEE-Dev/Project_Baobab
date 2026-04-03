using System;
using UnityEngine;

public class LogCutter : MonoBehaviour, ILogCutter
{
    public event Action CuttingDoneEvent;
    public event Action<ILogItemData> CuttingStartEvent;

    // 외부 의존성
    [SerializeField] private float cuttingDuration = 5.0f;

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

    public void StartCutting(ILogItemData _itemData)
    {
        if (bIsCutting) return;

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
}
