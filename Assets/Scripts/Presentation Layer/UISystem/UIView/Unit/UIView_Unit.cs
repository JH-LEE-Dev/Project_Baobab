using System.Collections.Generic;
using PresentationLayer.ObjectSystem;
using PresentationLayer.UISystem.HUD;
using UnityEngine;

namespace PresentationLayer.UISystem.View
{
    public class UIView_Unit : UIView
    {
        // //외부 의존성
        private IReadOnlyList<ITreeObj> trees;

        // //내부 의존성
        [Header("UI References")]
        [SerializeField] private Transform uiRoot;

        [Header("UI Pools")]
        [SerializeField] private GameObject hpBarPrefab;
        private ObjectPools hpBarPool;

        [Header("Offset Settings")]
        [SerializeField] private float treesYOffset = 1.5f;
        [SerializeField] private float animalsYOffset = 1.5f;
        
        private Dictionary<ITreeObj, HUD_ProgressBar> damagedTrees = new Dictionary<ITreeObj, HUD_ProgressBar>(32);

        // //퍼블릭 초기화 및 제어 메서드

        public override void Initialize(UIViewContext _ctx)
        {
            base.Initialize(_ctx);

            if (null == hpBarPool)
            {
                hpBarPool = gameObject.AddComponent<ObjectPools>();

                if (null != hpBarPool)
                {
                    hpBarPool.Initialize();
                    hpBarPool.Prewarm(hpBarPrefab, 32, this.transform);
                }
            }
        }

        public void TreeGetHit(ITreeObj _treeObj)
        {
            if (null == _treeObj)
            {
                Debug.Log("Null 임");
                return;
            }

            Debug.Log(_treeObj);

            // 이미 캐싱 돼 있으면 시간 연장
            if (damagedTrees.TryGetValue(_treeObj, out HUD_ProgressBar _bar))
            {
                _bar.UpdateValue(_treeObj.health.GetCurrentHealth() / _treeObj.health.GetMaxHealth());
                _bar.TriggerActiveForDuration(3.0f, FinishedBar);
            }
            else
                ShowHP_Trees(_treeObj, treesYOffset);
        }

        public void DependencyInjection(IReadOnlyList<ITreeObj> _trees)
        {
            trees = _trees;
        }

        private void ShowHP_Trees(ITreeObj _treeObj, float _YOffset)
        {   
            if (null == hpBarPool || null == _treeObj)
                return;

            HUD_ProgressBar _bar = hpBarPool.Spawn<HUD_ProgressBar>(hpBarPrefab, Vector3.zero, Quaternion.identity, this.transform);
            if (null == _bar)
                return;

            _bar.UpdateValue(_treeObj.health.GetCurrentHealth() / _treeObj.health.GetMaxHealth());
            _bar.UpdateYOffset(_YOffset);
            _bar.UpdateTargetObj(_treeObj.GetTransform().gameObject);
            
            // 딕셔너리에 등록 (중복 체크는 TreeGetHit에서 수행함)
            damagedTrees.Add(_treeObj, _bar);

            // 3초 동안 활성화하고, 종료 시 풀에 반납하도록 콜백 등록
            _bar.TriggerActiveForDuration(3.0f, FinishedBar);
        }

        private void FinishedBar(HUD_ProgressBar _bar)
        {
            if (null == _bar)
            {
                return;
            }

            // 해당 바를 사용 중이던 나무를 찾아 딕셔너리에서 제거
            ITreeObj _ownerTree = null;
            foreach (var _kvp in damagedTrees)
            {
                if (_kvp.Value == _bar)
                {
                    _ownerTree = _kvp.Key;
                    break;
                }
            }

            if (null != _ownerTree)
            {
                damagedTrees.Remove(_ownerTree);
            }

            hpBarPool?.Despawn(_bar.gameObject);
        }

        // //유니티 이벤트 함수

        protected override void OnShow()
        {
            base.OnShow();
        }

        protected override void OnHide()
        {
            base.OnHide();
        }

        public override void Update()
        {
            base.Update();
            
            // HP 바 위치 추적 등 추가 로직 필요 시 여기에 작성
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
