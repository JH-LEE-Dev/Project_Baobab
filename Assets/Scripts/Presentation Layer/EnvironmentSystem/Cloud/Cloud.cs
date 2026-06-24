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

    public void SetupCloud(System.Collections.Generic.List<Sprite> _sprites, Color _color, float _moveSpeed, float _minX, float _maxX)
    {
        if (_sprites != null && _sprites.Count > 0 && sr != null)
        {
            sr.sprite = _sprites[UnityEngine.Random.Range(0, _sprites.Count)];
            sr.color = _color;
        }
        moveSpeed = _moveSpeed;
        minX = _minX;
        maxX = _maxX;
        
        initialY = cachedTransform.position.y;
        float initialX = cachedTransform.position.x;
        timeOffset = (initialX - minX) / moveSpeed - Time.time;
    }

    public override Vector3 GetCurrentPosition()
    {
        float range = maxX - minX;
        if (range <= 0 || moveSpeed <= 0) return cachedTransform.position;

        float currentDistance = moveSpeed * (Time.time + timeOffset);
        float currentX = minX + (currentDistance % range);
        
        return new Vector3(currentX, initialY, 0f);
    }

    public override void Show()
    {
        cachedTransform.position = GetCurrentPosition();
        base.Show();
    }

    public override void ManualUpdate()
    {
        if (!bActivated) return;
        cachedTransform.position = GetCurrentPosition();
    }

    public override void ResetObj()
    {
        base.ResetObj();
    }
}
