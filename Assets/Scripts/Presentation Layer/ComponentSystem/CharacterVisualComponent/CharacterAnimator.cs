using UnityEngine;
using System.Collections.Generic;
public class CharacterAnimator : MonoBehaviour
{
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer baseSR;
    [SerializeField] private SpriteRenderer faceSR;
    [SerializeField] private SpriteRenderer onWaterBaseSR;
    [SerializeField] private SpriteRenderer onWaterFaceSR;
    [SerializeField] private SpriteRenderer shadowSR;

    [Space]
    [Header("InTown Base Animation Sprites")]
    [SerializeField] private List<Sprite> base_IdleR;
    [SerializeField] private List<Sprite> base_IdleD;
    [SerializeField] private List<Sprite> base_IdleRD;
    [SerializeField] private List<Sprite> base_IdleRU;
    [SerializeField] private List<Sprite> base_IdleU;
    [SerializeField] private List<Sprite> base_RunR;
    [SerializeField] private List<Sprite> base_RunD;
    [SerializeField] private List<Sprite> base_RunRD;
    [SerializeField] private List<Sprite> base_RunRU;
    [SerializeField] private List<Sprite> base_RunU;

    [Space]
    [Header("InDungeon Base Animation Sprites")]
    [SerializeField] private List<Sprite> InDungeon_base_IdleR;
    [SerializeField] private List<Sprite> InDungeon_base_IdleD;
    [SerializeField] private List<Sprite> InDungeon_base_IdleRD;
    [SerializeField] private List<Sprite> InDungeon_base_IdleRU;
    [SerializeField] private List<Sprite> InDungeon_base_IdleU;
    [SerializeField] private List<Sprite> InDungeon_base_RunR;
    [SerializeField] private List<Sprite> InDungeon_base_RunD;
    [SerializeField] private List<Sprite> InDungeon_base_RunRD;
    [SerializeField] private List<Sprite> InDungeon_base_RunRU;
    [SerializeField] private List<Sprite> InDungeon_base_RunU;

    [Space]
    [Header("InTown Face Animation Sprites")]
    [SerializeField] private List<Sprite> face_IdleR;
    [SerializeField] private List<Sprite> face_IdleD;
    [SerializeField] private List<Sprite> face_IdleRD;
    [SerializeField] private List<Sprite> face_RunR;
    [SerializeField] private List<Sprite> face_RunD;
    [SerializeField] private List<Sprite> face_RunRD;


    [Space]
    [Header("InDungeon Face Animation Sprites")]
    [SerializeField] private List<Sprite> inDungeon_Face_IdleR;
    [SerializeField] private List<Sprite> inDungeon_Face_IdleD;
    [SerializeField] private List<Sprite> inDungeon_Face_IdleRD;
    [SerializeField] private List<Sprite> inDungeon_Face_RunR;
    [SerializeField] private List<Sprite> inDungeon_Face_RunD;
    [SerializeField] private List<Sprite> inDungeon_Face_RunRD;

    [Space]
    [Header("InTown Face Blink Animation Sprites")]
    [SerializeField] private List<Sprite> blink_IdleR;
    [SerializeField] private List<Sprite> blink_IdleD;
    [SerializeField] private List<Sprite> blink_IdleRD;
    [SerializeField] private List<Sprite> blink_RunR;
    [SerializeField] private List<Sprite> blink_RunD;
    [SerializeField] private List<Sprite> blink_RunRD;

    [Space]
    [Header("InDungeon Face Blink Animation Sprites")]
    [SerializeField] private List<Sprite> inDungone_Blink_IdleR;
    [SerializeField] private List<Sprite> inDungone_Blink_IdleD;
    [SerializeField] private List<Sprite> inDungone_Blink_IdleRD;
    [SerializeField] private List<Sprite> inDungone_Blink_RunR;
    [SerializeField] private List<Sprite> inDungone_Blink_RunD;
    [SerializeField] private List<Sprite> inDungone_Blink_RunRD;

    [Space]
    [Header("Animation Speed")]
    [SerializeField] private float idleSample = 5;
    [SerializeField] private float runSample = 10;
}
