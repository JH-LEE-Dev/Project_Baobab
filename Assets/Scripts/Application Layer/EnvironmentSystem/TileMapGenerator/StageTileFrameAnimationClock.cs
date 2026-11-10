using UnityEngine;

/// <summary>
/// 셰이더 타일 애니메이션(StageTileFrameAnimation) 시계. 타일을 깐 때부터 지난 게임 시간(시간 배율 적용)을 셰이더 전역값으로 넘긴다.
/// 타일맵 애니메이션 시계와 같이 흐르므로 멈춤(시간 배율 0) 중에는 그림도 멈춘다.
/// </summary>
public class StageTileFrameAnimationClock : MonoBehaviour
{
    private static readonly int TileAnimTimeID = Shader.PropertyToID("_TileAnimTime");

    private double elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        Shader.SetGlobalFloat(TileAnimTimeID, (float)elapsed);
    }
}
