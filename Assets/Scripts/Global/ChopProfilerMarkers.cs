using Unity.Profiling;

/// <summary>
/// 벌목(공격 판정 → 나무 사망 → 원목 드랍) 구간을 프로파일러에서 구분해 보기 위한 측정 마커.
/// 충격파·회오리 베기 등으로 나무가 한꺼번에 쓰러질 때의 프레임 드랍을 실측하려고 넣었다.
///
/// 마커는 측정만 한다(게임 로직에 영향 없음). ProfilerMarker의 Begin/End는 프로파일러가 켜진 빌드
/// (에디터·Development Build)에서만 기록되고, 릴리스 빌드에서는 빈 호출이 된다.
/// 에디터·Development Build는 풀 이중 반납 검사(PoolSettings.CollectionCheck)가 켜져 있어 반납 비용이
/// 릴리스보다 크게 잡힌다는 점에 유의한다.
/// </summary>
public static class ChopProfilerMarkers
{
    /// <summary>충격파의 피해 판정 한 번(0.04초마다, 과열 폭발과 그로 인한 사망 처리 포함).</summary>
    public static readonly ProfilerMarker ShockWaveCheck = new ProfilerMarker("LumberBoy.Chop.ShockWaveCheck");

    /// <summary>나무 한 그루의 사망 처리 전체(이펙트·사운드·타일·원목 드랍·전리품·풀 반환).</summary>
    public static readonly ProfilerMarker TreeDead = new ProfilerMarker("LumberBoy.Chop.TreeDead");

    /// <summary>쓰러진 나무 한 그루의 원목 드랍(풀에서 꺼내기·발사).</summary>
    public static readonly ProfilerMarker SpawnLogs = new ProfilerMarker("LumberBoy.Chop.SpawnLogs");

    /// <summary>원목 풀이 비어 프리팹을 새로 만드는 순간. 전투 중에 잡히면 미리 채우기가 모자란 것이다.</summary>
    public static readonly ProfilerMarker CreateLogItem = new ProfilerMarker("LumberBoy.Chop.CreateLogItem");

    /// <summary>던전 준비 중 원목 풀 미리 채우기.</summary>
    public static readonly ProfilerMarker LogPoolPrewarm = new ProfilerMarker("LumberBoy.Chop.LogPoolPrewarm");

    /// <summary>쓰러진 나무의 풀 반환(상태 초기화·비활성화 포함).</summary>
    public static readonly ProfilerMarker TreeRelease = new ProfilerMarker("LumberBoy.Chop.TreeRelease");

    /// <summary>공격 판정 루프 동안 모아 둔 나무 타일맵 쓰기 반영.</summary>
    public static readonly ProfilerMarker TreeTileFlush = new ProfilerMarker("LumberBoy.Chop.TreeTileFlush");
}
