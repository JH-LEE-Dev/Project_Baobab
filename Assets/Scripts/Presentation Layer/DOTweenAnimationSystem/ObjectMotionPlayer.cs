using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;

namespace PresentationLayer.DOTweenAnimationSystem
{
    [System.Serializable]
    public class MotionEntry
    {
        public string motionTag;
        public ObjectMotionBase motionPrefab;
        public List<MotionTarget> targets = new List<MotionTarget>();

        [HideInInspector] public ObjectMotionBase motionInstance;
    }   

    public struct MotionPlaySettings
    {
        public UnityAction onStart;
        public UnityAction onComplete;
        public bool bReset;
        public bool skip;
        public bool isSkipCallback;
        public float forceDelayForward;
        public float forceDelayBackward;
        public float forceDurationForward;
        public float forceDurationBackward;

        public static MotionPlaySettings Default => new MotionPlaySettings()
        {
            forceDelayForward = -1f,
            forceDelayBackward = -1f,
            forceDurationForward = -1f,
            forceDurationBackward = -1f
        };
    }

    public class ObjectMotionPlayer : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] private List<MotionEntry> motionEntries = new List<MotionEntry>();

        // //내부 의존성
        private Dictionary<string, MotionEntry> motionMap;
        bool bInitialize = false;

        public void Initialize()
        {
            InitializeMotionMap();
        }

        private void Start()
        {
            Initialize();
        }

        private void InitializeMotionMap()
        {
            if (true == bInitialize || null != motionMap)
                return;

            motionMap = new Dictionary<string, MotionEntry>(motionEntries.Count);
            
            for (int i = 0; i < motionEntries.Count; i++)
            {
                if (string.IsNullOrEmpty(motionEntries[i].motionTag))
                    continue;
                
                motionMap[motionEntries[i].motionTag] = motionEntries[i];
            }

            bInitialize = true;
        }

        public MotionEntry PlayBackward(string _tag, MotionPlaySettings _settings)
        {
            if (null == motionMap)
                InitializeMotionMap();

            if (false == motionMap.ContainsKey(_tag))
                return null;

            PlayEntry(motionMap[_tag], true, _settings);

            if (_settings.skip)
            {
                SkipAll(_settings.isSkipCallback);
                StopAll();
            }

            return motionMap[_tag];
        }

        private void PlayEntry(MotionEntry _entry, bool _isBackward, MotionPlaySettings _settings)
        {
            if (null == _entry.motionInstance && null != _entry.motionPrefab)
            {
                _entry.motionInstance = Instantiate(_entry.motionPrefab, this.transform).GetComponent<ObjectMotionBase>();
                _entry.motionInstance.name = $"[Motion]_{_entry.motionTag}";
            }

            if (null == _entry.motionInstance || null == _entry.targets || 0 == _entry.targets.Count)
                return;

            if (_settings.forceDelayForward >= 0f) 
                _entry.motionInstance.SetDelayForward(_settings.forceDelayForward);

            if (_settings.forceDelayBackward >= 0f) 
                _entry.motionInstance.SetDelayBackward(_settings.forceDelayBackward);

            if (_settings.forceDurationForward >= 0f)
                _entry.motionInstance.SetDurationForward(_settings.forceDurationForward);

            if (_settings.forceDurationBackward >= 0f)
                _entry.motionInstance.SetDurationBackward(_settings.forceDurationBackward);

            if (false == _isBackward)
                _entry.motionInstance.Play(_entry.targets, _settings.onStart, _settings.onComplete, _settings.bReset);
            else
                _entry.motionInstance.PlayBackward(_entry.targets, _settings.onStart, _settings.onComplete, _settings.bReset);
        }

        // --- 기존 호환성 메서드 (향후 새 구조체 방식으로 교체 권장) ---
        public MotionEntry Play(string _tag, UnityAction _onStart = null, UnityAction _onComplete = null, 
            bool bReset = false, bool _skip = false, bool _isSkipCallback = false, float _forceDelayForward = -1f, float _forceDelayBackward = -1f)
        {
            MotionPlaySettings settings = MotionPlaySettings.Default;
            settings.onStart = _onStart;
            settings.onComplete = _onComplete;
            settings.bReset = bReset;
            settings.skip = _skip;
            settings.isSkipCallback = _isSkipCallback;
            settings.forceDelayForward = _forceDelayForward;
            settings.forceDelayBackward = _forceDelayBackward;

            return Play(_tag, settings);
        }

        public MotionEntry Play(string _tag, MotionPlaySettings _settings)
        {
            if (null == motionMap)
                InitializeMotionMap();

            if (false == motionMap.ContainsKey(_tag))
                return null;

            PlayEntry(motionMap[_tag], false, _settings);

            if (_settings.skip)
            {
                SkipAll(_settings.isSkipCallback);
                StopAll();
            }

            return motionMap[_tag];
        }

        public MotionEntry PlayBackward(string _tag, UnityAction _onStart = null, UnityAction _onComplete = null, 
            bool bReset = false, bool _skip = false, bool _isSkipCallback = false, float _forceDelayForward = -1f, float _forceDelayBackward = -1f)
        {
            MotionPlaySettings settings = MotionPlaySettings.Default;
            settings.onStart = _onStart;
            settings.onComplete = _onComplete;
            settings.bReset = bReset;
            settings.skip = _skip;
            settings.isSkipCallback = _isSkipCallback;
            settings.forceDelayForward = _forceDelayForward;
            settings.forceDelayBackward = _forceDelayBackward;

            return PlayBackward(_tag, settings);
        }

        public void Stop(string _tag)
        {
            if (null != motionMap && motionMap.ContainsKey(_tag))
                if (null != motionMap[_tag].motionInstance)
                    motionMap[_tag].motionInstance.Stop();
        }

        public bool IsPlaying(string _tag)
        {
            if (null == motionMap)
                InitializeMotionMap();

            if (false == motionMap.ContainsKey(_tag))
                return false;

            return null != motionMap[_tag].motionInstance && motionMap[_tag].motionInstance.IsPlaying();
        }

        public void StopAll()
        {
            for (int i = 0; i < motionEntries.Count; i++)
                if (null != motionEntries[i].motionInstance)
                    motionEntries[i].motionInstance.Stop();
        }

        public void SkipAll(bool _isCallback)
        {
            for (int i = 0; i < motionEntries.Count; i++)
                if (null != motionEntries[i].motionInstance)
                    motionEntries[i].motionInstance.Skip(_isCallback);
        }

        public void Skip(string _tag, bool _isCallback)
        {
            if (null != motionMap && motionMap.ContainsKey(_tag))
                if (null != motionMap[_tag].motionInstance)
                    motionMap[_tag].motionInstance.Skip(_isCallback);
        }

        /// <summary>
        /// 모든 모션을 정지하고 초기 상태(위치, 회전 등)로 되돌립니다.
        /// </summary>
        public void ResetAllMotions()
        {
            for (int i = 0; i < motionEntries.Count; i++)
            {
                if (null != motionEntries[i].motionInstance)
                {
                    motionEntries[i].motionInstance.Stop();
                    motionEntries[i].motionInstance.ResetToInitialState();
                }
            }
        }

        public void SettingEntryMotion(MotionEntry _entry, bool _bStop, bool _bResetPoint, bool _bSkip = false, bool _bSkipCallback = false)
        {
            if (null == _entry)
                return;

            if (_bSkip)
                _entry.motionInstance?.Skip(_bSkipCallback);

            if (_bStop)
                _entry.motionInstance?.Stop();

            if (_bResetPoint)
                _entry.motionInstance?.ResetToInitialState();
        }

        private void OnDestroy()
        {
            StopAll();
        }
    }
}
