using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// _CameraSortingLayerTexture를 샘플링하는 렌더러(용광로 아지랑이, 폭발 충격파 등)에 붙여서, 그 렌더러가 켜져 있는 동안
    /// SortingLayerTextureGate에 사용자로 등록한다. 렌더러를 지정하지 않으면 오브젝트가 활성인 동안 등록한다.
    /// 렌더러의 켜짐/꺼짐은 LateUpdate에서 확인하므로 같은 프레임 렌더 전에 반영된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SortingLayerTextureUser : MonoBehaviour
    {
        [SerializeField, Tooltip("캡처 텍스처를 샘플링하는 렌더러. 켜져 있는 동안만 사용자로 등록한다(비우면 오브젝트 활성 동안)")] private Renderer targetRenderer;

        //상태 변수
        private bool bAcquired;

        private void SetAcquired(bool _bAcquire)
        {
            if (_bAcquire == bAcquired) return;

            bAcquired = _bAcquire;
            if (true == _bAcquire) SortingLayerTextureGate.Acquire();
            else SortingLayerTextureGate.Release();
        }

        private bool IsRendering()
        {
            return null == targetRenderer || true == targetRenderer.enabled;
        }

        private void OnEnable()
        {
            SetAcquired(IsRendering());
        }

        private void LateUpdate()
        {
            SetAcquired(IsRendering());
        }

        private void OnDisable()
        {
            SetAcquired(false);
        }
    }
}
