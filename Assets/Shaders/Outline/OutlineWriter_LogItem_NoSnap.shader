// OutlineWriter_LogItem에서 출렁임(정점 sin 오프셋)과 월드 픽셀 스냅을 뺀 버전.
// 제재소 원목(LogItem_ToCut)용으로, OutlineShader_LogItem_NoSnap과 짝으로 쓴다 - 둘의 테두리 계산이 같아야 한다.
Shader "Custom/OutlineWriter_LogItem_NoSnap"
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

        Stencil
        {
            Ref 32
            Comp Always
            Pass Replace
        }

        Pass
        {
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _OutlineColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _OutlineWidth)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // 출렁임 없음: 본체(NoSnap)와 같은 자리에 그린다
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv; // 2D SRP Batcher 호환을 위해 TRANSFORM_TEX 제거
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 스냅 없음: 본체(NoSnap)와 같은 UV를 그대로 쓴다
                float2 uv = IN.uv;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(uv);
                float2 dy_uv = ddy(uv);

                // 짝인 OutlineShader_LogItem_NoSnap과 같은 식이어야 테두리 영역이 일치한다
                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;

                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

                // 아웃라인 로직 (PPU 단위의 인접 픽셀 샘플링)
                float2 uvOffset_X = 0;
                float2 uvOffset_Y = 0;

                if (abs(det) > 1e-8)
                {
                    float one_over_ppu = 1.0 / ppu;
                    // 월드 공간에서 1픽셀(1/ppu) 이동에 해당하는 UV 오프셋 계산
                    uvOffset_X = (one_over_ppu * (dy_wp.y * dx_uv - dy_wp.x * dy_uv)) / det;
                    uvOffset_Y = (one_over_ppu * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                }

                float outlineWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineWidth);
                float2 finalOffset_X = uvOffset_X * outlineWidth;
                float2 finalOffset_Y = uvOffset_Y * outlineWidth;

                half alphaUp = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + finalOffset_Y).a;
                half alphaDown = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - finalOffset_Y).a;
                half alphaLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - finalOffset_X).a;
                half alphaRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + finalOffset_X).a;
                half alphaUpLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + finalOffset_Y - finalOffset_X).a;
                half alphaUpRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + finalOffset_Y + finalOffset_X).a;
                half alphaDownLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - finalOffset_Y - finalOffset_X).a;
                half alphaDownRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - finalOffset_Y + finalOffset_X).a;

                half outlineAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)),
                                        max(max(alphaUpLeft, alphaUpRight), max(alphaDownLeft, alphaDownRight)));

                if (mainColor.a < 0.1 && outlineAlpha > 0.1)
                {
                    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineColor);
                }

                discard;
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
