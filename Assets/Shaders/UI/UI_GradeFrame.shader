Shader "UI/GradeFrame"
{
    // 슬롯의 안쪽 흰 선(RareLine 스프라이트)에만 등급 연출을 그린다. 짙은 갈색 외곽 테두리와 타일 채움은
    // 일반 슬롯과 완전히 같아서, 등급 슬롯도 같은 골격 위에 안쪽 선만 달라지는 구조가 된다.
    //
    // 레어도는 "단계가 오를수록 연출이 한 겹씩 더해지는" 방식으로 표현한다.
    //   금(1단계)     : 따뜻한 금색 선이 느리게 맥동
    //   다이아(2단계) : 청백색 선 + 주기적으로 지나가는 흰 광택선 + 모서리 십자 반짝임 + 옅은 안쪽 빛
    //   프리즘(3단계) : 흐르는 무지개 선 + 광택선 + 모서리 십자 반짝임 + 더 강한 안쪽 빛
    //
    // 좌표는 이미지 로컬 좌표(캔버스 1유닛 = 기준 해상도 1픽셀)를 픽셀 중심으로 스냅해 쓴다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (RareLine)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [KeywordEnum(Gold, Diamond, Prism)] _Mode("Mode", Float) = 0
        _ColorA("Color A", Color) = (1, 0.78, 0.2, 1)
        _ColorB("Color B", Color) = (1, 0.93, 0.55, 1)
        _Speed("Pulse Speed", Float) = 1.6

        [Header(Glint)]
        _GlintStrength("Glint Strength (0 = off)", Range(0,1)) = 0
        _SweepSpeed("Sweep Speed (cycles/sec)", Float) = 0.45
        _GlintWidth("Glint Width (px)", Range(0.5,6)) = 2
        _CornerSparkle("Corner Sparkle (0 = off)", Range(0,1)) = 0

        [Header(Prism)]
        _HueSpeed("Hue Speed (cycles/sec)", Float) = 0.25
        _Saturation("Saturation", Range(0,1)) = 0.8
        _HueSteps("Hue Steps (0 = smooth)", Range(0,16)) = 8

        [Header(Inner Glow)]
        // 선 안쪽으로 번지는 옅은 빛. 단계가 높을수록 강하다. 0이면 선만 그린다.
        _InnerGlow("Inner Glow Alpha (0 = off)", Range(0,1)) = 0
        _GlowDepth("Inner Glow Depth (px)", Range(1,6)) = 3

        // UI Mask Support
        // UI 정점의 로컬 좌표는 캔버스 배치 때 캔버스 기준으로 바뀌므로(슬롯 위치가 달라지면 패턴이 어긋난다)
        // 픽셀 좌표는 UV와 스프라이트 크기로 구한다. 스프라이트 한 장짜리 텍스처(아틀라스 아님)가 전제다.
        _PixelSize("Pixel Size (sprite w, h)", Vector) = (24, 24, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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
            #pragma multi_compile_local _MODE_GOLD _MODE_DIAMOND _MODE_PRISM
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

            float4 _ColorA;
            float4 _ColorB;
            float _Speed;
            float _GlintStrength;
            float _SweepSpeed;
            float _GlintWidth;
            float _CornerSparkle;
            float _HueSpeed;
            float _Saturation;
            float _HueSteps;
            float _InnerGlow;
            float _GlowDepth;

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
                OUT.localPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord);
                float lineMask = step(0.5, tex.a);

                float2 p = floor((IN.texcoord - 0.5) * _PixelSize.xy) + 0.5;
                float t = _Time.y;

                // 단계별 기본 색
                float3 lineColor;
                float hue = 0.0;
                #if defined(_MODE_DIAMOND)
                    lineColor = _ColorA.rgb;
                #elif defined(_MODE_PRISM)
                    float angle = atan2(p.y, p.x) * 0.15915494 + 0.5;
                    hue = frac(angle + t * _HueSpeed);
                    if (_HueSteps > 0.5)
                        hue = floor(hue * _HueSteps) / _HueSteps;
                    lineColor = HueToRgb(hue, _Saturation);
                #else
                    float wave = 0.5 + 0.5 * sin(t * _Speed + (p.x - p.y) * 0.3);
                    lineColor = lerp(_ColorA.rgb, _ColorB.rgb, wave);
                #endif

                // 광택선: 선 위를 대각선으로 지나가는 날카로운 흰 줄
                float u = p.x + p.y;
                float target = lerp(-32.0, 32.0, frac(t * _SweepSpeed));
                float glint = step(abs(u - target), _GlintWidth * 0.5) * _GlintStrength;
                lineColor = lerp(lineColor, float3(1.0, 1.0, 1.0), glint);

                // 모서리 십자 반짝임: 네 모서리가 서로 다른 박자로 번쩍인다.
                if (_CornerSparkle > 0.0)
                {
                    float2 corner = float2(sign(p.x), sign(p.y)) * 10.5;
                    float2 dc = abs(p - corner);
                    float cornerId = (corner.x > 0.0 ? 1.0 : 0.0) + (corner.y > 0.0 ? 2.0 : 0.0);
                    float pulse = saturate(sin(t * 2.3 + cornerId * 1.7));
                    pulse = pulse * pulse * pulse * pulse;
                    float cross = step(min(dc.x, dc.y), 0.5) * step(max(dc.x, dc.y), 2.5)
                                * step(max(abs(p.x), abs(p.y)), 11.6);
                    lineColor = lerp(lineColor, float3(1.0, 1.0, 1.0), cross * pulse * _CornerSparkle);
                }

                // 획득 버스트: Image.color 의 R 채널(1 - 세기)로 받는다(머티리얼 복제 없이 슬롯별 값 전달).
                float burst = saturate(1.0 - IN.color.r);
                lineColor = lerp(lineColor, float3(1.0, 1.0, 1.0), burst);

                half4 color;
                color.rgb = lineColor;
                color.a = lineMask;

                // 안쪽 빛: 선에서 안쪽으로 갈수록 옅어지는 같은 계열 색(타일 안쪽에만, 모서리 컷 바깥은 제외)
                float glowAmount = max(_InnerGlow, burst * 0.6);
                float glowDepth = _GlowDepth + burst * 3.0;
                if (glowAmount > 0.0 && lineMask < 0.5)
                {
                    // 등급 선(RareLine)은 가장자리에서 한 칸 안쪽(±10.5)에 있으므로, 안쪽 빛은 그 선부터 안쪽으로만 번진다.
                    // (±11.5 바깥 줄까지 칠하면 갈색 테두리 위에 회색 띠가 얹힌다)
                    float edgeDist = 10.5 - max(abs(p.x), abs(p.y));
                    float insideTile = step(abs(p.x) + abs(p.y), 18.5);
                    float glow = saturate(1.0 - edgeDist / glowDepth) * step(0.0, edgeDist) * insideTile;
                    #if defined(_MODE_PRISM)
                        float3 glowColor = HueToRgb(hue, _Saturation);
                    #else
                        float3 glowColor = lineColor;
                    #endif
                    color.rgb = lerp(glowColor, float3(1.0, 1.0, 1.0), burst);
                    color.a = glow * glowAmount;
                }

                // 알파만 Image.color 를 따른다(R 채널은 버스트 전달용이라 색에 곱하지 않는다).
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
