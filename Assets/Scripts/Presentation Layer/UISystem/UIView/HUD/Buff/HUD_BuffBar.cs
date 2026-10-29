using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버프 HUD 한 줄. 아이콘 목록/슬라이드 배치, 입자 오버레이, 저피로도 경고 틴트 동기화를 맡는다.
/// 입자는 한 장의 오버레이 텍스처에 CPU로 그린다(점/+/x 세 모양만, 모양과 톤이 단계적으로 줄어들며 사라진다).
/// </summary>
public class HUD_BuffBar : MonoBehaviour
{
    // 입자 모양
    private const byte KindDot = 0;
    private const byte KindPlus = 1;
    private const byte KindCross = 2;

    // 입자 패턴(0 점, 1 +, 2 x)
    public static readonly byte[] KindsEnter = { KindPlus, KindCross, KindPlus, KindCross };
    public static readonly byte[] KindsBig6 = { KindPlus, KindCross, KindPlus, KindCross, KindPlus, KindCross };
    public static readonly byte[] KindsHit = { KindPlus, KindDot, KindDot, KindDot };
    public static readonly byte[] KindsCross = { KindPlus, KindCross, KindPlus, KindCross, KindPlus };

    private struct FxParticle
    {
        public bool bAlive;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float ax;
        public float ay;
        public float age;
        public float life;
        public byte kind;
        public Color32 color;
    }

    // 외부 의존성
    [Header("UI Ref")]
    [SerializeField] private HUD_BuffIcon iconPrefab;
    [SerializeField] private RectTransform iconRoot;
    [Tooltip("입자 오버레이. 바 왼쪽 아래 기준 (-24, -24)에서 200x88 영역. 레이캐스트는 끈다.")]
    [SerializeField] private RawImage fxOverlay;
    [Tooltip("8칸을 넘길 때 마지막 칸에 놓이는 +N 표시")]
    [SerializeField] private RectTransform chipRoot;
    [SerializeField] private RawImage chipNumber;

    [Header("Icon")]
    [Tooltip("id -> 아이콘 테이블. BuffDisplayInfo.icon이 비어 있으면 여기서 id로 찾아 채운다.")]
    [SerializeField] private HUD_BuffIconTable iconTable;

    [Header("Number Glyph")]
    [Tooltip("숫자 스프라이트. 인덱스 d가 숫자 d (SmallCurrencyNum 시트는 1~9,0 순서이므로 _0~_8이 1~9, _9가 0).")]
    [SerializeField] private Sprite[] digitSprites;

    [Header("Layout")]
    [Tooltip("한 줄의 가로폭(스태미나 바 폭). 첫 줄은 firstRowReserved만큼 오른쪽을 비운다(대시 게이지 자리).")]
    [SerializeField] private float rowWidth = 160f;
    [Tooltip("최대 줄 수. 한 줄이 부족하면 위로 쌓고, 모든 줄이 차면 마지막 칸이 +N이 된다.")]
    [SerializeField] private int maxRows = 2;
    [Tooltip("줄 간 세로 간격(칸 21 + 여백 2)")]
    [SerializeField] private float rowPitchY = 23f;
    [SerializeField] private float slotPitch = 20f;
    [SerializeField] private float slideDuration = 0.3f;
    [Tooltip("이웃 칸이 빈자리로 미끄러져 갈 때의 이징")]
    [SerializeField] private Ease slideEase = Ease.OutBack;
    [SerializeField] private float slideOvershoot = 1.2f;
    [SerializeField] private int poolCapacity = 16;

    // 오버레이 영역(바 로컬 좌표 기준 원점 오프셋)
    private const int FxOriginX = 24;
    private const int FxOriginY = 24;
    private const int FxWidth = 200;
    private const int FxHeight = 112;
    private const int MaxParticles = 512;
    private const int ChipTexWidth = 24;
    private const float IconCellWidth = 18f;
    private const float FxFrameRate = 20f;

    private static readonly Color32 StageHot = new Color32(255, 244, 190, 255);
    private static readonly Color32 StageDark = new Color32(24, 18, 14, 255);

    // 내부 의존성
    private readonly List<HUD_BuffIcon> allIcons = new List<HUD_BuffIcon>(16);
    private readonly List<HUD_BuffIcon> layoutOrder = new List<HUD_BuffIcon>(16);
    private readonly Stack<HUD_BuffIcon> pool = new Stack<HUD_BuffIcon>(16);
    private readonly FxParticle[] particles = new FxParticle[MaxParticles];
    private HUD_Stemina staminaBar;
    private HUD_BuffNumberFont font;
    private Texture2D fxTexture;
    private Color32[] fxBuffer;
    private Texture2D chipTexture;
    private Color32[] chipBuffer;
    private RectTransform chipNumberRect;

    // 상태 변수
    private int particleCursor = 0;
    private int aliveParticles = 0;
    private bool bOverlayDirty = false;
    private float fxClock = 0f;
    private bool bInitialized = false;
    private int chipShown = -1;
    private Color lastTint = Color.white;
    private float firstRowReserved = 0f;

    public float SlideDuration
    {
        get { return slideDuration; }
    }

    public Ease SlideEase
    {
        get { return slideEase; }
    }

    public float SlideOvershoot
    {
        get { return slideOvershoot; }
    }

    public int ActiveCount
    {
        get { return layoutOrder.Count; }
    }

    // 저피로도 경고 틴트를 따라가기 위해 스태미나 바를 주입한다(null이면 동기화하지 않는다).
    public void Initialize(HUD_Stemina _stamina)
    {
        staminaBar = _stamina;

        // 첫 버프가 오기 전에도 Fx 오버레이에 투명 텍스처를 물려 둔다. 비어 있으면 RawImage가 흰 사각형으로 그려진다.
        EnsureInitialized();
    }

    // ===== 공개 API =====

    // 없으면 새로 등장시키고, 있으면 갱신한다(지속형은 가득 차오름).
    public void AddOrUpdate(in BuffDisplayInfo _info)
    {
        if (false == EnsureInitialized())
        {
            return;
        }

        BuffDisplayInfo _resolved = _info;

        if (null == _resolved.icon && null != iconTable)
        {
            _resolved.icon = iconTable.GetIcon(_resolved.id);
        }

        HUD_BuffIcon _icon = FindIcon(_resolved.id);

        if (null != _icon)
        {
            _icon.Refresh(_resolved);
            return;
        }

        _icon = AcquireIcon();

        if (null == _icon)
        {
            Debug.LogWarning("[HUD_BuffBar] 버프 칸이 가득 찼습니다. poolCapacity=" + poolCapacity + " id=" + _resolved.id);
            return;
        }

        layoutOrder.Add(_icon);
        _icon.Begin(_resolved, GetSlotPos(layoutOrder.Count - 1), true);
        _icon.SetTint(lastTint);
        RelayoutAll(false);
    }

    // 스택형은 현재 값, 지속형은 남은 시간(초)을 설정한다.
    public void SetValue(int _id, float _value)
    {
        HUD_BuffIcon _icon = FindIcon(_id);

        if (null != _icon)
        {
            _icon.SetValue(_value);
        }
    }

    // 발동 연출(불꽃 + 번쩍)을 재생한다.
    public void Trigger(int _id)
    {
        HUD_BuffIcon _icon = FindIcon(_id);

        if (null != _icon)
        {
            _icon.Trigger();
        }
    }

    // 연출과 함께 제거한다(번쩍 -> 부풀었다 줄어듦 -> 픽셀이 점으로 흩어짐).
    public void Dispel(int _id)
    {
        HUD_BuffIcon _icon = FindIcon(_id);

        if (null != _icon)
        {
            _icon.BeginExit();
        }
    }

    // 연출 없이 즉시 제거한다.
    public void Remove(int _id)
    {
        HUD_BuffIcon _icon = FindIcon(_id);

        if (null == _icon)
        {
            return;
        }

        layoutOrder.Remove(_icon);
        ReleaseIcon(_icon);
        RelayoutAll(false);
    }

    public void Clear()
    {
        for (int i = allIcons.Count - 1; 0 <= i; i--)
        {
            HUD_BuffIcon _icon = allIcons[i];

            if (true == _icon.gameObject.activeSelf)
            {
                ReleaseIcon(_icon);
            }
        }

        layoutOrder.Clear();

        for (int i = 0; i < MaxParticles; i++)
        {
            particles[i].bAlive = false;
        }

        aliveParticles = 0;
        bOverlayDirty = true;
        RelayoutAll(true);
    }

    // 현재 값을 읽는다(스택형은 스택 값, 지속형은 남은 시간). 없으면 false.
    public bool TryGetValue(int _id, out float _value)
    {
        HUD_BuffIcon _icon = FindIcon(_id);
        _value = null != _icon ? _icon.CurrentValue : 0f;
        return null != _icon;
    }

    public bool Contains(int _id)
    {
        return null != FindIcon(_id);
    }

    // ===== 아이콘이 쓰는 입자 API (좌표: 바 로컬, 왼쪽 아래 원점, y 위쪽) =====

    // 중심에서 퍼지는 점. 처음 7px 떨어진 곳에서 시작해 감속하며 흩어진다.
    public void FxDots(float _cx, float _cy, int _count, Color32 _color, float _life)
    {
        for (int i = 0; i < _count; i++)
        {
            float _ang = Random.Range(0f, 6.2832f);
            float _sp = Random.Range(18f, 36f);
            float _lf = Random.Range(_life * 0.7f, _life);
            float _delay = Random.Range(0f, 0.08f);
            float _cos = Mathf.Cos(_ang);
            float _sin = Mathf.Sin(_ang);
            Emit(_cx + _cos * 7f, _cy + _sin * 7f, _cos * _sp, _sin * _sp,
                -_cos * _sp / _lf, -_sin * _sp / _lf, _lf, _delay, KindDot, _color);
        }
    }

    // +/x/점이 바깥으로 퍼진다. _kinds 길이만큼, 순서대로 0.03초씩 늦게 시작.
    public void FxSparks(float _cx, float _cy, byte[] _kinds, Color32 _color, float _life, float _radMin, float _radMax)
    {
        for (int i = 0; i < _kinds.Length; i++)
        {
            float _ang = Random.Range(0f, 6.2832f);
            float _r = Random.Range(_radMin, _radMax);
            float _cos = Mathf.Cos(_ang);
            float _sin = Mathf.Sin(_ang);
            Emit(_cx + _cos * _r, _cy + _sin * _r, _cos * 14f, _sin * 14f + 5f, 0f, 0f, _life, 0.03f * i, _kinds[i], _color);
        }
    }

    // 위로 튀어 오르는 점(게이지 끝)
    public void FxRise(float _x, float _y, Color32 _color, float _life, float _delay)
    {
        Emit(_x, _y, 0f, 7f, 0f, 28f, _life, _delay, KindDot, _color);
    }

    // 아래로 떨어지는 점
    public void FxFall(float _x, float _y, Color32 _color, float _life, float _delay)
    {
        Emit(_x, _y, 0f, -12f, 0f, -40f, _life, _delay, KindDot, _color);
    }

    // 퇴장 때 아이콘 픽셀 하나가 점으로 흩어진다. _px/_py는 아이콘 픽셀 좌표(왼쪽 위 원점),
    // _originX/_originY는 칸 안에서 아이콘 이미지의 왼쪽 아래 위치다.
    public void FxShard(float _slotX, float _slotY, float _originX, float _originY, int _px, int _py, int _iconW, int _iconH, Color32 _color)
    {
        float _ang = Mathf.Atan2(_py - _iconH * 0.5f, _px - _iconW * 0.5f) + Random.Range(-0.5f, 0.5f);
        float _sp = Random.Range(12f, 34f);
        float _lf = Random.Range(0.6f, 1.0f);
        float _cos = Mathf.Cos(_ang);
        float _sin = Mathf.Sin(_ang);
        float _v = 1.1f * _sp;
        float _a = -1.1f * _sp / _lf;
        // 아이콘 픽셀(위에서 아래)을 로컬 좌표(아래에서 위)로 옮긴다. 아이콘 위치는 칸 안 (_originX, _originY).
        float _x = _slotX + _originX + _px;
        float _y = _slotY + _originY + _iconH - 1 - _py;
        // 화면 y는 아래 방향이 +이므로 sin 부호를 뒤집는다. 위로 8px/s 튀었다가 중력으로 떨어진다.
        Emit(_x, _y, _cos * _v, -_sin * _v + 8f, _cos * _a, -_sin * _a - 60f, _lf, 0f, KindDot, _color);
    }

    public void NotifyIconDone(HUD_BuffIcon _icon)
    {
        layoutOrder.Remove(_icon);
        ReleaseIcon(_icon);
        RelayoutAll(false);
    }

    // ===== 내부 =====

    private bool EnsureInitialized()
    {
        if (true == bInitialized)
        {
            return true;
        }

        if (null == iconPrefab || null == iconRoot || null == fxOverlay)
        {
            Debug.LogWarning("[HUD_BuffBar] 필수 참조가 비어 있습니다. iconPrefab/iconRoot/fxOverlay");
            return false;
        }

        font = new HUD_BuffNumberFont();

        if (false == font.Build(digitSprites))
        {
            Debug.LogWarning("[HUD_BuffBar] 숫자 스프라이트(digitSprites 0~9)를 읽지 못했습니다.");
            return false;
        }

        fxTexture = new Texture2D(FxWidth, FxHeight, TextureFormat.RGBA32, false);
        fxTexture.filterMode = FilterMode.Point;
        fxTexture.wrapMode = TextureWrapMode.Clamp;
        fxTexture.name = "BuffFx";
        fxBuffer = new Color32[FxWidth * FxHeight];
        fxTexture.SetPixels32(fxBuffer);
        fxTexture.Apply(false, false);
        fxOverlay.texture = fxTexture;
        fxOverlay.raycastTarget = false;

        if (null != chipNumber)
        {
            chipTexture = new Texture2D(ChipTexWidth, HUD_BuffNumberFont.GlyphHeight, TextureFormat.RGBA32, false);
            chipTexture.filterMode = FilterMode.Point;
            chipTexture.wrapMode = TextureWrapMode.Clamp;
            chipTexture.name = "BuffChip";
            chipBuffer = new Color32[ChipTexWidth * HUD_BuffNumberFont.GlyphHeight];
            chipNumber.texture = chipTexture;
            chipNumberRect = chipNumber.rectTransform;
        }

        if (null != chipRoot)
        {
            chipRoot.gameObject.SetActive(false);
        }

        bInitialized = true;
        return true;
    }

    private HUD_BuffIcon FindIcon(int _id)
    {
        for (int i = 0; i < layoutOrder.Count; i++)
        {
            HUD_BuffIcon _icon = layoutOrder[i];

            if (_id == _icon.Id && false == _icon.IsExiting)
            {
                return _icon;
            }
        }

        return null;
    }

    private HUD_BuffIcon AcquireIcon()
    {
        if (0 < pool.Count)
        {
            return pool.Pop();
        }

        if (poolCapacity <= allIcons.Count)
        {
            return null;
        }

        HUD_BuffIcon _icon = Instantiate(iconPrefab, iconRoot);
        _icon.Setup(this, font);
        allIcons.Add(_icon);
        return _icon;
    }

    private void ReleaseIcon(HUD_BuffIcon _icon)
    {
        _icon.Release();
        pool.Push(_icon);
    }

    // 첫 줄에서 오른쪽을 비워 둘 폭(대시 게이지가 보일 때). 바뀌면 칸들이 새 자리로 미끄러져 간다.
    public void SetFirstRowReserved(float _width)
    {
        float _w = Mathf.Max(0f, _width);

        if (Mathf.Approximately(_w, firstRowReserved))
        {
            return;
        }

        firstRowReserved = _w;

        if (true == bInitialized)
        {
            RelayoutAll(false);
        }
    }

    // 한 줄에 들어가는 칸 수. 칸 폭(18)과 피치(20) 사이의 여백은 줄 끝에서는 필요 없으므로 더해 계산한다.
    private int GetRowCapacity(int _row)
    {
        float _width = rowWidth - (0 == _row ? firstRowReserved : 0f);
        return Mathf.Max(0, Mathf.FloorToInt((_width + (slotPitch - IconCellWidth)) / slotPitch));
    }

    // 전체 칸 인덱스를 (줄, 열)로 바꿔 로컬 좌표를 돌려준다. 첫 줄이 아래, 다음 줄이 위.
    private Vector2 GetSlotPos(int _index)
    {
        int _remain = _index;

        for (int row = 0; row < maxRows; row++)
        {
            int _cap = GetRowCapacity(row);

            if (_remain < _cap)
            {
                return new Vector2(_remain * slotPitch, row * rowPitchY);
            }

            _remain -= _cap;
        }

        // 모든 줄이 찼을 때는 마지막 줄 끝(+N 칸)에 겹쳐 둔다
        int _last = Mathf.Max(0, maxRows - 1);
        return new Vector2(Mathf.Max(0, GetRowCapacity(_last) - 1) * slotPitch, _last * rowPitchY);
    }

    private int GetTotalCapacity()
    {
        int _total = 0;

        for (int row = 0; row < maxRows; row++)
        {
            _total += GetRowCapacity(row);
        }

        return _total;
    }

    // 퇴장이 끝난 칸이 빠지면 뒤 칸들이 앞으로 슬라이드한다. 한 줄이 차면 윗줄로 올라가고,
    // 모든 줄이 차면 마지막 칸이 +N이 된다.
    private void RelayoutAll(bool _bSnap)
    {
        int _count = layoutOrder.Count;
        int _total = GetTotalCapacity();
        bool _bOverflow = _total < _count;
        int _shown = true == _bOverflow ? Mathf.Max(0, _total - 1) : _count;

        for (int i = 0; i < _count; i++)
        {
            HUD_BuffIcon _icon = layoutOrder[i];
            _icon.SetOverflowHidden(_shown <= i);
            _icon.SetLayoutTarget(GetSlotPos(i), _bSnap);
        }

        if (null == chipRoot)
        {
            return;
        }

        chipRoot.gameObject.SetActive(_bOverflow);

        if (true == _bOverflow)
        {
            chipRoot.anchoredPosition = GetSlotPos(_shown);
            RefreshChip(_count - _shown);
        }
    }

    private void RefreshChip(int _hidden)
    {
        if (_hidden == chipShown || null == chipNumber)
        {
            return;
        }

        chipShown = _hidden;
        int _w = font.Compose(_hidden, true, chipBuffer, ChipTexWidth);
        chipTexture.SetPixels32(chipBuffer);
        chipTexture.Apply(false, false);
        chipNumber.uvRect = new Rect(0f, 0f, (float)_w / ChipTexWidth, 1f);
        chipNumberRect.sizeDelta = new Vector2(_w, HUD_BuffNumberFont.GlyphHeight);
        // 칸 안 프레임(1, 4, 16x16) 가운데
        chipNumberRect.anchoredPosition = new Vector2(1f + Mathf.Floor((16 - _w) * 0.5f), 8f);
    }

    private void Emit(float _x, float _y, float _vx, float _vy, float _ax, float _ay, float _life, float _delay, byte _kind, Color32 _color)
    {
        for (int n = 0; n < MaxParticles; n++)
        {
            int _i = particleCursor;
            particleCursor = (particleCursor + 1) % MaxParticles;

            if (true == particles[_i].bAlive)
            {
                continue;
            }

            particles[_i].bAlive = true;
            particles[_i].x = _x;
            particles[_i].y = _y;
            particles[_i].vx = _vx;
            particles[_i].vy = _vy;
            particles[_i].ax = _ax;
            particles[_i].ay = _ay;
            particles[_i].age = -_delay;
            particles[_i].life = _life;
            particles[_i].kind = _kind;
            particles[_i].color = _color;
            aliveParticles++;
            return;
        }
    }

    private void UpdateParticles(float _dt)
    {
        if (0 == aliveParticles && false == bOverlayDirty)
        {
            return;
        }

        System.Array.Clear(fxBuffer, 0, fxBuffer.Length);
        int _fidx = (int)(fxClock * FxFrameRate);

        for (int i = 0; i < MaxParticles; i++)
        {
            if (false == particles[i].bAlive)
            {
                continue;
            }

            particles[i].age += _dt;
            float _age = particles[i].age;

            if (_age < 0f)
            {
                continue;
            }

            float _k = _age / particles[i].life;

            if (1f <= _k)
            {
                particles[i].bAlive = false;
                aliveParticles--;
                continue;
            }

            float _px = particles[i].x + particles[i].vx * _age + 0.5f * particles[i].ax * _age * _age;
            float _py = particles[i].y + particles[i].vy * _age + 0.5f * particles[i].ay * _age * _age;
            DrawStaged(_px, _py, particles[i].kind, particles[i].color, _k, _fidx);
        }

        fxTexture.SetPixels32(fxBuffer);
        fxTexture.Apply(false, false);
        bOverlayDirty = 0 < aliveParticles;
    }

    // 띡 사라지지 않게: 모양과 톤이 단계적으로 줄어들고 마지막에 깜빡이며 꺼진다.
    private void DrawStaged(float _x, float _y, byte _kind, Color32 _color, float _k, int _fidx)
    {
        bool _bBig = KindDot != _kind;

        if (_k < 0.15f)
        {
            DrawShape(_x, _y, _kind, StageHot);
        }
        else if (_k < 0.40f)
        {
            DrawShape(_x, _y, _kind, _color);
        }
        else if (_k < 0.58f)
        {
            DrawShape(_x, _y, true == _bBig ? KindDot : _kind, _color);
        }
        else if (_k < 0.76f)
        {
            DrawShape(_x, _y, KindDot, MixColor(_color, StageDark, 0.5f));
        }
        else if (_k < 0.90f)
        {
            DrawShape(_x, _y, KindDot, MixColor(_color, StageDark, 0.78f));
        }
        else if (0 == (_fidx % 2))
        {
            DrawShape(_x, _y, KindDot, MixColor(_color, StageDark, 0.9f));
        }
    }

    private void DrawShape(float _x, float _y, byte _kind, Color32 _color)
    {
        int _cx = Mathf.RoundToInt(_x) + FxOriginX;
        int _cy = Mathf.RoundToInt(_y) + FxOriginY;

        PutPixel(_cx, _cy, _color);

        if (KindPlus == _kind)
        {
            PutPixel(_cx - 1, _cy, _color);
            PutPixel(_cx + 1, _cy, _color);
            PutPixel(_cx, _cy - 1, _color);
            PutPixel(_cx, _cy + 1, _color);
        }
        else if (KindCross == _kind)
        {
            PutPixel(_cx - 1, _cy - 1, _color);
            PutPixel(_cx + 1, _cy - 1, _color);
            PutPixel(_cx - 1, _cy + 1, _color);
            PutPixel(_cx + 1, _cy + 1, _color);
        }
    }

    private void PutPixel(int _x, int _y, Color32 _color)
    {
        if (_x < 0 || FxWidth <= _x || _y < 0 || FxHeight <= _y)
        {
            return;
        }

        fxBuffer[_y * FxWidth + _x] = _color;
    }

    private static Color32 MixColor(Color32 _a, Color32 _b, float _k)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(_a.r + (_b.r - _a.r) * _k),
            (byte)Mathf.RoundToInt(_a.g + (_b.g - _a.g) * _k),
            (byte)Mathf.RoundToInt(_a.b + (_b.b - _a.b) * _k),
            255);
    }

    private void ApplyDangerTint()
    {
        Color _tint = Color.white;

        if (null != staminaBar && true == staminaBar.IsWarning)
        {
            _tint = staminaBar.FillTint;
        }

        if (_tint == lastTint)
        {
            return;
        }

        lastTint = _tint;

        for (int i = 0; i < allIcons.Count; i++)
        {
            allIcons[i].SetTint(_tint);
        }
    }

    private void Update()
    {
        if (false == bInitialized)
        {
            return;
        }

        float _dt = Time.deltaTime;
        fxClock += _dt;

        ApplyDangerTint();

        // Tick 중 퇴장이 끝나면 layoutOrder가 바뀌므로 역순으로 순회한다.
        for (int i = layoutOrder.Count - 1; 0 <= i; i--)
        {
            if (i < layoutOrder.Count)
            {
                layoutOrder[i].Tick(_dt, fxClock);
            }
        }

        UpdateParticles(_dt);
    }

    private void OnDestroy()
    {
        if (null != fxTexture)
        {
            Destroy(fxTexture);
        }

        if (null != chipTexture)
        {
            Destroy(chipTexture);
        }
    }
}
