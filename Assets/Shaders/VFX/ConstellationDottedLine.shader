Shader "Custom/VFX/ConstellationDottedLine"
{
    Properties
    {
        [Header(Color Settings)]
        [HDR] _BaseColor ("Base Color (HDR)", Color) = (0.2, 0.7, 1.0, 1.0)
        [HDR] _ShimmerColor ("Shimmer Color (White/Bright)", Color) = (1.0, 1.0, 1.0, 1.0)
        _EmissionIntensity ("HDR Emission Intensity", Float) = 2.0

        [Header(Animation Settings)]
        _PulseSpeed ("Global Pulse Speed", Float) = 2.5
        _TwinkleSpeed ("Individual Twinkle Speed", Float) = 6.0
        _ColorShiftSpeed ("Color Shift Speed", Float) = 4.0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5.0 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1.0 // One (Additive) 또는 10 (OneMinusSrcAlpha)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0 // Off
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

        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "ConstellationDottedLinePass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR; // r: Twinkle Phase, g: Color Shift Phase, b: Sub-Noise, a: Base Alpha
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShimmerColor;
                float _EmissionIntensity;
                float _PulseSpeed;
                float _TwinkleSpeed;
                float _ColorShiftSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // input.color.g: 도트별 색상 변동 고유 위상 (0.0 ~ 1.0)
                // input.color.r: 도트별 트윙클 고유 위상 (0.0 ~ 1.0)
                // input.color.a: 도트 기본 알파

                // 1. 색상 불규칙 변동 (BaseColor ↔ ShimmerColor/White 왕복)
                // 두 개의 주파수가 다른 사인파를 합성하여 도트마다 완전히 불규칙하게 반짝이도록 처리
                float phaseG = input.color.g * 6.2831853f; // 2 * PI
                float phaseR = input.color.r * 12.566370f; // 4 * PI
                float waveA = sin(_Time.y * _ColorShiftSpeed + phaseG);
                float waveB = sin(_Time.y * (_ColorShiftSpeed * 1.618f) + phaseR);
                half shiftFactor = (half)saturate(waveA * 0.5f + waveB * 0.35f + 0.5f);

                half3 blendedColor = lerp(_BaseColor.rgb, _ShimmerColor.rgb, shiftFactor);

                // 2. 도트별 개별 트윙클 (Twinkle) & 전체 점선 숨쉬기 펄스 (Pulse)
                float twinkleWave = sin(_Time.y * _TwinkleSpeed + input.color.r * 6.2831853f);
                half twinkle = (half)(0.55f + 0.45f * twinkleWave);

                float pulseWave = sin(_Time.y * _PulseSpeed);
                half pulse = (half)(0.75f + 0.25f * pulseWave);

                half brightness = twinkle * pulse;

                // 3. 최종 색상 (HDR Emission 포함) 및 알파 계산
                half3 finalRgb = blendedColor * brightness * (half)_EmissionIntensity;
                half finalAlpha = input.color.a * _BaseColor.a * (half)(0.6f + 0.4f * brightness);

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
