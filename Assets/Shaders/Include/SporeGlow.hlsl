#ifndef SPORE_GLOW_INCLUDED
#define SPORE_GLOW_INCLUDED

// 포자 나무 숨빛. 나무 윗부분 그림에서 밝은 포자 알(하이라이트) 픽셀만 골라, 숲을 천천히 지나가는 빛의 물결에 맞춰
// 밝아졌다 어두워지게 한다. 밝아질 때는 블룸이 살짝 번진다. 그림의 색을 바꾸지 않고 밝기만 곱한다.
// 나무 셰이더(Custom-Sprite-Default-Tree)가 Lit2DCommon(=_MainTex 선언) 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _SporeGlowA.x가 0이라
// 텍스처를 더 읽지도 않고 아무것도 바꾸지 않는다.
// _SporeGlowA : 켜짐(0/1), 가장 밝을 때 더하는 밝기 비율, 물결 속도(라디안/초), 물결 크기(월드)
// _SporeGlowB : 포자 알로 볼 가장 어두운 채널 하한(sRGB 0~1), 포자 알로 볼 채도 상한(sRGB 채널 최대-최소), 밝아지는 순간을 좁히는 지수, (안 씀)
float4 _SporeGlowA;
float4 _SporeGlowB;

bool IsSporeGlowEnabled()
{
    return _SporeGlowA.x > 0.5;
}

float SporeGlowHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float SporeGlowNoise(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = SporeGlowHash21(i);
    float b = SporeGlowHash21(i + float2(1.0, 0.0));
    float c = SporeGlowHash21(i + float2(0.0, 1.0));
    float d = SporeGlowHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// _color: 조명까지 끝난 나무 색, _uv: 픽셀에 맞춘 텍스처 좌표, _positionWS: 월드 위치
half3 ApplySporeGlow(half3 _color, float2 _uv, float2 _positionWS)
{
    if (false == IsSporeGlowEnabled())
    {
        return _color;
    }

    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
    // 판정용이라 근사로 충분하다(Color.hlsl을 끌어오지 않는다).
    tex = pow(max(tex, 0.0h), 1.0h / 2.2h);
#endif
    half lo = min(tex.r, min(tex.g, tex.b));
    half hi = max(tex.r, max(tex.g, tex.b));
    if (lo < (half)_SporeGlowB.x || hi - lo > (half)_SporeGlowB.y)
    {
        return _color;
    }

    // 자리마다 다른 박자로 사인 물결이 돈다. 이웃 나무끼리는 박자가 비슷해 빛이 숲을 번져 가는 것처럼 보인다.
    float phase = SporeGlowNoise(_positionWS / max(_SporeGlowA.w, 0.5)) * 6.2831853 + _Time.y * _SporeGlowA.z;
    float pulse = pow(saturate(0.5 + 0.5 * sin(phase)), max(_SporeGlowB.z, 1.0));
    return _color * (1.0h + (half)(_SporeGlowA.y * pulse));
}

#endif // SPORE_GLOW_INCLUDED
