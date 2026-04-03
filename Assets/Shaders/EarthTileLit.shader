Shader "Custom/2D/EarthTileLit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
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

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 albedo = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (albedo.a <= 0.0001h)
                {
                    discard;
                }

                float3 tileNormalTS = normalize(float3(0.0, 1.0, 0.5));

                float3 directional = _GlobalDirectionalLightDirection.xyz;
                if (dot(directional, directional) <= 0.0001)
                {
                    directional = float3(0.0, 1.0, 1.0);
                }
                directional = normalize(directional);

                float ndl = saturate(dot(tileNormalTS, directional));
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
                    float ellipseYScale = max(_GlobalPointLightShape[i].x, 0.1);

                    float2 toLightXY = lightPositionWS.xy - input.positionWS.xy;
                    float2 attenuatedOffset = float2(toLightXY.x, toLightXY.y * ellipseYScale);
                    float distanceXY = length(attenuatedOffset);
                    float radial01 = saturate((distanceXY - innerRadius) / max(outerRadius - innerRadius, 0.0001));
                    float attenuation = 1.0 - (radial01 * radial01 * (3.0 - (2.0 * radial01)));
                    attenuation *= attenuation;

                    pointLighting += pointColor * (pointIntensity * attenuation);
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
