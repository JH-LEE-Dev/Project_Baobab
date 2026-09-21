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
///   2) 그러면 Advanced/Perfect 나무가 실제로 스폰됩니다. <b>지금은 그래도 원목이 정상적으로
///      떨어집니다</b> - 원석/용광로 계열이 SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED = false로 꺼져
///      있어서, 보석 단계를 거친 나무도 InDungeonObjectManager의 else 분기를 타 SpawnLogItem()으로
///      갑니다(원석이 들어오기 전과 같은 동작).
///
///      단, <b>원석 계열을 다시 켠다면 이 항목이 되살아납니다</b>: 그때는 보석 나무가 원목 대신
///      원석을 떨어뜨리는데 GemOreItemController의 드랍 테이블에 <b>다이아/프리즘 줄이 한 줄도
///      없어</b>, 체력 3~3.5배짜리 나무를 서너 번 베고도 원목도 원석도 얻지 못합니다.
///      히든맵과 원석을 <b>함께</b> 되살릴 때는 드랍 테이블을 먼저 채우십시오.
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
