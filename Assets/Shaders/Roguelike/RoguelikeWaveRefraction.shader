Shader "Custom/Roguelike/WaveRefraction"
{
    // 로그라이크 바닥 타일 파도 앞머리를 따라 퍼지는 약한 굴절 고리. URP 2D 카메라 소팅 레이어 텍스처(_CameraSortingLayerTexture,
    // Renderer2D가 Objects 레이어까지 캡처)를 고리 자리에서만 바깥/안쪽으로 몇 픽셀 밀어 그린다. 고리는 섬 중심에서의 파도 거리
    // (바닥 평면 거리 - 세로를 두 배로 편 거리)로 그려 화면에서는 가로로 긴 타원이다.
    // 미는 양은 게임 픽셀(1/32 월드) 단위로 끊는다 - 도트가 반 픽셀씩 번지지 않고 통째로 밀린다. 밀지 않는 곳은 버린다(원래 화면 그대로).
    // 고리 자리·세기는 RoguelikeTileWave가 MaterialPropertyBlock으로 매 프레임 넘긴다. 칸 물결과 같은 앞머리 일렁임은 _WobbleTex
    // (월드 사각형 _WobbleRect = 중심 xy·크기 zw를 덮는다. 0.5 = 0, 0~1 = ±_WobbleRange 파도 거리)로 받아 거리에 더한다.
    Properties
    {
        _RingCenter ("Ring Center (world xy)", Vector) = (0, 0, 0, 0)
        _RingRadius ("Ring Radius (wave distance)", Float) = 0
        _RingWidth ("Ring Width (wave distance)", Float) = 1.2
        _RingPixels ("Ring Strength (game pixels)", Float) = 2
        [NoScaleOffset] _WobbleTex ("Front Wobble (quad uv)", 2D) = "gray" {}
        _WobbleRange ("Front Wobble Range (wave distance)", Float) = 0
        _WobbleRect ("Front Wobble Rect (world center xy, size zw)", Vector) = (0, 0, 1, 1)
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
                float3 worldPos    : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);
            TEXTURE2D(_WobbleTex);
            SAMPLER(sampler_WobbleTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _RingCenter;
                float _RingRadius;
                float _RingWidth;
                float _RingPixels;
                float _WobbleRange;
                float4 _WobbleRect;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 게임 픽셀 중심에서 재야 한 픽셀 안이 모두 같이 밀린다.
                float2 worldPos = (floor(i.worldPos.xy * 32.0) + 0.5) / 32.0;
                float2 offset = worldPos - _RingCenter.xy;
                float2 wobbleUV = (worldPos - _WobbleRect.xy) / max(_WobbleRect.zw, 0.0001) + 0.5;
                float wobble = (SAMPLE_TEXTURE2D(_WobbleTex, sampler_WobbleTex, wobbleUV).r - 0.5) * 2.0 * _WobbleRange;
                float dist = length(float2(offset.x, offset.y * 2.0)) + wobble;
                float t = (dist - _RingRadius) / max(_RingWidth, 0.0001);
                if (abs(t) > 3.0) discard;

                // 렌즈 고리: 고리 안쪽은 바깥으로, 바깥쪽은 안쪽으로 당긴다(-t·e^(-t²), 최댓값 1로 맞춤).
                float lens = -t * exp(-t * t) * 2.3316;
                float2 direction = offset / max(length(offset), 0.0001);
                float2 pixels = round(direction * lens * _RingPixels);
                if (dot(pixels, pixels) < 0.5) discard;

                // 게임 픽셀 → 화면 UV(정사영 카메라: 화면 너비 = 2 × unity_OrthoParams.x 월드).
                float2 uvOffset = (pixels / 32.0) / (2.0 * unity_OrthoParams.xy);
                float2 screenUV = i.screenPos.xy / i.screenPos.w + uvOffset;
                half4 bg = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV);
                return half4(bg.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
