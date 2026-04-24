using UnityEngine;

public class Board : MonoBehaviour
{
    // // 외부 의존성
    [SerializeField] private Shadow shadowObject;

    // // 내부 의존성 및 캐싱 필드
    private IEnvironmentProvider environmentProvider;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        InitializeShadow(shadowObject);
    }

    public void Update()
    {
        UpdateShadow(shadowObject);
    }

    private void InitializeShadow(Shadow _shadow)
    {
        if (_shadow != null)
        {
            _shadow.Initialize();
        }
    }

    private void UpdateShadow(Shadow _shadow)
    {
        if (_shadow == null || environmentProvider == null)
        {
            return;
        }

        _shadow.ManualUpdate(
            environmentProvider.shadowDataProvider.CurrentShadowRotation,
            environmentProvider.shadowDataProvider.CurrentShadowScaleY,
            environmentProvider.shadowDataProvider.IsShadowActive
        );
    }
}
