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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 1. [수정] 경계선이 아닌 픽셀의 '중앙'으로 스냅 (+0.5)
                // floor + 0.001은 미세한 이동에서 값이 변하지 않는 '데드존'을 만들어 멈춤 현상을 유발합니다.
                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                // 2. [수정] 결정자(det) 체크 범위를 약간 더 여유있게 잡거나 
                // 정밀도가 낮은 환경을 위해 1e-6 정도로 조정합니다.
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 3. 샘플링 시 필터 모드에 따른 지터를 방지하기 위해 
                // 가능하면 SAMPLE_TEXTURE2D_LOD를 사용하여 밉맵 0레벨을 강제하는 것이 좋습니다.
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV);

                clip(texColor.a - 0.1);

                return half4(_BaseColor.rgb, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
