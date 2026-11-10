using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 물(용암)·물가·스텐실 타일 애니메이션을 타일맵 대신 셰이더로 돌린다(Include/TileFrameAnimation.hlsl).
/// 애니메이션 타일(AnimatedTile)을 첫 프레임 그림 하나짜리 타일(Tile)로 바꿔 깔고, 그 타일맵 머티리얼을 켜짐 표시를 넣은 사본으로 바꾼다.
/// 셰이더는 지금 프레임 그림이 첫 프레임 그림에서 텍스처 칸 몇 개 뒤인지만큼 UV를 옮겨 읽으므로 보이는 그림·박자는 같다.
/// 타일맵이 매 프레임 화면 밖까지 모든 애니메이션 타일을 훑던 비용(Tilemap.Update)이 없어진다.
/// 한 층이라도 조건(같은 바둑판 텍스처·같은 프레임 순서와 속도·시작 시간/프레임/플래그 없음·사각형 그림 등)에 안 맞으면
/// 아무것도 바꾸지 않고 원래 타일을 그대로 깐다. 꺼진 곳·로그라이크에서도 바꾸지 않는다. 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// </summary>
public sealed class StageTileFrameAnimation
{
    public struct Layer
    {
        public Tilemap tilemap;
        // 이 층에 깔 타일 배열(바꾸기 전 원본)
        public TileBase[] tiles;
        // 이 층에 따로 더 깔 수 있는 타일(맵 밖 바다 기본 타일 등)
        public TileBase extra;
    }

    private const int MaxFrames = 8;

    private static readonly int TileFrameGridID = Shader.PropertyToID("_TileFrameGrid");
    private static readonly int TileAnimTimeID = Shader.PropertyToID("_TileAnimTime");
    private static readonly int TileAnimParamsID = Shader.PropertyToID("_TileAnimParams");
    private static readonly int TileAnimSeqAID = Shader.PropertyToID("_TileAnimSeqA");
    private static readonly int TileAnimSeqBID = Shader.PropertyToID("_TileAnimSeqB");

    // 원본 머티리얼·바둑판 크기별 켜짐 사본(던전에 다시 들어와도 다시 만들지 않는다)
    private static readonly Dictionary<(Material, int, int), Material> animatedMaterials = new Dictionary<(Material, int, int), Material>();

    private struct Swap
    {
        public TilemapRenderer renderer;
        public Material original;
        public Material animated;
    }

    private struct LayerInfo
    {
        public bool bAnimated;
        public int columns;
        public int rows;
    }

    // // 내부 의존성
    // 애니메이션 타일 → 첫 프레임 그림 하나짜리 타일(던전에 다시 들어와도 다시 만들지 않는다)
    private readonly Dictionary<TileBase, TileBase> stillTiles = new Dictionary<TileBase, TileBase>();
    private readonly List<Swap> swaps = new List<Swap>(4);
    private readonly HashSet<TileBase> visited = new HashSet<TileBase>();
    private readonly float[] steps = new float[MaxFrames];
    private readonly float[] layerSteps = new float[MaxFrames];
    private GameObject clock;
    private bool bActive;

    public bool IsActive => bActive;

    // // 퍼블릭 제어 메서드

    /// <summary>
    /// 이번에 깔 타일들을 살펴 셰이더 애니메이션으로 바꿀 수 있으면 켠다. 먼저 이전 것을 되돌린다(End).
    /// 켜면 true. 그 뒤 타일을 깔 때 Convert로 바꾼 배열을 쓴다.
    /// </summary>
    public bool Begin(bool _enabled, string _sceneName, Layer[] _layers)
    {
        End();

        if (false == _enabled || GameModeContext.IsRoguelike || null == _layers || _layers.Length == 0)
        {
            return false;
        }

        LayerInfo[] infos = new LayerInfo[_layers.Length];
        float fps = -1f;
        int frameCount = -1;
        bool bAny = false;

        for (int l = 0; l < _layers.Length; l++)
        {
            if (false == InspectLayer(_layers[l], ref infos[l], ref fps, ref frameCount))
            {
                return false;
            }

            bAny |= infos[l].bAnimated;
        }

        if (false == bAny || fps <= 0f || frameCount < 2)
        {
            return false;
        }

        // 바꿀 층 머티리얼을 켜짐 사본으로 바꾼다. 렌더러나 머티리얼이 없는 층이 있으면 전부 그만둔다.
        for (int l = 0; l < _layers.Length; l++)
        {
            if (false == infos[l].bAnimated) continue;

            TilemapRenderer renderer = _layers[l].tilemap.GetComponent<TilemapRenderer>();
            Material original = null != renderer ? renderer.sharedMaterial : null;
            if (null == original || false == original.HasProperty(TileFrameGridID))
            {
                RestoreMaterials();
                return false;
            }

            Material animated = AnimatedMaterial(original, infos[l].columns, infos[l].rows);
            renderer.sharedMaterial = animated;
            swaps.Add(new Swap { renderer = renderer, original = original, animated = animated });
        }

        Shader.SetGlobalVector(TileAnimParamsID, new Vector4(fps, frameCount, 0f, 0f));
        Shader.SetGlobalVector(TileAnimSeqAID, new Vector4(steps[0], steps[1], steps[2], steps[3]));
        Shader.SetGlobalVector(TileAnimSeqBID, new Vector4(steps[4], steps[5], steps[6], steps[7]));
        Shader.SetGlobalFloat(TileAnimTimeID, 0f);

        // 타일 시계: 타일맵 애니메이션처럼 타일을 깐 때부터 게임 시간(시간 배율 적용)으로 흐른다.
        clock = new GameObject("[StageTileFrameAnimation]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && clock.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clock, targetScene);
        }

        clock.AddComponent<StageTileFrameAnimationClock>();

        bActive = true;
        return true;
    }

    /// <summary>
    /// 켜져 있으면 애니메이션 타일을 첫 프레임 그림 하나짜리 타일로 바꾼 배열을 _buffer에 담아 돌려준다. 꺼져 있으면 _tiles를 그대로 돌려준다.
    /// _tiles는 바꾸지 않는다.
    /// </summary>
    public TileBase[] Convert(TileBase[] _tiles, ref TileBase[] _buffer)
    {
        if (false == bActive || null == _tiles) return _tiles;

        if (null == _buffer || _buffer.Length != _tiles.Length)
        {
            _buffer = new TileBase[_tiles.Length];
        }

        // 칸이 많아(맵 안팎 수만 칸) UnityEngine.Object의 ==(엔진 쪽 생존 확인)를 쓰지 않고 참조만 비교한다.
        // 깔 타일 배열에 든 타일은 에셋이거나 여기서 만든 것이라 도중에 파괴되지 않는다.
        TileBase lastSource = null;
        TileBase lastResult = null;
        for (int i = 0; i < _tiles.Length; i++)
        {
            TileBase tile = _tiles[i];
            if (ReferenceEquals(null, tile))
            {
                _buffer[i] = null;
                continue;
            }

            if (false == ReferenceEquals(tile, lastSource))
            {
                lastSource = tile;
                lastResult = stillTiles.TryGetValue(tile, out TileBase still) ? still : tile;
            }

            _buffer[i] = lastResult;
        }

        return _buffer;
    }

    /// <summary>바꿨던 머티리얼을 되돌리고 시계를 지운다.</summary>
    public void End()
    {
        RestoreMaterials();

        if (null != clock)
        {
            Object.Destroy(clock);
            clock = null;
        }

        if (bActive)
        {
            Shader.SetGlobalVector(TileAnimParamsID, Vector4.zero);
            Shader.SetGlobalFloat(TileAnimTimeID, 0f);
        }

        bActive = false;
    }

    /// <summary>_material에 셰이더 타일 애니메이션 켜짐 표시가 있는지.</summary>
    public static bool HasFrameAnimation(Material _material)
    {
        return null != _material && _material.HasProperty(TileFrameGridID) && _material.GetVector(TileFrameGridID).x >= 0.5f;
    }

    /// <summary>
    /// 물 머티리얼을 복사해 만든 사본(_material)의 켜짐 표시를 끈다. 표시가 없는 셰이더거나 이미 꺼져 있으면 아무것도 바뀌지 않는다.
    /// </summary>
    public static void ClearFrameAnimation(Material _material)
    {
        if (HasFrameAnimation(_material))
        {
            _material.SetVector(TileFrameGridID, Vector4.zero);
        }
    }

    // // 내부 메서드

    private void RestoreMaterials()
    {
        for (int i = 0; i < swaps.Count; i++)
        {
            Swap swap = swaps[i];
            // 그 사이 다른 곳이 머티리얼을 바꿨으면(스테이지별 스텐실 머티리얼 등) 그대로 둔다.
            if (null != swap.renderer && swap.renderer.sharedMaterial == swap.animated)
            {
                swap.renderer.sharedMaterial = swap.original;
            }
        }

        swaps.Clear();
    }

    private static Material AnimatedMaterial(Material _original, int _columns, int _rows)
    {
        (Material, int, int) key = (_original, _columns, _rows);
        if (false == animatedMaterials.TryGetValue(key, out Material animated) || null == animated)
        {
            animated = new Material(_original) { name = _original.name + " (Frame Animation)" };
            animatedMaterials[key] = animated;
        }
        else
        {
            animated.CopyPropertiesFromMaterial(_original);
        }

        animated.SetVector(TileFrameGridID, new Vector4(1f, _columns, _rows, 0f));
        return animated;
    }

    // 층 하나의 타일을 살핀다. 애니메이션 타일이 하나라도 있으면 그 층의 모든 타일이 조건에 맞는 애니메이션 타일이어야 한다.
    private bool InspectLayer(Layer _layer, ref LayerInfo _info, ref float _fps, ref int _frameCount)
    {
        _info = new LayerInfo { bAnimated = false, columns = 0, rows = 0 };
        if (null == _layer.tilemap) return true;

        visited.Clear();
        bool bHasStill = false;
        TileBase last = null;

        int count = null != _layer.tiles ? _layer.tiles.Length : 0;
        for (int i = -1; i < count; i++)
        {
            // Convert와 같이 참조만 비교한다(칸이 많다).
            TileBase tile = i < 0 ? _layer.extra : _layer.tiles[i];
            if (ReferenceEquals(null, tile) || ReferenceEquals(tile, last)) continue;

            last = tile;
            if (false == visited.Add(tile)) continue;

            if (tile.GetType() != typeof(AnimatedTile))
            {
                bHasStill = true;
                continue;
            }

            if (false == InspectAnimatedTile((AnimatedTile)tile, _layer.tilemap, ref _info, ref _fps, ref _frameCount))
            {
                return false;
            }
        }

        // 애니메이션 타일과 다른 타일이 섞인 층은 머티리얼 하나로 나눠 그릴 수 없다.
        if (_info.bAnimated && bHasStill) return false;

        return true;
    }

    private bool InspectAnimatedTile(AnimatedTile _tile, Tilemap _tilemap, ref LayerInfo _info, ref float _fps, ref int _frameCount)
    {
        Sprite[] frames = _tile.m_AnimatedSprites;
        if (null == frames || frames.Length < 2 || frames.Length > MaxFrames) return false;
        if (_tile.m_MinSpeed <= 0f || false == Mathf.Approximately(_tile.m_MinSpeed, _tile.m_MaxSpeed)) return false;
        if (0f != _tile.m_AnimationStartTime || 0 != _tile.m_AnimationStartFrame || 0 != (int)_tile.m_TileAnimationFlags) return false;

        Sprite first = frames[0];
        if (null == first || null == first.texture || first.packed) return false;

        Texture2D texture = first.texture;
        Rect firstRect = first.rect;
        float cellWidth = firstRect.width;
        float cellHeight = firstRect.height;
        if (cellWidth < 1f || cellHeight < 1f) return false;
        if (false == IsWhole(texture.width / cellWidth) || false == IsWhole(texture.height / cellHeight)) return false;

        int columns = Mathf.RoundToInt(texture.width / cellWidth);
        int rows = Mathf.RoundToInt(texture.height / cellHeight);
        if (_info.bAnimated && (_info.columns != columns || _info.rows != rows)) return false;

        // 보조 텍스처(노멀맵·마스크 등)도 모든 프레임이 같아야 한다. 바꾼 타일은 첫 프레임 그림의 보조 텍스처를 쓰고 UV만 옮긴다.
        SecondarySpriteTexture[] firstSecondary = SecondaryTextures(first);

        int firstIndex = -1;
        for (int k = 0; k < frames.Length; k++)
        {
            Sprite frame = frames[k];
            if (null == frame || frame.texture != texture || frame.packed) return false;
            if (k > 0 && false == SameSecondaryTextures(firstSecondary, SecondaryTextures(frame))) return false;
            if (frame.rect.size != firstRect.size || frame.pivot != first.pivot || false == Mathf.Approximately(frame.pixelsPerUnit, first.pixelsPerUnit)) return false;
            // 사각형 그림만(모양을 딴 그림은 프레임마다 모양이 달라 첫 프레임 모양으로 그리면 잘린다)
            if (frame.vertices.Length != 4) return false;

            float column = frame.rect.x / cellWidth;
            float rowFromBottom = frame.rect.y / cellHeight;
            if (false == IsWhole(column) || false == IsWhole(rowFromBottom)) return false;

            int index = (rows - 1 - Mathf.RoundToInt(rowFromBottom)) * columns + Mathf.RoundToInt(column);
            if (k == 0) firstIndex = index;

            int step = index - firstIndex;
            if (step < 0) return false;
            layerSteps[k] = step;
        }

        // 프레임 순서·속도는 바꾼 층 모두가 같아야 한다(전역으로 넘긴다).
        float fps = _tile.m_MinSpeed * _tilemap.animationFrameRate;
        if (_frameCount < 0)
        {
            _frameCount = frames.Length;
            _fps = fps;
            for (int k = 0; k < MaxFrames; k++) steps[k] = k < frames.Length ? layerSteps[k] : 0f;
        }
        else
        {
            if (_frameCount != frames.Length || false == Mathf.Approximately(_fps, fps)) return false;
            for (int k = 0; k < frames.Length; k++)
            {
                if (steps[k] != layerSteps[k]) return false;
            }
        }

        _info.bAnimated = true;
        _info.columns = columns;
        _info.rows = rows;

        if (stillTiles.TryGetValue(_tile, out TileBase existing) && existing is Tile cached && null != cached && cached.sprite == first
            && cached.colliderType == _tile.m_TileColliderType)
        {
            return true;
        }

        Tile still = ScriptableObject.CreateInstance<Tile>();
        still.name = _tile.name + " (Still)";
        still.sprite = first;
        still.color = Color.white;
        still.transform = Matrix4x4.identity;
        still.flags = TileFlags.None;
        still.colliderType = _tile.m_TileColliderType;
        stillTiles[_tile] = still;
        return true;
    }

    private static SecondarySpriteTexture[] SecondaryTextures(Sprite _sprite)
    {
        int count = _sprite.GetSecondaryTextureCount();
        SecondarySpriteTexture[] textures = new SecondarySpriteTexture[count];
        if (count > 0) _sprite.GetSecondaryTextures(textures);
        return textures;
    }

    private static bool SameSecondaryTextures(SecondarySpriteTexture[] _a, SecondarySpriteTexture[] _b)
    {
        if (_a.Length != _b.Length) return false;

        for (int i = 0; i < _a.Length; i++)
        {
            if (_a[i].name != _b[i].name || _a[i].texture != _b[i].texture) return false;
        }

        return true;
    }

    private static bool IsWhole(float _value)
    {
        return Mathf.Abs(_value - Mathf.Round(_value)) < 0.0001f;
    }
}
