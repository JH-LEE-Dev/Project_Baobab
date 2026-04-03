using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class UI_FadeAnimation : UI_AnimationBase
{
    private Image img;

    public override void Initialize()
    {
        img = objRect.gameObject.GetComponent<Image>();
    }

    public override Tween PlayEnter()
    {
        return img.DOFade(0f, duration).SetEase(easeType);
    }

    public override void ResetState()
    {
        Color color = img.color;
        color.a = 1f;
        img.color = color;
    }
}
