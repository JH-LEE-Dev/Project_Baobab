using UnityEngine;
using UnityEngine.Animations;

public class ArmAnimTrigger : MonoBehaviour
{
    private ArmComponent arm;

    public void Start()
    {
        arm = transform.parent.GetComponent<ArmComponent>();

        if(arm == null)
            Debug.LogError("ArmAnimTrigger -> character is null");
    }

    public void AttackEnd()
    {
        arm.armAnimValueHandler.AttackEnd(true);
    }
}
