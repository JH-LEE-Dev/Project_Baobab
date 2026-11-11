#ifndef BLOOM_DECO_BREATH_INCLUDED
#define BLOOM_DECO_BREATH_INCLUDED

// 불씨 데코 숨쉬기. 블룸 데코 타일맵의 장식(불씨 바위 등)마다 제 박자로 불빛이 은은하게 밝아졌다 돌아오고,
// 그 장식에서 증기가 뿜어지는 순간에는 잠깐 더 밝아진다.
// 장식 하나의 네 꼭짓점은 "월드 위치 - UV × 그림 크기"가 모두 같은 점(그림 왼쪽 아래)이 되므로, 그 점으로 장식마다
// 고정된 박자를 정한다(이웃 장식끼리 박자가 따로 논다). 그림이 아틀라스에 묶이지 않은 낱장(FullRect)이고
// 뒤집지 않는다는 전제다. 계산은 정점에서 한 번만 하고 픽셀에는 배율 하나만 넘긴다.
// BloomDecoBreath 셰이더(4스테이지 블룸 데코 타일맵 전용 머티리얼)만 include 한다.

// 증기 뿜는 장식: xy = 장식 가운데(월드), z = 밝아지는 세기(0~1, StageDecoSteam이 매 프레임 갱신), w = 쓰지 않음
#define BLOOM_DECO_STEAM_FLASH_COUNT 4
float4 _DecoSteamFlash[BLOOM_DECO_STEAM_FLASH_COUNT];

float BloomDecoBreathHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 불빛 HDR 배율에 곱할 값(1 = 그대로).
// _uv: 그림 UV, _positionWS: 꼭짓점 월드 위치, _spriteWorldSize: 그림 크기(월드),
// _amount: 가장 밝을 때 더하는 비율, _speed: 박자 속도(라디안/초, 장식마다 0.75~1.25배),
// _sharpness: 클수록 밝아져 있는 순간이 짧다, _flashAmount: 증기 뿜을 때 더하는 비율, _flashRadius: 같은 장식으로 볼 거리(월드)
half BloomDecoBreathBoost(float2 _uv, float2 _positionWS, float2 _spriteWorldSize,
    float _amount, float _speed, float _sharpness, float _flashAmount, float _flashRadius)
{
    float2 center = _positionWS - _uv * _spriteWorldSize + _spriteWorldSize * 0.5;
    // 같은 장식의 꼭짓점끼리 아주 작은 오차가 나도 같은 값이 나오게 1/32 격자로 맞춘 뒤 난수를 만든다.
    float2 key = floor(center * 32.0 + 0.5);
    float phase = BloomDecoBreathHash21(key) * 6.2831853;
    float speedScale = lerp(0.75, 1.25, BloomDecoBreathHash21(key + 17.0));
    float pulse = pow(saturate(sin(_Time.y * _speed * speedScale + phase)), max(_sharpness, 1.0));

    float flash = 0.0;
    float radiusSq = _flashRadius * _flashRadius;
    [unroll]
    for (int i = 0; i < BLOOM_DECO_STEAM_FLASH_COUNT; i++)
    {
        float2 d = center - _DecoSteamFlash[i].xy;
        flash = max(flash, dot(d, d) < radiusSq ? _DecoSteamFlash[i].z : 0.0);
    }

    return (half)(1.0 + _amount * pulse + _flashAmount * flash);
}

#endif // BLOOM_DECO_BREATH_INCLUDED
