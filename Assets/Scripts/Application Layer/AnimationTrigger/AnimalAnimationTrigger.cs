using UnityEngine;

public class AnimalAnimationTrigger : MonoBehaviour
{
    private Animal animal;

    public void Start()
    {
        animal = transform.parent.GetComponent<Animal>();

        if(animal == null)
            Debug.LogError("AnimalAnimationTrigger -> animal is null");
    }

    public void RunStartEnd()
    {
        animal.animalAnimValueHandler.RunStartEnd(true);
    }

    public void IdleEnd()
    {
        animal.animalAnimValueHandler.IdleEnd();
    }
}
