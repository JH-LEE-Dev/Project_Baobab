using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;
using System;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    public enum TileType
    {
        Ground,
        Water
    }

    [Serializable]
    public struct MapTreeVisual
    {
        public Image leafImage;
        public Image trunkImage;
    }

    public struct MapTreeColor
    {
        public Color leafColor;
        public Color trunkColor;
    }

    /// <summary>
    /// 특정 지역(Region)의 시각적 요소(지형, 나무, 데코, 이름)를 관리하고 애니메이션을 재생하는 클래스입니다.
    /// 해당 지역의 MapType 정보를 보유하여 상위 매니저와 소통합니다.
    /// </summary>
    public class HUD_MapRegion : MonoBehaviour
    {
        // //외부 의존성
        [Header("Ground Visuals")]
        [SerializeField] private Image[] groundImages;      // 지형 이미지 4개

        [Header("Object Visuals")]
        [SerializeField] private MapTreeVisual[] treeVisuals; // 나무 비주얼 (잎, 기둥)
        [SerializeField] private Image[] decoImages;        // 동물 대신 들어간 데코 이미지들

        [Header("State Visuals")]
        [SerializeField] private GameObject lockObject;     // 잠금 시 활성화될 오브젝트
        [SerializeField] private GameObject unlockObject;   // 해제 시 활성화될 오브젝트

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI mapNameText; // 맵 이름 TMP

        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;
        [SerializeField] private float groundShowDelay = 0.015f;

        [Header("Focus Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float dimFactor = 0.5f;     // 비포커스 시 명암 계수

        [Header("Theme Configuration")]
        [SerializeField] private HUD_MapThemeConfig themeConfig;

        // //내부 의존성
        private Color[] groundOriginalColors;
        private MapTreeColor[] treeOriginalColors;
        private Color[] decoOriginalColors;
        private int[] shufflePool;                           // 가비지 프리 셔플 풀
        private int[] groundTileIndices;                     // 땅 타일 위치 추적용 풀

        private MapEnvironmentDataInfo mapEnvironmentInfo;
        private string currentMapName = string.Empty;
        private float uiAlpha = 1.0f;
        private int currentVisibleCount = 1; 
        private bool isLocked = false;
        private bool isFocused = false;
        private bool isInitialized = false;

        // GC Alloc 최적화를 위한 문자열 캐싱
        private static readonly string[] groundTags = 
        { 
            "Ground_1", "Ground_2", "Ground_3", "Ground_4", 
            "Ground_5", "Ground_6", "Ground_7", "Ground_8",
            "Ground_9", "Ground_10", "Ground_11", "Ground_12",
            "Ground_13", "Ground_14", "Ground_15", "Ground_16"
        };

        private static readonly string[] treeTags = { "Tree_1", "Tree_2", "Tree_3" };
        private static readonly string[] decoTags = { "Deco_1", "Deco_2", "Deco_3" };

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            CaptureOriginalColors();

            shufflePool = new int[16];
            groundTileIndices = new int[16];

            currentVisibleCount = 1;
            isInitialized = true;
        }

        public void Setup(string _mapName, MapEnvironmentDataInfo _info, bool _shouldPlayAnimation = false, bool _isInstant = false)
        {
            if (false == isInitialized)
                Initialize();

            currentMapName = _mapName;
            mapEnvironmentInfo = _info;

            if (null != mapNameText)
                mapNameText.text = currentMapName;

            ApplyTheme(_info.mapType);

            // 초기 리전 셋업 시 모든 나무는 100% 항상 활성화 상태 유지
            currentVisibleCount = 1;
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    if (null != treeVisuals[_i].leafImage)
                        treeVisuals[_i].leafImage.gameObject.SetActive(true);

                    if (null != treeVisuals[_i].trunkImage)
                        treeVisuals[_i].trunkImage.gameObject.SetActive(true);
                }
            }

            ResetObjectsVisibility(); // 데코 등의 노출 개수 초기 리셋

            if (true == _shouldPlayAnimation)
                PlayStartGroundAnimation();
            else if (true == _isInstant)
                PlayStartAnimationInstant();
        }

        /// <summary>
        /// 서브 지역 등급에 따라 데코 노출을 제어하고, 해당 서브 지역용 식생 테마(나무 종류들)를 겹침 없이 실시간 스왑 배정합니다.
        /// </summary>
        public void UpdateObjectCount(int _count)
        {
            if (false == isInitialized)
                Initialize();

            if (_count <= 0 || _count > 3)
                return;

            if (currentVisibleCount == _count)
                return;

            // 서브지역 선택에 맞춘 나무 식생 실시간 스왑 및 모든 종류 강제 노출
            SwapTreesForSubRegion(_count);

            // 데코 연출 조절
            if (null != decoImages)
            {
                for (int _i = 0; _i < decoImages.Length; _i++)
                {
                    bool _shouldBeVisible = (_i < _count);
                    bool _wasVisible = (_i < currentVisibleCount);

                    if (_shouldBeVisible && !_wasVisible)
                    {
                        if (null != decoImages[_i]) decoImages[_i].gameObject.SetActive(true);

                        if (_i < decoTags.Length)
                            motionPlayer.Play(decoTags[_i], bReset: true);
                    }
                    else if (!_shouldBeVisible && _wasVisible)
                    {
                        if (_i < decoTags.Length)
                            motionPlayer.PlayBackward(decoTags[_i], bReset: true);
                    }
                }
            }

            currentVisibleCount = _count;
        }

        public void SetLock(bool _isLock)
        {
            isLocked = _isLock;

            if (null != lockObject)
                lockObject.SetActive(isLocked);

            if (null != unlockObject)
                unlockObject.SetActive(false == isLocked);
        }

        public void SetUIAlpha(float _alpha)
        {
            uiAlpha = _alpha;
            ApplyVisualState();
        }

        public void SetFocus(bool _isFocus)
        {
            isFocused = _isFocus;
            ApplyVisualState();
        }

        public void PlayStartGroundAnimation()
        {
            if (null == groundImages || null == groundTags)
                return;

            int _loopCount = Mathf.Min(groundImages.Length, groundTags.Length);
            int _finalIdx = _loopCount - 1;

            for (int _i = 0; _i < _loopCount; _i++)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(groundTags[_i], bReset: true, _onComplete: PlayStartTreeAnimation, _forceDelayForward: groundShowDelay * _i);
                else
                    motionPlayer.Play(groundTags[_i], bReset: true, _forceDelayForward: groundShowDelay * _i);
            }
        }

        public void PlayStartTreeAnimation()
        {
            if (null == treeVisuals || null == treeTags)
                return;

            int _targetCount = Mathf.Min(currentVisibleCount, treeVisuals.Length);
            _targetCount = Mathf.Min(_targetCount, treeTags.Length);

            int _finalIdx = _targetCount - 1;
            const float _delay = 0.05f;

            if (_targetCount <= 0)
            {
                PlayStartDecoAnimation();
                return;
            }

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(treeTags[_i], bReset: true, _onComplete: PlayStartDecoAnimation, _forceDelayForward: _delay * _i);
                else
                    motionPlayer.Play(treeTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayStartDecoAnimation()
        {
            if (null == decoImages || null == decoTags)
                return;

            int _targetCount = Mathf.Min(currentVisibleCount, decoImages.Length);
            _targetCount = Mathf.Min(_targetCount, decoTags.Length);
            
            const float _delay = 0.05f;

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == decoImages[_i])
                    continue;

                motionPlayer.Play(decoTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayEndAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            PlayEndDecoAnimation(_onComplete, _isSkip);
            PlayEndTreeAnimation(_onComplete, _isSkip);
            PlayEndGroundAnimation(_onComplete, _isSkip);
        }

        public MapEnvironmentDataInfo GetMapEnvironmentInfo() => mapEnvironmentInfo;
        public bool IsLocked() => isLocked;
        public string GetMapName() => currentMapName;
        public MapType GetMapType() => mapEnvironmentInfo.mapType;

        // //내부 로직

        /// <summary>
        /// 맵 타입에 맞게 오토타일링(물 연결 & 물가 감지) 및 나무/데코의 땅(Ground) 위 동적 앵커 스냅 배치를 처리합니다.
        /// </summary>
        private void ApplyTheme(MapType _mapType)
        {
            if (null == themeConfig || null == themeConfig.MapThemes)
                return;

            MapThemeData _targetTheme = default;
            bool _found = false;

            for (int _i = 0; _i < themeConfig.MapThemes.Length; _i++)
            {
                if (themeConfig.MapThemes[_i].mapType == _mapType)
                {
                    _targetTheme = themeConfig.MapThemes[_i];
                    _found = true;
                    break;
                }
            }

            if (false == _found)
                return;

            // 1. 지형 단순 대입 및 랜덤 적용 (Shore 관련 로직 완전 제거)
            if (null != groundImages && null != _targetTheme.tileLayout)
            {
                int _len = Mathf.Min(groundImages.Length, _targetTheme.tileLayout.Length);
                for (int _i = 0; _i < _len; _i++)
                {
                    if (null == groundImages[_i])
                        continue;

                    TileType _currentType = _targetTheme.tileLayout[_i];

                    if (TileType.Water == _currentType)
                    {
                        if (null != _targetTheme.waterSprite)
                            groundImages[_i].sprite = _targetTheme.waterSprite;
                    }
                    else
                    {
                        if (null != _targetTheme.plainGroundSprites && 0 < _targetTheme.plainGroundSprites.Length)
                        {
                            int _rndIdx = UnityEngine.Random.Range(0, _targetTheme.plainGroundSprites.Length);
                            groundImages[_i].sprite = _targetTheme.plainGroundSprites[_rndIdx];
                        }
                    }
                }
            }

            // 2. 땅(Ground) 타일 위치들 추적 수집
            int _groundTileCount = 0;
            if (null != groundImages && null != _targetTheme.tileLayout)
            {
                int _len = Mathf.Min(groundImages.Length, _targetTheme.tileLayout.Length);
                
                if (null == groundTileIndices || groundTileIndices.Length < _len)
                    groundTileIndices = new int[_len * 2];

                for (int _i = 0; _i < _len; _i++)
                {
                    if (null != groundImages[_i] && TileType.Ground == _targetTheme.tileLayout[_i])
                    {
                        groundTileIndices[_groundTileCount] = _i;
                        _groundTileCount++;
                    }
                }
            }

            // 3. 나무 중복 없이 모든 종류 골고루 섞어 땅(Ground) 위에만 스냅 배치
            // 초기 셋업 시에는 기본 1레벨(SubRegion 1) 기준으로 임시 셔플 배치
            SwapTreesForSubRegion(1);
        }

        /// <summary>
        /// 서브지역 레벨에 맞춘 나무 식생 구성으로 스프라이트들을 실시간 강제 셔플 재배정합니다.
        /// </summary>
        private void SwapTreesForSubRegion(int _subRegionLevel)
        {
            if (null == themeConfig || null == themeConfig.MapThemes || null == treeVisuals)
                return;

            MapThemeData _targetTheme = default;
            bool _found = false;
            int _targetTypeIndex = (int)mapEnvironmentInfo.mapType;

            for (int _i = 0; _i < themeConfig.MapThemes.Length; _i++)
            {
                if ((int)themeConfig.MapThemes[_i].mapType == _targetTypeIndex)
                {
                    _targetTheme = themeConfig.MapThemes[_i];
                    _found = true;
                    break;
                }
            }

            if (false == _found || null == _targetTheme.subRegionTreePools)
                return;

            SubRegionTreeConfig _subConfig = default;
            bool _configFound = false;
            int _targetSubIndex = _subRegionLevel - 1;

            for (int _i = 0; _i < _targetTheme.subRegionTreePools.Length; _i++)
            {
                if (_targetTheme.subRegionTreePools[_i].subRegionIndex == _targetSubIndex)
                {
                    _subConfig = _targetTheme.subRegionTreePools[_i];
                    _configFound = true;
                    break;
                }
            }

            if (false == _configFound || null == _subConfig.treeSets || 0 == _subConfig.treeSets.Length)
                return;

            int _themeTreeCount = _subConfig.treeSets.Length;
            int _visualCount = treeVisuals.Length;

            // 3-1. 나무 종류 셔플 (shufflePool 앞 영역 사용: 0 ~ _themeTreeCount - 1)
            // 겹치지 않는 스폰 타일 선정을 위해 땅 타일 인덱스 개수도 함께 셔플하므로 셔플풀 크기 넉넉히 확보
            int _groundTileCount = 0;
            for (int _i = 0; _i < groundTileIndices.Length; _i++)
                if (0 != groundTileIndices[_i])
                    _groundTileCount++;

            if (null == shufflePool || shufflePool.Length < _themeTreeCount + _groundTileCount)
                shufflePool = new int[(_themeTreeCount + _groundTileCount) * 2];

            for (int _i = 0; _i < _themeTreeCount; _i++)
                shufflePool[_i] = _i;

            for (int _i = _themeTreeCount - 1; _i > 0; _i--)
            {
                int _j = UnityEngine.Random.Range(0, _i + 1);
                int _temp = shufflePool[_i];
                shufflePool[_i] = shufflePool[_j];
                shufflePool[_j] = _temp;
            }

            // 3-2. 땅 타일 위치 셔플 (shufflePool 뒷 영역 사용: _themeTreeCount ~ _themeTreeCount + _groundTileCount - 1)
            int _groundOffset = _themeTreeCount;
            for (int _i = 0; _i < _groundTileCount; _i++)
                shufflePool[_groundOffset + _i] = groundTileIndices[_i];

            for (int _i = _groundTileCount - 1; _i > 0; _i--)
            {
                int _j = UnityEngine.Random.Range(0, _i + 1);
                int _temp = shufflePool[_groundOffset + _i];
                shufflePool[_groundOffset + _i] = shufflePool[_groundOffset + _j];
                shufflePool[_groundOffset + _j] = _temp;
            }

            for (int _i = 0; _i < _visualCount; _i++)
            {
                int _themeIdx = shufflePool[_i % _themeTreeCount];
                int _targetTileIdx = shufflePool[_groundOffset + (_i % _groundTileCount)];

                // 잎과 기둥 이미지 교체
                if (null != treeVisuals[_i].leafImage && null != _subConfig.treeSets[_themeIdx].leafSprite)
                    treeVisuals[_i].leafImage.sprite = _subConfig.treeSets[_themeIdx].leafSprite;

                if (null != treeVisuals[_i].trunkImage && null != _subConfig.treeSets[_themeIdx].trunkSprite)
                    treeVisuals[_i].trunkImage.sprite = _subConfig.treeSets[_themeIdx].trunkSprite;

                // 나무의 부모 컨테이너(앵커)를 선택한 땅 타일의 한가운데로 자동 매핑(Snap)
                Transform _treeRoot = treeVisuals[_i].leafImage.transform.parent;
                RectTransform _treeRect = _treeRoot.GetComponent<RectTransform>();
                RectTransform _tileRect = groundImages[_targetTileIdx].GetComponent<RectTransform>();
                if (null != _treeRect && null != _tileRect)
                    _treeRect.anchoredPosition = _tileRect.anchoredPosition;
            }

            // 4. 데코(Deco) 오브젝트들도 겹치지 않게 땅(Ground) 위에 스냅 배치
            if (null != decoImages && null != themeConfig && null != themeConfig.MapThemes && 0 < _groundTileCount)
            {
                MapThemeData _activeTheme = default;
                bool _themeOk = false;
                for (int _i = 0; _i < themeConfig.MapThemes.Length; _i++)
                {
                    if ((int)themeConfig.MapThemes[_i].mapType == _targetTypeIndex)
                    {
                        _activeTheme = themeConfig.MapThemes[_i];
                        _themeOk = true;
                        break;
                    }
                }

                if (true == _themeOk && null != _activeTheme.decoSprites && 0 < _activeTheme.decoSprites.Length)
                {
                    int _themeDecoCount = _activeTheme.decoSprites.Length;
                    int _visualDecoCount = decoImages.Length;

                    // 데코가 겹치지 않고 무작위로 땅 위에 흩어지도록 셔플풀 재활용
                    if (null == shufflePool || shufflePool.Length < _groundTileCount)
                        shufflePool = new int[_groundTileCount * 2];

                    for (int _i = 0; _i < _groundTileCount; _i++)
                        shufflePool[_i] = groundTileIndices[_i];

                    for (int _i = _groundTileCount - 1; _i > 0; _i--)
                    {
                        int _j = UnityEngine.Random.Range(0, _i + 1);
                        int _temp = shufflePool[_i];
                        shufflePool[_i] = shufflePool[_j];
                        shufflePool[_j] = _temp;
                    }

                    for (int _i = 0; _i < _visualDecoCount; _i++)
                    {
                        int _rndDecoIdx = UnityEngine.Random.Range(0, _themeDecoCount);
                        int _targetTileIdx = shufflePool[_i % _groundTileCount];

                        if (null != decoImages[_i] && null != _activeTheme.decoSprites[_rndDecoIdx])
                            decoImages[_i].sprite = _activeTheme.decoSprites[_rndDecoIdx];

                        // 데코의 중심 좌표를 선택된 땅 타일의 한가운데로 자동 스냅
                        RectTransform _decoRect = decoImages[_i].GetComponent<RectTransform>();
                        RectTransform _tileRect = groundImages[_targetTileIdx].GetComponent<RectTransform>();
                        if (null != _decoRect && null != _tileRect)
                            _decoRect.anchoredPosition = _tileRect.anchoredPosition;
                    }
                }
            }
        }

        private void CaptureOriginalColors()
        {
            if (null != groundImages)
            {
                groundOriginalColors = new Color[groundImages.Length];
                for (int _i = 0; _i < groundImages.Length; _i++)
                    if (null != groundImages[_i])
                        groundOriginalColors[_i] = groundImages[_i].color;
            }

            if (null != treeVisuals)
            {
                treeOriginalColors = new MapTreeColor[treeVisuals.Length];
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    treeOriginalColors[_i] = new MapTreeColor();
                    if (null != treeVisuals[_i].leafImage)
                        treeOriginalColors[_i].leafColor = treeVisuals[_i].leafImage.color;

                    if (null != treeVisuals[_i].trunkImage)
                        treeOriginalColors[_i].trunkColor = treeVisuals[_i].trunkImage.color;
                }
            }

            if (null != decoImages)
            {
                decoOriginalColors = new Color[decoImages.Length];
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoOriginalColors[_i] = decoImages[_i].color;
            }
        }

        private void ResetObjectsVisibility()
        {
            if (null != decoImages)
            {
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoImages[_i].gameObject.SetActive(_i < currentVisibleCount);
            }
        }

        private void PlayStartAnimationInstant()
        {
            if (null == motionPlayer)
                return;

            if (null != groundImages && null != groundTags)
            {
                int _count = Mathf.Min(groundImages.Length, groundTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != groundImages[_i])
                        motionPlayer.Play(groundTags[_i], bReset: true, _skip: true);
            }

            if (null != treeVisuals && null != treeTags)
            {
                int _count = Mathf.Min(treeVisuals.Length, treeTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != treeVisuals[_i].leafImage || null != treeVisuals[_i].trunkImage)
                        motionPlayer.Play(treeTags[_i], bReset: true, _skip: true);
            }

            if (null != decoImages && null != decoTags)
            {
                int _count = Mathf.Min(decoImages.Length, decoTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != decoImages[_i])
                        motionPlayer.Play(decoTags[_i], bReset: true, _skip: true);
            }
        }

        private void PlayEndTreeAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == treeVisuals || 0 == treeVisuals.Length)
            {
                PlayEndGroundAnimation(_onComplete, _isSkip);
                return;
            }

            const float _delay = 0.05f;
            int _loopCount = Mathf.Min(treeVisuals.Length, treeTags.Length);

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                motionPlayer.PlayBackward(treeTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void PlayEndDecoAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == decoImages || 0 == decoImages.Length)
            {
                PlayEndTreeAnimation(_onComplete, _isSkip);
                return;
            }

            const float _delay = 0.05f;
            int _loopCount = Mathf.Min(decoImages.Length, decoTags.Length);

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == decoImages[_i])
                    continue;

                motionPlayer.PlayBackward(decoTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void PlayEndGroundAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == groundImages || 0 == groundImages.Length || null == groundTags)
            {
                _onComplete?.Invoke();
                return;
            }

            int _loopCount = Mathf.Min(groundImages.Length, groundTags.Length);
            int _finalIdx = 0;
            const float _delay = 0.05f;

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.PlayBackward(groundTags[_i], bReset: true, _onComplete: _onComplete, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
                else
                    motionPlayer.PlayBackward(groundTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void ApplyVisualState()
        {
            float _focusFactor = (true == isFocused) ? 1.0f : dimFactor;

            if (null != groundImages && null != groundOriginalColors)
                for (int _i = 0; _i < groundImages.Length; _i++)
                    if (null != groundImages[_i])
                        groundImages[_i].color = new Color(groundOriginalColors[_i].r * _focusFactor, groundOriginalColors[_i].g * _focusFactor, groundOriginalColors[_i].b * _focusFactor, groundOriginalColors[_i].a * uiAlpha);

            if (null != treeVisuals && null != treeOriginalColors)
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    if (null != treeVisuals[_i].leafImage)
                    {
                        Color _origLeaf = treeOriginalColors[_i].leafColor;
                        if (0f == _origLeaf.r && 0f == _origLeaf.g && 0f == _origLeaf.b)
                            _origLeaf = Color.white;

                        treeVisuals[_i].leafImage.color = new Color(_origLeaf.r * _focusFactor, _origLeaf.g * _focusFactor, _origLeaf.b * _focusFactor, _origLeaf.a * uiAlpha);
                    }

                    if (null != treeVisuals[_i].trunkImage)
                    {
                        Color _origTrunk = treeOriginalColors[_i].trunkColor;
                        if (0f == _origTrunk.r && 0f == _origTrunk.g && 0f == _origTrunk.b)
                            _origTrunk = Color.white;

                        treeVisuals[_i].trunkImage.color = new Color(_origTrunk.r * _focusFactor, _origTrunk.g * _focusFactor, _origTrunk.b * _focusFactor, _origTrunk.a * uiAlpha);
                    }
                }

            if (null != decoImages && null != decoOriginalColors)
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoImages[_i].color = new Color(decoOriginalColors[_i].r * _focusFactor, decoOriginalColors[_i].g * _focusFactor, decoOriginalColors[_i].b * _focusFactor, decoOriginalColors[_i].a * uiAlpha);

            if (null != mapNameText)
            {
                Color _textColor = mapNameText.color;
                _textColor.a = uiAlpha;
                mapNameText.color = _textColor;
            }
        }
    }
}
