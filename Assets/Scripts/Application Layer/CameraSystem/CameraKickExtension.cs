using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 카메라를 한 방향으로 한 번 세게 찍는 연출(CameraMoveController.ShakeCameraKick)의 오프셋을 카메라 자세에 더한다.
/// 흔들림(Perlin 노이즈)과 같은 Correction 채널에 넣어, 조준용 ScreenToWorldWithoutShake가 이 오프셋도 함께 뺀다.
/// 오프셋이 0이면 아무것도 바꾸지 않는다. CameraMoveController가 처음 찍을 때 가상 카메라에 붙인다.
/// </summary>
[AddComponentMenu("")]
public sealed class CameraKickExtension : CinemachineExtension
{
    /// <summary>이번 프레임에 더할 위치(월드). CameraMoveController.Update가 채운다.</summary>
    public Vector3 Offset;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase _vcam, CinemachineCore.Stage _stage, ref CameraState _state, float _deltaTime)
    {
        if (CinemachineCore.Stage.Noise != _stage || Vector3.zero == Offset) return;
        _state.PositionCorrection += Offset;
    }
}
