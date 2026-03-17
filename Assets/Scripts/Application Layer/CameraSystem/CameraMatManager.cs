using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class CameraMatManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float pixelsPerUnit = 32f;
    
    private Camera cam;
    private PixelPerfectCamera ppc;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        ppc = GetComponent<PixelPerfectCamera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (cam != null) cam.ResetProjectionMatrix();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != cam) return;

        float ppu = (ppc != null) ? ppc.assetsPPU : pixelsPerUnit;
        if (ppu <= 0) return;

        Vector3 pos = transform.position;

        float snappedX = Mathf.Round(pos.x * ppu) / ppu;
        float snappedY = Mathf.Round(pos.y * ppu) / ppu;

        float offX = pos.x - snappedX;
        float offY = pos.y - snappedY;

        cam.ResetProjectionMatrix();
        Matrix4x4 mat = cam.projectionMatrix;

        mat.m02 -= offX * mat.m00;
        mat.m12 -= offY * mat.m11;

        cam.projectionMatrix = mat;
    }
}
