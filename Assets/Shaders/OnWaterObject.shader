Shader "Custom/OnWaterObject"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _WaveSpeed("Wave Speed", Float) = 2.0
        _WaveStrength("Wave Strength", Float) = 0.01
        _WaveFreq("Wave Frequency", Float) = 15.0
        _DistortionAmount("Distortion Amount", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Equal
                Pass Zero
            }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1; // 월드 좌표 추가
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveFreq;
                float _DistortionAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                
                // 일렁임 계산을 위해 월드 XY 좌표 전달
                float3 wPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldPos = wPos.xy;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 월드 좌표를 기준으로 일렁임 계산 (애니메이션 프레임 변화에 영향받지 않음)
                float2 waveCoord = IN.worldPos;
                
                float2 distortion;
                distortion.x = sin(_Time.y * _WaveSpeed + waveCoord.y * _WaveFreq) * _WaveStrength;
                distortion.y = cos(_Time.y * _WaveSpeed * 0.7 + waveCoord.x * _WaveFreq * 0.8) * _WaveStrength;

                // 최종 샘플링 UV에 왜곡 적용 (DistortionAmount로 전체 강도 조절)
                float2 finalUV = IN.uv + (distortion * _DistortionAmount);

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, finalUV) * _BaseColor;
                
                if (color.a < 0.01) discard;

                return color;
            }
            ENDHLSL
        }
    }
}
