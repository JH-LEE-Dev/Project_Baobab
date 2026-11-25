Shader "Custom/EllipseRadiusIndicator"
{
     Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        _EllipseRadius("Ellipse Radius (Units)", Float) = 1.25
        _AttackDir("Attack Direction (XY)", Vector) = (1, 0, 0, 0)
        _CosThreshold("Cos Threshold", Float) = 0.63

        [Header(Thickness)]
        _LineThickness("Outer Line Thickness", Float) = 0.026
        _ArrowBaseWidth("Arrow Base Width", Float) = 0.15
        _ArrowLength("Arrow Length", Float) = 0.2
        _ArrowBaseInset("Arrow Base Inset", Float) = 0.015
        _EdgeSoftness("Edge Softness", Float) = 0.012

        [Header(Alpha)]
        _IndicatorAlpha("Indicator Alpha", Range(0, 1)) = 1

        [Header(Fill)]
        _FillAlpha("Fill Alpha", Range(0, 1)) = 0.3
        _FillStart("Fill Start", Range(0, 1)) = 0.5
        _AngleFade("Angle Fade Range", Float) = 0.1

        [Header(HDR)]
        _ArrowHDRIntensity("Arrow HDR Intensity", Float) = 2.35

        // 휘두르기 튕김: AttackComponent가 스윙마다 진행도(0~1)를 넣는다. 음수면 튕김 없음(평소 그림 그대로).
        [Header(Swing Punch)]
        _SwingPunch("Swing Punch Progress", Float) = -1
        _SwingPunchScale("Punch Scale", Float) = 0.15
        _SwingPunchBrighten("Punch Brighten", Float) = 1.2
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
                float3 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EllipseRadius;
                float4 _AttackDir;
                float _CosThreshold;
                float _LineThickness;
                float _ArrowBaseWidth;
                float _ArrowLength;
                float _ArrowBaseInset;
                float _EdgeSoftness;
                float _IndicatorAlpha;
                float _FillAlpha;
                float _FillStart;
                float _AngleFade;
                float _ArrowHDRIntensity;
                float _SwingPunch;
                float _SwingPunchScale;
                float _SwingPunchBrighten;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleY = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));

                float2 posUnits = IN.positionOS.xy * float2(scaleX, scaleY);
                float2 posIso = float2(posUnits.x, posUnits.y * 2.0);

                // 휘두르기 튕김: 휘두르는 순간 표시기 전체(외곽선·화살표·바탕)가 바깥으로 튕겼다가(진행도 0.25에 최대) 제자리로 돌아오며,
                // 최대일 때 진하고 하얘진다. 좌표를 줄여 읽는 방식이라 모양은 그대로 커진다. 진행도가 음수면 평소 그림과 같다.
                float punchCurve = 0.0;
                if (_SwingPunch >= 0.0)
                {
                    float swingT = saturate(_SwingPunch);
                    punchCurve = swingT < 0.25 ? sin(swingT / 0.25 * 1.5707963) : pow(1.0 - (swingT - 0.25) / 0.75, 2.0);
                    posIso /= 1.0 + _SwingPunchScale * punchCurve;
                }

                float dist = length(posIso);

                float2 dir = dist > 0.0001 ? posIso / dist : float2(1.0, 0.0);
                float2 attackDir = normalize(_AttackDir.xy);
                attackDir = dot(attackDir, attackDir) > 0.0001 ? attackDir : float2(1.0, 0.0);

                float radius = max(_EllipseRadius, 0.0001);
                float cosThreshold = clamp(_CosThreshold, -0.999, 0.999);
                float sectorMask = smoothstep(cosThreshold - _AngleFade, cosThreshold, dot(dir, attackDir));

                float outerLine = 1.0 - smoothstep(
                    _LineThickness,
                    _LineThickness + _EdgeSoftness,
                    abs(dist - radius)
                );
                outerLine *= sectorMask;

                float2 sideDir = float2(-attackDir.y, attackDir.x);
                float along = dot(posIso, attackDir);
                float side = dot(posIso, sideDir);

                float arrowBase = radius - _ArrowBaseInset;
                float arrowTip = radius + _ArrowLength;
                float arrowSpan = max(arrowTip - arrowBase, 0.0001);
                float arrowT = saturate((along - arrowBase) / arrowSpan);
                float arrowHalfWidth = lerp(_ArrowBaseWidth, 0.0, arrowT);

                float arrowLengthMask = smoothstep(arrowBase, arrowBase + _EdgeSoftness, along)
                    * (1.0 - smoothstep(arrowTip - _EdgeSoftness, arrowTip, along));
                float arrowWidthMask = 1.0 - smoothstep(arrowHalfWidth, arrowHalfWidth + _EdgeSoftness, abs(side));
                float arrowForwardMask = smoothstep(0.985, 0.998, dot(dir, attackDir));
                float arrowMask = saturate(arrowLengthMask * arrowWidthMask * arrowForwardMask);

                float shapeMask = max(outerLine, arrowMask);
                float normalizedDist = saturate(dist / radius);
                float insideRange = 1.0 - smoothstep(radius, radius + _EdgeSoftness, dist);
                float fillMask = smoothstep(_FillStart, 1.0, normalizedDist) * sectorMask * insideRange;
                float fillAlpha = fillMask * _FillAlpha;

                float alpha = max(shapeMask * _IndicatorAlpha, fillAlpha) * _BaseColor.a;

                float arrowHdrMask = saturate(arrowMask);
                half3 finalRgb = _BaseColor.rgb * lerp(1.0, _ArrowHDRIntensity, arrowHdrMask);

                // 튕김 중에만: 외곽선·화살표·바탕이 함께 진해지고 흰색 쪽으로 간다(화살표 HDR은 유지, 나머지는 1을 넘기지 않아 블룸이 생기지 않는다).
                // 평소(punchCurve = 0)에는 이 계산을 건너뛰어 예전 그림과 같다.
                if (punchCurve > 0.0)
                {
                    alpha = saturate(max(shapeMask * _IndicatorAlpha, fillAlpha) * (1.0 + _SwingPunchBrighten * punchCurve)) * _BaseColor.a;
                    finalRgb = lerp(finalRgb, max(finalRgb, half3(1.0, 1.0, 1.0)), punchCurve * 0.6);
                }
                half4 finalColor = half4(finalRgb, alpha);
                clip(finalColor.a - 0.005);
                return finalColor;
            }
            ENDHLSL
        }
    }

}
