#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// [임시] 버프 HUD 표시 테스트용 랜덤 버프 발생기. 실제 버프 신호가 연결되면 이 Temp 폴더째 삭제한다.
/// 연결 작업 중에는 Enabled를 false로 두고, 삭제 절차는 Docs/BuffHUD_Guide.md 8장을 따른다.
/// 스스로 생성되어 UIView_HUD의 BuffBar가 생기면 주기적으로 무작위 버프를 추가/갱신/해제한다.
/// 에디터와 개발 빌드에서만 컴파일된다.
/// </summary>
public class HUD_BuffRandomTester : MonoBehaviour
{
    // 끄려면 false
    private static readonly bool Enabled = true;
    private const int MaxActive = 6;
    private const int IdCount = 8;
    private const float FindInterval = 1f;
    private const float ActionMin = 1.5f;
    private const float ActionMax = 4f;

    // 외부 의존성
    private HUD_BuffBar bar;

    // 상태 변수
    private float nextFindTime = 0f;
    private float nextActionTime = 0f;

    private static void Boot()
    {
        if (false == Enabled)
        {
            return;
        }

        GameObject _go = new GameObject("HUD_BuffRandomTester");
        DontDestroyOnLoad(_go);
        _go.AddComponent<HUD_BuffRandomTester>();
    }

    private BuffDisplayInfo MakeInfo(int _id)
    {
        BuffDisplayInfo _info = new BuffDisplayInfo();
        _info.id = _id;
        _info.kind = (BuffGaugeKind)(_id % 4);
        // 아이콘은 비워 둔다: EBuffHudId(1~6)는 HUD_BuffBar의 아이콘 테이블이, 그 밖의 id는 임시 아이콘이 채운다.

        switch (_info.kind)
        {
            case BuffGaugeKind.StackHits:
                _info.maxValue = 5f;
                _info.initialValue = 1f;
                _info.decayDelay = 3f;
                _info.decayInterval = 2f;
                break;

            case BuffGaugeKind.StackThreshold:
                _info.maxValue = 100f;
                _info.thresholdValue = 70f;
                _info.initialValue = 10f;
                _info.decayDelay = 2f;
                _info.decayPerSecond = 8f;
                break;

            case BuffGaugeKind.Status:
                break;

            default:
                _info.duration = UnityEngine.Random.Range(6f, 12f);
                _info.maxValue = 1f;
                break;
        }

        return _info;
    }

    private int PickExistingId()
    {
        int _start = UnityEngine.Random.Range(0, IdCount);

        for (int i = 0; i < IdCount; i++)
        {
            int _id = (_start + i) % IdCount;

            if (true == bar.Contains(_id))
            {
                return _id;
            }
        }

        return -1;
    }

    private int PickAbsentId()
    {
        int _start = UnityEngine.Random.Range(0, IdCount);

        for (int i = 0; i < IdCount; i++)
        {
            int _id = (_start + i) % IdCount;

            if (false == bar.Contains(_id))
            {
                return _id;
            }
        }

        return -1;
    }

    private void DoAdd()
    {
        int _id = PickAbsentId();

        if (_id < 0)
        {
            return;
        }

        BuffDisplayInfo _info = MakeInfo(_id);
        bar.AddOrUpdate(_info);
        Debug.Log("[BuffTest] 추가 id=" + _id + " kind=" + _info.kind);
    }

    private void DoBoost(int _id)
    {
        BuffDisplayInfo _info = MakeInfo(_id);

        if (BuffGaugeKind.Duration == _info.kind || BuffGaugeKind.Status == _info.kind)
        {
            bar.AddOrUpdate(_info);
            Debug.Log("[BuffTest] 갱신 id=" + _id);
            return;
        }

        bar.TryGetValue(_id, out float _current);
        float _add = BuffGaugeKind.StackHits == _info.kind ? 1f : UnityEngine.Random.Range(10f, 25f);
        bar.SetValue(_id, Mathf.Min(_info.maxValue, _current + _add));
        Debug.Log("[BuffTest] 증가 id=" + _id + " " + _current + " -> +" + _add);
    }

    private void DoRandomAction()
    {
        int _count = bar.ActiveCount;
        int _existing = PickExistingId();
        float _r = UnityEngine.Random.value;

        if (_existing < 0 || (_r < 0.45f && _count < MaxActive))
        {
            DoAdd();
        }
        else if (_r < 0.85f)
        {
            DoBoost(_existing);
        }
        else if (_r < 0.95f)
        {
            bar.Dispel(_existing);
            Debug.Log("[BuffTest] 해제(Dispel) id=" + _existing);
        }
        else
        {
            bar.Remove(_existing);
            Debug.Log("[BuffTest] 즉시 제거 id=" + _existing);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeBoot()
    {
        Boot();
    }

    private void Update()
    {
        float _now = Time.unscaledTime;

        if (null == bar)
        {
            if (_now < nextFindTime)
            {
                return;
            }

            nextFindTime = _now + FindInterval;
            UIView_HUD _hud = FindAnyObjectByType<UIView_HUD>();
            bar = null != _hud ? _hud.BuffBar : null;

            if (null != bar)
            {
                nextActionTime = _now + 2f;
            }

            return;
        }

        // 마을 등에서 버프 줄이 숨겨진 동안은 동작하지 않는다
        if (_now < nextActionTime || false == bar.gameObject.activeInHierarchy)
        {
            return;
        }

        nextActionTime = _now + UnityEngine.Random.Range(ActionMin, ActionMax);
        DoRandomAction();
    }
}
#endif
