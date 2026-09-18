using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <b>[미사용 확정] 히든맵은 쓰지 않기로 확정된 기능입니다. 배선하지 마십시오.</b>
///
/// 이 클래스의 유일한 출구인 CalcHiddenMapGrade()를 부르는 곳은 InDungeonSystem.SetHiddenMapGrade()
/// 하나뿐이고, 그 메서드는 public이지만 <b>호출부가 0</b>입니다. 그래서
/// InDungeonObjectManager.hiddenMapGrade는 영원히 None이고, 나무 등급을 히든맵 분포로 굴리는 분기
/// (CalculateRandomTreeData의 첫 갈래)는 한 번도 돌지 않습니다.
///
/// 코드는 그대로 두되 <b>되살릴 때 반드시 먼저 볼 것</b>:
///
///   1) hiddenMapGradeProbDatas에 None 항목이 없습니다(Fascinating 65 / Advanced 30 / Perfect 5).
///      totalProb이 100이고 Random.Range(0, 100)을 굴리므로 <b>모든 히든맵이 반드시 등급을 갖습니다</b>.
///      "가끔 히든맵"이 아니라 "히든맵이면 무조건 고등급"이 됩니다.
///
///   2) 그러면 Advanced/Perfect 나무가 실제로 스폰되는데, 그 나무들은 보석 단계를 거쳐 죽으므로
///      원목 대신 원석을 떨어뜨립니다(InDungeonObjectManager.OnTreeDead). 그런데 GemOreItemController의
///      드랍 테이블에는 <b>다이아/프리즘 줄이 한 줄도 없습니다</b>. 즉 체력 3~3.5배짜리 나무를 서너 번
///      베고도 원목도 원석도 얻지 못합니다. 되살리려면 드랍 테이블을 먼저 채워야 합니다.
/// </summary>
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
