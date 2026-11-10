#ifndef TILE_FRAME_ANIMATION_INCLUDED
#define TILE_FRAME_ANIMATION_INCLUDED

// 타일 프레임 애니메이션을 셰이더에서 돌린다(StageTileFrameAnimation).
// 애니메이션 타일(AnimatedTile)을 첫 프레임 그림 하나짜리 타일로 바꿔 깔고, 지금 프레임 그림이 첫 프레임 그림에서
// 텍스처 칸 몇 개 뒤에 있는지만큼 UV를 옮겨 읽는다. 타일맵이 매 프레임 모든 애니메이션 타일을 훑던 비용이 없어진다.
// 프레임 그림은 텍스처를 같은 크기 칸으로 자른 바둑판에서 왼쪽 위부터 읽는 순서(왼쪽 → 오른쪽, 다음 줄)로 이어져 있어야 한다.
// 시간·프레임 순서는 바꾼 타일맵 모두가 같으므로 전역으로, 켜짐과 텍스처 바둑판 크기는 머티리얼마다 받는다.
// 켜지 않은 머티리얼(_TileFrameGrid.x = 0)은 UV를 그대로 돌려준다.
// _TileAnimTime : 타일을 깐 뒤 지난 시간(초). 타일맵 애니메이션 시계와 같다.
// _TileAnimParams : x = 초당 프레임 수, y = 프레임 수(최대 8)
// _TileAnimSeqA : 0~3번 프레임 그림이 첫 프레임 그림에서 몇 칸 뒤인지
// _TileAnimSeqB : 4~7번 프레임 그림이 첫 프레임 그림에서 몇 칸 뒤인지
float _TileAnimTime;
float4 _TileAnimParams;
float4 _TileAnimSeqA;
float4 _TileAnimSeqB;

// _grid : x = 켜짐(0/1), y = 텍스처 가로 칸 수, z = 세로 칸 수, w = (안 씀)
float2 TileFrameAnimationUV(float2 _uv, float4 _grid)
{
    if (_grid.x < 0.5 || _TileAnimParams.y < 1.0 || _grid.y < 1.0 || _grid.z < 1.0)
    {
        return _uv;
    }

    float frame = fmod(floor(max(_TileAnimTime, 0.0) * _TileAnimParams.x), _TileAnimParams.y);
    float4 slot = float4(0.0, 1.0, 2.0, 3.0);
    float step = frame < 4.0 ? dot(_TileAnimSeqA, (float4)(frame == slot)) : dot(_TileAnimSeqB, (float4)((frame - 4.0) == slot));
    if (step == 0.0)
    {
        return _uv;
    }

    // 지금 픽셀이 속한 칸(왼쪽 위부터 읽는 순서 번호)에서 step칸 뒤의 칸으로 옮긴다. UV는 아래가 0이다.
    float2 cells = _grid.yz;
    float2 cell = floor(_uv * cells);
    float rowTop = cells.y - 1.0 - cell.y;
    float index = rowTop * cells.x + cell.x + step;
    float newRowTop = floor(index / cells.x);
    float newColumn = index - newRowTop * cells.x;
    return _uv + float2(newColumn - cell.x, rowTop - newRowTop) / cells;
}

#endif // TILE_FRAME_ANIMATION_INCLUDED
