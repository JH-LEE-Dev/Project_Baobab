using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>바닥 타일 파도 연출 수치. 기획서 6장 "진입 연출 - 바닥 타일 파도 간격 / 배경 완성 시간"(TBD)의 자리다.</summary>
[Serializable]
public sealed class RoguelikeTileWaveSettings
{
    [Tooltip("파도 앞머리 속도(월드 단위/초). 클수록 타일 사이 간격이 짧다(기획서 6장 '바닥 타일 파도 간격').")]
    public float frontSpeed = 14f;
    [Tooltip("물결 마루가 가장 높이 솟는 높이(월드 단위).")]
    public float rippleHeight = 0.22f;
    [Tooltip("물결 한 파장의 길이(바닥 평면 거리, 대략 칸 수). 길수록 이웃 칸 높이 차가 작아 매끄럽다.")]
    public float rippleWavelength = 6f;
    [Tooltip("앞머리 뒤로 물결이 몇 파장 동안 이어지다 잦아드는지. 마루 → 골 → 작은 마루 순으로 줄어든다.")]
    public float rippleWaves = 1.5f;
    [Tooltip("물결 골(내려간 칸)이 가장 깊을 때 어두워지는 정도(0~1). 타일에 명암이 없어 높이 차만으로는 물결이 잘 안 읽힌다.")]
    public float rippleShade = 0.3f;
    [Tooltip("진입 5단계 '모든 배경이 N초 이내에 완성'의 N(기획서 10장 임시값 3초). 진입 파도는 바닥 파도 + 배경 조립이 이 시간 안에 끝나도록 앞머리 속도를 맞춘다(frontSpeed보다 느려지지 않는다).")]
    public float entryCompleteSeconds = 3f;
    [Tooltip("진입 4단계 '배경 조립': 바닥이 다 깔린 뒤 나머지 배경(해안선)이 조각 단위로 나타나는 데 걸리는 전체 시간(초).")]
    public float backgroundAssembleSeconds = 1.2f;
    [Tooltip("배경 조각 하나가 투명에서 드러나는 시간(초).")]
    public float backgroundPieceFadeSeconds = 0.3f;
    [Tooltip("배경 조각 한 변의 칸 수(퍼즐 조각 크기).")]
    public int backgroundPieceCells = 3;
    [Tooltip("기준 화면 바깥으로 이만큼(칸)까지는 파도로 바꾼다. 그보다 먼 칸은 화면 밖이라 바로 바꾼다.")]
    public float viewMarginTiles = 2f;
}

/// <summary>
/// 바닥 타일 파도(기획서 4-1 진입 3~5단계, 3-4 티어 전환 - 같은 연출을 공용으로 쓴다).
///
/// 섬 중앙에서 물결이 동심원으로 퍼진다(바닥 평면 기준 원 - 아이소 화면에서는 가로로 긴 타원). 칸 높이는 앞머리에서 떨어진
/// 거리만의 매끄러운 함수다(마루 → 골 → 작은 마루로 잦아드는 감쇠 물결) - 이웃 칸 높이가 연속이라 띠 전체가 한 물결로 출렁인다.
/// 첫 마루에서 그 칸이 새 타일로 바뀐다.
///   진입(Entry): 물은 처음부터 깔려 있고, 육지(바닥 블록)가 파도를 따라 솟아오르며 깔린다(3단계).
///               바닥이 다 깔리면 나머지 배경(해안선)이 퍼즐 조각처럼 하나씩 나타나 완성된다(4~5단계).
///   티어 전환(Transition): 직전 티어 지형이 파도를 따라 칸마다 새 티어 지형(바닥·물·해안선)으로 바뀐다.
///
/// [자연스럽게 - 빈 공간·구름 비침·물 섞임 없음]
///   - 실제 바닥·물·해안선 층은 솟지 않고 제자리에서 바뀌기만 한다. 파도 동안 화면 안 바닥은 전부 모든 타일 층 위의
///     복제층(리프트)이 그린다 - 움직이는 칸만 복제하면 앞 칸이 뒤 칸의 흙 옆면을 가리지 못해 굵은 흙 줄이 생긴다.
///     한 타일맵 안에서는 칸 순서대로 앞뒤가 정렬되므로, 솟은 칸의 옆면은 이웃과의 높이 차만큼만 보인다.
///     물도 같은 정렬 순서로 복제층에 그린다 - 실제 층처럼 앞 칸의 물이 물가 절벽 아랫부분을 덮어야, 파도 동안 절벽이
///     물 위로 드러났다가 끝나는 순간 잠기는 일이 없다. 물·해안선은 출렁이지 않는다.
///     실제 바닥을 들면 원래 자리가 비어 물이 드러나고, 지면 스텐실(GroundStencil)이 어긋나 구름 그림자가 육지에 비친다.
///   - 진입에서 섬 자리·물가 칸은 파도가 오기 전까지 깊은 물로 둔다(물가 모양 물을 두면 섬 윤곽이 미리 보인다).
///     물결이 닿는 순간 복제층의 그 칸에 블록이 제 크기로 나타나고 바다는 새 물로 바뀐다. 실제 층은 물결이 다 지나간 뒤 바꾼다.
///   - 지면 스텐실(GroundStencil) 복제층에는 솟은 블록과 같은 타일을 놓아, 블록 아래 남은 물 스텐실로 구름 그림자가 비치지 않게 한다.
///
/// 맵 생성(TileMapGenerator)은 평소처럼 한 번에 깐다. 이 클래스는 생성 직전/직후 타일을 떠 두었다가 화면 안 칸만
/// 시작 상태로 되돌려 놓고 순서대로 바꾼다. 충돌 타일맵은 건드리지 않는다(생성 결과가 곧바로 맞다).
/// 칸이 바뀌는 순간 TileRevealedEvent(칸 월드 위치)가 나간다 - 하이라이트·반짝임 같은 장식은 여기에 붙인다.
/// </summary>
public sealed class RoguelikeTileWave
{
    /// <summary>타일 한 칸이 새 지형으로 바뀌었다(물결 꼭대기, 칸 중심 월드 위치). 구독자가 없으면 비용이 없다.</summary>
    public event Action<Vector3> TileRevealedEvent;

    private struct WaveCell
    {
        public int index;
        public Vector3Int cell;
        public float key;
        public bool bInView;
        public Vector3 worldPos;
    }

    private struct BackgroundPiece
    {
        public int firstCell;
        public int cellCount;
        public float startTime;
    }

    private sealed class WaveCellComparer : IComparer<WaveCell>
    {
        public int Compare(WaveCell _a, WaveCell _b) => _a.key.CompareTo(_b.key);
    }

    private static readonly WaveCellComparer cellComparer = new WaveCellComparer();

    // 층 묶음. 바닥(0번이 바닥 블록), 물(0번이 물 타일), 해안선(물 가장자리).
    private readonly List<Tilemap> floorLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> waterLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> shoreLayers = new List<Tilemap>(2);
    private readonly List<WaveCell> cells = new List<WaveCell>(4096);
    // cells와 같은 순서. 칸이 물결 어느 단계인지(StageWaiting → Reached → Swapped → Done).
    private byte[] cellStage = new byte[0];
    private const byte StageWaiting = 0;
    private const byte StageReached = 1;
    private const byte StageSwapped = 2;
    private const byte StageDone = 3;
    private readonly List<int> pieceCells = new List<int>(2048);
    private readonly List<BackgroundPiece> pieces = new List<BackgroundPiece>(512);

    // 층마다 [영역 칸 수] 크기. before = 생성 직전, after = 생성 결과, start = 파도 시작 상태(물 층만).
    private TileBase[][] floorBefore, floorAfter;
    private TileBase[][] waterBefore, waterAfter, waterStart;
    private TileBase[][] shoreBefore, shoreAfter;
    private TileBase[] emptyBlock;
    // 솟는 블록을 그리는 복제층(바닥 층과 같은 순서). 파도가 끝나면 비운다.
    // 스텐실 층 자리는 같은 블록 모양으로 지면 스텐실을 함께 들어 올린다 - 솟은 블록 밑에 남은 물 스텐실로 구름 그림자가 비치지 않게.
    private readonly List<Tilemap> lifts = new List<Tilemap>(4);
    // 해안선(물 모서리) 복제층. 바닥 복제층이 실제 해안선을 덮으므로 티어 전환 동안 해안선도 그 위에 따로 그린다.
    private readonly List<Tilemap> shoreLifts = new List<Tilemap>(2);
    // 물 복제층(물 층마다). 실제 층에서는 물이 바닥 블록의 아랫부분(물가 절벽)을 덮는다. 바닥만 복제층으로 올리면
    // 절벽이 물 위로 통째로 드러났다가 파도가 끝나는 순간 물속으로 잠긴다. 그래서 물도 같은 앞뒤 순서로 복제층에 그린다.
    // 물과 해안선은 출렁이지 않는다(제자리) - 솟고 가라앉는 건 육지뿐이다.
    private readonly List<Tilemap> waterLifts = new List<Tilemap>(4);
    // 스텐실 리프트에 놓을 블록 모양을 가져올 바닥 층(스텐실이 아닌 첫 층). 없으면 -1.
    private int liftShapeLayer = -1;
    // 바닥 층 중 스텐실만 쓰는 층(GroundStencil). 복제층에는 블록 모양 타일을 놓는다(LiftTilesOf).
    private bool[] bStencilFloorLayer;
    private bool[] landCell;     // 직전이나 새 지형에 바닥이 있는 칸
    private bool[] nearLand;     // 새 지형의 바닥 칸과 그 둘레 한 칸

    private EnvironmentSystem environmentSystem;
    private RoguelikeTileWaveSettings settings;
    private BoundsInt region;
    private Vector2 viewHalfSize;
    private bool bHasRegion;
    private bool bHasBefore;
    private bool bPlaying;
    // PrepareAfterGenerate가 시작 상태를 깔아 두었고 Play가 아직 안 돌았다.
    private bool bPrepared;
    private bool bEntry;
    // 칸마다 따로 바꾸는 타일의 애니메이션(물결) 시간을 맞출 기준 칸. 따로 바꾼 칸은 재생 시점이 어긋나 주변과 이음선이 진다.
    private Vector3Int animRefCell;
    private bool bHasAnimRef;

    public bool IsPlaying => bPlaying;

    /// <summary>마지막으로 준비(PrepareAfterGenerate)한 파도가 로비 진입 파도인지(아니면 티어 전환).</summary>
    public bool IsEntryWave => bEntry;

    public void Initialize(EnvironmentSystem _environmentSystem, RoguelikeTileWaveSettings _settings)
    {
        environmentSystem = _environmentSystem;
        settings = _settings ?? new RoguelikeTileWaveSettings();
    }

    /// <summary>맵을 새로 깔기 직전에 부른다(티어 전환). 지금 화면 안 칸의 타일을 떠 둔다(아직 영역이 없으면 빈 상태로 친다).</summary>
    public void CaptureBefore()
    {
        bHasBefore = false;
        if (false == bHasRegion || false == BindLayers()) return;

        ReadLayers(floorLayers, floorBefore);
        ReadLayers(waterLayers, waterBefore);
        ReadLayers(shoreLayers, shoreBefore);
        bHasBefore = true;
    }

    /// <summary>
    /// 맵을 깐 직후에 부른다. 새 타일을 떠 두고 화면 안 칸을 파도 시작 상태로 되돌린다.
    /// _bEntry면 진입(물만 깔린 바다), 아니면 티어 전환(직전 지형). 티어 전환인데 직전 지형이 없으면 진입처럼 한다.
    /// </summary>
    public void PrepareAfterGenerate(Vector3 _center, Vector2 _viewHalfSize, bool _bEntry)
    {
        FinishImmediately();

        if (false == BindLayers()) return;

        BuildRegion(_center, _viewHalfSize);
        if (false == bHasRegion) return;

        EnsureBuffers();
        ReadLayers(floorLayers, floorAfter);
        ReadLayers(waterLayers, waterAfter);
        ReadLayers(shoreLayers, shoreAfter);

        bEntry = _bEntry || false == bHasBefore;
        bHasBefore = false;

        BuildMasks();
        BuildWaterStart();

        WriteLayers(floorLayers, bEntry ? null : floorBefore);
        WriteLayers(shoreLayers, bEntry ? null : shoreBefore);
        WriteLayers(waterLayers, waterStart);

        PickAnimationReference();
        EnsureLifts();
        BuildOrder(_center);
        if (bEntry) BuildBackgroundPieces();
        bPrepared = true;
    }

    /// <summary>PrepareAfterGenerate 뒤에 재생한다. 끝나면 화면 안 칸이 전부 새 지형이다. 게임 시간으로 흐른다(일시정지면 멈춘다).</summary>
    public IEnumerator Play()
    {
        if (false == bPrepared) yield break;

        bPrepared = false;
        bPlaying = true;

        // 앞머리는 화면 안 첫 칸에서 출발한다. 그 앞(화면 밖 여유분) 칸은 첫 프레임에 바로 바뀐다.
        float startKey = cells.Count > 0 ? cells[0].key : 0f;
        for (int i = 0; i < cells.Count; i++)
        {
            if (false == cells[i].bInView) continue;
            startKey = cells[i].key;
            break;
        }

        float length = RippleLength;
        float speed = Mathf.Max(0.1f, settings.frontSpeed);
        if (bEntry && cells.Count > 0)
        {
            // 바닥 파도 + 배경 조립이 entryCompleteSeconds 안에 끝나게 한다. 앞머리가 마지막 칸에 닿은 뒤 물결이 그 칸을 다 지나갈 때까지 친다.
            float frontSeconds = settings.entryCompleteSeconds - (pieces.Count > 0 ? Mathf.Max(0f, settings.backgroundAssembleSeconds) : 0f);
            float span = cells[cells.Count - 1].key - startKey + length;
            if (frontSeconds > 0.05f && span > 0f) speed = Mathf.Max(speed, span / frontSeconds);
        }

        if (cellStage.Length < cells.Count) cellStage = new byte[cells.Count];
        Array.Clear(cellStage, 0, cells.Count);

        // 티어 전환: 화면 안 바닥·물·해안선을 전부 복제층으로 옮겨 그린다. 진입은 물만 시작 상태(섬 자리는 깊은 물)로 옮기고, 바닥은 물결이 닿는 칸부터 생긴다.
        // 물을 일부 칸만 복제층에 두면 그 가장자리에서 물 타일의 아랫부분이 앞 칸 물에 가려지지 않아 어두운 삼각형이 비친다.
        if (false == bEntry) FillLiftsWithBefore();
        else FillWaterLifts(waterStart);

        float elapsed = 0f;
        int next = 0;   // 아직 물결이 닿지 않은 첫 칸
        int done = 0;   // 물결이 다 지나가지 않은 첫 칸

        while (done < cells.Count)
        {
            float front = startKey + elapsed * speed;

            while (next < cells.Count && cells[next].key <= front)
            {
                ReachCell(next);
                next++;
            }

            for (int j = done; j < next; j++) UpdateCell(j, front - cells[j].key, length);
            while (done < next && StageDone == cellStage[done]) done++;

            if (done >= cells.Count) break;
            yield return null;
            elapsed += Time.deltaTime;
        }

        // 모든 칸이 제자리·새 지형이다. 복제층을 비워 실제 층(해안선이 바닥 위에 그려지는 원래 순서)으로 돌린다.
        ClearLifts();

        // 진입 4~5단계: 나머지 배경(해안선)이 퍼즐 조각처럼 하나씩 나타나 완성된다.
        if (bEntry && pieces.Count > 0)
        {
            yield return AssembleBackgroundRoutine();
        }

        // 칸마다 따로 바꾼 타일은 애니메이션(물결) 재생 시점이 제각각이라 주변과 이음선이 질 수 있다. 끝나면 영역을 한 번에 다시 깔아 맞춘다.
        WriteAllAfter();

        bPlaying = false;
    }

    /// <summary>재생 도중 흐름이 끊길 때(포기·해제). 남은 칸을 바로 새 지형으로 채우고 솟아 있던 타일을 제자리에 놓는다.</summary>
    public void FinishImmediately()
    {
        if (false == bPlaying && false == bPrepared) return;

        bPlaying = false;
        bPrepared = false;

        WriteAllAfter();
    }

    // // 내부

    private bool BindLayers()
    {
        if (null == environmentSystem) return false;
        if (floorLayers.Count == 0 && waterLayers.Count == 0 && shoreLayers.Count == 0)
        {
            environmentSystem.GetVisualTilemapsForRun(floorLayers, waterLayers, shoreLayers);
        }

        return floorLayers.Count > 0;
    }

    /// <summary>기준 화면(+여유) 안에 중심이 들어오는 칸들의 최소 사각형. 섬은 매번 같은 자리라 한 번만 계산한다.</summary>
    private void BuildRegion(Vector3 _center, Vector2 _viewHalfSize)
    {
        if (bHasRegion) return;

        viewHalfSize = _viewHalfSize;
        Vector2 cellSize = environmentSystem.TileCellSize;
        float halfX = _viewHalfSize.x + settings.viewMarginTiles * cellSize.x;
        float halfY = _viewHalfSize.y + settings.viewMarginTiles * cellSize.y;

        BoundsInt grid = environmentSystem.TileGridBoundsForRun;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int y = grid.yMin; y < grid.yMax; y++)
        {
            for (int x = grid.xMin; x < grid.xMax; x++)
            {
                Vector3 pos = environmentSystem.GetTileCellCenterWorldForRun(new Vector3Int(x, y, 0));
                if (Mathf.Abs(pos.x - _center.x) > halfX || Mathf.Abs(pos.y - _center.y) > halfY) continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (minX > maxX) return;

        region = new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        bHasRegion = true;
    }

    private void EnsureBuffers()
    {
        int size = region.size.x * region.size.y;
        if (null != emptyBlock && emptyBlock.Length == size) return;

        emptyBlock = new TileBase[size];
        floorBefore = AllocLayers(floorLayers.Count, size);
        floorAfter = AllocLayers(floorLayers.Count, size);
        waterBefore = AllocLayers(waterLayers.Count, size);
        waterAfter = AllocLayers(waterLayers.Count, size);
        waterStart = AllocLayers(waterLayers.Count, size);
        shoreBefore = AllocLayers(shoreLayers.Count, size);
        shoreAfter = AllocLayers(shoreLayers.Count, size);
        landCell = new bool[size];
        nearLand = new bool[size];
    }

    private static TileBase[][] AllocLayers(int _layers, int _size)
    {
        TileBase[][] result = new TileBase[_layers][];
        for (int i = 0; i < _layers; i++) result[i] = new TileBase[_size];
        return result;
    }

    private void ReadLayers(List<Tilemap> _layers, TileBase[][] _into)
    {
        if (null == _into) return;
        for (int i = 0; i < _layers.Count; i++) _layers[i].GetTilesBlockNonAlloc(region, _into[i]);
    }

    /// <summary>_from이 null이면 영역을 비운다.</summary>
    private void WriteLayers(List<Tilemap> _layers, TileBase[][] _from)
    {
        if (false == bHasRegion || null == emptyBlock) return;
        for (int i = 0; i < _layers.Count; i++) _layers[i].SetTilesBlock(region, null != _from ? _from[i] : emptyBlock);
    }

    private void WriteAllAfter()
    {
        ClearLifts();
        WriteLayers(floorLayers, floorAfter);
        WriteLayers(waterLayers, waterAfter);
        WriteLayers(shoreLayers, shoreAfter);

        // 칸마다 따로 깐 물·해안선(물 모서리)은 애니메이션 시작 시점이 제각각이고, 같은 타일을 다시 깔면 시점이 그대로 남는다.
        // 끝에 영역의 물·해안선을 한 기준 시간으로 맞춘다(물과 물 모서리가 따로 출렁이지 않게).
        SyncRegionAnimation(waterLayers);
        SyncRegionAnimation(shoreLayers);
    }

    private void SyncRegionAnimation(List<Tilemap> _layers)
    {
        if (false == bHasAnimRef || false == bHasRegion || null == emptyBlock) return;
        int size = emptyBlock.Length;
        for (int l = 0; l < _layers.Count; l++)
        {
            Tilemap layer = _layers[l];
            float time = ReferenceAnimationTime(layer);
            for (int i = 0; i < size; i++)
            {
                Vector3Int cell = CellOf(i);
                if (layer.HasTile(cell)) layer.SetAnimationTime(cell, time);
            }
        }
    }

    /// <summary>
    /// 칸을 맞출 기준 애니메이션 시간. 그 층의 기준 칸(영역 바로 바깥, 파도가 건드리지 않는 칸)에 타일이 있으면 그 시간,
    /// 없으면(물 모서리처럼 섬 둘레에만 있는 층) 물 층의 기준 시간을 쓴다 - 맵 생성 때 물과 함께 깔려 같은 시점에 돌던 층이다.
    /// 타일이 없는 칸의 시간(0)을 쓰면 그 칸만 처음부터 다시 돌아 물과 어긋난다.
    /// </summary>
    private float ReferenceAnimationTime(Tilemap _layer)
    {
        if (null != _layer && _layer.HasTile(animRefCell)) return _layer.GetAnimationTime(animRefCell);
        for (int l = 0; l < waterLayers.Count; l++)
        {
            if (waterLayers[l].HasTile(animRefCell)) return waterLayers[l].GetAnimationTime(animRefCell);
        }

        return 0f;
    }

    private void BuildMasks()
    {
        int size = emptyBlock.Length;
        int width = region.size.x;
        int height = region.size.y;

        Array.Clear(nearLand, 0, size);
        for (int i = 0; i < size; i++)
        {
            bool bNewLand = HasAnyTile(floorAfter, i);
            landCell[i] = bNewLand || (false == bEntry && HasAnyTile(floorBefore, i));
            if (false == bNewLand) continue;

            int x = i % width;
            int y = i / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= height) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= width) continue;
                    nearLand[nx + ny * width] = true;
                }
            }
        }
    }

    /// <summary>
    /// 파도 시작 상태의 물. 진입은 새 물, 티어 전환은 직전 물이 기준이다.
    /// 진입에서 섬 자리·물가 칸은 깊은 물 한 칸의 타일(물 층마다)로 채운다 - 아직 바닥이 없는 자리라 바다로 보여야 하고,
    /// 물가 모양 물을 미리 두면 섬 윤곽이 먼저 보인다. 그 칸의 물은 물결 꼭대기에서 바닥과 함께 새 물로 바뀐다.
    /// </summary>
    private void BuildWaterStart()
    {
        int size = emptyBlock.Length;
        int sample = FindDeepWaterSample(bEntry ? waterAfter : waterBefore);
        if (sample < 0) sample = FindDeepWaterSample(waterAfter);

        for (int l = 0; l < waterLayers.Count; l++)
        {
            TileBase filler = sample >= 0 ? waterAfter[l][sample] : null;
            TileBase[] baseTiles = bEntry ? waterAfter[l] : waterBefore[l];
            TileBase[] start = waterStart[l];

            for (int i = 0; i < size; i++)
            {
                start[i] = bEntry && (landCell[i] || nearLand[i]) ? filler : baseTiles[i];
            }
        }
    }

    /// <summary>
    /// 들어 올리는 복제층(리프트)을 (처음 한 번) 만든다. 바닥 층마다 같은 격자·재질의 복제를 둔다. 모든 타일 층보다 위에 그리고 칸 단위로 정렬한다(솟은 블록끼리 앞뒤가 맞게).
    /// 지면 스텐실(GroundStencil - 구름 그림자를 가리는 층)의 복제에는 솟은 바닥 블록과 같은 타일을 놓아, 블록이 그려진 자리의 물 스텐실을 지운다.
    /// 진입에서 블록은 아직 물(물 스텐실 포함)뿐인 칸 위로 솟아오르므로, 이것이 없으면 솟은 블록 위로 구름 그림자가 비친다.
    ///
    /// 실제 바닥 타일은 들지 않고 제자리에 둔다 - 들면 원래 자리가 비어 물이 드러나고, 스텐실이 어긋나 구름 그림자가 육지에 비친다.
    /// </summary>
    private bool EnsureLifts()
    {
        if (lifts.Count > 0) return true;
        if (floorLayers.Count == 0) return false;

        bStencilFloorLayer = new bool[floorLayers.Count];
        for (int l = 0; l < floorLayers.Count; l++)
        {
            TilemapRenderer layerRenderer = null != floorLayers[l] ? floorLayers[l].GetComponent<TilemapRenderer>() : null;
            Material material = null != layerRenderer ? layerRenderer.sharedMaterial : null;
            bStencilFloorLayer[l] = null != material && null != material.shader && material.shader.name.Contains("Stencil");
        }

        liftShapeLayer = -1;
        for (int l = 0; l < floorLayers.Count; l++)
        {
            if (null != floorLayers[l] && false == bStencilFloorLayer[l]) { liftShapeLayer = l; break; }
        }

        int highestOrder = int.MinValue;
        HighestOrder(floorLayers, ref highestOrder);
        HighestOrder(waterLayers, ref highestOrder);
        HighestOrder(shoreLayers, ref highestOrder);
        if (int.MinValue == highestOrder) return false;

        // 복제층끼리의 앞뒤를 실제 층과 같게 맞춘다. 실제 바닥·물은 정렬 순서가 같고 둘 다 칸 단위 정렬이라 칸 위치로 앞뒤가 섞인다
        // (앞 칸의 물이 뒤 칸 블록의 절벽 아랫부분을 덮는다). 그래서 실제 정렬 순서가 같은 층은 복제층도 같은 순서를 주고,
        // 순서가 다른 층은 그 차례대로 모든 실제 층보다 위에 번호를 준다.
        List<LiftSlot> slots = new List<LiftSlot>(floorLayers.Count + waterLayers.Count + shoreLayers.Count);
        AddSlots(slots, floorLayers, 0);
        AddSlots(slots, waterLayers, 1);
        AddSlots(slots, shoreLayers, 2);
        slots.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : x.sequence.CompareTo(y.sequence));

        for (int l = 0; l < floorLayers.Count; l++) lifts.Add(null);
        for (int l = 0; l < waterLayers.Count; l++) waterLifts.Add(null);
        for (int l = 0; l < shoreLayers.Count; l++) shoreLifts.Add(null);

        int liftOrder = highestOrder;
        for (int i = 0; i < slots.Count; i++)
        {
            LiftSlot slot = slots[i];
            if (0 == i || slots[i - 1].rank != slot.rank) liftOrder++;
            // 바닥 블록 층이 없으면 스텐실 복제층에 놓을 모양이 없다.
            if (0 == slot.group && bStencilFloorLayer[slot.layer] && liftShapeLayer < 0) continue;

            Tilemap tilemap = CreateLift(slot.source, slot.renderer, liftOrder);
            if (0 == slot.group) lifts[slot.layer] = tilemap;
            else if (1 == slot.group) waterLifts[slot.layer] = tilemap;
            else shoreLifts[slot.layer] = tilemap;
        }

        return true;
    }

    private struct LiftSlot
    {
        public int group;     // 0 바닥, 1 물, 2 해안선
        public int layer;     // 그 묶음 안의 층 번호
        public int rank;      // 실제 층의 앞뒤 순서
        public int sequence;  // 순서가 같을 때 원래 나열 순서
        public Tilemap source;
        public TilemapRenderer renderer;
    }

    private static void AddSlots(List<LiftSlot> _slots, List<Tilemap> _layers, int _group)
    {
        for (int l = 0; l < _layers.Count; l++)
        {
            Tilemap source = _layers[l];
            TilemapRenderer renderer = null != source ? source.GetComponent<TilemapRenderer>() : null;
            if (null == renderer) continue;

            _slots.Add(new LiftSlot
            {
                group = _group,
                layer = l,
                rank = renderer.sortingOrder,
                sequence = _slots.Count,
                source = source,
                renderer = renderer,
            });
        }
    }

    private static Tilemap CreateLift(Tilemap _source, TilemapRenderer _sourceRenderer, int _order)
    {
        GameObject go = new GameObject("RoguelikeTileWaveLift_" + _source.name);
        go.transform.SetParent(_source.transform.parent, false);
        go.transform.localPosition = _source.transform.localPosition;
        go.transform.localRotation = _source.transform.localRotation;
        go.transform.localScale = _source.transform.localScale;

        Tilemap tilemap = go.AddComponent<Tilemap>();
        tilemap.orientation = _source.orientation;
        tilemap.orientationMatrix = _source.orientationMatrix;
        tilemap.tileAnchor = _source.tileAnchor;
        tilemap.color = _source.color;

        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sharedMaterial = _sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        renderer.sortingOrder = _order;
        // 칸마다 따로 정렬해야 솟은 칸과 이웃 칸의 앞뒤가 맞는다.
        renderer.mode = TilemapRenderer.Mode.Individual;
        return tilemap;
    }

    private static void HighestOrder(List<Tilemap> _layers, ref int _order)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            TilemapRenderer renderer = null != _layers[i] ? _layers[i].GetComponent<TilemapRenderer>() : null;
            if (null != renderer && renderer.sortingOrder > _order) _order = renderer.sortingOrder;
        }
    }

    private void ClearLifts()
    {
        if (false == bHasRegion || null == emptyBlock) return;
        for (int l = 0; l < lifts.Count; l++)
        {
            if (null != lifts[l]) lifts[l].SetTilesBlock(region, emptyBlock);
        }
        for (int l = 0; l < shoreLifts.Count; l++)
        {
            if (null != shoreLifts[l]) shoreLifts[l].SetTilesBlock(region, emptyBlock);
        }
        for (int l = 0; l < waterLifts.Count; l++)
        {
            if (null != waterLifts[l]) waterLifts[l].SetTilesBlock(region, emptyBlock);
        }
    }

    /// <summary>티어 전환 시작: 복제층에 직전 지형을 그대로 옮겨 놓는다(제자리, 칸마다 움직일 수 있게 변형 잠금을 푼다).</summary>
    private void FillLiftsWithBefore()
    {
        for (int l = 0; l < lifts.Count; l++)
        {
            if (null != lifts[l]) lifts[l].SetTilesBlock(region, LiftTilesOf(floorBefore, l));
        }
        for (int l = 0; l < shoreLifts.Count; l++)
        {
            if (null != shoreLifts[l]) shoreLifts[l].SetTilesBlock(region, shoreBefore[l]);
        }
        for (int j = 0; j < cells.Count; j++)
        {
            UnlockLiftCell(cells[j].cell);
            SyncLiftAnimation(shoreLifts, shoreLayers, cells[j].cell);
        }

        FillWaterLifts(waterBefore);
    }

    /// <summary>물 복제층에 영역 전체의 물을 깐다(출렁이지 않으므로 변형 잠금은 그대로, 애니메이션 시간만 실제 층에 맞춘다).</summary>
    private void FillWaterLifts(TileBase[][] _water)
    {
        for (int l = 0; l < waterLifts.Count; l++)
        {
            if (null != waterLifts[l]) waterLifts[l].SetTilesBlock(region, _water[l]);
        }

        if (false == bHasAnimRef) return;
        int size = emptyBlock.Length;
        for (int i = 0; i < size; i++) SyncLiftAnimation(waterLifts, waterLayers, CellOf(i));
    }

    /// <summary>복제층 칸의 애니메이션(물결) 시간을 실제 층의 기준 칸에 맞춘다. 안 맞추면 복제층과 실제 층의 물이 따로 출렁여 이음선이 진다.</summary>
    private void SyncLiftAnimation(List<Tilemap> _lifts, List<Tilemap> _sources, Vector3Int _cell)
    {
        if (false == bHasAnimRef) return;
        for (int l = 0; l < _lifts.Count && l < _sources.Count; l++)
        {
            if (null == _lifts[l] || false == _lifts[l].HasTile(_cell)) continue;
            _lifts[l].SetAnimationTime(_cell, ReferenceAnimationTime(_sources[l]));
        }
    }

    /// <summary>복제층 l에 놓을 타일 배열. 스텐실 복제층은 바닥 블록과 같은 모양이어야 하므로 블록 층의 타일을 쓴다.</summary>
    private TileBase[] LiftTilesOf(TileBase[][] _floorTiles, int _liftIndex)
    {
        return bStencilFloorLayer[_liftIndex] ? _floorTiles[liftShapeLayer] : _floorTiles[_liftIndex];
    }

    private void UnlockLiftCell(Vector3Int _cell)
    {
        for (int l = 0; l < lifts.Count; l++)
        {
            if (null != lifts[l] && lifts[l].HasTile(_cell)) lifts[l].SetTileFlags(_cell, TileFlags.None);
        }
        for (int l = 0; l < shoreLifts.Count; l++)
        {
            if (null != shoreLifts[l] && shoreLifts[l].HasTile(_cell)) shoreLifts[l].SetTileFlags(_cell, TileFlags.None);
        }
    }

    /// <summary>바닥도 해안선도 없고 섬 둘레도 아닌 물 칸(깊은 물) 하나.</summary>
    private int FindDeepWaterSample(TileBase[][] _water)
    {
        if (waterLayers.Count == 0) return -1;

        int size = emptyBlock.Length;
        for (int i = 0; i < size; i++)
        {
            if (null == _water[0][i] || nearLand[i] || landCell[i] || HasAnyTile(shoreAfter, i)) continue;
            return i;
        }

        return -1;
    }

    /// <summary>애니메이션 기준 칸: 영역 바로 바깥 칸(생성 때 깔린 채 파도가 건드리지 않는다).</summary>
    private void PickAnimationReference()
    {
        bHasAnimRef = false;

        BoundsInt grid = environmentSystem.TileGridBoundsForRun;
        int midY = region.yMin + region.size.y / 2;
        if (region.xMin - 1 >= grid.xMin) animRefCell = new Vector3Int(region.xMin - 1, midY, 0);
        else if (region.xMax < grid.xMax) animRefCell = new Vector3Int(region.xMax, midY, 0);
        else return;

        bHasAnimRef = true;
    }

    private void BuildOrder(Vector3 _center)
    {
        cells.Clear();

        Vector2 cellSize = environmentSystem.TileCellSize;
        int width = region.size.x;
        int count = region.size.x * region.size.y;

        for (int i = 0; i < count; i++)
        {
            bool bNeeded = bEntry
                // 진입: 물은 이미 깔려 있다. 육지 칸과 그 둘레 물가 칸만 파도로 바꾼다(해안선은 4단계 배경 조립에서).
                ? nearLand[i]
                // 티어 전환: 바닥이 있거나(직전·새 지형 어느 쪽이든 - 물결에 출렁인다) 물·해안선 타일이 바뀌는 칸.
                // 바닥 없이 물·해안선이 그대로인 칸(먼 바다)은 바꿀 것도 출렁일 것도 없다(복제층에는 그대로 깔려 있다).
                : HasAnyTile(floorAfter, i) || HasAnyTile(floorBefore, i)
                  || false == SameTiles(waterBefore, waterAfter, i) || false == SameTiles(shoreBefore, shoreAfter, i);
            if (false == bNeeded) continue;

            Vector3Int cell = CellOf(i);
            Vector3 pos = environmentSystem.GetTileCellCenterWorldForRun(cell);
            Vector2 offset = new Vector2(pos.x - _center.x, pos.y - _center.y);

            cells.Add(new WaveCell
            {
                index = i,
                cell = cell,
                // 바닥 평면 거리(아이소는 세로가 절반으로 눌려 보이므로 세로를 두 배로 편다). 중앙에서 동심원으로 퍼진다.
                key = new Vector2(offset.x, offset.y * 2f).magnitude,
                // 화면 가장자리에 걸친 칸(중심은 밖이어도 일부가 보이는 칸)도 화면 안으로 친다.
                bInView = Mathf.Abs(offset.x) <= viewHalfSize.x + cellSize.x && Mathf.Abs(offset.y) <= viewHalfSize.y + cellSize.y,
                worldPos = pos,
            });
        }

        cells.Sort(cellComparer);
    }

    /// <summary>진입 4단계용: 해안선이 있는 칸을 정사각 조각(퍼즐)으로 묶고 순서를 섞는다.</summary>
    private void BuildBackgroundPieces()
    {
        pieceCells.Clear();
        pieces.Clear();
        if (shoreLayers.Count == 0) return;

        int width = region.size.x;
        int height = region.size.y;
        int side = Mathf.Max(1, settings.backgroundPieceCells);

        for (int py = 0; py < height; py += side)
        {
            for (int px = 0; px < width; px += side)
            {
                int first = pieceCells.Count;
                for (int y = py; y < Mathf.Min(height, py + side); y++)
                {
                    for (int x = px; x < Mathf.Min(width, px + side); x++)
                    {
                        int i = x + y * width;
                        if (HasAnyTile(shoreAfter, i)) pieceCells.Add(i);
                    }
                }

                if (pieceCells.Count > first) pieces.Add(new BackgroundPiece { firstCell = first, cellCount = pieceCells.Count - first });
            }
        }

        // 퍼즐처럼 하나씩: 순서를 섞는다.
        for (int i = pieces.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            BackgroundPiece tmp = pieces[i];
            pieces[i] = pieces[j];
            pieces[j] = tmp;
        }
    }

    private IEnumerator AssembleBackgroundRoutine()
    {
        float total = Mathf.Max(0.01f, settings.backgroundAssembleSeconds);
        float fade = Mathf.Clamp(settings.backgroundPieceFadeSeconds, 0.01f, total);
        float spread = Mathf.Max(0f, total - fade);
        float step = pieces.Count > 1 ? spread / (pieces.Count - 1) : 0f;

        for (int p = 0; p < pieces.Count; p++)
        {
            BackgroundPiece piece = pieces[p];
            piece.startTime = p * step;
            pieces[p] = piece;
        }

        float elapsed = 0f;
        int nextPiece = 0;
        while (elapsed < total + Time.deltaTime)
        {
            while (nextPiece < pieces.Count && pieces[nextPiece].startTime <= elapsed)
            {
                ShowPiece(pieces[nextPiece]);
                nextPiece++;
            }

            for (int p = 0; p < nextPiece; p++)
            {
                float t = Mathf.Clamp01((elapsed - pieces[p].startTime) / fade);
                SetPieceAlpha(pieces[p], t);
            }

            if (nextPiece >= pieces.Count && elapsed >= pieces[pieces.Count - 1].startTime + fade) break;

            yield return null;
            elapsed += Time.deltaTime;
        }

        for (int p = 0; p < pieces.Count; p++) SetPieceAlpha(pieces[p], 1f);
    }

    private void ShowPiece(in BackgroundPiece _piece)
    {
        for (int c = 0; c < _piece.cellCount; c++)
        {
            int i = pieceCells[_piece.firstCell + c];
            Vector3Int cell = CellOf(i);
            for (int l = 0; l < shoreLayers.Count; l++)
            {
                if (null == shoreAfter[l][i]) continue;
                shoreLayers[l].SetTile(cell, shoreAfter[l][i]);
                shoreLayers[l].SetTileFlags(cell, TileFlags.None);
                shoreLayers[l].SetColor(cell, new Color(1f, 1f, 1f, 0f));
                SyncAnimation(shoreLayers[l], cell);
            }
        }
    }

    private void SetPieceAlpha(in BackgroundPiece _piece, float _alpha)
    {
        Color color = new Color(1f, 1f, 1f, _alpha);
        for (int c = 0; c < _piece.cellCount; c++)
        {
            int i = pieceCells[_piece.firstCell + c];
            Vector3Int cell = CellOf(i);
            for (int l = 0; l < shoreLayers.Count; l++)
            {
                if (null != shoreAfter[l][i]) shoreLayers[l].SetColor(cell, color);
            }
        }
    }

    private Vector3Int CellOf(int _index) => new Vector3Int(region.xMin + _index % region.size.x, region.yMin + _index / region.size.x, 0);

    private static bool SameTiles(TileBase[][] _a, TileBase[][] _b, int _index)
    {
        if (null == _a || null == _b) return _a == _b;
        for (int i = 0; i < _a.Length && i < _b.Length; i++)
        {
            if (_a[i][_index] != _b[i][_index]) return false;
        }

        return true;
    }

    private static bool HasAnyTile(TileBase[][] _layers, int _index)
    {
        if (null == _layers) return false;
        for (int i = 0; i < _layers.Length; i++)
        {
            if (null != _layers[i] && null != _layers[i][_index]) return true;
        }

        return false;
    }

    /// <summary>앞머리 뒤로 물결이 이어지는 거리(바닥 평면 거리). 이만큼 지나간 칸은 제자리에서 멈춘다.</summary>
    private float RippleLength => Mathf.Max(0.1f, settings.rippleWavelength) * Mathf.Max(0.25f, settings.rippleWaves);

    /// <summary>첫 마루(가장 높이 솟는 지점)까지의 거리. 티어 전환은 여기서 새 지형으로 바뀐다.</summary>
    private float CrestDistance => Mathf.Max(0.1f, settings.rippleWavelength) * 0.25f;

    /// <summary>
    /// 앞머리에서 _distance만큼 뒤에 있는 칸의 높이. 감쇠 물결: sin(2π·d/λ) × (1 - d/L).
    /// 앞머리(0)와 끝(L)에서 높이 0. 끝(L)이 반 파장의 배수면 sin도 0이라 멈출 때 기울기가 0에 가까워 튀지 않는다.
    /// </summary>
    private float RippleHeight(float _distance, float _length)
    {
        if (_distance <= 0f || _distance >= _length) return 0f;

        float wavelength = Mathf.Max(0.1f, settings.rippleWavelength);
        float fade = 1f - _distance / _length;
        return settings.rippleHeight * Mathf.Sin(2f * Mathf.PI * _distance / wavelength) * fade;
    }

    /// <summary>물결 앞머리가 칸에 닿았다. 진입은 새 바닥 블록이 이 칸에 제 크기로 나타난다.</summary>
    private void ReachCell(int _order)
    {
        WaveCell cell = cells[_order];
        cellStage[_order] = StageReached;

        if (false == bEntry) return;

        // 진입: 블록은 제 크기로 나타나고 그 칸의 바다는 새 물(육지면 없음, 물가면 물가 물)로 바뀐다.
        // 블록을 작게 돋아나게 하면 이웃 블록·바다와 맞물리지 않아 가장자리가 톱니처럼 지저분해진다. 제 크기면 섬 가장자리가
        // 늘 완성된 모양(앞 칸 바다가 절벽 아랫부분을 덮는 모양)으로 퍼져 나간다.
        SetLiftTiles(cell.cell, cell.index, floorAfter, null, waterAfter);
        ApplyCellPose(cell.cell, 0f, RippleLength);

        TileRevealedEvent?.Invoke(cell.worldPos);
    }

    private void UpdateCell(int _order, float _distance, float _length)
    {
        byte stage = cellStage[_order];
        if (StageDone == stage) return;

        WaveCell cell = cells[_order];

        // 티어 전환: 첫 마루에서 새 지형으로 바꾼다(실제 층과 복제층 둘 다).
        if (false == bEntry && StageReached == stage && _distance >= CrestDistance)
        {
            SwapCell(cell.cell, cell.index);
            SetLiftTiles(cell.cell, cell.index, floorAfter, shoreAfter, waterAfter);
            TileRevealedEvent?.Invoke(cell.worldPos);
            stage = StageSwapped;
        }

        if (_distance >= _length)
        {
            // 물결이 다 지나갔다. 진입은 이제서야 실제 바닥·물을 새 지형으로 바꾼다 - 그 전까지 블록 밑(솟은 만큼 드러나는 틈)은 바다다.
            if (bEntry || StageSwapped != stage) SwapCell(cell.cell, cell.index);
            ApplyCellPose(cell.cell, _length, _length);
            cellStage[_order] = StageDone;
            return;
        }

        cellStage[_order] = stage;
        ApplyCellPose(cell.cell, _distance, _length);
    }

    /// <summary>
    /// 실제 층의 칸을 새 지형(바닥·물, 티어 전환이면 해안선까지)으로 한꺼번에 바꾼다. 파도 동안 화면에는 복제층이 그려지므로
    /// 바뀌는 순간이 보이지 않는다. 진입의 해안선은 4단계에서 따로 나타난다.
    /// </summary>
    private void SwapCell(Vector3Int _cell, int _index)
    {
        SetCellLayers(floorLayers, floorAfter, _cell, _index);
        if (false == bEntry) SetCellLayers(shoreLayers, shoreAfter, _cell, _index);
        SetCellLayers(waterLayers, waterAfter, _cell, _index);
    }

    private void SetCellLayers(List<Tilemap> _layers, TileBase[][] _tiles, Vector3Int _cell, int _index)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            _layers[i].SetTile(_cell, _tiles[i][_index]);
            SyncAnimation(_layers[i], _cell);
        }
    }

    private void SyncAnimation(Tilemap _layer, Vector3Int _cell)
    {
        if (false == bHasAnimRef) return;
        _layer.SetAnimationTime(_cell, ReferenceAnimationTime(_layer));
    }

    /// <summary>복제층에 그 칸의 바닥(과 _shoreTiles가 있으면 해안선) 타일을 놓는다(칸에 타일이 없으면 비운다). 놓은 뒤 변형 잠금을 푼다.</summary>
    private void SetLiftTiles(Vector3Int _cell, int _index, TileBase[][] _floorTiles, TileBase[][] _shoreTiles, TileBase[][] _waterTiles)
    {
        PlaceFlatLiftTiles(waterLifts, waterLayers, _waterTiles, _cell, _index);

        for (int l = 0; l < lifts.Count && l < floorLayers.Count; l++)
        {
            Tilemap lift = lifts[l];
            if (null == lift) continue;

            TileBase tile = LiftTilesOf(_floorTiles, l)[_index];
            lift.SetTile(_cell, tile);
            if (null != tile) lift.SetTileFlags(_cell, TileFlags.None);
        }

        PlaceFlatLiftTiles(shoreLifts, shoreLayers, _shoreTiles, _cell, _index);
    }

    /// <summary>출렁이지 않는 복제층(물·해안선)에 그 칸의 타일을 놓는다. _tiles가 null이면 그대로 둔다.</summary>
    private void PlaceFlatLiftTiles(List<Tilemap> _lifts, List<Tilemap> _sources, TileBase[][] _tiles, Vector3Int _cell, int _index)
    {
        if (null == _tiles) return;
        for (int l = 0; l < _lifts.Count; l++)
        {
            Tilemap lift = _lifts[l];
            if (null == lift) continue;
            lift.SetTile(_cell, _tiles[l][_index]);
        }

        SyncLiftAnimation(_lifts, _sources, _cell);
    }

    /// <summary>
    /// 복제층의 그 칸을 물결 높이만큼 올리거나 내리고, 골(내려간 칸)은 살짝 어둡게 한다.
    /// </summary>
    private void ApplyCellPose(Vector3Int _cell, float _distance, float _length)
    {
        float height = RippleHeight(_distance, _length);

        // 타일에 명암이 없어 높이 차만으로는 물결이 잘 안 읽힌다. 골(내려간 칸)을 어둡게 해 마루와 골의 띠가 보이게 한다.
        float shade = 1f;
        if (settings.rippleHeight > 0f) shade = 1f - Mathf.Clamp01(settings.rippleShade) * Mathf.Clamp01(-height / settings.rippleHeight);

        Matrix4x4 matrix = Matrix4x4.Translate(new Vector3(0f, height, 0f));
        Color color = new Color(shade, shade, shade, 1f);
        ApplyPose(lifts, _cell, matrix, color);
    }

    private static void ApplyPose(List<Tilemap> _lifts, Vector3Int _cell, Matrix4x4 _matrix, Color _color)
    {
        for (int l = 0; l < _lifts.Count; l++)
        {
            Tilemap lift = _lifts[l];
            if (null == lift || false == lift.HasTile(_cell)) continue;

            lift.SetTransformMatrix(_cell, _matrix);
            lift.SetColor(_cell, _color);
        }
    }
}
