using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// AudioCueData 인스펙터: Clips 목록의 각 줄에 클립과 개별 음량(clipVolumes)을 나란히 그린다.
// clipVolumes는 clips와 같은 순서의 별도 리스트라, 추가·삭제·순서 변경 때 함께 맞춰 준다.
[CustomEditor(typeof(AudioCueData))]
public class AudioCueDataEditor : Editor
{
    private const float VolumeMin = 0f;
    private const float VolumeMax = 2f;
    private const float VolumeWidth = 150f;

    private SerializedProperty clipsProp;
    private SerializedProperty volumesProp;
    private ReorderableList list;

    private void OnEnable()
    {
        clipsProp = serializedObject.FindProperty("clips");
        volumesProp = serializedObject.FindProperty("clipVolumes");

        list = new ReorderableList(serializedObject, clipsProp, true, true, true, true);
        list.drawHeaderCallback = rect =>
        {
            Rect clipRect = new Rect(rect.x, rect.y, rect.width - VolumeWidth - 4f, rect.height);
            Rect volRect = new Rect(rect.xMax - VolumeWidth, rect.y, VolumeWidth, rect.height);
            EditorGUI.LabelField(clipRect, "Clip");
            EditorGUI.LabelField(volRect, "Volume");
        };
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            SyncVolumeCount();
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            Rect clipRect = new Rect(rect.x, rect.y, rect.width - VolumeWidth - 4f, rect.height);
            Rect volRect = new Rect(rect.xMax - VolumeWidth, rect.y, VolumeWidth, rect.height);

            EditorGUI.PropertyField(clipRect, clipsProp.GetArrayElementAtIndex(index), GUIContent.none);
            SerializedProperty vol = volumesProp.GetArrayElementAtIndex(index);
            vol.floatValue = EditorGUI.Slider(volRect, vol.floatValue, VolumeMin, VolumeMax);
        };
        list.onAddCallback = l =>
        {
            SyncVolumeCount();
            int index = clipsProp.arraySize;
            clipsProp.arraySize++;
            clipsProp.GetArrayElementAtIndex(index).objectReferenceValue = null;
            volumesProp.arraySize = clipsProp.arraySize;
            volumesProp.GetArrayElementAtIndex(index).floatValue = 1f;
            l.index = index;
        };
        list.onRemoveCallback = l =>
        {
            SyncVolumeCount();
            int index = l.index;
            if (index < 0 || index >= clipsProp.arraySize) return;
            // 오브젝트 참조 원소는 첫 DeleteArrayElementAtIndex가 null로만 비우므로 크기를 직접 줄인다.
            for (int i = index; i < clipsProp.arraySize - 1; i++)
            {
                clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clipsProp.GetArrayElementAtIndex(i + 1).objectReferenceValue;
                volumesProp.GetArrayElementAtIndex(i).floatValue = volumesProp.GetArrayElementAtIndex(i + 1).floatValue;
            }
            clipsProp.arraySize--;
            volumesProp.arraySize = clipsProp.arraySize;
            l.index = Mathf.Clamp(index, 0, clipsProp.arraySize - 1);
        };
        list.onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
        {
            SyncVolumeCount();
            volumesProp.MoveArrayElement(oldIndex, newIndex);
        };
    }

    // clipVolumes 개수를 clips에 맞춘다. 새로 생긴 칸은 1(원래 음량).
    private void SyncVolumeCount()
    {
        int oldSize = volumesProp.arraySize;
        int target = clipsProp.arraySize;
        if (oldSize == target) return;
        volumesProp.arraySize = target;
        for (int i = oldSize; i < target; i++)
            volumesProp.GetArrayElementAtIndex(i).floatValue = 1f;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScriptableObject)target), typeof(MonoScript), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
        list.DoLayoutList();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("playAllClips"));

        DrawPropertiesExcluding(serializedObject, "m_Script", "clips", "clipVolumes", "playAllClips");

        serializedObject.ApplyModifiedProperties();
    }
}
