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
        
        private Dictionary<ITreeObj, HUD_ProgressBar> hitTrees = new Dictionary<ITreeObj, HUD_ProgressBar>(32);

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
                return;
            }

            if (hitTrees.TryGetValue(_treeObj, out HUD_ProgressBar _bar))
            {
                // 이미 HP 바가 떠 있는 경우: 값 업데이트 및 시간 연장
                _bar.UpdateValue(_treeObj.health.GetCurrentHealth());
                _bar.TriggerActiveForDuration(3.0f, FinishedBar);
            }
            else
            {
                // 새로 생성하여 노출
                OnShow_HPBar(_treeObj);
            }
        }

        public void DependencyInjection(IReadOnlyList<ITreeObj> _trees)
        {
            trees = _trees;
        }

        private void OnShow_HPBar(ITreeObj _treeObj)
        {   
            if (null == hpBarPool || null == _treeObj)
                return;

            HUD_ProgressBar _bar = hpBarPool.Spawn<HUD_ProgressBar>(hpBarPrefab, Vector3.zero, Quaternion.identity, this.transform);
            if (null == _bar)
                return;

            _bar.SetMaxValue(_treeObj.health.GetMaxHealth());
            _bar.UpdateValue(_treeObj.health.GetCurrentHealth());
            _bar.UpdateTargetObj(_treeObj.GetTransform().gameObject);
            
            // 딕셔너리에 등록 (중복 체크는 TreeGetHit에서 수행함)
            hitTrees.Add(_treeObj, _bar);

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
            foreach (var _kvp in hitTrees)
            {
                if (_kvp.Value == _bar)
                {
                    _ownerTree = _kvp.Key;
                    break;
                }
            }

            if (null != _ownerTree)
            {
                hitTrees.Remove(_ownerTree);
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
