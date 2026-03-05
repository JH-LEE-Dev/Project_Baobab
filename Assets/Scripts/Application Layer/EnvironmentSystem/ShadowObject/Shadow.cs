using UnityEngine;

public class Shadow : MonoBehaviour
{
    //외부 의존성
    IShadowDataProvider shadowDataProvider;

    private float _dayCycleSpeed;
    private float _minHeightScale;
    private float _maxHeightScale;

    public void Initialize(IShadowDataProvider _shadowDataProvider)
    {     
        shadowDataProvider = _shadowDataProvider;
    }

    private void Update()
    {
        if (shadowDataProvider == null)
            return;

        _dayCycleSpeed = shadowDataProvider.dayCycleSpeed;
        _minHeightScale = shadowDataProvider.minHeightScale;
        _maxHeightScale = shadowDataProvider.maxHeightScale;

        // 1. 현재 시간 각도 계산 (0 ~ 2PI)
        float timeAngle = Time.time * _dayCycleSpeed;

        // 2. 공전 (회전)
        // 하루 주기에 맞춰 한 바퀴(360도)를 회전합니다.
        float rotationDegree = timeAngle * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0, 0, rotationDegree);

        // 3. 해의 고도에 따른 길이 변형 (Scale)
        // Cos 함수를 활용: 
        // 0도(일출) -> 최대 길이
        // 90도(정오) -> 최소 길이
        // 180도(일몰) -> 최대 길이
        float heightFactor = Mathf.Abs(Mathf.Cos(timeAngle));
        float targetScaleY = Mathf.Lerp(_minHeightScale, _maxHeightScale, heightFactor);

        transform.localScale = new Vector3(1f, targetScaleY, 1f);

        // 4. 밤낮에 따른 투명도 처리 (선택 사항)
        // 해가 지평선 아래로 내려가는 타이밍(Sin < 0)에 그림자를 숨기거나 흐리게 할 수 있습니다.
    }
}
