// 버섯 포자 알갱이. 파티클 정점색(색·알파)을 그대로 쓰고 밝기만 HDR로 올려 블룸이 살짝 번지게 한다.
// 픽셀아트에 맞게 사각 알갱이의 꼭짓점을 월드 픽셀(1/PPU) 격자에 맞춰, 크기가 정수 픽셀이면 정확히 그만큼의 칸을 덮는다.
// StageMushroomSpores의 파티클 시스템이 쓴다.
Shader "Custom/VFX/MushroomSpore"
{
    Properties
    {
        _Intensity("밝기 배수(HDR)", Float) = 2
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
            Name "MushroomSporePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb * _Intensity, input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
