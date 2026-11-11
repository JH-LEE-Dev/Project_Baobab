using UnityEngine;

/// <summary>
/// 나무 사망 이펙트를 터뜨릴 자리·색. 보통은 쓰러지는 순간의 나무 그림(TreeVisualComponent)에서 그대로 읽는다(From).
/// 나무가 풀로 돌아간 뒤에 터뜨리는 연출(지진에 튀어 오른 나무 - QuakeTreeLaunch)은 쓰러지는 순간 담아 두었다가 자리만 옮겨 쓴다.
/// </summary>
public struct TreeDeadVfxPose
{
    public Vector3 topPosition;
    public Quaternion topRotation;
    public Vector3 bottomPosition;
    public Quaternion bottomRotation;
    public ParticleColorSet topColor;
    public ParticleColorSet bottomColor;

    public static TreeDeadVfxPose From(TreeVisualComponent _visual)
    {
        return new TreeDeadVfxPose
        {
            topPosition = _visual.GetTopRootPosition(),
            topRotation = _visual.GetTopRootRotation(),
            bottomPosition = _visual.GetBottomRootPosition(),
            bottomRotation = _visual.GetBottomRootRotation(),
            topColor = _visual.GetTopVfxColor(),
            bottomColor = _visual.GetBottomVfxColor(),
        };
    }
}
