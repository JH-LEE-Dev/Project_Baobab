// 모루 위 원판 도끼 내구도 HUD. 그림은 화면 도끼 HUD의 원판 게이지 스프라이트를 그대로 쓴다
// (바탕 = Gauge.png, 테두리 = Gauge_Outline.png, 채움 = Gauge_Inside.png). 이 셰이더는 채움을 진행도만큼 깎고 밝히기만 한다.
//
// _Mode 0 : 스프라이트를 그대로 그린다(바탕 · 테두리).
// _Mode 1 : 채움 스프라이트를 위(12시)에서 시계 방향으로 _Fill만큼만 그린다. 판정은 텍셀 가운데 기준이라 픽셀 단위로 깎인다.
//           두드릴 때(_Flash) 하얗게 밝아진다.
Shader "Custom/VFX/AnvilRepairGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MainRect ("Sprite UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0,0,1,1)
        _SizePx ("Sprite Size (px)", Vector) = (30,30,0,0)
        _Mode ("Mode (0 sprite, 1 radial fill)", Float) = 0
        _Fill ("Fill (0-1)", Range(0, 1)) = 1
        _Flash ("Flash (0-1)", Range(0, 1)) = 0
        _Brighten ("Flash Brighten Amount", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "AnvilRepairGaugePass"

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
                float4 _MainRect;
                float4 _SizePx;
                float _Mode;
                float _Fill;
                float _Flash;
                float _Brighten;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (_Mode < 0.5)
                {
                    return col;
                }

                if (_Fill < 0.9999)
                {
                    // 텍셀 가운데에서 스프라이트 가운데까지의 방향으로 각도를 잰다(12시 = 0, 시계 방향으로 증가).
                    float2 local = (input.uv - _MainRect.xy) / max(_MainRect.zw - _MainRect.xy, 1e-5) * _SizePx.xy;
                    float2 d = (floor(local) + 0.5) - _SizePx.xy * 0.5;
                    float t = frac(atan2(d.x, d.y) / 6.28318530718 + 1.0);
                    if (t >= _Fill) col.a = 0;
                }

                col.rgb = lerp(col.rgb, half3(1, 1, 1), (half)(saturate(_Flash) * _Brighten));
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
