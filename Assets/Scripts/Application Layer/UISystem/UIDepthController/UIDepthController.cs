using UnityEngine;
using System.Collections.Generic;

public class UIDepthController : MonoBehaviour
{
    // //외부 의존성 (없음)

    // //내부 의존성
    private readonly List<IUIDepthCloseable> activeViews = new List<IUIDepthCloseable>(16);

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        activeViews.Clear();
    }

    public void RegisterView(IUIDepthCloseable _view)
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

    public void UnregisterView(IUIDepthCloseable _view)
    {
        if (_view == null)
        {
            return;
        }

        activeViews.Remove(_view);
    }

    /// <summary>
    /// 스택 맨 위의 살아있는 뷰를 닫습니다. 닫을 것이 있었으면 true를 반환합니다.
    ///
    /// 위에서부터 훑으면서 죽은 엔트리(파괴됨/비활성)는 그 자리에서 걷어내고 아래로 내려갑니다.
    /// 등록은 Show(), 해제는 Hide()에서 이뤄지므로 "등록되어 있다 = 활성"이 정상 상태이고,
    /// 그렇지 않은 엔트리는 짝이 맞지 않아 남은 유령이라 스택에 둘 이유가 없습니다.
    ///
    /// 유령을 그냥 두면 ESC가 통째로 죽습니다. 특히 파괴된 뷰가 위험한데, 여기 담기는 건
    /// IUIDepthCloseable(인터페이스) 참조라 == null이 UnityEngine.Object의 오버로드가 아닌
    /// 순수 참조 비교로 풀립니다. 파괴된 MonoBehaviour도 그 검사를 통과해 버리므로,
    /// 아래처럼 MonoBehaviour로 되돌려 유니티 규칙으로 다시 확인해야 합니다.
    /// </summary>
    public bool TryCloseTopView()
    {
        for (int i = activeViews.Count - 1; i >= 0; i--)
        {
            IUIDepthCloseable view = activeViews[i];

            if (true == IsDead(view))
            {
                activeViews.RemoveAt(i);
                continue;
            }

            view.Hide();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 스택에 남아 있을 이유가 없는 엔트리인지 판정합니다.
    /// (파괴된 오브젝트 / 이미 비활성인 뷰)
    /// </summary>
    private bool IsDead(IUIDepthCloseable _view)
    {
        if (_view == null)
        {
            return true;
        }

        // 유니티의 == 오버로드로 "파괴됨"까지 걸러낸다. 이 검사를 통과한 뒤에야
        // IsActive(내부적으로 gameObject 접근)를 만지는 것이 안전하다.
        if (_view is MonoBehaviour _behaviour && _behaviour == null)
        {
            return true;
        }

        return false == _view.IsActive;
    }
}
