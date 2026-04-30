using System.Collections.Generic;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

public class UI_Backpack : MonoBehaviour
{
    enum BACKACK_STATE { OPEN, CLOSE, END };

    [SerializeField] private Image baseBackpack;
    [SerializeField] private Image shadowBackpack;

    [SerializeField] private List<Sprite> baseSrc = new((int)BACKACK_STATE.END);
    [SerializeField] private List<Sprite> shadowSrc = new((int)BACKACK_STATE.END);

    [SerializeField] private UI_InvMotionPlayer invMotionPlayer;

    public void Initialize()
    {
        invMotionPlayer?.Initialize();
    }

    public void OpenInventory()
    {
        if (null == baseBackpack || null == shadowBackpack)
            return;

        baseBackpack.sprite = baseSrc[(int)BACKACK_STATE.OPEN];
        shadowBackpack.sprite = shadowSrc[(int)BACKACK_STATE.CLOSE];

        invMotionPlayer?.OpenInventory();
    }

    public void CloseInventory()
    {
        if (null == baseBackpack || null == shadowBackpack)
            return;

        baseBackpack.sprite = baseSrc[(int)BACKACK_STATE.CLOSE];
        shadowBackpack.sprite = shadowSrc[(int)BACKACK_STATE.CLOSE];

        invMotionPlayer?.CloseInventory();
    }

    public void SkipAnimation(bool _isTrigger)
    {
        invMotionPlayer?.SkipAnimation(_isTrigger);
    }
}
