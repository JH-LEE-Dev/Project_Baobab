using UnityEngine;
using System.Collections.Generic;

public class UIDepthController : MonoBehaviour
{
    // //외부 의존성 (없음)

    // //내부 의존성
    private readonly List<UIView> activeViews = new List<UIView>(16);

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        activeViews.Clear();
    }

    public void RegisterView(UIView _view)
    {
        if (_view == null)
        {
            return;
        }

        if (!activeViews.Contains(_view))
        {
            activeViews.Add(_view);
        }
    }

    public void UnregisterView(UIView _view)
    {
        if (_view == null)
        {
            return;
        }

        activeViews.Remove(_view);
    }

    public bool TryCloseTopView()
    {
        if (activeViews.Count == 0)
        {
            return false;
        }

        int lastIndex = activeViews.Count - 1;
        UIView topView = activeViews[lastIndex];

        if (topView != null && topView.gameObject.activeSelf)
        {
            topView.Hide();
            return true;
        }

        return false;
    }
}
