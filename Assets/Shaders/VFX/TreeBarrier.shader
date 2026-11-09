Shader "Custom/VFX/TreeBarrier"
{
    // 3스테이지 배리어 나무를 감싸는 별빛 보호막. 나무 밑동(오브젝트 원점) 기준의 32 PPU 월드 픽셀 격자로 계산해 도트로 그린다.
    // 평소: 은은한 외곽선 + 안쪽 체크 디더 + 가장자리 쪽 격자 점 + 유리 반사광 + 가끔 반짝이는 별 +
    //       웅웅거림(껍질 전체가 숨 쉬듯 거의 안 보이다가 사알짝 드러남, 가운데에서 가장자리로 번지는 물결) +
    //       조명이 지나가듯 껍질 표면 전체를 미세하게 훑는 빛(웅웅거림과 따로 가서 껍질이 숨어 있을 때도 보인다).
    // 피격(_HitAge 0~): 껍질 전체가 하얗게 번쩍이고, 맞은 자리에서 고리가 퍼지며 2:1 마름모 격자(에너지 막)가 드러났다가 사라진다.
    // 파괴(_BreakAge 0~): 맞은 자리에서 격자를 따라 금이 번지고, 마름모 조각이 맞은 자리부터 차례로 하얗게 번쩍인 뒤 떨어져 나간다.
    // _HitAge/_BreakAge/_HitPoint는 VFX_TreeBarrier가 연출 중에만 MaterialPropertyBlock으로 넣는다(평소에는 머티리얼 기본값이라 SRP 배처에 묶인다).
    Properties
    {
        _CenterY ("Shell Center Y (px)", Float) = 22
        _RadiusX ("Shell Radius X (px)", Float) = 21
        _RadiusY ("Shell Radius Y (px)", Float) = 30
        _RimColor ("Rim Color", Color) = (0.67, 0.86, 1.0, 1)
        _FillColor ("Fill Color", Color) = (0.36, 0.57, 0.9, 1)
        _GlintColor ("Glint Color", Color) = (1, 1, 1, 1)
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.42
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.08
        _DitherAlpha ("Inner Dither Alpha", Range(0, 1)) = 0.09
        _LatticeAlpha ("Lattice Dot Alpha", Range(0, 1)) = 0.2
        _ShimmerPeriod ("Shimmer Period (s)", Float) = 4.5
        _SweepAlpha ("Surface Sweep Alpha", Range(0, 1)) = 0.07
        _SweepWidth ("Surface Sweep Width (px, sigma)", Float) = 7
        _SweepHDR ("Surface Sweep HDR Boost", Float) = 0.25
        _HumPeriod ("Hum Period (s)", Float) = 3.4
        _HumMin ("Hum Min Visibility", Range(0, 1)) = 0.05
        _HumMax ("Hum Max Visibility", Range(0, 2)) = 0.35
        _HumHDR ("Hum Rim HDR Boost", Float) = 0.1
        _HitHDR ("Hit HDR Boost", Float) = 3.0
        _HitPoint ("Hit Point (px, origin relative)", Vector) = (0, 22, 0, 0)
        _HitAge ("Hit Age (s)", Float) = 10
        _BreakAge ("Break Age (s, <0 none)", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "TreeBarrierPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CenterY;
                float _RadiusX;
                float _RadiusY;
                half4 _RimColor;
                half4 _FillColor;
                half4 _GlintColor;
                half _RimAlpha;
                half _FillAlpha;
                half _DitherAlpha;
                half _LatticeAlpha;
                float _ShimmerPeriod;
                half _SweepAlpha;
                float _SweepWidth;
                float _SweepHDR;
                float _HumPeriod;
                half _HumMin;
                half _HumMax;
                float _HumHDR;
                float _HitHDR;
                float4 _HitPoint;
                float _HitAge;
                float _BreakAge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldXY : TEXCOORD0;
            };

            static const float PPU = 32.0;
            static const float LatticePeriod = 16.0;  // 마름모 격자 간격(px, 조각 하나가 가로 16 x 세로 8). 선은 2:1 기울기(아이소 도트에서 가장 깔끔한 기울기)
            static const float HitDuration = 0.45;
            static const float HitSweepTime = 0.34;    // 피격 빛 띠가 표면을 다 지나가는 시간(HitDuration 이하)
            static const float HitSweepWidth = 6.0;    // 피격 빛 띠 폭(px, sigma)
            static const float HitGlowRadius = 9.0;    // 맞은 자리 빛 번짐 반경(px, sigma)
            static const float RippleSpeed = 170.0;   // 피격 고리가 퍼지는 속도(px/s)
            static const float CrackSpeed = 520.0;    // 파괴 시 금이 번지는 속도(px/s)
            static const float PopSpeed = 230.0;      // 파괴 시 조각이 떨어져 나가는 물결 속도(px/s)
            static const float PopStart = 0.08;
            static const float PopJitter = 0.07;
            static const float PopFlash = 0.035;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(world);
                output.worldXY = world.xy;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float Mod(float x, float m)
            {
                return x - m * floor(x / m);
            }

            // 껍질 타원식 값(1 이하면 안쪽). p는 원점(밑동) 기준 픽셀 중심 좌표.
            float ShellE(float2 p, float scale)
            {
                float2 d = float2(p.x / (_RadiusX * scale), (p.y - _CenterY) / (_RadiusY * scale));
                return dot(d, d);
            }

            // 4방향 이웃 중 하나라도 밖이면 외곽선(1px, 8연결, L자 이중 픽셀 없음)
            bool IsOutline(float2 p, float scale)
            {
                if (ShellE(p, scale) > 1.0) return false;
                return ShellE(p + float2(1, 0), scale) > 1.0 || ShellE(p - float2(1, 0), scale) > 1.0
                    || ShellE(p + float2(0, 1), scale) > 1.0 || ShellE(p - float2(0, 1), scale) > 1.0;
            }

            // 바닥 접지 타원(밑동 둘레) 외곽선
            bool IsGroundRing(float2 p, float rx, float ry)
            {
                float e = (p.x * p.x) / (rx * rx) + (p.y * p.y) / (ry * ry);
                if (e > 1.0) return false;
                float2 a = p + float2(1, 0); float2 b = p - float2(1, 0); float2 c = p + float2(0, 1); float2 d = p - float2(0, 1);
                return (a.x * a.x) / (rx * rx) + (a.y * a.y) / (ry * ry) > 1.0 || (b.x * b.x) / (rx * rx) + (b.y * b.y) / (ry * ry) > 1.0
                    || (c.x * c.x) / (rx * rx) + (c.y * c.y) / (ry * ry) > 1.0 || (d.x * d.x) / (rx * rx) + (d.y * d.y) / (ry * ry) > 1.0;
            }

            // 2:1 마름모 격자선 위 픽셀인지. 한 줄에 2픽셀씩 이어지는 계단(x, x+1 / 다음 줄 x+2, x+3)이라 선이 끊기지 않는다.
            bool IsLatticeLine(float2 ip)
            {
                return Mod(ip.x + 2.0 * ip.y, LatticePeriod) < 1.5 || Mod(ip.x - 2.0 * ip.y, LatticePeriod) < 1.5;
            }

            bool IsLatticeNode(float2 ip)
            {
                return Mod(ip.x + 2.0 * ip.y, LatticePeriod) < 0.5 && Mod(ip.x - 2.0 * ip.y, LatticePeriod) < 0.5;
            }

            // 픽셀이 속한 마름모 조각의 번호와 중심(원점 기준 px)
            float2 LatticeCell(float2 ip, out float2 center)
            {
                float u = floor((ip.x + 2.0 * ip.y) / LatticePeriod);
                float v = floor((ip.x - 2.0 * ip.y) / LatticePeriod);
                float s = (u + 0.5) * LatticePeriod; // x + 2y
                float t = (v + 0.5) * LatticePeriod; // x - 2y
                center = float2((s + t) * 0.5, (s - t) * 0.25);
                return float2(u, v);
            }

            void Over(inout half4 dst, half3 rgb, half a)
            {
                dst.rgb = lerp(dst.rgb, rgb, a);
                dst.a = dst.a + a * (1.0h - dst.a);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 origin = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                float2 originPx = floor(origin * PPU + 0.5);
                float2 ip = floor(input.worldXY * PPU) - originPx;   // 정수 픽셀(원점 기준)
                float2 p = ip + 0.5;                                    // 픽셀 중심

                float e = ShellE(p, 1.0);
                clip(1.0 - e);

                float seed = Hash21(originPx);
                float r = sqrt(e);
                bool bRim = IsOutline(p, 1.0);
                bool bChecker = Mod(ip.x + ip.y, 2.0) < 0.5;

                // ---------- 파괴: 조각이 떨어져 나간 자리는 그리지 않는다 ----------
                bool bBreaking = _BreakAge >= 0.0;
                float popFlash = 0.0;
                float crack = 0.0;
                if (bBreaking)
                {
                    float2 cellCenter;
                    float2 cell = LatticeCell(ip, cellCenter);
                    float cellDist = distance(cellCenter, _HitPoint.xy);
                    float tPop = PopStart + cellDist / PopSpeed + Hash21(cell + seed * 37.0) * PopJitter;
                    clip(tPop + PopFlash - _BreakAge);
                    popFlash = _BreakAge >= tPop ? 1.0 : 0.0;

                    float d = distance(p, _HitPoint.xy);
                    crack = (d < _BreakAge * CrackSpeed && IsLatticeLine(ip)) ? 1.0 : 0.0;
                }

                // ---------- 평소 ----------
                // 웅웅거림: 껍질이 숨 쉬듯 보일락 말락 한다. 박자는 나무마다 다르고, 주기가 서로 안 맞는 두 물결을 섞어
                // 기계적으로 똑같이 깜빡이지 않게 한다. 밝아지는 물결은 가운데에서 가장자리로 번져 나간다(웅- 하고 퍼지는 느낌).
                // 평소 층(채움·디더·격자 점·접지 타원·외곽선·반사광)에만 곱하고, 피격/파괴 연출에는 곱하지 않는다.
                half hum;
                {
                    float tau = 6.2831853;
                    float t = _Time.y / _HumPeriod + seed * 7.0 - r * 0.35;
                    float wave = 0.65 * sin(t * tau) + 0.35 * sin(t * tau * 0.618 + seed * 13.0);
                    float h = saturate(0.5 + 0.5 * wave);
                    h = h * h * (3.0 - 2.0 * h);
                    hum = (half)(h * h * h);   // 대부분은 거의 안 보이는 쪽에 머물다가, 물결 꼭대기에서만 사알짝 드러난다
                }
                half vis = lerp(_HumMin, _HumMax, hum);

                half4 col = half4(_FillColor.rgb, _FillAlpha * vis);

                // 안쪽 체크 디더 띠(외곽선 안쪽 2px 정도). 외곽선과 나란한 실선을 쓰지 않아 HUGGING을 피한다.
                float rimBand = 1.0 - 2.5 / min(_RadiusX, _RadiusY);
                if (!bRim && r > rimBand && bChecker) Over(col, _FillColor.rgb, _DitherAlpha * vis);

                // 가장자리 쪽에만 보이는 격자 점(구면이 휘는 느낌)
                if (!bRim && r > 0.62 && IsLatticeNode(ip)) Over(col, _RimColor.rgb, _LatticeAlpha * vis * saturate((r - 0.62) / 0.25));

                // 바닥 접지 타원: 앞쪽 반은 또렷하게, 뒤쪽 반은 점선으로 희미하게
                if (IsGroundRing(p, _RadiusX * 0.68, _RadiusX * 0.3))
                {
                    if (p.y < 0.0) Over(col, _RimColor.rgb, 0.26 * vis);
                    else if (Mod(ip.x, 3.0) < 1.0) Over(col, _RimColor.rgb, 0.1 * vis);
                }

                // 외곽선(또렷해질 때 살짝 빛이 번진다)
                half hdr = 1.0h;
                if (bRim)
                {
                    Over(col, _RimColor.rgb, saturate(_RimAlpha * vis));
                    hdr = (half)(1.0 + _HumHDR * hum);
                }

                // 유리 반사광: 0.78배 안쪽 타원 윤곽의 왼쪽 위 짧은 호 + 점 하나
                if (IsOutline(p, 0.78))
                {
                    float ang = atan2((p.y - _CenterY) / _RadiusY, p.x / _RadiusX);
                    if (ang > 1.95 && ang < 2.65) Over(col, _GlintColor.rgb, 0.5 * vis);
                }
                {
                    float2 dotPos = float2(floor(-_RadiusX * 0.42), floor(_CenterY + _RadiusY * 0.47));
                    if (all(ip == dotPos)) Over(col, _GlintColor.rgb, 0.55 * vis);
                }

                // 조명이 지나가듯 표면 전체를 부드럽게 훑는 빛(나무마다 시작 시각이 다르다).
                // 왼쪽 아래 -> 오른쪽 위 대각선 띠가 껍질을 가로지른다. 구면 가운데가 빛 쪽으로 불룩하게 앞서 나가
                // 띠가 표면을 따라 휘고, 가장자리(비스듬한 면)일수록 조금 더 밝게 받는다.
                {
                    float shimmerT = frac(_Time.y / _ShimmerPeriod + seed);
                    float margin = _SweepWidth * 3.0;
                    float bandPos = lerp(-_RadiusX * 0.5 - margin, _CenterY + _RadiusY + _RadiusX * 0.5 + margin, shimmerT);
                    float bulge = sqrt(saturate(1.0 - e)) * 6.0;
                    float bandDist = (p.y + p.x * 0.5 + bulge) - bandPos;
                    float sweep = exp(-(bandDist * bandDist) / (2.0 * _SweepWidth * _SweepWidth));
                    float fresnel = 0.55 + 0.45 * r;
                    // 웅웅거림과 따로 간다: 껍질이 거의 안 보일 때도 지나가는 빛은 미세하게 보여 배리어가 있다는 걸 알려 준다
                    Over(col, _GlintColor.rgb, _SweepAlpha * sweep * fresnel);
                    if (bRim) Over(col, _GlintColor.rgb, 0.25 * sweep);
                    else if (r > 0.45 && IsLatticeNode(ip)) Over(col, _GlintColor.rgb, 0.15 * sweep);
                    hdr = max(hdr, (half)(1.0 + _SweepHDR * sweep));
                }

                // 외곽선 위에서 가끔 반짝이는 별(+ 모양) 3개
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float hk = Hash21(originPx + k * 17.0);
                    float period = 2.2 + hk * 1.8;
                    float tk = frac(_Time.y / period + hk * 3.7);
                    if (tk > 0.16) continue;
                    float ang = (hk * 0.8 + k * 0.33) * 6.2831853;
                    float2 sp = floor(float2(cos(ang) * _RadiusX * 0.93, _CenterY + sin(ang) * _RadiusY * 0.93));
                    float2 dd = abs(ip - sp);
                    bool bCore = dd.x + dd.y < 0.5;
                    bool bArm = tk < 0.1 && ((dd.x < 0.5 && dd.y < 1.5) || (dd.y < 0.5 && dd.x < 1.5));
                    if (bCore || bArm)
                    {
                        // 별도 웅웅거림을 따라 거의 안 보일 때는 흐리게, 드러날 때만 또렷하게 반짝인다
                        half starVis = saturate(vis * 1.3h);
                        Over(col, _GlintColor.rgb, (bCore ? 0.9h : 0.6h) * starVis);
                        hdr = max(hdr, (half)(1.0 + (bCore ? 1.0 : 0.4) * starVis));
                    }
                }

                // ---------- 피격: 번쩍임 + 퍼지는 고리 + 드러나는 마름모 격자 ----------
                if (_HitAge < HitDuration)
                {
                    float age = max(0.0, _HitAge);
                    float flash = exp(-age * 9.0);
                    float fade = 1.0 - age / HitDuration;
                    float d = distance(p, _HitPoint.xy);
                    float ring = age * RippleSpeed;

                    // 껍질 전체가 한 번 번쩍인다
                    Over(col, _RimColor.rgb, 0.24 * flash);
                    if (bRim) { Over(col, _GlintColor.rgb, 0.9 * flash); hdr = max(hdr, (half)(1.0 + _HitHDR * flash)); }

                    // 맞은 자리를 비추는 부드러운 빛(가운데가 밝고 가장자리로 번지며 빠르게 사그라든다)
                    {
                        float glow = exp(-(d * d) / (2.0 * HitGlowRadius * HitGlowRadius)) * exp(-age * 6.0);
                        Over(col, _GlintColor.rgb, 0.4 * glow);
                        hdr = max(hdr, (half)(1.0 + _HitHDR * 0.45 * glow));
                    }

                    // 맞은 자리에서 시작한 빛 띠가 타격 방향(맞은 자리 → 껍질 중심)으로 표면을 훑고 반대편까지 지나간다.
                    // 띠는 타격 방향에 수직이고, 평상시 빛처럼 껍질 볼록함만큼 휜다.
                    {
                        float2 toCenter = float2(0.0, _CenterY) - _HitPoint.xy;
                        float2 dir = dot(toCenter, toCenter) > 0.01 ? normalize(toCenter) : float2(0.0, -1.0);
                        float far = dot(toCenter, dir) + length(float2(_RadiusX * dir.x, _RadiusY * dir.y)); // 맞은 자리에서 반대편 껍질 끝까지
                        float t = saturate(age / HitSweepTime);
                        float bandPos = (far + HitSweepWidth * 2.0) * (1.0 - (1.0 - t) * (1.0 - t)); // 처음엔 빠르게, 끝에서 느려지며
                        float bulge = sqrt(saturate(1.0 - e)) * 6.0;
                        float bandDist = (dot(p - _HitPoint.xy, dir) + bulge) - bandPos;
                        float sweep = exp(-(bandDist * bandDist) / (2.0 * HitSweepWidth * HitSweepWidth)) * (1.0 - t);
                        float fresnel = 0.55 + 0.45 * r;
                        Over(col, _GlintColor.rgb, 0.45 * sweep * fresnel);
                        if (bRim) Over(col, _GlintColor.rgb, 0.8 * sweep);
                        else if (IsLatticeLine(ip)) Over(col, _RimColor.rgb, 0.35 * sweep);
                        if (false == bRim && IsLatticeNode(ip)) Over(col, _GlintColor.rgb, 0.6 * sweep);
                        hdr = max(hdr, (half)(1.0 + _HitHDR * 0.55 * sweep));
                    }

                    // 맞은 자리에서 퍼지는 고리(1px 띠)
                    if (abs(d - ring) < 0.9) { Over(col, _GlintColor.rgb, 0.85 * fade); hdr = max(hdr, (half)(1.0 + _HitHDR * 0.6 * fade)); }

                    // 고리 안쪽으로 마름모 격자가 드러났다 사그라든다
                    if (d < ring && IsLatticeLine(ip))
                    {
                        float trail = saturate(1.0 - (ring - d) / 26.0);
                        Over(col, _RimColor.rgb, 0.55 * trail * fade + 0.15 * flash);
                        hdr = max(hdr, (half)(1.0 + _HitHDR * 0.35 * trail * fade));
                    }

                    // 맞은 점의 하얀 코어(처음 두세 프레임)
                    if (age < 0.07 && d < 2.6) { Over(col, _GlintColor.rgb, 1.0); hdr = max(hdr, (half)(1.0 + _HitHDR)); }
                }

                // ---------- 파괴: 금 + 조각이 떨어지기 직전의 번쩍임 ----------
                if (bBreaking)
                {
                    float whole = exp(-_BreakAge * 14.0);
                    Over(col, _GlintColor.rgb, 0.35 * whole);
                    if (bRim) { Over(col, _GlintColor.rgb, 0.9); hdr = max(hdr, (half)(1.0 + _HitHDR * max(whole, 0.4))); }
                    // 금이 가는 동안 막 전체가 살짝 차오른다(체크 무늬 없이 균일하게)
                    Over(col, _RimColor.rgb, 0.1 + 0.18 * whole);
                    if (crack > 0.5) { Over(col, _GlintColor.rgb, 0.95); hdr = max(hdr, (half)(1.0 + _HitHDR * 0.8)); }
                    // 떨어져 나가기 직전 조각: 면은 반쯤만 밝히고 테두리(격자선)만 하얗게 터뜨린다
                    if (popFlash > 0.5)
                    {
                        Over(col, _GlintColor.rgb, IsLatticeLine(ip) ? 0.95 : 0.45);
                        hdr = max(hdr, (half)(1.0 + _HitHDR * 0.7));
                    }
                }

                col.rgb *= hdr;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
