using UnityEngine;

public class Shadow : MonoBehaviour
{
    //외부 의존성
    private IShadowDataProvider shadowDataProvider;

    //내부 의존성
    private float _minHeightScale;
    private float _maxHeightScale;

    public void Initialize(IShadowDataProvider _shadowDataProvider)
    {
        shadowDataProvider = _shadowDataProvider;
    }

    private void Update()
    {
        if (shadowDataProvider == null)
        {
            return;
        }

        float timePercent = shadowDataProvider.currentTimePercent;
        _minHeightScale = shadowDataProvider.minHeightScale;
        _maxHeightScale = shadowDataProvider.maxHeightScale;

        // 1. 현재 시간 각도 계산 (0 ~ 2PI)
        float timeAngle = timePercent * Mathf.PI * 2f;

        // 2. 공전 (회전) - 24시간 내내 회전 유지
        transform.localRotation = Quaternion.Euler(0, 0, timeAngle*Mathf.Rad2Deg);

        // 3. 해의 고도에 따른 길이 변형 (Scale)
        // 낮/밤 구분 없이 사인 곡선에 따라 길이를 조절하되, 
        // 시각적 처리는 Controller의 알파 페이딩에서 담당함.
        float heightFactor = Mathf.Abs(Mathf.Sin(timeAngle));
        float targetScaleY = Mathf.Lerp(_minHeightScale, _maxHeightScale, heightFactor);

        transform.localScale = new Vector3(1f, targetScaleY, 1f);
    }
}
