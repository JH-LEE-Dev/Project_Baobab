// 포자 안개. 바닥 위로 낮게 깔린 연보라 안개 덩어리가 바람 방향으로 천천히 흘러가며 모양을 바꾼다.
// StageSporeHaze가 카메라를 따라다니는 흰 스프라이트 한 장에 이 머티리얼을 끼워, 바닥 타일 바로 위·그림자와 오브젝트 아래에 그린다.
// 안개 모양은 월드 좌표로 계산하므로 카메라가 움직여도 안개는 땅에 붙어 흘러간다.
// 픽셀아트에 맞게 타일 픽셀(1/PPU) 단위로 끊고, 가장자리는 몇 단계 진하기로 나눠 디더링한다.
// 2D 조명을 받으므로 조명이 어두운 곳에서는 안개도 같이 어둡다.
Shader "Custom/2D/SporeHaze"
{
    Properties
    {
        [HideInInspector] _MainTex("Diffuse", 2D) = "white" {}
        [HideInInspector] _MaskTex("Mask", 2D) = "white" {}
        [HideInInspector] _NormalMap("Normal Map", 2D) = "bump" {}

        [HDR] _HazeColor("안개 색", Color) = (0.9, 0.82, 1, 1)
        _HazeMaxAlpha("가장 진한 곳 불투명도", Range(0, 1)) = 0.22
        _HazeScale("덩어리 크기(월드)", Float) = 9
        _HazeCoverage("덮는 정도(클수록 덜 덮음)", Range(0, 1)) = 0.5
        _HazeSoftness("가장자리 부드러움", Range(0.01, 1)) = 0.25
        _HazeDrift("흐름: 방향 x, y, 속도(월드/초)", Vector) = (1, -0.3, 0.35, 0)
        _HazeEvolve("모양 바뀌는 속도", Float) = 0.04
        _HazeWarp("일렁임(뭉게짐) 세기", Float) = 1.2
        _HazeSteps("진하기 단계 수(0이면 매끈)", Float) = 4

        // 스텐실로 그릴 곳을 고른다(기본은 늘 그림). 물(용암) 칸은 물 스텐실이 1번 비트를 켜 두므로,
        // 바닥 칸에만 그리고 물 칸은 건너뛰려면 Ref 0, ReadMask 1, Comp Equal(3).
        _HazeStencilRef("스텐실 기준값", Float) = 0
        _HazeStencilReadMask("스텐실 읽기 마스크", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _HazeStencilComp("스텐실 비교", Float) = 8

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref [_HazeStencilRef]
            ReadMask [_HazeStencilReadMask]
            WriteMask 0
            Comp [_HazeStencilComp]
            Pass Keep
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/StageAmbienceCommon.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                float2 hazePositionWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HazeColor;
                float _HazeMaxAlpha;
                float _HazeScale;
                float _HazeCoverage;
                float _HazeSoftness;
                float4 _HazeDrift;
                float _HazeEvolve;
                float _HazeWarp;
                float _HazeSteps;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.hazePositionWS = TransformObjectToWorld(input.positionOS).xy;

                return o;
            }

            float HazeHash21(float2 _p)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float HazeNoise(float2 _p)
            {
                float2 i = floor(_p);
                float2 f = frac(_p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = HazeHash21(i);
                float b = HazeHash21(i + float2(1.0, 0.0));
                float c = HazeHash21(i + float2(0.0, 1.0));
                float d = HazeHash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3겹 값 노이즈(0~1). 겹마다 살짝 돌려 격자 무늬가 드러나지 않게 한다.
            float HazeFbm(float2 _p)
            {
                const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    sum += HazeNoise(_p) * amp;
                    _p = mul(rot, _p) * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return sum / 0.875;
            }

            // 4x4 베이어 문턱(0~1 미만).
            float HazeBayer4(float2 _cell)
            {
                float2 c = fmod(_cell, 4.0);
                c = c < 0.0 ? c + 4.0 : c;
                static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (bayer[(int)c.y * 4 + (int)c.x] + 0.5) / 16.0;
            }

            // 안개 불투명도(0~1).
            float SampleHaze(float2 _positionWS)
            {
                float ppu = StageAmbiencePixelsPerUnit();
                float2 texel = floor(_positionWS * ppu);
                float2 posWS = (texel + 0.5) / ppu;

                float t = _Time.y;
                float2 dir = _HazeDrift.xy;
                float dirLen = length(dir);
                dir = dirLen > 0.0001 ? dir / dirLen : float2(1.0, 0.0);
                dir.y *= 2.0;

                // 아이소메트릭 바닥에 눕힌 모양이 되도록 세로를 두 배로 늘려 계산한다.
                float2 ground = float2(posWS.x, posWS.y * 2.0);
                float2 q = (ground - dir * (_HazeDrift.z * t)) / max(_HazeScale, 0.5);
                // 천천히 일렁이는 두 번째 노이즈로 좌표를 비틀어 뭉게구름처럼 모양이 바뀌게 한다.
                float evolve = t * _HazeEvolve;
                float2 warp = float2(HazeFbm(q * 0.6 + float2(0.0, evolve)),
                                     HazeFbm(q * 0.6 + float2(5.2, 1.3 - evolve)));

                float n = HazeFbm(q + (warp - 0.5) * _HazeWarp);
                float m = smoothstep(_HazeCoverage, _HazeCoverage + _HazeSoftness, n);

                if (_HazeSteps >= 1.0)
                {
                    m = saturate(floor(m * _HazeSteps + HazeBayer4(texel)) / _HazeSteps);
                }

                return m * _HazeMaxAlpha;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float haze = SampleHaze(input.hazePositionWS);
                if (haze <= 0.0)
                {
                    discard;
                }

                half4 main = half4(_HazeColor.rgb, (half)haze) * input.color;
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                const half3 normalTS = half3(0.0h, 0.0h, 1.0h);

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
