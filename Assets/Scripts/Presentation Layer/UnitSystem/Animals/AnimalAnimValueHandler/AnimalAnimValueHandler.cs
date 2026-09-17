// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  동물 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: Animal.Show()를 부르는 스포너가 없어 한 마리도 등장하지 않습니다.
//
// 참고: Animal.OnDisable의 Unregister 주석처럼 "켜면 살아난다"고 적어둔 검토 항목들은
//       이 결정으로 검토 대상에서 빠졌습니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: IAnimalObj가 AnimalHitSignal(InDungeonSystemSignals)과
//   UIView_Unit에 남아 있어, 파일만 지우면 컴파일이 깨집니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
using UnityEngine;

public class AnimalAnimValueHandler
{
    private Animator anim;
    private Animator shadowAnim;

    private bool bIdleAnimType = false;

    public readonly int runStartEndHash = Animator.StringToHash("bRunStartEnd");
    public readonly int idleTypeHash = Animator.StringToHash("bType");


    public void Initialize(Animator _anim, Animator _shadowAnim)
    {
        anim = _anim;
        shadowAnim = _shadowAnim;
    }

    public void RunStartEnd(bool _boolean)
    {
        anim.SetBool(runStartEndHash, _boolean);
        shadowAnim.SetBool(runStartEndHash, _boolean);
    }

    public void IdleEnd()
    {
        bIdleAnimType = Random.value < 0.5f;
        anim.SetBool(idleTypeHash, bIdleAnimType);
        shadowAnim.SetBool(idleTypeHash, bIdleAnimType);
    }
}
