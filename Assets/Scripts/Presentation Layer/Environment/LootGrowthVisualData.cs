using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성장형 전리품(씨앗 → 꽃 → 나무)의 단계별 비주얼 데이터.
/// - 아우라 색: 단계별 3개. 같은 단계면 전리품 종류가 달라도 같은 색을 쓴다.
/// - 스프라이트: (전리품, 단계)별. 확대(전리품 상호작용 화면, 64x64 한 장)와 전시(마을 기둥, 32x32 시트 프레임 애니메이션)를 따로 둔다.
///   아직 아트가 없는 항목은 isPlaceholder로 표시해 두면 임시임을 알 수 있고,
///   스프라이트를 넣고 isPlaceholder를 끄면 코드 수정 없이 바로 바뀐다.
/// - 원본은 Assets/Graphics/Magical Seed (Big = 확대, Small = 전시). 에디터의 MagicalSeedVisualBuilder가 채우고,
///   시트를 교체하면 MagicalSeedSheetPostprocessor가 슬라이스와 프레임 목록을 다시 맞춘다.
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
        [Tooltip("확대 스프라이트(전리품 상호작용 화면)")]
        public Sprite sprite;
        [Tooltip("true면 아직 정식 아트가 아닌 임시 스프라이트다. 정식 스프라이트를 넣은 뒤 끈다.")]
        public bool isPlaceholder;

        [Tooltip("전시 프레임(마을 기둥). 32x32 시트를 슬라이스한 순서(왼쪽 → 오른쪽)대로 넣는다.")]
        public Sprite[] displayFrames;
        [Tooltip("평소에 반복하는 프레임 인덱스 순서. 비우면 displayFrames 전부를 순서대로 반복한다.")]
        public int[] idleFrames;
        [Tooltip("actionInterval마다 한 번 재생하고 idle로 돌아오는 프레임 인덱스 순서. 비우면 idle만 반복한다.")]
        public int[] actionFrames;
        [Tooltip("초당 프레임 수(idle/action 공통)")]
        public float fps;
        [Tooltip("action 재생 간격(초) 최소/최대. 매번 이 범위에서 랜덤으로 고른다.")]
        public Vector2 actionInterval;
        [Tooltip("전시 그림이 실제로 그려진 영역의 중심(프레임 평균) - 스프라이트 중심 기준, 유닛. 후광을 여기에 맞춘다. 빌더가 픽셀에서 계산한다.")]
        public Vector2 displayArtCenter;
        [Tooltip("전시 그림의 밑둥(기둥 윗판 중앙에 놓일 점) - 스프라이트 중심 기준, 유닛. 비어 있으면 빌더가 픽셀에서 추정하고, 있으면 그대로 둔다(그림을 바꾸면 0으로 두고 다시 적용).")]
        public Vector2 displayBase;
        [Tooltip("확대 그림의 밑둥(상호작용 화면 기둥 윗판 중앙에 놓일 점) - 스프라이트 중심 기준, 유닛. 채우는 규칙은 displayBase와 같다.")]
        public Vector2 spriteBase;

        public bool HasDisplayFrames => null != displayFrames && 0 < displayFrames.Length;
    }

    [Header("Aura (단계별 - 같은 단계는 전리품이 달라도 같은 색)")]
    [Tooltip("인덱스 0 = 1단계(씨앗), 1 = 2단계(꽃), 2 = 3단계(나무). HDR 색.")]
    [ColorUsage(true, true)] [SerializeField] private Color[] tierAuraColors = new Color[MaxTier];

    [Header("Sprites ((전리품, 단계)별)")]
    [SerializeField] private List<TierSprite> tierSprites = new List<TierSprite>(12);

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

    // 확대 스프라이트가 등록돼 있고 비어 있지 않으면 true. _bPlaceholder로 임시 여부를 함께 돌려준다.
    // 단계가 정확히 없으면 FindEntryIndex 규칙대로 가까운 단계로 대신한다.
    public bool TryGetSprite(LootType _lootType, int _tier, out Sprite _sprite, out bool _bPlaceholder)
    {
        return TryGetSprite(_lootType, _tier, out _sprite, out _bPlaceholder, out _);
    }

    // _base: 확대 그림의 밑둥(spriteBase)
    public bool TryGetSprite(LootType _lootType, int _tier, out Sprite _sprite, out bool _bPlaceholder, out Vector2 _base)
    {
        _sprite = null;
        _bPlaceholder = false;
        _base = Vector2.zero;

        int _index = FindEntryIndex(_lootType, _tier, false);

        if (_index < 0)
        {
            return false;
        }

        _sprite = tierSprites[_index].sprite;
        _bPlaceholder = tierSprites[_index].isPlaceholder;
        _base = tierSprites[_index].spriteBase;
        return true;
    }

    // 전시 프레임이 등록된 항목을 찾는다. 단계가 정확히 없으면 FindEntryIndex 규칙대로 가까운 단계로 대신한다.
    public bool TryGetDisplay(LootType _lootType, int _tier, out TierSprite _entry)
    {
        _entry = default;

        int _index = FindEntryIndex(_lootType, _tier, true);

        if (_index < 0)
        {
            return false;
        }

        _entry = tierSprites[_index];
        return true;
    }

    // 1) 그 단계 이하 중 가장 높은 단계 (예: 2단계 아트가 없는 전리품의 2단계 = 1단계)
    // 2) 성장하지 않는 전리품(단계 0 이하)이면 가장 낮은 단계
    private int FindEntryIndex(LootType _lootType, int _tier, bool _bDisplay)
    {
        if (null == tierSprites)
        {
            return -1;
        }

        int _best = -1;
        int _lowest = -1;

        for (int i = 0; i < tierSprites.Count; i++)
        {
            TierSprite _entry = tierSprites[i];

            if (_entry.lootType != _lootType)
            {
                continue;
            }

            if (true == _bDisplay ? false == _entry.HasDisplayFrames : null == _entry.sprite)
            {
                continue;
            }

            if (_entry.tier <= _tier && (_best < 0 || tierSprites[_best].tier < _entry.tier))
            {
                _best = i;
            }

            if (_lowest < 0 || _entry.tier < tierSprites[_lowest].tier)
            {
                _lowest = i;
            }
        }

        if (0 <= _best)
        {
            return _best;
        }

        return _tier <= 0 ? _lowest : -1;
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
    public List<TierSprite> EditorTierSprites => tierSprites;

    public void SetEditorData(Color[] _auraColors, List<TierSprite> _sprites)
    {
        tierAuraColors = _auraColors;
        tierSprites = _sprites;
    }
#endif
}
