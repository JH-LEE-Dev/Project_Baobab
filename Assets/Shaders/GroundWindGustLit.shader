// URP 2D Sprite-Lit-Default와 같은 바닥 타일 셰이더에 돌풍(WindGust.hlsl)에 반응하는 잔디만 더한 것.
// 돌풍이 켜진 스테이지에서만 TileMapGenerator가 바닥 타일맵 머티리얼을 이걸로 바꿔 끼우고, 나머지 스테이지는
// 원래 머티리얼을 그대로 쓴다. 돌풍 전역값이 꺼져 있으면 Sprite-Lit-Default와 같은 결과를 그린다.
// 돌풍 덩어리가 지나가면 그 안의 풀잎(세로 3텍셀)이 제각각의 순간에 바람 쪽으로 1텍셀 눕고, 누운 동안 조금 밝아진다.
// 새 색을 칠하지 않고 타일 그림 자체를 텍셀 단위로 옮기므로 픽셀아트 그대로다.
Shader "Custom/2D/GroundWindGustLit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

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
            #include "Include/WindGust.hlsl"

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
                float2 gustPositionWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.gustPositionWS = TransformObjectToWorld(input.positionOS).xy;

                return o;
            }

            // 초록 풀 픽셀인가(흙·꽃·돌·절벽면 제외). 선형 색 기준.
            bool IsGrassColor(half3 _c)
            {
                return _c.g > _c.r * 1.25 && _c.g > _c.b * 1.6 && _c.g > 0.2;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float2 sampleUV = input.uv;
                bool bent = false;

                if (IsWindGustEnabled())
                {
                    float2 texelCenterWS = StageAmbienceTexelCenterWS(input.gustPositionWS, input.uv, _MainTex_TexelSize);
                    float gust = SampleWindGustGrass(texelCenterWS);
                    if (gust > 0.0)
                    {
                        float2 texelId = floor(texelCenterWS * StageAmbiencePixelsPerUnit());

                        // 풀잎 = 세로 3텍셀 한 줄. 줄마다 세로 마디를 어긋나게 해서 가로로 줄지어 보이지 않게 한다.
                        float columnHash = WindGustHash21(float2(texelId.x, 17.3));
                        float bladeRow = floor((texelId.y + floor(columnHash * 3.0)) / 3.0);
                        float bladeHash = WindGustHash21(float2(texelId.x, bladeRow) + 31.7);

                        // 풀잎마다 눕기 시작하는 돌풍 세기가 달라, 돌풍이 다가오면 하나둘씩 눕고 지나가면 하나둘씩 일어선다.
                        float bendStart = _WindGustD.y + _WindGustD.z * bladeHash;
                        if (gust > bendStart)
                        {
                            // 누운 풀잎은 바람 위쪽 이웃 텍셀 그림을 가져와 1텍셀 바람 쪽으로 밀린 것처럼 보인다.
                            float2 centerUV = StageAmbienceTexelCenterUV(input.uv, _MainTex_TexelSize);
                            float windSide = _WindGustA.x >= 0.0 ? 1.0 : -1.0;
                            float2 sourceUV = centerUV - float2(_MainTex_TexelSize.x * windSide, 0.0);
                            half4 destination = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, centerUV);
                            half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUV);
                            bool sourceOk = source.a < 0.5h || IsGrassColor(source.rgb);
                            bool destinationOk = destination.a < 0.5h || IsGrassColor(destination.rgb);
                            if (sourceOk && destinationOk)
                            {
                                sampleUV = sourceUV;
                                bent = true;
                            }
                        }
                    }
                }

                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                // 누운 풀잎은 빛을 받는 면이 보여 조금 밝아진다(풀색 자체의 무늬는 그대로).
                if (bent && main.a > 0.5h && IsGrassColor(main.rgb))
                {
                    main.rgb *= 1.0h + (half)_WindGustC.x;
                }

                // 이하 Lit2DCommon.CommonLitFragment와 같다(albedo만 위에서 바꾼 값을 쓴다).
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

                return CombinedShapeLightShared(surfaceData, inputData);
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
