using System;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 마을 전리품 기둥 빛(VFX_LootPillarGlow)이 쓰는 전시 프레임별 미리 계산 데이터.
    /// 런타임에 텍스처를 읽지 않도록 에디터 빌더(LootPillarGlowBuilder)가 LootGrowthVisualData의 전시 프레임마다 한 번 구워 둔다.
    /// - 후광: 실루엣을 가우시안으로 번지게 한 글로우 스프라이트(아틀라스 한 장의 조각). 피벗은 원본 프레임 피벗과 같은 자리.
    /// - 중심: 불투명 칸 중심의 평균(뒤 빛 원반 자리)
    /// - 경계: 불투명 칸의 범위(가장자리 반짝임 자리)
    /// 색은 담지 않는다(LootDisplayObject가 성장 단계 / 종류별 색을 넘긴다).
    /// </summary>
    [CreateAssetMenu(fileName = "LootPillarGlowData", menuName = "Game/VFX/Loot Pillar Glow Data")]
    public class LootPillarGlowData : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            [Tooltip("실루엣 블러 후광(흰색 + 알파). 피벗은 원본 프레임 피벗과 같은 자리, PPU도 같다.")]
            public Sprite halo;
            [Tooltip("불투명 칸 중심의 평균(피벗 기준 px, y 위쪽).")]
            public Vector2 centroid;
            [Tooltip("불투명 칸 경계(피벗 기준 칸 번호, 양끝 포함): x = minX, y = minY, z = maxX, w = maxY. 칸 i는 [i, i+1) px.")]
            public Vector4 bounds;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public int Count => null != entries ? entries.Length : 0;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 스프라이트의 항목 번호를 찾는다(전시 프레임이 수십 장뿐이고 프레임이 바뀔 때만 부르므로 선형 탐색). 없으면 -1.
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
