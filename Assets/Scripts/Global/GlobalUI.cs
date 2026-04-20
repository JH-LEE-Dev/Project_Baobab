using UnityEngine;

public static class GlobalUI
{
    private static Vector3[] corners = new Vector3[4];
    public static Vector3 KeepInsideScreenforUI(RectTransform targetUI)
    {
        Canvas.ForceUpdateCanvases();
        targetUI.GetWorldCorners(corners);

        float minX = 10f;
        float maxX = Screen.width;
        float minY = 10f;
        float maxY = Screen.height;

        Vector3 clampedPos = targetUI.position;

        if (corners[0].x < minX) 
            clampedPos.x += (minX - corners[0].x); // 왼쪽으로 나감
        if (corners[2].x > maxX) 
            clampedPos.x -= (corners[2].x - maxX); // 오른쪽으로 나감

        if (corners[0].y < minY) 
            clampedPos.y += (minY - corners[0].y); // 아래로 나감
        if (corners[2].y > maxY) 
            clampedPos.y -= (corners[2].y - maxY); // 위로 나감

        return clampedPos;
    }

    public static Sprite GetSpritefromPath(string _folderPath, string _itemName)
    {
        string getPath = _folderPath;

        if (getPath.EndsWith('/'))
            getPath += '/';

        getPath += _itemName;

        return Resources.Load<Sprite>(getPath);
    }

    /// <summary>
    /// 즉시 픽셀 단위로 좌표를 고정합니다. (기본 32 PPU 가상 해상도 기준)
    /// </summary>
    public static Vector3 SnapToPixel(Vector3 _position, float _pixelsPerUnit = 32f)
    {
        _position.x = Mathf.Round(_position.x * _pixelsPerUnit) / _pixelsPerUnit;
        _position.y = Mathf.Round(_position.y * _pixelsPerUnit) / _pixelsPerUnit;
        return _position;
    }

    /// <summary>
    /// 실제 화면 해상도의 물리 픽셀 단위로 정밀하게 스냅합니다.
    /// 월드 좌표의 부동 소수점 오차를 제거하기 위해 스크린 좌표계에서 정수 단위로 고정 후 복원합니다.
    /// </summary>
    public static Vector3 SnapToScreenPixel(Vector3 _position, Camera _camera)
    {
        if (_camera == null) return _position;

        // 1. 월드 좌표를 현재 카메라 시점의 실제 스크린 픽셀 좌표(0~1920 등)로 변환
        Vector3 screenPos = _camera.WorldToScreenPoint(_position);

        // 2. 픽셀 좌표를 정수 단위로 강제 스냅하여 미세한 소수점 오차를 제거합니다.
        screenPos.x = Mathf.Floor(screenPos.x + 0.5f);
        screenPos.y = Mathf.Floor(screenPos.y + 0.5f);

        // 3. 정수가 된 픽셀 좌표를 다시 월드 좌표로 역변환합니다.
        float originalZ = _position.z;
        Vector3 snappedWorldPos = _camera.ScreenToWorldPoint(screenPos);
        
        // Z축은 원래의 월드 Z값을 유지합니다.
        snappedWorldPos.z = originalZ;
        
        return snappedWorldPos;
    }
}
