Shader "UI/RadialRevealDimOverlay"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _RevealCenter ("Reveal Center", Vector) = (0.5, 0.5, 0, 0)
        _RevealRadius ("Reveal Radius", Float) = 0
        _RevealSoftness ("Reveal Softness", Float) = 1
        _OverlayAlpha ("Overlay Alpha", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 screenPosition : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _RevealCenter;
            float _RevealRadius;
            float _RevealSoftness;
            float _OverlayAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.screenPosition = ComputeScreenPos(OUT.vertex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 screenUV = IN.screenPosition.xy / IN.screenPosition.w;
                float2 screenPixel = screenUV * _ScreenParams.xy;
                float2 centerPixel = _RevealCenter.xy * _ScreenParams.xy;
                float distanceFromCenter = distance(screenPixel, centerPixel);
                float softness = max(_RevealSoftness, 0.0001);
                float outsideCircle = smoothstep(_RevealRadius, _RevealRadius + softness, distanceFromCenter);

                fixed4 color = IN.color;
                color.a *= saturate(_OverlayAlpha) * outsideCircle;
                return color;
            }
            ENDCG
        }
    }
}
