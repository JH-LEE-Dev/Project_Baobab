using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 나무 피격/성장 플래시를 한 곳에서 틱합니다.
///
/// 예전에는 나무마다 코루틴을 시작해(타격마다 코루틴 객체 할당) 매 프레임 세기를 갱신했다. 광역 공격으로 수십 그루가
/// 동시에 맞으면 코루틴 수십 개가 함께 돈다. 여기서는 등록 목록 하나를 LateUpdate에서 순회한다.
///
/// 적용되는 값의 순서는 코루틴 버전과 정확히 같다:
///   등록 프레임: curve(0) 적용 후 elapsed = deltaTime (StartCoroutine이 첫 MoveNext를 그 자리에서 실행하던 것과 동일)
///   이후 프레임: elapsed < duration이면 curve(elapsed/duration) 적용 후 elapsed += deltaTime, 아니면 0 적용 후 종료
/// 나무가 비활성화되면(풀 반환) Unity가 코루틴을 조용히 끊던 것과 같이 적용 없이 목록에서 뺀다.
/// </summary>
public static class TreeFlashSystem
{
    private struct Entry
    {
        public TreeVisualComponent visual;
        public AnimationCurve curve;
        public float duration;
        public float elapsed;
        public int registeredFrame;
    }

    private static readonly List<Entry> entries = new List<Entry>(64);
    private static TreeFlashSystemDriver driver;

    // 도메인 리로드를 끈 Enter Play Mode 설정에서도 이전 세션의 목록/드라이버 참조가 남지 않도록 플레이 시작마다 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear();
        driver = null;
    }

    /// <summary>
    /// 플래시를 시작합니다. 같은 나무의 진행 중인 플래시는 교체됩니다.
    /// </summary>
    public static void Begin(TreeVisualComponent _visual, AnimationCurve _curve, float _duration)
    {
        if (_visual == null || _curve == null) return;

        Cancel(_visual);

        // 코루틴 버전: while (elapsed < duration)이 처음부터 거짓이면 즉시 0을 적용하고 끝났다
        if (_duration <= 0f)
        {
            _visual.ApplyFlashAmountFromSystem(0f);
            return;
        }

        EnsureDriver();

        _visual.ApplyFlashAmountFromSystem(_curve.Evaluate(0f));
        entries.Add(new Entry
        {
            visual = _visual,
            curve = _curve,
            duration = _duration,
            elapsed = Time.deltaTime,
            registeredFrame = Time.frameCount
        });
    }

    /// <summary>
    /// 진행 중인 플래시를 적용 없이 중단합니다(StopCoroutine과 동일).
    /// </summary>
    public static void Cancel(TreeVisualComponent _visual)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(entries[i].visual, _visual))
            {
                RemoveAt(i);
                return;
            }
        }
    }

    internal static void Tick()
    {
        int frame = Time.frameCount;
        float dt = Time.deltaTime;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];

            // 등록된 프레임에는 이미 curve(0)을 적용했으므로 다음 프레임부터 진행한다
            if (entry.registeredFrame == frame) continue;

            // 파괴되었거나 비활성(풀 반환)이면 Unity가 코루틴을 끊던 것과 같이 조용히 뺀다
            if (entry.visual == null || !entry.visual.gameObject.activeInHierarchy)
            {
                RemoveAt(i);
                continue;
            }

            if (entry.elapsed < entry.duration)
            {
                entry.visual.ApplyFlashAmountFromSystem(entry.curve.Evaluate(entry.elapsed / entry.duration));
                entry.elapsed += dt;
                entries[i] = entry;
            }
            else
            {
                entry.visual.ApplyFlashAmountFromSystem(0f);
                RemoveAt(i);
            }
        }
    }

    private static void RemoveAt(int _index)
    {
        int last = entries.Count - 1;
        if (_index != last)
        {
            entries[_index] = entries[last];
        }
        entries.RemoveAt(last);
    }

    private static void EnsureDriver()
    {
        if (driver != null) return;

        GameObject go = new GameObject("TreeFlashSystem");
        // DontSave 계열은 플레이 종료 뒤에도 남아 다음 세션의 새 드라이버와 함께 틱이 두 번 돌 수 있으므로 숨김만 하고 씬 생존은 DontDestroyOnLoad에 맡긴다.
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        driver = go.AddComponent<TreeFlashSystemDriver>();
    }

    /// <summary>
    /// 코루틴이 재개되던 시점(Update 뒤, 렌더 전)과 같은 프레임 구간에서 틱을 돌리는 숨김 드라이버.
    /// </summary>
    private sealed class TreeFlashSystemDriver : MonoBehaviour
    {
        private void Awake()
        {
            // 어떤 경로로든 드라이버가 둘 이상 생기면 나중 것은 스스로 사라진다(틱이 두 번 돌면 플래시가 두 배 빨리 끝난다)
            if (driver != null && driver != this)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (driver != this) return;
            Tick();
        }
    }
}
