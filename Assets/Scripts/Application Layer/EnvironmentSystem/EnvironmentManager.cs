using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    private IsometricShadowController isometricShadowController;

    public void Initialize()
    {
        isometricShadowController = GetComponent<IsometricShadowController>();
    }
}
