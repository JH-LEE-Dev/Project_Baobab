using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장식 숨쉬기. 화면에 보이는(컬링으로 켜져 있는) 지정한 장식이 저마다의 박자로 몇 초에 한 번 은은하게 밝아졌다 돌아온다.
/// 박자는 장식 자리로 정해지므로(자리 해시) 장식마다 따로 기억할 것이 없고, 매 프레임 보이는 장식만 훑는다.
/// 장식의 밝기 값(StaticObj HDR 배수)만 바꾸므로 렌더링만 바뀌고 게임 동작에는 영향이 없다.
/// 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고, 다시 켤 때는 이전 것을 지우고 새로 만든다.
/// 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageDecoBreath : MonoBehaviour
{
    private static StageDecoBreath current;

    // //내부 의존성
    private DecoBreathSettings settings;
    private AnimatedObjGenerator generator;
    // 원래보다 밝게 그려지고 있는 장식(사라질 때 원래 밝기로 되돌린다)
    private readonly HashSet<StaticObj> brightDecos = new HashSet<StaticObj>();

    // // 퍼블릭 제어 메서드
    public static void Apply(DecoBreathSettings _settings, AnimatedObjGenerator _generator, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _generator || _settings.amount <= 0f
            || null == _settings.decoPrefabs || 0 == _settings.decoPrefabs.Count || GameModeContext.IsRoguelike)
        {
            return;
        }

        GameObject root = new GameObject("[StageDecoBreath]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageDecoBreath>();
        current.settings = _settings;
        current.generator = _generator;
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void Update()
    {
        if (null == generator)
        {
            return;
        }

        float time = Time.time;
        float minPeriod = Mathf.Max(0.5f, Mathf.Min(settings.periodRange.x, settings.periodRange.y));
        float maxPeriod = Mathf.Max(minPeriod, Mathf.Max(settings.periodRange.x, settings.periodRange.y));
        float sharpness = Mathf.Max(1f, settings.sharpness);

        List<StaticObj> prefabs = settings.decoPrefabs;
        for (int p = 0; p < prefabs.Count; p++)
        {
            List<StaticObj> decos = generator.GetActiveStaticObjects(prefabs[p]);
            if (null == decos)
            {
                continue;
            }

            for (int i = 0; i < decos.Count; i++)
            {
                StaticObj deco = decos[i];
                // 화면 밖 장식은 컬링으로 꺼져 있다.
                if (null == deco || false == deco.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 position = deco.transform.position;
                float period = Mathf.Lerp(minPeriod, maxPeriod, Hash(position.x, position.y, 0.29f));
                float phase = (time / period + Hash(position.x, position.y, 0.71f)) * Mathf.PI * 2f;
                // 사인의 윗부분만 써서 대부분은 원래 밝기, 박자마다 한 번 부드럽게 밝아진다.
                float glow = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), sharpness);

                if (glow > 0.001f)
                {
                    deco.SetHDRMultiplier(1f + settings.amount * glow);
                    brightDecos.Add(deco);
                }
                else if (brightDecos.Remove(deco))
                {
                    deco.SetHDRMultiplier(1f);
                }
            }
        }
    }

    // 장식 자리로 정하는 0~1 값(같은 자리는 늘 같은 박자).
    private static float Hash(float _x, float _y, float _salt)
    {
        float h = Mathf.Sin(_x * 127.1f + _y * 311.7f + _salt * 74.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    private void OnDestroy()
    {
        // 밝게 그리던 장식을 원래 밝기로 되돌린다(장식은 풀에서 다시 쓰인다).
        foreach (StaticObj deco in brightDecos)
        {
            if (null != deco)
            {
                deco.SetHDRMultiplier(1f);
            }
        }
        brightDecos.Clear();

        if (current == this)
        {
            current = null;
        }
    }
}
