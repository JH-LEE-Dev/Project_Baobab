Shader "UI/MeteorShowerFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _FillState ("Fill State", Vector) = (22,0,0,0)
        _BrightnessMultiplier ("Fill Brightness Multiplier", Float) = 1
        _MeteorStep ("Meteor Step", Float) = -1
        _Starlight ("Starlight HDR Gain", Range(0,1.4)) = 1.08
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
            #pragma target 3.5
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
            float4 _SpriteUVRect, _FillState, _ClipRect;
            float _SurfaceRows[64];
            float4 _Sparks[5];
            float _MeteorStep;
            half _Starlight, _BrightnessMultiplier;

            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.localPosition = v.vertex.xy;
                o.color = v.color;
                return o;
            }

            bool InsideFill(float2 pixel)
            {
                float2 uv = lerp(_SpriteUVRect.xy, _SpriteUVRect.zw, (pixel + 0.5) / 64.0);
                return pixel.y < _SurfaceRows[(int)pixel.x] && tex2Dlod(_MainTex, float4(uv, 0, 0)).a > 0.5;
            }

            half SparkLight(float2 pixel, float4 spark)
            {
                float2 offset = abs(pixel - spark.xy);
                float distance = offset.x + offset.y;
                if (spark.z == 0 || distance > 1)
                    return 0;

                // A cross steps down to a single dot if any arm would leave the liquid.
                bool cross = spark.w > 0 && InsideFill(spark.xy)
                    && InsideFill(spark.xy + float2(1, 0)) && InsideFill(spark.xy - float2(1, 0))
                    && InsideFill(spark.xy + float2(0, 1)) && InsideFill(spark.xy - float2(0, 1));
                if (cross)
                    return distance == 0 ? 1.0h : 0.54h;

                return distance == 0 ? spark.z : 0;
            }

            float2 MeteorPixel(float movement)
            {
                return float2(28 + floor(movement / 3), 16 - movement);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 localUV = (i.uv - _SpriteUVRect.xy) / (_SpriteUVRect.zw - _SpriteUVRect.xy);
                float2 p = clamp(floor(localUV * 64), 0, 63);
                float height = _SurfaceRows[(int)p.x];
                float surfaceDistance = height - 1 - p.y;
                half alpha = tex2D(_MainTex, i.uv).a * step(0, surfaceDistance);
                clip(alpha - 0.001h);

                float time = _FillState.y;
                float y = p.y - _FillState.x;
                float depth = y / max(1, height - _FillState.x - 1);
                float cloud = sin(p.x * 0.36 + y * 0.53 - time * 0.74) * 0.5 + 0.5;
                half3 midnight = half3(0.022, 0.032, 0.125);
                half3 dawn = half3(0.14, 0.38, 0.67);
                half3 color = lerp(midnight, dawn, floor(saturate(depth * 0.78 + cloud * 0.22) * 5) / 5);
                if (surfaceDistance < 0.5)
                    color = lerp(half3(0.27, 0.55, 0.8), half3(0.63, 0.86, 1.0), floor(cloud * 3) * 0.5);

                half dawnLight = smoothstep(0.22h, 0.98h, _FillState.w);
                half airglow = floor(cloud * 4) * 0.25h;
                half3 pearl = lerp(half3(0.24, 0.22, 0.51), half3(0.55, 0.79, 1.0), saturate(depth));
                color = lerp(color, pearl, dawnLight * (0.16h + 0.16h * airglow));
                half stars = 0;
                [unroll]
                for (int sparkIndex = 0; sparkIndex < 5; sparkIndex++)
                    stars = max(stars, SparkLight(p, _Sparks[sparkIndex]));

                if (_MeteorStep >= 0)
                {
                    float2 liquidPixel = float2(p.x, y);
                    if (all(liquidPixel == MeteorPixel(_MeteorStep)))
                        stars = max(stars, 0.98h);
                    if (_MeteorStep >= 2 && all(liquidPixel == MeteorPixel(_MeteorStep - 2)))
                        stars = max(stars, 0.48h);
                }

                half3 starlight = half3(0.79, 0.94, 1.0);
                color = lerp(color, starlight, saturate(stars));
                // Only the peak of a star's center pixel exceeds the game's bloom threshold.
                color += starlight * (_Starlight * smoothstep(0.86h, 0.99h, stars));
                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001h);
                #endif
                half3 finalColor = color * i.color.rgb * _BrightnessMultiplier;
                return half4(finalColor, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
