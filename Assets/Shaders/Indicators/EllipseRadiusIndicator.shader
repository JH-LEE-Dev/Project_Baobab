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
                half4 finalColor = half4(finalRgb, alpha);
                clip(finalColor.a - 0.005);
                return finalColor;
            }
            ENDHLSL
        }
    }

}
