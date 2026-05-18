using UnityEngine;

public class Cloud : EnvironmentObj
{
    private SpriteRenderer sr;
    private float moveSpeed;
    private float minX;
    private float maxX;
    private float timeOffset;
    private float initialY;

    public override void Initialize()
    {
        base.Initialize();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void SetupCloud(Sprite _sprite, float _moveSpeed, float _minX, float _maxX)
    {
        if (sr != null) sr.sprite = _sprite;
        moveSpeed = _moveSpeed;
        minX = _minX;
        maxX = _maxX;
        
        initialY = transform.position.y;
        float initialX = transform.position.x;
        timeOffset = (initialX - minX) / moveSpeed - Time.time;
    }

    public override Vector3 GetCurrentPosition()
    {
        float range = maxX - minX;
        if (range <= 0 || moveSpeed <= 0) return transform.position;

        float currentDistance = moveSpeed * (Time.time + timeOffset);
        float currentX = minX + (currentDistance % range);
        
        return new Vector3(currentX, initialY, 0f);
    }

    public override void Show()
    {
        transform.position = GetCurrentPosition();
        base.Show();
    }

    private void Update()
    {
        if (!bActivated) return;
        transform.position = GetCurrentPosition();
    }

    public override void ResetObj()
    {
        base.ResetObj();
    }
}
