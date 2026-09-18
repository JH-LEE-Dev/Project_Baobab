Shader "Custom/VFX/URP2D_HeatHaze"
{
    Properties
    {
        [MainTexture] _MainTex ("Base Mask Texture (White for Full Quad)", 2D) = "white" {}
        
        [Header(Distortion Settings)]
        _Intensity ("Intensity (Fade 0 to 1)", Range(0, 1)) = 1.0
        _DistortionStrength ("Distortion Strength", Range(0, 0.2)) = 0.03
        _Speed ("Haze Rise Speed", Float) = 2.0
        _Frequency ("Haze Wave Frequency", Float) = 12.0
        _WobbleAmount ("Horizontal Wobble Amount", Range(0, 0.1)) = 0.02
        
        [Header(Soft Mask Settings)]
        _MaskBottom ("Bottom Fade Start", Range(0, 0.5)) = 0.08
        _MaskTop ("Top Fade End", Range(0.3, 1.0)) = 0.75
        _MaskSides ("Sides Fade Width", Range(0, 0.5)) = 0.25

        [Header(Pixel Art Style)]
        [Toggle] _PixelSnap ("Enable Pixel Grid Snap", Float) = 0
        _PixelResolution ("Pixel Grid Resolution", Float) = 128.0

        [Header(Heat Tint)]
        [HDR] _HeatColor ("Heat Tint Color (Alpha = Blend)", Color) = (1.0, 0.45, 0.15, 0.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float4 screenPos    : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // URP 2D Camera Sorting Layer Texture (배경 캡처 버퍼)
            TEXTURE2D(_CameraSortingLayerTexture); 
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _DistortionStrength;
                float _Speed;
                float _Frequency;
                float _WobbleAmount;
                float _MaskBottom;
                float _MaskTop;
                float _MaskSides;
                float _PixelSnap;
                float _PixelResolution;
                float4 _HeatColor;
            CBUFFER_END

            // 절차적 2D 노이즈 함수
            float random(float2 p) 
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 p) 
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 강도가 0에 가까우면 연산 생략 (완전 투명)
                if (_Intensity <= 0.001)
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // 1. 사각형 판넬 테두리를 부드럽게 깎아주는 소프트 마스크 계산
                float sideFade = smoothstep(0.0, max(0.001, _MaskSides), i.uv.x) * 
                                 smoothstep(1.0, 1.0 - max(0.001, _MaskSides), i.uv.x);
                float bottomFade = smoothstep(0.0, max(0.001, _MaskBottom), i.uv.y);
                float topFade = smoothstep(1.0, _MaskTop, i.uv.y);
                float shapeMask = sideFade * bottomFade * topFade;

                // 메인 텍스처(스프라이트) 알파 반영
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                shapeMask *= texColor.a;

                // 마스크 영역 밖이면 버림
                if (shapeMask <= 0.001)
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // 2. 위로 스크롤되는 유기적 2중 파동 + 노이즈 계산
                float time = _Time.y * _Speed;
                float wave1 = sin(i.uv.y * _Frequency - time * 3.2 + i.uv.x * 4.5);
                float wave2 = cos(i.uv.y * (_Frequency * 1.5) - time * 2.1 - i.uv.x * 3.2);
                float organicWave = (wave1 + wave2) * 0.5;

                // 불규칙한 대류 현상용 노이즈
                float2 noiseUV = float2(i.uv.x * 3.5, i.uv.y * 2.0 - time * 1.5);
                float convection = (noise(noiseUV * 3.0) * 2.0 - 1.0) * 0.015;

                // 3. 수평/수직 왜곡 오프셋 결정
                float2 wobbleOffset = float2(
                    organicWave * _WobbleAmount + convection,
                    organicWave * 0.01
                );

                // 최종 마스크 및 세기 적용
                float2 finalOffset = wobbleOffset * _DistortionStrength * _Intensity * shapeMask;

                // 4. 픽셀 아트용 픽셀 스냅 옵션
                if (_PixelSnap > 0.5)
                {
                    float res = max(1.0, _PixelResolution);
                    finalOffset = round(finalOffset * res) / res;
                }

                // 5. 배경 화면 왜곡 샘플링 (_CameraSortingLayerTexture)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                screenUV += finalOffset;

                half4 bg = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV);

                // 6. 은은한 열기 틴트 블렌딩 (선택적)
                half3 finalRGB = lerp(bg.rgb, _HeatColor.rgb, _HeatColor.a * shapeMask * _Intensity);

                // 최종 알파
                float finalAlpha = shapeMask * _Intensity * i.color.a;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
