#ifndef STAGE_AMBIENCE_COMMON_INCLUDED
#define STAGE_AMBIENCE_COMMON_INCLUDED

// 스테이지 분위기 연출(돌풍·물 반짝임)이 타일 픽셀 단위로 값을 끊을 때 쓰는 공용 도우미.
// _StageAmbiencePixelsPerUnit은 StageAmbienceShaderGlobals가 채운다(타일 PPU).
float _StageAmbiencePixelsPerUnit;

float StageAmbiencePixelsPerUnit()
{
    return max(_StageAmbiencePixelsPerUnit, 1.0);
}

// 지금 그리는 화면 픽셀이 속한 텍스처 픽셀의 한가운데 월드 위치.
// 타일 스프라이트는 회전 없이 UV 축과 월드 축이 같은 방향이라, 텍셀 안에서의 위치만큼 되돌리면 된다.
float2 StageAmbienceTexelCenterWS(float2 _positionWS, float2 _uv, float4 _texelSize)
{
    float2 texelFrac = frac(_uv * _texelSize.zw) - 0.5;
    return _positionWS - texelFrac / StageAmbiencePixelsPerUnit();
}

// 텍스처 픽셀 중심의 UV.
float2 StageAmbienceTexelCenterUV(float2 _uv, float4 _texelSize)
{
    return (floor(_uv * _texelSize.zw) + 0.5) * _texelSize.xy;
}

#endif // STAGE_AMBIENCE_COMMON_INCLUDED
