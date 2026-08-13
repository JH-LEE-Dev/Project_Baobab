Shader "UI/AbilityLineShiny"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _RedShineColor ("Red Line Shine", Color) = (0.760784, 0.427451, 0.403922, 1)
        _GreenShineColor ("Green Line Shine", Color) = (0.686275, 0.847059, 0.713726, 1)
        _BlueShineColor ("Blue Line Shine", Color) = (0.760784, 0.956863, 0.996078, 1)
        _ShineSpeed ("Shine Cycles Per Second", Range(0, 2)) = 0.35
        _ShineWidth ("Shine Width", Range(0.01, 1)) = 0.24
        _ShineSoftness ("Shine Edge Softness", Range(0, 1)) = 0.85
        _ShineGap ("Shine Repeat Gap", Range(0, 2)) = 0.3
        _ShineIntensity ("Shine Intensity", Range(0, 1)) = 1

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

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
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 shineData : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 shineData : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _RedShineColor;
            fixed4 _GreenShineColor;
            fixed4 _BlueShineColor;
            float _ShineSpeed;
            float _ShineWidth;
            float _ShineSoftness;
            float _ShineGap;
            float _ShineIntensity;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                output.shineData = input.shineData;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;

                float colorIndex = input.shineData.y;
                float validColor = step(-0.5, colorIndex);
                float redWeight = 1.0 - step(0.5, colorIndex);
                float greenWeight = step(0.5, colorIndex) * (1.0 - step(1.5, colorIndex));
                float blueWeight = step(1.5, colorIndex);
                fixed3 shineColor =
                    _RedShineColor.rgb * redWeight +
                    _GreenShineColor.rgb * greenWeight +
                    _BlueShineColor.rgb * blueWeight;

                float halfWidth = max(_ShineWidth * 0.5, 0.0001);
                float innerHalfWidth = halfWidth * (1.0 - saturate(_ShineSoftness));
                float travelLength = 1.0 + halfWidth * 2.0 + max(_ShineGap, 0.0);
                float shineCenter = frac(_Time.y * max(_ShineSpeed, 0.0)) * travelLength - halfWidth;
                float distanceToShine = abs(input.shineData.x - shineCenter);
                float shine = 1.0 - smoothstep(innerHalfWidth, halfWidth, distanceToShine);
                shine *= validColor * saturate(_ShineIntensity);

                color.rgb = lerp(color.rgb, shineColor, shine);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
