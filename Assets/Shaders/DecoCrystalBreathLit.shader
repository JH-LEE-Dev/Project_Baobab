// URP 2D Sprite-Lit-Default와 같은 바닥 장식 타일 셰이더에 결정 숨쉬기만 더한 것.
// 결정 숨쉬기가 켜진 스테이지(2스테이지)에서만 TileMapGenerator가 바닥 장식 타일맵 머티리얼을 이걸로 바꿔 끼우고,
// 나머지 스테이지는 원래 머티리얼을 그대로 쓴다. 결정이 아닌 픽셀(돌·웅덩이·그림자)은 Sprite-Lit-Default와 같은 결과다.
// 장식 타일 하나하나가 제 박자로 몇 초에 한 번 결정만 은은하게 밝아졌다 돌아온다(가장 밝을 때 블룸 문턱을 살짝 넘는다).
// 박자는 타일 자리로 정해지므로 따로 기억할 것이 없다. 렌더링만 바뀌고 게임 동작에는 영향이 없다.
Shader "Custom/2D/DecoCrystalBreathLit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Crystal Breath)]
        // 가장 밝을 때 결정이 원래 밝기의 1 + 이 값 배가 된다.
        _CrystalBreathAmount("Amount", Float) = 0.8
        // 한 번 숨쉬는 시간(초, 타일마다 이 범위 안에서 정해진다)
        _CrystalBreathPeriodMin("Period Min", Float) = 4
        _CrystalBreathPeriodMax("Period Max", Float) = 7
        // 클수록 밝아져 있는 순간이 짧다(1이면 사인 그대로)
        _CrystalBreathSharpness("Sharpness", Float) = 3

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
        ZWrite [_ZWrite]

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
            #pragma multi_compile _ SKINNED_SPRITE

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
                // 타일 그림 한 장 안에서 늘 같은 값(그림 왼쪽 아래 모서리 자리). 타일마다 박자를 나누는 데 쓴다.
                float2 tileAnchorWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _CrystalBreathAmount;
                float _CrystalBreathPeriodMin;
                float _CrystalBreathPeriodMax;
                float _CrystalBreathSharpness;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                // 타일 그림은 회전·확대 없이 텍스처 픽셀 하나가 월드 1/PPU라, 월드 위치에서 UV만큼 되돌리면 그림 한 장 안에서 같은 값이 된다.
                float2 positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.tileAnchorWS = positionWS - input.uv * _MainTex_TexelSize.zw / StageAmbiencePixelsPerUnit();

                return o;
            }

            float CrystalBreathHash(float2 _p, float _salt)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031 + _salt);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 결정 픽셀이면 1, 아니면 0(sRGB 0~1로 판정).
            // 2스테이지 결정 장식은 하늘색·민트(초록이 빨강보다 0.14 이상 높다)와 꼭대기 흰 점이고,
            // 돌(회색)·웅덩이(초록-빨강 0.094)·그림자(검정)는 결정이 아니다.
            half CrystalBreathMask(half3 _srgb)
            {
                half cyan = smoothstep(0.105h, 0.135h, _srgb.g - _srgb.r);
                half tip = step(0.9h, min(_srgb.r, min(_srgb.g, _srgb.b))) * step(_srgb.r, _srgb.g + 0.005h) * step(_srgb.r, _srgb.b + 0.005h);
                return max(cyan, tip);
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 main = input.color * texel;

                // 이하 Lit2DCommon.CommonLitFragment와 같다.
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif

                half4 result = CombinedShapeLightShared(surfaceData, inputData);

                // 결정 숨쉬기: 조명이 끝난 색에 밝기만 곱한다(결정이 아닌 픽셀은 그대로).
                if (_CrystalBreathAmount > 0.0 && texel.a > 0.5h)
                {
                    half3 srgb = texel.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    // 판정용이라 근사로 충분하다.
                    srgb = pow(max(srgb, 0.0h), 1.0h / 2.2h);
                #endif
                    half crystal = CrystalBreathMask(srgb);
                    if (crystal > 0.0h)
                    {
                        // 타일 자리(텍셀 단위로 반올림해 그림 한 장이 한 값)로 박자와 시작점을 정한다.
                        float2 tileId = floor(input.tileAnchorWS * StageAmbiencePixelsPerUnit() + 0.5);
                        float minPeriod = max(0.5, min(_CrystalBreathPeriodMin, _CrystalBreathPeriodMax));
                        float maxPeriod = max(minPeriod, max(_CrystalBreathPeriodMin, _CrystalBreathPeriodMax));
                        float period = lerp(minPeriod, maxPeriod, CrystalBreathHash(tileId, 0.29));
                        float phase = (_Time.y / period + CrystalBreathHash(tileId, 0.71)) * 6.2831853;
                        // 사인의 윗부분만 써서 대부분은 원래 밝기, 박자마다 한 번 부드럽게 밝아진다.
                        float glow = pow(max(0.0, sin(phase)), max(1.0, _CrystalBreathSharpness));
                        result.rgb *= (half)(1.0 + _CrystalBreathAmount * glow * crystal);
                    }
                }

                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                float _CrystalBreathAmount;
                float _CrystalBreathPeriodMin;
                float _CrystalBreathPeriodMax;
                float _CrystalBreathSharpness;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _CrystalBreathAmount;
                float _CrystalBreathPeriodMin;
                float _CrystalBreathPeriodMax;
                float _CrystalBreathSharpness;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
