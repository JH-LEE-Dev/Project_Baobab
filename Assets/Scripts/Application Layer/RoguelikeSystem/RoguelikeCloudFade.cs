using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 섬 둘레 물에 비친 구름을 파도 선(RoguelikeWaveFront)에 맞춰 바꾼다. 구름은 환경 오브젝트 매니저가 맵을 깔 때마다 통째로 다시 깔아
/// (EnvironmentSystem.SpawnRoguelikeEnvironmentObjs) 그대로 두면 파도가 오기 전에 한꺼번에 사라지고 생긴다.
///
/// 티어 전환: 다시 깔기 직전에 보이는 구름의 그림만 떠서(복제 스프라이트, 같은 재질·불투명도) 남겨 두고, 파도 선이 닿으면 서서히 지운다.
///            새로 깐 구름은 투명하게 두었다가 파도 선이 닿으면 서서히 드러낸다. 마그마 숲처럼 구름이 없는 맵이면 지우기만 한다.
/// 진입: 직전 구름은 암전 아래에서 바로 사라지고, 새 구름만 파도를 따라 드러난다.
/// 구름은 흐르므로 칸이 아니라 지금 위치로 파도 거리를 잰다.
/// </summary>
public sealed class RoguelikeCloudFade
{
    private struct Ghost
    {
        public SpriteRenderer renderer;
        public float startAlpha;
        public float speed;
        public float elapsed;
        public bool bFading;
    }

    private struct Incoming
    {
        public EnvironmentObj cloud;
        public SpriteRenderer renderer;
        public float elapsed;
        public bool bFading;
    }

    private static readonly int alphaPropertyId = Shader.PropertyToID("_Alpha");

    private EnvironmentSystem environmentSystem;
    private Transform root;
    private float fadeSeconds = 1f;
    private float targetAlpha;

    private readonly List<Ghost> ghosts = new List<Ghost>(64);
    private readonly List<SpriteRenderer> ghostPool = new List<SpriteRenderer>(64);
    private readonly List<Incoming> incoming = new List<Incoming>(64);
    private readonly RoguelikeWaveFront front = new RoguelikeWaveFront();
    private MaterialPropertyBlock propertyBlock;

    public void Initialize(EnvironmentSystem _environmentSystem, Transform _parent)
    {
        environmentSystem = _environmentSystem;

        if (null == root)
        {
            root = new GameObject("RoguelikeCloudFade").transform;
            root.SetParent(_parent, false);
        }
    }

    /// <summary>구름을 다시 깔기 직전(티어 전환)에 부른다. 지금 화면에 보이는 구름의 그림을 떠서 남겨 둔다.</summary>
    public void CaptureBefore()
    {
        Flush();

        IReadOnlyList<EnvironmentObj> objs = environmentSystem.environmentObjManager.AllSpawnedObjs;
        for (int i = 0; i < objs.Count; i++)
        {
            if (false == objs[i] is Cloud cloud || false == cloud.bActivated) continue;

            SpriteRenderer source = cloud.GetComponentInChildren<SpriteRenderer>();
            if (null == source || false == source.enabled || null == source.sprite) continue;

            float alpha = ReadAlpha(source);
            if (alpha <= 0f) continue;

            SpriteRenderer copy = GetGhostRenderer();
            Transform copyTransform = copy.transform;
            copyTransform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            copyTransform.localScale = source.transform.lossyScale;

            copy.sprite = source.sprite;
            copy.color = source.color;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.sharedMaterial = source.sharedMaterial;
            copy.sortingLayerID = source.sortingLayerID;
            // 같은 자리의 새 구름이 먼저 그려지게 한 칸 뒤로 둔다(구름끼리는 스텐실로 먼저 그린 쪽만 남는다).
            copy.sortingOrder = source.sortingOrder + 1;
            WriteAlpha(copy, alpha);
            copy.gameObject.SetActive(true);

            ghosts.Add(new Ghost { renderer = copy, startAlpha = alpha, speed = cloud.MoveSpeed });
        }
    }

    /// <summary>
    /// 구름을 다시 깔고 파도 시작 상태를 준비한 직후(RoguelikeTileWave.PrepareAfterGenerate 뒤)에 부른다.
    /// 새 구름을 투명하게 두고 파도를 기다린다. _targetAlpha는 다 드러났을 때의 불투명도, _fadeSeconds는 한 구름이 드러나고 지워지는 시간.
    /// </summary>
    public void PrepareAfterSpawn(float _targetAlpha, float _fadeSeconds)
    {
        incoming.Clear();
        targetAlpha = _targetAlpha;
        fadeSeconds = Mathf.Max(0.01f, _fadeSeconds);
        front.Reset();

        IReadOnlyList<EnvironmentObj> objs = environmentSystem.environmentObjManager.AllSpawnedObjs;
        for (int i = 0; i < objs.Count; i++)
        {
            if (false == objs[i] is Cloud cloud) continue;

            SpriteRenderer renderer = cloud.GetComponentInChildren<SpriteRenderer>(true);
            if (null == renderer) continue;

            WriteAlpha(renderer, 0f);
            incoming.Add(new Incoming { cloud = cloud, renderer = renderer });
        }
    }

    /// <summary>매 프레임(게임 시간). 파도 선이 닿은 구름을 서서히 지우고 드러낸다. 남은 구름은 원래 속도로 흐른다.</summary>
    public void Tick(RoguelikeTileWave _wave, float _deltaTime)
    {
        if (0 == ghosts.Count && 0 == incoming.Count) return;

        for (int i = 0; i < ghosts.Count; i++)
        {
            Ghost ghost = ghosts[i];
            ghost.renderer.transform.position += new Vector3(ghost.speed * _deltaTime, 0f, 0f);
            ghosts[i] = ghost;
        }

        bool bFrontReady = front.Advance(_wave, _deltaTime);

        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = ghosts[i];
            if (false == ghost.bFading)
            {
                if (false == bFrontReady || _wave.GetWaveDistance(ghost.renderer.transform.position) > front.Distance) continue;
                ghost.bFading = true;
            }

            ghost.elapsed += _deltaTime;
            float t = Mathf.Clamp01(ghost.elapsed / fadeSeconds);
            if (t >= 1f)
            {
                ReleaseGhost(ghost.renderer);
                ghosts.RemoveAt(i);
                continue;
            }

            WriteAlpha(ghost.renderer, ghost.startAlpha * (1f - Mathf.SmoothStep(0f, 1f, t)));
            ghosts[i] = ghost;
        }

        for (int i = incoming.Count - 1; i >= 0; i--)
        {
            Incoming cloud = incoming[i];
            if (null == cloud.renderer)
            {
                incoming.RemoveAt(i);
                continue;
            }

            if (false == cloud.bFading)
            {
                if (false == bFrontReady || _wave.GetWaveDistance(cloud.cloud.GetCurrentPosition()) > front.Distance) continue;
                cloud.bFading = true;
            }

            cloud.elapsed += _deltaTime;
            float t = Mathf.Clamp01(cloud.elapsed / fadeSeconds);
            WriteAlpha(cloud.renderer, targetAlpha * Mathf.SmoothStep(0f, 1f, t));

            if (t >= 1f)
            {
                incoming.RemoveAt(i);
                continue;
            }
            incoming[i] = cloud;
        }
    }

    /// <summary>흐름이 끊기거나 구름을 다시 깔기 전. 남은 그림자 구름은 지우고 새 구름은 다 드러낸다.</summary>
    public void Flush()
    {
        for (int i = 0; i < ghosts.Count; i++) ReleaseGhost(ghosts[i].renderer);
        ghosts.Clear();

        for (int i = 0; i < incoming.Count; i++)
        {
            if (null != incoming[i].renderer) WriteAlpha(incoming[i].renderer, targetAlpha);
        }
        incoming.Clear();
    }

    // // 내부

    private SpriteRenderer GetGhostRenderer()
    {
        int last = ghostPool.Count - 1;
        if (last >= 0)
        {
            SpriteRenderer pooled = ghostPool[last];
            ghostPool.RemoveAt(last);
            return pooled;
        }

        GameObject go = new GameObject("CloudFadeGhost");
        go.transform.SetParent(root, false);
        return go.AddComponent<SpriteRenderer>();
    }

    private void ReleaseGhost(SpriteRenderer _renderer)
    {
        if (null == _renderer) return;

        _renderer.gameObject.SetActive(false);
        ghostPool.Add(_renderer);
    }

    private float ReadAlpha(SpriteRenderer _renderer)
    {
        if (null == propertyBlock) propertyBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(propertyBlock);
        if (propertyBlock.HasFloat(alphaPropertyId)) return propertyBlock.GetFloat(alphaPropertyId);

        Material material = _renderer.sharedMaterial;
        return null != material && material.HasProperty(alphaPropertyId) ? material.GetFloat(alphaPropertyId) : 1f;
    }

    private void WriteAlpha(SpriteRenderer _renderer, float _alpha)
    {
        if (null == propertyBlock) propertyBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(alphaPropertyId, _alpha);
        _renderer.SetPropertyBlock(propertyBlock);
    }
}
