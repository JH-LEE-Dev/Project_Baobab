Shader "ProjectBaobab/Rendering/HighResolutionBloom"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #pragma vertex Vert
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _BloomParams;
        TEXTURE2D_X(_BloomTexture);

        half3 ApplyThreshold(half3 color)
        {
            half brightness = max(max(color.r, color.g), color.b);
            half threshold = _BloomParams.x;
            half knee = max(_BloomParams.y, 0.0001h);
            half soft = saturate((brightness - threshold + knee) / (2.0h * knee));
            soft = soft * soft * knee;
            half contribution = max(brightness - threshold, soft) / max(brightness, 0.0001h);
            return color * contribution;
        }

        half3 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        half3 Blur9(float2 uv, float2 direction)
        {
            float radius = _BloomParams.w;
            float2 texel = _BlitTexture_TexelSize.xy * direction * radius;

            half3 color = SampleSource(uv) * 0.2270270270h;
            color += SampleSource(uv + texel * 1.3846153846) * 0.3162162162h;
            color += SampleSource(uv - texel * 1.3846153846) * 0.3162162162h;
            color += SampleSource(uv + texel * 3.2307692308) * 0.0702702703h;
            color += SampleSource(uv - texel * 3.2307692308) * 0.0702702703h;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Threshold"

            HLSLPROGRAM
            #pragma fragment FragThreshold

            half4 FragThreshold(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(ApplyThreshold(SampleSource(input.texcoord.xy)), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur Horizontal"

            HLSLPROGRAM
            #pragma fragment FragBlurHorizontal

            half4 FragBlurHorizontal(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Blur9(input.texcoord.xy, float2(1.0, 0.0)), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vertical"

            HLSLPROGRAM
            #pragma fragment FragBlurVertical

            half4 FragBlurVertical(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Blur9(input.texcoord.xy, float2(0.0, 1.0)), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma fragment FragComposite

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, sampler_LinearClamp, uv).rgb;
                source.rgb += bloom * _BloomParams.z;
                return source;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
