Shader "Custom/EllipseRadiusIndicator"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 0, 0, 1)
        _EllipseRadius("Ellipse Radius (Units)", Float) = 1.5
        _AttackDir("Attack Direction (XY)", Vector) = (1, 0, 0, 0)
        _CosThreshold("Cos Threshold", Float) = 0.707
        
        [Header(Softness Settings)]
        _DistFade("Distance Fade Range", Float) = 0.3
        _AngleFade("Angle Fade Range", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionOS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EllipseRadius;
                float4 _AttackDir;
                float _CosThreshold;
                float _DistFade;
                float _AngleFade;
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
                
                // 1. 아이소매트릭 거리 계산
                float dist = sqrt(posUnits.x * posUnits.x + (posUnits.y * 2.0) * (posUnits.y * 2.0));
                
                // 2. 각도 기반 알파 감쇠 (부드러운 양옆 절단)
                float2 worldDir = normalize(float2(posUnits.x, posUnits.y * 2.0));
                float dotProduct = dot(worldDir, _AttackDir.xy);
                float angleAlpha = smoothstep(_CosThreshold - _AngleFade, _CosThreshold, dotProduct);
                
                // 3. 거리 기반 알파 감쇠 (범위 끝에서 부드럽게 사라짐)
                // _EllipseRadius 지점에서 알파가 0이 되도록 설정
                float distAlpha = 1.0 - smoothstep(_EllipseRadius - _DistFade, _EllipseRadius, dist);
                
                // 4. 최종 결합
                half4 color = _BaseColor;
                
                // 외곽선(Line) 없이 면 채우기 농도만 조절 (0.3 베이스)
                // 각도 감쇠와 거리 감쇠를 곱해 자연스럽게 사라지도록 함
                float finalAlpha = 0.3 * angleAlpha * distAlpha;
                color.a *= finalAlpha;

                clip(color.a - 0.005);
                return color;
            }
            ENDHLSL
        }
    }
}
