Shader "Custom/IsometricProjectionShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        
        [Header(Projection Settings)]
        _ShadowAngle("Shadow Angle (Deg)", Float) = 225
        _ShadowLength("Shadow Length", Float) = 1.0
        _ShadowVerticalSquash("Isometric Squash", Range(0, 1)) = 0.5
        
        [Header(Pivot Settings)]
        _VerticalOffset("Foot Position (Local Y)", Float) = 0.0
        
        [Header(Status)]
        _AlphaMultiplier("Alpha Multiplier", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _ShadowAngle;
                float _ShadowLength;
                float _ShadowVerticalSquash;
                float _VerticalOffset;
                float _AlphaMultiplier;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 1. 로컬 좌표 가져오기
                float3 pos = IN.positionOS.xyz;
                
                // 2. 바닥(피벗) 위치를 기준으로 한 상대적 높이 계산
                float height = pos.y - _VerticalOffset;

                // 3. 그림자 방향 계산 (라디안)
                // 유니티 2D 회전과 맞추기 위해 90도를 더하거나 조정이 필요할 수 있음
                float angleRad = (_ShadowAngle + 90.0f) * (PI / 180.0f);
                float2 shadowDir = float2(cos(angleRad), sin(angleRad));

                // 4. 투영 공식 (Isometric Projection)
                // 높이가 있는 정점들만 shadowDir 방향으로 눕힘
                float projectedX = pos.x + (height * shadowDir.x * _ShadowLength);
                float projectedY = _VerticalOffset + (height * shadowDir.y * shadowDir.y < 0 ? shadowDir.y : shadowDir.y * _ShadowVerticalSquash);
                
                // 위 공식 대신 기존에 있던 공식을 좀 더 정제하여 사용
                pos.x = pos.x + (height * shadowDir.x * _ShadowLength);
                pos.y = _VerticalOffset + (height * shadowDir.y * _ShadowLength * _ShadowVerticalSquash);
                pos.z = 0; // 그림자는 평면에 그려지도록 처리

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.worldPos = TransformObjectToWorld(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                
                // 기본 컬러와 그림자 컬러 결합
                OUT.color = IN.color * _BaseColor;
                OUT.color.a *= _AlphaMultiplier;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 안정화된 월드 공간 32 PPU 픽셀 스냅 로직 (IsometricShadowURP 참고) ---
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 1. 경계면 진동 방지를 위해 아주 작은 offset(0.001)을 더해 스냅 안정화
                float2 snappedWorldPos = floor(worldPos * ppu + 0.001) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                // 2. UV 변화량(ddx, ddy)과 월드 좌표 변화량 사이의 관계를 행렬로 계산
                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 0.0000001)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) + 
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 3. 보정된 UV로 샘플링
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV);
                
                // 투명도 컷오프
                clip(texColor.a - 0.1);

                return half4(IN.color.rgb, texColor.a * IN.color.a);
            }
            ENDHLSL
        }
    }
}
