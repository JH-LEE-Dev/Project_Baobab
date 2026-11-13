Shader "Custom/VFX/PixelParticleGlow"
{
    // 픽셀 도트 파티클 셰이더(나무 사망 스테이지 이펙트 등).
    // - 모양: _MainTex(도트 모양 시트, Point 필터)의 알파로 자른다. 반투명 없이 또렷한 도트만 남는다.
    // - 명암: 시트의 G(회색 단계)가 입자 색에 곱해져 한 색으로 3~4단 명암을 낸다.
    // - 강조 픽셀: 시트에서 노란색(R=1, B=0)으로 칠한 픽셀은 입자 색 대신 _AccentColor로 그린다(별 조각의 별점, 결정 조각의 달아오른 모서리 등).
    //   한 입자 안에 나무 색 몸통 + 고정 색 강조를 함께 낼 수 있다.
    // - 발광: 정점 알파(0~1)가 발광 세기다(BrandWrapGlow와 같은 규약, 알파는 투명도가 아니다).
    //   _BodyGlow 1: 몸통이 GlowMin~GlowMax로 빛난다. 0: 몸통은 빛나지 않고 정점 알파는 강조 픽셀의 세기(AccentGlowMin~Max)만 정한다.
    // - 격자: 쿼드 꼭짓점을 월드 32 PPU 격자에 맞춘다. 입자 크기가 칸 크기(정수 픽셀)와 같으면 모양이 1px도 번지지 않는다.
    // - _Region: 스테이지 아틀라스 안에서 이 머티리얼이 쓸 영역(텍스처 시트 애니메이션 격자 전체). xy 크기, zw 시작.
    // - _Opacity: 머티리얼 전체의 고정 불투명도(기본 1). 큰 연기 덩이처럼 반투명한 한 색 덩이에만 쓴다. 시간에 따라 바뀌지 않는다.
    Properties
    {
        [NoScaleOffset] _MainTex ("Shape Sheet (Point)", 2D) = "white" {}
        _Region ("Atlas Region (xy 크기, zw 시작)", Vector) = (1, 1, 0, 0)
        _GlowMin ("Glow Min (몸통 HDR 배율, 정점 알파 0)", Float) = 1.0
        _GlowMax ("Glow Max (몸통 HDR 배율, 정점 알파 1)", Float) = 2.6
        _BodyGlow ("Body Glow (1: 정점 알파가 몸통 발광, 0: 강조 픽셀만)", Float) = 1
        _AccentColor ("Accent Color (시트의 노란 픽셀)", Color) = (1, 1, 1, 1)
        _AccentGlowMin ("Accent Glow Min (정점 알파 0)", Float) = 1.0
        _AccentGlowMax ("Accent Glow Max (정점 알파 1)", Float) = 1.0
        _PixelsPerUnit ("Pixels Per Unit", Float) = 32
        _Opacity ("Opacity (고정 불투명도)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelParticleGlowPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Region;
                float _GlowMin;
                float _GlowMax;
                float _BodyGlow;
                half4 _AccentColor;
                float _AccentGlowMin;
                float _AccentGlowMax;
                float _PixelsPerUnit;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // 꼭짓점마다 격자에 맞춘다. 쿼드 폭이 정수 픽셀이면 양 끝이 같은 방향으로 맞춰져 폭이 그대로 유지된다
                // (floor(x + 0.5)로 반올림 방향을 한쪽으로 고정해 .5 경계에서 한쪽만 다르게 맞춰지는 일을 막는다).
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.xy = floor(positionWS.xy * _PixelsPerUnit + 0.5) / _PixelsPerUnit;

                output.positionHCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uv = input.uv * _Region.xy + _Region.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(shape.a - 0.5h);

                float t = saturate(input.color.a);
                float bodyGlow = lerp(_GlowMin, _GlowMax, t * _BodyGlow);
                float3 body = input.color.rgb * shape.g * bodyGlow;

                float accent = saturate(shape.r - shape.b);
                float3 accentColor = _AccentColor.rgb * lerp(_AccentGlowMin, _AccentGlowMax, t);

                return half4(lerp(body, accentColor, accent), (half)_Opacity);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
