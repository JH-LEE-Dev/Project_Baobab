using UnityEngine;

// 엔딩 특성. 오프로드 운반 상자가 소풍 바구니로 바뀌고, 이 상태로 마을 오프로드에 타면 엔딩이 시작된다.
// 다른 특성처럼 프레스티지 경험치를 준다. 특성 창에서 찍으면 상자는 창을 닫을 때 전리품 진화 연출과 함께 바구니로 바뀐다.
[CreateAssetMenu(fileName = "Picnic Basket", menuName = "Game/Skill Command/Picnic Basket")]
public class SC_PicnicBasket : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.townObjSystemCH.SetPicnicBasket(true);
        _system.inDungeonObjManagerCH.SetPicnicBasket(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.townObjSystemCH.SetPicnicBasket(false);
        _system.inDungeonObjManagerCH.SetPicnicBasket(false);
    }
}
