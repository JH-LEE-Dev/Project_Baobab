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
    /// 실제 화면 해상도(예: 1920*1080)의 물리 픽셀 단위로 정밀하게 스냅합니다.
    /// 월드 공간 UI의 지터를 해결하는 데 가장 효과적입니다.
    /// </summary>
    public static Vector3 SnapToScreenPixel(Vector3 _position, Camera _camera)
    {
        if (_camera == null) return _position;

        // 화면 높이를 카메라 가시 높이(OrthoSize * 2)로 나누어 실제 Screen PPU를 계산
        float screenPPU = Screen.height / (_camera.orthographicSize * 2f);

        _position.x = Mathf.Round(_position.x * screenPPU) / screenPPU;
        _position.y = Mathf.Round(_position.y * screenPPU) / screenPPU;
        
        return _position;
    }
}
