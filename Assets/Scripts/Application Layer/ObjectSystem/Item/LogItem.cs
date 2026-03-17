using System.Collections;
using UnityEngine;

public class LogItem : Item
{
    // 내부 의존성
    private LogState logType;
    private TreeType treeType;
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    public void Initialize(LogState _logType, TreeType _treeType)
    {
        logType = _logType;
        treeType = _treeType;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
            }
        }
    }

    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        StartCoroutine(ParabolicMoveRoutine(_start, _end, _height, _duration));
    }

    private IEnumerator ParabolicMoveRoutine(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _duration;

            // 선형 보간 (바닥 위치)
            Vector3 currentGroundPos = Vector3.Lerp(_start, _end, t);

            // 포물선 높이 계산 (y = -4h(t-0.5)^2 + h)
            float heightOffset = -4 * _height * (t - 0.5f) * (t - 0.5f) + _height;

            if (visualTransform != null)
            {
                // 시각적 트랜스폼이 있는 경우 (높이만 따로 조절)
                transform.position = currentGroundPos;
                visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            }
            else
            {
                // 없는 경우 직접 position 수정
                transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
            }

            yield return null;
        }

        transform.position = _end;
        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
        }
    }
}
