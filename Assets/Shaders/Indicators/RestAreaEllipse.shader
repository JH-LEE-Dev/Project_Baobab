Shader "Custom/RestAreaEllipse"
{
    // 휴식 특성의 휴식 범위 타원. 오브젝트 스케일이 곧 타원 지름(2rx, 2ry)이고, UV 0~1 쿼드 위에 그린다.
    // 월드 좌표를 PPU 격자로 스냅해서 외곽선이 정수 픽셀에 맞게 끊긴다.
    //
    // 화면 캡처(_CameraSortingLayerTexture)를 읽지 않는다. 캡처에는 Objects 레이어(나무, 오프로드)가 이미 들어 있어서
    // 읽는 순간 사물까지 같이 일그러지기 때문이다. 바닥을 일렁이게 하지 않고 옅은 바탕과 빛 파동만 그린다.
    // 그래서 이 쿼드는 Indicators 레이어(Objects 아래)에 그대로 두면 항상 사물 밑에 깔린다.
    //
    // 내부는 계단 없이 그린다. 바탕은 아주 옅게 덮고(알파), 파동은 바닥을 덮지 않고 밝히기만 한다(가산).
    // 덮기와 가산을 한 패스로 내기 위해 프리멀티플라이드 블렌드(One, OneMinusSrcAlpha)를 쓴다.
    Properties
    {
        [Header(Wave)]
        _WaveStrength("Wave Strength", Range(0, 1)) = 1
        _WavePeriod("Wave Interval (Seconds)", Float) = 4
        _WaveAlpha("Wave Additive Alpha", Range(0, 0.4)) = 0.04

        [Header(Inner)]
        _BaseAlpha("Base Alpha", Range(0, 0.15)) = 0.025

        [Header(Reveal)]
        _RevealWaveAlpha("First Wave Additive Alpha", Range(0, 0.3)) = 0.04

        [Header(Pixel)]
        _PixelsPerUnit("Pixels Per Unit", Float) = 32

        [Header(Runtime)]
        _Fade("Fade", Range(0, 1)) = 1
        _Reveal("Reveal Progress (First Wave)", Range(0, 1)) = 1
        _WaveGate("Wave Gate", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _WaveStrength;
                float _WavePeriod;
                float _WaveAlpha;
                float _BaseAlpha;
                float _RevealWaveAlpha;
                float _PixelsPerUnit;
                float _Fade;
                float _Reveal;
                float _WaveGate;
            CBUFFER_END

            // 외곽 레이저선 코어(1px)와 보조선(안쪽 1px). 파동이 닿을수록 한 단계씩 밝아진다. 값은 sRGB.
            static const float3 kCore[4] =
            {
                float3(0.310, 0.769, 0.408), float3(0.361, 0.800, 0.447),
                float3(0.416, 0.831, 0.486), float3(0.490, 0.863, 0.557)
            };
            static const float3 kSub[4] =
            {
                float3(0.122, 0.478, 0.220), float3(0.165, 0.580, 0.275),
                float3(0.208, 0.651, 0.314), float3(0.271, 0.722, 0.369)
            };
            // 내부 바탕(따뜻한 연두)과 파동 빛(밝은 연두). 값은 sRGB.
            static const float3 kBaseColor = float3(0.56, 0.80, 0.30);
            static const float3 kWaveColor = float3(0.84, 0.96, 0.52);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 윤곽 픽셀은 쿼드 가장자리에서 반 픽셀 밖에 있을 수 있고 중심 스냅에 따라 최대 1px 더 밀리므로, 쿼드를 모서리 쪽으로 2px 넓혀 윤곽이 잘리지 않게 한다.
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                worldPos.xy += float2(IN.positionOS.x < 0.0 ? -1.0 : 1.0, IN.positionOS.y < 0.0 ? -1.0 : 1.0) * (2.0 / max(_PixelsPerUnit, 1.0));
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.positionWS = worldPos;
                return OUT;
            }

            // 프로젝트 색 공간이 Linear라 sRGB로 정한 팔레트 값을 선형으로 바꿔서 낸다.
            float3 SrgbToLinear(float3 _c)
            {
                return _c * (_c * (_c * 0.305306011 + 0.682171111) + 0.012522878);
            }

            float Lvl4(float _w)
            {
                return _w < 0.2 ? 0.0 : (_w < 0.45 ? 1.0 : (_w < 0.7 ? 2.0 : 3.0));
            }

            // 1px 타원 윤곽. PixelEllipse(midpoint)와 같은 픽셀 집합이다(반지름 192x96, 191x95에서 0픽셀 차이를 확인).
            // 가파른 구간(좌우 끝)은 행마다 x를, 완만한 구간(위아래)은 열마다 y를 반올림해 한 픽셀만 고르므로
            // 8방향으로만 이어지는 한 줄이 되고 L자 모서리 픽셀이 생기지 않는다(JAGGIES, DOUBLE PIXEL).
            bool ThinEdge(float2 _q, float2 _r)
            {
                float2 aq = abs(_q);
                bool bSteep = aq.y * _r.x * _r.x < aq.x * _r.y * _r.y;
                if (true == bSteep)
                {
                    float t = 1.0 - (aq.y / _r.y) * (aq.y / _r.y);
                    return 0.0 <= t && aq.x == round(_r.x * sqrt(t));
                }

                float t2 = 1.0 - (aq.x / _r.x) * (aq.x / _r.x);
                return 0.0 <= t2 && aq.y == round(_r.y * sqrt(t2));
            }

            // 중앙에서 퍼지는 빔 파동. 두 줄이 반 주기씩 엇갈리고, 앞쪽은 날카롭고 뒤쪽은 꼬리가 길다.
            float WaveAt(float _r)
            {
                float best = 0.0;
                float period = max(_WavePeriod, 0.05);
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float ph = frac(_Time.y / (period * 2.0) + k * 0.5);
                    float head = (1.0 - pow(1.0 - ph, 1.3)) * 1.15;
                    float amp = min(1.0, ph * 4.0) * (1.0 - ph * ph);
                    float d = _r - head;
                    float s = d > 0.0 ? 0.1 : 0.26;
                    best = max(best, exp(-(d / s) * (d / s)) * amp * _WaveStrength);
                }
                return best;
            }

            // 열릴 때 한 번 지나가는 첫 파동. 진행도(_Reveal) 0~1 동안 중앙에서 외곽 너머까지 가며, 시작과 끝은 세기 0이다.
            float FirstWaveAt(float _r)
            {
                if (1.0 <= _Reveal) return 0.0;

                float x = saturate(_Reveal);
                float u = 1.0 - x;
                float head = (1.0 - u * u * u) * 1.05;
                float d = (_r - head) / 0.08;
                return exp(-d * d) * _RevealWaveAlpha * sin(PI * x);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 오브젝트 스케일 = 타원 지름. 반지름은 그 절반.
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleY = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                float2 radius = max(float2(scaleX, scaleY) * 0.5, 0.0001);
                float2 center = float2(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w);

                // 중심도 픽셀 격자에 맞춰 두면 타원 윤곽이 좌우, 상하 대칭으로 깔끔하게 끊긴다.
                float ppu = max(_PixelsPerUnit, 1.0);
                float2 centerSnapped = (floor(center * ppu) + 0.5) / ppu;
                float2 snapped = (floor(IN.positionWS.xy * ppu) + 0.5) / ppu;
                // 윤곽은 PixelEllipse와 같은 픽셀(반지름 radiusPx). 채우는 영역은 윤곽 안쪽을 다 덮도록 반 픽셀 큰 연속 타원으로 정한다.
                float2 pix = round((snapped - centerSnapped) * ppu);
                float2 radiusPx = radius * ppu;
                float2 p = pix / (radiusPx + 0.5);

                float d2 = dot(p, p);
                float waveEdge = WaveAt(1.0) * _WaveGate;

                // 코어: 1px 윤곽. 반지름 안쪽 한 겹 윤곽을 보조선으로 겹쳐 2px 띠(레이저 느낌)를 만든다. 보조선도 같은 방식이라 L자 모서리가 없다.
                // 프리멀티플라이드 블렌드라 색에 알파(_Fade)를 곱해서 낸다.
                if (ThinEdge(pix, radiusPx))
                {
                    return half4(SrgbToLinear(kCore[(int)Lvl4(waveEdge * 1.1)]) * _Fade, _Fade);
                }

                clip(1.0 - d2);

                if (ThinEdge(pix, max(radiusPx - 1.0, 1.0)))
                {
                    return half4(SrgbToLinear(kSub[(int)Lvl4(waveEdge)]) * _Fade, _Fade);
                }

                float r = sqrt(d2);

                // 바탕: 계단 없이 고르게 아주 옅게 덮는다.
                float baseA = _BaseAlpha * _Fade;
                // 파동: 덮지 않고 밝히기만 한다. 평소 파동은 다 드러난 뒤 _WaveGate로 켜지고, 첫 파동은 열리는 동안 한 번만 지나간다.
                // 첫 파동은 시작 세기가 0이라 _Fade를 곱하지 않는다(곱하면 드러나기 전 구간에서 거의 사라진다).
                float addA = WaveAt(r) * _WaveAlpha * _WaveGate * _Fade + FirstWaveAt(r);

                clip(max(baseA, addA) - 0.001);
                float3 rgb = SrgbToLinear(kBaseColor) * baseA + SrgbToLinear(kWaveColor) * addA;
                return half4(rgb, baseA);
            }
            ENDHLSL
        }
    }
}
