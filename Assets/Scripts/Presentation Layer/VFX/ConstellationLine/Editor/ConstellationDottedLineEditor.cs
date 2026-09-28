using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ConstellationDottedLine 전용 에디터 커스텀 인스펙터 및 씬 뷰 인터랙티브 테스트 도구입니다.
/// Editor 폴더에 위치하여 실제 게임 빌드 시 100% 자동 제외됩니다.
/// </summary>
[CustomEditor(typeof(ConstellationDottedLine))]
public class ConstellationDottedLineEditor : Editor
{
    private static bool bClickToTestMode = false;
    private static int clickStep = 0; // 0: 시작점 대기, 1: 끝점 대기

    public override void OnInspectorGUI()
    {
        ConstellationDottedLine targetLine = target as ConstellationDottedLine;
        if (null == targetLine) return;

        // 기본 인스펙터 필드 출력
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("── [Editor Only Test Suite] ──", EditorStyles.boldLabel);

        // 1. 랜덤 시드 재생성 버튼
        if (true == GUILayout.Button("🎲 랜덤 지그재그 형태 재생성 (New Seed)", GUILayout.Height(28)))
        {
            Undo.RecordObject(targetLine, "Regenerate Constellation Seed");
            targetLine.RandomSeed = UnityEngine.Random.Range(1, 99999);
            targetLine.SetPoints(targetLine.EditorStartPos, targetLine.EditorEndPos);
            EditorUtility.SetDirty(targetLine);
        }

        // 2. 씬 뷰 마우스 클릭 테스트 토글
        EditorGUILayout.Space(4);
        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = true == bClickToTestMode ? new Color(0.4f, 1.0f, 0.4f) : prevColor;

        string toggleText = true == bClickToTestMode
            ? "🖱️ 씬 뷰 클릭 테스트 모드 [활성화 중 (클릭 시 지점 지정)]"
            : "🖱️ 씬 뷰 클릭 테스트 모드 켜기";

        if (true == GUILayout.Button(toggleText, GUILayout.Height(28)))
        {
            bClickToTestMode = !bClickToTestMode;
            clickStep = 0;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prevColor;

        if (true == bClickToTestMode)
        {
            EditorGUILayout.HelpBox(
                0 == clickStep
                    ? "씬 뷰에서 [시작 위치]를 좌클릭하세요."
                    : "씬 뷰에서 [도착 위치]를 좌클릭하세요.",
                MessageType.Info);
        }

        // 3. 테스트 시작/끝 좌표 원점 리셋
        EditorGUILayout.Space(4);
        if (true == GUILayout.Button("↺ 테스트 좌표 기본값으로 리셋", GUILayout.Height(22)))
        {
            Undo.RecordObject(targetLine, "Reset Constellation Test Points");
            targetLine.EditorStartPos = new Vector3(-2.0f, 0.0f, 0.0f);
            targetLine.EditorEndPos = new Vector3(2.0f, 0.0f, 0.0f);
            targetLine.SetPoints(targetLine.EditorStartPos, targetLine.EditorEndPos);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        ConstellationDottedLine targetLine = target as ConstellationDottedLine;
        if (null == targetLine) return;

        Event currentEvent = Event.current;

        // A. 씬 뷰 마우스 클릭 테스트 처리
        if (true == bClickToTestMode && null != currentEvent)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            if (EventType.MouseDown == currentEvent.type && 0 == currentEvent.button)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                // 2D Z=0 평면과의 교점 계산
                float enter = -ray.origin.z / ray.direction.z;
                Vector3 worldHitPos = ray.origin + ray.direction * enter;
                worldHitPos.z = 0.0f;

                Undo.RecordObject(targetLine, "Click Set Constellation Point");

                if (0 == clickStep)
                {
                    targetLine.EditorStartPos = worldHitPos;
                    clickStep = 1;
                }
                else
                {
                    targetLine.EditorEndPos = worldHitPos;
                    clickStep = 0;
                }

                targetLine.SetPoints(targetLine.EditorStartPos, targetLine.EditorEndPos);
                EditorUtility.SetDirty(targetLine);
                currentEvent.Use();
                SceneView.RepaintAll();
                return;
            }
        }

        // B. 씬 뷰 인터랙티브 드래그 핸들 (Position Handles)
        EditorGUI.BeginChangeCheck();

        Handles.color = new Color(0.2f, 0.8f, 1.0f, 0.9f);
        Handles.Label(targetLine.EditorStartPos + Vector3.up * 0.25f, "Start (A)");
        Vector3 newStart = Handles.PositionHandle(targetLine.EditorStartPos, Quaternion.identity);

        Handles.color = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        Handles.Label(targetLine.EditorEndPos + Vector3.up * 0.25f, "End (B)");
        Vector3 newEnd = Handles.PositionHandle(targetLine.EditorEndPos, Quaternion.identity);

        if (true == EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(targetLine, "Move Constellation Handle");
            targetLine.EditorStartPos = newStart;
            targetLine.EditorEndPos = newEnd;
            targetLine.SetPoints(newStart, newEnd);
            EditorUtility.SetDirty(targetLine);
        }
    }
}
