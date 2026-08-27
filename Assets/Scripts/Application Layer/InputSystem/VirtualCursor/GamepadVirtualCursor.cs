using System;
using UnityEngine;

/// <summary>
/// 패드용 가상 커서입니다. 마우스가 없는 상태에서 화면의 임의 지점을 가리켜야 할 때 씁니다.
///
/// 이 클래스는 "화면 좌표 하나"만 책임집니다. 커서 그림을 그리거나, 그 좌표로 무엇을 클릭할지는
/// 전부 소비하는 쪽(UI/게임플레이)의 몫입니다. 그래야 조작 배치가 정해지기 전에도
/// 시스템만 먼저 완성해 둘 수 있습니다.
///
/// [동작 규칙]
/// - 켜고 끄는 것은 화면이 정합니다. 별도의 토글 키가 없습니다.
///   커서가 필요한 화면(특성 UI 등)이 SetRequested(true)를 부르고, 닫을 때 false로 되돌립니다.
///   전용 키를 두지 않은 이유: 유저가 그 키의 존재와 용도를 알 방법이 없기 때문입니다.
/// - 요청이 있어도 **패드를 쓰는 중일 때만** 실제로 켜집니다. 마우스 유저에게는 진짜 커서가
///   이미 있으므로 나오면 안 됩니다. 창을 열어 둔 채 장치를 오가도 알아서 따라옵니다.
/// - 켜는 순간 위치는 항상 화면 중앙입니다. 마지막 위치를 기억하지 않습니다.
///   이유: 유저가 커서가 어디서 나타날지 예측할 수 있어야 하기 때문입니다.
///   화면 밖 구석에 남아 있던 커서가 되살아나면 "안 켜졌다"고 오해합니다.
/// - 오른쪽 스틱으로 움직입니다. 스틱 입력은 바깥에서 받습니다(어떤 액션에 물려 있는지 몰라도 되도록).
/// - 이동 속도는 픽셀이 아니라 "초당 화면 높이 배수"입니다.
///   이유: 스팀덱(1280x800)과 울트라와이드(3440x1440)에서 체감 속도가 같아야 합니다.
///   픽셀/초로 두면 고해상도에서 커서가 답답할 만큼 느려집니다.
/// </summary>
public class GamepadVirtualCursor
{
    /// <summary>커서가 켜지거나 꺼질 때 발생합니다. UI는 이걸로 커서 이미지를 보이고 숨기면 됩니다.</summary>
    public event Action<bool> ActiveChangedEvent;

    /// <summary>
    /// 커서가 실제로 움직였을 때 발생합니다. 인자는 화면 좌표(좌하단 원점, 픽셀)입니다.
    /// 켜지는 순간에도 중앙 좌표로 한 번 발생하므로, 구독자는 이 이벤트만 보고 위치를 맞출 수 있습니다.
    /// </summary>
    public event Action<Vector2> MovedEvent;

    private InputDeviceSettings settings;

    /// <summary>
    /// 감도 배율의 폭입니다. 슬라이더 가운데(1배)를 기준으로 위아래 이 배수만큼 벌어집니다.
    /// 3이면 최저 1/3배, 최고 3배가 됩니다.
    /// </summary>
    private const float SENSITIVITY_RANGE = 3f;

    // 유저 감도 설정에서 나온 속도 배율. 1이 기본이다.
    private float sensitivityScale = 1f;

    // 커서가 필요한 화면이 떠 있는지. UI 쪽에서 정해 줍니다.
    private bool bRequested = false;

    // 실제로 화면에 떠 있는지. (요청 && 패드 사용 중)
    private bool bActive = false;

    private Vector2 screenPosition;

    // 명시적으로 지정된 이동 가능 영역. 지정되지 않으면 화면 전체를 씁니다.
    private bool bUseExplicitBounds = false;
    private Rect explicitBounds;

    /// <summary>커서가 필요한 화면이 떠 있는지입니다. 실제 표시 여부와는 별개입니다.</summary>
    public bool IsRequested => bRequested;

    /// <summary>커서가 실제로 화면에 떠 있는지입니다. (요청이 있고, 지금 패드를 쓰는 중)</summary>
    public bool IsActive => bActive;

    /// <summary>커서의 화면 좌표입니다. (좌하단 원점, 픽셀 — 마우스 좌표와 같은 좌표계)</summary>
    public Vector2 ScreenPosition => screenPosition;

    /// <summary>유저 감도 설정에서 나온 속도 배율입니다. 1이 기본입니다.</summary>
    public float SensitivityScale => sensitivityScale;

    /// <summary>커서가 움직일 수 있는 영역입니다.</summary>
    public Rect Bounds => bUseExplicitBounds ? explicitBounds : FullScreenBounds;

    private static Rect FullScreenBounds => new Rect(0f, 0f, Screen.width, Screen.height);

    public void Initialize(InputDeviceSettings _settings)
    {
        // 트래커와 같은 규칙: 에셋이 없으면 기본값 인스턴스로 대체한다.
        settings = null != _settings ? _settings : InputDeviceSettings.CreateDefault();
    }

    /// <summary>
    /// 커서가 움직일 수 있는 영역을 화면 전체가 아닌 특정 사각형으로 제한합니다.
    ///
    /// 필요한 이유: 이 게임은 울트라와이드에서 PixelPerfectCamera의 Pillarbox가 켜져
    /// 화면 좌우에 검은 띠가 생깁니다. 그 띠는 카메라가 그리지 않는 영역이라,
    /// 커서가 거기까지 나가면 가리킬 대상이 아무것도 없습니다.
    /// 그래서 카메라의 pixelRect로 좁혀 줍니다. (InputManager가 매 프레임 넣어 줍니다)
    /// </summary>
    public void SetBounds(Rect _bounds)
    {
        if (_bounds.width <= 0f || _bounds.height <= 0f) return;

        bUseExplicitBounds = true;

        if (explicitBounds == _bounds) return;

        explicitBounds = _bounds;

        // 해상도가 바뀌거나 크롭이 켜지면 지금 좌표가 영역 밖에 있을 수 있다.
        if (true == bActive)
        {
            ApplyPosition(ClampToBounds(screenPosition));
        }
    }

    /// <summary>
    /// 유저가 정한 감도를 반영합니다. _normalized는 0~1이고 **0.5가 기본(1배)** 입니다.
    /// (옵션 슬라이더 0~100을 100으로 나눈 값을 그대로 넘기면 됩니다)
    ///
    /// 선형이 아니라 지수로 변환하는 이유: 감도는 곱셈으로 체감됩니다. 0.5배에서 0.6배로 가는
    /// 변화와 2.5배에서 3배로 가는 변화가 비슷하게 느껴져야 슬라이더가 고르게 움직이는 것처럼
    /// 느껴지는데, 선형으로 두면 낮은 쪽은 거의 차이가 없고 높은 쪽만 급격해집니다.
    ///
    /// 0에서도 배율이 0이 되지 않는 것이 중요합니다. 감도 0은 커서가 아예 안 움직이는 상태라,
    /// 유저가 실수로 끝까지 내리면 옵션 화면으로 되돌아갈 수단까지 잃습니다.
    /// </summary>
    public void SetSensitivityScale(float _normalized)
    {
        // NaN은 어떤 비교에도 false라 Clamp만으로는 걸러지지 않는다.
        if (true == float.IsNaN(_normalized)) _normalized = 0.5f;

        _normalized = Mathf.Clamp01(_normalized);

        sensitivityScale = Mathf.Pow(SENSITIVITY_RANGE, (_normalized - 0.5f) * 2f);
    }

    /// <summary>영역 제한을 풀고 화면 전체를 쓰도록 되돌립니다.</summary>
    public void ClearBounds()
    {
        bUseExplicitBounds = false;
    }

    /// <summary>
    /// 커서가 필요한 화면이 열렸는지 알려 줍니다. 특성 UI를 열 때 true, 닫을 때 false.
    ///
    /// 요청이 있어도 패드를 쓰는 중이 아니면 켜지지 않습니다. 그 판단에 필요해서
    /// 현재 장치 상태를 함께 받습니다.
    /// </summary>
    public void SetRequested(bool _bRequested, bool _bGamepadMode)
    {
        bRequested = _bRequested;

        UpdateActiveState(_bGamepadMode);
    }

    /// <summary>
    /// 요청과 현재 장치를 종합해 실제 표시 상태를 맞춥니다.
    ///
    /// 이 한 곳으로 모아 둔 덕분에 "창을 연 뒤 패드를 잡았다", "창이 열린 채 마우스를 만졌다",
    /// "패드를 쓰다 창을 닫았다"가 전부 같은 경로로 처리됩니다.
    /// </summary>
    private void UpdateActiveState(bool _bGamepadMode)
    {
        bool _bShouldBeActive = bRequested && _bGamepadMode;

        if (bActive == _bShouldBeActive) return;

        bActive = _bShouldBeActive;

        if (true == bActive)
        {
            screenPosition = Bounds.center;
        }

        ActiveChangedEvent?.Invoke(bActive);

        // 켤 때는 위치가 마침 중앙과 같더라도 반드시 한 번 알린다. ApplyPosition을 쓰면
        // "값이 안 바뀌었다"고 걸러져서, 두 번째로 켤 때 구독자가 좌표를 못 받는다.
        if (true == bActive)
        {
            MovedEvent?.Invoke(screenPosition);
        }
    }

    /// <summary>
    /// 커서 위치를 직접 지정합니다. (예: UI가 커서를 특정 버튼 위로 끌어다 놓고 싶을 때)
    /// 영역 밖 좌표는 자동으로 잘립니다.
    /// </summary>
    public void SetPosition(Vector2 _screenPosition)
    {
        if (false == bActive) return;

        ApplyPosition(ClampToBounds(_screenPosition));
    }

    /// <summary>
    /// 매 프레임 호출합니다.
    ///
    /// _stick은 오른쪽 스틱의 원시 벡터(-1~1)이고, _bGamepadMode는 지금 유저가 패드를 쓰고 있는지입니다.
    /// 일시정지(timeScale = 0) 중에도 커서는 움직여야 하므로 반드시 unscaled 델타를 넘기세요.
    /// </summary>
    public void Tick(float _unscaledDeltaTime, Vector2 _stick, bool _bGamepadMode)
    {
        // 마우스를 잡으면 진짜 커서가 돌아오므로 가상 커서는 물러나고, 다시 패드를 잡으면
        // 화면 중앙에서 되살아난다. 그 판정이 여기서 매 프레임 갱신된다.
        UpdateActiveState(_bGamepadMode);

        if (false == bActive) return;

        float _deadzone = null != settings ? settings.cursorStickDeadzone : 0.2f;
        float _magnitude = _stick.magnitude;

        if (_magnitude <= _deadzone) return;

        // 데드존 바깥을 0~1로 다시 편다. 이걸 안 하면 데드존을 넘는 순간
        // 커서가 이미 상당한 속도로 튀어나가서 미세 조작이 불가능하다.
        float _normalized = (_magnitude - _deadzone) / Mathf.Max(0.0001f, 1f - _deadzone);
        _normalized = Mathf.Clamp01(_normalized);

        // 지수를 씌워 살짝 기울였을 때를 더 느리게 만든다. 조준선 없이 작은 대상을 맞춰야 하므로
        // 선형보다 이쪽이 훨씬 다루기 쉽다.
        float _exponent = null != settings ? settings.cursorResponseExponent : 2f;
        if (_exponent > 1f)
        {
            _normalized = Mathf.Pow(_normalized, _exponent);
        }

        Rect _bounds = Bounds;

        // 기본 속도(제작자가 정한 값) × 감도(유저가 정한 값).
        // 둘을 곱으로 나눠 둔 덕분에, 나중에 기본 속도를 조정해도 유저 설정이 그대로 따라온다.
        float _screensPerSecond = null != settings ? settings.cursorSpeedScreensPerSecond : 1.1f;
        float _pixelsPerSecond = _screensPerSecond * sensitivityScale * _bounds.height;

        Vector2 _direction = _stick / _magnitude;
        Vector2 _next = screenPosition + _direction * (_normalized * _pixelsPerSecond * _unscaledDeltaTime);

        ApplyPosition(ClampToBounds(_next));
    }

    private Vector2 ClampToBounds(Vector2 _position)
    {
        Rect _bounds = Bounds;

        // 커서 그림이 화면 끝에서 반쯤 잘려 보이지 않도록 안쪽으로 조금 물린다.
        float _padding = null != settings ? settings.cursorEdgePaddingPixels : 4f;

        // 영역보다 여백이 크면 잘못된 범위가 되므로 영역 절반을 넘지 않게 막는다.
        _padding = Mathf.Min(_padding, Mathf.Min(_bounds.width, _bounds.height) * 0.5f);

        _position.x = Mathf.Clamp(_position.x, _bounds.xMin + _padding, _bounds.xMax - _padding);
        _position.y = Mathf.Clamp(_position.y, _bounds.yMin + _padding, _bounds.yMax - _padding);

        return _position;
    }

    private void ApplyPosition(Vector2 _position)
    {
        if (screenPosition == _position) return;

        screenPosition = _position;

        MovedEvent?.Invoke(screenPosition);
    }

    /// <summary>구독을 정리합니다. 켜져 있던 커서는 알림 없이 내립니다.</summary>
    public void Release()
    {
        bActive = false;
        bRequested = false;

        ActiveChangedEvent = null;
        MovedEvent = null;
    }
}
