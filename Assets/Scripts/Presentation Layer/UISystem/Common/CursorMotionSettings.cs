using System;
using DG.Tweening;
using UnityEngine;

[Serializable]
public class CursorMotionSettings
{
    [Header("Show Motion Settings")]
    public bool enableShowMotion = true;
    public float showDuration = 0.7f;
    public float shrinkSizeScale = 0.8f;
    [Range(0f, 1f)] public float shrinkTimeRatio = 0.08f;
    [Range(0f, 1f)] public float restoreTimeRatio = 0.12f;
    public Ease sizeRestoreEase = Ease.OutBack;
    public float startAngle = 20f;
    public float angleDamping = 0.62f;
    public int swingCount = 5;
    [Range(0f, 1f)] public float rotationTimeRatio = 0.8f;
    public Ease rotationEase = Ease.OutSine;

    [Header("Idle Motion Settings")]
    public bool enableIdleMotion = true;
    public float idleCycleDuration = 3f;
    public float idleSizeOffset = 1f;

    [Header("Hide Motion Settings")]
    public bool enableHideMotion = true;
    public float hideDuration = 0.15f;
    public float hideExpandOffset = 10f;
    public Ease hideEase = Ease.OutQuad;

    public CursorMotionSettings Clone()
    {
        return new CursorMotionSettings
        {
            enableShowMotion = this.enableShowMotion,
            showDuration = this.showDuration,
            shrinkSizeScale = this.shrinkSizeScale,
            shrinkTimeRatio = this.shrinkTimeRatio,
            restoreTimeRatio = this.restoreTimeRatio,
            sizeRestoreEase = this.sizeRestoreEase,
            startAngle = this.startAngle,
            angleDamping = this.angleDamping,
            swingCount = this.swingCount,
            rotationTimeRatio = this.rotationTimeRatio,
            rotationEase = this.rotationEase,

            enableIdleMotion = this.enableIdleMotion,
            idleCycleDuration = this.idleCycleDuration,
            idleSizeOffset = this.idleSizeOffset,

            enableHideMotion = this.enableHideMotion,
            hideDuration = this.hideDuration,
            hideExpandOffset = this.hideExpandOffset,
            hideEase = this.hideEase
        };
    }

    public static CursorMotionSettings Default => new CursorMotionSettings();

    public static CursorMotionSettings Instant => new CursorMotionSettings
    {
        enableShowMotion = false,
        enableIdleMotion = false,
        enableHideMotion = false,
        showDuration = 0f,
        shrinkSizeScale = 1f,
        startAngle = 0f,
        swingCount = 0,
        idleSizeOffset = 0f,
        hideDuration = 0f
    };

    public static CursorMotionSettings Subtle => new CursorMotionSettings
    {
        enableShowMotion = true,
        showDuration = 0.24f,
        shrinkSizeScale = 0.91f,
        shrinkTimeRatio = 0.35f,
        restoreTimeRatio = 0.65f,
        sizeRestoreEase = Ease.OutBack,
        startAngle = 3.5f,
        angleDamping = 0.5f,
        swingCount = 2,
        rotationTimeRatio = 0.85f,
        rotationEase = Ease.OutSine,

        enableIdleMotion = true,
        idleCycleDuration = 2.0f,
        idleSizeOffset = 1.2f,

        enableHideMotion = true,
        hideDuration = 0.1f,
        hideExpandOffset = 4f,
        hideEase = Ease.OutQuad
    };

    /// <summary>
    /// 가로로 긴 직사각형 UI(옵션 행, 대지역 버튼 등)에 최적화된 초미세 모션입니다.
    /// 긴 가로폭에서도 양 끝이 과하게 튀지 않도록 마이크로 각도(0.85도, 약 2.9px)의 미세 틸트와
    /// 쫀득한 6% 수축/복원(Scale Pop)을 적용하여 눈에 기분 좋게 들어오면서도 단정함을 유지합니다.
    /// </summary>
    public static CursorMotionSettings RowSubtle => new CursorMotionSettings
    {
        enableShowMotion = true,
        showDuration = 0.22f,
        shrinkSizeScale = 0.94f,
        shrinkTimeRatio = 0.35f,
        restoreTimeRatio = 0.65f,
        sizeRestoreEase = Ease.OutBack,
        startAngle = 0.85f,
        angleDamping = 0.4f,
        swingCount = 2,
        rotationTimeRatio = 0.85f,
        rotationEase = Ease.OutSine,

        enableIdleMotion = true,
        idleCycleDuration = 2.0f,
        idleSizeOffset = 1.0f,

        enableHideMotion = true,
        hideDuration = 0.1f,
        hideExpandOffset = 4f,
        hideEase = Ease.OutQuad
    };
}
