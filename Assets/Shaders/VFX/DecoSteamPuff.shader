// 데코 증기 김 덩어리. 파티클 하나(정사각형)에 울퉁불퉁한 구름 덩어리 하나를 픽셀 단위로 그린다.
// 덩어리는 동그라미 몇 개(혹)를 겹친 모양이고, 윗가장자리는 밝게·아랫가장자리는 어둡게 칠해 픽셀아트 구름처럼 보인다.
// 작게 나타나 커지고, 수명 끝무렵엔 작아지지 않고 픽셀이 점점이 흩어져 사라진다(캐릭터 먼지의 작아지는 원과 구분).
// 사각형 꼭짓점을 월드 픽셀(1/PPU) 격자에 맞추고, 크기가 정수 픽셀이면 한 칸 = 한 월드 픽셀로 그린다.
// StageDecoSteam의 파티클 시스템이 쓴다. 파티클 정점 스트림: 위치, 색, UV, 수명 비율, 고정 난수, 크기(가로·세로).
Shader "Custom/VFX/DecoSteamPuff"
{
    Properties
    {
        _PixelsPerUnit("월드 픽셀 수(PPU)", Float) = 32
        // 처음 크기 비율과 다 커지는 수명 비율
        _StartScale("처음 크기 비율", Range(0.1, 1)) = 0.45
        _GrowTime("다 커지는 때(수명 비율)", Range(0.05, 1)) = 0.45
        // 흩어지기 시작하는 수명 비율
        _DissolveStart("흩어짐 시작(수명 비율)", Range(0, 1)) = 0.55
        // 혹 위치가 덩어리마다 흔들리는 정도(칸 크기 비율)
        _LobeJitter("혹 흔들림", Range(0, 0.2)) = 0.08
        // 윗가장자리 밝기 배수, 아랫가장자리 밝기 배수
        _TopLight("윗가장자리 밝기", Float) = 1.18
        _BottomShade("아랫가장자리 밝기", Float) = 0.78
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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "DecoSteamPuffPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                // xy = UV, z = 수명 비율(0~1), w = 고정 난수(0~1)
                float4 data : TEXCOORD0;
                // xy = 파티클 크기(월드)
                float2 size : TEXCOORD1;
            };

            // 덩어리 모양(혹 위치·반지름)과 흩어짐 정도는 파티클 안에서 모두 같으므로 정점에서 한 번 구해 넘긴다.
            // 네 꼭짓점 값이 같아 보간해도 그대로이고, 픽셀에서는 거리 비교와 흩어짐 난수 하나만 한다.
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                // xy = 칸 좌표(UV × 칸 수), zw = 칸 수(픽셀)
                float4 cellInfo : TEXCOORD0;
                // 혹 0·1 중심, 혹 2·3 중심(칸 좌표)
                float4 lobes01 : TEXCOORD1;
                float4 lobes23 : TEXCOORD2;
                // 혹 0~3의 (반지름 + 여유)²
                float4 radiiSq : TEXCOORD3;
                // x = 흩어짐 정도(0~1), y = 흩어짐 난수 오프셋
                float2 misc : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float _PixelsPerUnit;
                float _StartScale;
                float _GrowTime;
                float _DissolveStart;
                float _LobeJitter;
                float _TopLight;
                float _BottomShade;
            CBUFFER_END

            float PuffHash(float2 _p)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float ppu = max(_PixelsPerUnit, 1.0);
                positionWS.xy = floor(positionWS.xy * ppu + 0.5) / ppu;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.color = input.color;

                float2 sizePx = max(round(input.size.xy * ppu), 1.0);
                output.cellInfo = float4(input.data.xy * sizePx, sizePx);

                float age = input.data.z;
                float seed = floor(input.data.w * 997.0);
                float n = sizePx.x;

                // 작게 나타나 커진다.
                float grow = smoothstep(0.0, 1.0, saturate(age / _GrowTime));
                float scale = lerp(_StartScale, 1.0, grow);

                // 혹 4개: 가운데 큰 혹, 좌우 혹, 위 혹. 덩어리마다 혹 자리가 조금씩 흔들린다.
                float j = _LobeJitter * n;
                float2 c = float2(n * 0.5, n * 0.42);
                float2 o1 = float2(PuffHash(float2(seed, 1.0)), PuffHash(float2(seed, 2.0))) - 0.5;
                float2 o2 = float2(PuffHash(float2(seed, 3.0)), PuffHash(float2(seed, 4.0))) - 0.5;
                float2 o3 = float2(PuffHash(float2(seed, 5.0)), PuffHash(float2(seed, 6.0))) - 0.5;
                float2 lobe1 = c + float2(-0.22 * n, -0.04 * n) * scale + o1 * j;
                float2 lobe2 = c + float2(0.21 * n, -0.02 * n) * scale + o2 * j;
                float2 lobe3 = c + float2(0.04 * n, 0.17 * n) * scale + o3 * j;
                output.lobes01 = float4(c, lobe1);
                output.lobes23 = float4(lobe2, lobe3);

                // 반지름에 픽셀 여유를 더해 둥근 끝에 1픽셀 돌기가 튀어나오지 않게 한다.
                const float pad = 0.4;
                float4 radii = float4(0.27, 0.19, 0.2, 0.2) * (n * scale) + pad;
                output.radiiSq = radii * radii;

                output.misc = float2(saturate((age - _DissolveStart) / max(1.0 - _DissolveStart, 0.001)), seed * 0.37);
                return output;
            }

            // 점 _p(칸 좌표)가 덩어리 안인지(혹 하나라도 안이면 안)
            bool InsidePuff(float2 _p, Varyings _input)
            {
                float2 d0 = _p - _input.lobes01.xy;
                float2 d1 = _p - _input.lobes01.zw;
                float2 d2 = _p - _input.lobes23.xy;
                float2 d3 = _p - _input.lobes23.zw;
                float4 distSq = float4(dot(d0, d0), dot(d1, d1), dot(d2, d2), dot(d3, d3));
                return any(distSq < _input.radiiSq);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 cell = min(floor(input.cellInfo.xy), input.cellInfo.zw - 1.0);
                float2 p = cell + 0.5;
                if (false == InsidePuff(p, input))
                {
                    discard;
                }

                // 끝무렵 픽셀이 하나씩 빠지며 흩어진다(가장자리부터 빠지도록 위아래 이웃이 빈 칸은 먼저 뺀다).
                bool bTopEdge = false == InsidePuff(p + float2(0.0, 1.0), input);
                bool bBottomEdge = false == InsidePuff(p - float2(0.0, 1.0), input);
                float dissolve = input.misc.x;
                if (dissolve > 0.0)
                {
                    float pixelNoise = PuffHash(cell + input.misc.y);
                    float edgeBias = (bTopEdge || bBottomEdge) ? 0.25 : 0.0;
                    if (pixelNoise * (1.0 - edgeBias) < dissolve * 1.05)
                    {
                        discard;
                    }
                }

                // 윗가장자리 밝게, 아랫가장자리 어둡게(픽셀 3단 음영)
                half shade = bTopEdge ? (half)_TopLight : (bBottomEdge ? (half)_BottomShade : 1.0h);
                return half4(saturate(input.color.rgb * shade), input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
