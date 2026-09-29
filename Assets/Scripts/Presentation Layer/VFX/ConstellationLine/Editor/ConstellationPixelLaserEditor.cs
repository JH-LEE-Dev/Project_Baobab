using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// ConstellationPixelLaser 전용 에디터 커스텀 인스펙터 및 씬 뷰 시뮬레이션 도구입니다.
    /// 씬 뷰에서 마우스로 별 위치를 콕콕 찍거나 3D 핸들로 드래그하여 임의로 지정하고,
    /// 에디터 비재생(Edit Mode) 및 플레이 모드 모두에서 원클릭으로 도미노 연쇄 발사를 테스트할 수 있습니다.
    /// </summary>
    [CustomEditor(typeof(ConstellationPixelLaser))]
    public class ConstellationPixelLaserEditor : Editor
    {
        private static bool bSequentialClickMode = false;
        private static int currentClickNodeIndex = 0;

        public override void OnInspectorGUI()
        {
            ConstellationPixelLaser targetLaser = target as ConstellationPixelLaser;
            if (null == targetLaser) return;

            DrawDefaultInspector();

            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("── [별자리 픽셀 레이저 인터랙티브 테스트 도구] ──", EditorStyles.boldLabel);

            List<Vector3> nodes = targetLaser.TestNodes;

            // 1. 레이저 발사 테스트 버튼
            Color prevBg = GUI.backgroundColor;
            EditorGUI.BeginDisabledGroup(false == Application.isPlaying);

            GUI.backgroundColor = new Color(0.1f, 1.0f, 0.85f);
            string fireButtonText = true == Application.isPlaying
                ? "⚡ [대표] 상호 양방향 전면 동시 발사 테스트 (Mutual Simultaneous Fire)"
                : "⚡ [대표] 상호 양방향 전면 동시 발사 테스트 (플레이 모드 실행 필요)";

            if (true == GUILayout.Button(fireButtonText, GUILayout.Height(38)))
            {
                targetLaser.TestFireLaser();
            }

            EditorGUI.EndDisabledGroup();

            // 1-2. 에디터 즉시 프리뷰 및 화면 지우기 버튼 모음
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 1.0f, 0.7f);
            if (true == GUILayout.Button("👁️ 씬 뷰에 레이저 즉시 표시 (Preview)", GUILayout.Height(28)))
            {
                targetLaser.PreviewHoldInEditor(targetLaser.TestNodes, targetLaser.TestIsClosedLoop);
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
            if (true == GUILayout.Button("🧹 레이저 끄기 (Clear)", GUILayout.Height(28)))
            {
                targetLaser.ClearTestDisplay();
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = prevBg;

            EditorGUILayout.Space(6);

            // 2. 씬 뷰 연속 클릭 별 위치 재배치 버튼
            GUI.backgroundColor = true == bSequentialClickMode ? new Color(0.4f, 1.0f, 0.4f) : new Color(0.3f, 0.85f, 1.0f);

            string clickBtnText = true == bSequentialClickMode
                ? $"🖱️ [클릭 배치 중] {currentClickNodeIndex + 1} / {nodes.Count} 번째 별 클릭 대기 중 (클릭 시 취소)"
                : $"🖱️ 씬 뷰에서 별 위치 순서대로 콕콕 찍기 (현재 {nodes.Count}개)";

            if (true == GUILayout.Button(clickBtnText, GUILayout.Height(30)))
            {
                bSequentialClickMode = !bSequentialClickMode;
                currentClickNodeIndex = 0;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;

            if (true == bSequentialClickMode)
            {
                EditorGUILayout.HelpBox(
                    $"씬 뷰에서 원하는 별 위치(나무 밑동 등)를 순서대로 클릭하세요!\n" +
                    $"현재 차례: #{currentClickNodeIndex} 별 ({currentClickNodeIndex + 1} / {nodes.Count})\n" +
                    $"모두 클릭하면 자동으로 완료되며, ESC 키로 취소할 수 있습니다.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6);

            // 3. 노드 추가 및 삭제 버튼 모음
            EditorGUILayout.BeginHorizontal();
            if (true == GUILayout.Button("➕ 새 별(Star) 추가", GUILayout.Height(26)))
            {
                Undo.RecordObject(targetLaser, "Add Laser Star Node");
                Vector3 newPos = 0 < nodes.Count ? nodes[nodes.Count - 1] + new Vector3(1.5f, 0.5f, 0.0f) : Vector3.zero;
                nodes.Add(newPos);
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }

            if (2 < nodes.Count && true == GUILayout.Button("➖ 마지막 별 삭제", GUILayout.Height(26)))
            {
                Undo.RecordObject(targetLaser, "Remove Laser Star Node");
                nodes.RemoveAt(nodes.Count - 1);
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // 4. 폐곡선 토글 및 초기화
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newLoop = EditorGUILayout.ToggleLeft("마지막 별에서 첫 별로 닫기 (폐곡선 루프)", targetLaser.TestIsClosedLoop, GUILayout.Width(260));
            if (true == EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetLaser, "Toggle Laser Closed Loop");
                targetLaser.TestIsClosedLoop = newLoop;
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }

            if (true == GUILayout.Button("↺ 기본 5개 별자리로 리셋", GUILayout.Height(24)))
            {
                Undo.RecordObject(targetLaser, "Reset Default 5 Star Nodes");
                nodes.Clear();
                nodes.Add(new Vector3(-3.0f, 0.0f, 0.0f));
                nodes.Add(new Vector3(-0.5f, 2.0f, 0.0f));
                nodes.Add(new Vector3(2.5f, 1.2f, 0.0f));
                nodes.Add(new Vector3(1.8f, -1.8f, 0.0f));
                nodes.Add(new Vector3(-1.5f, -1.5f, 0.0f));
                targetLaser.TestIsClosedLoop = true;
                bSequentialClickMode = false;
                EditorUtility.SetDirty(targetLaser);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // 4-2. 별자리 꼬임 자동 풀기 버튼 (원클릭 단순 다각형 정돈)
            if (3 <= nodes.Count)
            {
                EditorGUILayout.Space(6);
                GUI.backgroundColor = new Color(0.4f, 1.0f, 0.7f);
                if (true == GUILayout.Button("📐 별자리 꼬임 자동 풀기 (Auto Untangle)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(targetLaser, "Untangle Laser Star Nodes");
                    targetLaser.AutoUntangleNodes();
                    EditorUtility.SetDirty(targetLaser);
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = prevBg;
            }

            // 5. 노드별 좌표 리스트 직접 편집 패널
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── [별자리 노드별 좌표 편집] ──", EditorStyles.miniBoldLabel);

            for (int i = 0; i < nodes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"Star #{i}", GUILayout.Width(60));

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = EditorGUILayout.Vector3Field("", nodes[i]);
                if (true == EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetLaser, $"Change Star {i} Position");
                    nodes[i] = newPos;
                    EditorUtility.SetDirty(targetLaser);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.EndHorizontal();
            }
        }



        private void OnSceneGUI()
        {
            ConstellationPixelLaser targetLaser = target as ConstellationPixelLaser;
            if (null == targetLaser) return;

            List<Vector3> points = targetLaser.TestNodes;
            if (null == points || 0 == points.Count) return;

            Event currentEvent = Event.current;

            // A. 씬 뷰 순서대로 클릭 별 재배치 처리
            if (true == bSequentialClickMode && null != currentEvent)
            {
                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);

                // ESC 키로 취소
                if (EventType.KeyDown == currentEvent.type && KeyCode.Escape == currentEvent.keyCode)
                {
                    bSequentialClickMode = false;
                    currentClickNodeIndex = 0;
                    currentEvent.Use();
                    SceneView.RepaintAll();
                    return;
                }

                // 마우스 좌클릭 시 해당 별 위치 갱신
                if (EventType.MouseDown == currentEvent.type && 0 == currentEvent.button)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                    float enter = -ray.origin.z / ray.direction.z;
                    Vector3 worldHitPos = ray.origin + ray.direction * enter;
                    worldHitPos.z = 0.0f;

                    Undo.RecordObject(targetLaser, $"Click Place Laser Star {currentClickNodeIndex}");

                    if (points.Count > currentClickNodeIndex)
                    {
                        points[currentClickNodeIndex] = worldHitPos;
                        currentClickNodeIndex++;

                        EditorUtility.SetDirty(targetLaser);

                        if (points.Count <= currentClickNodeIndex)
                        {
                            bSequentialClickMode = false;
                            currentClickNodeIndex = 0;
                            targetLaser.AutoUntangleNodes();
                        }
                    }

                    currentEvent.Use();
                    SceneView.RepaintAll();
                    return;
                }
            }

            // B. 씬 뷰 3D 이동 핸들(PositionHandle) 및 시각화
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];

                // 핸들 색상 및 디스크 표시
                Handles.color = new Color(0.2f, 0.8f, 1.0f, 0.9f);
                Handles.DrawWireDisc(p, Vector3.forward, 0.3f);
                Handles.Label(p + new Vector3(0.0f, 0.45f, 0.0f), $"★ Star #{i}", EditorStyles.boldLabel);

                // 클릭 모드가 아닐 때 마우스 드래그 3D 이동 핸들 제공
                if (false == bSequentialClickMode)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(p, Quaternion.identity);
                    if (true == EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(targetLaser, $"Move Laser Star #{i}");
                        newPos.z = 0.0f;
                        points[i] = newPos;
                        EditorUtility.SetDirty(targetLaser);
                        SceneView.RepaintAll();
                    }
                }

                // 점선/곡선 연결 궤적
                bool bLoop = true == targetLaser.TestIsClosedLoop && 2 < points.Count;
                if (i < points.Count - 1 || true == bLoop)
                {
                    Handles.color = (i < points.Count - 1)
                        ? new Color(0.2f, 0.8f, 1.0f, 0.4f)
                        : new Color(0.2f, 0.8f, 1.0f, 0.25f);

                    if (0.001f < targetLaser.CurveRoundness && 2 < points.Count)
                    {
                        ConstellationPixelLaser.ComputeBezierControlPoints(
                            points,
                            i,
                            bLoop,
                            targetLaser.CurveRoundness,
                            out Vector3 p0,
                            out Vector3 p1,
                            out Vector3 c0,
                            out Vector3 c1);
                        Handles.DrawBezier(p0, p1, c0, c1, Handles.color, null, 2.5f);
                    }
                    else
                    {
                        Vector3 nextPt = (i < points.Count - 1) ? points[i + 1] : points[0];
                        Handles.DrawDottedLine(p, nextPt, 4.0f);
                    }
                }
            }
        }
    }
}
