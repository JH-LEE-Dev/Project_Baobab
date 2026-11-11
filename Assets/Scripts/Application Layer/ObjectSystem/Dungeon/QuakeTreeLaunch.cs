using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지진("흑요 부적")에 쓰러진 나무가 땅에 떠밀려 공중으로 튀어 오른 뒤 꼭대기에서 터지는 연출. 렌더링 전용이다.
///
/// 쓰러진 나무는 사망 처리(드랍·카운트·풀 반환)가 그 자리에서 그대로 끝난다. 이 클래스는 쓰러지는 순간의 나무 그림을 베낀
/// 대역(스프라이트 몇 장)만 띄워, 하얗게 번쩍이며 진원 반대쪽으로 기울어 솟구치게 하고, 꼭대기에 닿으면 대역을 숨기고 사망 이펙트를
/// 그 높이에서 터뜨린다(밑동 쪽 이펙트는 바닥 그대로). 그림자는 바닥에 남아 나무가 떠오른 게 보인다.
/// 게임 판정·정렬 값·나무 상태는 건드리지 않는다. 한꺼번에 띄울 수 있는 수를 넘으면 false를 돌려주고, 부른 쪽이 예전처럼 바로 터뜨린다.
/// </summary>
public sealed class QuakeTreeLaunch
{
    // 한꺼번에 떠 있을 수 있는 대역 수. 지진 한 번(III단계 범위)에 쓰러지는 나무 수보다 넉넉하다. 넘치면 바로 터진다.
    private const int MaxGhosts = 48;
    // 솟구쳐 꼭대기에 닿는 시간(초). 처음에 빠르고 꼭대기에서 느려진다(3차 감속).
    private const float RiseSeconds = 0.13f;
    // 맞는 순간 하얗게 번쩍였다가 이 시간(초) 동안 원래 색으로 돌아온다.
    private const float FlashSeconds = 0.07f;
    // 솟구치는 순간 세로로 늘어나고 가로로 좁아졌다가 꼭대기에서 원래 모양으로 돌아온다(비율).
    private const float LaunchStretch = 0.14f;
    private const float LaunchSquash = 0.09f;

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    private sealed class Ghost
    {
        public Transform root;
        public readonly List<SpriteRenderer> body = new List<SpriteRenderer>(6);
        public readonly List<SpriteRenderer> shadows = new List<SpriteRenderer>(2);
        public int bodyCount;
        public int shadowCount;
        public bool bActive;
        public float age;
        public float height;
        public float tiltDegrees;
        public Vector3 basePosition;
        public TreeDeadVfxPose pose;
        public TreeType treeType;
        public int gemStage;
    }

    private readonly InDungeonVFXManager vfxManager;
    private readonly Transform owner;
    private Transform container;
    private readonly List<Ghost> ghosts = new List<Ghost>(MaxGhosts);
    private int activeCount;
    private MaterialPropertyBlock block;

    private static readonly List<SpriteRenderer> bodyBuffer = new List<SpriteRenderer>(6);
    private static readonly List<SpriteRenderer> shadowBuffer = new List<SpriteRenderer>(2);

    public QuakeTreeLaunch(InDungeonVFXManager _vfxManager, Transform _owner)
    {
        vfxManager = _vfxManager;
        owner = _owner;
    }

    /// <summary>
    /// 지금 쓰러진 나무(_tree, 아직 풀로 돌아가기 전)를 _height(월드)만큼 솟구쳐 꼭대기에서 터지게 한다. _tiltDegrees만큼 기울며 오른다.
    /// 띄울 수 없으면(대역이 모자라거나 그릴 그림이 없으면) false - 부른 쪽이 사망 이펙트를 바로 터뜨린다.
    /// </summary>
    public bool TryLaunch(TreeObj _tree, float _height, float _tiltDegrees)
    {
        if (null == vfxManager || null == _tree || null == _tree.treeVisualComponent) return false;
        if (activeCount >= MaxGhosts) return false;

        TreeVisualComponent visual = _tree.treeVisualComponent;
        bodyBuffer.Clear();
        shadowBuffer.Clear();
        visual.CollectLaunchRenderers(bodyBuffer, shadowBuffer);
        if (0 == bodyBuffer.Count) return false;

        if (false == EnsureContainer()) return false;

        Ghost ghost = TakeGhost();
        ghost.bActive = true;
        ghost.age = 0f;
        ghost.height = Mathf.Max(0f, _height);
        ghost.tiltDegrees = _tiltDegrees;
        ghost.basePosition = _tree.transform.position;
        ghost.pose = TreeDeadVfxPose.From(visual);
        ghost.treeType = _tree.treeData.type;
        ghost.gemStage = _tree.gemStage;

        Transform root = ghost.root;
        root.SetPositionAndRotation(ghost.basePosition, Quaternion.identity);
        root.localScale = Vector3.one;
        root.gameObject.SetActive(true);

        block ??= new MaterialPropertyBlock();

        ghost.bodyCount = bodyBuffer.Count;
        for (int i = 0; i < bodyBuffer.Count; i++)
        {
            SpriteRenderer copy = GetRenderer(ghost.body, i, root, "Body");
            CopyRenderer(bodyBuffer[i], copy);
            // 맞는 순간 하얗게 번쩍인다(Tick에서 잦아든다).
            copy.GetPropertyBlock(block);
            block.SetColor(FlashColorId, Color.white);
            block.SetFloat(FlashAmountId, 1f);
            copy.SetPropertyBlock(block);
        }
        HideFrom(ghost.body, bodyBuffer.Count);

        ghost.shadowCount = shadowBuffer.Count;
        for (int i = 0; i < shadowBuffer.Count; i++)
        {
            // 그림자는 대역 루트가 아니라 묶음(바닥)에 붙여 솟구치지 않게 한다.
            SpriteRenderer copy = GetRenderer(ghost.shadows, i, container, "Shadow");
            CopyRenderer(shadowBuffer[i], copy);
        }
        HideFrom(ghost.shadows, shadowBuffer.Count);

        bodyBuffer.Clear();
        shadowBuffer.Clear();
        activeCount++;
        return true;
    }

    /// <summary>매 프레임(게임 시간). 솟구치는 대역을 움직이고, 꼭대기에 닿은 대역은 사망 이펙트를 터뜨리고 숨긴다.</summary>
    public void Tick(float _deltaTime)
    {
        if (0 == activeCount) return;

        for (int g = 0; g < ghosts.Count; g++)
        {
            Ghost ghost = ghosts[g];
            if (false == ghost.bActive) continue;

            if (null == ghost.root)
            {
                ghost.bActive = false;
                activeCount--;
                continue;
            }

            ghost.age += _deltaTime;
            if (ghost.age >= RiseSeconds)
            {
                Burst(ghost);
                continue;
            }

            float progress = ghost.age / RiseSeconds;
            float rest = 1f - progress;
            float eased = 1f - rest * rest * rest;

            Transform root = ghost.root;
            root.SetPositionAndRotation(ghost.basePosition + Vector3.up * (ghost.height * eased), Quaternion.Euler(0f, 0f, ghost.tiltDegrees * eased));
            root.localScale = new Vector3(1f - LaunchSquash * rest, 1f + LaunchStretch * rest, 1f);

            float flash = Mathf.Clamp01(1f - ghost.age / FlashSeconds);
            // 번쩍임이 다 잦아든 뒤에는 다시 쓰지 않는다(한 번은 0을 써서 끝낸다).
            if (ghost.age - _deltaTime < FlashSeconds)
            {
                for (int i = 0; i < ghost.bodyCount; i++)
                {
                    SpriteRenderer copy = ghost.body[i];
                    copy.GetPropertyBlock(block);
                    block.SetFloat(FlashAmountId, flash);
                    copy.SetPropertyBlock(block);
                }
            }
        }
    }

    /// <summary>떠 있는 대역을 터뜨리지 않고 모두 숨긴다(던전 정리 때).</summary>
    public void Clear()
    {
        for (int g = 0; g < ghosts.Count; g++)
        {
            Hide(ghosts[g]);
        }
        activeCount = 0;
    }

    // // 내부

    private void Burst(Ghost _ghost)
    {
        // 수관 쪽 이펙트는 솟구친 높이(기운 만큼 돌린 자리)에서, 밑동 쪽 이펙트는 바닥에서 터진다.
        TreeDeadVfxPose pose = _ghost.pose;
        Quaternion tilt = Quaternion.Euler(0f, 0f, _ghost.tiltDegrees);
        pose.topPosition = _ghost.basePosition + Vector3.up * _ghost.height + tilt * (pose.topPosition - _ghost.basePosition);

        Hide(_ghost);
        activeCount--;

        vfxManager.PlayTreeDeadVFX(pose, _ghost.treeType, _ghost.gemStage);
    }

    private static void Hide(Ghost _ghost)
    {
        _ghost.bActive = false;
        if (null != _ghost.root) _ghost.root.gameObject.SetActive(false);
        for (int i = 0; i < _ghost.shadows.Count; i++)
        {
            if (null != _ghost.shadows[i]) _ghost.shadows[i].enabled = false;
        }
    }

    private bool EnsureContainer()
    {
        if (null != container) return true;

        // 묶음이 사라졌으면(던전 매니저와 함께 파괴) 대역도 같이 사라졌으므로 처음부터 다시 만든다.
        ghosts.Clear();
        activeCount = 0;
        if (null == owner) return false;

        GameObject containerObject = new GameObject("[QuakeTreeLaunch]");
        container = containerObject.transform;
        container.SetParent(owner, false);
        container.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return true;
    }

    private Ghost TakeGhost()
    {
        for (int g = 0; g < ghosts.Count; g++)
        {
            if (false == ghosts[g].bActive && null != ghosts[g].root) return ghosts[g];
        }

        GameObject rootObject = new GameObject("QuakeTreeLaunchGhost");
        rootObject.transform.SetParent(container, false);
        Ghost ghost = new Ghost { root = rootObject.transform };
        ghosts.Add(ghost);
        return ghost;
    }

    private static SpriteRenderer GetRenderer(List<SpriteRenderer> _list, int _index, Transform _parent, string _name)
    {
        if (_index < _list.Count)
        {
            SpriteRenderer existing = _list[_index];
            existing.enabled = true;
            return existing;
        }

        GameObject child = new GameObject(_name);
        child.transform.SetParent(_parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        _list.Add(renderer);
        return renderer;
    }

    private static void HideFrom(List<SpriteRenderer> _list, int _start)
    {
        for (int i = _start; i < _list.Count; i++)
        {
            _list[i].enabled = false;
        }
    }

    /// <summary>원본과 같은 모습·자리·정렬로 베낀다. 원본의 속성 블록(깎인 금·열기 등)도 그대로 옮긴다.</summary>
    private void CopyRenderer(SpriteRenderer _source, SpriteRenderer _copy)
    {
        _copy.gameObject.layer = _source.gameObject.layer;
        _copy.sprite = _source.sprite;
        _copy.sharedMaterial = _source.sharedMaterial;
        _copy.color = _source.color;
        _copy.flipX = _source.flipX;
        _copy.flipY = _source.flipY;
        _copy.drawMode = _source.drawMode;
        if (SpriteDrawMode.Simple != _source.drawMode)
        {
            _copy.size = _source.size;
            _copy.tileMode = _source.tileMode;
        }
        _copy.maskInteraction = _source.maskInteraction;
        _copy.spriteSortPoint = _source.spriteSortPoint;
        _copy.sortingLayerID = _source.sortingLayerID;
        _copy.sortingOrder = _source.sortingOrder;
        _copy.renderingLayerMask = _source.renderingLayerMask;
        _copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _copy.receiveShadows = false;

        _source.GetPropertyBlock(block);
        _copy.SetPropertyBlock(block);

        Transform sourceTransform = _source.transform;
        Transform copyTransform = _copy.transform;
        copyTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        Vector3 parentScale = null != copyTransform.parent ? copyTransform.parent.lossyScale : Vector3.one;
        Vector3 scale = sourceTransform.lossyScale;
        copyTransform.localScale = new Vector3(SafeDivide(scale.x, parentScale.x), SafeDivide(scale.y, parentScale.y), SafeDivide(scale.z, parentScale.z));
    }

    private static float SafeDivide(float _value, float _by) => Mathf.Approximately(_by, 0f) ? _value : _value / _by;
}
