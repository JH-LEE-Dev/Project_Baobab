using UnityEngine;

/// <summary>
/// 강아지 머리 위에서 하트가 뽕 하고 생겨 하늘로 살랑살랑 떠오르며 흩어지는 연출.
/// 주인이 상호작용했을 때와 밥을 먹는 동안 나온다.
///
/// 하트 그림(Heart_0~5)은 큰 하트가 작은 하트, 점으로 흩어지는 순서라 떠오르는 동안 차례로 넘긴다.
/// 렌더러는 강아지 자식이지만 생긴 자리(월드)를 기준으로 움직여서, 강아지가 달려가도 따라가지 않는다.
/// </summary>
public class PuppyHeartEffect : MonoBehaviour
{
    private const int PoolSize = 4;

    [SerializeField, Tooltip("Heart_0~5. 큰 하트가 흩어지며 사라지는 순서.")]
    private Sprite[] frames = new Sprite[6];
    [SerializeField, Tooltip("강아지 몸통과 같은 Custom-Sprite-Default.")]
    private Material material;
    [SerializeField] private string sortingLayerName = "FlyingItem";
    [SerializeField] private int sortingOrder = 0;

    [Header("Motion")]
    [SerializeField, Tooltip("발밑 기준으로 하트가 생기는 높이(머리 바로 위).")]
    private float spawnHeight = 0.4f;
    [SerializeField, Tooltip("생기는 자리의 좌우 흔들림(월드).")]
    private float spawnJitterX = 0.06f;
    [SerializeField, Tooltip("생겨서 사라질 때까지의 시간(초).")]
    private float lifetime = 1.1f;
    [SerializeField, Tooltip("떠오르는 높이(월드). 처음엔 빠르고 갈수록 느려진다.")]
    private float riseHeight = 0.55f;
    [SerializeField, Tooltip("떠오르며 좌우로 살랑이는 폭(월드).")]
    private float swayAmplitude = 0.035f;
    [SerializeField, Tooltip("초당 살랑임 횟수.")]
    private float swayFrequency = 1.2f;
    [SerializeField, Tooltip("뽕 하고 커지는 시간(초).")]
    private float popInDuration = 0.15f;
    [SerializeField, Range(0f, 1f), Tooltip("뽕 하고 커지기 시작하는 크기 비율.")]
    private float popInStartScale = 0.4f;
    [SerializeField, Range(0f, 1f), Tooltip("수명 중 이 비율부터 투명해지기 시작한다.")]
    private float fadeStart = 0.75f;

    private class Heart
    {
        public SpriteRenderer renderer;
        public Vector2 origin;
        public float elapsed;
        public float swayPhase;
        public bool bActive;
    }

    private Heart[] hearts;

    private void Awake()
    {
        hearts = new Heart[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject _go = new GameObject("Heart");
            _go.transform.SetParent(transform, false);

            SpriteRenderer _renderer = _go.AddComponent<SpriteRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.sortingLayerName = sortingLayerName;
            _renderer.sortingOrder = sortingOrder;
            _go.SetActive(false);

            hearts[i] = new Heart { renderer = _renderer };
        }
    }

    /// <summary>_feetPosition(강아지 발밑) 머리 위에서 하트 하나를 띄운다. 다 쓰고 있으면 가장 오래된 하트를 다시 쓴다.</summary>
    public void Play(Vector2 _feetPosition)
    {
        if (null == hearts || null == frames || 0 == frames.Length) return;

        Heart _heart = null;
        for (int i = 0; i < hearts.Length; i++)
        {
            if (false == hearts[i].bActive)
            {
                _heart = hearts[i];
                break;
            }

            if (null == _heart || hearts[i].elapsed > _heart.elapsed)
                _heart = hearts[i];
        }

        _heart.origin = _feetPosition + new Vector2(Random.Range(-spawnJitterX, spawnJitterX), spawnHeight);
        _heart.elapsed = 0f;
        _heart.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        _heart.bActive = true;
        _heart.renderer.gameObject.SetActive(true);
        Apply(_heart);
    }

    public void StopAll()
    {
        if (null == hearts) return;

        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].bActive = false;
            hearts[i].renderer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (null == hearts) return;

        float _dt = Time.deltaTime;
        for (int i = 0; i < hearts.Length; i++)
        {
            Heart _heart = hearts[i];
            if (false == _heart.bActive) continue;

            _heart.elapsed += _dt;
            if (_heart.elapsed >= lifetime)
            {
                _heart.bActive = false;
                _heart.renderer.gameObject.SetActive(false);
                continue;
            }

            Apply(_heart);
        }
    }

    private void Apply(Heart _heart)
    {
        float _t = Mathf.Clamp01(_heart.elapsed / lifetime);

        // 그림은 수명에 맞춰 큰 하트 → 작은 하트 → 점 순서로 넘긴다.
        int _frame = Mathf.Min(frames.Length - 1, (int)(_t * frames.Length));
        _heart.renderer.sprite = frames[_frame];

        // 처음엔 빠르게 솟고 갈수록 느려지며, 막 생겼을 땐 흔들림 없이 곧게 올라간다.
        float _inv = 1f - _t;
        float _rise = riseHeight * (1f - _inv * _inv * _inv);
        float _sway = swayAmplitude * Mathf.Sin(_heart.swayPhase + 2f * Mathf.PI * swayFrequency * _heart.elapsed) * Mathf.Clamp01(_t * 3f);
        _heart.renderer.transform.position = new Vector3(_heart.origin.x + _sway, _heart.origin.y + _rise, 0f);

        // 뽕: 작게 생겨 살짝 넘치게 커졌다가 제 크기로 돌아온다.
        float _scale = 1f;
        if (_heart.elapsed < popInDuration)
        {
            float _p = _heart.elapsed / popInDuration - 1f;
            const float Overshoot = 1.70158f;
            float _back = 1f + (Overshoot + 1f) * _p * _p * _p + Overshoot * _p * _p;
            _scale = Mathf.LerpUnclamped(popInStartScale, 1f, _back);
        }

        _heart.renderer.transform.localScale = new Vector3(_scale, _scale, 1f);

        Color _color = _heart.renderer.color;
        _color.a = _t <= fadeStart ? 1f : 1f - (_t - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart);
        _heart.renderer.color = _color;
    }

    private void OnDisable()
    {
        StopAll();
    }
}
