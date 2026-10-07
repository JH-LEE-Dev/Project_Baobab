Shader "UI/AxeOverheatAura"
{
    // 도끼 HUD의 과열 아우라. 도끼 윤곽을 균일하게 감싸는 동심원 오라(안쪽 하늘빛 림 -> 파랑 -> 진파랑 -> 규칙 디더로 옅어지는 바깥 테두리)와
    // 둘레를 도는 밝은 빛 띠(파랑/보라/청록), 정해진 자리에서 깜빡이는 반짝임, 점화 충격파를 픽셀 격자 위에서 셰이더로 그린다.
    // 바깥으로 갈수록 옅어지는 부분은 4x4 규칙(Bayer) 디더라서 무작위 노이즈가 아니라 도트 아트의 글로우 그라데이션처럼 읽힌다.
    // CPU는 아무것도 하지 않는다.
    //
    // _MainTex 는 도끼 스프라이트에서 미리 구운 "실루엣 거리 텍스처"(HUD_AxeOverheatAura가 스프라이트마다 한 번만 만든다)다.
    //   R = 실루엣까지의 3-4 챔퍼 거리(칸 수 * 3, 0~255), G = 실루엣 안쪽(255) / 바깥(0)
    // 파손으로 도끼 스프라이트가 바뀔 때는 텍스처만 교체하면 되고, 아우라는 새 실루엣을 바로 따라간다.
    //
    // 픽셀 느낌: 모든 계산은 텍스처 픽셀 격자(= HUD 캔버스 1픽셀)의 정수 좌표에서 하고, 색은 계단식으로 끊고, 반투명을 쓰지 않는다(알파는 0 또는 1).
    // 불꽃의 일렁임은 12fps 계단식 시간으로만 움직인다.
    Properties
    {
        [PerRendererData] _MainTex("Distance Texture", 2D) = "black" {}
        _Color("Tint", Color) = (1,1,1,1)

        [Header(Timing)]
        // HUD_AxeOverheatAura가 켜고 끌 때만 넣는다(Time.timeSinceLevelLoad 기준). _EndTime < _StartTime 이면 계속 타는 중이다.
        _StartTime("Start Time", Float) = -1000
        _EndTime("End Time", Float) = -2000
        _ExtinguishDuration("Extinguish Duration", Float) = 0.45
        _BreathFps("Breath Fps", Float) = 6

        [Header(Aura)]
        _AuraThickness("Aura Thickness (px)", Float) = 5
        _BreathAmp("Breath Amplitude (px)", Float) = 1.5
        _DitherStart("Dither Starts At (0..1 of thickness)", Range(0.2,0.9)) = 0.55
        _SweepSpeed("Light Band Laps Per Second", Float) = 0.35
        _SweepWidth("Light Band Width (0..1 of perimeter)", Range(0.05,0.5)) = 0.2

        [Header(Color)]
        _ColorIce("Ice", Color) = (0.59, 0.84, 1, 1)
        _ColorBlue("Blue", Color) = (0.16, 0.43, 1, 1)
        _ColorDeepBlue("Deep Blue", Color) = (0.10, 0.27, 0.86, 1)
        _ColorViolet("Violet", Color) = (0.51, 0.27, 1, 1)
        _ColorCyan("Cyan", Color) = (0.24, 1, 0.9, 1)

        
        [Header(Ignite)]
        _IgniteRingScale("Ignite Ring Scale", Float) = 1.6

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
            "CanUseSpriteAtlas"="False"
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            float _StartTime;
            float _EndTime;
            float _ExtinguishDuration;
            float _BreathFps;
            float _AuraThickness;
            float _BreathAmp;
            float _DitherStart;
            float _SweepSpeed;
            float _SweepWidth;
            float4 _ColorIce;
            float4 _ColorBlue;
            float4 _ColorDeepBlue;
            float4 _ColorViolet;
            float4 _ColorCyan;
            float _IgniteRingScale;

            static const float TWO_PI = 6.28318531;

            // 4x4 Bayer 규칙 디더 문턱(0~1). 같은 픽셀 좌표는 항상 같은 값이라 무작위 노이즈가 아니라 정해진 점 패턴이 된다.
            float Bayer4(float2 _pixel)
            {
                int x = (int)fmod(_pixel.x, 4.0);
                int y = (int)fmod(_pixel.y, 4.0);
                int index = y * 4 + x;
                float matrix16[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (matrix16[index] + 0.5) / 16.0;
            }

            // 점화(솟았다 안착)와 소화(줄어듦)를 합친 세기. 1/12 단위 계단식이라 도트 애니메이션처럼 보인다.
            float AuraIntensity(float _now, out float _elapsed)
            {
                _elapsed = max(0.0, _now - _StartTime);
                float rise = 1.0 - pow(1.0 - saturate(_elapsed / 0.35), 2.0);
                float flare = 0.6 * max(0.0, 1.0 - abs(_elapsed - 0.14) / 0.14);
                float intensity = rise + flare;

                if (_EndTime >= _StartTime)
                {
                    float u = saturate((_now - _EndTime) / max(0.01, _ExtinguishDuration));
                    intensity *= pow(1.0 - u, 1.4);
                }

                return floor(intensity * 12.0) / 12.0;
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
                float2 texSize = _MainTex_TexelSize.zw;
                float2 pixel = floor(IN.texcoord * texSize);          // 텍스처 픽셀 정수 좌표(왼쪽 아래 원점)
                float2 sampleUv = (pixel + 0.5) / texSize;
                half4 map = tex2D(_MainTex, sampleUv);
                float distPx = map.r * 255.0 / 3.0;                    // 실루엣까지의 거리(px). 안쪽은 0
                bool bSolid = map.g > 0.5;

                float now = _Time.y;
                float elapsed;
                float intensity = AuraIntensity(now, elapsed);

                float2 centered = pixel + 0.5 - texSize * 0.5;         // 텍스처 중심 기준 좌표

                float3 rgb = float3(0.0, 0.0, 0.0);
                float alpha = 0.0;

                // ---- 균일한 동심원 오라 ----
                // 실루엣에서 바깥으로: 하늘빛 림(1px) -> 파랑 -> 진파랑 -> (규칙 디더로 옅어지는 가장자리). 둘레 전체가 거의 같은 두께이고,
                // 6프레임 주기로 한 칸씩 숨 쉬듯 커졌다 작아진다. 밝은 빛 띠가 둘레를 한 바퀴 돌며 그 자리의 오라를 보라/청록으로 물들이고 살짝 두껍게 한다.
                if (intensity > 0.0)
                {
                    float angle01 = atan2(centered.y, centered.x) / TWO_PI + 0.5;

                    float breathFrame = floor(now * _BreathFps);
                    float breath = 0.5 + 0.5 * sin(breathFrame * (TWO_PI / 6.0));
                    // 둘레를 따라 완만하게 일렁이는 물결(3주기). 정수 프레임으로만 움직인다.
                    float undulate = 0.5 + 0.5 * sin(angle01 * TWO_PI * 3.0 + breathFrame * 0.9);

                    // 둘레를 도는 빛 띠: 앞선이 한 바퀴 돈다. 띠의 중심일수록 강하다.
                    float front = frac(now * _SweepSpeed);
                    float delta = abs(angle01 - front);
                    delta = min(delta, 1.0 - delta);
                    float sweep = saturate(1.0 - delta / max(0.05, _SweepWidth));
                    float lap = floor(now * _SweepSpeed);
                    float3 sweepColor = 0.0 == fmod(lap, 2.0) ? _ColorViolet.rgb : _ColorCyan.rgb;

                    float thickness = floor((_AuraThickness + breath * _BreathAmp + undulate + sweep * 2.0) * intensity + 0.5);

                    if (bSolid)
                    {
                        // 실루엣 안쪽에도 진파랑을 깐다(도끼 이미지가 위에서 덮는다). 파손으로 스프라이트가 비는 칸의 빈틈을 메운다.
                        rgb = _ColorDeepBlue.rgb;
                        alpha = 1.0;
                    }
                    else if (distPx > 0.0 && distPx <= thickness + 0.01)
                    {
                        float t = distPx / max(1.0, thickness);          // 0 = 윤곽, 1 = 바깥 끝
                        float threshold = Bayer4(pixel);

                        // 바깥쪽은 규칙 디더로 점점 성기게(도트 글로우). 빛 띠 위에서는 더 멀리까지 촘촘하다.
                        float ditherT = saturate((t - _DitherStart) / max(0.05, 1.0 - _DitherStart));
                        bool bVisible = ditherT <= 0.0 || threshold >= ditherT * (1.0 - sweep * 0.5);

                        if (bVisible)
                        {
                            float3 color;
                            if (distPx <= 1.0)
                                color = lerp(_ColorIce.rgb, float3(1, 1, 1), 0.25 * sweep);   // 윤곽에 붙는 밝은 림
                            else if (t <= 0.5)
                                color = _ColorBlue.rgb;
                            else
                                color = _ColorDeepBlue.rgb;

                            // 빛 띠: 중심은 보라/청록으로 확실히, 가장자리는 디더로 섞는다
                            if (sweep > 0.0 && (sweep > 0.6 || threshold < sweep))
                                color = sweepColor;

                            rgb = color;
                            alpha = 1.0;
                        }
                    }

                    // 정해진 자리의 반짝임: 둘레를 24등분해서 조각마다 위상이 다르게 깜빡이는 1px 점(바깥 끝 바로 밖). 무작위로 떠오르지 않는다.
                    if (!bSolid && distPx > thickness + 0.5 && distPx <= thickness + 2.5)
                    {
                        float sector = floor(angle01 * 24.0);
                        float twinkle = floor(now * 4.0) + sector * 3.0;
                        bool bLit = 0.0 == fmod(twinkle, 8.0) && fmod(pixel.x + pixel.y, 3.0) < 0.5;
                        if (bLit)
                        {
                            rgb = _ColorIce.rgb;
                            alpha = 1.0;
                        }
                    }
                }

                // ---- 점화 충격파: 도끼 중심에서 계단식으로 퍼지는 점선 타원 ----
                if (elapsed < 0.4 && _EndTime < _StartTime)
                {
                    float t = elapsed - 0.02;
                    if (t >= 0.0)
                    {
                        float ringStep = floor(t / 0.06);
                        float radius = (6.0 + ringStep * 6.0) * _IgniteRingScale;
                        float3 ringColor = ringStep < 2.0 ? float3(1, 1, 1) : (ringStep < 4.0 ? _ColorIce.rgb : _ColorBlue.rgb);
                        float size = ringStep < 3.0 ? 2.0 : 1.0;
                        float dotCount = 20.0;
                        float shift = 0.0 == fmod(ringStep, 2.0) ? 0.0 : TWO_PI / dotCount * 0.5;
                        float a = atan2(centered.y / 0.7, centered.x);
                        float k = round((a - shift) / (TWO_PI / dotCount));
                        float ang = shift + k * (TWO_PI / dotCount);
                        float2 dotPos = floor(float2(cos(ang) * radius, sin(ang) * radius * 0.7));
                        float2 dd = floor(centered) - dotPos;
                        if (dd.x >= 0.0 && dd.x < size && dd.y >= 0.0 && dd.y < size)
                        {
                            rgb = ringColor;
                            alpha = 1.0;
                        }
                    }
                }

                half4 color = half4(rgb, alpha * IN.color.a);

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
