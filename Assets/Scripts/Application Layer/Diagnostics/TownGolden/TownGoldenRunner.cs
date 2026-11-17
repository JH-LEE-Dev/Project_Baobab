// ─────────────────────────────────────────────────────────────────────────────
// 마을 골든(결과 불변 검증) 도구. 에디터 전용이라 빌드에는 컴파일되지 않습니다.
//
// [무엇을 하는가]
// 메인 메뉴 → 마을(이어하기)로 실제 경로 그대로 들어가, 정해진 입력 대본(이동·텐트 열기·탭 넘기기·닫기)을
// 고정 dt(1/60초)·고정 난수로 재생하면서 마을 씬이 로드된 프레임부터 프레임마다 아래 값을 기록합니다.
//   - R    : 게임 화면 전체(UI 포함) 픽셀. Game 창이 매 프레임 그리는 렌더 텍스처를 읽습니다.
//   - W:…  : 렌더러의 켜짐·정렬·트랜스폼·머티리얼·스프라이트·색(파티클은 입자 하나하나까지). 계층 앞 2단계로 묶음.
//   - U:…  : UI 그래픽의 켜짐·색·스프라이트·채움·사각형 꼭짓점·글자, 캔버스·캔버스 그룹. 계층 앞 5단계로 묶음.
//   - P:…  : 강체 위치·속도, 카메라. 계층 앞 2단계로 묶음.
//   - A:…  : 재생 중인 오디오 소스(클립·볼륨·피치·반복). 계층 앞 3단계로 묶음.
//   - RAND : 그 프레임 끝의 Random 상태, Time.time, frameCount
//   - L    : 10프레임마다 세이브와 똑같이 채운 GameSaveData의 JSON(돈·인벤토리·특성·가공 라인·오프로드·운반 중 원목)
// 최적화 전에 "기록"하고, 최적화 뒤에 "비교"합니다. 결과는 Library/TownGolden/<라벨>/ 에만 씁니다.
//
// [실행마다 흔들리는 값]
// Time.unscaledTime·unscaledDeltaTime은 captureDeltaTime을 따르지 않는 실제 시간입니다(실측: 같은 프레임에서 13.99초/14.47초).
// 그래서 이걸 쓰는 UI 트윈·BGM 페이드 등은 같은 코드로 돌려도 매번 조금씩 다릅니다.
// 기준을 두 번 기록해(base, base2) 두 기준이 서로 같은 칸(프레임×묶음)만 비교하고, 흔들린 칸은 "가림"으로 셉니다.
//
// [세이브 보호]
// 마을 진입 전에 SaveManager.bSaveBlockedByUnreadableSave를 켜서 이 세션의 모든 저장(도착 자동저장·포커스·종료)을 막습니다.
// 새 게임·세이브 삭제·출발·메인 메뉴 복귀 경로는 부르지 않습니다. 상태 변경(heavy의 특성·전리품·오프로드 채우기)은 메모리에서만 합니다.
//
// [결정성 전제]
// - 플레이 시작부터 Time.captureDeltaTime = 1/60. 메인 메뉴에서 정확히 StartFrame에 GoToTownScene(false)를 부릅니다.
// - 씬 로드가 끝나는 프레임과 조작이 풀리는 프레임을 머리에 적고, 비교 때 다르면 INVALID(다시 돌릴 것)로 표시합니다.
//   (Time.frameCount를 시드로 쓰는 코드가 많아 프레임 번호까지 같아야 합니다)
// - 첫 씬 로드 전과 매 프레임 PlayerLoop 맨 앞에서 Random.InitState. 에디터가 프레임 사이에 난수를 쓰는 일이 있어서입니다.
// - 파티클 자동 시드는 처음 보는 순간(프레임 맨 앞, 파티클 갱신 직전) 끄고 계층 경로로 만든 시드로 다시 시작합니다.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Diagnostics.TownGolden
{
    [DefaultExecutionOrder(-32000)]
    public class TownGoldenRunner : MonoBehaviour
    {
        // 실행 중 표시. 플레이 중 스크립트가 다시 불리면(파일 수정 → 재컴파일) 하네스가 사라지고 세이브 차단도 풀릴 수 있어,
        // 에디터 쪽(TownGoldenMenu.ReloadGuard)이 이 값을 보고 즉시 세이브를 막고 플레이를 멈춘다.
        public const string KeyRunning = "TownGolden.Running";
        public const string KeyMode = "TownGolden.Mode";          // "record" / "compare"
        public const string KeyLabel = "TownGolden.Label";
        public const string KeyBaseline = "TownGolden.Baseline";  // 비교 기준 라벨. "base|base2"면 두 기준이 같은 칸만 비교
        public const string KeyScenario = "TownGolden.Scenario";  // "save" / "heavy"
        public const string KeySaveImages = "TownGolden.SaveImages";
        public const string KeyEndFrames = "TownGolden.EndFrames";
        // 하네스 자체 진단용 플래그(공백 구분): noCdt noPace noSeed dumpCap. 평소엔 비어 있음.
        public const string KeyDebug = "TownGolden.Debug";
        private static string debugFlags = string.Empty;

        private const float FixedDelta = 1.0f / 60.0f;
        private const int StartFrame = 120;     // 플레이 시작 후 이 프레임에 마을로 출발(페이싱 때문에 벽시계로 약 30초)
        private const double FramePeriodSec = 0.2371; // 프레임마다 벽시계 간격을 이 값으로 맞춘다(unscaledDeltaTime 고정용). 딱 떨어지는 값(0.25)이면 1초 같은 실제 시간 대기가 정확히 4프레임 경계에 걸려 지터로 끝나는 프레임이 갈린다(실측). 한 프레임 일이 이보다 길면 overrun
        private const double HeavyPeriodSec = 2.9137;  // overrun 프레임은 간격을 이 값의 배수로 올림해 맞춘다(덤프·씬 로드 프레임, 실측 최대 약 2초)
        private const int DumpEvery = 60;       // 상세 덤프 간격(프레임)
        private const int LogicEvery = 10;      // L(세이브 데이터) 해시 간격
        private const uint BaseSeed = 0x5EED1234u;
        private const BindingFlags FL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private struct EarlyHook { }
        private struct ParticleHook { }
        private struct PaceHook { }
        private struct ProbeHook { }
        private struct MouseHook { }
        private struct DashHook { }
        private struct RandWords { public int a, b, c, d; }

        // PlayerLoop 단계마다 난수 상태를 찍어 어느 단계에서 소비가 갈리는지 본다(RP:단계 묶음, 바뀐 단계만).
        private static readonly Dictionary<string, ulong> phaseRand = new Dictionary<string, ulong>();
        private static ulong lastRandWord;
        private static RandWords lastRandState;
        private static readonly StringBuilder drawLog = new StringBuilder();

        // 페이싱: n번째 프레임의 시간 갱신(TimeUpdate)이 시작 + n×FramePeriodSec 순간에 오도록 기다린다.
        // unscaledTime/unscaledDeltaTime은 captureDeltaTime을 따르지 않는 실제 시간이라, 이렇게 해야 실제 시간을 쓰는
        // 연출(UI 트윈·BGM 페이드·실시간 대기)이 끝나는 프레임이 실행마다 같아진다(그 콜백이 난수를 쓰면 다른 것들까지 흔들린다).
        private static readonly System.Diagnostics.Stopwatch paceClock = new System.Diagnostics.Stopwatch();
        private static long paceFrames;
        private static int paceOverruns;
        private static double paceWorstSec;
        private static double paceStart;
        private static double paceMaxNormalSec;
        private static int paceTight;
        private static bool paceForceHeavy; // 이번 프레임에 덤프를 썼다(일 시간이 기준 간격 근처라 실행마다 overrun 여부가 갈리지 않게 항상 긴 간격)
        private static readonly StringBuilder paceOverrunFrames = new StringBuilder();

        private static TownGoldenRunner instance;

        private string mode, label, baseline, scenario;
        private bool bSaveImages;
        private int endFrames;
        private string outputFolder;

        private int state;
        private int bootFrame = -1, goFrame = -1, loadedFrame = -1, controlFrame = -1;
        private bool bTownLoaded;
        private string error;

        // 던전 시나리오("d_<ForestType>" 또는 "d_<ForestType>_heavy"): 마을에서 실제 출발 경로(차량 탑승 → 숲 선택 UI 이벤트 → 주행·하늘 연출)로 던전에 들어가 대본을 돈다.
        private bool bDungeon, bDungeonHeavy;
        private MapType dungeonMap;
        private ForestType dungeonForest;
        private bool bDungeonLoaded;
        private int rideFrame = -1, menuFrame = -1, departFrame = -1, dungeonLoadedFrame = -1, dungeonUnlockFrame = -1, dungeonControlFrame = -1;
        private bool bTreeBudgetFixed;
        // 대시 키는 다음 물리 틱에서 실제 시간 0.1초 안에만 받아들여진다(Character.DashRequestLifetime). 골든은 프레임 간격을 실제 시간 0.237초로
        // 맞추므로 Update에서 누르면 다음 프레임 물리 틱 전에 만료된다. 그래서 같은 프레임의 FixedUpdate 맨 앞에서 누른다.
        private static bool bDashQueued;
        // 마우스 고정 위치(화면 중심 기준 픽셀 오프셋). 던전 대본이 조준 방향을 바꿀 때 쓴다.
        private static Vector2 mousePinOffset;

        private readonly StringBuilder frames = new StringBuilder(1 << 20);
        private readonly StringBuilder groupFrames = new StringBuilder(1 << 22);
        private readonly StringBuilder setupLog = new StringBuilder();
        private bool bAudioPreloaded;
        private const int StartupHeavyFrames = 8;
        private const int ScreenTile = 120; // 화면 해시 타일 크기(px). 1080p에서 16x9칸
        private ulong[] tileHash;
        private string[] tileNames;
        private bool bDecoOff;

        // 메인 메뉴 전 효과음 미리 불러오기(PreloadSfxClipsRoutine)는 실제 시간 예산으로 프레임을 나눠서
        // 메뉴가 뜨는 프레임(배경 randomOffset을 뽑는 프레임)이 실행마다 45~55로 달랐다.
        // AudioManager가 생기자마자 같은 클립을 전부 불러 두면 그 루틴은 할 일이 없어 같은 프레임에 끝난다.
        // 불러온 데이터는 원래도 세션 내내 남으므로 재생 결과는 같다.
        private void PreloadAudio()
        {
            AudioManager am = AudioManager.Instance;
            if (null == am) return;
            bAudioPreloaded = true;
            AudioDatabase db = typeof(AudioManager).GetField("database", (System.Reflection.BindingFlags)(4 | 16 | 32)).GetValue(am) as AudioDatabase;
            int n = 0;
            if (null != db && null != db.sounds)
            {
                for (int i = 0; i < db.sounds.Count; i++)
                {
                    AudioData d = db.sounds[i];
                    if (null == d) continue;
                    n += PreloadOne(d.clip);
                    if (null == d.cueData || null == d.cueData.clips) continue;
                    for (int c = 0; c < d.cueData.clips.Count; c++) n += PreloadOne(d.cueData.clips[c]);
                }
            }
            setupLog.Append("audio preloaded frame ").Append(Time.frameCount).Append(" clips ").Append(n).Append('\n');
        }

        // 물결·물풀 장식(DecoSpritePatternAnimator·HomeWaterGrassSway)은 OnEnable에서 첫 대기 시간을 난수로 뽑는다.
        // 씬 로드 중 OnEnable 순서가 실행마다 달라 같은 난수열을 다른 장식이 받아, 깨는 프레임이 실행마다 달랐다
        // (이 둘을 끄면 마을의 코루틴 난수 차이가 전부 사라짐). 로드 직후 경로 순서로 껐다 켜서 첫 대기를 다시 뽑게 한다.
        // 고정 시드로 뽑고 원래 난수 상태로 되돌려, 이 프레임의 나머지 난수는 건드리지 않는다.
        private void RestartDecoAnimators()
        {
            List<MonoBehaviour> list = new List<MonoBehaviour>();
            foreach (HomeWaterGrassSway c in FindObjectsByType<HomeWaterGrassSway>(FindObjectsInactive.Exclude)) if (c.isActiveAndEnabled) list.Add(c);
            foreach (DecoSpritePatternAnimator c in FindObjectsByType<DecoSpritePatternAnimator>(FindObjectsInactive.Exclude)) if (c.isActiveAndEnabled) list.Add(c);
            List<KeyValuePair<string, MonoBehaviour>> keyed = new List<KeyValuePair<string, MonoBehaviour>>(list.Count);
            for (int i = 0; i < list.Count; i++) keyed.Add(new KeyValuePair<string, MonoBehaviour>(PathOf(list[i].transform) + "|" + list[i].GetType().Name, list[i]));
            keyed.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            if (debugFlags.Contains("noDeco"))
            {
                for (int i = 0; i < keyed.Count; i++) keyed[i].Value.enabled = false;
                setupLog.Append("deco off frame ").Append(Time.frameCount).Append(" count ").Append(keyed.Count).Append('\n');
                return;
            }
            UnityEngine.Random.State saved = UnityEngine.Random.state;
            UnityEngine.Random.InitState(unchecked((int)(BaseSeed ^ 0x5EEDDEC0u)));
            for (int i = 0; i < keyed.Count; i++) { keyed[i].Value.enabled = false; keyed[i].Value.enabled = true; }
            UnityEngine.Random.state = saved;
            setupLog.Append("deco restart frame ").Append(Time.frameCount).Append(" count ").Append(keyed.Count).Append('\n');
            BreakDecoSortTies(keyed);
        }

        // 정렬 키(정렬 레이어·순서·정렬 축(0,1,0) 위치 y)가 비트까지 같고 화면에서 겹치는 장식 스프라이트는 엔진이 그리는 순서가
        // 실행마다 뒤바뀌었다(월드 상태는 같은데 갈대가 흔들리는 동안만 겹친 픽셀이 다름). 경로 순서대로 y를 아주 조금(1e-5) 벌려
        // 순서를 고정한다. 기준·비교 모두 같은 하네스라 같은 순서로 그려진다.
        private void BreakDecoSortTies(List<KeyValuePair<string, MonoBehaviour>> _keyed)
        {
            var srs = new List<KeyValuePair<string, SpriteRenderer>>();
            for (int i = 0; i < _keyed.Count; i++)
            {
                SpriteRenderer sr = _keyed[i].Value.GetComponent<SpriteRenderer>();
                if (null != sr) srs.Add(new KeyValuePair<string, SpriteRenderer>(_keyed[i].Key, sr));
            }
            int tied = 0, groups = 0;
            var done = new HashSet<SpriteRenderer>();
            for (int i = 0; i < srs.Count; i++)
            {
                SpriteRenderer a = srs[i].Value;
                if (done.Contains(a)) continue;
                int n = 0;
                for (int j = i + 1; j < srs.Count; j++)
                {
                    SpriteRenderer b = srs[j].Value;
                    if (done.Contains(b)) continue;
                    if (a.sortingLayerID != b.sortingLayerID || a.sortingOrder != b.sortingOrder) continue;
                    if (SortY(a) != SortY(b)) continue;
                    if (false == a.bounds.Intersects(b.bounds)) continue;
                    n++;
                    done.Add(b);
                    Transform tb = b.transform;
                    tb.position = tb.position + new Vector3(0f, 1e-5f * n, 0f);
                    tied++;
                }
                if (0 < n) groups++;
            }
            setupLog.Append("deco sort ties groups ").Append(groups).Append(" nudged ").Append(tied).Append('\n');
        }

        private static float SortY(SpriteRenderer _sr)
        {
            return SpriteSortPoint.Pivot == _sr.spriteSortPoint ? _sr.transform.position.y : _sr.bounds.center.y;
        }

        private static int PreloadOne(AudioClip _clip)
        {
            if (null == _clip || AudioClipLoadType.Streaming == _clip.loadType || AudioDataLoadState.Unloaded != _clip.loadState) return 0;
            _clip.LoadAudioData();
            return 1;
        }
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        private Texture2D readback;
        private EditorWindow gameView;
        private MethodInfo gameViewRender;

        private readonly HashSet<int> seededParticles = new HashSet<int>();
        private readonly List<Renderer> rendererBuffer = new List<Renderer>(8192);
        private readonly List<Graphic> graphicBuffer = new List<Graphic>(4096);
        private readonly List<Canvas> canvasBuffer = new List<Canvas>(64);
        private readonly List<CanvasGroup> canvasGroupBuffer = new List<CanvasGroup>(256);
        private readonly List<Rigidbody2D> bodyBuffer = new List<Rigidbody2D>(1024);
        private readonly List<AudioSource> audioBuffer = new List<AudioSource>(256);
        private readonly List<Camera> cameraBuffer = new List<Camera>(8);
        private readonly List<GameObject> rootBuffer = new List<GameObject>(256);
        private ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[1024];
        private readonly Vector3[] corners = new Vector3[4];
        private GameSaveData logicData;

        // 묶음(그룹) 해시: 이름 → 번호, 이번 프레임 값(0 = 이번 프레임에 없음)
        private readonly Dictionary<string, int> groupIndex = new Dictionary<string, int>();
        private readonly List<string> groupNames = new List<string>();
        private readonly List<ulong> groupValues = new List<ulong>();
        private readonly Dictionary<long, int> transformGroupCache = new Dictionary<long, int>();
        private Acc acc;

        // //실행

        public static void Launch(string _mode, string _label, string _baseline, string _scenario, bool _bSaveImages, int _endFrames)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[TownGolden] 플레이 중에는 시작할 수 없습니다."); return; }
            SessionState.SetString(KeyMode, _mode);
            SessionState.SetString(KeyLabel, _label);
            SessionState.SetString(KeyBaseline, _baseline);
            SessionState.SetString(KeyScenario, _scenario);
            SessionState.SetBool(KeySaveImages, _bSaveImages);
            SessionState.SetInt(KeyEndFrames, _endFrames);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string m = SessionState.GetString(KeyMode, string.Empty);
            if (string.IsNullOrEmpty(m)) return;
            // 한 번만 쓰고 지운다(다음 수동 플레이가 골든으로 돌지 않게).
            SessionState.EraseString(KeyMode);
            SessionState.SetBool(KeyRunning, true);

            debugFlags = SessionState.GetString(KeyDebug, string.Empty);
            if (false == debugFlags.Contains("noCdt")) Time.captureDeltaTime = FixedDelta;
            // 첫 씬의 Awake/Start가 쓰는 난수도 고정한다(첫 PlayerLoop 갱신보다 먼저 불린다).
            UnityEngine.Random.InitState(unchecked((int)BaseSeed));
            GameObject go = new GameObject("[TownGoldenRunner]");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TownGoldenRunner>();
            instance.mode = m;
            instance.bootFrame = Time.frameCount;
            pathCache.Clear();
            paceClock.Reset();
            InstallHooks();
        }

        private static void InstallHooks()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveHooks(ref loop);
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                PlayerLoopSystem sub = loop.subSystemList[i];
                if (typeof(TimeUpdate) == sub.type)
                {
                    var list = new List<PlayerLoopSystem>(sub.subSystemList);
                    list.Insert(0, new PlayerLoopSystem { type = typeof(PaceHook), updateDelegate = PaceTick });
                    sub.subSystemList = list.ToArray();
                    loop.subSystemList[i] = sub;
                }
                else if (typeof(Initialization) == sub.type)
                {
                    var list = new List<PlayerLoopSystem>(sub.subSystemList);
                    list.Insert(0, new PlayerLoopSystem { type = typeof(EarlyHook), updateDelegate = EarlyTick });
                    sub.subSystemList = list.ToArray();
                    loop.subSystemList[i] = sub;
                }
                else if (typeof(PreLateUpdate) == sub.type)
                {
                    var list = new List<PlayerLoopSystem>(sub.subSystemList);
                    int at = list.FindIndex(s => typeof(PreLateUpdate.ParticleSystemBeginUpdateAll) == s.type);
                    if (at < 0) at = 0;
                    list.Insert(at, new PlayerLoopSystem { type = typeof(ParticleHook), updateDelegate = ParticleTick });
                    sub.subSystemList = list.ToArray();
                    loop.subSystemList[i] = sub;
                }
            }
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                PlayerLoopSystem sub = loop.subSystemList[i];
                if (null == sub.subSystemList || typeof(TimeUpdate) == sub.type || typeof(Initialization) == sub.type) continue;
                var list = new List<PlayerLoopSystem>();
                // 마우스 고정: 입력 갱신(PreUpdate) 뒤, 스크립트 Update 앞. 실제 마우스 위치를 읽는 연출(메인 메뉴 배경 패럴랙스 등)을 고정한다.
                if (typeof(Update) == sub.type) list.Add(new PlayerLoopSystem { type = typeof(MouseHook), updateDelegate = MouseTick });
                // 대시 키: 물리 틱(FixedUpdate) 맨 앞에서 눌러 같은 틱에 처리되게 한다(bDashQueued 주석 참고).
                if (typeof(FixedUpdate) == sub.type) list.Add(new PlayerLoopSystem { type = typeof(DashHook), updateDelegate = DashTick });
                foreach (PlayerLoopSystem child in sub.subSystemList)
                {
                    list.Add(child);
                    if (typeof(ParticleHook) == child.type || typeof(MouseHook) == child.type || typeof(DashHook) == child.type) continue;
                    string phase = sub.type.Name + "." + (null != child.type ? child.type.Name : "?");
                    list.Add(new PlayerLoopSystem { type = typeof(ProbeHook), updateDelegate = () => Probe(phase) });
                }
                sub.subSystemList = list.ToArray();
                loop.subSystemList[i] = sub;
            }
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static void RemoveHooks(ref PlayerLoopSystem _loop)
        {
            if (null == _loop.subSystemList) return;
            for (int i = 0; i < _loop.subSystemList.Length; i++)
            {
                PlayerLoopSystem sub = _loop.subSystemList[i];
                if (null == sub.subSystemList) continue;
                var list = new List<PlayerLoopSystem>(sub.subSystemList);
                int removed = list.RemoveAll(s => typeof(EarlyHook) == s.type || typeof(ParticleHook) == s.type || typeof(PaceHook) == s.type || typeof(ProbeHook) == s.type || typeof(MouseHook) == s.type || typeof(DashHook) == s.type);
                if (0 < removed) { sub.subSystemList = list.ToArray(); _loop.subSystemList[i] = sub; }
            }
        }

        private static void UninstallHooks()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveHooks(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static void PaceTick()
        {
            if (null == instance || debugFlags.Contains("noPace")) return;
            if (false == paceClock.IsRunning) { paceClock.Restart(); paceFrames = 0; paceOverruns = 0; paceWorstSec = 0.0; paceStart = 0.0; paceMaxNormalSec = 0.0; paceTight = 0; paceOverrunFrames.Length = 0; return; }
            paceFrames++;
            double now = paceClock.Elapsed.TotalSeconds;
            double work = now - paceStart;
            double target;
            // 시작 직후 프레임(메인 메뉴를 세우는 4프레임 등)은 일 시간이 기준 간격 언저리(240~290ms)라 실행마다 판정이 갈렸다. 항상 긴 간격.
            bool bForceHeavy = paceForceHeavy || Time.frameCount <= StartupHeavyFrames;
            paceForceHeavy = false;
            if (work <= FramePeriodSec && false == bForceHeavy)
            {
                target = paceStart + FramePeriodSec;
                if (work > paceMaxNormalSec) paceMaxNormalSec = work;
                if (work > FramePeriodSec * 0.8) paceTight++;
            }
            else
            {
                // 이번 프레임 일이 간격보다 길었다(덤프·씬 로드). 지금으로 다시 잡으면 그 일 시간(실행마다 다름)이 그대로
                // unscaledDeltaTime이 되므로, HeavyPeriodSec 배수로 올림해 같은 간격을 만든다. 따라잡기도 하지 않는다.
                paceOverruns++;
                if (work > paceWorstSec) paceWorstSec = work;
                double k = Math.Max(1.0, Math.Ceiling(work / HeavyPeriodSec));
                target = paceStart + k * HeavyPeriodSec;
                if (paceOverrunFrames.Length < 4000) paceOverrunFrames.Append(Time.frameCount).Append(':').Append((int)(work * 1000.0)).Append(' ');
            }
            paceStart = target;
            double sleepMs = (target - now) * 1000.0 - 2.0;
            if (sleepMs > 0.0) System.Threading.Thread.Sleep((int)sleepMs);
            while (paceClock.Elapsed.TotalSeconds < target) { }
        }

        private static ulong RandWord()
        {
            UnityEngine.Random.State st = UnityEngine.Random.state;
            RandWords w = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<UnityEngine.Random.State, RandWords>(ref st);
            return ((ulong)(uint)w.a << 32 | (uint)w.b) ^ ((ulong)(uint)w.c << 16) ^ ((ulong)(uint)w.d * 0x9E3779B97F4A7C15UL);
        }

        private static void Probe(string _phase)
        {
            if (null == instance) return;
            ulong h = RandWord();
            if (h == lastRandWord) return;
            lastRandWord = h;
            phaseRand[_phase] = h;
            // 직전 상태에서 몇 번 뽑았는지(xorshift128 한 걸음씩, 최대 4096)
            UnityEngine.Random.State st = UnityEngine.Random.state;
            RandWords cur = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<UnityEngine.Random.State, RandWords>(ref st);
            RandWords w = lastRandState;
            int n = 0;
            while (n < 4096 && (w.a != cur.a || w.b != cur.b || w.c != cur.c || w.d != cur.d))
            {
                uint t = (uint)w.a; t ^= t << 11;
                uint s3 = (uint)w.d;
                w.a = w.b; w.b = w.c; w.c = w.d;
                w.d = (int)(s3 ^ (s3 >> 19) ^ t ^ (t >> 8));
                n++;
            }
            lastRandState = cur;
            if (drawLog.Length < 2000000) drawLog.Append(Time.frameCount).Append(' ').Append(_phase).Append(' ').Append(4096 == n ? "?" : n.ToString()).Append('\n');
        }

        private static void MouseTick()
        {
            if (null == instance) return;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (null == mouse) return;
            Vector2 pin = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + mousePinOffset;
            if (mouse.position.ReadValue() != pin) UnityEngine.InputSystem.LowLevel.InputState.Change(mouse.position, pin);
        }

        private static void DashTick()
        {
            if (null == instance || false == bDashQueued) return;
            bDashQueued = false;
            InputReader r = instance.Reader();
            if (null != r) RaiseEvent(r, "DashKeyPressedEvent");
        }

        // 프레임 맨 앞: 난수 고정.
        private static void EarlyTick()
        {
            if (null == instance) return;
            if (false == instance.bAudioPreloaded) instance.PreloadAudio();
            UnityEngine.Random.InitState(unchecked((int)(BaseSeed + (uint)Time.frameCount * 7919u)));
            lastRandWord = RandWord();
            UnityEngine.Random.State st0 = UnityEngine.Random.state;
            lastRandState = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<UnityEngine.Random.State, RandWords>(ref st0);
            if (drawLog.Length < 2000000) drawLog.Append(Time.frameCount).Append(" T ").Append(Time.timeAsDouble.ToString("R")).Append(' ').Append(Time.timeScale.ToString("R")).Append(' ').Append(Time.deltaTime.ToString("R")).Append(' ').Append(Time.unscaledDeltaTime.ToString("R")).Append('\n');
            if (false == debugFlags.Contains("noSeed")) instance.SeedParticles();
        }

        private static void ParticleTick()
        {
            if (null == instance || debugFlags.Contains("noSeed")) return;
            instance.SeedParticles();
        }

        private void SeedParticles()
        {
            ParticleSystem[] all = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                ParticleSystem ps = all[i];
                if (false == seededParticles.Add(ps.GetInstanceID())) continue;
                if (false == ps.useAutoRandomSeed) continue;
                bool bPlaying = ps.isPlaying;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = StableHash(PathOf(ps.transform));
                if (bPlaying) ps.Play(false);
            }
        }

        private void Start()
        {
            label = SessionState.GetString(KeyLabel, "base");
            baseline = SessionState.GetString(KeyBaseline, "base");
            scenario = SessionState.GetString(KeyScenario, "save");
            bSaveImages = SessionState.GetBool(KeySaveImages, false);
            endFrames = SessionState.GetInt(KeyEndFrames, 1260);
            if (scenario.StartsWith("d_", StringComparison.Ordinal))
            {
                bDungeon = true;
                string forest = scenario.Substring(2);
                if (forest.EndsWith("_heavy", StringComparison.Ordinal)) { bDungeonHeavy = true; forest = forest.Substring(0, forest.Length - 6); }
                dungeonForest = (ForestType)Enum.Parse(typeof(ForestType), forest);
                dungeonMap = (MapType)Enum.Parse(typeof(MapType), forest.Substring(0, forest.LastIndexOf('_')));
            }
            outputFolder = Path.Combine(GetRootFolder(), label);
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(Path.Combine(outputFolder, "dump"));
            acc = new Acc(this);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if ("MainMenuScene" != active) Fail("플레이 시작 씬이 MainMenuScene이 아닙니다: " + active);
            StartCoroutine(EndOfFrameLoop());
        }

        private void OnPlayModeChanged(PlayModeStateChange _s)
        {
            if (PlayModeStateChange.ExitingPlayMode != _s) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Time.captureDeltaTime = 0.0f;
            UninstallHooks();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
        {
            if ("TownScene" == _scene.name && false == bTownLoaded)
            {
                bTownLoaded = true;
                loadedFrame = Time.frameCount;
            }
            if ("DungeonScene" == _scene.name && bDungeon && false == bDungeonLoaded)
            {
                bDungeonLoaded = true;
                dungeonLoadedFrame = Time.frameCount;
                FixDungeonVariance();
            }
        }

        // 던전에서 실행마다 갈리는 것을 고정한다(기준·비교 모두 같은 하네스라 같은 값으로 돈다).
        // - 나무 생성은 실제 시간 예산(treeSpawnFrameTimeBudgetMs)으로 프레임을 나눠 실행마다 나무가 생기는 프레임이 달랐다. 예산을 아주 크게 해 한 번에 만든다.
        // - 지진 연출용 System.Random은 시드 없이 만들어져 실행마다 다르다. 고정 시드로 바꾼다.
        private void FixDungeonVariance()
        {
            InDungeonObjectManager idom = FindAnyObjectByType<InDungeonObjectManager>();
            if (null != idom && false == bTreeBudgetFixed)
            {
                FieldInfo bf = typeof(InDungeonObjectManager).GetField("treeSpawnFrameTimeBudgetMs", FL);
                if (null != bf) { setupLog.Append("tree spawn budget ").Append(bf.GetValue(idom)).Append(" -> 1e9\n"); bf.SetValue(idom, 1e9f); bTreeBudgetFixed = true; }
            }
            FieldInfo qf = typeof(InDungeonObjectManager).GetField("quakeVisualRandom", BindingFlags.Static | BindingFlags.NonPublic);
            if (null != qf)
            {
                try { qf.SetValue(null, new System.Random(unchecked((int)(BaseSeed ^ 0x0BADF00Du)))); setupLog.Append("quake random seeded\n"); }
                catch (Exception ex) { setupLog.Append("quake random seed failed ").Append(ex.GetType().Name).Append('\n'); }
            }
            setupLog.Append("dungeon loaded frame ").Append(Time.frameCount).Append('\n');
        }

        // //대본

        private void Update()
        {
            if (null != error) return;
            try
            {
                switch (state)
                {
                    case 0:
                    {
                        if (Time.frameCount - bootFrame < StartFrame) return;
                        BootStrap bs = FindAnyObjectByType<BootStrap>();
                        SaveManager sm = RealSaveManager();
                        if (null == sm || null == bs || ESaveCheckState.Ready != sm.SaveCheckState) return;
                        typeof(SaveManager).GetField("bSaveBlockedByUnreadableSave", FL).SetValue(sm, true);
                        if (true != (bool)typeof(SaveManager).GetField("bSaveBlockedByUnreadableSave", FL).GetValue(sm)) { Fail("세이브 차단 실패"); return; }
                        goFrame = Time.frameCount;
                        bs.GoToTownScene(false);
                        state = 1;
                        break;
                    }
                    case 1:
                    {
                        if (false == bTownLoaded) { if (Time.frameCount - goFrame > 3000) Fail("마을 로드 시간 초과"); return; }
                        if (false == bDecoOff)
                        {
                            bDecoOff = true;
                            RestartDecoAnimators();
                        }
                        InputManager im = FindAnyObjectByType<InputManager>();
                        if (null == im || null == im.inputReader || im.inputReader.IsMovePaused || null == FindAnyObjectByType<TownSystem>())
                        {
                            if (Time.frameCount - goFrame > 6000) Fail("조작 해제 시간 초과");
                            return;
                        }
                        controlFrame = Time.frameCount;
                        // 이 프레임은 heavy 준비 등으로 일 시간이 기준 간격 언저리(234~262ms)라 실행마다 판정이 갈렸다. 항상 긴 간격.
                        paceForceHeavy = true;
                        SaveManager realSm = RealSaveManager();
                        if (null == realSm || true != (bool)typeof(SaveManager).GetField("bSaveBlockedByUnreadableSave", FL).GetValue(realSm)) { Fail("마을 진입 뒤 세이브 차단이 풀려 있음"); return; }
                        setupLog.Append("save blocked ok, SaveManager count ").Append(FindObjectsByType<SaveManager>(FindObjectsInactive.Include).Length).Append('\n');
                        if ("heavy" == scenario || bDungeonHeavy) SetupHeavy();
                        if (bDungeon)
                        {
                            FixDungeonVariance();
                            state = 10;
                            break;
                        }
                        state = 2;
                        RunScript(0);
                        break;
                    }
                    case 2:
                    {
                        RunScript(Time.frameCount - controlFrame);
                        break;
                    }
                    case 10:
                    {
                        // 차량 탑승(실제로 차에 다가가 상호작용할 때 TownSystem이 부르는 PortalActivated).
                        if (Time.frameCount - controlFrame < 30) return;
                        rideFrame = Time.frameCount;
                        typeof(TownSystem).GetMethod("PortalActivated", FL).Invoke(FindAnyObjectByType<TownSystem>(), null);
                        state = 11;
                        break;
                    }
                    case 11:
                    {
                        // 탑승이 끝나 숲 선택 창(UIView_MenuPopup)이 뜨면 60프레임 뒤에 그 창이 내는 숲 선택 이벤트를 그대로 낸다.
                        UIView_MenuPopup popup = FindAnyObjectByType<UIView_MenuPopup>(FindObjectsInactive.Include);
                        if (menuFrame < 0)
                        {
                            if (null != popup && popup.IsVisible) menuFrame = Time.frameCount;
                            else if (Time.frameCount - rideFrame > 3000) Fail("숲 선택 창 대기 시간 초과");
                            return;
                        }
                        if (Time.frameCount - menuFrame < 60) return;
                        departFrame = Time.frameCount;
                        RaiseEvent(popup, "DungeonSelectedEvent", dungeonMap, dungeonForest);
                        setupLog.Append("depart frame ").Append(departFrame).Append(' ').Append(dungeonMap).Append(' ').Append(dungeonForest).Append('\n');
                        state = 12;
                        break;
                    }
                    case 12:
                    {
                        // 던전 로드 → 하강 연출 → 캐릭터 활성화(HUD 상승, 0.7초 뒤)까지 기다린다.
                        if (false == bDungeonLoaded) { if (Time.frameCount - departFrame > 6000) Fail("던전 로드 시간 초과"); return; }
                        InputManager im = FindAnyObjectByType<InputManager>();
                        InDungeonSystem ids = FindAnyObjectByType<InDungeonSystem>();
                        bool bInDungeon = null != ids && true == (bool)typeof(InDungeonSystem).GetField("bCurrentlyDungeonScene", FL).GetValue(ids);
                        if (dungeonUnlockFrame < 0)
                        {
                            if (null == im || null == im.inputReader || im.inputReader.IsMovePaused || false == bInDungeon)
                            {
                                if (Time.frameCount - dungeonLoadedFrame > 6000) Fail("던전 조작 해제 시간 초과");
                                return;
                            }
                            dungeonUnlockFrame = Time.frameCount;
                        }
                        // 조작이 풀린 뒤 0.7초(PopupUIGoUPCoroutine) 지나 캐릭터 활성화·HUD 상승·스태미나 감소가 시작된다. 넉넉히 60프레임 뒤부터 대본.
                        if (Time.frameCount - dungeonUnlockFrame < 60) return;
                        dungeonControlFrame = Time.frameCount;
                        paceForceHeavy = true;
                        SaveManager dsm = RealSaveManager();
                        if (null == dsm || true != (bool)typeof(SaveManager).GetField("bSaveBlockedByUnreadableSave", FL).GetValue(dsm)) { Fail("던전 진입 뒤 세이브 차단이 풀려 있음"); return; }
                        setupLog.Append("dungeon control frame ").Append(dungeonControlFrame).Append('\n');
                        state = 13;
                        RunDungeonScript(0);
                        break;
                    }
                    case 13:
                    {
                        RunDungeonScript(Time.frameCount - dungeonControlFrame);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Fail("대본 예외(state " + state + "): " + ex);
            }
        }

        // 조작이 풀린 프레임(t=0)부터의 입력 대본. 키보드 입력과 같은 이벤트(InputReader.MoveEvent 등)를 직접 발행한다.
        private void RunScript(int _t)
        {
            if (debugFlags.Contains("tentFirst"))
            {
                if (30 == _t) Tent(true);
                if (_t >= endFrames) state = 3;
                return;
            }
            switch (_t)
            {
                case 30: Move(1, 0); break;
                case 120: Move(0, 1); break;
                case 210: Move(-1, 0); break;
                case 330: Move(0, -1); break;
                case 420: Move(0, 0); break;
                case 480: Tent(true); break;
                case 560: TabShift(1); break;
                case 640: TabShift(1); break;
                case 720: TabShift(-1); break;
                case 800: Tent(false); break;
                case 860: Move(-1, -1); break;
                case 960: Move(1, 1); break;
                case 1060: Move(0, 0); break;
            }
            if ("heavy" == scenario && 0 < _t && 0 == _t % 300) FillOffroad();
            if (_t >= endFrames) state = 3;
        }

        // 던전 대본(t=0은 캐릭터 활성화 뒤). 도끼는 누른 채로 두면 쿨타임마다 다시 휘두르므로 처음에 눌러 끝까지 둔다.
        // 120프레임마다 8방향을 돌며 걷고, 조준(마우스 고정 위치)은 걷는 방향으로, 240프레임마다 대시.
        private static readonly Vector2Int[] DungeonDirs =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        };

        private void RunDungeonScript(int _t)
        {
            InputReader r = Reader();
            int stopAt = endFrames - 90;
            if (0 == _t)
            {
                mousePinOffset = new Vector2(220f, 0f);
                if (null != r) RaiseEvent(r, "MouseClickEvent");
            }
            if (30 <= _t && _t < stopAt && 0 == (_t - 30) % 120)
            {
                Vector2Int d = DungeonDirs[((_t - 30) / 120 * 3) % DungeonDirs.Length];
                Move(d.x, d.y);
                mousePinOffset = new Vector2(d.x, d.y).normalized * 220f;
            }
            if (100 <= _t && _t < stopAt && 0 == (_t - 100) % 240) bDashQueued = true;
            if (stopAt == _t)
            {
                Move(0, 0);
                if (null != r) RaiseEvent(r, "MouseReleaseEvent");
            }
            if (_t >= endFrames) state = 3;
        }

        // 진짜 세이브 매니저는 BootStrap에 붙은 것이다. GameInstaller 프리팹에도 SaveManager 컴포넌트가 하나 더 있어
        // (초기화되지 않음) FindAnyObjectByType은 실행마다 어느 쪽을 집을지 정해지지 않는다(실측: 로직 해시가 실행마다 달랐음).
        private static SaveManager RealSaveManager()
        {
            BootStrap bs = FindAnyObjectByType<BootStrap>();
            return null != bs ? bs.GetComponent<SaveManager>() : null;
        }

        private InputReader Reader()
        {
            InputManager im = FindAnyObjectByType<InputManager>();
            return null != im ? im.inputReader : null;
        }

        private static void RaiseEvent(object _target, string _eventField, params object[] _args)
        {
            FieldInfo f = _target.GetType().GetField(_eventField, FL);
            Delegate d = null != f ? f.GetValue(_target) as Delegate : null;
            if (null != d) d.DynamicInvoke(_args);
        }

        private void Move(int _x, int _y)
        {
            InputReader r = Reader();
            if (null != r) RaiseEvent(r, "MoveEvent", new Vector2(_x, _y));
        }

        private void TabShift(int _dir)
        {
            InputReader r = Reader();
            if (null != r) RaiseEvent(r, "UITabShiftEvent", _dir);
        }

        private void Tent(bool _bOpen)
        {
            TownSystem ts = FindAnyObjectByType<TownSystem>();
            typeof(TownSystem).GetMethod("TentInteract", FL).Invoke(ts, new object[] { _bOpen });
        }

        // 가장 무거운 마을: 모든 특성 최대, 전리품 기둥 4개, 오프로드 가득, 캐릭터를 가공 라인 앞으로.
        private void SetupHeavy()
        {
            SkillManager skm = FindAnyObjectByType<SkillManager>();
            MethodInfo apply = typeof(SkillManager).GetMethod("TryApplySkillWithoutCost", FL);
            var map = (IDictionary)typeof(SkillManager).GetField("skillNodeMap", FL).GetValue(skm);
            var keys = new ArrayList(map.Keys);
            for (int pass = 0; pass < 60; pass++)
            {
                bool bAny = false;
                foreach (object k in keys)
                {
                    object node = map[k];
                    Type nt = node.GetType();
                    int cur = (int)nt.GetField("currentLevel").GetValue(node);
                    int max = (int)nt.GetField("maxLevel").GetValue(node);
                    if (cur >= max) continue;
                    apply.Invoke(skm, new object[] { k });
                    if ((int)nt.GetField("currentLevel").GetValue(node) > cur) bAny = true;
                }
                if (false == bAny) break;
            }

            InDungeonObjectManager idom = FindAnyObjectByType<InDungeonObjectManager>();
            idom.bHasAcquiredLostAndFoundBox = true;
            idom.bHasAcquiredSporePotion = true;
            idom.bHasAcquiredStarCompass = true;
            idom.bHasAcquiredObsidianCharm = true;
            LootPillarManager lpm = FindAnyObjectByType<LootPillarManager>();
            var pillars = (IList)typeof(LootPillarManager).GetField("spawnedPillars", FL).GetValue(lpm);
            foreach (object o in pillars) if (o is Component c && null != c) Destroy(c.gameObject);
            lpm.SpawnAcquiredPillars(idom);
            FindAnyObjectByType<TownSystem>().ApplyLootPillarColliderState(idom);

            LogContainer lc = FindAnyObjectByType<LogContainer>();
            ShopNPC shop = FindAnyObjectByType<ShopNPC>();
            Character ch = FindAnyObjectByType<Character>();
            if (null != lc && null != shop && null != ch)
            {
                Vector3 p = (lc.transform.position + shop.transform.position) * 0.5f + new Vector3(0f, -1.5f, 0f);
                ch.transform.position = new Vector3(p.x, p.y, ch.transform.position.z);
                Rigidbody2D rb = ch.GetComponent<Rigidbody2D>();
                if (null != rb) rb.position = ch.transform.position;
            }
            FillOffroad();
            setupLog.Append("heavy setup done, pillars ").Append(pillars.Count).Append('\n');
        }

        private void FillOffroad()
        {
            OffroadContainer oc = FindAnyObjectByType<OffroadContainer>();
            if (null == oc) return;
            int slots = (int)typeof(OffroadContainer).GetField("currentSlotCount", FL).GetValue(oc);
            int per = (int)typeof(OffroadContainer).GetField("maxItemsPerSlot", FL).GetValue(oc);
            var data = new InventorySaveData();
            oc.PopulateSaveData(ref data);
            TreeType[] types = { TreeType.OakTree, TreeType.PineTree, TreeType.BirchTree, TreeType.SporepuffTree, TreeType.StarrootTree, TreeType.CinderTree };
            LogState[] states = { LogState.Normal, LogState.Fascinating, LogState.Advanced, LogState.Perfect, LogState.Normal, LogState.Damaged };
            data.slots = new List<InventorySlotSaveData>();
            for (int i = 0; i < slots; i++)
            {
                var s = new InventorySlotSaveData();
                s.itemSaveData = new ItemSaveData { itemType = ItemType.Log, treeType = types[i % types.Length], logState = states[i % states.Length], color = Color.white };
                s.totalCount = per;
                s.treeTypeCounts = new int[(int)TreeType.Max];
                s.treeTypeCounts[(int)s.itemSaveData.treeType] = per;
                data.slots.Add(s);
            }
            oc.LoadSaveData(data);
        }

        // //기록

        private IEnumerator EndOfFrameLoop()
        {
            while (true)
            {
                yield return endOfFrame;
                if (null != error) { Finish(); yield break; }
                if (false == bTownLoaded) { phaseRand.Clear(); continue; }

                int frame = Time.frameCount;
                bool bDump = 0 == (frame - loadedFrame) % DumpEvery;
                // 덤프 프레임은 어차피 긴 간격이라 여기서 GC를 미리 돌린다. 보통 프레임에 300ms급 GC가 끼면 그 프레임이 긴 간격이 되어
                // 다음 프레임의 unscaledDeltaTime이 기준과 달라지고, 실제 시간으로 움직이는 텐트 UI 연출이 통째로 어긋났다.
                if (bDump) { paceForceHeavy = true; System.GC.Collect(); }
                StringBuilder dump = bDump ? new StringBuilder(1 << 18) : null;
                for (int i = 0; i < groupValues.Count; i++) groupValues[i] = 0UL;
                foreach (KeyValuePair<string, ulong> kv in phaseRand) SetGroup("RP:" + kv.Key, kv.Value);
                phaseRand.Clear();
                acc.dump = dump;

                ulong r = debugFlags.Contains("noCap") ? 0UL : bDump || false == debugFlags.Contains("dumpCap") ? CaptureScreen(frame, bDump) : 0UL;
                ulong w = HashWorld();
                ulong u = HashUI();
                ulong p = HashPhysics();
                ulong a = HashAudio();
                ulong rand = HashRandom();
                string l = 0 == (frame - loadedFrame) % LogicEvery ? HashLogic().ToString("x16") : "-";
                frames.Append(frame).Append(' ').Append(r.ToString("x16")).Append(' ').Append(w.ToString("x16")).Append(' ')
                    .Append(u.ToString("x16")).Append(' ').Append(p.ToString("x16")).Append(' ').Append(a.ToString("x16")).Append(' ')
                    .Append(rand.ToString("x16")).Append(' ').Append(l).Append('\n');
                groupFrames.Append(frame);
                for (int i = 0; i < groupValues.Count; i++)
                {
                    if (0UL == groupValues[i]) continue;
                    groupFrames.Append(' ').Append(i).Append('=').Append(groupValues[i].ToString("x16"));
                }
                groupFrames.Append('\n');
                acc.dump = null;
                if (null != dump) File.WriteAllText(Path.Combine(outputFolder, "dump", scenario + "_" + frame.ToString("D5") + ".txt"), dump.ToString());

                if (3 == state) { Finish(); yield break; }
            }
        }

        // 프레임 끝에서 Game 창의 PlayModeView.RenderView를 직접 불러 지금 상태를 그리게 하고 그 렌더 텍스처를 읽는다
        // (카메라 스택 + Screen Space Overlay UI까지 실제 Game 창과 같은 경로).
        // ScreenCapture·m_TargetTexture만 읽기는 에디터가 포커스를 잃으면 창이 다시 그려지지 않아 옛 화면이 나오고(실측),
        // CaptureScreenshotIntoRenderTexture는 스텐실로 가리는 UI를 빠뜨린다(실측).
        private ulong CaptureScreen(int _frame, bool _bImage)
        {
            if (null == gameView)
            {
                Type gvType = Type.GetType("UnityEditor.GameView,UnityEditor");
                UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gvType);
                if (0 < views.Length) gameView = (EditorWindow)views[0];
                gameViewRender = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView").GetMethod("RenderView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            RenderTexture rt = null != gameView && null != gameViewRender
                ? gameViewRender.Invoke(gameView, new object[] { new Vector2(-100000f, -100000f), false }) as RenderTexture : null;
            if (null == rt) { acc.Begin("R"); acc.S("no game view"); return acc.h; }
            if (null == readback || readback.width != rt.width || readback.height != rt.height)
            {
                if (null != readback) Destroy(readback);
                readback = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            RenderTexture.active = prev;
            NativeArray<ulong> words = readback.GetRawTextureData<ulong>();
            ulong hash = 14695981039346656037UL ^ ((ulong)rt.width << 32 | (uint)rt.height);
            for (int i = 0; i < words.Length; i++) { hash ^= words[i]; hash *= 1099511628211UL; hash ^= hash >> 29; }

            // 화면을 타일로 나눠 따로 해시한다. 정렬 순서가 같은 스프라이트끼리 겹친 곳은 엔진이 그리는 순서가 실행마다
            // 바뀔 수 있어(월드 상태 덤프는 같은데 물풀 한 무더기 픽셀만 다름) 기준끼리 다른 타일만 가리고 나머지 화면은 계속 비교한다.
            int wordsPerRow = rt.width / 2;
            int cols = (rt.width + ScreenTile - 1) / ScreenTile;
            int rows = (rt.height + ScreenTile - 1) / ScreenTile;
            if (null == tileHash || tileHash.Length != cols * rows) tileHash = new ulong[cols * rows];
            for (int i = 0; i < tileHash.Length; i++) tileHash[i] = 14695981039346656037UL ^ (ulong)i;
            int wordsPerTile = ScreenTile / 2;
            for (int y = 0; y < rt.height; y++)
            {
                int rowBase = y * wordsPerRow;
                int tileRow = (y / ScreenTile) * cols;
                for (int x = 0; x < wordsPerRow; x++)
                {
                    int t = tileRow + x / wordsPerTile;
                    ulong th = tileHash[t] ^ words[rowBase + x];
                    th *= 1099511628211UL; th ^= th >> 29;
                    tileHash[t] = th;
                }
            }
            if (null == tileNames || tileNames.Length != tileHash.Length)
            {
                tileNames = new string[tileHash.Length];
                for (int ty = 0; ty < rows; ty++)
                    for (int tx = 0; tx < cols; tx++)
                        tileNames[ty * cols + tx] = "R:x" + (tx * ScreenTile).ToString("D4") + ",y" + (ty * ScreenTile).ToString("D4");
            }
            for (int i = 0; i < tileHash.Length; i++) SetGroup(tileNames[i], tileHash[i]);
            if (bSaveImages && _bImage)
            {
                readback.Apply(false);
                File.WriteAllBytes(Path.Combine(outputFolder, "dump", scenario + "_" + _frame.ToString("D5") + ".png"), readback.EncodeToPNG());
            }
            return hash;
        }

        private void CollectRoots()
        {
            rootBuffer.Clear();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded) rootBuffer.AddRange(s.GetRootGameObjects());
            }
            // DontDestroyOnLoad 씬(이 러너 자신이 있는 씬). 러너는 빼고.
            foreach (GameObject g in gameObject.scene.GetRootGameObjects()) if (g != gameObject) rootBuffer.Add(g);
        }

        private ulong HashWorld()
        {
            acc.Begin("W");
            CollectRoots();
            for (int ri = 0; ri < rootBuffer.Count; ri++)
            {
                rendererBuffer.Clear();
                rootBuffer[ri].GetComponentsInChildren(true, rendererBuffer);
                for (int i = 0; i < rendererBuffer.Count; i++)
                {
                    Renderer rd = rendererBuffer[i];
                    acc.Group(rd.transform, 'W', 2);
                    bool bVisible = rd.enabled && rd.gameObject.activeInHierarchy;
                    if (null != acc.dump) acc.Line("W " + PathOf(rd.transform) + " [" + rd.GetType().Name + "]");
                    acc.B(bVisible);
                    if (false == bVisible) continue;
                    acc.I(rd.sortingLayerID); acc.I(rd.sortingOrder);
                    Matrix4x4 m = rd.localToWorldMatrix;
                    for (int k = 0; k < 16; k++) acc.F(m[k]);
                    Material mat = rd.sharedMaterial;
                    acc.S(null != mat ? mat.name : "-");
                    if (rd is SpriteRenderer sr)
                    {
                        acc.S(null != sr.sprite ? sr.sprite.name : "-");
                        acc.C(sr.color); acc.B(sr.flipX); acc.B(sr.flipY);
                        acc.I((int)sr.drawMode); acc.F(sr.size.x); acc.F(sr.size.y); acc.I((int)sr.maskInteraction);
                    }
                    else if (rd is ParticleSystemRenderer)
                    {
                        ParticleSystem ps = rd.GetComponent<ParticleSystem>();
                        if (null != ps) MixParticles(ps);
                    }
                    else if (rd is LineRenderer lr)
                    {
                        acc.I(lr.positionCount);
                        for (int k = 0; k < lr.positionCount; k++) { Vector3 v = lr.GetPosition(k); acc.F(v.x); acc.F(v.y); acc.F(v.z); }
                    }
                }
            }
            return acc.End();
        }

        private void MixParticles(ParticleSystem _ps)
        {
            int count = _ps.particleCount;
            acc.I(count);
            if (0 == count) return;
            if (particleBuffer.Length < count) particleBuffer = new ParticleSystem.Particle[Mathf.Max(count, particleBuffer.Length * 2)];
            int read = _ps.GetParticles(particleBuffer, count);
            for (int i = 0; i < read; i++)
            {
                ParticleSystem.Particle p = particleBuffer[i];
                Vector3 pos = p.position; Vector3 rot = p.rotation3D; Vector3 size = p.GetCurrentSize3D(_ps);
                acc.F(pos.x); acc.F(pos.y); acc.F(pos.z); acc.F(rot.x); acc.F(rot.y); acc.F(rot.z);
                acc.F(size.x); acc.F(size.y); acc.F(size.z);
                Color32 c = p.GetCurrentColor(_ps);
                acc.U(((ulong)c.r << 24) | ((ulong)c.g << 16) | ((ulong)c.b << 8) | c.a);
                acc.F(p.remainingLifetime);
            }
        }

        private ulong HashUI()
        {
            acc.Begin("U");
            CollectRoots();
            for (int ri = 0; ri < rootBuffer.Count; ri++)
            {
                GameObject root = rootBuffer[ri];
                canvasBuffer.Clear();
                root.GetComponentsInChildren(true, canvasBuffer);
                for (int i = 0; i < canvasBuffer.Count; i++)
                {
                    Canvas c = canvasBuffer[i];
                    acc.Group(c.transform, 'U', 5);
                    if (null != acc.dump) acc.Line("C " + PathOf(c.transform));
                    acc.B(c.enabled && c.gameObject.activeInHierarchy); acc.I(c.sortingOrder); acc.I(c.sortingLayerID); acc.I((int)c.renderMode); acc.F(c.scaleFactor);
                }
                canvasGroupBuffer.Clear();
                root.GetComponentsInChildren(true, canvasGroupBuffer);
                for (int i = 0; i < canvasGroupBuffer.Count; i++)
                {
                    CanvasGroup g = canvasGroupBuffer[i];
                    acc.Group(g.transform, 'U', 5);
                    if (null != acc.dump) acc.Line("G " + PathOf(g.transform));
                    acc.B(g.enabled && g.gameObject.activeInHierarchy); acc.F(g.alpha); acc.B(g.interactable); acc.B(g.blocksRaycasts);
                }
                graphicBuffer.Clear();
                root.GetComponentsInChildren(true, graphicBuffer);
                for (int i = 0; i < graphicBuffer.Count; i++)
                {
                    Graphic g = graphicBuffer[i];
                    acc.Group(g.transform, 'U', 5);
                    bool bVisible = g.enabled && g.gameObject.activeInHierarchy;
                    if (null != acc.dump) acc.Line("UI " + PathOf(g.transform) + " [" + g.GetType().Name + "]");
                    acc.B(bVisible);
                    if (false == bVisible) continue;
                    acc.C(g.color);
                    // materialForRendering은 쓰지 않는다: 마스크·스텐실 수정자(IMaterialModifier)의 GetModifiedMaterial이
                    // 새 스텐실 머티리얼을 만들고 지금 그리던 것을 StencilMaterial.Remove로 해제해 다음 프레임 그 UI가 사라진다
                    // (실측: 천막 원형 공개 스텐실). CanvasRenderer에 실제로 걸린 머티리얼만 읽는다.
                    CanvasRenderer cr = g.canvasRenderer;
                    Material mat = 0 < cr.materialCount ? cr.GetMaterial(0) : null;
                    acc.S(null != mat ? mat.name : "-");
                    Texture tex = g.mainTexture;
                    acc.S(null != tex ? tex.name : "-");
                    g.rectTransform.GetWorldCorners(corners);
                    for (int k = 0; k < 4; k++) { acc.F(corners[k].x); acc.F(corners[k].y); acc.F(corners[k].z); }
                    if (g is Image img)
                    {
                        acc.S(null != img.sprite ? img.sprite.name : "-");
                        acc.I((int)img.type); acc.F(img.fillAmount); acc.I((int)img.fillMethod); acc.B(img.preserveAspect);
                    }
                    else if (g is TMPro.TMP_Text tmp)
                    {
                        acc.S(tmp.text); acc.F(tmp.fontSize); acc.S(null != tmp.font ? tmp.font.name : "-"); acc.I(tmp.maxVisibleCharacters);
                    }
                    else if (g is Text txt)
                    {
                        acc.S(txt.text); acc.I(txt.fontSize);
                    }
                    else if (g is RawImage raw)
                    {
                        Rect uv = raw.uvRect; acc.F(uv.x); acc.F(uv.y); acc.F(uv.width); acc.F(uv.height);
                    }
                }
            }
            return acc.End();
        }

        private ulong HashPhysics()
        {
            acc.Begin("P");
            CollectRoots();
            for (int ri = 0; ri < rootBuffer.Count; ri++)
            {
                GameObject root = rootBuffer[ri];
                bodyBuffer.Clear();
                root.GetComponentsInChildren(true, bodyBuffer);
                for (int i = 0; i < bodyBuffer.Count; i++)
                {
                    Rigidbody2D rb = bodyBuffer[i];
                    acc.Group(rb.transform, 'P', 2);
                    if (null != acc.dump) acc.Line("RB " + PathOf(rb.transform));
                    acc.B(rb.gameObject.activeInHierarchy); acc.B(rb.simulated);
                    Vector2 p = rb.position; Vector2 v = rb.linearVelocity;
                    acc.F(p.x); acc.F(p.y); acc.F(rb.rotation); acc.F(v.x); acc.F(v.y);
                }
                cameraBuffer.Clear();
                root.GetComponentsInChildren(true, cameraBuffer);
                for (int i = 0; i < cameraBuffer.Count; i++)
                {
                    Camera cam = cameraBuffer[i];
                    acc.Group(cam.transform, 'P', 2);
                    if (null != acc.dump) acc.Line("CAM " + PathOf(cam.transform));
                    acc.B(cam.enabled && cam.gameObject.activeInHierarchy);
                    Vector3 cp = cam.transform.position; acc.F(cp.x); acc.F(cp.y); acc.F(cp.z); acc.F(cam.orthographicSize);
                }
            }
            return acc.End();
        }

        private ulong HashAudio()
        {
            acc.Begin("A");
            CollectRoots();
            for (int ri = 0; ri < rootBuffer.Count; ri++)
            {
                audioBuffer.Clear();
                rootBuffer[ri].GetComponentsInChildren(true, audioBuffer);
                for (int i = 0; i < audioBuffer.Count; i++)
                {
                    AudioSource s = audioBuffer[i];
                    acc.Group(s.transform, 'A', 3);
                    if (null != acc.dump) acc.Line("AU " + PathOf(s.transform));
                    bool bPlaying = s.isPlaying;
                    acc.B(bPlaying);
                    if (false == bPlaying) continue;
                    acc.S(null != s.clip ? s.clip.name : "-"); acc.F(s.volume); acc.F(s.pitch); acc.B(s.loop);
                }
            }
            return acc.End();
        }

        private ulong HashRandom()
        {
            acc.Begin("RAND");
            acc.GroupName("RAND");
            if (null != acc.dump) acc.Line("RANDOM/TIME");
            acc.S(JsonUtility.ToJson(UnityEngine.Random.state)); acc.F(Time.time); acc.I(Time.frameCount);
            return acc.End();
        }

        // 세이브와 같은 순서로 GameSaveData를 채운다(SaveManager.SaveGameData의 1~8-2단계, 파일·클라우드 쓰기는 없음).
        private ulong HashLogic()
        {
            acc.Begin("L");
            acc.GroupName("L");
            SaveManager sm = RealSaveManager();
            if (null == sm) { acc.S("no SaveManager"); return acc.End(); }
            if (null == logicData) logicData = new GameSaveData();
            logicData.Clear();
            var skillSystem = (SkillSystem)typeof(SaveManager).GetField("skillSystem", FL).GetValue(sm);
            var inventoryManager = (InventoryManager)typeof(SaveManager).GetField("inventoryManager", FL).GetValue(sm);
            var logProcessingManager = (LogProcessingManager)typeof(SaveManager).GetField("logProcessingManager", FL).GetValue(sm);
            var offroadContainer = (OffroadContainer)typeof(SaveManager).GetField("offroadContainer", FL).GetValue(sm);
            var idom = (InDungeonObjectManager)typeof(SaveManager).GetField("inDungeonObjectManager", FL).GetValue(sm);
            var townUnitSpawner = (TownUnitSpawner)typeof(SaveManager).GetField("townUnitSpawner", FL).GetValue(sm);

            if (null != skillSystem && null != skillSystem.skillManager) skillSystem.skillManager.PopulateSkillSaveData(ref logicData.skillTreeSaveData);
            if (null != inventoryManager) inventoryManager.PopulateInventorySaveData(ref logicData.inventorySaveData);
            if (null != logProcessingManager) logProcessingManager.PopulateSaveData(ref logicData.logProcessingSaveData);
            if (null != offroadContainer) offroadContainer.PopulateSaveData(ref logicData.offroadContainerSaveData);
            if (null != idom)
            {
                logicData.bHasAcquiredLostAndFoundBox = idom.bHasAcquiredLostAndFoundBox;
                logicData.bHasAcquiredSporePotion = idom.bHasAcquiredSporePotion;
                logicData.bHasAcquiredStarCompass = idom.bHasAcquiredStarCompass;
                logicData.bHasAcquiredObsidianCharm = idom.bHasAcquiredObsidianCharm;
            }
            if (null != logProcessingManager) logProcessingManager.AppendTransitToSaveData(ref logicData.logProcessingSaveData);
            if (null != offroadContainer)
            {
                int characterMaxPerSlot = null != inventoryManager ? inventoryManager.GetMaxItemsPerSlot() : 0;
                int logContainerMaxPerSlot = null != logProcessingManager ? logProcessingManager.GetContainerMaxItemsPerSlot() : 0;
                IReadOnlyList<OffroadPorterNPC> porters = null != townUnitSpawner ? townUnitSpawner.NPCs : null;
                offroadContainer.AppendTransitToSaveData(ref logicData.offroadContainerSaveData, ref logicData.inventorySaveData,
                    ref logicData.logProcessingSaveData.containerInventoryData, characterMaxPerSlot, logContainerMaxPerSlot, porters);
            }
            string json = JsonUtility.ToJson(logicData);
            if (null != acc.dump) acc.Line("LOGIC " + json);
            acc.S(json);
            return acc.End();
        }

        // //묶음

        private int GroupIndexOf(string _name)
        {
            if (groupIndex.TryGetValue(_name, out int idx)) return idx;
            idx = groupNames.Count;
            groupIndex[_name] = idx;
            groupNames.Add(_name);
            groupValues.Add(0UL);
            return idx;
        }

        private int GroupOfTransform(Transform _t, char _category, int _depth)
        {
            long key = ((long)_t.GetInstanceID() << 8) | _category;
            if (transformGroupCache.TryGetValue(key, out int idx)) return idx;
            var parts = new List<string>(16);
            for (Transform t = _t; null != t; t = t.parent) parts.Add(t.name + "#" + t.GetSiblingIndex());
            parts.Reverse();
            var sb = new StringBuilder();
            sb.Append(_category).Append(':');
            Scene scene = _t.gameObject.scene;
            sb.Append(scene.name).Append('/');
            for (int i = 0; i < parts.Count && i < _depth; i++) { if (0 < i) sb.Append('/'); sb.Append(parts[i]); }
            idx = GroupIndexOf(sb.ToString());
            transformGroupCache[key] = idx;
            return idx;
        }

        private void SetGroup(string _name, ulong _value)
        {
            int idx = GroupIndexOf(_name);
            groupValues[idx] = 0UL == _value ? 1UL : _value;
        }

        // //마무리·비교

        private void Fail(string _message)
        {
            if (null == error) error = _message;
            Debug.LogError("[TownGolden] " + _message);
        }

        private string Header()
        {
            return "scenario " + scenario + "\nmode " + mode + "\nscreen " + Screen.width + "x" + Screen.height
                + "\nframes boot " + bootFrame + " go " + goFrame + " loaded " + loadedFrame + " control " + controlFrame
                + (bDungeon ? " ride " + rideFrame + " menu " + menuFrame + " depart " + departFrame + " dloaded " + dungeonLoadedFrame + " dunlock " + dungeonUnlockFrame + " dcontrol " + dungeonControlFrame : string.Empty)
                + "\npace period " + FramePeriodSec.ToString("F3") + "s heavy " + HeavyPeriodSec.ToString("F1") + "s frames " + paceFrames + " overruns " + paceOverruns + " worst " + (paceWorstSec * 1000.0).ToString("F1") + "ms maxNormal " + (paceMaxNormalSec * 1000.0).ToString("F1") + "ms tight " + paceTight + "\noverrun frames " + paceOverrunFrames.ToString() + (0 < debugFlags.Length ? "\ndebug " + debugFlags : string.Empty)
                + "\n" + setupLog + (null != error ? "error " + error.Replace('\n', ' ') + "\n" : string.Empty) + "--\n";
        }

        private void Finish()
        {
            StopAllCoroutines();
            string header = Header();
            File.WriteAllText(Path.Combine(outputFolder, scenario + ".txt"), header + "# frame R W U P A RAND L\n" + frames);
            var names = new StringBuilder();
            for (int i = 0; i < groupNames.Count; i++) names.Append("#g ").Append(i).Append(' ').Append(groupNames[i]).Append('\n');
            File.WriteAllText(Path.Combine(outputFolder, scenario + "_groups.txt"), header + names + groupFrames);
            File.WriteAllText(Path.Combine(outputFolder, scenario + "_draws.txt"), drawLog.ToString());
            drawLog.Length = 0;

            string report = "compare" == mode ? Compare(header)
                : "[TownGolden] record '" + label + "' " + scenario + (null != error ? " ERROR " + error : " OK") + " frames " + CountLines(frames) + " groups " + groupNames.Count + "\n" + header;
            File.WriteAllText(Path.Combine(outputFolder, "_report_" + scenario + ".txt"), report);
            if (null != error || report.Contains(": FAIL") || report.Contains(": INVALID")) Debug.LogError(report); else Debug.Log(report);

            Time.captureDeltaTime = 0.0f;
            UninstallHooks();
            SessionState.EraseBool(KeyRunning);
            EditorApplication.ExitPlaymode();
        }

        private static int CountLines(StringBuilder _sb)
        {
            int n = 0;
            for (int i = 0; i < _sb.Length; i++) if ('\n' == _sb[i]) n++;
            return n;
        }

        // 묶음 파일을 (프레임, 묶음 이름) → 해시로 읽는다. L은 기록된 프레임에만 있다.
        private static Dictionary<int, Dictionary<string, string>> LoadCells(string _path, out string _header)
        {
            var cells = new Dictionary<int, Dictionary<string, string>>();
            var names = new Dictionary<string, string>();
            var sbHeader = new StringBuilder();
            bool bBody = false;
            foreach (string line in File.ReadAllLines(_path))
            {
                if (false == bBody) { if ("--" == line) bBody = true; else sbHeader.Append(line).Append('\n'); continue; }
                if (line.StartsWith("#g "))
                {
                    int sp = line.IndexOf(' ', 3);
                    names[line.Substring(3, sp - 3)] = line.Substring(sp + 1);
                    continue;
                }
                if (0 == line.Length) continue;
                string[] p = line.Split(' ');
                var row = new Dictionary<string, string>();
                for (int k = 1; k < p.Length; k++)
                {
                    int eq = p[k].IndexOf('=');
                    row[names[p[k].Substring(0, eq)]] = p[k].Substring(eq + 1);
                }
                cells[int.Parse(p[0])] = row;
            }
            _header = sbHeader.ToString();
            return cells;
        }

        private string Compare(string _header)
        {
            string[] bases = baseline.Split('|');
            string basePath = Path.Combine(GetRootFolder(), bases[0], scenario + "_groups.txt");
            if (false == File.Exists(basePath)) return "[TownGolden] compare ERROR: 기준 없음 " + basePath + "\n";
            var b1 = LoadCells(basePath, out string h1);
            Dictionary<int, Dictionary<string, string>> b2 = null;
            string h2 = null;
            if (1 < bases.Length)
            {
                string p2 = Path.Combine(GetRootFolder(), bases[1], scenario + "_groups.txt");
                if (false == File.Exists(p2)) return "[TownGolden] compare ERROR: 두 번째 기준 없음 " + p2 + "\n";
                b2 = LoadCells(p2, out h2);
            }

            var names = new Dictionary<string, string>();
            for (int i = 0; i < groupNames.Count; i++) names[i.ToString()] = groupNames[i];
            var mine = new Dictionary<int, Dictionary<string, string>>();
            foreach (string line in groupFrames.ToString().Split('\n'))
            {
                if (0 == line.Length) continue;
                string[] p = line.Split(' ');
                var row = new Dictionary<string, string>();
                for (int k = 1; k < p.Length; k++) { int eq = p[k].IndexOf('='); row[names[p[k].Substring(0, eq)]] = p[k].Substring(eq + 1); }
                mine[int.Parse(p[0])] = row;
            }

            string Frames(string _h) => FindLine(_h, "frames ") + " " + FindLine(_h, "screen ");
            bool bInvalid = Frames(h1) != Frames(_header) || (null != h2 && Frames(h2) != Frames(h1));
            // 긴 간격이 된 프레임 목록이 기준과 다르면(예상 밖 끊김) 그 뒤 unscaledDeltaTime이 달라 실제 시간 연출이 어긋난다.
            // 코드 차이가 아니라 실행 환경 탓이므로 FAIL이 아니라 INVALID(다시 돌릴 것)로 둔다.
            string Overruns(string _h)
            {
                var sbo = new StringBuilder();
                foreach (string tok in FindLine(_h, "overrun frames ").Split(' ')) { int c = tok.IndexOf(':'); if (0 < c) sbo.Append(tok, 0, c).Append(' '); }
                return sbo.ToString();
            }
            string overrunNote = null;
            if (Overruns(h1) != Overruns(_header)) overrunNote = "끊김 프레임이 기준1과 다름: 기준1 [" + Overruns(h1) + "] 이번 [" + Overruns(_header) + "]";
            else if (null != h2 && Overruns(h2) != Overruns(h1)) overrunNote = "끊김 프레임이 기준끼리 다름: 기준1 [" + Overruns(h1) + "] 기준2 [" + Overruns(h2) + "]";
            if (null != overrunNote) bInvalid = true;

            // 범주(R, W, U, P, A, RAND, L)별 집계
            var stable = new Dictionary<string, int>();
            var masked = new Dictionary<string, int>();
            var diffs = new Dictionary<string, int>();
            var firstDiff = new Dictionary<string, int>();
            var groupDiffs = new Dictionary<string, int>();
            string Cat(string _g) { int c = _g.IndexOf(':'); return c < 0 ? _g : _g.Substring(0, c); }
            void Inc(Dictionary<string, int> _d, string _k) { _d.TryGetValue(_k, out int v); _d[_k] = v + 1; }

            // 기준끼리 한 번이라도 다른 묶음(불안정). 그 프레임은 가리고, 기준끼리 같았던 프레임의 차이도 FAIL이 아니라 WARN으로 센다.
            // (예: 정렬 순서가 같은 갈대 스프라이트가 겹친 타일은 흔들릴 때만 엔진의 그리기 순서가 실행마다 뒤바뀜.
            //  두 기준이 같은 순서를 뽑은 프레임에서 이번 실행만 다른 순서를 뽑을 수 있다.)
            // 같은 프레임에 기준끼리 같은 범주가 하나라도 달랐다면(텐트가 열리는 프레임처럼 실제 시간 미세 오차에 민감한 프레임)
            // 그 프레임·범주의 다른 묶음 차이도 WARN으로 센다. 기준끼리 한 번도 다르지 않은 범주(W·P·RAND·L)는 그대로 엄격하다.
            var unstable = new HashSet<string>();
            var unstableFrameCat = new HashSet<long>();
            if (null != b2)
            {
                foreach (var kv in b1)
                {
                    if (false == b2.TryGetValue(kv.Key, out var r2u)) continue;
                    var ks = new HashSet<string>(kv.Value.Keys); ks.UnionWith(r2u.Keys);
                    foreach (string g in ks)
                    {
                        kv.Value.TryGetValue(g, out string x1); r2u.TryGetValue(g, out string x2);
                        if (x1 == x2) continue;
                        unstable.Add(g);
                        unstableFrameCat.Add(((long)kv.Key << 8) | (uint)CatId(Cat(g)));
                    }
                }
            }
            var warns = new Dictionary<string, int>();
            var warnGroups = new Dictionary<string, int>();

            int missingFrames = 0;
            foreach (var kv in b1)
            {
                int f = kv.Key;
                Dictionary<string, string> r2 = null;
                if (null != b2) b2.TryGetValue(f, out r2);
                if (false == mine.TryGetValue(f, out var rm)) { missingFrames++; continue; }
                var keys = new HashSet<string>(kv.Value.Keys);
                keys.UnionWith(rm.Keys);
                if (null != r2) keys.UnionWith(r2.Keys);
                foreach (string g in keys)
                {
                    string cat = Cat(g);
                    kv.Value.TryGetValue(g, out string v1);
                    string v2 = null;
                    if (null != r2) r2.TryGetValue(g, out v2);
                    if (null != b2 && v1 != v2) { Inc(masked, cat); continue; }
                    Inc(stable, cat);
                    rm.TryGetValue(g, out string vm);
                    if (vm == v1) continue;
                    // 오디오(A)는 원샷이 끝나는 시점이 오디오 스레드의 실제 시간을 따라, 다음 소리가 풀의 어느 소스에 배정되는지가
                    // 실행마다 달라진다(heavy 자체 점검에서 AudioSource_10 한 칸). 그래서 A는 항상 WARN으로만 보고한다.
                    if ("A" == cat || unstable.Contains(g) || unstableFrameCat.Contains(((long)f << 8) | (uint)CatId(cat))) { Inc(warns, cat); Inc(warnGroups, g); continue; }
                    Inc(diffs, cat);
                    Inc(groupDiffs, g);
                    if (false == firstDiff.ContainsKey(cat)) firstDiff[cat] = f;
                }
            }

            int totalDiff = 0;
            foreach (var d in diffs) totalDiff += d.Value;
            int totalWarn = 0;
            foreach (var w in warns) totalWarn += w.Value;
            string verdict = null != error ? "ERROR" : bInvalid ? "INVALID" : (0 < totalDiff || 0 < missingFrames || mine.Count != b1.Count) ? "FAIL" : 0 < totalWarn ? "WARN" : "PASS";
            var sb = new StringBuilder();
            sb.Append("[TownGolden] compare '").Append(label).Append("' vs '").Append(baseline).Append("' ").Append(scenario).Append(" : ").Append(verdict)
              .Append(" (프레임 ").Append(mine.Count).Append('/').Append(b1.Count).Append(", 빠진 프레임 ").Append(missingFrames).Append(")\n");
            sb.Append("기준1 ").Append(Frames(h1)).Append(" | ").Append(FindLine(h1, "pace ")).Append('\n');
            if (null != h2) sb.Append("기준2 ").Append(Frames(h2)).Append(" | ").Append(FindLine(h2, "pace ")).Append('\n');
            sb.Append("이번  ").Append(Frames(_header)).Append(" | ").Append(FindLine(_header, "pace ")).Append('\n');
            if (null != overrunNote) sb.Append("INVALID: ").Append(overrunNote).Append(" (다시 돌릴 것)\n");
            foreach (string cat in new[] { "R", "W", "U", "P", "A", "RAND", "L" })
            {
                stable.TryGetValue(cat, out int s); masked.TryGetValue(cat, out int m); diffs.TryGetValue(cat, out int d); warns.TryGetValue(cat, out int wn);
                sb.Append(cat).Append(" : 비교 ").Append(s).Append("칸, 가림(기준끼리 다름) ").Append(m).Append("칸, 다름 ").Append(d)
                  .Append(0 < d ? "칸(처음 " + firstDiff[cat] + ")" : "칸").Append(", 불안정 묶음 차이(WARN) ").Append(wn).Append("칸\n");
            }
            sb.Append("불안정 묶음(기준끼리 한 번이라도 다름) ").Append(unstable.Count).Append("개\n");
            if (0 < warnGroups.Count)
            {
                var wl = new List<KeyValuePair<string, int>>(warnGroups);
                wl.Sort((x, y) => y.Value.CompareTo(x.Value));
                sb.Append("WARN 묶음(많은 순):\n");
                for (int i = 0; i < wl.Count && i < 15; i++) sb.Append("  ").Append(wl[i].Value).Append("  ").Append(wl[i].Key).Append('\n');
            }
            if (0 < groupDiffs.Count)
            {
                var list = new List<KeyValuePair<string, int>>(groupDiffs);
                list.Sort((x, y) => y.Value.CompareTo(x.Value));
                sb.Append("다른 묶음(많은 순):\n");
                for (int i = 0; i < list.Count && i < 25; i++) sb.Append("  ").Append(list[i].Value).Append("  ").Append(list[i].Key).Append('\n');
            }
            if (null != error) sb.Append("error ").Append(error).Append('\n');
            return sb.ToString();
        }

        private static int CatId(string _cat)
        {
            switch (_cat)
            {
                case "R": return 1;
                case "W": return 2;
                case "U": return 3;
                case "P": return 4;
                case "A": return 5;
                case "RAND": return 6;
                case "L": return 7;
                case "RP": return 8;
                default: return 255;
            }
        }

        private static string FindLine(string _text, string _prefix)
        {
            foreach (string line in _text.Split('\n')) if (line.StartsWith(_prefix)) return line;
            return string.Empty;
        }

        public static string GetRootFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "TownGolden");
        }

        private static readonly Dictionary<int, string> pathCache = new Dictionary<int, string>();

        // 계층 경로(이름#형제 순서). 덤프·파티클 시드에 쓴다. 다시 부모가 바뀌는 일은 드물어 처음 값을 캐시한다(두 실행 모두 같은 규칙).
        private static string PathOf(Transform _t)
        {
            int id = _t.GetInstanceID();
            if (pathCache.TryGetValue(id, out string cached)) return cached;
            string path = BuildPath(_t);
            pathCache[id] = path;
            return path;
        }

        private static string BuildPath(Transform _t)
        {
            var sb = new StringBuilder();
            for (Transform t = _t; null != t; t = t.parent)
            {
                sb.Insert(0, t.name + "#" + t.GetSiblingIndex() + (null != t.parent ? "/" : string.Empty));
            }
            return sb.ToString();
        }

        private static uint StableHash(string _text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < _text.Length; i++) { hash ^= _text[i]; hash *= 16777619u; }
            return hash;
        }

        // 해시 누적기. 범주 해시와 지금 묶음의 해시에 같이 섞는다. 덤프가 있으면 같은 값을 글자로도 남긴다.
        private sealed class Acc
        {
            public ulong h;
            public StringBuilder dump;
            private readonly TownGoldenRunner owner;
            private int group = -1;

            public Acc(TownGoldenRunner _owner) { owner = _owner; }

            public void Begin(string _category) { h = 14695981039346656037UL; group = -1; }
            public ulong End() { group = -1; return h; }
            public void Group(Transform _t, char _category, int _depth) { group = owner.GroupOfTransform(_t, _category, _depth); }
            public void GroupName(string _name) { group = owner.GroupIndexOf(_name); }

            private void Mix(ulong _v)
            {
                h ^= _v; h *= 1099511628211UL; h ^= h >> 31;
                if (group < 0) return;
                ulong g = owner.groupValues[group];
                if (0UL == g) g = 14695981039346656037UL;
                g ^= _v; g *= 1099511628211UL; g ^= g >> 31;
                owner.groupValues[group] = 0UL == g ? 1UL : g;
            }

            public void U(ulong _v) { Mix(_v); if (null != dump) dump.Append(' ').Append(_v.ToString("x")); }
            public void B(bool _b) { U(_b ? 1UL : 0UL); }
            public void I(int _i) { U((ulong)(uint)_i); }
            public void F(float _f)
            {
                Mix((ulong)(uint)BitConverter.SingleToInt32Bits(_f));
                if (null != dump) dump.Append(' ').Append(_f.ToString("R"));
            }
            public void C(Color _c) { F(_c.r); F(_c.g); F(_c.b); F(_c.a); }
            public void S(string _s)
            {
                string s = _s ?? "<null>";
                Mix(StableHash(s));
                Mix((ulong)s.Length);
                if (null != dump) dump.Append(" \"").Append(s.Replace('\n', '|')).Append('"');
            }
            public void Line(string _s) { if (null != dump) dump.Append('\n').Append(_s); }
        }
    }
}

#endif
