Shader "Custom/ShockWaveArc"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        _MinRadius("Min Radius", Range(0, 1)) = 0.3
        _MaxRadius("Max Radius", Range(0, 1)) = 0.5
        _Angle("Angle", Range(0, 180)) = 120
        _AngleEdgeFade("Angle Edge Fade", Range(0.001, 0.5)) = 0.08
        _AttackDir("Attack Direction", Vector) = (1, 0, 0, 0)
        _Alpha("Alpha", Range(0, 1)) = 1
        _TrailAlpha("Trail Alpha", Range(0, 1)) = 0.45
        _TrailOffset("Trail Offset", Range(0, 0.5)) = 0.12
        _TrailThickness("Trail Thickness", Range(0.001, 0.08)) = 0.018
        _TrailNoise("Trail Noise", Range(0, 0.25)) = 0.08
        _TrailFrequency("Trail Frequency", Range(1, 32)) = 12
        _TrailTime("Trail Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _MinRadius;
                float _MaxRadius;
                float _Angle;
                float _AngleEdgeFade;
                float4 _AttackDir;
                float _Alpha;
                float _TrailAlpha;
                float _TrailOffset;
                float _TrailThickness;
                float _TrailNoise;
                float _TrailFrequency;
                float _TrailTime;
            CBUFFER_END

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), u);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float2 centered = IN.uv * 2.0 - 1.0;
                float radius = length(centered);
                float2 dir = radius > 0.0001 ? centered / radius : float2(1.0, 0.0);

                float halfAngle = radians(_Angle * 0.5);
                float cosThreshold = cos(halfAngle);
                float angleDot = dot(dir, normalize(_AttackDir.xy));
                float angleMask = smoothstep(cosThreshold, cosThreshold + _AngleEdgeFade, angleDot);

                float angleCoord = atan2(dir.y, dir.x);
                float noiseA = Noise1D(angleCoord * _TrailFrequency + _TrailTime * 8.0);
                float noiseB = Noise1D(angleCoord * (_TrailFrequency * 0.43) - _TrailTime * 5.0 + 17.0);
                float jagged = (noiseA * 0.7 + noiseB * 0.3) * 2.0 - 1.0;
                float noisyInnerRadius = saturate(_MinRadius - _TrailOffset + jagged * _TrailNoise);
                float innerMask = smoothstep(noisyInnerRadius, noisyInnerRadius + _TrailThickness, radius);
                float radiusMask = innerMask * step(radius, _MaxRadius);

                float trailBand = 1.0 - smoothstep(_TrailThickness, _TrailThickness * 2.5, abs(radius - noisyInnerRadius));
                float trailBehindFront = step(radius, noisyInnerRadius);
                float trailMask = trailBand * trailBehindFront * angleMask * _TrailAlpha;

                half4 color = tex * IN.color;
                color.a *= saturate(angleMask * radiusMask + trailMask) * _Alpha;

                clip(color.a - 0.005);
                return color;
            }
            ENDHLSL
        }
    }
}
