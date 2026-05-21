Shader "Custom/TreeOutlineShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 1
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

        // 1번 Pass: 나무 본체 렌더링 + 스텐실 16 기록
        Pass
        {
            Name "TreeBody"
            Stencil
            {
                Ref 16
                Comp Always
                Pass Replace
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
                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);
                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV) * _BaseColor;
                if (mainColor.a < 0.1) discard;

                return mainColor;
            }
            ENDHLSL
        }

        // 2번 Pass: 외곽선 렌더링 (스텐실 16이 없는 곳에만 그림) + 스텐실 16 기록
        Pass
        {
            Name "TreeOutline"
            Stencil
            {
                Ref 16
                Comp NotEqual // 이미 그려진 나무 본체나 외곽선 영역(16)은 피함
                Pass Replace  // 외곽선이 그려진 자리도 16으로 채워 다른 나무가 못 들어오게 함
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
                half4 _OutlineColor;
                float _OutlineWidth;
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
                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);
                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 외곽선 계산 로직
                float2 uvOffset_X = 0;
                float2 uvOffset_Y = 0;
                if (abs(det) > 1e-8)
                {
                    float one_over_ppu = 1.0 / ppu;
                    uvOffset_X = (one_over_ppu * (dy_wp.y * dx_uv - dy_wp.x * dy_uv)) / det;
                    uvOffset_Y = (one_over_ppu * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                }

                float2 finalOffset_X = uvOffset_X * _OutlineWidth;
                float2 finalOffset_Y = uvOffset_Y * _OutlineWidth;

                half alphaUp = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y).a;
                half alphaDown = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y).a;
                half alphaLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_X).a;
                half alphaRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_X).a;
                
                half outlineAlpha = max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight));

                // 본체 내부가 아니고 주변에 알파가 있다면 외곽선 색상 반환
                if (outlineAlpha > 0.1)
                {
                    return _OutlineColor;
                }

                discard;
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
