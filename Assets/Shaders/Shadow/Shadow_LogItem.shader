Shader "Custom/Shadow_LogItem"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        [Header(Height Based Frame Selection)]
        // xMin,yMin,xMax,yMax (0~1 UV). Frame1이 SpriteRenderer에 실제로 할당된 기본(0 위치) 스프라이트여야 한다.
        _ShadowFrameRect0("Frame -1 UV Rect", Vector) = (0,0,1,1)
        _ShadowFrameRect1("Frame 0 UV Rect (Base)", Vector) = (0,0,1,1)
        _ShadowFrameRect2("Frame 1 UV Rect", Vector) = (0,0,1,1)
        _ShadowFrameRect3("Frame 2+ UV Rect", Vector) = (0,0,1,1)
        // 포물선 비행 중(Dropped 이전)에만 CPU가 매 프레임 갱신하는 값(픽셀 단위). Dropped 상태에서는 0으로 유지되어
        // 별도의 CPU 갱신 없이 아래의 사인파 공식만으로 둥둥 뜨는 프레임 전환이 이루어진다.
        // 원목(LogItem)은 높이를 스프라이트 색의 알파로 넘기고(아래 frag 참고) 이 값은 속성 블록에 언제나 0으로 둔다. 다른 아이템은 이 값으로 넘긴다(둘은 더해진다).
        _ShadowHeightPixels("Flight Height In Pixels (CPU-Driven)", Float) = 0
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
                Ref 4
                ReadMask 4
                WriteMask 4
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // Core2D.hlsl: Core.hlsl에 더해, 인스턴싱으로 그릴 때의 스프라이트 색(unity_SpriteColor)을 인스턴스별로 읽게 해 준다
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                // 스프라이트 색의 알파(묶여 그려질 때는 정점 색, 따로 그려질 때는 unity_SpriteColor에 들어온다. 둘 중 하나는 1이다)
                nointerpolation float spriteAlpha : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // 모든 LogItem이 동일한 그림자 스프라이트시트를 공유하므로, 프레임 Rect는 인스턴스별 값이 아니라
            // 공유 머티리얼에 한 번만 기록되는 일반 프로퍼티로 둔다(인스턴싱 버퍼 전달 신뢰성 문제 회피).
            float4 _ShadowFrameRect0;
            float4 _ShadowFrameRect1;
            float4 _ShadowFrameRect2;
            float4 _ShadowFrameRect3;
            // Shadow 오브젝트의 로컬 오프셋(xy). 본체(Animator)와 같은 부모를 공유하는 회전/스케일 없는 형제이므로,
            // 이 값을 빼면 본체 셰이더와 동일한 월드 기준점을 복원해 둥둥 뜨는 위상을 정확히 맞출 수 있다.
            float4 _ShadowLocalOffset;

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShadowHeightPixels)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.spriteAlpha = IN.color.a * unity_SpriteColor.a;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
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

                // 원목 본체(Custom-Sprite-Default_LogItem.shader)와 동일한 공식으로 Dropped 상태의 둥둥 뜨는
                // 픽셀 오프셋을 재현한다. CPU 개입 없이 월드 위치 + 시간만으로 계산되므로 항상 GPU에서만 처리된다.
                // Shadow 자신의 로컬 오프셋을 빼서 본체(Animator)와 동일한 월드 기준점을 복원한다.
                float3 rawPivotWorldPos = TransformObjectToWorld(float3(0, 0, 0));
                float3 pivotWorldPos = rawPivotWorldPos - float3(_ShadowLocalOffset.xy, 0.0);
                float floatingPhase = (pivotWorldPos.x + pivotWorldPos.y) * 10.0;
                float idleOffsetPixels = sin(_Time.y * 2.5 + floatingPhase) * 1.0; // -1..1 픽셀

                // 포물선 비행 중일 때만 CPU가 채워주는 값(착지 시 0으로 리셋된 뒤 다시 갱신되지 않음)
                // 원목은 스프라이트 색의 알파로 넘긴다: 알파 1 = 0픽셀, 알파 0.2 = 2.5픽셀 이상(LogItem.SetShadowFlightHeight와 같은 식).
                // 렌더러마다 내용이 다른 속성 블록을 쓰면 그림자가 하나씩 따로 그려지기 때문이다(날아가는 원목 270개일 때 그리기 준비 약 0.9ms).
                // 블록 내용이 모두 같으면 함께 그려지고, 스프라이트 색은 달라도 함께 그려진다.
                // 2.5픽셀 이상은 아래 판정에서 언제나 마지막 프레임이라(둥둥 값이 -1 이상) 잘라도 결과가 같다.
                // 이 셰이더의 그림자는 스프라이트 색을 다른 데 쓰지 않으며, 색이 흰색(알파 1)이면 0픽셀이라 예전과 같다.
                float flightHeightPixels = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadowHeightPixels)
                    + (1.0 - saturate(IN.spriteAlpha)) * (2.5 / 0.8);

                float totalPixels = idleOffsetPixels + flightHeightPixels;

                float4 frameRect;
                if (totalPixels < -0.5)
                    frameRect = _ShadowFrameRect0;
                else if (totalPixels < 0.5)
                    frameRect = _ShadowFrameRect1;
                else if (totalPixels < 1.5)
                    frameRect = _ShadowFrameRect2;
                else
                    frameRect = _ShadowFrameRect3;

                // snappedUV는 SpriteRenderer에 실제로 할당된 기본(0 위치) 스프라이트 기준이므로,
                // 이를 0~1 로컬 좌표로 정규화한 뒤 선택된 프레임의 UV 사각형으로 재매핑한다.
                float4 baseRect = _ShadowFrameRect1;
                float2 localUV = (snappedUV - baseRect.xy) / max(baseRect.zw - baseRect.xy, 1e-6);
                float2 remappedUV = frameRect.xy + localUV * (frameRect.zw - frameRect.xy);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, remappedUV);

                clip(texColor.a - 0.1);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                return half4(baseColor.rgb, texColor.a * baseColor.a);
            }
            ENDHLSL
        }
    }
}
