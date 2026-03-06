using UnityEngine;

public class TreeObj : MonoBehaviour
{
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    private IEnvironmentProvider environmentProvider;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        shadowObject.Initialize(environmentProvider.shadowDataProvider);
    }

    private void Update()
    {
        if(shadowObject != null)
        {
            shadowObject.GetComponent<SpriteRenderer>().sprite = animatorObject.GetComponentInChildren<SpriteRenderer>().sprite;
        }
    }
}
