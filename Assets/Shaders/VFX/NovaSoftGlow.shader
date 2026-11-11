Shader "Custom/VFX/NovaSoftGlow"
{
    Properties
    {
        _Glow ("Glow (HDR 배율)", Float) = 2.0
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

        Blend One One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "NovaSoftGlowPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Glow;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            // 정점 색(RGB, 색 비율)에 정점 알파(세기)와 _Glow를 곱해 배경에 더한다(Blend One One).
            // 정점 색은 float이라 알파가 1을 넘을 수 있고, 부드러운 그라데이션이 8비트 계단 없이 보간된다.
            // 기여가 8비트 한 단계의 절반(1/510)도 안 되면 화면에 보이지 않으므로 바로 버린다.
            half4 frag(Varyings input) : SV_Target
            {
                float3 glow = input.color.rgb * (input.color.a * _Glow);
                if (max(glow.r, max(glow.g, glow.b)) < (1.0 / 510.0)) discard;

                return half4(glow, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
