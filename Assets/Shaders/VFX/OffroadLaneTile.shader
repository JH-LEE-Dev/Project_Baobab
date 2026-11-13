// 오프로드 경로 표시용 타일 점등 셰이더. 칸 하나 = 32x16px 쿼드 한 장이고, 쿼드 안에서 아이소 타일 윗면(2:1 마름모)만 텍셀 단위로 잘라 그린다.
// 정점 색: r = 덮기 진하기(0~1), g = 발광(0~2를 0~1로 눌러 담음), b = 이웃 칸 꺼짐 비트(0 NE, 1 NW, 2 SW, 3 SE, 4 N, 5 S, 6 E, 7 W), a = 미사용.
// uv0 = 쿼드 안 텍셀 좌표(x 0~32, y 0~16, y는 위에서 아래로).
// 칸 모양은 타일 그림 윗면과 같은 마름모(폭 4, 8, ..., 32, 32, ..., 4)다. 이웃 칸과 겹치는 2px 띠는 항상 화면 앞쪽(아래) 칸이 그린다(타일 그림이 앞 타일이 뒤 타일을 덮는 순서와 같다):
// 켜진 SW/SE 이웃 칸의 마름모 안에도 들어가는 텍셀은 버린다. 그래서 모든 칸 경계가 가로 2px씩 내려가는 2:1 계단 한 가지이고 칸 좌우 꼭짓점이 한 줄이다.
// 바깥 테두리(1px 선): 영역 = 켜진 칸의 마름모 합집합(자기 칸 + 켜진 이웃 8칸의 마름모). 기본 테두리 = 영역 픽셀 중 위 또는 아래 픽셀이 영역 밖인 픽셀.
// 볼록 좌우 꼭짓점은 테두리가 2x2로 뭉치므로(윗면 가운데 두 줄 폭이 같아서) 꼭짓점이 바깥으로 드러난 쪽의 가장 바깥 세로 한 줄(2텍셀)을 그리지 않는다(꼭짓점이 세로 1x2).
// 오목 옆 모서리는 위아래는 영역 안인데 옆만 바깥인 픽셀을 테두리에 더해 끊긴 선을 세로 1x2로 잇는다. 꼭짓점 정리는 꼭짓점 둘레(행 3~12, 좌우 끝 7열)에서만 계산하고, 옆 보정은 모든 텍셀에서 계산한다(세로로 쌓인 칸 사이 오목 홈은 칸 가운데쪽에 생긴다).
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
            #pragma target 3.0

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

            // 꺼짐 비트 _bit(0~7)가 켜져 있으면 1
            float HasBit(float _flags, float _bit)
            {
                return fmod(floor(_flags / exp2(_bit) + 0.001), 2.0);
            }

            // 이웃 칸 _bit이 켜져 있으면 1
            float On(float _flags, float _bit)
            {
                return 1.0 - HasBit(_flags, _bit);
            }

            // 영역: 이 칸과 켜진 이웃 8칸(NE, NW, SW, SE, N, S, E, W)의 마름모 합집합. (_x, _y)는 이 칸 쿼드 기준 텍셀 좌표이고 쿼드 밖도 된다.
            float InUnion(float _x, float _y, float _flags)
            {
                float r = InsideDiamond(_x, _y);
                r = max(r, On(_flags, 4.0) * InsideDiamond(_x, _y + 16.0));
                r = max(r, On(_flags, 5.0) * InsideDiamond(_x, _y - 16.0));
                r = max(r, On(_flags, 0.0) * InsideDiamond(_x - 16.0, _y + 8.0));
                r = max(r, On(_flags, 1.0) * InsideDiamond(_x + 16.0, _y + 8.0));
                r = max(r, On(_flags, 3.0) * InsideDiamond(_x - 16.0, _y - 8.0));
                r = max(r, On(_flags, 2.0) * InsideDiamond(_x + 16.0, _y - 8.0));
                r = max(r, On(_flags, 7.0) * InsideDiamond(_x + 32.0, _y));
                r = max(r, On(_flags, 6.0) * InsideDiamond(_x - 32.0, _y));
                return r;
            }

            // 기본 테두리: 영역 픽셀 중 위 또는 아래 픽셀이 영역 밖
            float BaseEdge(float _x, float _y, float _flags)
            {
                return InUnion(_x, _y, _flags) * max(1.0 - InUnion(_x, _y - 1.0, _flags), 1.0 - InUnion(_x, _y + 1.0, _flags));
            }

            // (_bx, _by)가 왼쪽 위인 2x2가 전부 기본 테두리인가
            float EdgeBlock(float _bx, float _by, float _flags)
            {
                return BaseEdge(_bx, _by, _flags) * BaseEdge(_bx + 1.0, _by, _flags) * BaseEdge(_bx, _by + 1.0, _flags) * BaseEdge(_bx + 1.0, _by + 1.0, _flags);
            }

            // 볼록 좌우 꼭짓점에서 지우는 픽셀. 2x2 테두리 덩어리의 왼쪽 바깥 두 픽셀이 영역 밖이면 그 덩어리의 왼쪽 세로 한 줄을,
            // (왼쪽이 아니고) 오른쪽 바깥 두 픽셀이 영역 밖이면 오른쪽 세로 한 줄을 지운다.
            float Killed(float _x, float _y, float _flags)
            {
                float k = 0.0;
                for (int n = 0; n < 2; n++)
                {
                    float y0 = _y - n;
                    float leftOut = (1.0 - InUnion(_x - 1.0, y0, _flags)) * (1.0 - InUnion(_x - 1.0, y0 + 1.0, _flags));
                    k = max(k, EdgeBlock(_x, y0, _flags) * leftOut);

                    float rightOut = (1.0 - InUnion(_x + 1.0, y0, _flags)) * (1.0 - InUnion(_x + 1.0, y0 + 1.0, _flags));
                    float blockLeftOut = (1.0 - InUnion(_x - 2.0, y0, _flags)) * (1.0 - InUnion(_x - 2.0, y0 + 1.0, _flags));
                    k = max(k, EdgeBlock(_x - 1.0, y0, _flags) * rightOut * (1.0 - blockLeftOut));
                }

                return k * InUnion(_x, _y, _flags);
            }

            // 꼭짓점 정리 뒤의 영역
            float InUnionTrimmed(float _x, float _y, float _flags)
            {
                return InUnion(_x, _y, _flags) * (1.0 - Killed(_x, _y, _flags));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 texel = floor(input.uv);
                if (InsideDiamond(texel.x, texel.y) < 0.5) discard;

                float cover = input.color.r;
                float glow = input.color.g * 2.0;
                float flags = round(input.color.b * 255.0);

                // 이웃 칸과 겹치는 2px 띠는 항상 화면 앞쪽(아래) 칸이 그린다(타일 그림이 앞 타일이 뒤 타일을 덮는 순서와 같다).
                // 켜져 있는 SW 이웃((-16, +8)텍셀 이동) 또는 SE 이웃((+16, +8))의 마름모 안에도 들어가는 텍셀은 그 이웃이 그리므로 버린다. 위쪽 절반은 버리지 않는다.
                // 그러면 모든 칸 경계가 가로 2px씩 내려가는 2:1 계단 한 가지가 되고 칸 좌우 꼭짓점은 한 줄이다.
                if (InsideDiamond(texel.x + 16.0, texel.y - 8.0) > 0.5 && HasBit(flags, 2.0) < 0.5) discard;
                if (InsideDiamond(texel.x - 16.0, texel.y - 8.0) > 0.5 && HasBit(flags, 3.0) < 0.5) discard;

                // 바깥 테두리. 꼭짓점 둘레(행 3~12, 좌우 끝 7열)는 꼭짓점 정리와 오목 옆 모서리 보정까지 계산하고, 나머지는 기본 테두리와 옆 보정(위아래 계산은 같음, 옆 픽셀은 정리 전 영역 기준)만 쓴다.
                float edge;
                if (abs(texel.y - 7.5) <= 4.5 && (texel.x <= 6.0 || 25.0 <= texel.x))
                {
                    if (Killed(texel.x, texel.y, flags) > 0.5) discard;

                    float upIn = InUnionTrimmed(texel.x, texel.y - 1.0, flags);
                    float downIn = InUnionTrimmed(texel.x, texel.y + 1.0, flags);
                    edge = max(1.0 - upIn, 1.0 - downIn);
                    float sideOut = max(1.0 - InUnionTrimmed(texel.x - 1.0, texel.y, flags), 1.0 - InUnionTrimmed(texel.x + 1.0, texel.y, flags));
                    edge = max(edge, sideOut);
                }
                else
                {
                    edge = BaseEdge(texel.x, texel.y, flags);
                    edge = max(edge, max(1.0 - InUnion(texel.x - 1.0, texel.y, flags), 1.0 - InUnion(texel.x + 1.0, texel.y, flags)));
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
