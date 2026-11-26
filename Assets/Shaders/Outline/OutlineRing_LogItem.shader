Shader "Custom/OutlineRing_LogItem"
{
    // 원목 외곽선을 스텐실 없이 한 번에 그린다. 원목 하나에 렌더러 셋(스텐실 쓰기, 몸통, 외곽선)을 쓰던 것을 둘로 줄이려고 만들었다.
    // 예전: OutlineWriter_LogItem(스텐실 32 쓰기) -> 몸통(스텐실 지움) -> OutlineShader_LogItem(스텐실 32인 곳에 외곽선)
    // 지금: 이 셰이더가 작성기 자리(몸통보다 정렬 순서 한 칸 앞)에서 외곽선 색을 바로 칠하고, 몸통이 그 뒤에 그려진다.
    //       외곽선 고리와 자기 몸통은 겹치지 않으므로 순서가 바뀌어도 같은 그림이 된다.
    // 칠하는 자리는 예전에 외곽선이 실제로 칠해지던 자리와 같게 맞춘다:
    //   작성기가 스텐실을 찍던 자리(작성기의 행렬식 식 그대로) 그리고 외곽선 셰이더가 고리로 판정하던 자리,
    //   그중 몸통이 그려지는 자리(알파 0.01 이상 - 예전에는 몸통이 스텐실을 지웠다)는 뺀다.
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR] _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
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

        Pass
        {
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

                float3 pivotWorldPos = TransformObjectToWorld(float3(0,0,0));
                float floatingOffset = (pivotWorldPos.x + pivotWorldPos.y) * 10.0;
                float floatOffset = sin(_Time.y * 2.5 + floatingOffset) * (1.0 / 32.0);
                IN.positionOS.y += floatOffset;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 주어진 행렬식으로 예전 셰이더와 같은 순서의 계산을 해서 (자기 알파, 둘레 여덟 칸의 최대 알파)를 돌려준다
            half2 SampleRing(float det, float2 uv, float2 worldDelta, float2 dx_wp, float2 dy_wp, float2 dx_uv, float2 dy_uv, half baseAlpha, float outlineWidth)
            {
                float ppu = 32.0;
                float2 snappedUV = uv;

                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                half mainAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV).a * baseAlpha;

                float2 uvOffset_X = 0;
                float2 uvOffset_Y = 0;

                if (abs(det) > 1e-8)
                {
                    float one_over_ppu = 1.0 / ppu;
                    uvOffset_X = (one_over_ppu * (dy_wp.y * dx_uv - dy_wp.x * dy_uv)) / det;
                    uvOffset_Y = (one_over_ppu * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                }

                float2 finalOffset_X = uvOffset_X * outlineWidth;
                float2 finalOffset_Y = uvOffset_Y * outlineWidth;

                half alphaUp = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y).a;
                half alphaDown = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y).a;
                half alphaLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_X).a;
                half alphaRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_X).a;
                half alphaUpLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y - finalOffset_X).a;
                half alphaUpRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y + finalOffset_X).a;
                half alphaDownLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y - finalOffset_X).a;
                half alphaDownRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y + finalOffset_X).a;

                half outlineAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)),
                                        max(max(alphaUpLeft, alphaUpRight), max(alphaDownLeft, alphaDownRight)));

                return half2(mainAlpha, outlineAlpha);
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

                half baseAlpha = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor).a;
                float outlineWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineWidth);

                // 작성기(OutlineWriter_LogItem)의 행렬식 식. 둘째 항이 dx_wp.x 인 것까지 그대로 둔다(돌아간 원목에서 찍히던 자리를 맞추려고)
                float detWriter = dx_wp.x * dy_wp.y - dx_wp.y * dx_wp.x;
                // 외곽선 셰이더(OutlineShader_LogItem)와 몸통의 행렬식 식
                float detOutline = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;

                half2 writer = SampleRing(detWriter, IN.uv, worldDelta, dx_wp, dy_wp, dx_uv, dy_uv, baseAlpha, outlineWidth);
                half2 outline = SampleRing(detOutline, IN.uv, worldDelta, dx_wp, dy_wp, dx_uv, dy_uv, baseAlpha, outlineWidth);

                bool bWriterRing = writer.x < 0.1 && writer.y > 0.1;
                bool bOutlineRing = outline.x < 0.1 && outline.y > 0.1;
                // 몸통은 알파 0.01 이상인 자리에 그려지며 예전에는 그 자리의 스텐실을 지웠다
                bool bBodyCovers = outline.x >= 0.01;

                if (bWriterRing && bOutlineRing && false == bBodyCovers)
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
