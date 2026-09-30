Shader "Custom/TreeHeatIndicator"
{
    // 나무 열기 발산 예고 인디케이터. 등각(가로:세로 = 2:1) 타원 테두리 + 반투명 채움을 그린다.
    // 채움은 _Progress(0~1)에 맞춰 중심에서 바깥으로 차오르며, 가득 차는 순간 열기가 방출된다.
    // 쿼드는 TreeHeatIndicator가 만드는 1x1 FullRect 스프라이트를 쓰므로 UV가 항상 0~1 전체를 덮는다.
    Properties
    {
        [HDR] _RingColor("Ring Color", Color) = (1, 0.06, 0.03, 1)
        [HDR] _FillColor("Fill Color", Color) = (1, 0.32, 0.04, 1)
        _FillAlpha("Fill Alpha (Idle)", Range(0, 1)) = 0.25
        _ChargedFillAlpha("Fill Alpha (Charged)", Range(0, 1)) = 0.6
        _RingThickness("Ring Thickness (Pixels)", Float) = 2
        _PPU("Pixels Per Unit", Float) = 32

        // 아래 값들은 TreeHeatIndicator가 MaterialPropertyBlock으로 인스턴스마다 넣는다.
        [HideInInspector] _RadiusX("Radius X (Units)", Float) = 1.5
        [HideInInspector] _QuadSize("Quad Size (Units)", Vector) = (3.25, 1.75, 0, 0)
        [HideInInspector] _Progress("Progress", Range(0, 1)) = 0
        [HideInInspector] _Alpha("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half4 _FillColor;
                float _FillAlpha;
                float _ChargedFillAlpha;
                float _RingThickness;
                float _PPU;
                float _RadiusX;
                float4 _QuadSize;
                float _Progress;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 쿼드 중심 기준 월드 단위 좌표를 픽셀 격자에 맞춰 도트 느낌의 계단 테두리를 만든다.
                float ppu = max(_PPU, 1.0);
                float2 p = (IN.uv - 0.5) * _QuadSize.xy;
                p = (floor(p * ppu) + 0.5) / ppu;

                float rx = max(_RadiusX, 0.0001);
                float ry = rx * 0.5;
                float outer = (p.x * p.x) / (rx * rx) + (p.y * p.y) / (ry * ry);
                clip(1.0 - outer);

                // 안쪽 타원 반지름을 가로/세로 모두 같은 픽셀 수만큼 줄여, 위아래 테두리가 얇아지지 않게 한다.
                float thickness = _RingThickness / ppu;
                float irx = max(rx - thickness, 0.0001);
                float iry = max(ry - thickness, 0.0001);
                float inner = (p.x * p.x) / (irx * irx) + (p.y * p.y) / (iry * iry);
                float ringMask = step(1.0, inner);

                // 등각 거리(0 = 중심, 1 = 테두리)가 진행도 안쪽이면 진하게 채운다.
                float isoDist = sqrt(outer);
                float charged = step(isoDist, _Progress);
                float fillAlpha = lerp(_FillAlpha, _ChargedFillAlpha, charged) * _FillColor.a;

                half4 color = ringMask > 0.5
                    ? half4(_RingColor.rgb, _RingColor.a)
                    : half4(_FillColor.rgb, fillAlpha);
                color.a *= _Alpha;
                clip(color.a - 0.003);
                return color;
            }
            ENDHLSL
        }
    }
}
