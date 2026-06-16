Shader "Custom/Effects/ShinyCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Shiny Settings)]
        [HDR] _ShinyColor ("Shiny Color", Color) = (1, 1, 1, 0.5)
        _ShinyLocation ("Shiny Location", Range(-1.0, 2.0)) = -1.0
        _ShinyWidth ("Shiny Width", Range(0.01, 1.0)) = 0.2
        _ShinySoftness ("Shiny Softness", Range(0.01, 1.0)) = 0.1
        _ShinyAngle ("Shiny Angle", Range(0, 360)) = 45.0
        
        [Toggle(UI_OVERLAY)] _OverlayMode ("Overlay Mode (Add only glow)", Float) = 0

        [Header(UI and Blend)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
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
        ZTest [_ZTest]
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
            #pragma multi_compile_local _ UI_OVERLAY

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            // Shiny
            fixed4 _ShinyColor;
            float _ShinyLocation;
            float _ShinyWidth;
            float _ShinySoftness;
            float _ShinyAngle;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);

                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Base texture sampling
                half4 texSample = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 color = texSample * IN.color;
                
                // Calculate shiny line
                float rad = _ShinyAngle * 3.14159265 / 180.0;
                float s = sin(rad);
                float c = cos(rad);
                
                // Rotate UV around center (0.5, 0.5)
                float2 centeredUV = IN.texcoord - float2(0.5, 0.5);
                float2 rotatedUV = float2(
                    centeredUV.x * c - centeredUV.y * s,
                    centeredUV.x * s + centeredUV.y * c
                ) + float2(0.5, 0.5);
                
                // Dist to the shiny line
                float dist = abs(rotatedUV.x - _ShinyLocation);
                float glow = smoothstep(_ShinyWidth, _ShinyWidth - _ShinySoftness, dist);
                
                #ifdef UI_OVERLAY
                    // 오버레이 모드: 원본 이미지를 그리지 않고, 반짝이는 효과만 렌더링. 마스킹을 위해 알파만 챙김.
                    float maskAlpha = texSample.a * IN.color.a;
                    color.rgb = _ShinyColor.rgb * glow;
                    color.a = glow * _ShinyColor.a * maskAlpha; // 마스킹 영역에만 글로우 표시
                #else
                    // 기존 모드: 원래 이미지 위에 빛을 더함
                    color.rgb += _ShinyColor.rgb * glow * _ShinyColor.a * color.a;
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
