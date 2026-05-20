using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomSortable : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Transform sortAnchor;
    [SerializeField] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>(4);

    // 내부 의존성 및 설정
    private SortingGroup sortingGroup;
    [SerializeField] private int offset;
    [SerializeField] private int precision = 100;
    protected float height = 0;

    public void Initialize(Transform _sortAnchor, SpriteRenderer[] _initialRenderers = null)
    {
        sortAnchor = _sortAnchor;
        
        spriteRenderers.Clear();
        sortingGroup = null;

        if (_initialRenderers != null)
        {
            AddSpriteRenderers(_initialRenderers);
        }
        
        if (spriteRenderers.Count == 0)
        {
            AddSpriteRenderers(GetComponentsInChildren<SpriteRenderer>(true));
        }
    }

    public void AddSpriteRenderer(SpriteRenderer _renderer)
    {
        if (_renderer == null) return;
        if (!spriteRenderers.Contains(_renderer))
        {
            spriteRenderers.Add(_renderer);
            
            // 등록된 SpriteRenderer로부터 SortingGroup을 자동으로 찾아 할당
            if (sortingGroup == null)
            {
                sortingGroup = _renderer.GetComponentInParent<SortingGroup>();
            }
        }
    }

    public void AddSpriteRenderers(SpriteRenderer[] _renderers)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            AddSpriteRenderer(_renderers[i]);
        }
    }

    public void SetHeight(float _height)
    {
        height = _height;
    }

    private void UpdateSortingOrder()
    {
        Transform anchor = sortAnchor != null ? sortAnchor : transform;

        // 아이소매트릭 2D 정렬 로직:
        // 지면의 Y 좌표가 낮을수록(앞에 있을수록) 정렬 순서가 커야 합니다.
        // height는 캐릭터가 공중에 떠 있는 높이를 의미하므로, 실제 정렬의 기준이 되는 지면 위치는 (현재 Y - height)입니다.
        float groundY = anchor.position.y - height;
        int order = -Mathf.RoundToInt(groundY * precision) + offset;

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
        }
        
        // SortingGroup이 있더라도 개별 SpriteRenderer들의 순서를 동기화해야 할 경우를 위해 별도 처리
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].sortingOrder = order;
            }
        }
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }
}
