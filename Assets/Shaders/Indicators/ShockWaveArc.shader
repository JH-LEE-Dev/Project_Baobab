Shader "Custom/ShockWaveArc"
{
    // 충격파 본체(부채꼴 파동) + 잔상 파동. ShockWaveVisualRunner가 판정과 같은 반경·각도 값을 MaterialPropertyBlock으로 넘기고,
    // 픽셀(32 PPU 게임 격자)마다 파동 안인지와 색을 여기서 정한다(회전베기 칼날과 같은 방식).
    // 구성: 앞 가장자리 빛나는 마루(HDR, 블룸에 번짐) -> 흰 덩어리 -> 뒤로 식는 금빛 띠 -> 체커 도트로 흩어지는 가시.
    // 시작 순간 하얗게 번쩍이며 뒤로 두꺼워지고(_TrailTime 기준), 사라질 때는 투명해지는 대신 색이 한 단계씩 식으며 체커로 부서진다(_Alpha 기준).
    // 출력은 BrandWrapGlow와 같다(색 * lerp(_GlowMin, _GlowMax, 발광 세기), 알파 1). 색 a = 발광 세기.
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        _MinRadius("Min Radius", Range(0, 1)) = 0.3
        _MaxRadius("Max Radius", Range(0, 1)) = 0.5
        _Angle("Angle", Range(0, 360)) = 120
        _AngleEdgeFade("Angle Edge Fade", Range(0.001, 0.5)) = 0.08
        _AttackDir("Attack Direction", Vector) = (1, 0, 0, 0)
        _Alpha("Alpha", Range(0, 1)) = 1
        _TrailAlpha("Trail Alpha", Range(0, 1)) = 0.45
        _TrailOffset("Trail Offset", Range(0, 0.5)) = 0.1
        _TrailThickness("Trail Thickness", Range(0.001, 0.08)) = 0.03
        _TrailNoise("Trail Noise", Range(0, 0.25)) = 0.15
        _TrailFrequency("Trail Frequency", Range(1, 32)) = 18
        _TrailTime("Trail Time", Float) = 0
        _TrailSeed("Trail Seed", Float) = 0

        _GlowMin("Glow Min (HDR 발광 최소 배율, 발광 세기 0)", Float) = 1.0
        _GlowMax("Glow Max (HDR 발광 최대 배율, 발광 세기 1)", Float) = 2.4
        _RimPx("Rim Px (빛나는 마루 두께, 게임 픽셀)", Float) = 5
        _ColorWhite("White (rgb, a = 발광 세기)", Vector) = (1, 1, 1, 1)
        _ColorLight("Light", Vector) = (1, 0.9255, 0.698, 0.0784)
        _ColorMid("Mid", Vector) = (1, 0.7608, 0.3216, 0)
        _ColorDark("Dark", Vector) = (0.8392, 0.4706, 0.1569, 0)

        // 러너가 매 프레임 넘기는 값
        _Center("Center (xy 월드), 그림 반지름(z), 픽셀/유닛(w)", Vector) = (0, 0, 1, 32)
        _Echo("잔상 (안쪽 반경, 바깥 반경, 켜짐, 식는 정도)", Vector) = (0, 0, 0, 0)
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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _MinRadius;
                float _MaxRadius;
                float _Angle;
                float _AngleEdgeFade;
                float4 _AttackDir;
                float _Alpha;
                float _TrailAlpha;
                float _TrailOffset;
                float _TrailThickness;
                float _TrailNoise;
                float _TrailFrequency;
                float _TrailTime;
                float _TrailSeed;
                float _GlowMin;
                float _GlowMax;
                float _RimPx;
                float4 _ColorWhite;
                float4 _ColorLight;
                float4 _ColorMid;
                float4 _ColorDark;
                float4 _Center;
                float4 _Echo;
            CBUFFER_END

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), u);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS.xy;
                OUT.color = IN.color;
                return OUT;
            }

            // 정점 색(로그라이크 포자 충격파의 초록 틴트 등)을 곱하고 발광 배율을 건다.
            half4 Output(float4 _color, half4 _tint, float _glow)
            {
                float glow = lerp(_GlowMin, _GlowMax, saturate(_glow));
                return half4((half3)(_color.rgb * _tint.rgb * glow), 1.0h);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 이 조각이 속한 게임 픽셀(정수)과 중심에서의 칸 수
                float ppu = _Center.w;
                float visualRadius = _Center.z;
                float2 pixel = floor(IN.positionWS * ppu);
                int ix = (int)(pixel.x - floor(_Center.x * ppu));
                int iy = (int)(pixel.y - floor(_Center.y * ppu));
                bool bChecker = 0 == ((ix + iy) & 1);

                // 픽셀 중심을 스프라이트 uv(가로 반지름 visualRadius, 세로 절반인 등각 타원)로 옮긴다.
                float2 offset = (pixel + 0.5) / ppu - _Center.xy;
                float2 centered = float2(offset.x / visualRadius, offset.y / (visualRadius * 0.5));
                float2 uv = centered * 0.5 + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0)) discard;
                half4 tex = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0);
                if (tex.a <= 0.0) discard;

                float radius = length(centered);
                float2 dir = radius > 0.0001 ? centered / radius : float2(1.0, 0.0);
                float cosThreshold = cos(radians(_Angle * 0.5));
                float angleDot = dot(dir, normalize(_AttackDir.xy));
                float angleMask = _Angle >= 359.0 ? 1.0 : smoothstep(cosThreshold, cosThreshold + _AngleEdgeFade, angleDot);
                if (angleMask <= 0.0) discard;
                // 부채꼴 양옆 가장자리는 체커 도트로 흩어진다.
                if (angleMask < 0.55 && false == bChecker) discard;

                float angleCoord = atan2(dir.y, dir.x);
                float noiseA = Noise1D(angleCoord * _TrailFrequency + _TrailTime * 8.0 + _TrailSeed * 37.17);
                float noiseB = Noise1D(angleCoord * (_TrailFrequency * 0.43) - _TrailTime * 5.0 + _TrailSeed * 91.73 + 17.0);
                float jagged = (noiseA * 0.7 + noiseB * 0.3) * 2.0 - 1.0;
                float inner = saturate(_MinRadius - _TrailOffset + jagged * _TrailNoise);

                // 등장: 처음 0.07초 하얗게 번쩍이고, 앞 가장자리(판정)는 그대로 둔 채 뒤로 두꺼워졌다가 돌아온다.
                float t = _TrailTime;
                float flash = 1.0 - smoothstep(0.0, 1.0, saturate(t / 0.07));
                float pop = t < 0.04 ? 0.5 * t / 0.04 : 0.5 * max(0.0, 1.0 - (t - 0.04) / 0.1);
                inner = max(0.0, _MaxRadius - (_MaxRadius - inner) * (1.0 + pop));

                // 반지름 방향 정규화 1이 화면에서 몇 게임 픽셀인지(등각 타원 보정)
                float pxPerUnit = visualRadius * ppu * sqrt(dir.x * dir.x + 0.25 * dir.y * dir.y);
                float cool = saturate(1.0 - _Alpha);

                // 본체
                if (radius <= _MaxRadius && radius >= inner)
                {
                    float thickness = max(0.0001, _MaxRadius - inner);
                    float across = (radius - inner) / thickness; // 0 = 뒤(안쪽), 1 = 앞(바깥)
                    float pxToFront = (_MaxRadius - radius) * pxPerUnit;
                    float b = across + flash * 0.7 - cool * 1.25;
                    float rimPx = _RimPx * (1.0 - cool * 0.7) + flash * 3.0;

                    if (pxToFront < rimPx && cool < 0.55) return Output(_ColorWhite, IN.color, 1.0);
                    if (b > 0.6) return Output(_ColorWhite, IN.color, flash * 0.8);
                    if (b > 0.4) return Output(_ColorLight, IN.color, _ColorLight.a + flash * 0.5);
                    if (b > 0.22) return Output(_ColorMid, IN.color, _ColorMid.a);
                    if (b > 0.08)
                    {
                        if (cool > 0.6 && false == bChecker) discard;
                        return Output(_ColorDark, IN.color, _ColorDark.a);
                    }
                    if (b > -0.2 && bChecker) return Output(_ColorDark, IN.color, _ColorDark.a);
                    discard;
                }

                // 잔상 파동: 본체 뒤를 조금 늦게 따라오는 얇은 두 번째 파동
                if (_Echo.z > 0.5 && radius < inner)
                {
                    float echoOuter = _Echo.y;
                    float echoInner = saturate(_Echo.x + jagged * _TrailNoise * 0.6);
                    if (radius <= echoOuter && radius >= echoInner)
                    {
                        float echoCool = saturate(_Echo.w);
                        float echoAcross = (radius - echoInner) / max(0.0001, echoOuter - echoInner);
                        float echoPx = (echoOuter - radius) * pxPerUnit;
                        if (echoCool > 0.75 && false == bChecker) discard;
                        if (echoPx < 2.0 && echoCool < 0.4) return Output(_ColorLight, IN.color, 0.35);
                        float eb = echoAcross - echoCool;
                        if (eb > 0.55) return Output(_ColorMid, IN.color, _ColorMid.a);
                        if (eb > 0.2 || bChecker) return Output(_ColorDark, IN.color, _ColorDark.a);
                        discard;
                    }
                }

                // 본체 바로 뒤 꼬리: 체커 도트
                float behind = (inner - radius) * pxPerUnit;
                float trailPx = _TrailThickness * 2.5 * pxPerUnit + 2.0;
                if (radius < inner && behind < trailPx && bChecker && cool < 0.5)
                {
                    if (behind > trailPx * 0.5 && 0 != ((ix >> 1) & 1)) discard;
                    return Output(_ColorDark, IN.color, _ColorDark.a);
                }

                discard;
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
}
