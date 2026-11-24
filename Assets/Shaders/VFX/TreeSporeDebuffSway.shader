Shader "Custom/VFX/TreeSporeDebuffSway"
{
    // 포자 디버프 상단 메쉬(나무에 붙은 수액 빛·맺힌 방울·표식 등 픽셀 쿼드). 정점 색 그대로 라이팅 없이 그리고(SpriteGlow와 같은 블렌드),
    // 쿼드마다 붙은 나무 피벗으로 나무 본체와 같은 바람 흔들림(ApplyWindSway)을 계산해 쿼드를 통째로 옮긴다.
    // 옮기는 양은 쿼드 가운데에서 구해 픽셀 격자로 반올림하므로 쿼드가 비스듬해지지 않고 나무 그림 픽셀과 같은 칸으로 움직인다.
    //
    // 정점 데이터(TreeSporeDebuffSwayBuffer)
    //  TEXCOORD0 : xy = 쿼드 가운데(월드, 흔들기 전), zw = 나무 Top 렌더러 위치 xy(흔들림 위상·회전 중심)
    //  TEXCOORD1 : x = 나무 Top 렌더러 위치 z(지진 물결 탑승 값), y = 흔들기 방식(0 = 고정, 1 = 지진만, 2 = 바람 + 지진), zw = Top 렌더러 월드 배율
    Properties
    {
        _Glow("Glow (HDR 발광 배율, 1 = 그대로)", Float) = 1.0

        [Header(Wind Sway)]
        _SwayPositionAmplitude("Sway Position Amplitude", Float) = 0.03
        _SwayRotationAmplitude("Sway Rotation Amplitude", Float) = 1.25
        _SwayMainSpeed("Sway Main Speed", Float) = 0.55
        _SwayDetailSpeed("Sway Detail Speed", Float) = 1.45
        _SwayDetailWeight("Sway Detail Weight", Float) = 0.35
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "../Include/TreeWindSway.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float _Glow;
        float _SwayPositionAmplitude;
        float _SwayRotationAmplitude;
        float _SwayMainSpeed;
        float _SwayDetailSpeed;
        float _SwayDetailWeight;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float4 color : COLOR;
        float4 swayA : TEXCOORD0;
        float4 swayB : TEXCOORD1;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float4 color : COLOR;
    };

    Varyings vert(Attributes input)
    {
        Varyings output = (Varyings)0;
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

        float mode = input.swayB.y;
        if (mode > 0.5)
        {
            float2 center = input.swayA.xy;
            float3 pivot = float3(input.swayA.zw, input.swayB.x);
            float2 scale = input.swayB.zw;
            float2 safeScale = float2(abs(scale.x) > 1e-4 ? scale.x : 1.0, abs(scale.y) > 1e-4 ? scale.y : 1.0);

            // 나무 본체가 오브젝트 공간에서 돌리는 것과 같게, 쿼드 가운데를 Top 렌더러 오브젝트 공간으로 옮겨 흔든 뒤 되돌린다.
            float3 centerOS = float3((center - pivot.xy) / safeScale, 0.0);
            float3 swayedOS = ApplyWindSway(centerOS, pivot, mode > 1.5 ? 1.0 : 0.0, _SwayPositionAmplitude,
                _SwayRotationAmplitude, _SwayMainSpeed, _SwayDetailSpeed, _SwayDetailWeight);
            float2 delta = swayedOS.xy * safeScale + pivot.xy - center;
            positionWS.xy += round(delta * 32.0) / 32.0;
        }

        output.positionHCS = TransformWorldToHClip(positionWS);
        output.color = input.color;
        return output;
    }

    half4 frag(Varyings input) : SV_Target
    {
        half4 c = input.color;
        c.rgb *= (half)_Glow;
        return c;
    }
    ENDHLSL

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
            Name "TreeSporeDebuffSway2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            ENDHLSL
        }

        Pass
        {
            Name "TreeSporeDebuffSwayForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
