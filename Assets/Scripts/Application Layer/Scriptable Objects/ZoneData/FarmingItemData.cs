using UnityEngine;

[CreateAssetMenu(fileName = "FarmingItemData", menuName = "ScriptableObjects/Zone/FarmingItemData")]
public class FarmingItemData : ScriptableObject
{
    //외부 의존성
    [SerializeField] public string itemName;
    [SerializeField] public Sprite itemIcon;
}
