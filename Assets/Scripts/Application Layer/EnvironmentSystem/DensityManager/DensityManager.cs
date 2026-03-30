using UnityEngine;

public class DensityManager : MonoBehaviour, IDensityProvider
{
    private float totalDensity = 100f;
    private float animalDensity;
    private float treeDensity;

    public bool CanCreateAnimal()
    {
        return false;
    }

    public bool CanCreateTree()
    {
        return true;
    }

    public float GetAnimalDensity()
    {
        return 0;
    }

    public float GetTreeDensity()
    {
        return 0;
    }

    public void Initialize()
    {
        animalDensity = 15f;
        treeDensity = 30f;
    }
}
