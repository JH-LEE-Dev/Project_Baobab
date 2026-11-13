// 오프로드 경로 표시용 타일 점등 셰이더. 칸 하나 = 32x16px 쿼드 한 장이고, 쿼드 안에서 아이소 타일 윗면(2:1 마름모)만 텍셀 단위로 잘라 그린다.
// 정점 색: r = 덮기 진하기(0~1), g = 발광(0~2를 0~1로 눌러 담음), b = 이웃 칸 꺼짐 비트(0 NE, 1 NW, 2 SW, 3 SE, 4 N, 5 S), a = 미사용.
// uv0 = 쿼드 안 텍셀 좌표(x 0~32, y 0~16, y는 위에서 아래로).
// 출력은 프리멀티플라이드: rgb = 바탕색 x 덮기 + 밝은색 x 발광, a = 덮기. Blend One OneMinusSrcAlpha 한 번으로 덮기와 가산을 같이 한다.
Shader "Custom/VFX/OffroadLaneTile"
{
    Properties
    {
        _BaseColor ("Base Color (덮는 바탕색)", Color) = (1.0, 0.69, 0.22, 1.0)
        _HighColor ("High Color (발광에 더하는 밝은색)", Color) = (1.0, 0.925, 0.745, 1.0)
        _CoverScale ("Cover Scale (덮기 알파 배율)", Range(0.0, 2.0)) = 1.0
        _GlowScale ("Glow Scale (발광 가산 배율)", Range(0.0, 4.0)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OffroadLaneTilePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HighColor;
                float _CoverScale;
                float _GlowScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            // 텍셀(_x 0~31, _y 0~15, 위에서 아래)이 아이소 타일 윗면(폭 4, 8, ..., 32, 32, 28, ..., 4) 안인지
            float InsideDiamond(float _x, float _y)
            {
                float dx = abs(_x + 0.5 - 16.0);
                float dy = abs(_y + 0.5 - 8.0);
                return step(dx * 0.5 + dy, 8.5 - 0.001);
            }

            // 이웃 칸과 겹치지 않고 정확히 맞물리는 좁은 마름모(폭 2, 6, ..., 30, 30, ..., 2). 넓은 마름모와의 차이(가장자리 1텍셀 띠)는
            // 이웃 칸의 좁은 마름모 안이라, 그 이웃이 켜져 있으면 그리지 않아야 경계가 두 번 덮이지 않는다.
            float InsideNarrow(float _x, float _y)
            {
                float dx = abs(_x + 0.5 - 16.0);
                float dy = abs(_y + 0.5 - 8.0);
                return step(dx * 0.5 + dy, 8.0 - 0.001);
            }

            // 꺼짐 비트 _bit(0~5)가 켜져 있으면 1
            float HasBit(float _flags, float _bit)
            {
                return fmod(floor(_flags / exp2(_bit) + 0.001), 2.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 texel = floor(input.uv);
                if (InsideDiamond(texel.x, texel.y) < 0.5) discard;

                float cover = input.color.r;
                float glow = input.color.g * 2.0;
                float flags = round(input.color.b * 255.0);

                // 가장자리 띠 텍셀은 그쪽 이웃 칸(위 왼쪽 NW, 위 오른쪽 NE, 아래 왼쪽 SW, 아래 오른쪽 SE)이 켜져 있으면 이웃이 그린다
                if (InsideNarrow(texel.x, texel.y) < 0.5)
                {
                    float rimBit = texel.y < 8.0 ? (texel.x < 16.0 ? 1.0 : 0.0) : (texel.x < 16.0 ? 2.0 : 3.0);
                    if (HasBit(flags, rimBit) < 0.5) discard;
                }

                // 위쪽 절반은 위 텍셀이, 아래쪽 절반은 아래 텍셀이 마름모 밖이면 그 방향 이웃 칸과 맞닿은 가장자리다(2:1 계단).
                // 꼭짓점 4텍셀(x 14~17)은 대각 이웃(N, S) 칸과 맞닿는다.
                float edge = 0.0;
                bool bLeft = texel.x < 16.0;
                bool bTip = 14.0 <= texel.x && texel.x <= 17.0;

                if (InsideDiamond(texel.x, texel.y - 1.0) < 0.5)
                {
                    float bit = (texel.y < 0.5 && bTip) ? 4.0 : (bLeft ? 1.0 : 0.0);
                    edge = max(edge, HasBit(flags, bit));
                }

                if (InsideDiamond(texel.x, texel.y + 1.0) < 0.5)
                {
                    float bit = (14.5 < texel.y && bTip) ? 5.0 : (bLeft ? 2.0 : 3.0);
                    edge = max(edge, HasBit(flags, bit));
                }

                float alpha = lerp(cover * 0.8, min(0.9, cover + 0.5), edge);
                float add = glow + edge * (0.1 + cover * 0.3);
                alpha = saturate(alpha * _CoverScale);

                float3 rgb = _BaseColor.rgb * alpha + _HighColor.rgb * (add * _GlowScale);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
