Shader "Custom/VFX/SpriteBeamFlow"
{
    // 전리품 기둥 빛(VFX_LootPillarGlow)의 빛 줄기 심. SpriteGlowAdditive처럼 텍스처 알파 x 렌더러 색(unity_SpriteColor)을 가산하되,
    // 안쪽 결이 위로 흐르도록 승인된 시뮬레이션과 같은 식으로 밝기를 흔든다.
    //   flow = 0.62 + 0.38 * sin(-hPx * 0.55 + t * 5.2 + sin(u * 2.3) * 1.2) * cos(u * 3.1 - t * 0.7)
    //   h  = 기둥 윗면 0 ~ 꼭대기 1 (텍스처 아래 끝은 _BottomH), hPx = h * _HeightPx
    //   u  = 가로 -1 ~ 1을 위로 갈수록 좁아지는 폭(1 - 0.35h)으로 나눈 값
    // 텍스처 알파(심 마스크)가 결과 알파를 정하므로, 흐름 계산 전에 버릴 픽셀을 먼저 discard한다(가이드 10.8).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Glow ("Glow (HDR 발광 배율, 1 = 그대로)", Float) = 1.0
        _HeightPx ("빛 줄기 높이(px, h = 1)", Float) = 46.0
        _BottomH ("텍스처 아래 끝의 h (기둥 윗면 아래로 내려온 비율, 음수)", Float) = -0.0435
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

        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float _Glow;
            float _HeightPx;
            float _BottomH;
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
            output.color = input.color * unity_SpriteColor; // Unity 6 URP: SpriteRenderer.color는 정점 색이 아니라 unity_SpriteColor로 들어온다
            output.uv = input.uv;
            return output;
        }

        half4 frag(Varyings input) : SV_Target
        {
            half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            half alpha = mask * (half)input.color.a;
            if (alpha < (half)(1.0 / 510.0)) discard;

            float h = lerp(_BottomH, 1.0, input.uv.y);
            float u = (input.uv.x * 2.0 - 1.0) / (1.0 - 0.35 * saturate(h));
            float t = _Time.y;
            float flow = 0.62 + 0.38 * sin(-h * _HeightPx * 0.55 + t * 5.2 + sin(u * 2.3) * 1.2) * cos(u * 3.1 - t * 0.7);

            return half4(input.color.rgb * (half)(flow * _Glow), alpha);
        }
        ENDHLSL

        Pass
        {
            Name "SpriteBeamFlowPass"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            ENDHLSL
        }

        Pass
        {
            Name "SpriteBeamFlowForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
