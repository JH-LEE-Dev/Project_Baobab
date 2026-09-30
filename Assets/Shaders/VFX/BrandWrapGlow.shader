Shader "Custom/VFX/BrandWrapGlow"
{
    Properties
    {
        _GlowMin ("Glow Min (HDR 발광 최소 배율, 정점 알파 0)", Float) = 1.0
        _GlowMax ("Glow Max (HDR 발광 최대 배율, 정점 알파 1)", Float) = 2.8
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
        ZTest Always
        Cull Off

        Pass
        {
            Name "BrandWrapGlowPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _GlowMin;
                float _GlowMax;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            // 색은 정점 색(LDR)에, 발광 세기(0~1)는 정점 알파에 실려 온다. 알파는 투명도가 아니라 HDR 배율을 정하는 값이라
            // 출력 알파는 항상 1이다(도트는 반투명 없이 또렷하게). 밝은 별만 블룸 임계값을 넘어 번쩍인다.
            half4 frag(Varyings input) : SV_Target
            {
                float glow = lerp(_GlowMin, _GlowMax, saturate(input.color.a));
                return half4(input.color.rgb * (half)glow, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
