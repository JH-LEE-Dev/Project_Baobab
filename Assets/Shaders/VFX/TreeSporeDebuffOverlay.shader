Shader "Custom/VFX/TreeSporeDebuffOverlay"
{
    // 포자 디버프 나무 오버레이(구운 얼룩·수액 스프라이트). 라이팅 없이 그리되 나무 본체(Custom-Sprite-Default-Tree)와 같은
    // 바람 흔들림(ApplyWindSway)과 픽셀 격자 UV 스냅을 써서 흔들리는 나무 그림에 픽셀 단위로 붙어 있다.
    // 오버레이는 나무 Top 렌더러의 자식으로 같은 자리·같은 피벗이라 오브젝트 위치(위상)가 같다.
    // 흔들림 값은 TreeSporeDebuffVfxManager가 나무 Top 렌더러에서 읽어 MaterialPropertyBlock으로 넣는다.
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        [Header(Wind Sway)]
        _EnableWindSway("Enable Wind Sway", Float) = 1
        _SwayPositionAmplitude("Sway Position Amplitude", Float) = 0.03
        _SwayRotationAmplitude("Sway Rotation Amplitude", Float) = 1.25
        _SwayMainSpeed("Sway Main Speed", Float) = 0.55
        _SwayDetailSpeed("Sway Detail Speed", Float) = 1.45
        _SwayDetailWeight("Sway Detail Weight", Float) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "../Include/TreeWindSway.hlsl"

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
                float2 worldPos : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU 인스턴싱(재질에서 켬). 나무 본체 셰이더와 같은 구성이다.
            // 인스턴싱이 꺼져 있으면 같은 오버레이 그림을 쓰는 렌더러들이 스프라이트 묶음 그리기로 합쳐지면서 정점이 월드 좌표로 미리 옮겨지고
            // UNITY_MATRIX_M이 단위 행렬이 된다. 그러면 흔들림 피벗이 나무 밑동이 아니라 월드 원점이 되어(위상도 원점 기준으로 모두 같아짐)
            // 원점에서 먼 나무일수록 오버레이가 나무에서 크게 떨어져 그려졌다. 인스턴싱으로 그리면 렌더러마다 자기 행렬을 그대로 쓴다.
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _EnableWindSway)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayPositionAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayRotationAmplitude)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayMainSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayDetailWeight)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                // 나무 본체와 같은 식(같은 피벗 위상, 같은 돌풍·지진)
                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                input.positionOS = ApplyWindSway(
                    input.positionOS,
                    objectPivotWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EnableWindSway),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayPositionAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayRotationAmplitude),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayMainSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayDetailWeight));

                Varyings o = CommonUnlitVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // 나무 본체와 같은 픽셀 격자 UV 스냅(흔들려 비스듬해진 그림도 격자 픽셀로 읽어 나무 픽셀과 맞춘다)
                float ppu = 32.0;
                float2 worldPos = input.worldPos;
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

                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
