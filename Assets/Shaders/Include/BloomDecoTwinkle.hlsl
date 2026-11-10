#ifndef BLOOM_DECO_TWINKLE_INCLUDED
#define BLOOM_DECO_TWINKLE_INCLUDED

// 바닥 꽃버섯 반짝임. 블룸 데코 타일맵의 장식들이 숲을 따라 느리게 이어지는 물결에 맞춰 하나씩 잠깐 밝아졌다 꺼진다.
// 물결 박자는 월드 위치의 부드러운 노이즈로 정해 이웃 장식끼리 차례로 반짝이고, 장식 하나(1칸 미만)는 거의 같은 박자라 통째로 밝아진다.
// BloomDecoTwinkle 셰이더(2스테이지 블룸 데코 타일맵 전용 머티리얼)만 include 한다.

float BloomDecoTwinkleHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float BloomDecoTwinkleNoise(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = BloomDecoTwinkleHash21(i);
    float b = BloomDecoTwinkleHash21(i + float2(1.0, 0.0));
    float c = BloomDecoTwinkleHash21(i + float2(0.0, 1.0));
    float d = BloomDecoTwinkleHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// _amount: 가장 밝을 때 더하는 밝기 비율, _speed: 박자 속도(라디안/초), _scale: 물결 크기(월드), _sharpness: 클수록 반짝이는 순간이 짧다
half3 ApplyBloomDecoTwinkle(half3 _color, float2 _positionWS, float _amount, float _speed, float _scale, float _sharpness)
{
    float phase = BloomDecoTwinkleNoise(_positionWS / max(_scale, 0.5)) * 9.424778 + _Time.y * _speed;
    float pulse = pow(saturate(sin(phase)), max(_sharpness, 1.0));
    return _color * (1.0h + (half)(_amount * pulse));
}

#endif // BLOOM_DECO_TWINKLE_INCLUDED
