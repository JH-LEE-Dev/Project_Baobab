using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Presentation.UISystem.Animation
{
    /// <summary>
    /// 모든 모션을 일관성 있게 관리하고 재생하는 통합 매니저 클래스
    /// </summary>
    public class ObjectMotionPlayer : MonoBehaviour
    {
        private class StepContext
        {
            public int index;
            public string tag;
            public Action<int, string> action;
            public void Call() => action?.Invoke(index, tag);
        }

        [Serializable]
        public struct MotionEntry
        {
            [Tooltip("비어있을 경우 스크립트의 기본 태그를 사용합니다.")]
            public string tag;                  
            public ObjectMotionBlockBase block; 
        }

        // //외부 의존성
        [Header("Motion Settings")]
        [SerializeField] private List<MotionEntry> motionLibrary = new List<MotionEntry>();

        // //내부 의존성
        private Transform targetTransform;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private SpriteRenderer spriteRenderer;
        private Sequence currentSequence;

        // 런타임 통합 저장소 (최적화된 조회용)
        private readonly Dictionary<string, ObjectMotionBlockBase> runtimeLibrary = new Dictionary<string, ObjectMotionBlockBase>(8);

        private readonly Stack<StepContext> contextPool = new Stack<StepContext>(10);
        private readonly List<StepContext> activeContexts = new List<StepContext>(10);

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            targetTransform = transform;
            rectTransform = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            runtimeLibrary.Clear();

            // 1. 자신에게 부착된 블록 자동 수집 (기본값)
            ObjectMotionBlockBase[] attachedBlocks = GetComponents<ObjectMotionBlockBase>();
            for (int i = 0; i < attachedBlocks.Length; i++)
            {
                RegisterToLibrary(attachedBlocks[i].MotionTag, attachedBlocks[i]);
            }

            // 2. 인스펙터 라이브러리 등록 (프리팹이나 수동 설정으로 덮어쓰기 가능)
            for (int i = 0; i < motionLibrary.Count; i++)
            {
                var entry = motionLibrary[i];
                if (entry.block != null)
                {
                    // 인스펙터에 별도의 태그를 적었다면 그것을 쓰고, 아니면 스크립트의 태그를 사용
                    string finalTag = string.IsNullOrEmpty(entry.tag) ? entry.block.MotionTag : entry.tag;
                    RegisterToLibrary(finalTag, entry.block);
                }
            }
        }

        private void RegisterToLibrary(string _tag, ObjectMotionBlockBase _block)
        {
            if (string.IsNullOrEmpty(_tag) || _block == null) return;
            runtimeLibrary[_tag] = _block;
        }

        public Transform TargetTransform => targetTransform;
        public RectTransform TargetRect => rectTransform;
        public CanvasGroup TargetCanvasGroup => canvasGroup;
        public SpriteRenderer TargetSpriteRenderer => spriteRenderer;

        /// <summary>
        /// 태그를 사용하여 등록된 특정 모션을 재생합니다. (FSM 방식)
        /// </summary>
        public void PlayMotion(string _tag, Action<int, string> _onStepStart = null, Action<int, string> _onStepComplete = null, TweenCallback _onComplete = null)
        {
            if (runtimeLibrary.TryGetValue(_tag, out ObjectMotionBlockBase block))
            {
                ExecuteBlock(block, _onStepStart, _onStepComplete, _onComplete);
            }
            else
            {
                Debug.LogWarning($"[ObjectMotionPlayer] {gameObject.name} : '{_tag}' 태그의 모션을 찾을 수 없습니다.");
            }
        }

        public TweenCallback GetStepCallback(int _index, string _tag, Action<int, string> _action)
        {
            if (_action == null) 
                return null;

            StepContext context = (contextPool.Count > 0) ? contextPool.Pop() : new StepContext();

            context.index = _index;
            context.tag = _tag;
            context.action = _action;

            activeContexts.Add(context);

            return context.Call;
        }

        private void ExecuteBlock(ObjectMotionBlockBase _block, Action<int, string> _onStepStart, Action<int, string> _onStepComplete, TweenCallback _onComplete)
        {
            if (_block == null) 
                return;

            ResetContexts();
            currentSequence?.Kill();
            currentSequence = _block.BuildSequence(this, _onStepStart, _onStepComplete);

            if (_onComplete != null) 
                currentSequence.OnComplete(_onComplete);

            currentSequence.Play();
        }

        private void ResetContexts()
        {
            for (int i = 0; i < activeContexts.Count; i++)
            {
                activeContexts[i].action = null;
                contextPool.Push(activeContexts[i]);
            }
            
            activeContexts.Clear();
        }

        // //유니티 이벤트 함수
        private void Awake() => Initialize();
        private void OnDestroy()
        {
            currentSequence?.Kill();
            ResetContexts();
            contextPool.Clear();
            runtimeLibrary.Clear();
        }
    }
}