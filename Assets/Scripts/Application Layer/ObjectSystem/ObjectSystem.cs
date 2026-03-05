using UnityEngine;

public class ObjectSystem
{
    private ObjectManager objectManager;

    public void Initailize(ObjectManager _objectManager)
    {
        objectManager = _objectManager;
    }

    public void SetupObjects()
    {
        objectManager.SpawnTree();
    }
}
