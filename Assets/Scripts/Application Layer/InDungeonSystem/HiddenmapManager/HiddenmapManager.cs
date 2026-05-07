using System;
using System.Collections.Generic;
using UnityEngine;

public class HiddenmapManager : MonoBehaviour
{

    private bool currentlyHiddenMap = false;

    private HiddenMapGrade currentHiddenMapGrade = HiddenMapGrade.None;

    [SerializeField] private List<HiddenMapGradeProbData> hiddenMapGradeProbDatas;


    public void Initialize()
    {

    }

    public HiddenMapGrade CalcHiddenMapGrade()
    {
        if (hiddenMapGradeProbDatas == null || hiddenMapGradeProbDatas.Count == 0)
        {
            currentHiddenMapGrade = HiddenMapGrade.None;
            currentlyHiddenMap = false;
            return currentHiddenMapGrade;
        }

        float totalProb = 0f;
        for (int i = 0; i < hiddenMapGradeProbDatas.Count; i++)
        {
            totalProb += hiddenMapGradeProbDatas[i].probability;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalProb);
        float cumulativeProb = 0f;

        for (int i = 0; i < hiddenMapGradeProbDatas.Count; i++)
        {
            cumulativeProb += hiddenMapGradeProbDatas[i].probability;
            if (randomValue <= cumulativeProb)
            {
                currentHiddenMapGrade = hiddenMapGradeProbDatas[i].grade;
                break;
            }
        }

        currentlyHiddenMap = currentHiddenMapGrade != HiddenMapGrade.None;
        return currentHiddenMapGrade;
    }
}
