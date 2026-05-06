Shader "TextMeshPro/Bitmap Pixel Outline"
{
    Properties
    {
        _MainTex            ("Font Atlas", 2D) = "white" {}
        _FaceTex            ("Font Texture", 2D) = "white" {}
        _FaceColor          ("Text Color", Color) = (1,1,1,1)
        _OutlineColor       ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth       ("Outline Width (Atlas Texels)", Range(0,8)) = 1
        _AlphaCutoff        ("Alpha Cutoff", Range(0,1)) = 0.5

        _VertexOffsetX      ("Vertex OffsetX", float) = 0
        _VertexOffsetY      ("Vertex OffsetY", float) = 0
        _MaskSoftnessX      ("Mask SoftnessX", float) = 0
        _MaskSoftnessY      ("Mask SoftnessY", float) = 0

        _ClipRect           ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

        _StencilComp        ("Stencil Comparison", Float) = 8
        _Stencil            ("Stencil ID", Float) = 0
        _StencilOp          ("Stencil Operation", Float) = 0
        _StencilWriteMask   ("Stencil Write Mask", Float) = 255
        _StencilReadMask    ("Stencil Read Mask", Float) = 255

        _CullMode           ("Cull Mode", Float) = 0
        _ColorMask          ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Lighting Off
        Cull [_CullMode]
        ZTest [unity_GUIZTestMode]
        ZWrite Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex       : POSITION;
                fixed4 color        : COLOR;
                float4 texcoord0    : TEXCOORD0;
                float2 texcoord1    : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex           : SV_POSITION;
                fixed4 color            : COLOR;
                float2 atlasUV          : TEXCOORD0;
                float2 originalUVMin    : TEXCOORD1;
                float2 originalUVMax    : TEXCOORD2;
                float4 mask             : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            sampler2D _FaceTex;
            float4 _FaceTex_ST;

            fixed4 _FaceColor;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _AlphaCutoff;

            float _VertexOffsetX;
            float _VertexOffsetY;
            float4 _ClipRect;
            float _MaskSoftnessX;
            float _MaskSoftnessY;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                float4 vert = v.vertex;
                vert.x += _VertexOffsetX;
                vert.y += _VertexOffsetY;

                vert.xy += (vert.w * 0.5) / _ScreenParams.xy;

                float4 vPosition = UnityPixelSnap(UnityObjectToClipPos(vert));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }

                v2f OUT;
                OUT.vertex = vPosition;
                OUT.color = v.color * _FaceColor;
                OUT.atlasUV = v.texcoord0.xy;
                OUT.originalUVMin = v.texcoord0.zw;
                OUT.originalUVMax = v.texcoord1;

                float2 pixelSize = vPosition.w;
                pixelSize /= abs(float2(_ScreenParams.x * UNITY_MATRIX_P[0][0], _ScreenParams.y * UNITY_MATRIX_P[1][1]));

                const float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
                OUT.mask = float4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float centerAlpha = tex2D(_MainTex, IN.atlasUV).a;
                float neighborAlpha = centerAlpha;
                float radius = max(0.0, _OutlineWidth);

                [unroll]
                for (int y = -8; y <= 8; y++)
                {
                    [unroll]
                    for (int x = -8; x <= 8; x++)
                    {
                        if (abs(x) <= radius && abs(y) <= radius)
                        {
                            float2 offset = float2(x, y) * _MainTex_TexelSize.xy;
                            neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.atlasUV + offset).a);
                        }
                    }
                }

                float2 originalUVSize = IN.originalUVMax - IN.originalUVMin;
                float hasOriginalBounds = step(0.000001, originalUVSize.x * originalUVSize.y);
                float2 insideMin = step(IN.originalUVMin, IN.atlasUV);
                float2 insideMax = step(IN.atlasUV, IN.originalUVMax);
                float insideOriginalGlyphQuad = lerp(1.0, insideMin.x * insideMin.y * insideMax.x * insideMax.y, hasOriginalBounds);
                float faceMask = step(_AlphaCutoff, centerAlpha) * insideOriginalGlyphQuad;
                float outlineMask = step(_AlphaCutoff, neighborAlpha) * (1.0 - faceMask);

                fixed4 faceColor = fixed4(IN.color.rgb, IN.color.a * faceMask);
                fixed4 outlineColor = fixed4(_OutlineColor.rgb, _OutlineColor.a * IN.color.a * outlineMask);
                fixed4 color = lerp(outlineColor, faceColor, faceMask);

                #if UNITY_UI_CLIP_RECT
                    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                    color *= m.x * m.y;
                #endif

                #if UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
