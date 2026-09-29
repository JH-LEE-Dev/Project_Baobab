using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whirlwind 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// 프레임은 Resources.LoadAll 대신 호출부(AttackComponent)에서 인스펙터로 연결한 배열을 전달받는다.
/// ShockWave 이펙트와 동일한 Sorting Layer/Order(TagManager "ShockWave" 레이어, 오더 0)를 사용한다.
/// </summary>
public class WhirlwindVFX : MonoBehaviour
{
    private const float FrameRate = 24f;

    // ShockWave.prefab의 SpriteRenderer(m_SortingLayer: 11 = "ShockWave", m_SortingOrder: 0)와 동일하게 맞춘다.
    private const string SortingLayerName = "ShockWave";
    private const int SortingOrder = 0;

    // 판정 범위에 딱 맞춘 크기가 시각적으로 작아 보여서 추가로 곱해주는 배율
    private const float ExtraScaleMultiplier = 1.25f;

    // 3타마다 반복 발동되므로 SporeExplosionVFX와 동일하게 풀링해서 재사용한다.
    // 풀은 static이라 씬 전환 후에도 살아남으므로, 안에 든 오브젝트도 DontDestroyOnLoad로 함께 살려 둔다
    // (CreateInstance 참고). 그렇지 않으면 던전 → 마을 → 던전 재입장 때 파괴된 인스턴스를 꺼내 쓰다 예외가 난다.
    private static readonly Stack<WhirlwindVFX> pool = new Stack<WhirlwindVFX>();

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameTimer;
    private int currentFrame;

    // 프레임 배열이 프리팹에 연결되지 않았을 때 한 번만 경고한다. 그대로 두면 회전·카메라 흔들림만
    // 나가고 스프라이트는 안 보이는데, 콘솔에 아무 단서도 남지 않아 원인을 찾기 어렵다.
    private static bool bMissingFramesWarned = false;

    public static void Spawn(Vector3 _position, float _attackRadius, Sprite[] _frames)
    {
        if (_frames == null || _frames.Length == 0)
        {
            if (!bMissingFramesWarned)
            {
                bMissingFramesWarned = true;
                Debug.LogWarning("[WhirlwindVFX] whirlwindFrames가 비어 있어 회전 베기 스프라이트 연출을 건너뜁니다. AttackComponent 인스펙터에서 Whirlwind VFX 프레임을 연결하세요.");
            }
            return;
        }

        // 풀 오브젝트는 DontDestroyOnLoad라 보통 살아있지만, 외부에서 파괴됐을 가능성에 대비해
        // 파괴된 참조는 버리고 살아있는 것만 재사용한다.
        WhirlwindVFX instance = null;
        while (pool.Count > 0)
        {
            WhirlwindVFX pooled = pool.Pop();
            if (pooled != null)
            {
                instance = pooled;
                break;
            }
        }
        if (instance == null) instance = CreateInstance();

        instance.gameObject.SetActive(true);
        instance.transform.position = _position;
        instance.Play(_attackRadius, _frames);
    }

    private static WhirlwindVFX CreateInstance()
    {
        GameObject go = new GameObject("WhirlwindVFX");
        DontDestroyOnLoad(go); // static 풀과 수명을 맞춘다 (pool 필드 주석 참고)
        WhirlwindVFX instance = go.AddComponent<WhirlwindVFX>();
        instance.spriteRenderer = go.AddComponent<SpriteRenderer>();
        return instance;
    }

    private void Play(float _attackRadius, Sprite[] _frames)
    {
        frames = _frames;
        currentFrame = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;

        // 공격 판정 타원(가로 지름 2*R, 세로 지름 R)과 스프라이트(128x64 = 가로:세로 2:1) 비율이 동일하므로,
        // 균일(x=y) 스케일만으로 스프라이트 가로폭을 판정 범위 가로 지름에 맞추면 세로도 함께 맞는다.
        float spriteWidthUnits = frames[0].bounds.size.x;
        float uniformScale = spriteWidthUnits > 0.0001f ? (_attackRadius * 2f) / spriteWidthUnits : 1f;
        uniformScale *= ExtraScaleMultiplier;
        transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    // DontDestroyOnLoad라 씬이 내려가도 파괴되지 않으므로, 재생 중에 씬이 바뀌면 다음 씬에
    // 이전 씬 좌표로 남아 보이지 않도록 곧바로 풀로 되돌린다. 활성(재생 중)일 때만 구독한다.
    // (프로젝트에 같은 이름의 SceneManager 클래스가 있어 Unity 것은 완전한 이름으로 쓴다.)
    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene _scene)
    {
        ReturnToPool();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                ReturnToPool();
                return;
            }

            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private void ReturnToPool()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
