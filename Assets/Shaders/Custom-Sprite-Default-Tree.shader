Shader "Custom/Custom-Sprite-Default-Tree"
{
     Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1

        _FlashAmount("Flash Amount", Range(0,1)) = 0
        _FlashColor("Flash Color", Color) = (1,1,1,1)
        // 열기 차오름 진행도(0~1). 카운트다운 중인 나무에만 MaterialPropertyBlock으로 넣는다.
        [HideInInspector] _HeatCharge("Heat Charge", Float) = 0
        // 깎인 정도(0~1). 4스테이지에서 깎인 나무에만 MaterialPropertyBlock으로 넣는다(금이 가며 빛이 샌다).
        [HideInInspector] _DamageLook("Damage Look", Float) = 0
        // 배리어가 깨진 직후의 잔광 세기(0~1). 3스테이지에서 배리어가 깨진 나무에만 MaterialPropertyBlock으로 잠깐 넣는다.
        [HideInInspector] _BarrierAfterglow("Barrier Afterglow", Float) = 0
        // 2스테이지 포자막 상태(x 깎인 정도, y 번쩍임, z 차오르는 중, w 가득 참 맥동). 포자막 렌더러에만 MaterialPropertyBlock으로 넣는다.
        [HideInInspector] _SporeShieldFx("Spore Shield Fx", Vector) = (0,0,0,0)

        [Header(Wind Sway)]
        _EnableWindSway("Enable Wind Sway", Float) = 0
        _SwayPositionAmplitude("Sway Position Amplitude", Float) = 0.03
        _SwayRotationAmplitude("Sway Rotation Amplitude", Float) = 1.25
        _SwayMainSpeed("Sway Main Speed", Float) = 0.55
        _SwayDetailSpeed("Sway Detail Speed", Float) = 1.45
        _SwayDetailWeight("Sway Detail Weight", Float) = 0.35
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Stencil
        {
            Ref 0
            WriteMask 32
            Comp Always
            Pass Replace
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/TreeWindSway.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
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
                // xy: 월드 위치, zw: 그림 기준점 월드 위치(나무 단위 반짝임 박자용)
                float4 worldPos    : TEXCOORD7;
                // 나무 한 그루에 한 번 구한 값(TreeDamageLook.hlsl의 ComputeTreeDamageFx). 정점마다 같아서 보간하지 않는다.
                nointerpolation float4 damageFx : TEXCOORD8;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "Include/SporeGlow.hlsl"
            #include "Include/TreeStarTwinkle.hlsl"
            #include "Include/TreeForestBreath.hlsl"
            #include "Include/TreeHeatCharge.hlsl"
            #include "Include/TreeDamageLook.hlsl"
            #include "Include/TreeBarrierAfterglow.hlsl"
            #include "Include/TreeSporeShield.hlsl"
            #include "Include/StageAmbienceCommon.hlsl"
            #include "Include/LavaGlow.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _EnableWindSway)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayPositionAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayRotationAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayMainSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailWeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _HeatCharge)
                UNITY_DEFINE_INSTANCED_PROP(float, _DamageLook)
                UNITY_DEFINE_INSTANCED_PROP(float, _BarrierAfterglow)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SporeShieldFx)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // Wind Sway 버텍스 변위
                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                input.positionOS = ApplyWindSway(
                    input.positionOS,
                    objectPivotWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EnableWindSway),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayPositionAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayRotationAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayMainSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailWeight)
                );

                Varyings o = CommonLitVertex(input);
                o.worldPos = float4(TransformObjectToWorld(input.positionOS).xy, objectPivotWorldPos.xy);
                o.damageFx = ComputeTreeDamageFx(objectPivotWorldPos.xy, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DamageLook));
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonLitFragment(input, input.color);
                clip(color.a - 0.01);
                // 포자 나무 숨빛(꺼진 스테이지는 그대로)
                color.rgb = ApplySporeGlow(color.rgb, input.uv, worldPos);
                // 나무 별빛 반짝임(꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeStarTwinkle(color.rgb, input.uv, worldPos, input.worldPos.zw);
                // 숲의 숨결 물결이 밑동에 닿으면 나무가 밝아지고 별점이 차례로 반짝인다(물결이 없으면 그대로)
                color.rgb = ApplyTreeForestBreath(color.rgb, input.uv, worldPos, input.worldPos.zw);
                // 열기 차오름(카운트다운 중인 나무만, 꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeHeatCharge(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HeatCharge));
                // 깎인 나무의 금 또는 숯불(4스테이지에서만, 깎이지 않은 나무·꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeDamageLook(color.rgb, input.uv, input.damageFx);
                // 용암 빛(발밑이 용암 가까이면 밑동부터 물든다, 꺼진 스테이지는 그대로)
                color.rgb = ApplyLavaGlowUpright(color.rgb, snappedWorldPos, input.worldPos.w, floor(worldPos * ppu));
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                // 배리어가 깨진 직후의 푸른 테두리 잔광(깨진 나무만 잠깐, 그 밖에는 그대로)
                color.rgb = ApplyTreeBarrierAfterglow(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BarrierAfterglow));
                // 과열이 가득 차는 순간의 열기 물결(물결이 도는 동안만, 그 밖에는 그대로)
                color.rgb = ApplyOverheatBurstFlare(color.rgb, input.damageFx.z);
                color.rgb = lerp(color.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashColor).rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);
                // 2스테이지 포자막 상태(포자막 렌더러만, 값이 없으면 그대로). 번쩍임 위에 얹어 결정이 흰 번쩍임에 묻히지 않게 한다.
                color.rgb = ApplyTreeSporeShield(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SporeShieldFx));
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/TreeWindSway.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
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
                float3  worldPos        : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _EnableWindSway)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayPositionAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayRotationAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayMainSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailWeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _HeatCharge)
                UNITY_DEFINE_INSTANCED_PROP(float, _DamageLook)
                UNITY_DEFINE_INSTANCED_PROP(float, _BarrierAfterglow)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SporeShieldFx)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // Wind Sway 버텍스 변위
                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                input.positionOS = ApplyWindSway(
                    input.positionOS,
                    objectPivotWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EnableWindSway),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayPositionAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayRotationAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayMainSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailWeight)
                );

                Varyings o = CommonNormalsVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonNormalsFragment(input, input.color);
                clip(color.a - 0.01);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/TreeWindSway.hlsl"

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
                // xy: 월드 위치, zw: 그림 기준점 월드 위치(나무 단위 반짝임 박자용)
                float4 worldPos : TEXCOORD7;
                // 나무 한 그루에 한 번 구한 값(TreeDamageLook.hlsl의 ComputeTreeDamageFx). 정점마다 같아서 보간하지 않는다.
                nointerpolation float4 damageFx : TEXCOORD8;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Include/SporeGlow.hlsl"
            #include "Include/TreeStarTwinkle.hlsl"
            #include "Include/TreeForestBreath.hlsl"
            #include "Include/TreeHeatCharge.hlsl"
            #include "Include/TreeDamageLook.hlsl"
            #include "Include/TreeBarrierAfterglow.hlsl"
            #include "Include/TreeSporeShield.hlsl"
            #include "Include/StageAmbienceCommon.hlsl"
            #include "Include/LavaGlow.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _EnableWindSway)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayPositionAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayRotationAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayMainSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailWeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _HeatCharge)
                UNITY_DEFINE_INSTANCED_PROP(float, _DamageLook)
                UNITY_DEFINE_INSTANCED_PROP(float, _BarrierAfterglow)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SporeShieldFx)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // Wind Sway 버텍스 변위
                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                input.positionOS = ApplyWindSway(
                    input.positionOS,
                    objectPivotWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EnableWindSway),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayPositionAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayRotationAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayMainSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailWeight)
                );

                Varyings o = CommonUnlitVertex(input);
                o.worldPos = float4(TransformObjectToWorld(input.positionOS).xy, objectPivotWorldPos.xy);
                o.damageFx = ComputeTreeDamageFx(objectPivotWorldPos.xy, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DamageLook));
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonUnlitFragment(input, input.color);
                // 포자 나무 숨빛(꺼진 스테이지는 그대로)
                color.rgb = ApplySporeGlow(color.rgb, input.uv, worldPos);
                // 나무 별빛 반짝임(꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeStarTwinkle(color.rgb, input.uv, worldPos, input.worldPos.zw);
                // 숲의 숨결 물결이 밑동에 닿으면 나무가 밝아지고 별점이 차례로 반짝인다(물결이 없으면 그대로)
                color.rgb = ApplyTreeForestBreath(color.rgb, input.uv, worldPos, input.worldPos.zw);
                // 열기 차오름(카운트다운 중인 나무만, 꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeHeatCharge(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HeatCharge));
                // 깎인 나무의 금 또는 숯불(4스테이지에서만, 깎이지 않은 나무·꺼진 스테이지는 그대로)
                color.rgb = ApplyTreeDamageLook(color.rgb, input.uv, input.damageFx);
                // 용암 빛(발밑이 용암 가까이면 밑동부터 물든다, 꺼진 스테이지는 그대로)
                color.rgb = ApplyLavaGlowUpright(color.rgb, snappedWorldPos, input.worldPos.w, floor(worldPos * ppu));
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                // 배리어가 깨진 직후의 푸른 테두리 잔광(깨진 나무만 잠깐, 그 밖에는 그대로)
                color.rgb = ApplyTreeBarrierAfterglow(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BarrierAfterglow));
                // 과열이 가득 차는 순간의 열기 물결(물결이 도는 동안만, 그 밖에는 그대로)
                color.rgb = ApplyOverheatBurstFlare(color.rgb, input.damageFx.z);
                color.rgb = lerp(color.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashColor).rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);
                // 2스테이지 포자막 상태(포자막 렌더러만, 값이 없으면 그대로). 번쩍임 위에 얹어 결정이 흰 번쩍임에 묻히지 않게 한다.
                color.rgb = ApplyTreeSporeShield(color.rgb, input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SporeShieldFx));
                return color;
            }
            ENDHLSL
        }
    }
}
