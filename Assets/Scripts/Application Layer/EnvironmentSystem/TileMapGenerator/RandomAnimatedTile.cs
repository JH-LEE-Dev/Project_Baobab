using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Random Animated Tile", menuName = "Tiles/Random Animated Tile")]
public class RandomAnimatedTile : TileBase
{
    //내부 의존성
    [SerializeField] private Sprite[] animatedSprites;
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 1f;
    [SerializeField] private bool randomizeStartTime = true;

    public override void GetTileData(Vector3Int _position, ITilemap _tilemap, ref TileData _tileData)
    {
        if (animatedSprites != null && animatedSprites.Length > 0)
        {
            _tileData.sprite = animatedSprites[0];
        }
    }

    public override bool GetTileAnimationData(Vector3Int _position, ITilemap _tilemap, ref TileAnimationData _tileAnimationData)
    {
        if (animatedSprites != null && animatedSprites.Length > 0)
        {
            _tileAnimationData.animatedSprites = animatedSprites;
            
            // 좌표 기반 결정론적 해시 생성 (호출할 때마다 고유하고 일정한 시드값 보장)
            int hash = _position.x * 73856093 ^ _position.y * 19349663 ^ _position.z * 83492791;
            float seed = Mathf.Abs(hash % 1000) / 1000f; // 0.0f ~ 1.0f
            
            // 속도 무작위 보간
            _tileAnimationData.animationSpeed = minSpeed + seed * (maxSpeed - minSpeed);
            
            if (randomizeStartTime)
            {
                // 위치에 따른 고유한 시작 시간 오프셋 적용 (0 ~ 10초)
                _tileAnimationData.animationStartTime = seed * 10f; 
            }
            
            return true;
        }
        return false;
    }
}
