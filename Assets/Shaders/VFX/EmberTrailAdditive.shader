Shader "Custom/VFX/EmberTrailAdditive"
{
    // 4스테이지 불씨 드랍 빛 꼬리(OverheatEmberTrail) 전용 가산 셰이더.
    // 쿼드마다 UV(-2~2, 반지름 단위)로 가우시안 원판을 그린다. 출력 = 정점 색(RGB) x 정점 알파(세기) x _Intensity x exp(-|uv|^2).
    // 정점 색은 선형 목표 색, 정점 알파는 세기 / _Intensity로 넣는다(Color32로 1 넘는 HDR 세기를 담기 위해).
    Properties
    {
        _Intensity ("Intensity (정점 알파 1일 때의 HDR 세기)", Float) = 2.0
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
            Name "EmberTrailAdditivePass"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = (half4)input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float d2 = dot(input.uv, input.uv);
                // 반지름 2배 밖(exp(-4) 미만)은 버린다. 시안의 원판 범위와 같다.
                if (4.0 < d2) discard;

                half k = (half)exp(-d2) * input.color.a * (half)_Intensity;
                if (0.001h > k) discard;
                return half4(input.color.rgb * k, 0.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "EmberTrailAdditiveUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = (half4)input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float d2 = dot(input.uv, input.uv);
                if (4.0 < d2) discard;

                half k = (half)exp(-d2) * input.color.a * (half)_Intensity;
                if (0.001h > k) discard;
                return half4(input.color.rgb * k, 0.0h);
            }
            ENDHLSL
        }
    }
}
