using UnityEngine;

public class CharacterAnimTrigger : MonoBehaviour
{
    public bool bActivated = true;
    public void PlayFootstepSound()
    {
        // 사용되지 않는 컴포넌트 (발소리는 CharacterAnimator의 Dust VFX 타이밍에 재생됨)
    }
}
