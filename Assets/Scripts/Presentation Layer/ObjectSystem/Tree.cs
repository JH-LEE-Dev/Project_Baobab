using UnityEngine;

public class Tree : MonoBehaviour
{
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    private IEnvironmentProvider environmentProvider;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        
        shadowObject.Initialize(environmentProvider.shadowDataProvider);
    }
}
