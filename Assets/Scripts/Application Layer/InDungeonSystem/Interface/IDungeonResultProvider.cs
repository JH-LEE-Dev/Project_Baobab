using System.Collections.Generic;

public interface IDungeonResultProvider
{
    public int GetTreeKillCnt();

    public int GetLostLogItemCnt();

    /// <summary>
    /// 이번 런에서 이 등급의 원석으로 얻은 재화량. 인벤토리 HUD의 원석 숫자와 같은 단위다
    /// (떨어진 알갱이 개수가 아니라 그 알갱이들이 담고 있던 재화의 합).
    /// 한 번도 안 주웠으면 0이다.
    /// </summary>
    public long GetAcquiredGemOreAmount(GemOreType _gemOreType);

    /// <summary>이번 런에서 원석을 조금이라도 주웠는지. 등급별로 묻지 않고 한 번에 판정할 때 쓴다.</summary>
    public bool HasAcquiredAnyGemOre();

    /// <summary>
    /// 이번 런에서 얻은 전리품 목록. 얻은 순서대로 들어 있고 같은 종류가 두 번 들어가지 않는다.
    /// 아무것도 못 얻었으면 빈 목록이며 null이 되지 않는다.
    ///
    /// <b>내부 목록을 그대로 돌려준다.</b> 읽기만 할 것 (다음 런이 시작되면 이 목록이 비워진다).
    /// </summary>
    public IReadOnlyList<LootType> GetAcquiredLoots();
}
