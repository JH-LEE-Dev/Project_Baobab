using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class ObjectMotionPlayer : MonoBehaviour
    {
        [System.Serializable]
        public class MotionEntry
        {
            public string motionTag;
            public ObjectMotionBase motionPrefab;
            public List<MotionTarget> targets = new List<MotionTarget>();
            
            [HideInInspector] public ObjectMotionBase motionInstance;
        }

        // //외부 의존성
        [SerializeField] private List<MotionEntry> motionEntries = new List<MotionEntry>();

        // //내부 의존성
        private Dictionary<string, MotionEntry> motionMap;

        private void Initialize()
        {
            InitializeMotionMap();
        }

        private void Start()
        {
            Initialize();
            Play("Test");
        }

        private void InitializeMotionMap()
        {
            if (null != motionMap)
                return;

            motionMap = new Dictionary<string, MotionEntry>(motionEntries.Count);
            
            for (int i = 0; i < motionEntries.Count; i++)
            {
                if (string.IsNullOrEmpty(motionEntries[i].motionTag))
                    continue;
                
                motionMap[motionEntries[i].motionTag] = motionEntries[i];
            }
        }

        public void Play(string _tag, UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == motionMap)
                InitializeMotionMap();

            if (false == motionMap.ContainsKey(_tag))
                return;

            PlayEntry(motionMap[_tag], _onStart, _onComplete);
        }

        public void Play(string _tag, float _duration, float _delay, UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == motionMap)
                InitializeMotionMap();

            if (false == motionMap.ContainsKey(_tag))
                return;

            MotionEntry _entry = motionMap[_tag];
            
            // 인스턴스 초기화 보장
            if (null == _entry.motionInstance)
            {
                if (null == _entry.motionPrefab)
                    return;

                _entry.motionInstance = Instantiate(_entry.motionPrefab, this.transform);
                _entry.motionInstance.name = $"[Motion]_{_entry.motionTag}";
            }

            if (null != _entry.motionInstance)
                _entry.motionInstance.SetRuntimeSettings(_duration, _delay);

            PlayEntry(_entry, _onStart, _onComplete);
        }

        private void PlayEntry(MotionEntry _entry, UnityAction _onStart, UnityAction _onComplete)
        {
            if (null == _entry.motionInstance && null != _entry.motionPrefab)
            {
                _entry.motionInstance = Instantiate(_entry.motionPrefab, this.transform);
                _entry.motionInstance.name = $"[Motion]_{_entry.motionTag}";
            }

            if (null == _entry.motionInstance || null == _entry.targets || 0 == _entry.targets.Count)
                return;

            _entry.motionInstance.Play(_entry.targets, _onStart, _onComplete);
        }

        public void Stop(string _tag)
        {
            if (null != motionMap && motionMap.ContainsKey(_tag))
                if (null != motionMap[_tag].motionInstance)
                    motionMap[_tag].motionInstance.Stop();
        }

        public void StopAll()
        {
            for (int i = 0; i < motionEntries.Count; i++)
                if (null != motionEntries[i].motionInstance)
                    motionEntries[i].motionInstance.Stop();
        }

        private void OnDestroy()
        {
            StopAll();
        }
    }
}