using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성장형 전리품(씨앗 → 꽃 → 나무)의 단계별 비주얼 데이터.
/// - 아우라 색: 단계별 3개. 같은 단계면 전리품 종류가 달라도 같은 색을 쓴다.
/// - 스프라이트: (전리품, 단계)별. 아직 아트가 없는 항목은 isPlaceholder로 표시해 두면 임시임을 알 수 있고,
///   스프라이트를 넣고 isPlaceholder를 끄면 코드 수정 없이 바로 바뀐다.
/// 마을 기둥(LootDisplayObject)과 전리품 상호작용 화면(HUD_LootReveal)이 같은 데이터를 읽는다.
/// </summary>
[CreateAssetMenu(fileName = "LootGrowthVisualData", menuName = "Game/Loot/Growth Visual Data")]
public class LootGrowthVisualData : ScriptableObject
{
    public const int MaxTier = 3;

    [Serializable]
    public struct TierSprite
    {
        public LootType lootType;
        [Range(1, MaxTier)] public int tier;
        public Sprite sprite;
        [Tooltip("true면 아직 정식 아트가 아닌 임시 스프라이트다. 정식 스프라이트를 넣은 뒤 끈다.")]
        public bool isPlaceholder;
    }

    [Header("Aura (단계별 - 같은 단계는 전리품이 달라도 같은 색)")]
    [Tooltip("인덱스 0 = 1단계(씨앗), 1 = 2단계(꽃), 2 = 3단계(나무). HDR 색.")]
    [ColorUsage(true, true)] [SerializeField] private Color[] tierAuraColors = new Color[MaxTier];

    [Header("Sprites ((전리품, 단계)별)")]
    [SerializeField] private List<TierSprite> tierSprites = new List<TierSprite>(9);

    public bool TryGetAuraColor(int _tier, out Color _color)
    {
        _color = default;

        if (_tier < 1 || MaxTier < _tier || null == tierAuraColors || tierAuraColors.Length < _tier)
        {
            return false;
        }

        _color = tierAuraColors[_tier - 1];
        return true;
    }

    // 스프라이트가 등록돼 있고 비어 있지 않으면 true. _bPlaceholder로 임시 여부를 함께 돌려준다.
    public bool TryGetSprite(LootType _lootType, int _tier, out Sprite _sprite, out bool _bPlaceholder)
    {
        _sprite = null;
        _bPlaceholder = false;

        if (null == tierSprites)
        {
            return false;
        }

        for (int i = 0; i < tierSprites.Count; i++)
        {
            TierSprite _entry = tierSprites[i];

            if (_entry.lootType != _lootType || _entry.tier != _tier || null == _entry.sprite)
            {
                continue;
            }

            _sprite = _entry.sprite;
            _bPlaceholder = _entry.isPlaceholder;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    // 에디터 편의: 임시 스프라이트가 남아 있으면 인스펙터에서 알 수 있도록 개수를 노출한다.
    public int PlaceholderCount
    {
        get
        {
            int _count = 0;

            for (int i = 0; null != tierSprites && i < tierSprites.Count; i++)
            {
                if (true == tierSprites[i].isPlaceholder)
                {
                    _count++;
                }
            }

            return _count;
        }
    }

    // 인덱스 접근(빌더용)
    public void SetEditorData(Color[] _auraColors, List<TierSprite> _sprites)
    {
        tierAuraColors = _auraColors;
        tierSprites = _sprites;
    }
#endif
}
