Shader "UI/GradeShards"
{
    // 슬롯 타일 뒤(가장 아래 레이어)에서 모서리 바깥으로 삐져나오는 보석 결정 파편.
    // 이미지 크기는 타일보다 크게(40x40) 잡고, 모양은 전부 셰이더에서 그린다. 개수는 _ShardCount(0~4).
    // 버스트: Image.color 의 R 채널(1 - 세기)로 받아 파편이 순간 커지고 밝아진다.
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        // 0 = 금, 1 = 다이아, 2 = 프리즘
        _Mode("Mode (0 Gold, 1 Diamond, 2 Prism)", Float) = 0
        _ColorA("Color A", Color) = (1, 0.74, 0.16, 1)
        _ColorB("Color B", Color) = (1, 0.93, 0.5, 1)
        _OutlineColor("Outline Color", Color) = (0.29, 0.19, 0.09, 1)
        _ShardCount("Shard Count (0..4)", Range(0,4)) = 1
        _TwinkleSpeed("Twinkle Speed", Float) = 2

        // UI 정점의 로컬 좌표는 캔버스 배치 때 캔버스 기준으로 바뀌므로(슬롯 위치가 달라지면 패턴이 어긋난다)
        // 픽셀 좌표는 UV와 스프라이트 크기로 구한다. 스프라이트 한 장짜리 텍스처(아틀라스 아님)가 전제다.
        _PixelSize("Pixel Size (sprite w, h)", Vector) = (40, 40, 0, 0)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _PixelSize;

            float _Mode;
            float4 _ColorA;
            float4 _ColorB;
            float4 _OutlineColor;
            float _ShardCount;
            float _TwinkleSpeed;

            // 파편 정의: (중심 x, 중심 y, 반폭, 반높이). 타일은 중심 기준 ±12px 이므로 바깥으로 4~5px 나온다.
            // 앞에서부터 _ShardCount개를 쓴다. 배지(좌상단 안쪽)와 개수 숫자(우하단 안쪽)는 타일 안쪽이라 겹치지 않는다.
            float4 GetShard(int _i)
            {
                if (0 == _i) return float4( 10.5,  13.5, 3.0, 5.5);
                if (1 == _i) return float4(-14.0,   4.5, 5.5, 3.0);
                if (2 == _i) return float4( -2.5,  14.0, 3.0, 4.5);
                return float4( 14.0,  -6.5, 5.5, 3.0);
            }

            float3 HueToRgb(float hue, float sat)
            {
                float3 rgb = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return lerp(float3(1.0, 1.0, 1.0), rgb, sat);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.texcoord = v.texcoord;
                OUT.localPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = floor((IN.texcoord - 0.5) * _PixelSize.xy) + 0.5;
                float t = _Time.y;
                float burst = saturate(1.0 - IN.color.r);

                half4 color = half4(0, 0, 0, 0);

                [unroll] for (int i = 0; i < 4; i++)
                {
                    if (i < (int)(_ShardCount + 0.5))
                    {
                        float4 s = GetShard(i);
                        float grow = 1.0 + burst * 0.5;
                        float2 d = abs(p - s.xy);
                        float k = d.x / (s.z * grow) + d.y / (s.w * grow);

                        if (k <= 1.0)
                        {
                            float pulse = 0.5 + 0.5 * sin(t * _TwinkleSpeed + i * 2.1);

                            float3 fill;
                            if (_Mode < 0.5)
                                fill = lerp(_ColorA.rgb, _ColorB.rgb, pulse);
                            else if (_Mode < 1.5)
                                fill = lerp(_ColorA.rgb, float3(1.0, 1.0, 1.0), pulse * 0.5);
                            else
                                fill = HueToRgb(frac(t * 0.2 + i * 0.27), 0.8);

                            // 안쪽 윗-왼쪽 면은 하이라이트(결정의 빛 받는 면)
                            float2 rel = p - s.xy;
                            float highlight = step(rel.x, 0.0) * step(0.0, rel.y) * step(k, 0.55);
                            fill = lerp(fill, float3(1.0, 1.0, 1.0), highlight * (0.6 + 0.4 * pulse));
                            fill = lerp(fill, float3(1.0, 1.0, 1.0), burst);

                            float outline = step(0.72, k);
                            color.rgb = lerp(fill, _OutlineColor.rgb, outline);
                            color.a = 1.0;
                        }
                    }
                }

                color.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPosition.xy, _ClipRect);
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
