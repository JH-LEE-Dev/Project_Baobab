using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 실물 패드로 컨트롤러 지원을 점검하기 위한 진단 창입니다.
///
/// 만든 이유: 이 프로젝트의 자동 테스트는 EditMode에서 도는데, Input System의 액션은
/// Dynamic 업데이트(플레이 모드)에서만 처리되고 버튼의 "이번 프레임 눌림"도 에디터에서는
/// 잡히지 않습니다. 그래서 패드 조준·버튼 입력·리바인딩 취소는 자동 검증의 사각지대이고,
/// 사람이 한 번은 직접 눌러봐야 합니다. 그 확인을 몇 분 안에 끝내라고 만든 창입니다.
///
/// 편집 모드에서도 장치 원시값·벤더 판별·진동은 확인할 수 있고,
/// 액션 값과 게임의 실제 장치 상태는 플레이 모드에서만 나옵니다. (창에 표시됩니다)
/// </summary>
public class GamepadDiagnosticsWindow : EditorWindow
{
    private const string MENU_PATH = "Tools/Input/게임패드 진단";

    // 드리프트 실측용. 유저가 손을 뗀 상태에서 스틱이 얼마나 흔들리는지 최대값을 누적한다.
    private float leftStickDriftMax;
    private float rightStickDriftMax;
    private float driftSampleSeconds;

    private GamepadHaptics haptics;
    private double lastUpdateTime;

    private InputReader previewReader;

    private Vector2 scroll;

    [MenuItem(MENU_PATH)]
    private static void Open()
    {
        GamepadDiagnosticsWindow _window = GetWindow<GamepadDiagnosticsWindow>();
        _window.titleContent = new GUIContent("게임패드 진단");
        _window.minSize = new Vector2(420f, 520f);
        _window.Show();
    }

    private void OnEnable()
    {
        haptics = new GamepadHaptics();
        lastUpdateTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        // 창을 닫았는데 패드가 계속 울리면 안 된다.
        haptics?.Release();
        haptics = null;

        previewReader?.Release();
        previewReader = null;
    }

    private void OnEditorUpdate()
    {
        double _now = EditorApplication.timeSinceStartup;
        float _delta = (float)(_now - lastUpdateTime);
        lastUpdateTime = _now;

        haptics?.Tick(_delta);

        SampleDrift(_delta);

        Repaint();
    }

    private void SampleDrift(float _delta)
    {
        Gamepad _pad = Gamepad.current;
        if (null == _pad) return;

        driftSampleSeconds += _delta;

        float _left = _pad.leftStick.ReadValue().magnitude;
        float _right = _pad.rightStick.ReadValue().magnitude;

        if (_left > leftStickDriftMax) leftStickDriftMax = _left;
        if (_right > rightStickDriftMax) rightStickDriftMax = _right;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawModeBanner();
        EditorGUILayout.Space();

        DrawConnectedDevices();
        EditorGUILayout.Space();

        DrawLiveInput();
        EditorGUILayout.Space();

        DrawDriftMeasurement();
        EditorGUILayout.Space();

        DrawHapticsTest();
        EditorGUILayout.Space();

        DrawBindingTable();
        EditorGUILayout.Space();

        DrawChecklist();

        EditorGUILayout.EndScrollView();
    }

    private void DrawModeBanner()
    {
        if (true == EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드입니다. 모든 항목을 확인할 수 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "편집 모드입니다. 장치 원시값·벤더 판별·진동은 확인할 수 있지만,\n" +
            "액션 값과 게임의 실제 장치 상태(조준·버튼·리바인딩)는 플레이 모드에서만 확인됩니다.",
            MessageType.Warning);
    }

    private void DrawConnectedDevices()
    {
        EditorGUILayout.LabelField("연결된 패드", EditorStyles.boldLabel);

        if (Gamepad.all.Count == 0)
        {
            EditorGUILayout.HelpBox("연결된 패드가 없습니다.", MessageType.None);
            return;
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad _pad = Gamepad.all[i];
            bool _isCurrent = _pad == Gamepad.current;

            EditorGUILayout.LabelField(
                (_isCurrent ? "▶ " : "   ") + _pad.displayName,
                _pad.layout + (_isCurrent ? "  (current)" : ""));

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("제조사 / 제품", _pad.description.manufacturer + " / " + _pad.description.product);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2f);

        // 게임이 실제로 쓰는 판별기를 그대로 태워야 의미가 있다.
        if (true == EditorApplication.isPlaying)
        {
            InputManager _live = FindLiveInputManager();

            if (null != _live)
            {
                EditorGUILayout.LabelField("현재 장치", _live.CurrentDevice.ToString());
                EditorGUILayout.LabelField("아이콘 세트", _live.CurrentGamepadIconSet + "  (자동 판별: " + _live.DetectedGamepadIconSet + ")");
                EditorGUILayout.LabelField("이번 프레임 입력", _live.AnyInputThisFrame ? "있음" : "없음");
            }
            else
            {
                EditorGUILayout.HelpBox("씬에서 InputManager를 찾지 못했습니다.", MessageType.Warning);
            }
        }
    }

    private void DrawLiveInput()
    {
        EditorGUILayout.LabelField("실시간 입력 (원시값)", EditorStyles.boldLabel);

        Gamepad _pad = Gamepad.current;
        if (null == _pad)
        {
            EditorGUILayout.LabelField("—");
            return;
        }

        Vector2 _left = _pad.leftStick.ReadValue();
        Vector2 _right = _pad.rightStick.ReadValue();

        EditorGUILayout.LabelField("왼쪽 스틱", string.Format("{0}   크기 {1:0.000}", _left, _left.magnitude));
        EditorGUILayout.LabelField("오른쪽 스틱", string.Format("{0}   크기 {1:0.000}", _right, _right.magnitude));
        EditorGUILayout.LabelField("트리거 L / R", string.Format("{0:0.00} / {1:0.00}", _pad.leftTrigger.ReadValue(), _pad.rightTrigger.ReadValue()));

        EditorGUILayout.LabelField("눌린 버튼", GetPressedButtons(_pad));
    }

    private static string GetPressedButtons(Gamepad _pad)
    {
        string _result = "";

        _result += _pad.buttonSouth.isPressed ? "South(A/×) " : "";
        _result += _pad.buttonEast.isPressed ? "East(B/○) " : "";
        _result += _pad.buttonWest.isPressed ? "West(X/□) " : "";
        _result += _pad.buttonNorth.isPressed ? "North(Y/△) " : "";
        _result += _pad.leftShoulder.isPressed ? "LB " : "";
        _result += _pad.rightShoulder.isPressed ? "RB " : "";
        _result += _pad.leftStickButton.isPressed ? "L3 " : "";
        _result += _pad.rightStickButton.isPressed ? "R3 " : "";
        _result += _pad.startButton.isPressed ? "Start " : "";
        _result += _pad.selectButton.isPressed ? "Select " : "";
        _result += _pad.dpad.up.isPressed ? "D↑ " : "";
        _result += _pad.dpad.down.isPressed ? "D↓ " : "";
        _result += _pad.dpad.left.isPressed ? "D← " : "";
        _result += _pad.dpad.right.isPressed ? "D→ " : "";

        return string.IsNullOrEmpty(_result) ? "—" : _result;
    }

    private void DrawDriftMeasurement()
    {
        EditorGUILayout.LabelField("스틱 드리프트 실측", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "패드에서 손을 완전히 떼고 초기화한 뒤 10초 이상 두세요.\n" +
            "최대값이 장치 전환 문턱값(기본 0.5)에 가까우면 문턱값을 올려야 합니다.",
            MessageType.None);

        EditorGUILayout.LabelField("측정 시간", string.Format("{0:0.0}초", driftSampleSeconds));

        DrawDriftRow("왼쪽 스틱 최대", leftStickDriftMax);
        DrawDriftRow("오른쪽 스틱 최대", rightStickDriftMax);

        if (GUILayout.Button("측정 초기화"))
        {
            leftStickDriftMax = 0f;
            rightStickDriftMax = 0f;
            driftSampleSeconds = 0f;
        }
    }

    private static void DrawDriftRow(string _label, float _value)
    {
        string _verdict;

        if (_value >= 0.5f) _verdict = "  ← 위험: 문턱값 이상. 오작동합니다";
        else if (_value >= 0.3f) _verdict = "  ← 주의: 여유가 부족합니다";
        else _verdict = "  ← 양호";

        EditorGUILayout.LabelField(_label, string.Format("{0:0.000}{1}", _value, _verdict));
    }

    private void DrawHapticsTest()
    {
        EditorGUILayout.LabelField("진동 테스트", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(null == Gamepad.current))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("약")) haptics.Play(0.2f, 0.1f, 0.3f);
            if (GUILayout.Button("중")) haptics.Play(0.5f, 0.3f, 0.3f);
            if (GUILayout.Button("강")) haptics.Play(1f, 0.6f, 0.3f);
            if (GUILayout.Button("정지")) haptics.Stop();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField("재생 중", haptics != null && haptics.IsPlaying ? "예" : "아니오");

        if (null == Gamepad.current)
        {
            EditorGUILayout.HelpBox("패드가 없어 진동을 시험할 수 없습니다.", MessageType.None);
        }
    }

    private void DrawBindingTable()
    {
        EditorGUILayout.LabelField("바인딩", EditorStyles.boldLabel);

        InputReader _reader = GetPreviewReader();
        if (null == _reader) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = _reader.GetRebindableActions();

        for (int i = 0; i < _actions.Count; i++)
        {
            ERebindableAction _action = _actions[i];

            string _keyboard = _reader.GetBindingPath(_action, EInputDeviceType.KeyboardMouse);
            string _gamepad = _reader.GetBindingPath(_action, EInputDeviceType.Gamepad);
            bool _rebindable = _reader.IsRebindable(_action, EInputDeviceType.Gamepad);

            EditorGUILayout.LabelField(
                _action.ToString(),
                string.Format("{0}   |   {1}{2}",
                    _keyboard,
                    string.IsNullOrEmpty(_gamepad) ? "(없음)" : _gamepad,
                    _rebindable ? "" : "  [고정]"));
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("패드 중복", _reader.HasAnyConflict(EInputDeviceType.Gamepad) ? "있음 (저장 차단됨)" : "없음");
        EditorGUILayout.LabelField("키보드 중복", _reader.HasAnyConflict(EInputDeviceType.KeyboardMouse) ? "있음 (저장 차단됨)" : "없음");
    }

    private void DrawChecklist()
    {
        EditorGUILayout.LabelField("실기 확인 체크리스트 (플레이 모드)", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("A. 회귀 확인 — 기존 키보드/마우스가 그대로인가", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "A1. 패드를 꽂은 채 WASD로 이동이 정상인가\n" +
            "    (Move 액션에 왼쪽 스틱이 추가되어 두 입력이 같은 액션을 공유한다.\n" +
            "     손을 뗀 상태에서 캐릭터가 저절로 밀리면 스틱 드리프트가 데드존을 넘은 것 →\n" +
            "     위의 드리프트 실측으로 수치를 확인할 것)\n" +
            "A2. 마우스로 UI 버튼 클릭·호버가 정상인가\n" +
            "    (EventSystem이 패키지 기본 애셋에서 프로젝트 애셋으로 교체되었다)\n" +
            "A3. 마우스 조준·공격이 이전과 똑같은가\n" +
            "A4. 키 설정에서 키보드 리바인딩·중복 경고·저장이 이전과 똑같은가",
            MessageType.None);

        EditorGUILayout.LabelField("B. 패드 신규 기능", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "B1. 오른쪽 스틱으로 조준 → 캐릭터가 그 방향을 바라보는가\n" +
            "B2. 이동 중에도 조준 방향이 유지되는가 (조준점이 캐릭터를 따라오는가)\n" +
            "B3. 스틱에서 손을 떼도 마지막 조준 방향을 유지하는가\n" +
            "B4. RT 공격 / A 상호작용 / Y 인벤토리 / X 물약 / Start 메뉴\n" +
            "B5. 마우스를 움직이면 즉시 키보드 표기로 돌아오는가 (깜빡임 없이)\n" +
            "B6. 플레이 중 패드를 뽑으면 키보드 표기로 즉시 돌아오는가\n" +
            "B7. 알트탭으로 나갔을 때 진동이 멈추는가\n" +
            "B8. 패드로 조작하면 OS 커서가 사라지고, 마우스를 움직이면 다시 나타나는가",
            MessageType.None);

        EditorGUILayout.LabelField("C. 키 설정 화면 (패드)", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "C1. 패드 항목 변경 대기 중 B로 취소가 되는가\n" +
            "C2. 키보드 항목 변경 중 패드 버튼을 눌러도 잡히지 않는가\n" +
            "C3. 패드 항목 변경 중 키보드를 눌러도 잡히지 않는가\n" +
            "C4. 이동(Move) 항목은 패드에서 변경 버튼이 잠겨 있는가\n" +
            "C5. 패드 버튼을 중복으로 지정하면 경고가 뜨고 저장이 막히는가",
            MessageType.None);
    }

    private InputReader GetPreviewReader()
    {
        // 플레이 중이면 게임이 실제로 쓰는 것을 그대로 보여준다.
        if (true == EditorApplication.isPlaying)
        {
            InputManager _live = FindLiveInputManager();
            if (null != _live && null != _live.inputReader) return _live.inputReader;
        }

        if (null == previewReader)
        {
            previewReader = new InputReader();
            previewReader.Initialize(null);
        }

        return previewReader;
    }

    private static InputManager FindLiveInputManager()
    {
        // 씬에 InputManager는 하나뿐이므로 순서를 보장하는 FindFirstObjectByType이 필요 없다.
        // (그쪽은 인스턴스 ID 순서에 의존해 deprecated 되었다)
        return Object.FindAnyObjectByType<InputManager>(FindObjectsInactive.Include);
    }
}
