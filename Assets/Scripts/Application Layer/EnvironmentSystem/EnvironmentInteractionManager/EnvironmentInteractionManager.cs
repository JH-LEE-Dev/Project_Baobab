using System.Collections.Generic;
using UnityEngine;

public class EnvironmentInteractionManager : MonoBehaviour
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private Character character;
    private TownObjectManager townObjectManager;
    private InDungeonObjectManager inDungeonObjectManager;
    private InDungeonUnitSpawner inDungeonUnitSpawner;

    [SerializeField] private LayerMask treeLayer;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float shadowFadeDuration = 0.1f;
    [SerializeField] private float shadowOffsetMinScale = 0.7f; // 그림자 스케일이 최소일 때 Offset이 줄어드는 비율
    [SerializeField] private float shadowLengthDamping = 0.8f; // 그림자 장축 스케일이 줄어드는 정도를 완화하는 비율 (1: 그대로, 0: 항상 1)

    // 내부 상태 (최적화: HashSet 사용으로 중복 체크 O(1) 달성)
    private readonly List<IStaticCollidable> nearbyCollidables = new List<IStaticCollidable>(16);
    private readonly HashSet<TreeObj> currentlyFadedTrees = new HashSet<TreeObj>();
    private readonly List<TreeObj> treesToReset = new List<TreeObj>(8);

    private const float FADED_ALPHA = 0.5f;
    private const float NORMAL_ALPHA = 1.0f;

    public void Initialize()
    {

    }

    public void DI_Character(Character _character)
    {
        character = _character;
    }

    public void DI(IEnvironmentProvider _environmentProvider,
        TownObjectManager _townObjectManager,
        InDungeonObjectManager _inDungeonObjectManager,
        InDungeonUnitSpawner _inDungeonUnitSpawner)
    {
        environmentProvider = _environmentProvider;
        townObjectManager = _townObjectManager;
        inDungeonObjectManager = _inDungeonObjectManager;
        inDungeonUnitSpawner = _inDungeonUnitSpawner;
    }

    [Header("Optimization Settings")]
    [SerializeField] private float updateInterval = 0.1f; // 판정 주기 (초)
    private float updateTimer = 0f;

    public void Update()
    {
        if (character == null || environmentProvider == null) return;

        // 최적화: 타이머 기반 업데이트 스로틀링
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        UpdateTreeTransparency();
        UpdateUnitShadowColor();
    }

    private void UpdateTreeTransparency()
    {
        // 1. 캐릭터 주변 나무 수집
        CollisionSystem.Instance.GetCollidablesInSurroundingCells(character.Position, treeLayer, nearbyCollidables);

        // 2. 현재 투명한 나무들 중 리셋 대상 선별
        treesToReset.Clear();
        foreach (var _tree in currentlyFadedTrees)
        {
            if (_tree == null) continue;

            bool _isStillNearby = false;
            for (int j = 0; j < nearbyCollidables.Count; j++)
            {
                if (ReferenceEquals(nearbyCollidables[j], _tree))
                {
                    _isStillNearby = true;
                    break;
                }
            }

            if (!_isStillNearby || !IsInsideAlphaDownRange(_tree))
            {
                treesToReset.Add(_tree);
            }
        }

        // 3. 리셋 처리 (HashSet에서 O(1) 삭제)
        for (int i = 0; i < treesToReset.Count; i++)
        {
            var _tree = treesToReset[i];
            _tree.FadeAlpha(NORMAL_ALPHA, fadeDuration);
            currentlyFadedTrees.Remove(_tree);
        }

        // 4. 새롭게 투명해질 나무 처리
        for (int i = 0; i < nearbyCollidables.Count; i++)
        {
            if (nearbyCollidables[i] is TreeObj _tree)
            {
                if (IsInsideAlphaDownRange(_tree))
                {
                    if (currentlyFadedTrees.Add(_tree)) // Add 성공 시(중복 아닐 때)에만 페이드 실행
                    {
                        _tree.FadeAlpha(FADED_ALPHA, fadeDuration);
                    }
                }
            }
        }
    }

    private bool IsInsideAlphaDownRange(TreeObj _tree)
    {
        Vector2 _diff = character.Position - (_tree.Position + _tree.AdColliderOffset);
        float _distanceSqr = _diff.sqrMagnitude;
        float _radius = _tree.AlphaDownRadius;

        return _distanceSqr <= (_radius * _radius);
    }

    private void UpdateUnitShadowColor()
    {
        var _shadowData = environmentProvider.shadowDataProvider;
        // CurrentShadowRotation에 Z축 90도 회전을 더한 값으로 Inverse 처리
        Quaternion _invShadowRot = Quaternion.Inverse(_shadowData.CurrentShadowRotation * Quaternion.Euler(0, 0, 90f));
        float _shadowScaleY = _shadowData.CurrentShadowScaleY; // 미리 계산

        // 1. 캐릭터 체크
        CheckUnitShadow(character, _invShadowRot, _shadowScaleY);

        // 2. 활성 동물 체크
        if (inDungeonUnitSpawner != null)
        {
            var _activeAnimals = inDungeonUnitSpawner.ActiveAnimals;
            for (int i = 0; i < _activeAnimals.Count; i++)
            {
                CheckUnitShadow(_activeAnimals[i], _invShadowRot, _shadowScaleY);
            }
        }
    }

    private void CheckUnitShadow(MonoBehaviour _unit, Quaternion _invShadowRot, float _shadowScaleY)
    {
        if (_unit == null) return;

        bool _isInShadow = false;
        Vector2 _unitPos = _unit.transform.position;

        // 마을 나무
        if (townObjectManager != null)
        {
            var _activeTrees = townObjectManager.ActiveTrees;
            for (int i = 0; i < _activeTrees.Count; i++)
            {
                if (_activeTrees[i] == null) continue;
                if (IsUnderTreeShadow(_unitPos, _activeTrees[i], _invShadowRot, _shadowScaleY))
                {
                    _isInShadow = true;
                    break;
                }
            }
        }

        // 던전 나무
        if (!_isInShadow && inDungeonObjectManager != null)
        {
            var _activeTrees = inDungeonObjectManager.ActiveTrees;
            for (int i = 0; i < _activeTrees.Count; i++)
            {
                if (_activeTrees[i] == null) continue;
                if (IsUnderTreeShadow(_unitPos, _activeTrees[i], _invShadowRot, _shadowScaleY))
                {
                    _isInShadow = true;
                    break;
                }
            }
        }

        if (_unit is Character _c) _c.SetInShadow(_isInShadow, shadowFadeDuration);
        else if (_unit is Animal _a) _a.SetInShadow(_isInShadow, shadowFadeDuration);
    }

    private bool IsUnderTreeShadow(Vector2 _unitPos, TreeObj _tree, Quaternion _invShadowRot, float _shadowScaleY)
    {
        // 1. 장축 배율 보정 (원형인 1.0을 기준으로 덜 줄어들고 덜 늘어나게 함)
        float _baseScaleY = _shadowScaleY * 3.0f;
        float _effectiveScaleY = Mathf.Lerp(1.0f, _baseScaleY, shadowLengthDamping);

        var _shadowData = environmentProvider.shadowDataProvider;
        Vector2 _treePos = _tree.transform.position;
        float _radius = _tree.TopShadowRadius;
        if (_radius <= 0) return false;

        // 최적화 1: 조기 종료 (그림자의 최대 길이보다 멀리 있으면 연산 건너뜀)
        float _maxRange = _radius * Mathf.Max(1.0f, _effectiveScaleY) + 0.5f;
        if (Vector2.SqrMagnitude(_unitPos - _treePos) > (_maxRange * _maxRange)) return false;

        // 2. 로컬 좌표계 변환
        Vector2 _localPos = _invShadowRot * (_unitPos - _treePos);

        // 3. 중심점(Offset) 동적 보정
        // ScaleY가 Max일 때가 기본 Offset이며, ScaleY가 작아질수록 Offset도 shadowOffsetMinScale 범위까지 줄어듭니다.
        float _scaleFactor = Mathf.InverseLerp(_shadowData.minHeightScale, _shadowData.maxHeightScale, _shadowData.CurrentShadowScaleY);
        float _offsetMultiplier = Mathf.Lerp(shadowOffsetMinScale, 1.0f, _scaleFactor);
        Vector2 _dynamicOffset = _tree.TopShadowOffset * _offsetMultiplier;

        float _dx = _localPos.x - _dynamicOffset.x;
        float _dy = _localPos.y - _dynamicOffset.y;

        // 4. 타원 판정 (나눗셈 제거 최적화)
        float _ry = _radius * _effectiveScaleY;
        float _term1 = _dx * _ry;
        float _term2 = _dy * _radius;

        return (_term1 * _term1) + (_term2 * _term2) <= (_ry * _ry * _radius * _radius);
    }
}
