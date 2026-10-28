using UnityEngine;

/// <summary>버프 HUD가 게이지를 그리는 방식.</summary>
public enum BuffGaugeKind
{
    // 시간이 지나며 줄어드는 지속형. 갱신하면 가득 찬다.
    Duration = 0,
    // 타수 스택형. 값은 정수이고 게이지는 값/최대값 비율(한 번 맞을 때마다 계단식으로 오른다).
    StackHits = 1,
    // 임계 수치형. 값이 연속적으로 차오르고 thresholdValue를 넘으면 발동 연출이 나온다.
    StackThreshold = 2,
    // 상태형. 켜져 있는 동안만 보이고 게이지는 가득 찬 채 고정, 숫자와 자동 만료가 없다. Dispel/Remove로만 사라진다.
    Status = 3,
}

/// <summary>버프 하나를 HUD에 그리기 위한 표시 정보. 게임 로직 수치는 담지 않고 표시에 필요한 값만 둔다.</summary>
[System.Serializable]
public struct BuffDisplayInfo
{
    // 식별자(AddOrUpdate/SetValue/Dispel/Remove가 이 값으로 대상을 찾는다)
    public int id;
    public BuffGaugeKind kind;

    // 아이콘(권장 12x12, Point 필터). null이면 임시 아이콘을 쓴다.
    public Sprite icon;
    // 프레임 윗줄 강조색. 알파 0이면 종류별 기본색.
    public Color accentColor;

    // Duration: 지속 시간(초)
    public float duration;

    // 스택형: 게이지가 가득 차는 값 / 시작 값
    public float maxValue;
    public float initialValue;
    // StackThreshold: 발동 수치. 게이지는 이 값에서 가득 차고 넘기는 순간 발동 연출이 나온다.
    // (maxValue는 값이 더 쌓일 수 있는 상한, 임계점 이상으로 보정된다)
    public float thresholdValue;
    // 스택형 감소: 마지막 증가 후 decayDelay초가 지나면 감소 시작.
    // StackHits는 decayInterval초마다 1씩, StackThreshold는 초당 decayPerSecond씩 깎인다. 0이면 감소하지 않는다.
    public float decayDelay;
    public float decayInterval;
    public float decayPerSecond;
}
