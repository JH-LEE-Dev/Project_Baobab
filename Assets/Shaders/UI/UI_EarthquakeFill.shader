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

            float HashCell(float2 cell)
            {
                float2 wrapped = cell - 289.0 * floor(cell / 289.0);
                float seed = fmod(dot(wrapped, float2(37.0, 71.0)), 289.0);
                return fmod(seed * seed * 13.0 + seed * 17.0, 289.0) / 289.0;
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 fraction = frac(samplePosition);
                float2 blend = fraction * fraction * (3.0 - 2.0 * fraction);
                float bottom = lerp(HashCell(cell), HashCell(cell + float2(1, 0)), blend.x);
                float top = lerp(HashCell(cell + float2(0, 1)), HashCell(cell + 1.0), blend.x);
                return lerp(bottom, top, blend.y);
            }

            float LavaFlow(float2 pixel, float time)
            {
                float2 samplePoint = float2((pixel.x - 32.0) * 0.22, pixel.y * 0.26);
                float eddy = ValueNoise(samplePoint * 0.72 + float2(9.3, time * 0.25)) - 0.5;
                float2 current = samplePoint + float2(eddy * 1.45, -time * 0.54 + eddy * 0.5);
                float body = ValueNoise(current);
                float veins = ValueNoise(current * 1.85 + float2(6.1, 3.4));
                return (body - 0.5) * 2.0 + (veins - 0.5) * 0.65;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = (i.uv - _SpriteUVRect.xy) / (_SpriteUVRect.zw - _SpriteUVRect.xy);
                float2 p = clamp(floor(uv * 64), 0, 63);
                p.y -= _LiquidState.x;
                float time = _LiquidState.y;
                float level = _LiquidState.z;
                float wave = (ValueNoise(float2((p.x - 32.0) * 0.20 - time * 0.36, 17.6 + time * 0.23)) - 0.5) * 2.8;
                float surface = clamp(round(level + wave * saturate(min(level, 20 - level) * 0.5)), 0, 20);
                float depth = surface - 1.0 - p.y;
                half liquid = step(0.0, p.y) * step(0.0, depth);
                half alpha = tex2D(_MainTex, i.uv).a * liquid;
                clip(alpha - 0.001h);

                float flow = LavaFlow(p, time);
                float heat = saturate(0.44 + 0.36 * flow + 0.33 * saturate(1.0 - depth / max(1.0, surface)));
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
