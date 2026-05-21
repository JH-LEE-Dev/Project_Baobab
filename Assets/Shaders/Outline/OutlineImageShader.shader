Shader "Custom/OutlineImageShader"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        
        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 1
        
        [Header(Shadow)]
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowOffset("Shadow Offset (Pixels)", Vector) = (2, -2, 0, 0)
        
        // UI Masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half4 _ShadowColor;
                float4 _ShadowOffset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _BaseColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // 1. 메인 컬러 샘플링
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * IN.color;
                
                // 2. 아웃라인 로직 (8방향)
                float2 texelSize = _MainTex_TexelSize.xy;
                float2 texelOffset = texelSize * _OutlineWidth;

                half alphaUp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, texelOffset.y)).a;
                half alphaDown = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, texelOffset.y)).a;
                half alphaLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(texelOffset.x, 0)).a;
                half alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texelOffset.x, 0)).a;
                
                half alphaUpLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texelOffset.x, texelOffset.y)).a;
                half alphaUpRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texelOffset.x, texelOffset.y)).a;
                half alphaDownLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texelOffset.x, -texelOffset.y)).a;
                half alphaDownRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texelOffset.x, -texelOffset.y)).a;
                
                half outlineAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)), 
                                        max(max(alphaUpLeft, alphaUpRight), max(alphaDownLeft, alphaDownRight)));

                // 3. 그림자 로직
                float2 shadowUV = uv - (_ShadowOffset.xy * texelSize);
                half shadowAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV).a;

                // 우선순위 결정: 메인 이미지 > 외곽선 > 그림자
                if (mainColor.a > 0.1)
                {
                    return mainColor;
                }
                
                if (outlineAlpha > 0.1)
                {
                    half4 finalOutlineColor = _OutlineColor;
                    finalOutlineColor.a *= IN.color.a;
                    return finalOutlineColor;
                }

                if (shadowAlpha > 0.1)
                {
                    half4 finalShadowColor = _ShadowColor;
                    finalShadowColor.a *= IN.color.a;
                    return finalShadowColor;
                }

                discard;
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
