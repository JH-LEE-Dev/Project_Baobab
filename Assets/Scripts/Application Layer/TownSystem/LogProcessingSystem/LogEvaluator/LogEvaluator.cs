using System;
using System.Collections;
using UnityEngine;

public class LogEvaluator : MonoBehaviour
{
    public event Action<int> logEvaluatedEvent;

    [SerializeField] private LogItemValueDataBase logItemValueDataBase;
    [SerializeField] private GameObject storageObj;
    [SerializeField] private float evaluationDelay = 1.5f;

    private Animator anim;
    private Animator storageAnim;
    private Coroutine stopAnimCoroutine;

    private readonly int startHash = Animator.StringToHash("bStart");

    //Perfect 등급 : value * 10
    //Advance 등급 : value * 3
    //Fascinating 등급 : value * 1.3
    //Normal 등급 : value * (0.9,1.0,1.1 균등 확률로 설정)
    //Wet 등급 : value * 0.7
    //Damaged 등급 : value * (0.5,0.6,0.7 균등 확률로 설정)
    //Destroyed 등급 : value * 0.1

    public void Initialize()
    {
        anim = GetComponent<Animator>();
        storageAnim = storageObj.GetComponent<Animator>();
    }

    public void EvaluateLog(ILogItemData _itemData)
    {
        if (stopAnimCoroutine != null) StopCoroutine(stopAnimCoroutine);
        anim.SetBool(startHash, true);

        LogItemValueData valueData = logItemValueDataBase.Get(_itemData.treeType);
        if (valueData == null)
        {
            Debug.LogError($"LogEvaluator: Value data for {_itemData.treeType} not found.");
            return;
        }

        float baseValue = valueData.value;
        float multiplier = 1.0f;

        switch (_itemData.logState)
        {
            case LogState.Perfect:
                multiplier = 10.0f;
                break;
            case LogState.Advanced:
                multiplier = 3.0f;
                break;
            case LogState.Fascinating:
                multiplier = 1.3f;
                break;
            case LogState.Normal:
                float[] normalMultipliers = { 0.9f, 1.0f, 1.1f };
                multiplier = normalMultipliers[UnityEngine.Random.Range(0, normalMultipliers.Length)];
                break;
            case LogState.Wet:
                multiplier = 0.7f;
                break;
            case LogState.Damaged:
                float[] damagedMultipliers = { 0.5f, 0.6f, 0.7f };
                multiplier = damagedMultipliers[UnityEngine.Random.Range(0, damagedMultipliers.Length)];
                break;
            case LogState.Destoyed:
                multiplier = 0.1f;
                break;
        }

        int finalPrice = Mathf.RoundToInt(baseValue * multiplier);
        logEvaluatedEvent?.Invoke(finalPrice);

        stopAnimCoroutine = StartCoroutine(StopAnimationRoutine());
    }

    private IEnumerator StopAnimationRoutine()
    {
        yield return new WaitForSeconds(evaluationDelay);
        anim.SetBool(startHash, false);
        stopAnimCoroutine = null;
    }
}
