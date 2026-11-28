using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomSortable : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Transform sortAnchor;
    [SerializeField] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>(4);

    // 내부 의존성 및 설정
    private static readonly List<SpriteRenderer> tempRenderers = new List<SpriteRenderer>(32);
    private SortingGroup sortingGroup;
    [SerializeField] private int offset;
    [SerializeField] private int precision = 100;
    protected float height = 0;
    
    // 켜져 있는 정렬기 전부. 지진 물결(QuakeObjectRide)이 그 위 물체를 찾을 때 FindObjectsByType 대신 훑는다.
    private static readonly HashSet<CustomSortable> enabledSortables = new HashSet<CustomSortable>();

    public int CurrentSortingOrder { get; private set; }

    /// <summary>
    /// 지면 위치(GroundPosition)가 달라질 수 있는 일이 생길 때마다 바뀌는 번호: 켜질 때(풀에서 다시 꺼내져 다른 자리에 놓일 수 있다),
    /// 공중 높이가 바뀔 때, 기준점이 바뀔 때. 제자리에 서 있는 물체(IsStaticGround)의 지면 위치를 기억해 두는 쪽(QuakeObjectRide)은
    /// 이 번호가 그대로인 동안만 기억한 값을 쓴다.
    /// </summary>
    public int GroundSerial { get; private set; }

    /// <summary>
    /// 켜져 있는 동안 자리를 옮기지 않는 물체(나무)인지. 이런 물체를 옮기는 코드는 껐다 켜거나(풀에 넣었다 꺼내기) 옮긴 뒤 끄기까지 한 번에 해야 한다 -
    /// 켜 둔 채로 다른 자리에 놓으면 GroundSerial이 그대로라 기억해 둔 자리가 어긋난다.
    /// </summary>
    public bool IsStaticGround { get; private set; }

    public void SetStaticGround(bool _bStatic) => IsStaticGround = _bStatic;

    /// <summary>정렬 기준이 되는 지면 위치(정렬 기준점에서 공중 높이를 뺀 자리). UpdateSortingOrder와 같은 기준이다.</summary>
    public Vector3 GroundPosition
    {
        get
        {
            Transform anchor = sortAnchor != null ? sortAnchor : transform;
            Vector3 position = anchor.position;
            position.y -= height;
            return position;
        }
    }

    /// <summary>켜져 있는 정렬기를 _into에 담는다(비우고 담는다).</summary>
    public static void CollectEnabled(List<CustomSortable> _into)
    {
        _into.Clear();
        // AddRange(HashSet)은 임시 배열을 새로 만들어 복사한다. 같은 순서(HashSet 열거 순서 = CopyTo 순서)로 직접 담는다.
        if (_into.Capacity < enabledSortables.Count) _into.Capacity = enabledSortables.Count;
        foreach (CustomSortable sortable in enabledSortables) _into.Add(sortable);
    }

    private void OnEnable()
    {
        enabledSortables.Add(this);
        unchecked { GroundSerial++; }
    }

    private void OnDisable() => enabledSortables.Remove(this);

    public void Initialize(Transform _sortAnchor, SpriteRenderer[] _initialRenderers = null, SortingGroup _sortingGroup = null)
    {
        sortAnchor = _sortAnchor;
        sortingGroup = _sortingGroup;
        unchecked { GroundSerial++; }
        
        spriteRenderers.Clear();

        if (_initialRenderers != null)
        {
            AddSpriteRenderers(_initialRenderers);
        }
        
        if (spriteRenderers.Count == 0)
        {
            spriteRenderers.AddRange(GetComponentsInChildren<SpriteRenderer>(true));
        }
    }

    public void SetSortingGroup(SortingGroup _sortingGroup)
    {
        sortingGroup = _sortingGroup;
    }

    public void AddSpriteRenderer(SpriteRenderer _renderer)
    {
        if (_renderer == null) return;

        // List.Contains는 칸마다 UnityEngine.Object.Equals(살아있는지 확인 후 같은 참조인지)로 비교한다.
        // _renderer는 위에서 살아있음을 확인했으므로 같은 참조인지만 보면 결과가 같다(파괴된 칸은 원래도 다르다고 봤다).
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (ReferenceEquals(spriteRenderers[i], _renderer)) return;
        }

        spriteRenderers.Add(_renderer);
    }

    public void AddSpriteRenderers(SpriteRenderer[] _renderers)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            AddSpriteRenderer(_renderers[i]);
        }
    }

    public void AddChildSpriteRenderers(GameObject _targetObject)
    {
        if (_targetObject == null) return;

        tempRenderers.Clear();
        _targetObject.GetComponentsInChildren<SpriteRenderer>(true, tempRenderers);

        for (int i = 0; i < tempRenderers.Count; i++)
        {
            AddSpriteRenderer(tempRenderers[i]);
        }

        tempRenderers.Clear();
    }

    public void SetHeight(float _height)
    {
        if (height != _height) unchecked { GroundSerial++; }
        height = _height;
    }

    /// <summary>
    /// UpdateSortingOrder와 동일한 규칙(precision/offset)을 이 오브젝트 자신이 아닌 임의의 월드 Y좌표에
    /// 적용해 정렬 순서를 계산한다. 발사 위치가 본체와 다른 이펙트(예: Muzzle에서 나가는 파티클)를
    /// 본체와 같은 정렬 규칙으로 맞추고 싶을 때 쓴다. height는 이 오브젝트의 공중 높이를 그대로
    /// 재사용하므로, 호출하는 쪽에서 이미 그 높이만큼 보정된 Y좌표를 넘겨야 한다.
    /// </summary>
    public int ComputeSortingOrder(float _worldY)
    {
        float groundY = _worldY - height;
        return -Mathf.RoundToInt(groundY * precision) + offset;
    }

    private void UpdateSortingOrder()
    {
        Transform anchor = sortAnchor != null ? sortAnchor : transform;

        // 아이소매트릭 2D 정렬 로직:
        // 지면의 Y 좌표가 낮을수록(앞에 있을수록) 정렬 순서가 커야 합니다.
        // height는 캐릭터가 공중에 떠 있는 높이를 의미하므로, 실제 정렬의 기준이 되는 지면 위치는 (현재 Y - height)입니다.
        float groundY = anchor.position.y - height;
        int order = -Mathf.RoundToInt(groundY * precision) + offset;
        CurrentSortingOrder = order;

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

    public void ManualLateUpdate()
    {
        UpdateSortingOrder();
    }
}
