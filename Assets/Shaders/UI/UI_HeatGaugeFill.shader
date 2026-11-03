// 과열 게이지 바(HeatBar_Base 9x50) 한 장에 테두리 · 빈칸 달아오름 · 용암 채움 · 번쩍임을 그린다.
// 값은 HUD_HeatGauge가 바뀔 때만 머티리얼에 넣는다. 시간은 _TickRate로 끊어 픽셀 애니메이션처럼 보이게 한다.
Shader "UI/HeatGaugeFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Layout)]
        _SpriteUVRect ("Sprite UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0,0,1,1)
        _BarSize ("Bar Size (px)", Vector) = (9,50,0,0)
        _TickRate ("Tick Rate (fps)", Float) = 24

        [Header(Palette Dark To White)]
        _Palette0 ("Palette 0", Color) = (0.165,0.024,0.063,1)
        _Palette1 ("Palette 1", Color) = (0.369,0.059,0.078,1)
        _Palette2 ("Palette 2", Color) = (0.639,0.125,0.102,1)
        _Palette3 ("Palette 3", Color) = (0.878,0.275,0.110,1)
        _Palette4 ("Palette 4", Color) = (1,0.541,0.118,1)
        _Palette5 ("Palette 5", Color) = (1,0.788,0.235,1)
        _Palette6 ("Palette 6", Color) = (1,0.953,0.651,1)
        _Palette7 ("Palette 7", Color) = (1,1,1,1)
        _GlowColor ("Empty Glow Color", Color) = (0.588,0.188,0.118,1)
        _FrameHotColor ("Frame Hot Color", Color) = (1,0.769,0.431,1)

        [Header(Runtime State)]
        _FillPx ("Fill Pixels", Float) = 0
        _Ratio ("Fill Ratio", Float) = 0
        _Stage ("Gauge Stage", Float) = 0
        _IsMax ("Is Max", Float) = 0
        _Flash ("Flash", Float) = 0
        _PaletteCap ("Palette Cap", Float) = 7
        _FlowSpeed ("Flow Speed", Float) = 0
        _BubbleAmount ("Bubble Amount", Float) = 0
        _FrameHeat ("Frame Heat", Float) = 0
        _HeatBase ("Heat Base", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float4 _SpriteUVRect;
            float4 _BarSize;
            float _TickRate;
            fixed4 _Palette0, _Palette1, _Palette2, _Palette3, _Palette4, _Palette5, _Palette6, _Palette7;
            fixed4 _GlowColor;
            fixed4 _FrameHotColor;
            float _FillPx, _Ratio, _Stage, _IsMax, _Flash;
            float _PaletteCap, _FlowSpeed, _BubbleAmount, _FrameHeat, _HeatBase;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash(float2 _p)
            {
                return frac(sin(_p.x * 12.9898 + _p.y * 78.233) * 43758.5453);
            }

            float ValueNoise(float2 _p)
            {
                float2 i = floor(_p);
                float2 f = _p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed3 Palette(float _index)
            {
                if (_index < 0.5) return _Palette0.rgb;
                if (_index < 1.5) return _Palette1.rgb;
                if (_index < 2.5) return _Palette2.rgb;
                if (_index < 3.5) return _Palette3.rgb;
                if (_index < 4.5) return _Palette4.rgb;
                if (_index < 5.5) return _Palette5.rgb;
                if (_index < 6.5) return _Palette6.rgb;
                return _Palette7.rgb;
            }

            // 채움 픽셀 하나의 색. _fx는 채움 안 열(0~4), _fy는 채움 바닥부터의 행.
            fixed3 LavaColor(float _fx, float _fy, float _tick)
            {
                float flow = _FlowSpeed + _IsMax * 12.0;

                // 높이가 오를수록 뜨거워지고, 노이즈 2겹이 위로 흐른다.
                float heat = _HeatBase + (_fy / 45.0) * 0.62;
                float n = ValueNoise(float2(_fx * 0.8 + 3.0, _fy * 0.32 - _tick * flow * 0.32)) * 0.65
                        + ValueNoise(float2(_fx * 1.7 + 11.0, _fy * 0.7 - _tick * flow * 0.55)) * 0.35;
                heat += (n - 0.5) * 0.6;

                // 기포: 열마다 위상이 다른 밝은 한 칸이 위로 올라간다.
                if (0.0 < _BubbleAmount)
                {
                    float period = max(8.0, _FillPx + 6.0);
                    float bubbleRow = fmod(floor(_tick * flow) + floor(Hash(float2(_fx, 7.0)) * 97.0), period);
                    if (abs(bubbleRow - _fy) < 0.5 && Hash(float2(_fx, floor(_tick * flow / period))) > 1.0 - 0.5 * _BubbleAmount)
                        heat += 0.35;
                }

                heat += _Ratio * 0.1;

                // 꼭대기 백열 테두리(2단계부터 깜빡이고, 4단계는 흰색까지)
                float dist = _FillPx - 1.0 - _fy;
                if (dist < 0.5)
                {
                    if (1.5 <= _Stage)
                        heat = Hash(float2(floor(_tick * _TickRate), _fx)) > 0.35 ? (3.5 < _Stage ? 1.2 : 0.86) : 0.75;
                    else
                        heat = 0.5;
                }
                else if (dist < 1.5 && 1.5 <= _Stage)
                {
                    heat += 0.22;
                }

                float pulse = (0.5 < _IsMax && 0.0 < sin(_tick * 6.2832 * 1.6)) ? 1.0 : 0.0;
                float index = clamp(floor(heat * 8.0) + pulse, 0.0, _PaletteCap);

                // 1단계 잔불: 드문드문 한 칸씩 밝아진다.
                if (_Stage < 1.5 && Hash(float2(floor(_tick * 8.0), _fx * 31.0 + _fy)) > 0.97)
                    index = min(_PaletteCap, index + 1.0);

                return lerp(Palette(index), fixed3(1, 1, 1), _Flash);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 baseColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                // 아틀라스에 들어가도 스프라이트 영역 기준으로 픽셀 좌표를 구한다.
                float2 uv01 = (IN.texcoord - _SpriteUVRect.xy) / max(_SpriteUVRect.zw - _SpriteUVRect.xy, 1e-5);
                float2 pix = clamp(floor(uv01 * _BarSize.xy), 0.0, _BarSize.xy - 1.0);

                fixed3 col = baseColor.rgb;

                if (0.5 < _Stage)
                {
                    float tick = floor(_Time.y * _TickRate) / _TickRate;
                    float framePulse = _FrameHeat * (0.6 + 0.4 * sin(tick * 6.2832 * (3.5 < _Stage ? 4.0 : 1.5)));

                    bool inner = 2.0 <= pix.x && pix.x <= _BarSize.x - 3.0 && 2.0 <= pix.y && pix.y <= _BarSize.y - 3.0;
                    float fx = pix.x - 2.0;
                    float fy = pix.y - 2.0;

                    if (inner && fy < _FillPx)
                    {
                        col = LavaColor(fx, fy, tick);
                    }
                    else if (inner)
                    {
                        // 채움 바로 위 빈칸이 달아오른다(위로 갈수록 약하게).
                        float above = fy - _FillPx;
                        if (above < 6.0)
                            col = lerp(col, _GlowColor.rgb, framePulse * (1.0 - above / 6.0));
                    }
                    else if (0.9 < baseColor.r && 3.5 < _Stage && 0.6 < framePulse && pix.y <= _FillPx + 4.0)
                    {
                        // 4단계: 흰 테두리가 채움 높이까지 주황으로 깜빡인다.
                        col = _FrameHotColor.rgb;
                    }
                }

                half4 color = half4(col, baseColor.a) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
