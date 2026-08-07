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
}
