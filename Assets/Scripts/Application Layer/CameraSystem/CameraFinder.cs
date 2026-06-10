using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class CameraFinder : MonoBehaviour
{
    public event Action CameraFindEvent;

    public static CameraFinder Instance { get; private set; }

    [SerializeField] private Camera ppMainCamera;
    [SerializeField] private Camera overlayCamera;
    [SerializeField] private Camera ppUiCamera;

    public Camera PPMainCamera => ppMainCamera;
    public Camera OverlayCamera => overlayCamera;
    public Camera PPUiCamera => ppUiCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        FindCameras();
    }

    private void FindCameras()
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];

            // 씬에 적재되지 않은 프리팹 에셋 등은 제외
            if (cam.gameObject.scene.name == null)
            {
                continue;
            }

            string objName = cam.gameObject.name;
            if (objName == "PP Main Camera")
            {
                ppMainCamera = cam;
            }
            else if (objName == "Overlay Camera")
            {
                overlayCamera = cam;
            }
            else if (objName == "PP UI Camera")
            {
                ppUiCamera = cam;
            }
        }

        CameraFindEvent?.Invoke();
    }
}
