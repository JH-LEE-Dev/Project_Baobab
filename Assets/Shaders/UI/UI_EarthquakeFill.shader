Shader "UI/EarthquakeFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _LiquidState ("Liquid State", Vector) = (22,0,0,1)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                half4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _SpriteUVRect, _LiquidState, _ClipRect;

            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.localPosition = v.vertex.xy;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = (i.uv - _SpriteUVRect.xy) / (_SpriteUVRect.zw - _SpriteUVRect.xy);
                float2 p = clamp(floor(uv * 64), 0, 63);
                p.y -= _LiquidState.x;
                float time = _LiquidState.y;
                float level = _LiquidState.z;
                float wave = 0.72 * sin(p.x * 0.34 - time * 3.7) + 0.50 * sin(p.x * 0.79 + time * 2.3);
                float surface = clamp(round(level + wave * saturate(min(level, 20 - level) * 0.5)), 0, 20);
                float depth = surface - 1.0 - p.y;
                half liquid = step(0.0, p.y) * step(0.0, depth);
                half alpha = tex2D(_MainTex, i.uv).a * liquid;
                clip(alpha - 0.001h);

                float flow = sin(p.x * 0.47 + p.y * 0.61 - time * 1.5)
                    + 0.58 * sin(p.x * 0.91 - p.y * 0.42 + time * 2.1);
                float heat = saturate(0.44 + 0.24 * flow + 0.33 * saturate(1.0 - depth / max(1.0, surface)));
                heat = floor(heat * 6.0) / 6.0;
                half3 rock = half3(0.072, 0.009, 0.019);
                half3 magma = half3(0.72, 0.064, 0.012);
                half3 ember = half3(1.0, 0.47, 0.07);
                half3 color = heat < 0.58 ? lerp(rock, magma, heat / 0.58)
                    : lerp(magma, ember, (heat - 0.58) / 0.42);
                if (level < 20.0 && depth < 0.5)
                    color = lerp(ember, half3(1.0, 0.76, 0.25), saturate(flow * 0.38 + 0.44));

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001h);
                #endif
                return half4(color * _LiquidState.w * i.color.rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
