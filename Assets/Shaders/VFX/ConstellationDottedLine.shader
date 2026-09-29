Shader "Custom/VFX/ConstellationDottedLine"
{
    Properties
    {
        [Header(Color Settings)]
        [HDR] _BaseColor ("Base Color (HDR)", Color) = (1.0, 0.78, 0.2, 1.0)
        [HDR] _ShimmerColor ("Shimmer Color (White/Bright)", Color) = (1.0, 0.98, 0.85, 1.0)
        _EmissionIntensity ("HDR Emission Intensity", Float) = 4.0

        [Header(Edge Fade Settings)]
        _EdgeFadeLength ("Edge Fade Length", Range(0.01, 0.45)) = 0.18

        [Header(Animation Settings)]
        _PulseSpeed ("Global Pulse Speed", Float) = 2.5
        _TwinkleSpeed ("Individual Twinkle Speed", Float) = 6.0
        _ColorShiftSpeed ("Color Shift Speed", Float) = 4.0

        [Header(Traveling Flow Pulse)]
        [Toggle] _EnablePingPong ("Ping-Pong Wave Motion", Float) = 1.0
        _FlowSpeed ("Flow Pulse Speed", Float) = 1.2
        _FlowWidth ("Flow Pulse Width", Float) = 0.14
        _FlowIntensity ("Flow HDR Boost", Float) = 0.6

        [Header(Pixel Wave Undulation)]
        [Toggle] _EnablePixelWave ("Enable Pixel Wave Undulation", Float) = 1.0
        _WaveSpeed ("Wave Undulation Speed", Float) = 3.0
        _WaveFrequency ("Wave Frequency (Cycles)", Float) = 2.0
        _WaveAmplitudePixels ("Wave Amplitude in Pixels", Float) = 2.0

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
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0; // uv.x: Global Progress (0~1), uv.y: Segment Progress (0~1)
                float4 color        : COLOR;     // r, g, b: Node-Interpolated Base Color, a: Base Alpha
                float2 uv1          : TEXCOORD1; // x: Twinkle Phase, y: Color Shift Phase
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float2 uv1          : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShimmerColor;
                float _EmissionIntensity;
                float _EdgeFadeLength;
                float _PulseSpeed;
                float _TwinkleSpeed;
                float _ColorShiftSpeed;
                float _EnablePingPong;
                float _FlowSpeed;
                float _FlowWidth;
                float _FlowIntensity;
                float _EnablePixelWave;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitudePixels;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 localPos = input.positionOS.xyz;

                // 픽셀 단위 계단식 물결 변위 (100% GPU 버텍스 무할당 연산)
                // 1. 각 세그먼트(지점과 지점 사이) 양끝 결착 앵커 (노드에 다다를수록 진폭이 0으로 수렴)
                float anchor = smoothstep(0.0f, 0.2f, input.uv.y) * smoothstep(1.0f, 0.8f, input.uv.y);

                // 2. 전체 선 진행도(uv.x) 및 시간에 따른 연속 사인 파동
                float wave = sin(_Time.y * _WaveSpeed + input.uv.x * _WaveFrequency * 6.2831853f);

                // 3. 정확한 1픽셀(1/32) 단위 계단식 양자화 (Quantization Snap)
                float stepCount = round(wave * _WaveAmplitudePixels * anchor);
                float pixelDisplacement = stepCount * (1.0f / 32.0f);

                // 4. 선의 법선(수직) 방향으로 도트 단위 이동 (형상 찌그러짐 0%)
                localPos += input.normalOS.xyz * (pixelDisplacement * _EnablePixelWave);

                output.positionHCS = TransformObjectToHClip(localPos);
                output.uv = input.uv;
                output.color = input.color;
                output.uv1 = input.uv1;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // input.color.rgb: 노드간(황금빛 나무 ↔ 푸른색 큰 별) 부드럽게 보간된 동적 그라데이션 베이스 색상
                // input.color.a: 도트 기본 알파
                // input.uv.x: 전체 별자리 경로 진행도 (0.0 ~ 1.0, 펄스 파동 유영용)
                // input.uv.y: 현재 세그먼트(지점과 지점 사이) 국소 진행도 (0.0 ~ 1.0, 알파 페이드용)
                // input.uv1.x: 개별 도트 트윙클 고유 위상
                // input.uv1.y: 개별 도트 색상 시프트 고유 위상

                // 1. 색상 불규칙 변동 (BaseColor ↔ Shimmer/White 왕복 미세 반짝임)
                float phaseG = input.uv1.y * 6.2831853f; // 2 * PI
                float phaseR = input.uv1.x * 12.566370f; // 4 * PI
                float waveA = sin(_Time.y * _ColorShiftSpeed + phaseG);
                float waveB = sin(_Time.y * (_ColorShiftSpeed * 1.618f) + phaseR);
                half shiftFactor = (half)saturate(waveA * 0.5f + waveB * 0.35f + 0.5f);
                half3 shimmer = _ShimmerColor.rgb * (shiftFactor * 0.12f);
                half3 blendedColor = input.color.rgb + shimmer;

                // 2. 도트별 개별 트윙클 (Twinkle) & 전체 점선 숨쉬기 펄스 (Pulse)
                float twinkleWave = sin(_Time.y * _TwinkleSpeed + input.uv1.x * 6.2831853f);
                half twinkle = (half)(0.75f + 0.25f * twinkleWave);

                float pulseWave = sin(_Time.y * _PulseSpeed);
                half pulse = (half)(0.85f + 0.15f * pulseWave);

                half brightness = twinkle * pulse;

                // 3. 선 전체를 타고 흐르는 동적 펄스 파동 (Ping-Pong 왕복 또는 단방향 유영)
                float waveHead = (_EnablePingPong > 0.5f)
                    ? (0.5f + 0.5f * sin(_Time.y * _FlowSpeed))
                    : frac(_Time.y * _FlowSpeed);

                float distToWave = abs(input.uv.x - waveHead);
                float flowWave = saturate(1.0f - (distToWave / _FlowWidth));
                flowWave = flowWave * flowWave; // 날카롭고 강렬한 발광 피크
                half flowBoost = (half)(1.0f + flowWave * _FlowIntensity);

                // 4. 지점과 지점 사이(세그먼트) 양끝 소프트 페이드아웃 (나무 링 / 큰 별 근처 띡 박히는 느낌 제거)
                float distFromSegEdge = min(input.uv.y, 1.0f - input.uv.y);
                half segEdgeFade = (half)smoothstep(0.0f, _EdgeFadeLength, distFromSegEdge);

                // 5. 최종 색상 (HDR Emission + Flow Boost + Segment Edge Fade 포함) 및 알파 계산
                half3 finalRgb = blendedColor * brightness * flowBoost * (half)_EmissionIntensity * segEdgeFade;
                half finalAlpha = input.color.a * (half)saturate(0.5f + 0.5f * (brightness * flowBoost)) * segEdgeFade;

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
