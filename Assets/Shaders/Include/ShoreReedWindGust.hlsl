#ifndef SHORE_REED_WIND_GUST_INCLUDED
#define SHORE_REED_WIND_GUST_INCLUDED

#include "WindGust.hlsl"

// 피봇 자리 돌풍 세기로 읽을 프레임(-1이면 스프라이트 프레임 그대로).
float ComputeReedGustFrame(float2 _pivotWS, float _mid, float _strong)
{
    float gust = SampleWindGust(_pivotWS);
    if (gust >= _strong) return 2.0;
    if (gust >= _mid) return 1.0;
    return -1.0;
}

float2 ApplyReedGustFrame(float2 _uv, float _frame, float _frameCount)
{
    if (_frame >= 0.0)
    {
        float n = max(_frameCount, 1.0);
        _uv.x = (frac(_uv.x * n) + _frame) / n;
    }
    return _uv;
}

#endif // SHORE_REED_WIND_GUST_INCLUDED
