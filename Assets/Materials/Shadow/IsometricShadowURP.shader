Shader "Custom/IsometricShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref 0
                Comp Equal
                ReadMask 7
                WriteMask 7
                Pass IncrSat
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { 
                float4 positionHCS : SV_POSITION; 
                float2 uv : TEXCOORD0; 
                float3 worldPos : TEXCOORD1; 
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 안정화된 월드 공간 32 PPU 픽셀 스냅 로직 ---
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 1. 경계면 진동 방지를 위해 아주 작은 offset(0.001)을 더해 스냅 안정화
                float2 snappedWorldPos = floor(worldPos * ppu + 0.001) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                // 2. UV 변화량(ddx, ddy)과 월드 좌표 변화량 사이의 관계를 행렬로 계산
                // ddx/ddy는 픽셀 쿼드(2x2) 단위로 계산되므로, 고해상도에서는 매우 작은 값을 가집니다.
                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                // 행렬의 결정자(Determinant)를 계산하여 안정성 체크
                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 0.0000001)
                {
                    // 월드 좌표 변화량(worldDelta)에 대응하는 정확한 UV 변화량을 역행렬로 산출
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) + 
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 3. 보정된 UV로 샘플링
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV);
                
                clip(texColor.a - 0.1);

                return half4(_BaseColor.rgb, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
