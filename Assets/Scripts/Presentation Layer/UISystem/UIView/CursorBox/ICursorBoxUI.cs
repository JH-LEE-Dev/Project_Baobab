using UnityEngine;

public interface ICursorBoxUI
{
    /// <summary>
    /// 대상 RectTransform 위에 기본 크기로 커서를 표시합니다.
    /// </summary>
    void Show(RectTransform _target);

    /// <summary>
    /// 대상 RectTransform 위에 지정한 크기로 커서를 표시합니다.
    /// </summary>
    void Show(RectTransform _target, Vector2 _size);

    /// <summary>
    /// 대상 RectTransform 위에 지정한 크기 및 오프셋으로 커서를 표시합니다.
    /// </summary>
    void Show(RectTransform _target, Vector2 _size, Vector2 _offset);

    /// <summary>
    /// 대상 RectTransform 위에 지정한 크기, 오프셋 및 커스텀 모션 설정을 적용하여 커서를 표시합니다.
    /// </summary>
    void Show(RectTransform _target, Vector2 _size, Vector2 _offset, CursorMotionSettings _customMotion);

    /// <summary>
    /// 특정 화면 픽셀 좌표(Screen Position)에 커서를 표시합니다.
    /// </summary>
    void ShowScreenPosition(Vector2 _screenPosition, Vector2 _size);

    /// <summary>
    /// 커서를 부드럽게 숨깁니다 (Hide 모션 재생).
    /// </summary>
    void Hide();

    /// <summary>
    /// 현재 표시 중인 대상이 일치할 때만 커서를 숨깁니다.
    /// </summary>
    void Hide(RectTransform _target);

    /// <summary>
    /// 커서를 즉시 숨깁니다 (애니메이션 없이 비활성화).
    /// </summary>
    void HideImmediately();

    /// <summary>
    /// 현재 특정 대상을 가리키고 있는지 여부를 반환합니다.
    /// </summary>
    bool IsTarget(RectTransform _target);

    /// <summary>
    /// 현재 커서가 화면에 표시 중인지 여부
    /// </summary>
    bool IsShowing { get; }

    /// <summary>
    /// 현재 추적 중인 대상 RectTransform
    /// </summary>
    RectTransform CurrentTarget { get; }

    /// <summary>
    /// 전역 기본 모션 설정
    /// </summary>
    CursorMotionSettings MotionSettings { get; set; }
}
