// 돌풍에 실려 날아가는 꽃잎·잎(StageWindPetals). 파티클 정점색(색·알파)에 꽃잎 그림(점 샘플링)을 곱해 그린다.
// 픽셀아트에 맞게 사각 알갱이의 꼭짓점을 월드 픽셀(1/PPU) 격자에 맞춰, 4픽셀 크기면 그림 한 장이 정확히 4x4 칸을 덮는다.
Shader "Custom/VFX/WindPetal"
{
    Properties
    {
        [NoScaleOffset] _MainTex("꽃잎 그림(StageWindPetals가 넣는다)", 2D) = "white" {}
        _Intensity("밝기 배수", Float) = 1
        _PixelsPerUnit("월드 픽셀 수(PPU)", Float) = 32
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "WindPetalPass"

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
                float _Intensity;
                float _PixelsPerUnit;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float ppu = max(_PixelsPerUnit, 1.0);
                // round(x + n) = round(x) + n 이므로 정수 픽셀 크기의 사각형은 맞춘 뒤에도 같은 크기를 유지한다.
                positionWS.xy = floor(positionWS.xy * ppu + 0.5) / ppu;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 petal = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return half4(input.color.rgb * petal.rgb * _Intensity, input.color.a * petal.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
