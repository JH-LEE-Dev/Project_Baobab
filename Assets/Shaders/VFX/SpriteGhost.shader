Shader "Custom/VFX/SpriteGhost"
{
    // 스프라이트를 라이팅 없이 한 가지 색(SpriteRenderer.color의 RGB)의 실루엣으로 그린다. 대시 잔상(DashAfterimage)용.
    // 원래 색은 버리고 밝기만 조금 남겨(_Detail) 실루엣 안의 윤곽이 읽히게 한다. 알파는 원본 알파 x 렌더러 알파.
    // 결과 밝기는 1.1배를 넘지 않아 후처리 블룸(임계값 1.345)을 받지 않는다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Detail ("Detail (원본 밝기를 남기는 정도, 0 = 완전 단색)", Range(0, 1)) = 0.5
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
            Name "SpriteGhostPass"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Detail;
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half shade = lerp((half)1.0, lerp((half)0.75, (half)1.1, luminance), (half)_Detail);
                return half4(input.color.rgb * shade, tex.a * input.color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SpriteGhostForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Detail;
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half shade = lerp((half)1.0, lerp((half)0.75, (half)1.1, luminance), (half)_Detail);
                return half4(input.color.rgb * shade, tex.a * input.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
