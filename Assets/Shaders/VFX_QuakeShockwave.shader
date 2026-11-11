Shader "Custom/VFX/URP2D_QuakeShockwave"
{
    // 지진 앞머리를 따라 화면 전체로 퍼지는 충격파 왜곡. 화면을 덮는 쿼드 하나가 URP 2D 카메라 소팅 레이어 텍스처
    // (_CameraSortingLayerTexture, Renderer2D가 Objects 레이어까지 캡처)를 샘플링해 앞머리 고리 부분만 일그러뜨린다.
    // 고리 밖은 알파 0으로 버려 원래 화면이 그대로 보인다(VFX_HeatBurstDistortion과 같은 방식).
    //
    // 거리는 바닥 평면 거리(세로 차이를 두 배로 편 거리)라서 물결 앞머리(QuakeTileRipple)와 같은 마름모꼴 타원으로 퍼진다.
    // 진원·반지름·세기는 QuakeImpactFeedback이 MaterialPropertyBlock으로 매 프레임 넘긴다.
    Properties
    {
        _Center ("Center (world xy)", Vector) = (0, 0, 0, 0)
        _Radius ("Front Radius (floor distance)", Float) = 0.0
        _Width ("Ring Width (floor distance)", Float) = 0.7
        _Strength ("Strength (screen height units)", Float) = 0.012
        _Fade ("Fade (0~1)", Range(0, 1)) = 0.0

        [Header(Pixel Art Style)]
        [Toggle] _PixelSnap ("Snap Offset To Screen Pixels", Float) = 1
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
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS  : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _Center;
                float _Radius;
                float _Width;
                float _Strength;
                float _Fade;
                float _PixelSnap;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS.xy;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 delta = i.positionWS - _Center.xy;
                float2 floorDelta = float2(delta.x, delta.y * 2.0);
                float dist = length(floorDelta);

                // 앞머리 반지름에서 가장 센 가우시안 고리.
                float ringT = (dist - _Radius) / max(_Width, 0.0001);
                float ring = exp(-ringT * ringT) * _Fade;

                // 왜곡이 사실상 없는 곳(알파가 8비트 한 단계의 절반 미만)은 화면 샘플 없이 버린다.
                float alpha = saturate(ring * 2.5);
                if (alpha < 0.00196) discard;

                // 진원에서 바깥쪽(화면 방향)으로 민다. 세로 길이를 1로 둔 단위라 화면비로 가로를 나눠 고르게 만든다.
                float2 radialDir = delta / max(length(delta), 0.0001);
                float2 offset = radialDir * ring * _Strength;
                offset.x *= _ScreenParams.y / max(_ScreenParams.x, 1.0);

                // 화면 픽셀 단위로 반올림해 도트가 깨지듯 일렁이게 한다.
                if (_PixelSnap > 0.5)
                {
                    offset = round(offset * _ScreenParams.xy) / _ScreenParams.xy;
                }

                float2 screenUV = i.screenPos.xy / i.screenPos.w + offset;
                half4 bg = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV);
                return half4(bg.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
