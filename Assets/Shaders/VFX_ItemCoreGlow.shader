Shader "URP2D/VFX_ItemCoreGlow"
{
    Properties
    {
        [Header(Blend Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1 // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1 // One (Additive)

        [Header(Color and Intensity)]
        [HDR] _CoreColor ("Core Color (Center HDR)", Color) = (3.5, 3.2, 1.8, 1.0)
        [HDR] _OuterColor ("Outer Glow Color (HDR)", Color) = (2.4, 1.4, 0.25, 1.0)
        _Intensity ("Overall Intensity", Range(0.0, 10.0)) = 1.0

        [Header(Radius and Falloff)]
        _CoreRadius ("Core Radius (0 to 1)", Range(0.01, 0.8)) = 0.25
        _GlowRadius ("Glow Radius (0 to 1)", Range(0.05, 1.0)) = 0.85
        _Falloff ("Falloff Exponent", Range(0.5, 5.0)) = 2.0

        [Header(Breathing Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0.0, 0.3)) = 0.08

        [Header(Pixel Perfect Settings)]
        [Toggle(_PIXELATE_ON)] _PixelateEnabled ("Enable Pixel Style", Float) = 1.0
        _PixelResolution ("Pixel Grid Resolution", Range(8.0, 128.0)) = 32.0

        [Header(Optional Texture Mask)]
        _MainTex ("Sprite / Texture Mask (White for default)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ItemCoreGlow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _PIXELATE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _OuterColor;
                float _Intensity;
                float _CoreRadius;
                float _GlowRadius;
                float _Falloff;
                float _PulseSpeed;
                float _PulseAmount;
                float _PixelateEnabled;
                float _PixelResolution;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 스프라이트 중심 (0,0), 외곽 (-1 ~ +1) 로컬 정규 좌표계
                float2 localPos = (input.uv - float2(0.5, 0.5)) * 2.0;

                // 1. 픽셀 그리드 양자화 스냅
                #if defined(_PIXELATE_ON)
                if (_PixelateEnabled > 0.5)
                {
                    float halfRes = max(2.0, _PixelResolution * 0.5);
                    localPos = (floor(localPos * halfRes) + 0.5) / halfRes;
                }
                #endif

                // 2. 중심으로부터의 원형 거리
                float dist = length(localPos);

                // 3. 호흡 맥동(Pulse)
                float time = _Time.y;
                float pulse = sin(time * _PulseSpeed) * _PulseAmount;
                float dynamicGlowRadius = clamp(_GlowRadius + pulse, 0.05, 1.0);
                float dynamicCoreRadius = max(0.01, _CoreRadius * (dynamicGlowRadius / max(0.01, _GlowRadius)));

                // 4. 사각형 영역 100% 완전 제거 (원형 바깥은 렌더링 즉시 폐기)
                if (dist > dynamicGlowRadius)
                {
                    discard;
                }

                // 5. 코어 및 외곽 글로우 감쇠
                float coreFactor = saturate(1.0 - (dist / dynamicCoreRadius));
                coreFactor = pow(coreFactor, 1.8);

                float glowFactor = saturate(1.0 - (dist / dynamicGlowRadius));
                glowFactor = pow(glowFactor, max(0.1, _Falloff));

                #if defined(_PIXELATE_ON)
                if (_PixelateEnabled > 0.5)
                {
                    // 픽셀 아트 4단계 밴딩
                    glowFactor = ceil(glowFactor * 4.0) / 4.0;
                }
                #endif

                // 6. 2중 HDR 컬러 합성
                half4 coreTerm = _CoreColor * coreFactor;
                half4 glowTerm = _OuterColor * glowFactor;
                half4 finalColor = (coreTerm + glowTerm) * _Intensity;

                // 7. 정점 색상 반영
                finalColor *= input.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
