Shader "Custom/RestAreaEllipse"
{
    // 휴식 특성의 휴식 범위 타원. 오브젝트 스케일이 곧 타원 지름(2rx, 2ry)이고, UV 0~1 쿼드 위에 그린다.
    // 픽셀아트와 어울리도록 화면 대신 월드 좌표를 PPU 격자로 스냅해서 계단이 맞게 끊긴다.
    Properties
    {
        [Header(Color)]
        _RimColor("Rim Color", Color) = (1, 0.82, 0.45, 1)
        _InnerColor("Inner Color", Color) = (1, 0.52, 0.2, 1)

        [Header(Rim)]
        _RimWidth("Rim Width (Units)", Float) = 0.0625
        _RimAlpha("Rim Alpha", Range(0, 1)) = 0.9

        [Header(Dash Ring)]
        _DashGap("Gap From Rim (Units)", Float) = 0.0625
        _DashWidth("Dash Width (Units)", Float) = 0.0625
        _DashCount("Dash Count", Float) = 36
        _DashSpeed("Dash Speed (Cycles Per Sec)", Float) = 0.35
        _DashAlpha("Dash Alpha", Range(0, 1)) = 0.55

        [Header(Fill)]
        _FillAlpha("Fill Alpha", Range(0, 1)) = 0.16
        _FillStart("Fill Start", Range(0, 1)) = 0.35
        _FillSteps("Fill Steps", Float) = 4

        [Header(Ripple)]
        _RippleSpeed("Ripple Speed (Per Sec)", Float) = 0.22
        _RippleWidth("Ripple Width (Units)", Float) = 0.09375
        _RippleAlpha("Ripple Alpha", Range(0, 1)) = 0.35

        [Header(Flicker)]
        _Flicker("Flicker Strength", Range(0, 1)) = 0.12
        _BreathSpeed("Rim Breath Speed", Float) = 1.6

        [Header(Pixel)]
        _PixelsPerUnit("Pixels Per Unit", Float) = 32

        [Header(Runtime)]
        _Fade("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _InnerColor;
                float _RimWidth;
                float _RimAlpha;
                float _DashGap;
                float _DashWidth;
                float _DashCount;
                float _DashSpeed;
                float _DashAlpha;
                float _FillAlpha;
                float _FillStart;
                float _FillSteps;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleAlpha;
                float _Flicker;
                float _BreathSpeed;
                float _PixelsPerUnit;
                float _Fade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // [inner, outer] 구간이면 1. 픽셀 스냅 좌표라 부드러운 경계 대신 계단으로 끊는다.
            float Band(float _d, float _inner, float _outer)
            {
                return step(_inner, _d) * step(_d, _outer);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 오브젝트 스케일 = 타원 지름. 반지름은 그 절반.
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleY = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                float2 radius = max(float2(scaleX, scaleY) * 0.5, 0.0001);
                float2 center = float2(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w);

                float ppu = max(_PixelsPerUnit, 1.0);
                float2 snappedWS = (floor(IN.positionWS.xy * ppu) + 0.5) / ppu;
                float2 p = (snappedWS - center) / radius;

                // 1 = 테두리. 쿼터뷰 비율(2:1)이 스케일에 들어 있으므로 정규화 공간에선 원이다.
                float d = length(p);
                float unitToNorm = 1.0 / radius.x;

                float t = _Time.y;

                // 모닥불처럼 일렁이는 밝기. 서로 안 맞는 주기 셋을 섞어 반복이 티 나지 않게 한다.
                float flicker = 1.0 + _Flicker * (sin(t * 7.3) * 0.5 + sin(t * 13.1 + 1.7) * 0.3 + sin(t * 23.7 + 0.4) * 0.2);
                float breath = 0.85 + 0.15 * sin(t * _BreathSpeed * 6.2831853);

                // 테두리 실선
                float rimW = _RimWidth * unitToNorm;
                float rim = Band(d, 1.0 - rimW, 1.0);

                // 안쪽으로 한 칸 띄운 점선 고리. 시계 방향으로 천천히 돈다.
                float dashOuter = 1.0 - rimW - _DashGap * unitToNorm;
                float dashInner = dashOuter - _DashWidth * unitToNorm;
                float angle01 = atan2(p.y, p.x) * (1.0 / 6.2831853) + 0.5;
                float dashOn = step(0.5, frac(angle01 * _DashCount + t * _DashSpeed));
                float dash = Band(d, dashInner, dashOuter) * dashOn;

                // 가장자리로 갈수록 짙어지는 채움. 계단 단계로 끊어 픽셀아트 톤에 맞춘다.
                float steps = max(_FillSteps, 1.0);
                float fillT = saturate((d - _FillStart) / max(1.0 - _FillStart, 0.0001));
                fillT = floor(fillT * steps) / steps;
                float fill = fillT * _FillAlpha;

                // 중심(모닥불)에서 퍼져 나가는 온기 파문 두 줄. 바깥으로 갈수록 옅어진다.
                float rippleW = _RippleWidth * unitToNorm;
                float ripple = 0.0;
                [unroll]
                for (int i = 0; i < 2; i++)
                {
                    float r = frac(t * _RippleSpeed + i * 0.5);
                    float ring = Band(d, r - rippleW, r);
                    ripple = max(ripple, ring * (1.0 - r) * smoothstep(0.0, 0.15, r));
                }
                ripple *= step(d, dashInner);

                float inside = step(d, 1.0);

                float alpha = max(max(rim * _RimAlpha * breath, dash * _DashAlpha), max(ripple * _RippleAlpha, fill));
                alpha *= inside * _Fade * flicker;

                half3 rgb = lerp(_InnerColor.rgb, _RimColor.rgb, saturate(max(rim, dash) + d * d));
                half4 col = half4(rgb, saturate(alpha));
                clip(col.a - 0.005);
                return col;
            }
            ENDHLSL
        }
    }
}
