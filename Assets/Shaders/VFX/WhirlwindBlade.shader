Shader "Custom/VFX/WhirlwindBlade"
{
    // 회전베기 초승달 칼날 두 개 + 잔상 칼날. WhirlwindVFX가 칼날을 덮는 사각형 쿼드 하나를 그리고, 픽셀(32 PPU 격자)마다
    // 칼날 안인지와 색을 여기서 정한다. 판정식은 예전 CPU 픽셀 스캔(WhirlwindVFX.SampleBlade)과 같고, 매 프레임 값은
    // MaterialPropertyBlock으로 받는다. 출력은 BrandWrapGlow와 같다(색 * lerp(_GlowMin, _GlowMax, 발광 세기), 알파 1).
    Properties
    {
        _GlowMin ("Glow Min (HDR 발광 최소 배율, 발광 세기 0)", Float) = 1.0
        _GlowMax ("Glow Max (HDR 발광 최대 배율, 발광 세기 1)", Float) = 2.4
        _ColorWhite ("White (rgb, a = 발광 세기)", Vector) = (1, 1, 1, 1)
        _ColorLight ("Light", Vector) = (1, 1, 1, 0)
        _ColorMid ("Mid", Vector) = (1, 1, 1, 0)
        _ColorDark ("Dark", Vector) = (1, 1, 1, 0)
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
            Name "WhirlwindBladePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _GlowMin;
                float _GlowMax;
                float4 _ColorWhite;
                float4 _ColorLight;
                float4 _ColorMid;
                float4 _ColorDark;

                // x, y = 중심 픽셀(월드 px), z = 판정 반지름(px), w = 픽셀/유닛
                float4 _BladeCenter;
                // x = 바깥 반지름^2, y = 안쪽 반지름^2, z = 1px 테두리(반지름 비율), w = 최소 두께(반지름 비율)
                float4 _BladeBand;
                // x = 머리 각도, y = 길이(도), z = 두께, w = 밝기
                float4 _BladeMain;
                // x = 잔상 머리 각도, y = 잔상 길이(도), z = 잔상 켜짐(0/1), w = 식는 정도(energy)
                float4 _BladeEcho;
                // x = 팝 배율, y = 바깥 가장자리 반지름, z = 꼬리 말림, w = 잔상 반지름 비율
                float4 _BladeShape;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                return output;
            }

            // Mathf.Repeat
            float RepeatDegrees(float _t, float _length)
            {
                return clamp(_t - floor(_t / _length) * _length, 0.0, _length);
            }

            // WhirlwindVFX.SampleBlade
            bool SampleBlade(float _behind, float _span, float _rho, float _outerRho, float _thickness, float _scale, float _minWidth,
                out float _s, out float _across, out float _rhoOuter)
            {
                _s = 0.0;
                _across = 0.0;
                _rhoOuter = 0.0;
                if (_behind > _span) return false;

                _s = 1.0 - _behind / _span;
                float curl = 1.0 - _s;
                _rhoOuter = (_outerRho - _BladeShape.z * curl * curl) * _scale;
                if (_rho > _rhoOuter) return false;

                float shape = _s < 0.8
                    ? pow(max(_s, 0.0) / 0.8, 1.2) // _s는 0 이상(뒤로 잰 각도 <= 길이)
                    : sqrt(max(0.0, 1.0 - (_s - 0.8) * (_s - 0.8) / 0.04));

                float rhoInner = _rhoOuter - _thickness * shape * _scale;
                if (_rho < rhoInner || _rhoOuter - rhoInner < _minWidth) return false;

                _across = (_rho - rhoInner) / max(0.0001, _rhoOuter - rhoInner);
                return true;
            }

            half4 Output(float4 _color)
            {
                float glow = lerp(_GlowMin, _GlowMax, saturate(_color.a));
                return half4((half3)(_color.rgb * glow), 1.0h);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 이 조각이 속한 월드 픽셀(정수)과 중심에서의 칸 수
                float2 pixel = floor(input.positionWS * _BladeCenter.w);
                int ix = (int)(pixel.x - _BladeCenter.x);
                int iy = (int)(pixel.y - _BladeCenter.y);

                float invRadius = 1.0 / _BladeCenter.z;
                float fy = iy * 2.0 * invRadius;
                float fx = ix * invRadius;
                float rhoSq = fx * fx + fy * fy;
                if (rhoSq > _BladeBand.x || rhoSq < _BladeBand.y) discard;

                float phi = degrees(atan2(fy, fx));
                float rho = sqrt(rhoSq);
                bool bChecker = 0 == ((ix + iy) & 1);

                float head = _BladeMain.x;
                float span = _BladeMain.y;
                float thickness = _BladeMain.z;
                float brightness = _BladeMain.w;
                float energy = _BladeEcho.w;
                float scale = _BladeShape.x;
                float rimRho = _BladeBand.z;
                float minWidth = _BladeBand.w;

                float s, across, rhoOuter;
                float behind = RepeatDegrees(head - phi, 180.0);
                if (SampleBlade(behind, span, rho, _BladeShape.y, thickness, scale, minWidth, s, across, rhoOuter))
                {
                    float b = (0.58 * s + 0.42 * across) * brightness;
                    bool bRim = rhoOuter - rho < rimRho;

                    float4 color;
                    if ((bRim && s > 0.55 && energy > 0.5) || b > 0.9) color = _ColorWhite;
                    else if (b > 0.66) color = _ColorLight;
                    else if (b > 0.42) color = _ColorMid;
                    else if (b > 0.2) color = _ColorDark;
                    else if (bChecker) color = _ColorDark;
                    else discard;

                    if ((s < 0.14 || energy < 0.45) && false == bChecker) discard;

                    return Output(color);
                }

                if (_BladeEcho.z < 0.5) discard;

                float es, eAcross, eRhoOuter;
                float echoBehind = RepeatDegrees(_BladeEcho.x - phi, 180.0);
                if (SampleBlade(echoBehind, _BladeEcho.y, rho, _BladeShape.y * _BladeShape.w, thickness * 0.8, scale, minWidth, es, eAcross, eRhoOuter))
                {
                    if (false == bChecker && es < 0.75) discard;
                    return Output(es * (0.5 + 0.5 * eAcross) * energy > 0.5 ? _ColorMid : _ColorDark);
                }

                discard;
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
