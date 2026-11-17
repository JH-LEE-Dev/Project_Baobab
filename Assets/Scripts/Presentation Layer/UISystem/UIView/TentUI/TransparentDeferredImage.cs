using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 색 알파가 0이라 그려지지 않는 동안 위치·크기·스프라이트 변화로 생기는 정점 재생성을 미루는 Image.
// 지금 올라가 있는 메시의 정점 알파가 모두 0이고 지금 다시 만들어도 정점 알파가 모두 0이면(색 알파 0, 메시 수정 컴포넌트 없음),
// 투명 메시 생략(cullTransparentMesh)이 켜진 CanvasRenderer는 두 메시 모두 그리지 않으므로 화면 결과가 같다.
// 색이 바뀌면 color setter가 SetVerticesDirty를 부르고, 알파가 0이 아니면 그때 지금 상태로 다시 만든다.
public class TransparentDeferredImage : Image
{
    private static readonly List<Color32> meshColorBuffer = new List<Color32>(4);

    // 지금 CanvasRenderer에 올라가 있는 메시의 정점 알파가 모두 0인지
    private bool isUploadedMeshTransparent;

    public override void SetVerticesDirty()
    {
        if (CanDeferVertices())
            return;

        base.SetVerticesDirty();
    }

    protected override void UpdateGeometry()
    {
        base.UpdateGeometry();
        isUploadedMeshTransparent = IsMeshTransparent(workerMesh);
    }

    protected override void OnDisable()
    {
        // 꺼지면 CanvasRenderer 메시가 지워지므로, 다시 켜질 때는 원래대로 한 번 만든다.
        isUploadedMeshTransparent = false;
        base.OnDisable();
    }

    private bool CanDeferVertices()
    {
        if (false == isUploadedMeshTransparent || false == IsActive())
            return false;

        if (0f != color.a)
            return false;

        if (null != m_OnDirtyVertsCallback)
            return false;

        CanvasRenderer renderer = canvasRenderer;
        if (null == renderer || false == renderer.cullTransparentMesh)
            return false;

        // 메시 수정 컴포넌트는 색 알파와 무관한 정점(외곽선 등)을 더할 수 있으므로 미루지 않는다.
        if (TryGetComponent(typeof(IMeshModifier), out Component _))
            return false;

        return true;
    }

    private static bool IsMeshTransparent(Mesh _mesh)
    {
        if (null == _mesh)
            return false;

        meshColorBuffer.Clear();
        _mesh.GetColors(meshColorBuffer);
        if (meshColorBuffer.Count != _mesh.vertexCount)
            return false;

        for (int i = 0; i < meshColorBuffer.Count; i++)
        {
            if (0 != meshColorBuffer[i].a)
                return false;
        }

        return true;
    }
}
