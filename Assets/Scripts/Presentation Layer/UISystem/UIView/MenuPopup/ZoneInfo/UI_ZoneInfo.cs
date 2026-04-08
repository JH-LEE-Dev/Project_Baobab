using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UI_ZoneInfo : MonoBehaviour
{
    [Header("Basic Info")]
    [SerializeField] private TMP_Text zoneNameText;

    [Header("Resource Containers")]
    [SerializeField] private Transform treeContainer;
    [SerializeField] private Transform animalContainer;
    
    [Header("Prefabs")]
    [SerializeField] private UI_FarmingItemElement farmingItemPrefab;

    private List<UI_FarmingItemElement> treeElements = new List<UI_FarmingItemElement>(10);
    private List<UI_FarmingItemElement> animalElements = new List<UI_FarmingItemElement>(10);

    public void Initialize()
    {
        // 정보창 자체는 켜두고 내용만 초기화하거나 첫 데이터를 기다립니다.
        gameObject.SetActive(true);
    }

    public void Show(ZoneData _data)
    {
        if (_data == null) return;

        gameObject.SetActive(true);

        if (zoneNameText != null) 
            zoneNameText.text = _data.ZoneName;
        
        UpdateElements(_data.FarmableTrees, treeContainer, treeElements);
        UpdateElements(_data.FarmableAnimals, animalContainer, animalElements);
    }

    public void Hide()
    {
        // 요구사항에 따라 창을 끄지 않고 유지합니다. 
        // 필요하다면 여기서 텍스트를 비우는 등의 처리를 할 수 있지만, 
        // 항상 무언가 표시되길 원하시므로 아무것도 하지 않거나 가시성을 유지합니다.
    }

    public void OnShow() { gameObject.SetActive(true); }
    public void OnHide() { gameObject.SetActive(false); }

    private void UpdateElements(IReadOnlyList<FarmingItemData> _datas, Transform _container, List<UI_FarmingItemElement> _elements)
    {
        if (_container == null || farmingItemPrefab == null) return;

        while (_elements.Count < _datas.Count)
        {
            UI_FarmingItemElement element = Instantiate(farmingItemPrefab, _container);
            _elements.Add(element);
        }

        for (int i = 0; i < _elements.Count; i++)
        {
            if (i < _datas.Count)
            {
                _elements[i].gameObject.SetActive(true);
                _elements[i].Initialize(_datas[i]);
            }
            else
            {
                _elements[i].gameObject.SetActive(false);
            }
        }
    }
}
