using UnityEngine;

/// <summary>
/// 셰이더 타일 애니메이션(StageTileFrameAnimation) 시계. 타일을 깐 때부터 지난 게임 시간(시간 배율 적용)을 셰이더 전역값으로 넘긴다.
/// 타일맵 애니메이션 시계와 같이 흐르므로 멈춤(시간 배율 0) 중에는 그림도 멈춘다.
/// </summary>
public class StageTileFrameAnimationClock : MonoBehaviour
{
    private static readonly int TileAnimTimeID = Shader.PropertyToID("_TileAnimTime");

    private double elapsed;

    // 타일맵 시계 흉내(마을): 엔진 타일맵처럼 타일을 깐 다음 프레임부터 float로 더하고, 한 바퀴(프레임 수 / 속도)가 차면 그만큼 뺀다.
    private bool bTilemapClock;
    private float loopLength;
    private float time;
    private int startFrame;

    /// <summary>엔진 타일맵 시계와 같은 방식으로 흐르게 한다. _loopLength는 한 바퀴 길이(초).</summary>
    public void UseTilemapClock(float _loopLength)
    {
        bTilemapClock = _loopLength > 0f;
        loopLength = _loopLength;
        time = 0f;
        startFrame = Time.frameCount;
    }

    private void Update()
    {
        if (bTilemapClock)
        {
            // 타일을 깐 프레임에는 엔진 타일맵 시계도 아직 흐르지 않는다.
            if (Time.frameCount != startFrame)
            {
                time += Time.deltaTime;
                if (time >= loopLength) time -= loopLength;
            }

            Shader.SetGlobalFloat(TileAnimTimeID, time);
            return;
        }

        elapsed += Time.deltaTime;
        Shader.SetGlobalFloat(TileAnimTimeID, (float)elapsed);
    }
}
