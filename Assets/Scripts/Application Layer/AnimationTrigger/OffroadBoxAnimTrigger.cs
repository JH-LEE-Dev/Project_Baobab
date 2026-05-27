using UnityEngine;
using UnityEngine.Animations;

public class OffroadBoxAnimTrigger : MonoBehaviour
{
    private OffroadContainerVComponent offroadContainer;

    public void Start()
    {
        offroadContainer = transform.parent.GetComponent<OffroadContainerVComponent>();
    }

    public void OpenAnimationEnd()
    {

    }

    public void CloseAnimationEnd()
    {
        
    }
}
