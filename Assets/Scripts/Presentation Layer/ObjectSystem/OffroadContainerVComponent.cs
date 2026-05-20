using UnityEngine;

public class OffroadContainerVComponent : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private CustomSortable customSortable;

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
    }

    public void LateUpdate()
    {
        customSortable.SetHeight(0f);
        customSortable.ManualLateUpdate();
    }
}
