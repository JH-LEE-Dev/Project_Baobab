using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ConstellationDottedLine 전용 에디터 커스텀 인스펙터 및 씬 뷰 다중 노드 인터랙티브 테스트 도구입니다.
/// Editor 폴더에 위치하여 실제 게임 빌드 시 100% 자동 제외됩니다.
/// </summary>
[CustomEditor(typeof(ConstellationDottedLine))]
public class ConstellationDottedLineEditor : Editor
{
    private static bool bSequentialClickMode = false;
    private static int currentClickNodeIndex = 0;

    public override void OnInspectorGUI()
    {
        ConstellationDottedLine targetLine = target as ConstellationDottedLine;
        if (null == targetLine) return;

        // 1. 기본 인스펙터 필드 출력
        DrawDefaultInspector();

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("── [다중 노드 별자리 테스트 스위트] ──", EditorStyles.boldLabel);

        List<ConstellationNode> nodes = targetLine.EditorNodes;

        // 2. 씬 뷰 연속 클릭 노드 재배치 버튼 (핵심 요청 기능!)
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = true == bSequentialClickMode ? new Color(0.4f, 1.0f, 0.4f) : new Color(0.3f, 0.85f, 1.0f);

        string clickButtonText = true == bSequentialClickMode
            ? $"🖱️ [클릭 배치 진행 중] {currentClickNodeIndex + 1} / {nodes.Count} 번째 노드 클릭 대기 중 (클릭 시 취소)"
            : $"🖱️ 씬 뷰에서 노드 위치 순서대로 콕콕 찍기 (현재 {nodes.Count}개)";

        if (true == GUILayout.Button(clickButtonText, GUILayout.Height(32)))
        {
            bSequentialClickMode = !bSequentialClickMode;
            currentClickNodeIndex = 0;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prevBg;

        if (true == bSequentialClickMode)
        {
            EditorGUILayout.HelpBox(
                $"씬 뷰에서 원하는 나무 링 위치를 순서대로 클릭하세요!\n" +
                $"현재 차례: #{currentClickNodeIndex} 노드 ({currentClickNodeIndex + 1} / {nodes.Count})\n" +
                $"모두 클릭하면 자동으로 완료되며, ESC 키로 취소할 수 있습니다.",
                MessageType.Info);
        }

        EditorGUILayout.Space(6);

        // 3. 별자리 꼬임 자동 풀기 버튼 (원클릭 단순 다각형 정돈)
        if (true == GUILayout.Button("📐 별자리 꼬임 자동 풀기 (Auto Untangle)", GUILayout.Height(28)))
        {
            Undo.RecordObject(targetLine, "Untangle Constellation Nodes");
            targetLine.AutoUntangleNodes();
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);

        // 4. 노드 추가 및 삭제 버튼 모음
        EditorGUILayout.BeginHorizontal();
        if (true == GUILayout.Button("➕ 새 노드 추가", GUILayout.Height(26)))
        {
            Undo.RecordObject(targetLine, "Add Constellation Node");
            Vector3 newPos = 0 < nodes.Count ? nodes[nodes.Count - 1].position + new Vector3(1.5f, 0.5f, 0.0f) : Vector3.zero;
            nodes.Add(new ConstellationNode(newPos, false));
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }

        if (2 < nodes.Count && true == GUILayout.Button("➖ 마지막 노드 삭제", GUILayout.Height(26)))
        {
            Undo.RecordObject(targetLine, "Remove Constellation Node");
            nodes.RemoveAt(nodes.Count - 1);
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        // 4. 일괄 색상 변경 버튼 모음
        EditorGUILayout.BeginHorizontal();
        if (true == GUILayout.Button("🌳 전체 나무(황금빛)로 변경", GUILayout.Height(24)))
        {
            Undo.RecordObject(targetLine, "Set All Nodes As Trees");
            for (int i = 0; i < nodes.Count; i++)
            {
                ConstellationNode n = nodes[i];
                n.isBigStar = false;
                nodes[i] = n;
            }
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }

        if (true == GUILayout.Button("⭐ 전체 큰 별(푸른빛)로 변경", GUILayout.Height(24)))
        {
            Undo.RecordObject(targetLine, "Set All Nodes As Big Stars");
            for (int i = 0; i < nodes.Count; i++)
            {
                ConstellationNode n = nodes[i];
                n.isBigStar = true;
                nodes[i] = n;
            }
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (true == GUILayout.Button("✨ 번갈아가며 큰 별(푸른빛) 전환", GUILayout.Height(24)))
        {
            Undo.RecordObject(targetLine, "Alternate Big Star Nodes");
            for (int i = 0; i < nodes.Count; i++)
            {
                ConstellationNode n = nodes[i];
                n.isBigStar = 0 != (i % 2);
                nodes[i] = n;
            }
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }

        if (true == GUILayout.Button("↺ 사진 기본 5개 노드로 초기화", GUILayout.Height(24)))
        {
            Undo.RecordObject(targetLine, "Reset Default 5 Star Nodes");
            nodes.Clear();
            nodes.Add(new ConstellationNode(new Vector3(-3.2f, 0.2f, 0.0f), false));  // 노드 0: 황금빛 나무
            nodes.Add(new ConstellationNode(new Vector3(0.5f, 1.3f, 0.0f), false));   // 노드 1: 상단 푸른 나무
            nodes.Add(new ConstellationNode(new Vector3(0.8f, -0.4f, 0.0f), false));  // 노드 2: 중단 푸른 나무
            nodes.Add(new ConstellationNode(new Vector3(2.8f, -0.2f, 0.0f), false));  // 노드 3: 우측 푸른 나무
            nodes.Add(new ConstellationNode(new Vector3(0.9f, -1.8f, 0.0f), false));  // 노드 4: 하단 푸른 나무
            targetLine.IsClosedLoop = true;
            targetLine.SetNodes(nodes, true);
            bSequentialClickMode = false;
            EditorUtility.SetDirty(targetLine);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        // 5. 노드별 인스펙터 토글 패널
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── [노드별 상태 및 타입 토글] ──", EditorStyles.miniBoldLabel);

        for (int i = 0; i < nodes.Count; i++)
        {
            ConstellationNode node = nodes[i];
            EditorGUILayout.BeginHorizontal("box");

            string typeLabel = true == node.isBigStar ? "⭐ 큰 별 (푸른빛)" : "🌳 나무 (황금빛)";
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = true == node.isBigStar ? new Color(0.4f, 0.7f, 1.0f) : new Color(1.0f, 0.85f, 0.4f);

            EditorGUILayout.LabelField($"Node #{i}", GUILayout.Width(60));

            if (true == GUILayout.Button(typeLabel, GUILayout.Width(130)))
            {
                Undo.RecordObject(targetLine, $"Toggle Node {i} Type");
                node.isBigStar = !node.isBigStar;
                nodes[i] = node;
                targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
                EditorUtility.SetDirty(targetLine);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = originalBg;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = EditorGUILayout.Vector3Field("", node.position);
            if (true == EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetLine, $"Change Node {i} Position");
                node.position = newPos;
                nodes[i] = node;
                targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
                EditorUtility.SetDirty(targetLine);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void OnSceneGUI()
    {
        ConstellationDottedLine targetLine = target as ConstellationDottedLine;
        if (null == targetLine) return;

        List<ConstellationNode> nodes = targetLine.EditorNodes;
        if (null == nodes || 0 == nodes.Count) return;

        Event currentEvent = Event.current;

        // A. 씬 뷰 순서대로 클릭 노드 재배치 처리 (핵심 기능!)
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

            // 마우스 좌클릭 시 해당 노드 위치 갱신
            if (EventType.MouseDown == currentEvent.type && 0 == currentEvent.button)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                float enter = -ray.origin.z / ray.direction.z;
                Vector3 worldHitPos = ray.origin + ray.direction * enter;
                worldHitPos.z = 0.0f;

                Undo.RecordObject(targetLine, $"Click Place Constellation Node {currentClickNodeIndex}");

                if (nodes.Count > currentClickNodeIndex)
                {
                    ConstellationNode node = nodes[currentClickNodeIndex];
                    node.position = worldHitPos;
                    nodes[currentClickNodeIndex] = node;

                    currentClickNodeIndex++;

                    targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
                    EditorUtility.SetDirty(targetLine);

                    // 모든 노드 지정 완료 시 자동 종료
                    if (nodes.Count <= currentClickNodeIndex)
                    {
                        bSequentialClickMode = false;
                        currentClickNodeIndex = 0;
                    }
                }

                currentEvent.Use();
                SceneView.RepaintAll();
                return;
            }

            // 씬 뷰 상단 안내 배너 렌더링
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(20, 20, 360, 64), EditorStyles.helpBox);
            GUILayout.Label($"🖱️ [씬 뷰 연속 클릭 모드]", EditorStyles.boldLabel);
            GUILayout.Label($"클릭하세요: #{currentClickNodeIndex} 노드 ({currentClickNodeIndex + 1} / {nodes.Count}) 위치\n(취소: ESC 키 또는 인스펙터 버튼)", EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        bool bModified = false;

        // B. 씬 뷰 노드 핸들 및 상태 라벨 렌더링
        for (int i = 0; i < nodes.Count; i++)
        {
            ConstellationNode node = nodes[i];
            Vector3 currentPos = node.position;

            // 1. 노드 타입별 컬러
            Color handleColor = true == node.isBigStar ? new Color(0.2f, 0.75f, 1.0f, 1.0f) : new Color(1.0f, 0.82f, 0.1f, 1.0f);
            Handles.color = handleColor;

            string labelText = true == node.isBigStar ? $"★ #{i} 큰별(푸른빛)" : $"● #{i} 나무(황금빛)";
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = handleColor },
                fontSize = 12
            };

            Handles.Label(currentPos + Vector3.up * 0.35f, labelText, labelStyle);

            // 2. 씬 뷰 Position Handle 조작 (클릭 모드가 아닐 때만)
            if (false == bSequentialClickMode)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(currentPos, Quaternion.identity);
                if (true == EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetLine, $"Move Constellation Node {i}");
                    node.position = newPos;
                    nodes[i] = node;
                    bModified = true;
                }
            }
        }

        // C. 노드 간 가이드 점선 라인 표시
        if (1 < nodes.Count)
        {
            Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.35f);
            int segCount = true == targetLine.IsClosedLoop && 2 < nodes.Count ? nodes.Count : nodes.Count - 1;
            for (int s = 0; s < segCount; s++)
            {
                Vector3 pA = nodes[s].position;
                Vector3 pB = nodes[(s + 1) % nodes.Count].position;
                Handles.DrawDottedLine(pA, pB, 4.0f);
            }
        }

        if (true == bModified)
        {
            targetLine.SetNodes(nodes, targetLine.IsClosedLoop);
            EditorUtility.SetDirty(targetLine);
        }
    }
}
