Shader "UI/AbilityTiledBackGround"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TileSize ("Tile Size", Vector) = (64, 64, 0, 0)
        _RectSize ("Rect Size", Vector) = (640, 360, 0, 0)
        _Pivot ("Pivot", Vector) = (0.5, 0.5, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 localPosition : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _TileSize;
            float4 _RectSize;
            float4 _Pivot;
            float4 _AbilityTileBGOffset;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color * _Color;
                OUT.localPosition = v.vertex.xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 tileSize = max(_TileSize.xy, float2(1.0, 1.0));
                float2 localPixel = IN.localPosition + (_RectSize.xy * _Pivot.xy);
                float2 tilePixel = floor(localPixel - _AbilityTileBGOffset.xy);
                float2 samplePixel = floor(frac(tilePixel / tileSize) * tileSize) + 0.5;
                float2 uv = samplePixel * _MainTex_TexelSize.xy;

                return tex2D(_MainTex, uv) * IN.color;
            }
            ENDCG
        }
    }
}
