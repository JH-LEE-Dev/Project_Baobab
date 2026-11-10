#ifndef TREE_HEAT_CHARGE_INCLUDED
#define TREE_HEAT_CHARGE_INCLUDED

// 나무 열기 차오름. 열기를 내뿜기로 한 나무가 카운트다운 동안 받는 진행도(_HeatCharge, 0~1, 나무 렌더러마다
// MaterialPropertyBlock으로 넣는다)만큼 뜨거운 픽셀(빨강이 강한 줄기·불씨)이 점점 밝아지며 달군 빛 색을 띤다.
// 나무 셰이더(Custom-Sprite-Default-Tree)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지·로그라이크는 _TreeHeatChargeA.x가 0이라
// 진행도가 들어와도 아무것도 바꾸지 않는다.
// _TreeHeatChargeA : 켜짐(0/1), 뜨거운 픽셀 밝기 증가량, 뜨거운 픽셀에 더하는 빛 양, 차오르는 곡선(거듭제곱)
// _TreeHeatChargeB : 뜨거운 픽셀 최소 빨강, 빨강이 초록·파랑보다 큰 최소 차
// _TreeHeatChargeColor : 뜨거운 픽셀에 더하는 빛 색(선형)
float4 _TreeHeatChargeA;
float4 _TreeHeatChargeB;
float4 _TreeHeatChargeColor;

// _color: 조명까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표, _charge: 이 나무의 진행도(0~1)
half3 ApplyTreeHeatCharge(half3 _color, float2 _uv, float _charge)
{
    if (_TreeHeatChargeA.x < 0.5 || _charge <= 0.0)
    {
        return _color;
    }

    float level = pow(saturate(_charge), max(_TreeHeatChargeA.w, 0.1));

    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
    // 판정용이라 근사로 충분하다.
    tex = pow(max(tex, 0.0h), 1.0h / 2.2h);
#endif
    bool isHot = tex.r >= (half)_TreeHeatChargeB.x && tex.r - max(tex.g, tex.b) >= (half)_TreeHeatChargeB.y;

    // 뜨거운 픽셀만 밝히고 달군 빛 색을 더한다(어두운 나무 껍질에 빛을 더하면 그림 전체가 뿌옇게 바래 보여서 줄기만 칠한다).
    if (false == isHot)
    {
        return _color;
    }

    half3 color = _color * (half)(1.0 + _TreeHeatChargeA.y * level);
    color += (half3)(_TreeHeatChargeColor.rgb * (_TreeHeatChargeA.z * level));
    return color;
}

#endif // TREE_HEAT_CHARGE_INCLUDED
