using UnityEngine;

public class CharacterAnimTrigger : MonoBehaviour
{
    public bool bActivated = true;
    public void PlayFootstepSound()
    {
        if (bActivated)
            Sound.Play(SoundID.GrassFootstep, transform.position);
    }
}
