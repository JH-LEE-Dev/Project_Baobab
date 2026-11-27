using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// 바닥·물 타일맵(개별 모드)을 "줄마다 한 덩어리 메쉬"로 대신 그린다. 그림은 개별 모드와 같고 그리기 준비만 싸다.
///
/// [왜]
///   바닥과 물 타일맵은 칸 단위로 서로 앞뒤를 가려야 해서 TilemapRenderer 개별 모드(Individual)로 그린다. 개별 모드는 화면 안의
///   칸(약 950장)을 프레임마다 하나씩 준비하고 그린다. 이 비용이 숲의 그리기·컬링에서 가장 큰 몫이었다.
///
/// [어떻게 같은 그림이 되는가]
///   개별 모드는 칸을 화면 세로 위치 순서(위쪽부터)로 그린다. 아이소메트릭 격자에서 세로 위치가 같은 칸(칸 x + y가 같은 "줄")끼리는
///   가로로 한 칸 폭만큼 떨어져 있어 서로 겹치지 않는다. 그래서 줄 안의 순서는 그림에 영향이 없고, 줄과 줄의 순서만 지키면 된다.
///   - 타일맵마다 줄 하나를 메쉬 하나로 만든다(그 줄에 쓰인 텍스처마다 서브메쉬 하나).
///   - 정렬 순서를 "타일맵의 정렬 순서 - 줄 번호"로 주어 위쪽 줄부터 그려지게 한다. 바닥과 물이 같은 줄 번호 기준을 쓰므로
///     두 타일맵의 줄이 개별 모드처럼 서로 섞여 그려진다.
///   - 정점 위치·UV·색은 타일맵에서 칸마다 읽은 값(스프라이트 메쉬, 칸 변환, 칸 색 × 타일맵 색)으로 만든다.
///   원래 TilemapRenderer는 끄기만 한다. 타일 데이터는 그대로라 지진 물결(QuakeTileRipple)·충돌·길찾기 등 타일을 읽는 쪽은 바뀌지 않는다.
///
/// [재질]
///   TilemapRenderer는 그릴 때 칸의 스프라이트 텍스처를 끼워 준다. 메쉬는 그럴 수 없어 텍스처마다 원본 재질의 사본을 둔다.
///   - 원본 렌더러에 걸린 속성 블록 값(물의 _HDRIntensity 등)은 사본 재질에 옮겨 담는다. 속성 블록을 메쉬 렌더러에 그대로 주면
///     SRP 배처 경로를 벗어나고, 그 경로에서는 스프라이트 색(unity_SpriteColor)이 채워지지 않아 타일이 그려지지 않는다.
///     같은 이유로 사본의 인스턴싱은 끈다.
///   - 원본 렌더러의 재질이 바뀌면(스테이지 재질 교체, 타일 프레임 애니메이션 켜짐 사본) 그리기 직전에 알아채 사본을 다시 맞춘다.
///
/// [쓰지 않는 경우 - 원래 타일맵 그대로 그린다]
///   SRP 배처가 꺼져 있음, 타일맵이 개별 모드가 아님, 바닥과 물의 정렬 층·순서가 다름, 칸 변환에 이동이 들어 있음(개별 모드의
///   정렬 위치가 달라진다). 로그라이크는 지형을 실행 중에 바꾸므로 부르는 쪽(TileMapGenerator)에서 쓰지 않는다.
///
/// [알려진 부작용 - 타일 그림과는 무관]
///   정렬 층·순서·정렬 축 거리가 모두 같은 렌더러끼리 겹칠 때 누가 위에 그려지는지는 엔진이 렌더러를 등록해 둔 순서를 따른다
///   (나무 렌더러를 전부 껐다가 반대 순서로 켜면 겹친 자리가 뒤집히고, 원래 순서로 다시 켜면 돌아오는 것으로 확인).
///   줄 메쉬 렌더러 수백 개가 화면에 등록되면 그 배치가 달라져, 같은 줄에 한 칸 간격으로 나란히 선 두 나무의 수관이 겹치는
///   좁은 띠(가로 3픽셀쯤)에서 어느 나무가 위인지가 일부 쌍에서 원래와 반대가 된다. 게임이 정해 둔 순서가 아니라 원래도
///   나무가 다시 자라는 순서 등에 따라 달라지는 값이다. 줄 메쉬를 만들기만 하고 숨겨 두면(타일맵이 그림) 달라지지 않는다.
///
/// 검증(2026-11-27):
///   - 네 숲 3단계와 별뿌리 숲 1단계에서 같은 프레임에 두 방식으로 그려 화면 전체 픽셀 대조(지진 중 포함) - 차이 0.
///   - 숲 골든(별뿌리·마그마·포자 1단계): 로직·난수·물리 기록 차이 0. 별뿌리 숲의 그림 차이 1,056칸은 덤프 9프레임의
///     1,252픽셀 모두 위 부작용의 띠(나란한 나무 쌍의 가운데선에서 0.25칸 이내) 안이었다.
/// </summary>
public sealed class TileRowStripRenderer
{
    private struct TileRec
    {
        public int row;
        public int tex;
        public int geo;
        public Vector3 anchor;
        public Matrix4x4 matrix;
        public Color color;
    }

    private sealed class SpriteGeo
    {
        public Vector2[] vertices;
        public Vector2[] uv;
        public ushort[] triangles;
    }

    private sealed class Strip
    {
        public GameObject go;
        public MeshRenderer renderer;
        public Mesh mesh;
    }

    private sealed class Layer
    {
        public Tilemap map;
        public TilemapRenderer source;
        public GameObject root;
        public Material syncedSource;
        public readonly List<Texture> textures = new List<Texture>(8);
        public readonly List<Sprite> textureSprites = new List<Sprite>(8);
        public readonly List<Material> materials = new List<Material>(8);
        public readonly List<Strip> strips = new List<Strip>(320);
        public readonly List<TileRec> recs = new List<TileRec>(20000);
        public int used;
        // 이번 Rebuild에서 줄 메쉬를 만든 층인지(예전 빌드의 층이 남아 있을 수 있다)
        public bool bBuilt;
    }

    private sealed class RecComparer : IComparer<TileRec>
    {
        public int Compare(TileRec _a, TileRec _b)
        {
            if (_a.row != _b.row) return _a.row < _b.row ? -1 : 1;
            if (_a.tex != _b.tex) return _a.tex < _b.tex ? -1 : 1;
            return 0;
        }
    }

    /// <summary>끄면 다음 맵 생성부터 줄 메쉬를 만들지 않고 원래 타일맵으로 그린다(비교·문제 확인용).</summary>
    public static bool bEnabled = true;

    /// <summary>지금 줄 메쉬를 가진 인스턴스(비교·진단용). 없으면 null.</summary>
    public static TileRowStripRenderer Current { get; private set; }

    private static readonly RecComparer recComparer = new RecComparer();

    // // 내부 의존성
    private readonly List<Layer> layers = new List<Layer>(2);
    private readonly Dictionary<Sprite, int> geoIndex = new Dictionary<Sprite, int>(64);
    private readonly List<SpriteGeo> geos = new List<SpriteGeo>(64);
    private readonly List<Vector3> vertexScratch = new List<Vector3>(1024);
    private readonly List<Vector2> uvScratch = new List<Vector2>(1024);
    private readonly List<Color> colorScratch = new List<Color>(1024);
    private readonly List<List<int>> triangleScratch = new List<List<int>>(8);
    private readonly List<Material> materialScratch = new List<Material>(8);
    private readonly SecondarySpriteTexture[] secondaryScratch = new SecondarySpriteTexture[8];
    // 유니티 오브젝트라 생성자(타일맵 생성기의 필드 초기화) 시점에는 만들 수 없다. 처음 쓸 때 만든다.
    private MaterialPropertyBlock blockScratch;
    private TileBase[] blockBuffer = new TileBase[0];
    private bool bActive;
    private bool bShown;

    public bool IsActive => bActive;

    /// <summary>
    /// 지금 타일맵 내용으로 줄 메쉬를 다시 만들고 원래 렌더러를 끈다. 만들 수 없는 조건이면 원래 타일맵 그대로 둔다.
    /// 타일을 다 깔고(재질·속성 블록까지 정해진 뒤) 부른다. _water는 없어도 된다.
    /// </summary>
    public void Rebuild(Tilemap _ground, Tilemap _water)
    {
        Clear();
        if (false == bEnabled || null == _ground) return;
        // 줄 메쉬는 SRP 배처 경로에서만 스프라이트 색이 채워진다(위 [재질] 참고)
        if (false == GraphicsSettings.useScriptableRenderPipelineBatching) return;

        Layer ground = PrepareLayer(0, _ground);
        if (null == ground) return;

        Layer water = null != _water ? PrepareLayer(1, _water) : null;
        if (null != _water && null == water) return;

        // 두 타일맵이 같은 정렬 층·순서일 때만 줄 번호로 섞어 그릴 수 있다
        if (null != water && (water.source.sortingLayerID != ground.source.sortingLayerID || water.source.sortingOrder != ground.source.sortingOrder)) return;

        if (false == Collect(ground)) return;
        if (null != water && false == Collect(water)) return;

        int rowMin = int.MaxValue;
        int rowMax = int.MinValue;
        RowRange(ground, ref rowMin, ref rowMax);
        if (null != water) RowRange(water, ref rowMin, ref rowMax);
        if (rowMin > rowMax) return;
        // 정렬 순서는 16비트 범위다
        if (ground.source.sortingOrder - (rowMax - rowMin) < short.MinValue) return;

        BuildLayer(ground, rowMin);
        if (null != water) BuildLayer(water, rowMin);

        bActive = true;
        Current = this;
        SetShown(true);
        RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
    }

    /// <summary>줄 메쉬를 감추고 원래 타일맵 렌더러를 다시 켠다. 만들어 둔 메쉬 오브젝트는 다음 Rebuild에서 다시 쓴다.</summary>
    public void Clear()
    {
        RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        if (true == bActive) SetShown(false);
        bActive = false;
        if (ReferenceEquals(Current, this)) Current = null;
        for (int l = 0; l < layers.Count; l++)
        {
            Layer layer = layers[l];
            layer.recs.Clear();
            layer.syncedSource = null;
            layer.bBuilt = false;
            if (null != layer.root) layer.root.SetActive(false);
        }
    }

    /// <summary>만든 오브젝트·메쉬·재질을 모두 없앤다(타일맵 생성기가 파괴될 때).</summary>
    public void Dispose()
    {
        Clear();
        for (int l = 0; l < layers.Count; l++)
        {
            Layer layer = layers[l];
            for (int i = 0; i < layer.strips.Count; i++)
            {
                if (null != layer.strips[i].mesh) Object.Destroy(layer.strips[i].mesh);
            }

            DestroyMaterials(layer);
            if (null != layer.root) Object.Destroy(layer.root);
        }

        layers.Clear();
    }

    /// <summary>줄 메쉬(true)와 원래 타일맵(false) 가운데 어느 쪽으로 그릴지 바꾼다. 줄 메쉬가 만들어져 있을 때만 뜻이 있다(비교·진단용).</summary>
    public void SetShown(bool _bStrips)
    {
        if (false == bActive) return;

        bShown = _bStrips;
        for (int l = 0; l < layers.Count; l++)
        {
            Layer layer = layers[l];
            if (false == layer.bBuilt) continue;
            if (null != layer.source) layer.source.enabled = false == _bStrips;
            if (null != layer.root) layer.root.SetActive(_bStrips);
        }

        if (true == _bStrips) SyncMaterials(true);
    }

    // // 내부 메서드

    private Layer PrepareLayer(int _index, Tilemap _map)
    {
        TilemapRenderer source = _map.GetComponent<TilemapRenderer>();
        if (null == source || TilemapRenderer.Mode.Individual != source.mode || null == source.sharedMaterial) return null;

        while (layers.Count <= _index) layers.Add(new Layer());
        Layer layer = layers[_index];
        if (false == ReferenceEquals(layer.map, _map))
        {
            // 다른 타일맵이면(격자가 새로 만들어짐) 예전 오브젝트는 버린다
            if (null != layer.root) Object.Destroy(layer.root);
            for (int i = 0; i < layer.strips.Count; i++)
            {
                if (null != layer.strips[i].mesh) Object.Destroy(layer.strips[i].mesh);
            }

            layer.strips.Clear();
            layer.root = null;
        }

        layer.map = _map;
        layer.source = source;
        layer.used = 0;
        layer.recs.Clear();
        return layer;
    }

    // 타일맵의 칸을 훑어 그릴 칸 목록을 만든다. 줄 메쉬로 같은 그림을 낼 수 없는 칸이 있으면 false.
    private bool Collect(Layer _layer)
    {
        Tilemap map = _layer.map;
        BoundsInt bounds = map.cellBounds;
        int cellCount = bounds.size.x * bounds.size.y * bounds.size.z;
        if (0 >= cellCount) return true;

        if (blockBuffer.Length < cellCount) blockBuffer = new TileBase[cellCount];
        int got = map.GetTilesBlockNonAlloc(bounds, blockBuffer);
        Vector3 anchorOffset = map.tileAnchor;
        Matrix4x4 orientation = map.orientationMatrix;
        Color mapColor = map.color;
        _layer.textures.Clear();
        _layer.textureSprites.Clear();

        int index = 0;
        for (int z = bounds.zMin; z < bounds.zMax; z++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++, index++)
                {
                    if (index >= got || null == blockBuffer[index]) continue;

                    Vector3Int cell = new Vector3Int(x, y, z);
                    Sprite sprite = map.GetSprite(cell);
                    if (null == sprite) continue;

                    Matrix4x4 tileMatrix = map.GetTransformMatrix(cell);
                    // 칸 변환에 이동이 있으면 개별 모드의 정렬 위치가 칸 자리와 달라져 줄 순서로는 같은 그림이 안 된다
                    if (0f != tileMatrix.m03 || 0f != tileMatrix.m13 || 0f != tileMatrix.m23) return false;

                    Texture texture = sprite.texture;
                    int tex = _layer.textures.IndexOf(texture);
                    if (0 > tex)
                    {
                        tex = _layer.textures.Count;
                        _layer.textures.Add(texture);
                        _layer.textureSprites.Add(sprite);
                    }

                    if (false == geoIndex.TryGetValue(sprite, out int geo))
                    {
                        geo = geos.Count;
                        geos.Add(new SpriteGeo { vertices = sprite.vertices, uv = sprite.uv, triangles = sprite.triangles });
                        geoIndex.Add(sprite, geo);
                    }

                    _layer.recs.Add(new TileRec
                    {
                        row = x + y,
                        tex = tex,
                        geo = geo,
                        anchor = map.CellToLocalInterpolated(new Vector3(x + anchorOffset.x, y + anchorOffset.y, z + anchorOffset.z)),
                        matrix = orientation * tileMatrix,
                        color = map.GetColor(cell) * mapColor,
                    });
                }
            }
        }

        System.Array.Clear(blockBuffer, 0, cellCount);
        _layer.recs.Sort(recComparer);
        return true;
    }

    private static void RowRange(Layer _layer, ref int _rowMin, ref int _rowMax)
    {
        List<TileRec> recs = _layer.recs;
        if (0 == recs.Count) return;

        // 줄 번호 순으로 정렬돼 있다
        if (recs[0].row < _rowMin) _rowMin = recs[0].row;
        if (recs[recs.Count - 1].row > _rowMax) _rowMax = recs[recs.Count - 1].row;
    }

    private void BuildLayer(Layer _layer, int _rowMin)
    {
        Tilemap map = _layer.map;
        if (null == _layer.root)
        {
            _layer.root = new GameObject("TileRowStrips_" + map.name);
            _layer.root.transform.SetParent(map.transform.parent, false);
        }

        // 타일맵과 같은 자리(메쉬 정점은 타일맵 로컬 좌표다)
        Transform rootTransform = _layer.root.transform;
        rootTransform.localPosition = map.transform.localPosition;
        rootTransform.localRotation = map.transform.localRotation;
        rootTransform.localScale = map.transform.localScale;
        _layer.root.layer = map.gameObject.layer;
        _layer.root.SetActive(false);

        DestroyMaterials(_layer);
        for (int i = 0; i < _layer.textures.Count; i++)
        {
            _layer.materials.Add(new Material(_layer.source.sharedMaterial) { name = "TileRowStrip_" + map.name + "_" + i });
        }

        _layer.syncedSource = null;
        _layer.bBuilt = true;

        List<TileRec> recs = _layer.recs;
        int start = 0;
        while (start < recs.Count)
        {
            int row = recs[start].row;
            int end = start;
            while (end < recs.Count && recs[end].row == row) end++;

            BuildStrip(_layer, row, _rowMin, start, end);
            start = end;
        }

        for (int i = _layer.used; i < _layer.strips.Count; i++)
        {
            if (null != _layer.strips[i].go) _layer.strips[i].go.SetActive(false);
        }
    }

    private void BuildStrip(Layer _layer, int _row, int _rowMin, int _start, int _end)
    {
        Strip strip = NextStrip(_layer);
        vertexScratch.Clear();
        uvScratch.Clear();
        colorScratch.Clear();
        materialScratch.Clear();

        List<TileRec> recs = _layer.recs;
        int subMesh = -1;
        int currentTex = -1;
        for (int i = _start; i < _end; i++)
        {
            TileRec rec = recs[i];
            if (rec.tex != currentTex)
            {
                currentTex = rec.tex;
                subMesh++;
                while (triangleScratch.Count <= subMesh) triangleScratch.Add(new List<int>(1024));
                triangleScratch[subMesh].Clear();
                materialScratch.Add(_layer.materials[rec.tex]);
            }

            SpriteGeo geo = geos[rec.geo];
            int baseIndex = vertexScratch.Count;
            Vector2[] vertices = geo.vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                vertexScratch.Add(rec.anchor + rec.matrix.MultiplyPoint3x4(new Vector3(vertices[v].x, vertices[v].y, 0f)));
                uvScratch.Add(geo.uv[v]);
                colorScratch.Add(rec.color);
            }

            List<int> triangles = triangleScratch[subMesh];
            ushort[] source = geo.triangles;
            for (int t = 0; t < source.Length; t++) triangles.Add(baseIndex + source[t]);
        }

        Mesh mesh = strip.mesh;
        mesh.Clear();
        mesh.indexFormat = vertexScratch.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertexScratch);
        mesh.SetUVs(0, uvScratch);
        mesh.SetColors(colorScratch);
        mesh.subMeshCount = subMesh + 1;
        for (int s = 0; s <= subMesh; s++) mesh.SetTriangles(triangleScratch[s], s, false);
        mesh.RecalculateBounds();

        strip.renderer.SetSharedMaterials(materialScratch);
        strip.renderer.sortingLayerID = _layer.source.sortingLayerID;
        // 줄 번호가 클수록 화면 위쪽이라 먼저 그려져야 한다
        strip.renderer.sortingOrder = _layer.source.sortingOrder - (_row - _rowMin);
        strip.renderer.renderingLayerMask = _layer.source.renderingLayerMask;
        strip.go.layer = _layer.map.gameObject.layer;
        strip.go.SetActive(true);
    }

    private static Strip NextStrip(Layer _layer)
    {
        if (_layer.used < _layer.strips.Count) return _layer.strips[_layer.used++];

        Strip strip = new Strip();
        strip.go = new GameObject("Row");
        strip.go.transform.SetParent(_layer.root.transform, false);
        strip.mesh = new Mesh { name = "TileRowStrip" };
        strip.mesh.MarkDynamic();
        strip.go.AddComponent<MeshFilter>().sharedMesh = strip.mesh;
        strip.renderer = strip.go.AddComponent<MeshRenderer>();
        strip.renderer.shadowCastingMode = ShadowCastingMode.Off;
        strip.renderer.receiveShadows = false;
        strip.renderer.lightProbeUsage = LightProbeUsage.Off;
        strip.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        strip.renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        strip.renderer.allowOcclusionWhenDynamic = false;
        _layer.strips.Add(strip);
        _layer.used++;
        return strip;
    }

    private static void DestroyMaterials(Layer _layer)
    {
        for (int i = 0; i < _layer.materials.Count; i++)
        {
            if (null != _layer.materials[i]) Object.Destroy(_layer.materials[i]);
        }

        _layer.materials.Clear();
    }

    private void OnBeginContextRendering(ScriptableRenderContext _context, List<Camera> _cameras)
    {
        if (true == bActive && true == bShown) SyncMaterials(false);
    }

    // 사본 재질을 원본 타일맵 재질의 지금 값으로 맞춘다. _bForce가 아니면 원본 렌더러의 재질이 바뀐 층만 다시 맞춘다.
    private void SyncMaterials(bool _bForce)
    {
        for (int l = 0; l < layers.Count; l++)
        {
            Layer layer = layers[l];
            if (false == layer.bBuilt || null == layer.source || 0 == layer.materials.Count) continue;

            Material source = layer.source.sharedMaterial;
            if (null == source) continue;
            if (false == _bForce && ReferenceEquals(source, layer.syncedSource)) continue;

            layer.syncedSource = source;
            bool bBlock = layer.source.HasPropertyBlock();
            if (true == bBlock)
            {
                if (null == blockScratch) blockScratch = new MaterialPropertyBlock();
                layer.source.GetPropertyBlock(blockScratch);
            }

            for (int i = 0; i < layer.materials.Count; i++)
            {
                Material material = layer.materials[i];
                if (material.shader != source.shader) material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
                material.enableInstancing = false;
                if (true == bBlock) BakeBlock(material, blockScratch);

                material.mainTexture = layer.textures[i];
                int count = layer.textureSprites[i].GetSecondaryTextures(secondaryScratch);
                for (int s = 0; s < count; s++)
                {
                    if (null != secondaryScratch[s].texture) material.SetTexture(secondaryScratch[s].name, secondaryScratch[s].texture);
                }
            }
        }
    }

    // 속성 블록에 든 값을 재질에 옮겨 담는다(QuakeTileRipple.ApplySourceMaterial과 같은 방식)
    private static void BakeBlock(Material _material, MaterialPropertyBlock _block)
    {
        Shader shader = _material.shader;
        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            int id = shader.GetPropertyNameId(p);
            switch (shader.GetPropertyType(p))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    if (_block.HasFloat(id)) _material.SetFloat(id, _block.GetFloat(id));
                    break;
                case ShaderPropertyType.Color:
                    if (_block.HasColor(id)) _material.SetColor(id, _block.GetColor(id));
                    break;
                case ShaderPropertyType.Vector:
                    if (_block.HasVector(id)) _material.SetVector(id, _block.GetVector(id));
                    break;
            }
        }
    }
}
