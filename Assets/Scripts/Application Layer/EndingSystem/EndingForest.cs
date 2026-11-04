using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 엔딩의 1스테이지 숲(풀빛너른숲). 본편 지형 생성기(TileMapGenerator)와 같은 타일·장식·밀도(Stage1TileData)와
/// 같은 나무 그림(Tree.prefab + Tree Visual Data Base)을 쓰되, 게임플레이 로직(충돌·체력·벌목)은 붙이지 않는다.
///
/// [크레딧 오솔길] 셀 좌표 y가 줄어드는 쪽(화면 우하단)으로 곧게 뻗은 흙길. 차가 달리는 동안 앞쪽 줄을 만들고
///   지나간 줄은 지우고 나무는 풀에 돌려 쓴다. 크레딧 길이(빨리 감기 포함)와 무관하게 메모리가 일정하다.
///   길 이쪽(화면 아래, 셀 x가 음수)의 숲은 수관이 차를 가리지 않도록 길에서 충분히 떨어뜨리고, 그 사이 빈 띠에는
///   나무를 드문드문만 심는다. 빈 띠 나무는 차가 뒤로 지나갈 때 본편(EnvironmentInteractionManager)처럼 반투명해진다.
/// [소풍] 오솔길을 모두 지운 뒤 같은 자리에 풀빛너른숲 3(소나무 60 · 자작나무 40)의 빈터를 한 번에 만든다.
///
/// 오브젝트 위치는 본편과 같이 셀 중심에서 반 칸 위(TileMapGenerator.worldPosMap), 정렬은 -y × 100이다.
/// 같은 셀은 언제 만들어도 같은 모습이 되도록 셀 좌표 해시로 무작위를 정한다.
/// </summary>
public class EndingForest : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap decoTilemap;
    [SerializeField] private Tilemap bloomDecoTilemap;
    [SerializeField] private StageTileDataSO tileData;
    [SerializeField] private TreeVisualDataBase treeVisualDataBase;
    [SerializeField] private GameObject treePrefab;

    [Header("오솔길")]
    [Tooltip("셀 x 범위(길 가운데가 0). 카메라가 위에서 내려오는 순간과 크레딧 중 왼쪽으로 옮겨간 화면까지 덮는다.")]
    [SerializeField] private int roadMinX = -38;
    [SerializeField] private int roadMaxX = 54;
    [Tooltip("차가 있는 줄 기준으로 앞(진행 방향)으로 미리 만들어 두는 줄 수.")]
    [SerializeField] private int rowsAhead = 44;
    [Tooltip("차가 있는 줄 기준으로 뒤에 남겨 두는 줄 수. 시작 직후 카메라가 위에서 내려오는 동안은 RoadKeepBehindAtStart를 쓴다.")]
    [SerializeField] private int rowsBehind = 32;
    [SerializeField] private int rowsBehindAtStart = 50;
    [Tooltip("길 가장자리(|x| = 2)가 흙이 될 확률. 가장자리를 들쭉날쭉하게 만든다.")]
    [SerializeField] private float roadEdgeSandChance = 0.3f;
    [Tooltip("길 저편(화면 위쪽) 나무가 시작하는 셀 x.")]
    [SerializeField] private int farSideTreeStartX = 3;
    [Tooltip("길 이편(화면 아래쪽) 나무가 시작하는 셀 x(음수). 수관 높이(약 3유닛)가 차를 가리지 않는 거리.")]
    [SerializeField] private int nearSideTreeStartX = -7;
    [Tooltip("오솔길 양옆 숲의 나무 밀도(덩어리 노이즈의 성긴 곳~빽빽한 곳). 소풍 빈터는 아래 treeDensityMin/Max를 쓴다.")]
    [SerializeField] private float roadTreeDensityMin = 0.1f;
    [SerializeField] private float roadTreeDensityMax = 0.45f;
    [Tooltip("길 이편 빈 띠(nearSideTreeStartX 안쪽)에 나무를 심기 시작하는 셀 x(음수). 이 칸부터 nearSideTreeStartX 직전까지가 빈 띠다.")]
    [SerializeField] private int nearVergeTreeStartX = -3;
    [Tooltip("빈 띠의 나무 밀도 = 숲 밀도 × 이 값.")]
    [SerializeField] private float nearVergeDensityScale = 0.65f;

    [Header("빈 띠 나무 반투명 (본편 EnvironmentInteractionManager와 같은 값)")]
    [SerializeField] private float occludedAlpha = 0.5f;
    [SerializeField] private float occlusionFadeDuration = 0.3f;
    [SerializeField] private float occlusionInterval = 0.1f;
    [Tooltip("차체 그림 영역 중 이 비율 이상을 수관이 덮을 때만 반투명하게 한다(그림 여백끼리 모서리만 닿는 경우 제외).")]
    [SerializeField, Range(0f, 1f)] private float occlusionMinCoverage = 0.2f;
    [SerializeField] private float treeDensityMin = 0.03f;
    [SerializeField] private float treeDensityMax = 0.22f;
    [SerializeField] private float treeClumpScale = 0.11f;

    [Header("소풍 빈터 (풀빛너른숲 3)")]
    [SerializeField] private int picnicHalfExtent = 34;
    [Tooltip("빈터 반지름(아이소 타원, 가로 기준 월드 유닛).")]
    [SerializeField] private float picnicClearingRadius = 3.8f;
    [Tooltip("빈터 앞쪽(화면 아래) 나무가 인물을 가리지 않도록 비우는 깊이(월드 유닛).")]
    [SerializeField] private float picnicFrontClearDepth = 2.4f;
    [Tooltip("돗자리 주변에 장식(풀·돌·나비)을 두지 않는 반지름(월드 유닛).")]
    [SerializeField] private float picnicMatClearRadius = 1.6f;
    [Tooltip("나무·바위를 두지 않는 자리(빈터 가운데 기준). 세워 둔 차 등.")]
    [SerializeField] private Vector2[] picnicKeepClearPoints = { new Vector2(-3.25f, 1.6f) };
    [SerializeField] private float picnicKeepClearRadius = 2.2f;
    [Tooltip("빈터 둘레 나무 밀도 하한. 둘레가 빽빽해야 숲속 빈터로 읽힌다.")]
    [SerializeField] private float picnicRimDensity = 0.22f;
    [Tooltip("둘레 밀도가 보통 숲 밀도로 돌아오는 거리(빈터 반지름 바깥, 월드 유닛).")]
    [SerializeField] private float picnicRimWidth = 3f;

    [Header("그림자 (본편 LightingController와 같은 식, 시각 고정)")]
    [SerializeField, Range(0f, 1f)] private float timeOfDay = 0.4f;
    [SerializeField] private float shadowAngleOffset = 34f;
    [SerializeField] private float minShadowHeightScale = 0.75f;
    [SerializeField] private float maxShadowHeightScale = 1.5f;

    private static readonly TreeType[] RoadTreeTypes = { TreeType.OakTree, TreeType.PineTree, TreeType.BirchTree };
    private static readonly float[] RoadTreeWeights = { 0.4f, 0.35f, 0.25f };
    // 풀빛너른숲 3의 나무 구성(Density Data Base: PineTree 60, BirchTree 40).
    private static readonly TreeType[] PicnicTreeTypes = { TreeType.PineTree, TreeType.BirchTree };
    private static readonly float[] PicnicTreeWeights = { 0.6f, 0.4f };

    private class TreeView
    {
        public GameObject root;
        public TreeVisualComponent visual;
        public CustomSortable sortable;
        public Shadow topShadow;
        public Shadow bottomShadow;
        // 수관 영역을 잴 렌더러(그림자 제외). 빈 띠 나무가 차를 가리는지 볼 때만 쓴다.
        public SpriteRenderer[] bodyRenderers;
        public bool bVerge;
        public bool bFaded;
    }

    private readonly Stack<TreeView> treePool = new Stack<TreeView>();
    private readonly Dictionary<GameObject, Stack<GameObject>> decoPools = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> decoPrefabOf = new Dictionary<GameObject, GameObject>();
    private readonly Dictionary<int, List<TreeView>> rowTrees = new Dictionary<int, List<TreeView>>();
    private readonly Dictionary<int, List<GameObject>> rowDecos = new Dictionary<int, List<GameObject>>();
    private readonly List<TreeView> picnicTrees = new List<TreeView>();
    private readonly List<TreeView> vergeTrees = new List<TreeView>();
    private float occlusionTimer;
    private readonly List<GameObject> picnicDecos = new List<GameObject>();

    private Transform poolRoot;
    private Transform objectRoot;
    private bool bRoadActive;
    private int builtMinRow;
    private int builtMaxRow;
    private int keepBehind;
    private float shadowAngle;
    private float shadowScaleY;
    private Vector3Int[] rowPositions;
    private TileBase[] rowGroundTiles;
    private TileBase[] rowDecoTiles;
    private TileBase[] rowBloomTiles;
    private Vector2 picnicCenter;

    /// <summary>셀 한 줄(진행 방향으로 한 칸)을 지날 때 월드에서 움직이는 거리.</summary>
    public Vector3 RowStep => null != grid ? grid.CellToWorld(new Vector3Int(0, -1, 0)) - grid.CellToWorld(Vector3Int.zero) : new Vector3(0.5f, -0.25f, 0f);

    /// <summary>길 가운데 줄(셀 x = 0)의 _row번째 줄 위 지면 위치. _row는 소수도 된다(차가 셀 사이에 있을 때).</summary>
    public Vector3 GetRoadPoint(float _row)
    {
        return ObjectPosition(Vector3Int.zero) + RowStep * _row;
    }

    public Vector3 PicnicCenter => ObjectPosition(Vector3Int.zero);

    public float ShadowAngle => shadowAngle;
    public float ShadowScaleY => shadowScaleY;

    private void Awake()
    {
        GameObject pool = new GameObject("Pool");
        pool.SetActive(false);
        poolRoot = pool.transform;
        poolRoot.SetParent(transform, false);

        objectRoot = new GameObject("Objects").transform;
        objectRoot.SetParent(transform, false);

        // LightingController.UpdateShadow와 같은 식으로 그림자 각도·길이를 한 번 정한다.
        shadowAngle = Mathf.Repeat(shadowAngleOffset + (timeOfDay - 0.25f) * 360f, 360f);
        float sinValue = Mathf.Sin(timeOfDay * Mathf.PI * 2f);
        shadowScaleY = Mathf.Lerp(minShadowHeightScale, maxShadowHeightScale, sinValue * sinValue);

        int width = roadMaxX - roadMinX + 1;
        rowPositions = new Vector3Int[width];
        rowGroundTiles = new TileBase[width];
        rowDecoTiles = new TileBase[width];
        rowBloomTiles = new TileBase[width];
    }

    #region Road

    /// <summary>차가 _row번째 줄에 있을 때 보이는 범위를 모두 만든다.</summary>
    public void BeginRoad(float _row)
    {
        ClearAll();

        bRoadActive = true;
        keepBehind = rowsBehindAtStart;
        int row = Mathf.FloorToInt(_row);
        builtMinRow = row - keepBehind;
        builtMaxRow = builtMinRow - 1;
        UpdateRoad(_row);
    }

    /// <summary>카메라가 내려온 뒤에는 뒤쪽을 덜 남긴다.</summary>
    public void ShrinkRoadBehind()
    {
        keepBehind = rowsBehind;
    }

    /// <summary>차(또는 카메라)가 있는 줄. 앞쪽 줄을 만들고, 뒤로 멀어진 줄을 지운다.</summary>
    public void UpdateRoad(float _row)
    {
        if (false == bRoadActive) return;

        int row = Mathf.FloorToInt(_row);
        while (builtMaxRow < row + rowsAhead)
        {
            builtMaxRow++;
            BuildRoadRow(builtMaxRow);
        }

        while (builtMinRow < row - keepBehind)
        {
            ClearRoadRow(builtMinRow);
            builtMinRow++;
        }
    }

    // _row번째 줄 = 셀 y가 -_row인 줄. 셀 x는 길을 가로지르는 방향이다.
    private void BuildRoadRow(int _row)
    {
        int cellY = -_row;
        List<TreeView> trees = null;
        List<GameObject> decos = null;

        for (int i = 0; i < rowPositions.Length; i++)
        {
            int x = roadMinX + i;
            Vector3Int cell = new Vector3Int(x, cellY, 0);
            rowPositions[i] = cell;
            rowDecoTiles[i] = null;
            rowBloomTiles[i] = null;

            int ax = Mathf.Abs(x);
            bool bSand = ax <= 1 || (2 == ax && Hash(x, cellY, 1) < roadEdgeSandChance);
            // 차가 지나가는 칸(|x| <= 1)에는 바위를 두지 않는다.
            bool bLane = ax <= 1;

            rowGroundTiles[i] = PickGround(bSand, x, cellY);
            DecorateCell(cell, bSand, bLane, ref rowGroundTiles[i], ref rowDecoTiles[i], ref rowBloomTiles[i], ref decos, out bool bBlocked);

            bool bVerge = x < 0 && x > nearSideTreeStartX && x <= nearVergeTreeStartX;
            if (false == bSand && false == bBlocked && (x >= farSideTreeStartX || x <= nearSideTreeStartX || bVerge))
            {
                float clump = Mathf.PerlinNoise((x + 1000f) * treeClumpScale, (cellY + 1000f) * treeClumpScale);
                float density = Mathf.Lerp(roadTreeDensityMin, roadTreeDensityMax, Mathf.SmoothStep(0f, 1f, clump));
                if (bVerge)
                {
                    density *= nearVergeDensityScale;
                }
                else
                {
                    // 길 바로 옆은 조금 성기게 해서 길이 숲 사이로 트여 보이게 한다.
                    int edgeDistance = x > 0 ? x - farSideTreeStartX : nearSideTreeStartX - x;
                    if (edgeDistance < 2) density *= 0.5f;
                }

                if (Hash(x, cellY, 2) < density)
                {
                    TreeType type = PickWeighted(RoadTreeTypes, RoadTreeWeights, Hash(x, cellY, 3));
                    TreeView tree = PlaceTree(cell, type);
                    if (null != tree)
                    {
                        if (null == trees) trees = new List<TreeView>(8);
                        trees.Add(tree);

                        if (bVerge)
                        {
                            tree.bVerge = true;
                            vergeTrees.Add(tree);
                        }
                    }
                }
            }
        }

        ApplyRow();

        if (null != trees) rowTrees[_row] = trees;
        if (null != decos) rowDecos[_row] = decos;
    }

    private void ClearRoadRow(int _row)
    {
        int cellY = -_row;
        for (int i = 0; i < rowPositions.Length; i++)
        {
            rowPositions[i] = new Vector3Int(roadMinX + i, cellY, 0);
            rowGroundTiles[i] = null;
            rowDecoTiles[i] = null;
            rowBloomTiles[i] = null;
        }

        ApplyRow();

        if (rowTrees.TryGetValue(_row, out List<TreeView> trees))
        {
            for (int i = 0; i < trees.Count; i++) ReleaseTree(trees[i]);
            rowTrees.Remove(_row);
        }

        if (rowDecos.TryGetValue(_row, out List<GameObject> decos))
        {
            for (int i = 0; i < decos.Count; i++) ReleaseDeco(decos[i]);
            rowDecos.Remove(_row);
        }
    }

    /// <summary>
    /// 빈 띠 나무 중 수관이 _occludee(차체 그림 영역)와 겹치는 나무를 반투명하게, 벗어난 나무를 되돌린다.
    /// 본편 EnvironmentInteractionManager처럼 일정 간격으로만 판정하고 알파는 트윈으로 바꾼다.
    /// </summary>
    public void UpdateOcclusion(Bounds _occludee)
    {
        occlusionTimer += Time.deltaTime;
        if (occlusionTimer < occlusionInterval) return;
        occlusionTimer = 0f;

        float occludeeArea = Mathf.Max(0.0001f, _occludee.size.x * _occludee.size.y);
        for (int i = 0; i < vergeTrees.Count; i++)
        {
            TreeView tree = vergeTrees[i];
            if (null == tree.visual) continue;

            bool bCover = false;
            if (TryGetCanopyBounds(tree, out Bounds canopy))
            {
                float overlapX = Mathf.Min(canopy.max.x, _occludee.max.x) - Mathf.Max(canopy.min.x, _occludee.min.x);
                float overlapY = Mathf.Min(canopy.max.y, _occludee.max.y) - Mathf.Max(canopy.min.y, _occludee.min.y);
                bCover = overlapX > 0f && overlapY > 0f && overlapX * overlapY >= occludeeArea * occlusionMinCoverage;
            }
            if (bCover == tree.bFaded) continue;

            tree.bFaded = bCover;
            tree.visual.FadeAlpha(bCover ? occludedAlpha : 1f, occlusionFadeDuration);
        }
    }

    private static bool TryGetCanopyBounds(TreeView _tree, out Bounds _bounds)
    {
        _bounds = default;
        bool bAny = false;
        if (null == _tree.bodyRenderers) return false;

        for (int i = 0; i < _tree.bodyRenderers.Length; i++)
        {
            SpriteRenderer sr = _tree.bodyRenderers[i];
            if (null == sr || false == sr.enabled || null == sr.sprite || false == sr.gameObject.activeInHierarchy) continue;

            if (bAny) _bounds.Encapsulate(sr.bounds);
            else _bounds = sr.bounds;
            bAny = true;
        }

        return bAny;
    }

    private void ApplyRow()
    {
        if (null != groundTilemap) groundTilemap.SetTiles(rowPositions, rowGroundTiles);
        if (null != decoTilemap) decoTilemap.SetTiles(rowPositions, rowDecoTiles);
        if (null != bloomDecoTilemap) bloomDecoTilemap.SetTiles(rowPositions, rowBloomTiles);
    }

    #endregion

    #region Picnic

    /// <summary>오솔길을 모두 지우고 같은 자리(셀 0,0 중심)에 소풍 빈터를 만든다.</summary>
    public void BuildPicnic()
    {
        ClearAll();
        picnicCenter = PicnicCenter;

        int size = picnicHalfExtent * 2 + 1;
        Vector3Int[] positions = new Vector3Int[size * size];
        TileBase[] ground = new TileBase[positions.Length];
        TileBase[] deco = new TileBase[positions.Length];
        TileBase[] bloom = new TileBase[positions.Length];
        List<GameObject> decos = picnicDecos;
        int index = 0;

        for (int y = -picnicHalfExtent; y <= picnicHalfExtent; y++)
        {
            for (int x = -picnicHalfExtent; x <= picnicHalfExtent; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                Vector2 offset = (Vector2)ObjectPosition(cell) - picnicCenter;
                bool bMatArea = offset.magnitude < picnicMatClearRadius || IsNearKeepClearPoint(offset, picnicKeepClearRadius * 0.7f);

                positions[index] = cell;
                ground[index] = PickGround(false, x, y);

                bool bBlocked = false;
                if (false == bMatArea)
                {
                    DecorateCell(cell, false, false, ref ground[index], ref deco[index], ref bloom[index], ref decos, out bBlocked);
                }

                if (false == bBlocked && CanPlacePicnicTree(offset))
                {
                    float clump = Mathf.PerlinNoise((x + 500f) * treeClumpScale, (y + 500f) * treeClumpScale);
                    // 빈터 둘레는 빽빽하게 두르고, 멀어지면 보통 숲 밀도로 돌아온다. 둘레가 성기면 빈터 경계가 흐려 보인다.
                    float forest = Mathf.Lerp(treeDensityMin, treeDensityMax, Mathf.SmoothStep(0f, 1f, clump));
                    float edge = Mathf.InverseLerp(picnicClearingRadius, picnicClearingRadius + picnicRimWidth, IsoRadius(offset));
                    float density = Mathf.Lerp(Mathf.Max(forest * 1.5f, picnicRimDensity), forest, edge);

                    if (Hash(x, y, 12) < density)
                    {
                        TreeType type = PickWeighted(PicnicTreeTypes, PicnicTreeWeights, Hash(x, y, 13));
                        TreeView tree = PlaceTree(cell, type);
                        if (null != tree) picnicTrees.Add(tree);
                    }
                }

                index++;
            }
        }

        if (null != groundTilemap) groundTilemap.SetTiles(positions, ground);
        if (null != decoTilemap) decoTilemap.SetTiles(positions, deco);
        if (null != bloomDecoTilemap) bloomDecoTilemap.SetTiles(positions, bloom);
    }

    private bool CanPlacePicnicTree(Vector2 _offset)
    {
        if (IsoRadius(_offset) < picnicClearingRadius) return false;
        if (IsNearKeepClearPoint(_offset, picnicKeepClearRadius)) return false;

        // 빈터 앞쪽(화면 아래)의 나무는 수관이 인물을 가린다. 인물 정면 띠는 더 깊이 비운다.
        if (_offset.y < 0f && _offset.y > -picnicFrontClearDepth && Mathf.Abs(_offset.x) < picnicClearingRadius + 1f) return false;

        return true;
    }

    private bool IsNearKeepClearPoint(Vector2 _offset, float _radius)
    {
        if (null == picnicKeepClearPoints) return false;

        for (int i = 0; i < picnicKeepClearPoints.Length; i++)
        {
            if (IsoRadius(_offset - picnicKeepClearPoints[i]) < _radius) return true;
        }

        return false;
    }

    // 아이소 화면에서 둥글게 보이는 거리(세로를 두 배로 친다).
    private static float IsoRadius(Vector2 _offset)
    {
        return new Vector2(_offset.x, _offset.y * 2f).magnitude;
    }

    #endregion

    #region Cells

    private TileBase PickGround(bool _bSand, int _x, int _y)
    {
        if (null == tileData) return null;

        List<TileBase> tiles = _bSand ? tileData.SandTiles : tileData.GrassTiles;
        if (null == tiles || 0 == tiles.Count) return null;
        return tiles[Mathf.Min(tiles.Count - 1, (int)(Hash(_x, _y, 4) * tiles.Count))];
    }

    // TileMapGenerator.ApplyTiles의 장식 순서와 밀도를 그대로 따른다: 바위(StaticObj) → 나비 등(AnimatedObj) → 땅 장식 → 풀 장식.
    private void DecorateCell(Vector3Int _cell, bool _bSand, bool _bNoRock,
        ref TileBase _ground, ref TileBase _deco, ref TileBase _bloom, ref List<GameObject> _decos, out bool _bBlocked)
    {
        _bBlocked = false;
        if (null == tileData) return;

        int x = _cell.x;
        int y = _cell.y;
        Vector3 pos = ObjectPosition(_cell);

        List<StaticObj> staticPrefabs = _bSand ? tileData.SandStaticObjPrefabs : tileData.GrassStaticObjPrefabs;
        float staticDensity = _bSand ? tileData.SandStaticObjDensity : tileData.GrassStaticObjDensity;
        if (false == _bNoRock && null != staticPrefabs && staticPrefabs.Count > 0 && Hash(x, y, 5) < staticDensity)
        {
            StaticObj prefab = staticPrefabs[Mathf.Min(staticPrefabs.Count - 1, (int)(Hash(x, y, 6) * staticPrefabs.Count))];
            if (null != prefab)
            {
                AddDeco(ref _decos, SpawnDeco(prefab.gameObject, pos));
                _bBlocked = true;
                return;
            }
        }

        bool bAnimated = false;
        List<AnimatedObj> animatedPrefabs = tileData.AnimatedObjPrefabs;
        if (false == _bSand && null != animatedPrefabs && animatedPrefabs.Count > 0 && Hash(x, y, 7) < tileData.AnimatedObjDensity)
        {
            AnimatedObj prefab = animatedPrefabs[Mathf.Min(animatedPrefabs.Count - 1, (int)(Hash(x, y, 8) * animatedPrefabs.Count))];
            if (null != prefab)
            {
                AddDeco(ref _decos, SpawnDeco(prefab.gameObject, pos));
                bAnimated = true;
            }
        }

        if (bAnimated) return;

        bool bReplaceGround = tileData.UseGroundTilemapForDeco;
        float groundDecoProb = _bSand ? tileData.SandDecoDensity : tileData.GroundDecoDensity;
        float bloomDecoProb = _bSand ? tileData.BloomSandDecoDensity : tileData.BloomGroundDecoDensity;
        List<TileBase> groundDecos = tileData.GroundDecoTiles;
        List<TileBase> bloomGroundDecos = tileData.BloomGroundDecoTiles;

        if (null != groundDecos && groundDecos.Count > 0 && Hash(x, y, 9) < groundDecoProb)
        {
            PlaceDeco(groundDecos[Mathf.Min(groundDecos.Count - 1, (int)(Hash(x, y, 10) * groundDecos.Count))], bReplaceGround, ref _ground, ref _deco);
            return;
        }

        if (null != bloomGroundDecos && bloomGroundDecos.Count > 0 && Hash(x, y, 9) < groundDecoProb + bloomDecoProb)
        {
            _bloom = bloomGroundDecos[Mathf.Min(bloomGroundDecos.Count - 1, (int)(Hash(x, y, 10) * bloomGroundDecos.Count))];
            return;
        }

        if (_bSand) return;

        List<TileBase> grassDecos = tileData.GrassDecoTiles;
        List<TileBase> bloomGrassDecos = tileData.BloomGrassDecoTiles;
        float grassValue = Hash(x, y, 11);
        if (null != grassDecos && grassDecos.Count > 0 && grassValue < tileData.GrassDecoDensity)
        {
            PlaceDeco(grassDecos[Mathf.Min(grassDecos.Count - 1, (int)(Hash(x, y, 14) * grassDecos.Count))], bReplaceGround, ref _ground, ref _deco);
        }
        else if (null != bloomGrassDecos && bloomGrassDecos.Count > 0 && grassValue < tileData.GrassDecoDensity + tileData.BloomGrassDecoDensity)
        {
            _bloom = bloomGrassDecos[Mathf.Min(bloomGrassDecos.Count - 1, (int)(Hash(x, y, 14) * bloomGrassDecos.Count))];
        }
    }

    private static void PlaceDeco(TileBase _decoTile, bool _bReplaceGround, ref TileBase _ground, ref TileBase _deco)
    {
        if (null == _decoTile) return;

        if (_bReplaceGround) _ground = _decoTile;
        else _deco = _decoTile;
    }

    private static void AddDeco(ref List<GameObject> _decos, GameObject _deco)
    {
        if (null == _deco) return;
        if (null == _decos) _decos = new List<GameObject>(4);
        _decos.Add(_deco);
    }

    // 본편 오브젝트 위치: 셀 중심에서 반 칸 위(TileMapGenerator.worldPosMap).
    private Vector3 ObjectPosition(Vector3Int _cell)
    {
        if (null == grid) return Vector3.zero;

        Vector3 center = null != groundTilemap ? groundTilemap.GetCellCenterWorld(_cell) : grid.GetCellCenterWorld(_cell);
        return center + new Vector3(0f, grid.cellSize.y * 0.5f, 0f);
    }

    private static TreeType PickWeighted(TreeType[] _types, float[] _weights, float _value)
    {
        float total = 0f;
        for (int i = 0; i < _weights.Length; i++) total += _weights[i];

        float pick = _value * total;
        for (int i = 0; i < _types.Length; i++)
        {
            pick -= _weights[i];
            if (pick < 0f) return _types[i];
        }

        return _types[_types.Length - 1];
    }

    // 셀 좌표와 용도(_salt)로 정해지는 0~1 값. 같은 셀은 언제 만들어도 같은 모습이 된다.
    private static float Hash(int _x, int _y, int _salt)
    {
        unchecked
        {
            uint h = (uint)_x * 374761393u + (uint)_y * 668265263u + (uint)_salt * 2246822519u + 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777216f;
        }
    }

    #endregion

    #region Objects

    private TreeView PlaceTree(Vector3Int _cell, TreeType _type)
    {
        if (null == treeVisualDataBase) return null;

        TreeView tree = GetTree();
        if (null == tree) return null;

        Transform t = tree.root.transform;
        t.SetParent(objectRoot, false);
        t.position = ObjectPosition(_cell);

        if (null != tree.visual)
        {
            tree.visual.ApplyVisual(new TreeData(_type, TreeGrade.Normal, treeVisualDataBase.Get(_type), default));
        }

        if (null != tree.topShadow) tree.topShadow.ManualUpdate(shadowAngle, shadowScaleY, true);
        if (null != tree.bottomShadow) tree.bottomShadow.ManualUpdate(shadowAngle, shadowScaleY, true);
        if (null != tree.sortable) tree.sortable.ManualLateUpdate();
        if (null != tree.visual) tree.visual.UpdateSortingOrder();

        return tree;
    }

    // Tree.prefab을 그림으로만 쓴다. 비활성 부모 아래에서 만들어 TreeObj 등의 OnEnable이 돌기 전에 로직·충돌을 떼어 낸다.
    private TreeView GetTree()
    {
        if (treePool.Count > 0) return treePool.Pop();
        if (null == treePrefab) return null;

        GameObject go = Instantiate(treePrefab, poolRoot);
        go.name = "EndingTree";

        TreeObj treeObj = go.GetComponent<TreeObj>();
        Shadow topShadow = null;
        Shadow bottomShadow = null;
        if (null != treeObj) topShadow = treeObj.TopShadowObject;

        Shadow[] shadows = go.GetComponentsInChildren<Shadow>(true);
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] == topShadow) continue;
            if (null == topShadow && shadows[i].name.Contains("Top")) topShadow = shadows[i];
            else if (null == bottomShadow) bottomShadow = shadows[i];
        }

        Collider2D[] colliders = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) DestroyImmediate(colliders[i]);

        EHealthComponent health = go.GetComponent<EHealthComponent>();
        if (null != treeObj) DestroyImmediate(treeObj);
        if (null != health) DestroyImmediate(health);

        Rigidbody2D[] bodies = go.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++) DestroyImmediate(bodies[i]);

        TreeView tree = new TreeView
        {
            root = go,
            visual = go.GetComponentInChildren<TreeVisualComponent>(true),
            sortable = go.GetComponent<CustomSortable>(),
            topShadow = topShadow,
            bottomShadow = bottomShadow,
        };

        if (null != topShadow) topShadow.Initialize();
        if (null != bottomShadow) bottomShadow.Initialize();
        if (null != tree.visual) tree.visual.Initialize(null != topShadow ? topShadow.transform : go.transform, tree.sortable);

        // 수관 영역은 땅에 눕는 그림자를 뺀 그림으로 잰다.
        List<SpriteRenderer> body = new List<SpriteRenderer>(8);
        if (null != tree.visual)
        {
            SpriteRenderer[] renderers = tree.visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Transform t = renderers[i].transform;
                if (renderers[i].name.Contains("Shadow")) continue;
                if (null != topShadow && t.IsChildOf(topShadow.transform)) continue;
                if (null != bottomShadow && t.IsChildOf(bottomShadow.transform)) continue;
                body.Add(renderers[i]);
            }
        }
        tree.bodyRenderers = body.ToArray();

        return tree;
    }

    private void ReleaseTree(TreeView _tree)
    {
        if (null == _tree || null == _tree.root) return;

        if (_tree.bVerge)
        {
            vergeTrees.Remove(_tree);
            _tree.bVerge = false;
        }

        if (_tree.bFaded)
        {
            if (null != _tree.visual) _tree.visual.SetAlpha(1f);
            _tree.bFaded = false;
        }

        _tree.root.transform.SetParent(poolRoot, false);
        treePool.Push(_tree);
    }

    // 바위(StaticObj)·나비(AnimatedObj) 프리팹을 본편처럼 놓는다. 둘 다 스스로 그림만 돌리는 장식이다.
    private GameObject SpawnDeco(GameObject _prefab, Vector3 _position)
    {
        if (null == _prefab) return null;

        if (false == decoPools.TryGetValue(_prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            decoPools.Add(_prefab, pool);
        }

        GameObject go;
        if (pool.Count > 0)
        {
            go = pool.Pop();
        }
        else
        {
            go = Instantiate(_prefab, poolRoot);
            decoPrefabOf[go] = _prefab;

            AnimatedObj animated = go.GetComponent<AnimatedObj>();
            if (null != animated) animated.Initialize();
        }

        go.transform.SetParent(objectRoot, false);
        go.transform.position = _position;

        AnimatedObj anim = go.GetComponent<AnimatedObj>();
        if (null != anim)
        {
            anim.ResetAnimationToRandomFrame();
            anim.SetSortingOrder();
        }
        else
        {
            CustomSortable sortable = go.GetComponent<CustomSortable>();
            if (null != sortable) sortable.ManualLateUpdate();
        }

        return go;
    }

    private void ReleaseDeco(GameObject _deco)
    {
        if (null == _deco) return;

        if (decoPrefabOf.TryGetValue(_deco, out GameObject prefab) && decoPools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            _deco.transform.SetParent(poolRoot, false);
            pool.Push(_deco);
            return;
        }

        Destroy(_deco);
    }

    /// <summary>만든 타일과 오브젝트를 모두 걷는다(오브젝트는 풀로 돌아간다).</summary>
    public void ClearAll()
    {
        bRoadActive = false;

        if (null != groundTilemap) groundTilemap.ClearAllTiles();
        if (null != decoTilemap) decoTilemap.ClearAllTiles();
        if (null != bloomDecoTilemap) bloomDecoTilemap.ClearAllTiles();

        foreach (KeyValuePair<int, List<TreeView>> pair in rowTrees)
        {
            for (int i = 0; i < pair.Value.Count; i++) ReleaseTree(pair.Value[i]);
        }
        rowTrees.Clear();

        foreach (KeyValuePair<int, List<GameObject>> pair in rowDecos)
        {
            for (int i = 0; i < pair.Value.Count; i++) ReleaseDeco(pair.Value[i]);
        }
        rowDecos.Clear();

        for (int i = 0; i < picnicTrees.Count; i++) ReleaseTree(picnicTrees[i]);
        picnicTrees.Clear();

        for (int i = 0; i < picnicDecos.Count; i++) ReleaseDeco(picnicDecos[i]);
        picnicDecos.Clear();
    }

    #endregion
}
