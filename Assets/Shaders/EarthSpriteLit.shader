Shader "Custom/2D/EarthSpriteLit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            float4 _GlobalDirectionalLightDirection;
            float4 _GlobalDirectionalLightColor;
            float4 _GlobalAmbientLightColor;
            float _GlobalDirectionalLightIntensity;
            float _GlobalAmbientLightIntensity;
            float _GlobalPointLightCount;
            float4 _GlobalPointLightPositions[8];
            float4 _GlobalPointLightColors[8];
            float4 _GlobalPointLightParams[8];
            float4 _GlobalPointLightShape[8];

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half3 DecodeSpriteNormalTS(half4 packedNormal)
            {
                half3 normalTS = UnpackNormal(packedNormal);
                normalTS.xy *= sign(unity_SpriteProps.xy);
                return normalize(normalTS);
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 albedo = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (albedo.a <= 0.0001h)
                {
                    discard;
                }

                half4 packedNormal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = DecodeSpriteNormalTS(packedNormal);

                // Treat a missing/invalid normal map as unlit black, matching the intended workflow.
                if (dot(normalTS, normalTS) <= 0.0001h)
                {
                    return half4(0.0h, 0.0h, 0.0h, albedo.a);
                }

                half3 lightDirTS = _GlobalDirectionalLightDirection.xyz;
                if (dot(lightDirTS, lightDirTS) <= 0.0001h)
                {
                    lightDirTS = half3(0.0h, 0.0h, 1.0h);
                }
                lightDirTS = normalize(lightDirTS);

                half ndl = saturate(dot(normalTS, lightDirTS));
                float3 pointLighting = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= (int)_GlobalPointLightCount)
                    {
                        break;
                    }

                    float3 lightPositionWS = _GlobalPointLightPositions[i].xyz;
                    float outerRadius = max(_GlobalPointLightPositions[i].w, 0.0001);
                    float3 pointColor = _GlobalPointLightColors[i].rgb;
                    float pointIntensity = _GlobalPointLightParams[i].x;
                    float innerRadius = saturate(_GlobalPointLightParams[i].y) * outerRadius;
                    float height = max(_GlobalPointLightParams[i].z, 0.0001);
                    float ellipseYScale = max(_GlobalPointLightShape[i].x, 0.1);
                    float normalInfluence = saturate(_GlobalPointLightShape[i].y);
                    float verticalBias = max(_GlobalPointLightShape[i].z, 0.0);

                    float2 toLightXY = lightPositionWS.xy - input.positionWS.xy;
                    float2 attenuatedOffset = float2(toLightXY.x, toLightXY.y * ellipseYScale);
                    float distanceXY = length(attenuatedOffset);
                    float radial01 = saturate((distanceXY - innerRadius) / max(outerRadius - innerRadius, 0.0001));
                    float attenuation = 1.0 - (radial01 * radial01 * (3.0 - (2.0 * radial01)));
                    attenuation *= attenuation;

                    if (attenuation <= 0.0)
                    {
                        continue;
                    }

                    float3 pointLightDirTS = normalize(float3(attenuatedOffset.xy, height + verticalBias));
                    float pointNdotL = dot(normalTS, pointLightDirTS);
                    float pointNormalResponse = saturate((pointNdotL + normalInfluence) / (1.0 + normalInfluence));

                    pointLighting += pointColor * (pointIntensity * attenuation * pointNormalResponse);
                }

                float3 ambient = _GlobalAmbientLightColor.rgb * _GlobalAmbientLightIntensity;
                float3 direct = _GlobalDirectionalLightColor.rgb * (_GlobalDirectionalLightIntensity * ndl);
                float3 lighting = saturate(ambient + direct + pointLighting);

                return half4(albedo.rgb * lighting, albedo.a);
            }
            ENDHLSL
        }
    }
}
