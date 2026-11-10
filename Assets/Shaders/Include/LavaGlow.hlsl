#ifndef LAVA_GLOW_INCLUDED
#define LAVA_GLOW_INCLUDED

// 용암 빛 번짐. 바닥 셰이더(GroundLavaGlowLit)가 StageAmbienceCommon.hlsl 뒤에 include 한다.
// StageLavaGlow가 맵을 만들 때 칸마다 용암에서 받는 빛(0~1)을 담은 텍스처와 월드 → 칸 변환을 전역으로 넘긴다.
// 텍셀 가운데 월드 위치로 그 텍스처를 읽어(칸 사이는 부드럽게 이어진다) 감쇠 곡선을 걸고, 천천히 일렁이게 한 뒤
// 4x4 베이어 문턱으로 몇 단계로 끊어 픽셀아트처럼 칠한다. 꺼진 스테이지는 _LavaGlowA.x가 0이라 아무것도 바꾸지 않는다.
// _LavaGlowA : 켜짐(0/1), 더하는 세기, 곱하는 세기(바닥 색을 데우는 정도), 진하기 단계 수
// _LavaGlowB : 감쇠 곡선(거듭제곱), 일렁임 정도(0~1), 일렁임 속도, 일렁임 무늬 크기(월드)
// _LavaGlowColor : 빛 색(선형)
// _LavaGlowMapA : 월드 → 칸 좌표 2x2 역행렬(m00, m01, m10, m11)
// _LavaGlowMapB : xy = 칸 (0,0) 가운데의 월드 위치, zw = 텍스처 왼쪽 아래 칸 번호
// _LavaGlowMapC : xy = 1 / 텍스처 크기(칸 수)
// _LavaGlowObject : 서 있는 그림(나무·캐릭터)용. x = 빛 배율(0이면 끔), y = 발밑에서 빛이 사라지는 높이(월드)
// 서 있는 그림은 나무 셰이더(Custom-Sprite-Default-Tree)와 캐릭터 셰이더(Custom-Sprite-Default-NoSnap)가
// ApplyLavaGlowUpright로 칠한다. 발밑(그림 기준점 높이)의 빛을 읽어 발밑에서 가장 밝고 위로 갈수록 옅어진다.

TEXTURE2D(_LavaGlowTex);
SAMPLER(sampler_LavaGlowTex);
float4 _LavaGlowA;
float4 _LavaGlowB;
float4 _LavaGlowColor;
float4 _LavaGlowMapA;
float4 _LavaGlowMapB;
float4 _LavaGlowMapC;
float4 _LavaGlowObject;

bool IsLavaGlowEnabled()
{
    return _LavaGlowA.x > 0.5;
}

// 월드 위치가 받는 용암 빛(0~1, 감쇠 곡선 전)
float SampleLavaGlow(float2 _positionWS)
{
    float2 delta = _positionWS - _LavaGlowMapB.xy;
    float2 cell = float2(dot(_LavaGlowMapA.xy, delta), dot(_LavaGlowMapA.zw, delta));
    float2 uv = (cell - _LavaGlowMapB.zw + 0.5) * _LavaGlowMapC.xy;
    return SAMPLE_TEXTURE2D_LOD(_LavaGlowTex, sampler_LavaGlowTex, uv, 0).r;
}

float LavaGlowHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float LavaGlowNoise(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = LavaGlowHash21(i);
    float b = LavaGlowHash21(i + float2(1.0, 0.0));
    float c = LavaGlowHash21(i + float2(0.0, 1.0));
    float d = LavaGlowHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 4x4 베이어 문턱(0~1 미만).
float LavaGlowBayer4(float2 _texelId)
{
    float2 c = fmod(_texelId, 4.0);
    c = c < 0.0 ? c + 4.0 : c;
    static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (bayer[(int)c.y * 4 + (int)c.x] + 0.5) / 16.0;
}

// _color: 조명까지 끝난 색, _texelCenterWS: 지금 픽셀이 속한 텍셀 가운데 월드 위치
// 감쇠 곡선 전 빛(_glow)에 감쇠·일렁임·단계 끊기를 걸어 색에 칠한다. _strength는 세기 배율(바닥은 1).
// _texelId는 베이어 문턱을 고르는 텍셀 번호다(바닥·나무는 월드 픽셀 격자, 움직이는 캐릭터는 그림 기준점 기준).
half3 ApplyLavaGlowLevel(half3 _color, float _glow, float2 _texelCenterWS, float2 _texelId, float _strength)
{
    if (_glow <= 0.002)
    {
        return _color;
    }

    float glow = pow(saturate(_glow), _LavaGlowB.x);

    // 천천히 일렁인다(빛 무늬가 조금씩 밝아졌다 어두워진다).
    if (_LavaGlowB.y > 0.0)
    {
        float flicker = LavaGlowNoise(_texelCenterWS / _LavaGlowB.w + float2(_Time.y * _LavaGlowB.z, -_Time.y * _LavaGlowB.z * 0.7));
        glow *= 1.0 + _LavaGlowB.y * (flicker * 2.0 - 1.0);
    }

    // 텍셀 단위로 몇 단계로 끊는다. 단계 사이는 베이어 문턱으로 섞어 계단이 매끈한 띠로 보이지 않게 한다.
    float steps = max(_LavaGlowA.w, 1.0);
    float level = saturate(floor(saturate(glow) * steps + LavaGlowBayer4(_texelId)) / steps) * _strength;
    if (level <= 0.0)
    {
        return _color;
    }

    half3 color = _color * (half)(1.0 + _LavaGlowA.z * level);
    color += (half3)(_LavaGlowColor.rgb * (_LavaGlowA.y * level));
    return color;
}

half3 ApplyLavaGlow(half3 _color, float2 _texelCenterWS)
{
    float2 texelId = floor(_texelCenterWS * StageAmbiencePixelsPerUnit());
    return ApplyLavaGlowLevel(_color, SampleLavaGlow(_texelCenterWS), _texelCenterWS, texelId, 1.0);
}

// 서 있는 그림(나무·캐릭터). _groundY: 그림이 서 있는 발밑 월드 높이, _texelId: 베이어 문턱용 텍셀 번호.
// 발밑 높이·픽셀 가로 위치의 바닥 빛을 읽고, 발밑에서 위로 _LavaGlowObject.y만큼 올라가며 옅어진다.
// 꺼진 스테이지(_LavaGlowA.x = 0)나 배율 0이면 텍스처를 읽지 않고 그대로 돌려준다.
half3 ApplyLavaGlowUpright(half3 _color, float2 _texelCenterWS, float _groundY, float2 _texelId)
{
    if (false == IsLavaGlowEnabled() || _LavaGlowObject.x <= 0.0)
    {
        return _color;
    }

    float height = max(_texelCenterWS.y - _groundY, 0.0);
    float fade = saturate(1.0 - height / max(_LavaGlowObject.y, 0.1));
    if (fade <= 0.0)
    {
        return _color;
    }

    float glow = SampleLavaGlow(float2(_texelCenterWS.x, _groundY)) * fade;
    return ApplyLavaGlowLevel(_color, glow, _texelCenterWS, _texelId, _LavaGlowObject.x);
}

// 픽셀 스냅 없이 움직이는 렌더러(캐릭터, Custom-Sprite-Default-NoSnap)용.
// _worldPos: xy = 픽셀 월드 위치, zw = 렌더러 기준점 월드 위치.
// _receiver: x = 켜짐(0/1, 받기로 켠 렌더러만 MaterialPropertyBlock으로 1), y = 기준점에서 발밑까지 내려가는 높이(월드).
// 텍셀 번호·텍셀 가운데를 기준점 기준으로 잡아, 움직이는 동안 베이어 무늬가 그림에 붙어 다니고 꿈틀대지 않게 한다.
half3 ApplyLavaGlowReceiver(half3 _color, float4 _worldPos, float4 _receiver)
{
    if (_receiver.x < 0.5)
    {
        return _color;
    }

    float ppu = StageAmbiencePixelsPerUnit();
    float2 local = _worldPos.xy - _worldPos.zw;
    float2 texelId = floor(local * ppu);
    float2 texelCenterWS = _worldPos.zw + (texelId + 0.5) / ppu;
    return ApplyLavaGlowUpright(_color, texelCenterWS, _worldPos.w - _receiver.y, texelId);
}

#endif // LAVA_GLOW_INCLUDED
