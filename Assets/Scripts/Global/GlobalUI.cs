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

    public static Vector3 SnapToPixel(Vector3 _position, float _pixelsPerUnit = 32f)
    {
        _position.x = Mathf.Round(_position.x * _pixelsPerUnit) / _pixelsPerUnit;
        _position.y = Mathf.Round(_position.y * _pixelsPerUnit) / _pixelsPerUnit;
        return _position;
    }
}
