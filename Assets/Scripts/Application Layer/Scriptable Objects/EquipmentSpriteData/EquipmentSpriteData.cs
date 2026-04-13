using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentSpriteData", menuName = "ScriptableObjects/Equipment/SpriteData")]
public class EquipmentSpriteData : ScriptableObject
{
    [Serializable]
    public struct EquipmentSprite
    {
        public WeaponMode type;
        public Sprite mainSprite;
        public Sprite subSprite; // 탄약 등 추가 이미지용
    }

    [SerializeField] private List<EquipmentSprite> equipmentSprites;
    
    // 빠른 조회를 위한 딕셔너리 캐싱
    private Dictionary<WeaponMode, EquipmentSprite> spriteCache;

    public void Initialize()
    {
        if (spriteCache != null) 
            return;

        spriteCache = new Dictionary<WeaponMode, EquipmentSprite>();
        foreach (var item in equipmentSprites)
        {
            if (!spriteCache.ContainsKey(item.type))
            {
                spriteCache.Add(item.type, item);
            }
        }
    }

    public bool TryGetSprites(WeaponMode _type, out Sprite _main, out Sprite _sub)
    {
        Initialize();
        
        if (spriteCache.TryGetValue(_type, out var data))
        {
            _main = data.mainSprite;
            _sub = data.subSprite;
            return true;
        }

        _main = null;
        _sub = null;
        return false;
    }
}
