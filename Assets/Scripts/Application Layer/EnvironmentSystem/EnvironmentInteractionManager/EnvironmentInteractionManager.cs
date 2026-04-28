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

    // 내부 상태
    private List<IStaticCollidable> nearbyCollidables = new List<IStaticCollidable>(16);
    private List<TreeObj> currentlyFadedTrees = new List<TreeObj>(8);
    private List<TreeObj> treesToReset = new List<TreeObj>(8);

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

    public void Update()
    {
        if (character == null || environmentProvider == null) return;

        UpdateTreeTransparency();
        UpdateUnitShadowColor();
    }

    private void UpdateTreeTransparency()
    {
        // 1. 캐릭터 주변 9개 셀의 나무 수집 (CollisionSystem 이용)
        CollisionSystem.Instance.GetCollidablesInSurroundingCells(character.Position, treeLayer, nearbyCollidables);

        // 2. 현재 투명한 나무들 중 리셋이 필요한 나무 선별
        treesToReset.Clear();
        for (int i = 0; i < currentlyFadedTrees.Count; i++)
        {
            TreeObj _tree = currentlyFadedTrees[i];
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

        // 3. 리셋 대상 처리 및 리스트에서 제거
        for (int i = 0; i < treesToReset.Count; i++)
        {
            treesToReset[i].FadeAlpha(NORMAL_ALPHA, fadeDuration);
            currentlyFadedTrees.Remove(treesToReset[i]);
        }

        // 4. 주변 나무들 중 새로 투명하게 만들어야 할 나무 처리
        for (int i = 0; i < nearbyCollidables.Count; i++)
        {
            if (nearbyCollidables[i] is TreeObj _tree)
            {
                if (IsInsideAlphaDownRange(_tree))
                {
                    if (!currentlyFadedTrees.Contains(_tree))
                    {
                        _tree.FadeAlpha(FADED_ALPHA, fadeDuration);
                        currentlyFadedTrees.Add(_tree);
                    }
                }
            }
        }
    }

    private bool IsInsideAlphaDownRange(TreeObj _tree)
    {
        Vector2 _treeInteractionPos = _tree.Position + _tree.AdColliderOffset;
        float _distanceSqr = (character.Position - _treeInteractionPos).sqrMagnitude;
        float _combinedRadius = _tree.AlphaDownRadius;

        return _distanceSqr <= (_combinedRadius * _combinedRadius);
    }

    private void UpdateUnitShadowColor()
    {
        Quaternion _shadowRot = environmentProvider.shadowDataProvider.CurrentShadowRotation;
        float _shadowScaleY = environmentProvider.shadowDataProvider.CurrentShadowScaleY;

        // 1. 캐릭터 그림자 체크
        CheckUnitShadow(character, _shadowRot, _shadowScaleY);

        // 2. 활성화된 동물들만 그림자 체크 (최적화)
        if (inDungeonUnitSpawner != null && inDungeonUnitSpawner.ActiveAnimals != null)
        {
            var _activeAnimals = inDungeonUnitSpawner.ActiveAnimals;
            for (int i = 0; i < _activeAnimals.Count; i++)
            {
                CheckUnitShadow(_activeAnimals[i], _shadowRot, _shadowScaleY);
            }
        }
    }

    private void CheckUnitShadow(MonoBehaviour _unit, Quaternion _shadowRot, float _shadowScaleY)
    {
        if (_unit == null) return;

        bool _isInShadow = false;
        Vector2 _unitPos = _unit.transform.position;

        // 마을의 활성 나무 체크 (최적화)
        if (townObjectManager != null && townObjectManager.ActiveTrees != null)
        {
            var _activeTrees = townObjectManager.ActiveTrees;
            for (int i = 0; i < _activeTrees.Count; i++)
            {
                if (_activeTrees[i] == null) continue;
                if (IsUnderTreeShadow(_unitPos, _activeTrees[i], _shadowRot, _shadowScaleY))
                {
                    _isInShadow = true;
                    break;
                }
            }
        }

        // 던전의 활성 나무 체크 (최적화, 이미 위에서 찾았다면 스킵)
        if (!_isInShadow && inDungeonObjectManager != null && inDungeonObjectManager.ActiveTrees != null)
        {
            var _activeTrees = inDungeonObjectManager.ActiveTrees;
            for (int i = 0; i < _activeTrees.Count; i++)
            {
                TreeObj _tree = _activeTrees[i];
                if (_tree == null) continue;
                if (IsUnderTreeShadow(_unitPos, _tree, _shadowRot, _shadowScaleY))
                {
                    _isInShadow = true;
                    break;
                }
            }
        }

        // 결과 적용
        if (_unit is Character _c) _c.SetInShadow(_isInShadow, shadowFadeDuration);
        else if (_unit is Animal _a) _a.SetInShadow(_isInShadow, shadowFadeDuration);
    }

    private bool IsUnderTreeShadow(Vector2 _unitPos, TreeObj _tree, Quaternion _shadowRot, float _shadowScaleY)
    {
        // 1. 나무 위치 기준 상대 좌표 계산
        Vector2 _relativePos = _unitPos - (Vector2)_tree.transform.position;
        
        // 2. 그림자 회전의 역행렬을 적용하여 그림자 로컬 좌표계로 변환 (그림자 방향을 Y축으로 정렬)
        Vector2 _localPos = Quaternion.Inverse(_shadowRot) * _relativePos;

        // 3. 그림자 중심점(오프셋)과의 차이 계산
        float _dx = _localPos.x - _tree.TopShadowOffset.x;
        float _dy = _localPos.y - _tree.TopShadowOffset.y;

        // 4. 타원 판정 로직 (Y축 스케일 반영)
        // (x/radius)^2 + (y/(radius*scaleY))^2 <= 1
        float _radius = _tree.TopShadowRadius;
        if (_radius <= 0) return false;

        float _normalizedX = _dx / _radius;
        float _normalizedY = _dy / (_radius * _shadowScaleY*1.5f);

        return (_normalizedX * _normalizedX) + (_normalizedY * _normalizedY) <= 1.0f;
    }
}
