// 흐르는 구름 그림자(StageCloudShadows). 화면을 덮는 판 하나가 그 뒤에 그려진 것(땅·물·나무)에 그림자 색을 곱한다.
// 구름 모양은 월드 좌표의 값 노이즈 세 겹으로 만들고 바람 방향으로 흘려 보낸다. 아이소메트릭 바닥에 맞게 세로를 절반으로 눌러 그린다.
// 텍셀(1/PPU) 단위로 계산하고 그림자 단계 경계만 4x4 순서 디더링으로 엇갈려 픽셀아트 그대로다.
// 값은 StageCloudShadows가 머티리얼 사본에 넣는다.
// _CloudA : 흐르는 방향 x, y(정규화), 속도(월드/초), 구름 덩어리 크기(월드)
// _CloudB : 덮는 비율(0~1), 경계 퍼짐 폭, 단계 수, 단계 경계가 엇갈리는 폭(0~1)
// _CloudColor : 그림자 속에서 곱하는 색
Shader "Custom/StageCloudShadow"
{
    Properties
    {
        _CloudA("방향·속도·크기", Vector) = (1, -0.35, 0.6, 10)
        _CloudB("덮는 비율·퍼짐·단계·엇갈림", Vector) = (0.04, 0.05, 2, 0.6)
        _CloudColor("그림자 색", Color) = (0.78, 0.84, 0.95, 1)
        _PixelsPerUnit("월드 픽셀 수(PPU)", Float) = 32
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

        // 곱하기: 결과 = 그 자리 색 x 그림자 색(그림자 밖은 1이라 그대로)
        Blend DstColor Zero
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudShadowPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudA;
                float4 _CloudB;
                half4 _CloudColor;
                float _PixelsPerUnit;
            CBUFFER_END

            // 정수 좌표용 해시(큰 좌표에서도 무늬가 깨지지 않는다).
            float CloudHash21(float2 _p)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float CloudNoise(float2 _p)
            {
                float2 i = floor(_p);
                float2 f = frac(_p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = CloudHash21(i);
                float b = CloudHash21(i + float2(1.0, 0.0));
                float c = CloudHash21(i + float2(0.0, 1.0));
                float d = CloudHash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 4x4 순서 디더링 문턱(0~1). WaterDepth.hlsl의 WaterDepthBayer와 같은 표.
            float CloudBayer(float2 _texelId)
            {
                float2 p = _texelId - 4.0 * floor(_texelId * 0.25);
                float2 high = floor(p * 0.5);
                float2 low = p - 2.0 * high;
                float m = 4.0 * abs(2.0 * low.x - 3.0 * low.y) + abs(2.0 * high.x - 3.0 * high.y);
                return (m + 0.5) / 16.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                float2 texelId = floor(input.positionWS * ppu);
                float2 centerWS = (texelId + 0.5) / ppu;

                float2 dir = _CloudA.xy;
                float scale = max(_CloudA.w, 0.5);
                float2 drift = dir * (_Time.y * _CloudA.z);
                // 아이소메트릭 바닥: 화면 세로 1이 바닥 2만큼이라 세로를 두 배로 늘려 둥근 구름이 바닥에 눕게 한다.
                float2 p = (centerWS - drift) * float2(1.0, 2.0) / scale;
                // 값 노이즈 세 겹을 겹마다 방향을 틀어 얹는다(격자 방향 네모 모양이 드러나지 않게).
                // 잔 겹은 조금 다르게 흘러 구름 모양이 천천히 바뀐다.
                float2 slide = dir * (_Time.y * _CloudA.z / scale);
                const float2x2 turnA = float2x2(0.8, -0.6, 0.6, 0.8);
                const float2x2 turnB = float2x2(0.28, -0.96, 0.96, 0.28);
                float2 q = mul(turnA, p);
                float n = CloudNoise(q) * 0.55
                        + CloudNoise(mul(turnB, q) * 2.03 + slide * 0.4 + 17.3) * 0.3
                        + CloudNoise(mul(turnA, q) * 4.07 - slide * 0.6 + 41.7) * 0.15;

                float coverage = saturate(_CloudB.x);
                float soft = max(_CloudB.y, 0.005);
                // 세 겹을 더한 값은 0.5 둘레(표준편차 약 0.13)에 몰려 있어 덮는 비율 0~1을 0.8~0.3 문턱으로 옮긴다
                // (0.04면 넓게 볼 때 약 1~2%, 0.12면 약 7%, 0.3이면 약 22%, 0.5면 약 절반).
                float threshold = lerp(0.8, 0.3, coverage);
                float amount = smoothstep(threshold - soft, threshold + soft, n);

                float bands = max(_CloudB.z, 1.0);
                float level = saturate(floor(amount * bands + 0.5 + (CloudBayer(texelId) - 0.5) * _CloudB.w) / bands);
                return half4(lerp(half3(1.0, 1.0, 1.0), _CloudColor.rgb, (half)level), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
