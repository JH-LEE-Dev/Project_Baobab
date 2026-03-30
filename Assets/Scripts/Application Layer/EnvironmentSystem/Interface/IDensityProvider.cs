using UnityEngine;

public interface IDensityProvider
{
    float GetAnimalDensity();
    float GetTreeDensity();
    bool CanCreateAnimal();
    bool CanCreateTree();
}

