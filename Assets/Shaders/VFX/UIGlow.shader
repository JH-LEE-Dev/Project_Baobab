Shader "Custom/UI/UIGlow"
{
    // 상세 HUD 전리품 빛(UI_LootRevealGlow)용 UGUI 빛 셰이더. 텍스처는 알파(빛 모양)만 쓴다.
    // UI 정점 색은 0~1로 잘리므로 HDR 세기를 정점 알파에 싣는다.
    //   _Mode 0 (덮기, Blend SrcAlpha OneMinusSrcAlpha): rgb = 정점 rgb x _TintScale, alpha = 텍스처 알파 x 정점 알파
    //   _Mode 1 (가산, Blend SrcAlpha One):            rgb = 정점 rgb x (정점 알파 x _GlowMax), alpha = 텍스처 알파
    // 정점 알파는 CanvasGroup 알파가 곱해져 들어오므로 HUD 페이드에 맞춰 같이 옅어진다.
    // 알파가 1/510 미만이면 버린다(가이드 10.8). UGUI 마스크(스텐실)·RectMask2D를 지원한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Mode ("Mode (0 덮기, 1 가산)", Float) = 1
        _GlowMax ("Glow Max (가산: 정점 알파 1의 발광 배율)", Float) = 2.5
        _TintScale ("Tint Scale (덮기: 정점 rgb 배율)", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1

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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIGlow"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;
            float _Mode;
            float _GlowMax;
            float _TintScale;
            float _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }

                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half shape = tex2D(_MainTex, IN.texcoord).a;

                #ifdef UNITY_UI_CLIP_RECT
                shape *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                if (shape * IN.color.a < (half)(1.0 / 510.0)) discard;
                half alpha = 0.5 > _Mode ? shape * IN.color.a : shape;

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                half3 rgb = 0.5 > _Mode ? IN.color.rgb * (half)_TintScale : IN.color.rgb * (half)(IN.color.a * _GlowMax);
                return half4(rgb, alpha);
            }
            ENDCG
        }
    }
}
