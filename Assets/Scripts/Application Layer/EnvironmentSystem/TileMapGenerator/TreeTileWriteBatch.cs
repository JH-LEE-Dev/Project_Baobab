using System;

/// <summary>
/// 공격 한 번의 판정 루프(충격파·다중 공격·부메랑·폭발 등)에서 나무가 여러 그루 쓰러질 때, 나무마다 하던 타일맵 쓰기
/// (충돌 타일 지우기·데코 되돌리기)를 루프가 끝날 때 한 번에 반영하도록 묶는 구간.
///
/// Tilemap.SetTile은 호출마다 네이티브 경계를 넘고 타일 갱신을 예약해서, 한 프레임에 수십 그루가 쓰러지면 그루당
/// 최대 3회씩 쌓인다. 구간 안에서는 같은 쓰기를 순서대로 모아 두었다가 End에서 SetTiles로 반영한다.
///
/// 결과가 같은 이유: 구간은 한 번의 동기 호출 안에서 열리고 닫히므로 그 사이에 물리 스텝·렌더링이 끼지 않고,
/// 던전 충돌·데코 타일맵을 스크립트에서 읽는 곳도 없다(타일 내용 대신 cellToIndex/walkablePositions 같은 부기는
/// 지금처럼 즉시 반영된다). 같은 타일맵에 대한 쓰기 순서는 그대로 지켜지고, 모아 둔 지우기와 순서가 얽힐 수 있는
/// 다른 쓰기(나무 심기·데코 걷어내기)가 들어오면 모아 둔 것을 먼저 반영한다(TileMapGenerator 참고).
///
/// Presentation 계층(충격파 등)에는 타일맵 제공자를 흘려보낼 통로가 없어 정적 창구를 쓴다. 중첩 가능하며(포자막 연쇄
/// 폭발이 충격파 루프 안에서 다시 열리는 경우 등) 가장 바깥 End에서 한 번 반영한다. 호출부는 try/finally로 감싼다.
/// </summary>
public static class TreeTileWriteBatch
{
    private static int depth = 0;
    private static Action flushHandler;

    /// <summary>지금 묶음 구간 안인지.</summary>
    public static bool IsActive => depth > 0;

    /// <summary>던전 타일맵 제공자가 "모아 둔 쓰기를 반영하는" 처리를 등록한다.</summary>
    public static void Register(Action _flush)
    {
        flushHandler = _flush;
    }

    public static void Unregister(Action _flush)
    {
        if (flushHandler == _flush) flushHandler = null;
    }

    public static void Begin()
    {
        depth++;
    }

    public static void End()
    {
        if (depth <= 0) return;

        depth--;
        if (depth == 0) flushHandler?.Invoke();
    }
}
