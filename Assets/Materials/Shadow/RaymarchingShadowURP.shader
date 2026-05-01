Shader "Custom/RaymarchingShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map (Caster Texture)", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        _XYAngle("Shadow Direction (Radians)", Range(0, 6.2831)) = 0
        _ZAngle("Light Altitude (Radians)", Range(0.1, 1.5)) = 0.5
        _MaxDistance("Max Shadow Distance", Range(0, 5)) = 1.0
        _Expansion("Mesh Expansion", Range(1, 10)) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half4 _ShadowColor;
                float _XYAngle;
                float _ZAngle;
                float _MaxDistance;
                float _Expansion;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 메시 자체의 크기를 중앙 기준으로 확장합니다.
                float3 pos = IN.positionOS.xyz;
                pos.xy *= _Expansion;
                OUT.positionHCS = TransformObjectToHClip(pos);
                
                // 확장된 메시 전체에 대한 0~1 UV 좌표를 프래그먼트로 넘깁니다.
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 현재 픽셀(IN.uv)을 원본 스프라이트의 좌표계(0~1)로 변환합니다.
                // 0.5를 기준으로 Expansion만큼 다시 축소하여 중앙에 배치하는 논리입니다.
                float2 spriteUV = (IN.uv - 0.5) * _Expansion + 0.5;
                
                float2 shadowDir = float2(cos(_XYAngle), sin(_XYAngle));
                float slope = tan(_ZAngle);
                
                bool isVisible = false;

                // 레이마칭: 현재 위치에서 광원 방향으로 역추적하여 가림막(Occluder)이 있는지 확인합니다.
                for (int i = 1; i < 100; i++)
                {
                    // dist는 월드 단위가 아닌 '확장된 UV 단위'의 거리입니다.
                    float dist = (float(i) / 100.0) * _MaxDistance;
                    
                    // 현재 지점(spriteUV)에서 광원 방향(그림자 반대 방향)으로 샘플링 지점을 이동합니다.
                    float2 sampleUV = spriteUV - shadowDir * dist;

                    // 샘플링하려는 위치가 원본 스프라이트 텍스처 영역 내부에 있을 때만 체크합니다.
                    if (sampleUV.x >= 0.0 && sampleUV.x <= 1.0 && sampleUV.y >= 0.0 && sampleUV.y <= 1.0)
                    {
                        half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV);
                        
                        // 알파값이 있는 픽셀(Occluder)을 발견하면 높이를 비교합니다.
                        if (sampled.a > 0.1)
                        {
                            // UV.y를 높이로 활용 (상단 1, 하단 0)
                            float occluderHeight = sampleUV.y; 
                            float rayHeight = dist * slope;

                            if (occluderHeight > rayHeight)
                            {
                                isVisible = true;
                                break;
                            }
                        }
                    }
                }

                if (isVisible)
                {
                    return _ShadowColor;
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
