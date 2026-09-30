Shader "Custom/OverheatShockWaveArc"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [HDR] _Blue("Flame Blue", Color) = (0.08, 0.45, 1.5, 1)
        [HDR] _Cyan("Edge Cyan", Color) = (0.12, 1.75, 2.1, 1)
        [HDR] _Violet("Violet Accent", Color) = (0.85, 0.12, 1.25, 1)
        [HDR] _Ember("Ember Accent", Color) = (1.55, 0.14, 0.025, 1)
        _MinRadius("Min Radius", Range(0, 1)) = 0.3
        _MaxRadius("Max Radius", Range(0, 1)) = 0.5
        _Angle("Angle", Range(0, 180)) = 90
        _AngleEdgeFade("Angle Edge Fade", Range(0.001, 0.5)) = 0.04
        _AttackDir("Attack Direction", Vector) = (1, 0, 0, 0)
        _Alpha("Alpha", Range(0, 1)) = 1
        _TrailTime("Trail Time", Float) = 0
        _TrailSeed("Trail Seed", Float) = 0
        _FlameDepth("Flame Depth", Range(0.01, 0.3)) = 0.12
        _FlameFrequency("Flame Frequency", Range(2, 40)) = 19
        _RimWidth("Rim Width", Range(0.002, 0.15)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Blue;
                float4 _Cyan;
                float4 _Violet;
                float4 _Ember;
                float _MinRadius;
                float _MaxRadius;
                float _Angle;
                float _AngleEdgeFade;
                float4 _AttackDir;
                float _Alpha;
                float _TrailTime;
                float _TrailSeed;
                float _FlameDepth;
                float _FlameFrequency;
                float _RimWidth;
            CBUFFER_END

            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), f);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half textureAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float2 direction = radius > 0.0001 ? centered / radius : float2(1, 0);
                float2 attackDirection = normalize(_AttackDir.xy);
                float angleDot = dot(direction, attackDirection);
                float threshold = cos(radians(_Angle * 0.5));
                float angleMask = smoothstep(threshold, threshold + _AngleEdgeFade, angleDot);

                float side = dot(direction, float2(-attackDirection.y, attackDirection.x));
                float angularPosition = atan2(side, angleDot);
                float broadNoise = Noise1D(angularPosition * _FlameFrequency + _TrailSeed * 7.3 - _TrailTime * 6.0);
                float sharpNoise = abs(sin(angularPosition * (_FlameFrequency * 1.9) + _TrailSeed * 3.1 + _TrailTime * 9.0));
                float flame = broadNoise * 0.65 + sharpNoise * 0.35;
                float innerEdge = _MinRadius - flame * _FlameDepth;
                float innerMask = smoothstep(innerEdge, innerEdge + 0.018, radius);
                float outerMask = 1.0 - smoothstep(_MaxRadius - 0.012, _MaxRadius + 0.008, radius);
                float bodyMask = innerMask * outerMask * angleMask * textureAlpha;

                float frontRim = smoothstep(_MaxRadius - _RimWidth, _MaxRadius, radius);
                float flameTips = smoothstep(0.58, 0.96, flame) * (1.0 - smoothstep(innerEdge, innerEdge + _FlameDepth * 0.9, radius));
                float violetStreak = smoothstep(0.78, 0.96, Noise1D(angularPosition * 31.0 + _TrailSeed * 13.0)) * smoothstep(innerEdge, _MaxRadius, radius);
                float emberNoise = Noise1D(angularPosition * 23.0 - _TrailTime * 4.0 + _TrailSeed * 19.0);
                float innerFlameBand = 1.0 - smoothstep(innerEdge + 0.012, innerEdge + _FlameDepth * 0.55, radius);
                float emberStreak = smoothstep(0.86, 0.98, emberNoise) * innerFlameBand * (0.4 + flameTips * 0.6);
                float bodyGradient = saturate((radius - innerEdge) / max(_MaxRadius - innerEdge, 0.001));

                half3 color = lerp(_Blue.rgb, _Cyan.rgb, bodyGradient * 0.72 + frontRim * 0.28);
                color = lerp(color, _Violet.rgb, violetStreak * 0.42);
                color = lerp(color, _Ember.rgb, emberStreak * 0.38);
                color += _Cyan.rgb * (frontRim * 0.28 + flameTips * 0.2);
                half alpha = saturate(bodyMask * (0.62 + frontRim * 0.18 + flameTips * 0.18)) * _Alpha * input.color.a;
                clip(alpha - 0.004);
                return half4(color * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
