using System;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 희귀 전리품 등장 이펙트(VFX_RareLootAppear)가 쓰는 스프라이트별 미리 계산 데이터.
    /// 런타임에 텍스처를 읽지 않도록 에디터 빌더(RareLootAppearBuilder)가 전리품 스프라이트마다 한 번 구워 둔다.
    /// - 색은 담지 않는다(모든 전리품이 같은 황금빛이며 VFX_RareLootAppear 인스펙터에서 정한다).
    /// - 실루엣: 불투명 칸의 피벗 기준 픽셀 오프셋(백열 섬광용)
    /// - 둘레 빛: 실루엣을 부드럽게 번지게 한 글로우 스프라이트(아이템 뒤에 반투명으로 깐다). 1px 외곽선은 LootItem의 아웃라인이 그린다.
    /// </summary>
    [CreateAssetMenu(fileName = "RareLootVfxData", menuName = "Game/VFX/Rare Loot Vfx Data")]
    public class RareLootVfxData : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            [Tooltip("불투명 칸의 피벗 기준 픽셀 오프셋(y 위쪽).")]
            public Vector2Int[] silhouette;
            [Tooltip("실루엣 모양 블러 글로우(흰색 + 알파). 피벗은 원본 스프라이트 피벗과 같은 자리.")]
            public Sprite rimGlow;
            [Tooltip("실루엣 경계(피벗 기준 픽셀, 양끝 포함): x = minX, y = minY, z = maxX, w = maxY.")]
            public Vector4 bounds;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public int Count => null != entries ? entries.Length : 0;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 스프라이트의 항목 번호를 찾는다(항목이 몇 개뿐이라 선형 탐색). 없으면 -1.
        /// </summary>
        public int IndexOf(Sprite _sprite)
        {
            if (null == _sprite || null == entries) return -1;

            for (int i = 0; i < entries.Length; i++)
            {
                if (_sprite == entries[i].sprite) return i;
            }

            return -1;
        }

        public ref readonly Entry Get(int _index)
        {
            return ref entries[_index];
        }

#if UNITY_EDITOR
        public void SetEditorEntries(Entry[] _entries)
        {
            entries = _entries;
        }
#endif
    }
}
