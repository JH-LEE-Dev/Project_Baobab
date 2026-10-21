Shader "UI/CardGradeFrame"
{
    // 희귀 카드의 프레임 자체를 등급 색으로 바꿔 그린다(팔레트 스왑). 프레임을 덮는 패턴 층이 아니라 프레임 그림을 그대로 읽어
    // 손으로 그린 5톤(안쪽 그림자 · 몸통 · 바깥 링 · 가운데 링 · 하이라이트)을 등급 팔레트 5톤으로 1:1 치환하므로,
    // 원래 프레임의 명암·둥근 모서리·링이 그대로 남고 윗부분만 따로 노는 이질감이 없다.
    //
    // [움직임은 절제] 몸통에는 아주 옅은 대각 해칭(브러시드 메탈), 링은 천천히 맥동(톤 한 단계씩 오르내림),
    // 몇 초에 한 번 대각 광택 띠가 지나가며 닿은 톤을 한두 단계 밝히고, 네 모서리에서 작은 십자 반짝이 번갈아 켜진다.
    // (카드 밖의 후광·입자는 UI_RoguelikeCardAura가 그린다.)
    // 모두 정수 픽셀·단계식이고 반투명 그라데이션이 없다. 프리즘(_Rainbow)은 링만 단계식 무지개로 흐른다.
    //
    // [텍스처] 프레임 스프라이트를 한 장짜리로 복사한 Card_FrameFull(50x81)이 전제다(시트에서 잘린 조각은 UV로 픽셀 좌표를 못 구한다).
    // [색 구분] 선형 색 공간 기준: 안쪽 그림자 < 0.0289 < 몸통 < 0.0513 < 바깥 링 < 0.1045 < 가운데 링 < 0.2496 < 하이라이트(원본 프레임 색의 밝기).
    // [Image.color] R = 공개 진행도(1이면 완전히 레어, 0~1이면 대각 광택 띠가 지나가며 레어로 바뀌는 중), G = 밝기 배율(다른 카드 선택 중 어두워짐), 알파는 그대로.
    Properties
    {
        [PerRendererData] _MainTex("Frame Sprite (full copy)", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _PixelSize("Pixel Size (sprite w, h)", Vector) = (50, 81, 0, 0)
        // 반짝이 놓일 모서리: 링 중심까지의 반폭/반높이(px). 카드(50x81)는 (22.5, 38).
        _LineHalf("Corner Position Half Size (px)", Vector) = (22.5, 38, 0, 0)

        [Header(Palette 5 tones)]
        _Tone0("Inner Shadow", Color) = (0.20, 0.23, 0.29, 1)
        _Tone1("Body", Color) = (0.30, 0.34, 0.41, 1)
        _Tone2("Outer Ring", Color) = (0.50, 0.55, 0.63, 1)
        _Tone3("Middle Ring", Color) = (0.74, 0.79, 0.86, 1)
        _Tone4("Highlight", Color) = (0.95, 0.97, 1.0, 1)

        [Header(Prism Ring)]
        [Toggle] _Rainbow("Rainbow Ring (prism)", Float) = 0
        _RainbowSaturation("Rainbow Saturation", Range(0,1)) = 0.6
        _RainbowSteps("Rainbow Steps", Range(2,12)) = 8
        _HueSpeed("Hue Speed (cycles/sec)", Float) = 0.2

        [Header(Motion)]
        _HatchStrength("Body Hatch Strength", Range(0,0.5)) = 0.16
        _PulseSpeed("Ring Pulse Speed", Float) = 1.8
        _GlintPeriod("Glint Period (sec)", Range(1,10)) = 3.6
        _GlintDuration("Glint Sweep Fraction of Period", Range(0.05,1)) = 0.3
        _GlintWidth("Glint Width (px)", Range(1,10)) = 5
        _CornerSparkle("Corner Sparkle (0 = off)", Range(0,1)) = 1


        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [HideInInspector] _ClipRect("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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
            float4 _LineHalf;

            float4 _Tone0;
            float4 _Tone1;
            float4 _Tone2;
            float4 _Tone3;
            float4 _Tone4;

            float _Rainbow;
            float _RainbowSaturation;
            float _RainbowSteps;
            float _HueSpeed;

            float _HatchStrength;
            float _PulseSpeed;
            float _GlintPeriod;
            float _GlintDuration;
            float _GlintWidth;
            float _CornerSparkle;

            float3 HueToRgb(float hue, float sat)
            {
                float3 rgb = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return lerp(float3(1.0, 1.0, 1.0), rgb, sat);
            }

            float3 Tone(int index)
            {
                if (index <= 0) return _Tone0.rgb;
                if (index == 1) return _Tone1.rgb;
                if (index == 2) return _Tone2.rgb;
                if (index == 3) return _Tone3.rgb;
                return _Tone4.rgb;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.localPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord);
                float mask = step(0.5, tex.a);

                // 픽셀 중심 좌표(이미지 중심 기준). 픽셀 번호에서 바로 구해 홀수 크기에서도 한 픽셀이 쪼개지지 않는다.
                float2 p = floor(IN.texcoord * _PixelSize.xy) + 0.5 - _PixelSize.xy * 0.5;
                float t = _Time.y;

                // 원본 프레임 톤 구분(선형 색 공간 밝기)
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                int idx = lum < 0.0289 ? 0 : (lum < 0.0513 ? 1 : (lum < 0.1045 ? 2 : (lum < 0.2496 ? 3 : 4)));
                int lift = 0;

                // 링 맥동: 대각으로 흐르며 한 단계 밝아졌다 돌아온다.
                if (idx >= 2)
                {
                    float pulse = 0.5 + 0.5 * sin(t * _PulseSpeed + (p.x - p.y) * 0.12);
                    if (pulse > 0.82) lift = 1;
                }

                // 광택 띠: 몇 초에 한 번 대각으로 지나간다(정수 픽셀 단위).
                float u = frac(t / _GlintPeriod);
                if (u < _GlintDuration)
                {
                    float sweep = u / _GlintDuration;
                    float range = _LineHalf.x * 0.8 + _LineHalf.y + _GlintWidth + 2.0;
                    float s = floor(lerp(-range, range, sweep));
                    float bandDist = abs(floor(p.x * 0.8 + p.y) - s);
                    if (bandDist < _GlintWidth * 0.5) lift = max(lift, 2);
                    else if (bandDist < _GlintWidth) lift = max(lift, 1);
                }

                int shown = min(4, idx + lift);
                float3 col = Tone(shown);

                // 몸통의 옅은 대각 해칭(브러시드 메탈). 한 칸 위 톤으로 살짝만 섞는다.
                if (idx == 1 && fmod(abs(floor(p.x + p.y)), 4.0) < 0.5)
                    col = lerp(col, Tone(min(4, shown + 1)), _HatchStrength * 2.0);

                // 프리즘: 링이 단계식 무지개로 흐른다. 맥동·광택으로 올라간 부분은 흰색 쪽으로 밝힌다.
                if (_Rainbow > 0.5 && idx >= 2)
                {
                    float angle = atan2(p.y, p.x) * 0.15915494 + 0.5;
                    float hue = floor(frac(angle + t * _HueSpeed) * _RainbowSteps) / _RainbowSteps;
                    float3 rainbow = HueToRgb(hue, _RainbowSaturation);
                    float level = idx == 2 ? 0.6 : (idx == 3 ? 0.88 : 1.0);
                    col = lerp(rainbow * level, float3(1.0, 1.0, 1.0), lift > 0 ? 0.35 * lift : 0.0);
                }

                // 모서리 십자 반짝: 네 모서리가 서로 다른 박자로 켜졌다 꺼진다(링 위에서만 보인다).
                if (_CornerSparkle > 0.0)
                {
                    float2 corner = float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0) * _LineHalf.xy;
                    float2 dc = abs(p - corner);
                    float cornerId = (p.x > 0.0 ? 1.0 : 0.0) + (p.y > 0.0 ? 2.0 : 0.0);
                    float pulse = saturate(sin(t * 2.1 + cornerId * 1.9));
                    pulse = pulse * pulse * pulse * pulse;
                    float cross = step(min(dc.x, dc.y), 0.5) * step(max(dc.x, dc.y), 2.5);
                    col = lerp(col, float3(1.0, 1.0, 1.0), cross * step(0.55, pulse) * _CornerSparkle);
                }

                // 공개 연출(R 채널 = 진행도, 1이면 완전히 레어): 대각 광택 띠가 왼쪽 위에서 오른쪽 아래로 지나가며 레어로 바꾼다.
                // 띠가 지나간 자리는 레어 팔레트, 아직 안 지나간 자리는 투명(아래의 원래 프레임이 그대로 보인다). 정수 픽셀 단위로 끊어 움직인다.
                float outAlpha = mask * IN.color.a;
                float reveal = IN.color.r;
                if (reveal < 0.999)
                {
                    float sweepRange = _LineHalf.x * 0.8 + _LineHalf.y + _GlintWidth + 4.0;
                    float sweep = floor(lerp(-sweepRange, sweepRange, reveal));
                    float delta = floor(p.x * 0.8 - p.y) - sweep;
                    float bandHalf = _GlintWidth * 0.5;
                    if (delta > bandHalf)
                        outAlpha = 0.0;
                    else if (delta > -bandHalf)
                        col = abs(delta) < 1.0 ? lerp(Tone(4), float3(1.0, 1.0, 1.0), 0.7) : Tone(4);
                    else if (delta > -bandHalf - 3.0)
                        col = Tone(min(4, shown + 1));
                }

                // 밝기(G 채널)
                col *= IN.color.g;

                half4 color = half4(col, outAlpha);

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
