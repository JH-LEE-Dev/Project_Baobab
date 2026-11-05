Shader "Custom/UI/UIBeamFlow"
{
    // 상세 HUD 전리품 빛의 빛 줄기 심(UGUI). 가산이며 rgb = 정점 rgb x (정점 알파 x _GlowMax) x flow.
    // flow는 마을 기둥 빛의 SpriteBeamFlow와 같은 식이다(안쪽 결이 위로 흐름):
    //   flow = 0.62 + 0.38 * sin(-hPx * 0.55 + t * 5.2 + sin(u * 2.3) * 1.2) * cos(u * 3.1 - t * 0.7)
    // 텍스처 알파(심 마스크) x 정점 알파가 1/510 미만이면 흐름 계산 전에 버린다(가이드 10.8).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GlowMax ("Glow Max (정점 알파 1의 발광 배율)", Float) = 2.5
        _HeightPx ("빛 줄기 높이(결 계산용 px, h = 1)", Float) = 46.0
        _BottomH ("텍스처 아래 끝의 h (음수)", Float) = -0.0435

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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIBeamFlow"
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
            float _GlowMax;
            float _HeightPx;
            float _BottomH;
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
                half alpha = shape;

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                float h = lerp(_BottomH, 1.0, IN.texcoord.y);
                float u = (IN.texcoord.x * 2.0 - 1.0) / (1.0 - 0.35 * saturate(h));
                float t = _Time.y;
                float flow = 0.62 + 0.38 * sin(-h * _HeightPx * 0.55 + t * 5.2 + sin(u * 2.3) * 1.2) * cos(u * 3.1 - t * 0.7);
                half3 rgb = IN.color.rgb * (half)(IN.color.a * _GlowMax * flow);
                return half4(rgb, alpha);
            }
            ENDCG
        }
    }
}
