Shader "UI/CardGradeFrame"
{
    // 희귀 카드의 프레임 자체를 등급 색으로 바꿔 그린다(팔레트 스왑). 프레임을 덮는 패턴 층이 아니라 프레임 그림을 그대로 읽어
    // 손으로 그린 5톤(안쪽 그림자 · 몸통 · 바깥 링 · 가운데 링 · 하이라이트)을 등급 팔레트 5톤으로 1:1 치환하므로,
    // 원래 프레임의 명암·둥근 모서리·링이 그대로 남고 윗부분만 따로 노는 이질감이 없다.
    //
    // [오로라] 프레임 위로 느리게 흐르는 아주 은은한 마력색 띠(시뮬레이션 make_shader_sim_v3.py의 세기 0 수식 그대로).
    // 테두리 안쪽 링(투명에서 2~4칸)과 몸통 · 홈 벽에만 마력색을 조금 섞고, 바깥 윤곽(1칸째)과 금 덮개는 섞지 않는다.
    // 프리즘(_AuroraRainbow)은 마력색 대신 둘레 위치 + 시간으로 정한 8단계 무지개를 쓴다. 시간은 30fps 단계(floor(시간 * 30) / 30).
    // 카드마다 무늬가 다르도록 시드(Image.color B 채널)를 받고(수식은 make_shader_sim_v4.py의 shade), 띠 간격 계수는 _AuroraBandScale(민무늬 구간이 없도록 2.6).
    // 오로라 · 프리즘 무지개 시간은 전역 값 _CardAuroraTime(UI_RoguelikeCard가 프레임당 한 번 Time.unscaledTime으로 넣음 - 일시정지 중에도 흐름)이고, 0 이하이면 _Time.y로 대신한다. 기존 움직임(_LegacyMotion)의 시간은 그대로 _Time.y다.
    //
    // [기존 움직임] _LegacyMotion이 켜져 있으면 링 맥동 · 대각 광택 띠 · 모서리 십자 반짝이 돈다(몸통 해칭은 _HatchStrength, 프리즘 무지개 링은 _Rainbow 로 따로 끈다).
    // 모두 정수 픽셀·단계식이고 반투명 그라데이션이 없다.
    //
    // [텍스처] 프레임 그림을 한 장짜리로 복사한 Card_FrameFull(그림 50x81 + 위 투명 1행 = 50x82)이나 64x96 캔버스째인 CardFrame이 전제다(UV로 픽셀 좌표를 구하므로 한 장짜리여야 한다).
    // 픽셀 좌표 p는 항상 Card_FrameFull 기준(그림 x = p.x + 24.5, 그림 y(위에서) = 41 - p.y)이 되도록 _PixelSize · _PixelShift 를 맞춘다(CardFrame 64x96 은 _PixelShift = (0, -1.5)).
    // [색 구분] 선형 색 공간 기준: 안쪽 그림자 < 0.0289 < 몸통 < 0.0513 < 바깥 링 < 0.1045 < 가운데 링 < 0.2496 < 하이라이트(원본 프레임 색의 밝기).
    // [_GradeChannels 1] Image.color R = 공개 진행도(1이면 완전히 레어, 0~1이면 대각 광택 띠가 지나가며 레어로 바뀌는 중), G = 밝기 배율(다른 카드 선택 중 어두워짐), B = 오로라 시드(0~1), 알파는 그대로.
    // [_GradeChannels 0] Image.color G = 밝기 배율을 rgb 모두에 곱한다, B = 오로라 시드(공개 와이프 해석 없음). [_PaletteSwap 0] 5톤 치환 없이 원래 텍스처 색을 쓴다(일반 카드).
    Properties
    {
        [PerRendererData] _MainTex("Frame Sprite (full copy)", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _PixelSize("Pixel Size (sprite w, h)", Vector) = (50, 82, 0, 0)
        // 스프라이트의 오른쪽 · 위에 붙은 투명 줄 때문에 밀린 그림 중심을 되돌리는 값(px). 짝수 맞춤 전 그림(50x81)과 같은 픽셀 좌표가 되도록 투명 줄 폭의 절반이다.
        _PixelShift("Pixel Origin Shift (px)", Vector) = (0, 0.5, 0, 0)
        // 반짝이 놓일 모서리: 링 중심까지의 반폭/반높이(px). 카드 그림(50x81)은 (22.5, 38).
        _LineHalf("Corner Position Half Size (px)", Vector) = (22.5, 38, 0, 0)

        [Header(Mode)]
        [Toggle] _PaletteSwap("Palette Swap (1 = grade 5 tones, 0 = keep texture colors)", Float) = 1
        [Toggle] _GradeChannels("Grade Channels (1 = Image.color R/G = reveal/brightness, 0 = plain tint)", Float) = 1
        [Toggle] _KeepGold("Keep Gold Cap Colors", Float) = 0

        [Header(Palette 5 tones)]
        _Tone0("Inner Shadow", Color) = (0.20, 0.23, 0.29, 1)
        _Tone1("Body", Color) = (0.30, 0.34, 0.41, 1)
        _Tone2("Outer Ring", Color) = (0.50, 0.55, 0.63, 1)
        _Tone3("Middle Ring", Color) = (0.74, 0.79, 0.86, 1)
        _Tone4("Highlight", Color) = (0.95, 0.97, 1.0, 1)

        [Header(Aurora)]
        _AuroraColor0("Aurora Color 0 (dark)", Color) = (0.478431, 0.290196, 0.078431, 1)
        _AuroraColor1("Aurora Color 1 (mid)", Color) = (0.878431, 0.603922, 0.188235, 1)
        [Toggle] _AuroraRainbow("Aurora Rainbow (prism)", Float) = 0
        // 섞는 비율 (테두리 진한 띠, 테두리 옅은 띠, 몸통 진한 띠, 몸통 옅은 띠). 모두 0이면 오로라 없음.
        _AuroraMix("Aurora Mix (edge strong, edge soft, body strong, body soft)", Vector) = (0, 0, 0, 0)
        _AuroraSpeed("Aurora Speed", Float) = 0.55
        // 띠 간격 계수(클수록 띠가 촘촘하고 민무늬 구간이 줄어든다).
        _AuroraBandScale("Aurora Band Scale", Float) = 2.6

        [Header(Prism Ring)]
        [Toggle] _Rainbow("Rainbow Ring (prism)", Float) = 0
        _RainbowSaturation("Rainbow Saturation", Range(0,1)) = 0.6
        _RainbowSteps("Rainbow Steps", Range(2,12)) = 8
        _HueSpeed("Hue Speed (cycles/sec)", Float) = 0.2

        [Header(Motion)]
        [Toggle] _LegacyMotion("Legacy Motion (ring pulse, glint band, corner sparkle)", Float) = 1
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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // 프레임 그림의 칸 구분용 색(텍스처 색과 같은 색 공간). 선형 프로젝트면 선형 값, 감마 프로젝트면 sRGB 값.
            // 금 덮개: #a8744a #d9a640 #f3cc59 / 몸통: #462d1f / 홈 벽: #2a1b13 #663931
            #ifdef UNITY_COLORSPACE_GAMMA
                #define CARD_GOLD0 float3(0.658824, 0.454902, 0.290196)
                #define CARD_GOLD1 float3(0.850980, 0.650980, 0.250980)
                #define CARD_GOLD2 float3(0.952941, 0.800000, 0.349020)
                #define CARD_BODY  float3(0.274510, 0.176471, 0.121569)
                #define CARD_WALL0 float3(0.164706, 0.105882, 0.074510)
                #define CARD_WALL1 float3(0.400000, 0.223529, 0.192157)
            #else
                #define CARD_GOLD0 float3(0.391572, 0.174647, 0.068478)
                #define CARD_GOLD1 float3(0.693872, 0.381326, 0.051269)
                #define CARD_GOLD2 float3(0.896269, 0.603827, 0.099899)
                #define CARD_BODY  float3(0.061246, 0.026241, 0.013702)
                #define CARD_WALL0 float3(0.023153, 0.010960, 0.006512)
                #define CARD_WALL1 float3(0.132868, 0.040915, 0.030713)
            #endif
            #define CARD_COLOR_TOLERANCE 0.004

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
            float4 _PixelShift;
            float4 _LineHalf;

            float _PaletteSwap;
            float _GradeChannels;
            float _KeepGold;

            float4 _Tone0;
            float4 _Tone1;
            float4 _Tone2;
            float4 _Tone3;
            float4 _Tone4;

            float4 _AuroraColor0;
            float4 _AuroraColor1;
            float _AuroraRainbow;
            float4 _AuroraMix;
            float _AuroraSpeed;
            float _AuroraBandScale;
            // 오로라 · 프리즘 무지개 시간(전역 값, Properties 아님). UI_RoguelikeCard가 프레임당 한 번 Time.unscaledTime을 넣는다. 0 이하이면 _Time.y로 대신한다.
            float _CardAuroraTime;

            float _Rainbow;
            float _RainbowSaturation;
            float _RainbowSteps;
            float _HueSpeed;

            float _LegacyMotion;
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

            // 색이 상수와 거의 같은가(텍스처는 압축 없는 점 샘플이라 정확히 같은 색이다).
            bool NearColor(float3 a, float3 b)
            {
                float3 d = abs(a - b);
                return max(d.x, max(d.y, d.z)) < CARD_COLOR_TOLERANCE;
            }

            // 오로라 섞기는 시뮬레이션(sRGB 값끼리 섞음)과 같은 결과가 되도록 감마 값 기준으로 한다. 감마 프로젝트에서는 그대로.
            float3 ToMixSpace(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                c = max(c, float3(0.0, 0.0, 0.0));
                return float3(LinearToGammaSpaceExact(c.x), LinearToGammaSpaceExact(c.y), LinearToGammaSpaceExact(c.z));
            #endif
            }

            float3 FromMixSpace(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return float3(GammaToLinearSpaceExact(c.x), GammaToLinearSpaceExact(c.y), GammaToLinearSpaceExact(c.z));
            #endif
            }

            // 시뮬레이션 hash(x, y) = frac(sin(x * 127.1 + y * 311.7) * 43758.5453)
            float AuroraHash(float x, float y)
            {
                return frac(sin(x * 127.1 + y * 311.7) * 43758.5453);
            }

            // 값 노이즈(smoothstep 보간)
            float AuroraVNoise(float x, float y)
            {
                float xi = floor(x);
                float yi = floor(y);
                float xt = x - xi;
                float yt = y - yi;
                float xf = xt * xt * (3.0 - 2.0 * xt);
                float yf = yt * yt * (3.0 - 2.0 * yt);
                float a = AuroraHash(xi, yi);
                float b = AuroraHash(xi + 1.0, yi);
                float c = AuroraHash(xi, yi + 1.0);
                float d = AuroraHash(xi + 1.0, yi + 1.0);
                return a + (b - a) * xf + (c - a) * yf + (a - b - c + d) * xf * yf;
            }

            float AuroraFbm(float x, float y)
            {
                return 0.65 * AuroraVNoise(x, y) + 0.35 * AuroraVNoise(x * 2.1 + 5.2, y * 2.1 + 1.3);
            }

            // 시뮬레이션 analyse의 둘레 위치 s(0..250). 그림 좌표 x 1..48, y 2..80(위에서)에서 가장 가까운 변 기준(동률은 위 · 오른쪽 · 아래 · 왼쪽 순).
            float AuroraPerimeter(float2 p)
            {
                float gx = p.x + 24.5;
                float gy = 41.0 - p.y;
                float dT = gy - 2.0;
                float dB = 80.0 - gy;
                float dL = gx - 1.0;
                float dR = 48.0 - gx;
                float m = min(min(dT, dB), min(dL, dR));
                float s = 172.0 + (80.0 - gy);
                if (m == dT) s = gx - 1.0;
                else if (m == dR) s = 47.0 + (gy - 2.0);
                else if (m == dB) s = 125.0 + (48.0 - gx);
                return s;
            }

            // 프리즘의 마력색(시뮬레이션 arcAt의 prism 분기). lv 0 = 어두움(채도 0.55, 밝기 0.55), 1 = 중간(채도 0.65, 밝기 0.85). 결과는 sRGB 값.
            float3 AuroraPrism(float s, float tq, float lv)
            {
                float h = frac(s / 250.0 + tq * 0.08);
                float q = floor(h * 8.0) / 8.0;
                return HueToRgb(q, 0.55 + lv * 0.1) * (lv > 0.5 ? 0.85 : 0.55);
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
                float3 texRgb = tex.rgb;
                float mask = step(0.5, tex.a);

                // 픽셀 중심 좌표(그림 중심 기준). 픽셀 번호에서 바로 구해 한 픽셀이 쪼개지지 않는다. _PixelShift는 투명 줄이 붙은 만큼 중심을 그림 쪽으로 되돌린다.
                float2 p = floor(IN.texcoord * _PixelSize.xy) + 0.5 - _PixelSize.xy * 0.5 + _PixelShift.xy;
                float t = _Time.y;
                bool bSwap = _PaletteSwap > 0.5;
                bool bGrade = _GradeChannels > 0.5;
                bool bLegacy = _LegacyMotion > 0.5;

                // 원본 프레임 톤 구분(선형 색 공간 밝기)
                float lum = dot(texRgb, float3(0.299, 0.587, 0.114));
                int idx = lum < 0.0289 ? 0 : (lum < 0.0513 ? 1 : (lum < 0.1045 ? 2 : (lum < 0.2496 ? 3 : 4)));
                int lift = 0;

                if (bLegacy)
                {
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
                }

                int shown = min(4, idx + lift);
                float3 col = bSwap ? Tone(shown) : texRgb;

                // 몸통의 옅은 대각 해칭(브러시드 메탈). 한 칸 위 톤으로 살짝만 섞는다.
                if (bSwap && idx == 1 && fmod(abs(floor(p.x + p.y)), 4.0) < 0.5)
                    col = lerp(col, Tone(min(4, shown + 1)), _HatchStrength * 2.0);

                // 프리즘: 링이 단계식 무지개로 흐른다. 맥동·광택으로 올라간 부분은 흰색 쪽으로 밝힌다.
                if (bSwap && _Rainbow > 0.5 && idx >= 2)
                {
                    float angle = atan2(p.y, p.x) * 0.15915494 + 0.5;
                    float hue = floor(frac(angle + t * _HueSpeed) * _RainbowSteps) / _RainbowSteps;
                    float3 rainbow = HueToRgb(hue, _RainbowSaturation);
                    float level = idx == 2 ? 0.6 : (idx == 3 ? 0.88 : 1.0);
                    col = lerp(rainbow * level, float3(1.0, 1.0, 1.0), lift > 0 ? 0.35 * lift : 0.0);
                }

                // 모서리 십자 반짝: 네 모서리가 서로 다른 박자로 켜졌다 꺼진다(링 위에서만 보인다).
                if (bLegacy && _CornerSparkle > 0.0)
                {
                    float2 corner = float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0) * _LineHalf.xy;
                    float2 dc = abs(p - corner);
                    float cornerId = (p.x > 0.0 ? 1.0 : 0.0) + (p.y > 0.0 ? 2.0 : 0.0);
                    float pulse = saturate(sin(t * 2.1 + cornerId * 1.9));
                    pulse = pulse * pulse * pulse * pulse;
                    float cross = step(min(dc.x, dc.y), 0.5) * step(max(dc.x, dc.y), 2.5);
                    col = lerp(col, float3(1.0, 1.0, 1.0), cross * step(0.55, pulse) * _CornerSparkle);
                }

                // 금 덮개: 텍스처의 금 3색이면 그대로 둔다(팔레트 치환 · 오로라 모두 안 함).
                bool bCap = NearColor(texRgb, CARD_GOLD0) || NearColor(texRgb, CARD_GOLD1) || NearColor(texRgb, CARD_GOLD2);
                bool bKeepCap = bCap && _KeepGold > 0.5;
                if (bKeepCap) col = texRgb;

                // 오로라: 느리게 휘는 값노이즈 띠. 테두리 안쪽 링(투명에서 2~4칸)은 비율 두 단계, 몸통 · 홈 벽은 더 옅게. 바깥 윤곽(1칸째)은 섞지 않는다.
                if (0.0 < mask && false == bKeepCap && 0.0001 < dot(_AuroraMix, float4(1.0, 1.0, 1.0, 1.0)))
                {
                    // 테두리 깊이: 같은 행 · 열로 1~4칸 떨어진 칸 중 투명한 칸이 있으면 그 거리(없으면 5 이상). 안쪽 칸부터 덮어써 가장 가까운 거리가 남는다.
                    float2 texel = 1.0 / _PixelSize.xy;
                    float2 uvCenter = (floor(IN.texcoord * _PixelSize.xy) + 0.5) * texel;
                    float depth = 5.0;
                    [unroll]
                    for (int k = 4; k >= 1; k--)
                    {
                        float2 o = texel * (float)k;
                        float a0 = tex2Dlod(_MainTex, float4(uvCenter + float2(o.x, 0.0), 0.0, 0.0)).a;
                        float a1 = tex2Dlod(_MainTex, float4(uvCenter - float2(o.x, 0.0), 0.0, 0.0)).a;
                        float a2 = tex2Dlod(_MainTex, float4(uvCenter + float2(0.0, o.y), 0.0, 0.0)).a;
                        float a3 = tex2Dlod(_MainTex, float4(uvCenter - float2(0.0, o.y), 0.0, 0.0)).a;
                        depth = min(min(a0, a1), min(a2, a3)) < 0.5 ? (float)k : depth;
                    }

                    bool bEdge = 1.5 < depth && depth < 4.5;
                    bool bBodyZone = false == bEdge && (NearColor(texRgb, CARD_BODY) || (4.5 < depth && (NearColor(texRgb, CARD_WALL0) || NearColor(texRgb, CARD_WALL1))));

                    if (bEdge || bBodyZone)
                    {
                        float auroraTime = 0.0 >= _CardAuroraTime ? _Time.y : _CardAuroraTime;
                        float tq = floor(auroraTime * 30.0) / 30.0;
                        float T = tq * _AuroraSpeed;
                        // 카드마다 다른 무늬: 시드(정점 색 B)로 노이즈 위치와 띠 위상을 옮긴다.
                        float seed = IN.color.b;
                        float so = seed * 41.3;
                        float wx = p.x * 0.06 + 0.7 * AuroraVNoise(p.y * 0.035 + T * 0.10 + so, 3.1) + so;
                        float wy = p.y * 0.03 - T * 0.12 + so * 0.7;
                        float n = AuroraFbm(wx + T * 0.04, wy);
                        float band = frac(n * _AuroraBandScale + T * 0.05 + seed);

                        float amount = 0.0;
                        float strong = 0.0;
                        if (bEdge)
                        {
                            if (band < 0.10) { amount = _AuroraMix.x; strong = 1.0; }
                            else if (band < 0.24) { amount = _AuroraMix.y; }
                        }
                        else
                        {
                            if (band < 0.08) { amount = _AuroraMix.z; strong = 1.0; }
                            else if (band < 0.20) { amount = _AuroraMix.w; }
                        }

                        if (0.0 < amount)
                        {
                            float3 arc;
                            if (_AuroraRainbow > 0.5) arc = AuroraPrism(AuroraPerimeter(p), tq, strong);
                            else arc = ToMixSpace(strong > 0.5 ? _AuroraColor1.rgb : _AuroraColor0.rgb);
                            col = FromMixSpace(lerp(ToMixSpace(col), arc, amount));
                        }
                    }
                }

                float outAlpha = mask * IN.color.a;

                if (bGrade)
                {
                    // 공개 연출(R 채널 = 진행도, 1이면 완전히 레어): 대각 광택 띠가 왼쪽 위에서 오른쪽 아래로 지나가며 레어로 바꾼다.
                    // 띠가 지나간 자리는 레어 팔레트, 아직 안 지나간 자리는 투명(아래의 원래 프레임이 그대로 보인다). 정수 픽셀 단위로 끊어 움직인다.
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
                }
                else
                {
                    // 일반 앞면: 밝기(G 채널)만 rgb 모두에 곱한다(B 채널은 오로라 시드라 색으로 쓰지 않는다).
                    col *= IN.color.g;
                }

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
