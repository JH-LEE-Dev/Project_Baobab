using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지진 물결 위의 물체(나무·원목·아이템·차량·캐릭터 등 바닥 기준으로 정렬되는 것)를 선 칸의 높이만큼 함께 출렁이게 보이게 한다.
/// 물체는 모두 타일보다 위 정렬 층에 그려져 타일 복제층과 같이 움직이지 않는다. 옮기지 않으면 바닥만 물체 밑으로 미끄러져 보인다.
///
/// [그리는 동안에만 옮긴다 - 게임은 원래 자리만 본다]
///   렌더 파이프라인이 화면을 그리기 직전(beginContextRendering)에 옮기고, 다 그린 직후(endContextRendering)에 원래 자리로 되돌린다.
///   Update·LateUpdate·물리·트윈 등 게임 코드는 언제나 원래 자리만 본다. 충돌·판정·카메라 추적이 바뀌지 않는다.
///   - 정렬 순서는 CustomSortable이 원래 자리로 미리 정해 둔 값이라 옮겨도 앞뒤가 그대로다.
///   - 충돌체(Collider2D·Rigidbody2D)가 달린 트랜스폼은 옮기지 않는다. 옮겼다 되돌려도 물리는 바뀐 것으로 보고 자세·모양을 다시 맞추고,
///     정적 충돌체는 모양을 다시 만들며 트리거 판정이 다시 날 수 있다. 충돌체가 달린 물체(캐릭터·차량)는 충돌체가 없는 자식만 옮긴다.
///   - 그림이 충돌체와 같은 트랜스폼에 있으면(차량 짐칸 상자) 원본은 그 자리에서 숨기고(forceRenderingOff) 같은 모습의 대역을 옮겨 그린다.
///     원본 렌더러의 켜짐·재질·정렬 값은 건드리지 않는다. 스프라이트 색(SpriteRenderer.color)은 렌더러 갱신 단계에서만 GPU로 넘어가
///     그리기 직전에 바꾸면 반영되지 않으므로, 대역의 색은 흰색 그대로 두고 원본 색을 대역 전용 재질의 _Color에 곱해 넣는다(재질 값은 그릴 때 읽힌다).
///   - 물체 안에서 정렬기가 여럿이면(차량: 차체·바퀴·짐칸 상자) 부분마다 제 지면 위치의 칸 높이를 따른다. 정렬기가 없는 부분(차량 그림자)은
///     가장 가까운 정렬기를 따른다 - 짐칸 상자 그림자는 상자와, 차량 그림자는 차체와 같이 움직인다.
///   - 물(용암) 칸은 출렁이지 않으므로 그 위의 물체도 그대로다.
/// 연출 중 새로 나타난 물체(쓰러진 나무의 원목 등)는 잠깐마다 켜진 정렬기 목록(CustomSortable.CollectEnabled)에서 찾아 더한다.
/// </summary>
public sealed class QuakeObjectRide
{
    private const float PixelsPerUnit = 32f;
    // 새로 나타난 물체를 찾는 간격(초). 켜진 정렬기를 전부 훑으므로 매 프레임 하지 않는다. 원목은 떨어지는 데 이보다 오래 걸린다.
    private const float CollectInterval = 0.1f;

    private sealed class Part
    {
        public Transform transform;     // 옮길 트랜스폼. 대역이면 원본 렌더러의 트랜스폼(옮기지 않는다)
        public SpriteRenderer source;   // 대역으로 그릴 원본(충돌체와 같은 트랜스폼의 그림). 없으면 transform을 옮긴다
        public CustomSortable sample;   // 칸을 고를 지면 위치. 없으면 transform 자리
        public SpriteRenderer standIn;  // source의 대역(source가 있을 때만)
        public Material standInMaterial; // 대역 전용 재질(원본 재질 값 + 원본 색). 원본 재질에 _Color가 없으면 비어 있다
    }

    private sealed class Rider
    {
        public Transform root;
        public Part[] parts;
    }

    private QuakeTileRipple ripple;
    private Rect area;
    private bool bActive;
    private bool bSubscribed;
    private bool bApplied;
    private float collectTimer;

    private readonly List<Rider> riders = new List<Rider>(512);
    private readonly HashSet<Transform> roots = new HashSet<Transform>();
    private readonly HashSet<CustomSortable> known = new HashSet<CustomSortable>();
    // 지금 옮겨 둔 트랜스폼과 옮기기 전 로컬 자리(되돌릴 때 그대로 쓴다).
    private readonly List<Transform> shifted = new List<Transform>(512);
    private readonly List<Vector3> savedLocal = new List<Vector3>(512);
    // 대역 렌더러 전부와, 지금 숨겨 둔 원본·드러낸 대역.
    private readonly List<SpriteRenderer> standIns = new List<SpriteRenderer>(4);
    private readonly List<Material> standInMaterials = new List<Material>(4);
    private readonly List<SpriteRenderer> hiddenSources = new List<SpriteRenderer>(4);
    private readonly List<SpriteRenderer> shownStandIns = new List<SpriteRenderer>(4);
    private MaterialPropertyBlock propertyBlock;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly List<Part> partBuffer = new List<Part>(16);
    private static readonly List<int> pixelBuffer = new List<int>(16);
    private static readonly List<CustomSortable> sortableBuffer = new List<CustomSortable>(1024);
    private static readonly List<CustomSortable> innerSortables = new List<CustomSortable>(8);

    /// <summary>물결 시작. _area(월드) 안의 물체를 모아 그릴 때마다 _ripple의 칸 높이만큼 옮긴다.</summary>
    public void Begin(QuakeTileRipple _ripple, Rect _area)
    {
        Stop();

        ripple = _ripple;
        area = _area;
        bActive = true;
        collectTimer = 0f;
        Collect();
        Subscribe(true);
    }

    /// <summary>물결이 도는 동안 매 프레임(게임 시간). 새로 나타난 물체를 잠깐마다 더한다.</summary>
    public void Tick(float _deltaTime)
    {
        if (false == bActive) return;

        // 그리던 중 예외로 되돌리기를 못 했으면 게임 코드가 돌기 전에 되돌린다.
        Restore();

        collectTimer += _deltaTime;
        if (collectTimer < CollectInterval) return;

        collectTimer = 0f;
        Collect();
    }

    /// <summary>끝낸다. 옮겨 둔 것을 되돌리고 그리기 콜백을 뗀다. 대역은 지운다.</summary>
    public void Stop()
    {
        Restore();
        Subscribe(false);

        bActive = false;
        ripple = null;
        riders.Clear();
        roots.Clear();
        known.Clear();

        for (int i = 0; i < standIns.Count; i++)
        {
            if (null != standIns[i]) DestroyObject(standIns[i].gameObject);
        }
        standIns.Clear();

        for (int i = 0; i < standInMaterials.Count; i++)
        {
            if (null != standInMaterials[i]) DestroyObject(standInMaterials[i]);
        }
        standInMaterials.Clear();
    }

    // // 내부

    private void Subscribe(bool _bOn)
    {
        if (_bOn == bSubscribed) return;

        bSubscribed = _bOn;
        if (_bOn)
        {
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            RenderPipelineManager.endContextRendering += OnEndRendering;
        }
        else
        {
            RenderPipelineManager.beginContextRendering -= OnBeginRendering;
            RenderPipelineManager.endContextRendering -= OnEndRendering;
        }
    }

    private void OnBeginRendering(ScriptableRenderContext _context, List<Camera> _cameras) => Apply();

    private void OnEndRendering(ScriptableRenderContext _context, List<Camera> _cameras) => Restore();

    /// <summary>영역 안에서 켜져 있는, 아직 모르는 정렬기를 더한다.</summary>
    private void Collect()
    {
        CustomSortable.CollectEnabled(sortableBuffer);
        for (int i = 0; i < sortableBuffer.Count; i++)
        {
            CustomSortable sortable = sortableBuffer[i];
            if (null == sortable || known.Contains(sortable)) continue;
            if (false == area.Contains(sortable.transform.position)) continue;

            known.Add(sortable);

            // 차량은 부품(차체·바퀴·짐칸 상자·수리 상자)마다 정렬기가 있고 그림자는 차량 바로 아래에 있다. 차량 전체를 한 물체로 모은다.
            OffroadVehicleObj vehicle = sortable.GetComponentInParent<OffroadVehicleObj>();
            Transform root = null != vehicle ? vehicle.transform : sortable.transform;
            if (roots.Contains(root) || HasTrackedAncestor(root)) continue;

            partBuffer.Clear();
            CollectParts(root, null, partBuffer);
            if (partBuffer.Count == 0) continue;

            SampleNearest(root, partBuffer);
            for (int p = 0; p < partBuffer.Count; p++)
            {
                Part part = partBuffer[p];
                if (null == part.source) continue;

                part.standIn = CreateStandIn();
                standIns.Add(part.standIn);
            }

            roots.Add(root);
            riders.Add(new Rider { root = root, parts = partBuffer.ToArray() });
        }

        sortableBuffer.Clear();
        partBuffer.Clear();
    }

    private bool HasTrackedAncestor(Transform _transform)
    {
        for (Transform parent = _transform.parent; null != parent; parent = parent.parent)
        {
            if (roots.Contains(parent)) return true;
        }
        return false;
    }

    /// <summary>
    /// _transform 아래가 한 덩어리(충돌체 없음·정렬기 하나 이하)면 통째로 담는다. 아니면 자식들로 나눠 담고,
    /// 이 트랜스폼 자신의 그림은 대역으로 담는다. 그림이 없는 부분은 뺀다. _inherited는 가장 가까운 위쪽 정렬기다.
    /// </summary>
    private static void CollectParts(Transform _transform, CustomSortable _inherited, List<Part> _into)
    {
        CustomSortable own = _transform.GetComponent<CustomSortable>();
        if (null != own) _inherited = own;

        _transform.GetComponentsInChildren(true, innerSortables);
        int sortableCount = innerSortables.Count;
        CustomSortable single = sortableCount == 1 ? innerSortables[0] : null;
        innerSortables.Clear();

        bool bPhysics = null != _transform.GetComponentInChildren<Collider2D>(true) || null != _transform.GetComponentInChildren<Rigidbody2D>(true);
        if (false == bPhysics && sortableCount <= 1)
        {
            if (null != _transform.GetComponentInChildren<Renderer>(true))
            {
                _into.Add(new Part { transform = _transform, sample = null != _inherited ? _inherited : single });
            }
            return;
        }

        // 자신의 그림(정렬 그룹 밖의 스프라이트만 - 대역은 정렬 그룹에 들어가지 못한다).
        SpriteRenderer sprite = _transform.GetComponent<SpriteRenderer>();
        if (null != sprite && null == sprite.GetComponentInParent<SortingGroup>(true))
        {
            _into.Add(new Part { transform = _transform, source = sprite, sample = _inherited });
        }

        for (int c = 0; c < _transform.childCount; c++)
        {
            CollectParts(_transform.GetChild(c), _inherited, _into);
        }
    }

    /// <summary>정렬기를 못 정한 부분(차량 그림자 등)은 물체 안에서 가장 가까운 정렬기를 따른다.</summary>
    private static void SampleNearest(Transform _root, List<Part> _parts)
    {
        bool bMissing = false;
        for (int p = 0; p < _parts.Count; p++) bMissing |= null == _parts[p].sample;
        if (false == bMissing) return;

        _root.GetComponentsInChildren(true, innerSortables);
        if (innerSortables.Count > 0)
        {
            for (int p = 0; p < _parts.Count; p++)
            {
                Part part = _parts[p];
                if (null != part.sample) continue;

                Vector3 position = part.transform.position;
                float best = float.MaxValue;
                for (int s = 0; s < innerSortables.Count; s++)
                {
                    float distance = (innerSortables[s].GroundPosition - position).sqrMagnitude;
                    if (distance >= best) continue;

                    best = distance;
                    part.sample = innerSortables[s];
                }
            }
        }
        innerSortables.Clear();
    }

    /// <summary>대역은 평소 숨겨 둔다(forceRenderingOff). 색은 흰색 그대로 둔다 - 원본 색은 재질로 넣는다.</summary>
    private static SpriteRenderer CreateStandIn()
    {
        GameObject standIn = new GameObject("QuakeRideStandIn");
        SpriteRenderer renderer = standIn.AddComponent<SpriteRenderer>();
        renderer.forceRenderingOff = true;
        return renderer;
    }

    private static void DestroyObject(UnityEngine.Object _object)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
        else UnityEngine.Object.DestroyImmediate(_object);
    }

    /// <summary>대역의 겉모습을 원본에 맞춘다(그리기 직전). 원본 색은 재질(또는 속성 블록)의 _Color에 곱해 넣는다.</summary>
    private void CopyLook(Part _part)
    {
        SpriteRenderer source = _part.source;
        SpriteRenderer standIn = _part.standIn;
        standIn.gameObject.layer = source.gameObject.layer;
        standIn.sprite = source.sprite;
        standIn.flipX = source.flipX;
        standIn.flipY = source.flipY;
        standIn.drawMode = source.drawMode;
        if (SpriteDrawMode.Simple != source.drawMode)
        {
            standIn.size = source.size;
            standIn.tileMode = source.tileMode;
        }
        standIn.maskInteraction = source.maskInteraction;
        standIn.spriteSortPoint = source.spriteSortPoint;
        standIn.sortingLayerID = source.sortingLayerID;
        standIn.sortingOrder = source.sortingOrder;
        standIn.renderingLayerMask = source.renderingLayerMask;

        propertyBlock ??= new MaterialPropertyBlock();
        source.GetPropertyBlock(propertyBlock);

        Material material = source.sharedMaterial;
        Color tint = source.color;
        if (null == material || false == material.HasProperty(ColorId))
        {
            // _Color가 없는 셰이더 - 색을 곱할 곳이 없어 원본 재질 그대로 그린다(색은 흰색).
            standIn.sharedMaterial = material;
        }
        else if (propertyBlock.HasColor(ColorId))
        {
            standIn.sharedMaterial = material;
            propertyBlock.SetColor(ColorId, propertyBlock.GetColor(ColorId) * tint);
        }
        else
        {
            if (null == _part.standInMaterial)
            {
                _part.standInMaterial = new Material(material) { name = "QuakeRideStandIn", hideFlags = HideFlags.DontSave };
                standInMaterials.Add(_part.standInMaterial);
            }
            else if (_part.standInMaterial.shader != material.shader)
            {
                _part.standInMaterial.shader = material.shader;
            }

            _part.standInMaterial.CopyPropertiesFromMaterial(material);
            _part.standInMaterial.SetColor(ColorId, material.GetColor(ColorId) * tint);
            standIn.sharedMaterial = _part.standInMaterial;
        }

        standIn.SetPropertyBlock(propertyBlock);
    }

    private void Apply()
    {
        if (bApplied || false == bActive || null == ripple) return;
        bApplied = true;

        for (int r = 0; r < riders.Count; r++)
        {
            Rider rider = riders[r];
            if (null == rider.root || false == rider.root.gameObject.activeInHierarchy) continue;

            // 높이를 모두 읽은 뒤에 옮긴다. 먼저 옮긴 부분의 정렬기를 다른 부분이 따르면 옮긴 자리로 칸을 고르게 된다.
            pixelBuffer.Clear();
            for (int p = 0; p < rider.parts.Length; p++)
            {
                Part part = rider.parts[p];
                int pixels = 0;
                if (null != part.transform && part.transform.gameObject.activeInHierarchy)
                {
                    pixels = ripple.PixelsAt(null != part.sample ? part.sample.GroundPosition : part.transform.position);
                }
                pixelBuffer.Add(pixels);
            }

            for (int p = 0; p < rider.parts.Length; p++)
            {
                if (0 == pixelBuffer[p]) continue;

                Part part = rider.parts[p];
                Vector3 offset = new Vector3(0f, pixelBuffer[p] / PixelsPerUnit, 0f);
                if (null != part.source)
                {
                    DrawStandIn(part, offset);
                    continue;
                }

                shifted.Add(part.transform);
                savedLocal.Add(part.transform.localPosition);
                part.transform.position += offset;
            }
        }
    }

    /// <summary>원본을 그 자리에서 숨기고, 같은 모습의 대역을 _offset만큼 옮겨 드러낸다.</summary>
    private void DrawStandIn(Part _part, Vector3 _offset)
    {
        SpriteRenderer source = _part.source;
        SpriteRenderer standIn = _part.standIn;
        if (null == source || null == standIn) return;
        if (false == source.enabled || source.forceRenderingOff || null == source.sprite) return;

        CopyLook(_part);
        Transform sourceTransform = source.transform;
        standIn.transform.SetPositionAndRotation(sourceTransform.position + _offset, sourceTransform.rotation);
        standIn.transform.localScale = sourceTransform.lossyScale;

        standIn.forceRenderingOff = false;
        shownStandIns.Add(standIn);
        source.forceRenderingOff = true;
        hiddenSources.Add(source);
    }

    private void Restore()
    {
        if (false == bApplied) return;
        bApplied = false;

        for (int i = 0; i < shifted.Count; i++)
        {
            if (null != shifted[i]) shifted[i].localPosition = savedLocal[i];
        }

        for (int i = 0; i < hiddenSources.Count; i++)
        {
            if (null != hiddenSources[i]) hiddenSources[i].forceRenderingOff = false;
        }

        for (int i = 0; i < shownStandIns.Count; i++)
        {
            if (null != shownStandIns[i]) shownStandIns[i].forceRenderingOff = true;
        }

        shifted.Clear();
        savedLocal.Clear();
        hiddenSources.Clear();
        shownStandIns.Clear();
    }
}
