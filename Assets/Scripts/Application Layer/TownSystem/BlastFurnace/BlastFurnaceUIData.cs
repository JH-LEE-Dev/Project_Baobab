using UnityEngine;

/// <summary>
/// 용광로 한 대의 상태를 UI에 넘기기 위한 묶음. UI는 이 값만 보고 그리면 된다.
///
/// BlastFurnaceManager가 매 프레임 갱신하는 목록에 담겨 오며, 목록은 재사용되므로
/// 참조를 들고 있으면 진행도(progress01)가 실시간으로 따라 움직인다.
/// 용광로 수(= 목록 길이)는 바뀌지 않는다. 해금되지 않은 것도 unlocked=false로 들어 있다.
/// </summary>
public struct BlastFurnaceUIData
{
    /// <summary>용광로 월드 위치(접지점). UI를 이 위에 띄우면 된다.</summary>
    public Vector3 position;

    /// <summary>이 용광로가 다루는 원석 등급. 지금 가공 중인 것도 같은 등급이다.</summary>
    public GemOreType gemOreType;

    /// <summary>특성으로 열렸는지. false면 마을에 보이지 않는다.</summary>
    public bool unlocked;

    /// <summary>지금 가공이 돌고 있는지.</summary>
    public bool running;

    /// <summary>가공 진행도 0~1. 가공 중이 아니면 0.</summary>
    public float progress01;

    /// <summary>
    /// 용광로에 들어 있는 원석 수. <b>가공 중인 배치도 포함된 값이다.</b>
    /// 한 배치 몫은 가공이 끝나는 순간에 한 번에 빠지므로, 도는 동안에는 숫자가 줄지 않는다.
    /// </summary>
    public int storedOre;

    /// <summary>주괴 하나를 만드는 데 필요한 원석 수. storedOre와 함께 "3 / 10"처럼 쓰면 된다.</summary>
    public int orePerIngot;
}
