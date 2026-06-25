using System.Collections.Generic;
using UnityEngine;

namespace Project_Baobab.Presentation.EnvironmentSystem
{
    /// <summary>
    /// 맵별 고유 파티클 설정 데이터를 담는 클래스입니다.
    /// 이제 레이어 개수, 속도, 크기, HDR 등 모든 세팅이 맵마다 독립적으로 관리됩니다.
    /// </summary>
    [System.Serializable]
    public class MapParticleMapping
    {
        public MapType mapType;
        public ParticleSystem particlePrefab;

        [Header("Layer Generation")]
        [Tooltip("이 맵에서 생성할 파티클 층의 개수")]
        public int autoGenerateLayerCount = 3;
        [Tooltip("화면 밖 래핑(텔레포트) 여유 공간")]
        public float autoWrapPadding = 2.0f;

        [Header("Parallax Speed")]
        public Vector2 minParallaxFactor = new Vector2(0.2f, 0.2f);
        public Vector2 maxParallaxFactor = new Vector2(0.8f, 0.8f);

        [Header("Size Scaling")]
        public float minParticleSizeMultiplier = 0.5f;
        public float maxParticleSizeMultiplier = 1.2f;

        [Header("HDR Scaling")]
        public float minHDRIntensity = 1.0f;
        public float maxHDRIntensity = 3.0f;

        [Header("Emission Scaling")]
        [Tooltip("각 층별 방출량(Rate Over Time) 수동 지정 (0번 층부터 순서대로). 비워두면 프리팹 원본 개수를 따릅니다.")]
        public List<float> customEmissionRates = new List<float>();
    }

    public class EnvironmentParticleSystem : MonoBehaviour
    {
        // 외부 노출 설정
        [Header("Map Particle Settings")]
        [Tooltip("각 맵별로 독립적인 파티클 세팅을 추가하세요.")]
        public List<MapParticleMapping> mapParticleMappings = new List<MapParticleMapping>();

        // 내부 의존성
        private struct ParallaxLayerSetting
        {
            public ParticleSystem targetParticleSystem;
            public Vector2 parallaxFactor;
            public ParticleSystem.Particle[] particlesBuffer;
            public MapType mapType;
        }

        private List<ParallaxLayerSetting> layers;
        private MapType currentMapType = MapType.None;
        private Camera mainCamera;
        private Transform cameraTransform;
        private Vector3 previousCameraPosition;
        
        private MaterialPropertyBlock propertyBlock;
        private static readonly int HdrIntensityPropertyId = Shader.PropertyToID("_HDRIntensity");

        // 현재 활성화된 맵의 캐싱 데이터 (Update 최적화용)
        private float currentWrapPadding = 2.0f;
        private bool isInitialized = false;

        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                previousCameraPosition = cameraTransform.position;
            }
            else
            {
                Debug.LogWarning("[EnvironmentParticleSystem] 메인 카메라를 찾을 수 없습니다.");
            }

            // 예상 최대 용량 기반 List 초기화 (단편화 방지 최적화)
            int expectedCapacity = mapParticleMappings != null ? mapParticleMappings.Count * 5 : 15;
            layers = new List<ParallaxLayerSetting>(expectedCapacity);
            
            propertyBlock = new MaterialPropertyBlock();
        }

        public void ChangeMap(MapType _targetMapType)
        {
            if (currentMapType == _targetMapType) return;

            bool isAlreadyPooled = false;

            for (int i = 0; i < layers.Count; i++)
            {
                ParallaxLayerSetting layer = layers[i];
                if (layer.targetParticleSystem == null) continue;

                // 타겟 맵이 아닐 경우, 또는 타겟이 타운(Town)일 경우 모든 파티클 강제 종료
                if (_targetMapType == MapType.Town || layer.mapType != _targetMapType)
                {
                    layer.targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else
                {
                    layer.targetParticleSystem.Play();
                    isAlreadyPooled = true;
                }
            }

            currentMapType = _targetMapType;

            // None이거나 Town이면 추가 파티클을 생성하지 않고 종료
            if (_targetMapType == MapType.None || _targetMapType == MapType.Town) return;

            // 타겟 맵의 매핑 데이터 찾기
            MapParticleMapping targetMapping = null;
            for (int i = 0; i < mapParticleMappings.Count; i++)
            {
                if (mapParticleMappings[i].mapType == _targetMapType)
                {
                    targetMapping = mapParticleMappings[i];
                    break;
                }
            }

            if (targetMapping == null)
            {
                Debug.LogWarning($"[EnvironmentParticleSystem] MapType '{_targetMapType}'에 매핑된 프리팹 세팅이 없습니다.");
                return;
            }

            // Update 최적화를 위해 현재 맵의 래핑 패딩값 캐싱
            currentWrapPadding = targetMapping.autoWrapPadding;

            if (isAlreadyPooled) return;

            if (targetMapping.particlePrefab == null)
            {
                Debug.LogWarning($"[EnvironmentParticleSystem] MapType '{_targetMapType}'의 파티클 프리팹이 비어있습니다.");
                return;
            }

            // 새로운 맵 세팅 기반으로 레이어 최초 1회 생성
            int layerCount = targetMapping.autoGenerateLayerCount;
            for (int i = 0; i < layerCount; i++)
            {
                ParticleSystem instance = Instantiate(targetMapping.particlePrefab, transform);
                instance.name = $"{_targetMapType}_Layer_{i}";
                instance.transform.localPosition = Vector3.zero;

                float t = layerCount > 1 ? (float)i / (layerCount - 1) : 1f;
                Vector2 factor = Vector2.Lerp(targetMapping.minParallaxFactor, targetMapping.maxParallaxFactor, t);
                float sizeMult = Mathf.Lerp(targetMapping.minParticleSizeMultiplier, targetMapping.maxParticleSizeMultiplier, t);
                float hdrInt = Mathf.Lerp(targetMapping.minHDRIntensity, targetMapping.maxHDRIntensity, t);

                var renderer = instance.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat(HdrIntensityPropertyId, hdrInt);
                    renderer.SetPropertyBlock(propertyBlock);
                }

                var mainModule = instance.main;
                mainModule.startSizeMultiplier *= sizeMult;

                var emissionModule = instance.emission;
                if (targetMapping.customEmissionRates != null && i < targetMapping.customEmissionRates.Count)
                {
                    emissionModule.rateOverTime = new ParticleSystem.MinMaxCurve(targetMapping.customEmissionRates[i]);
                }

                if (mainModule.simulationSpace != ParticleSystemSimulationSpace.Local)
                {
                    mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
                }

                // Prewarm이나 PlayOnAwake 옵션으로 인해 크기/Emission 변경 전에 미리 쏟아진 파티클들 초기화 후 재시작
                bool wasPlaying = instance.isPlaying;
                instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (wasPlaying || mainModule.playOnAwake)
                {
                    instance.Play();
                }

                ParallaxLayerSetting newLayer = new ParallaxLayerSetting()
                {
                    targetParticleSystem = instance,
                    parallaxFactor = factor,
                    particlesBuffer = new ParticleSystem.Particle[mainModule.maxParticles],
                    mapType = _targetMapType
                };
                
                layers.Add(newLayer);
            }
        }

        private float WrapCoordinate(float _value, float _boundSize)
        {
            float halfBound = _boundSize * 0.5f;
            return Mathf.Repeat(_value + halfBound, _boundSize) - halfBound;
        }

        private void Awake()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                    previousCameraPosition = cameraTransform.position;
                }
                else
                {
                    return;
                }
            }

            Vector3 currentCameraPos = cameraTransform.position;
            Vector3 cameraDelta = currentCameraPos - previousCameraPosition;
            previousCameraPosition = currentCameraPos;

            transform.position = new Vector3(currentCameraPos.x, currentCameraPos.y, transform.position.z);

            if (currentMapType == MapType.None) return;

            // 카메라 크기 및 래핑 바운드 계산을 for문 밖에서 1회만 수행하도록 최적화
            float camHeight = 0f;
            if (mainCamera.orthographic)
            {
                camHeight = mainCamera.orthographicSize * 2f;
            }
            else
            {
                float distance = Mathf.Abs(cameraTransform.position.z - transform.position.z);
                camHeight = 2.0f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            float camWidth = camHeight * mainCamera.aspect;
            
            // 캐싱된 currentWrapPadding 사용
            Vector2 currentWrapBounds = new Vector2(camWidth + currentWrapPadding, camHeight + currentWrapPadding);

            for (int i = 0; i < layers.Count; i++)
            {
                ParallaxLayerSetting layer = layers[i];

                if (layer.mapType != currentMapType) continue;
                if (layer.targetParticleSystem == null || layer.particlesBuffer == null) continue;

                int aliveParticlesCount = layer.targetParticleSystem.GetParticles(layer.particlesBuffer);
                if (aliveParticlesCount == 0) continue;

                float moveRatioX = 1f - layer.parallaxFactor.x;
                float moveRatioY = 1f - layer.parallaxFactor.y;
                Vector3 parallaxDelta = new Vector3(-cameraDelta.x * moveRatioX, -cameraDelta.y * moveRatioY, 0f);

                for (int j = 0; j < aliveParticlesCount; j++)
                {
                    Vector3 pos = layer.particlesBuffer[j].position;
                    pos += parallaxDelta;
                    pos.x = WrapCoordinate(pos.x, currentWrapBounds.x);
                    pos.y = WrapCoordinate(pos.y, currentWrapBounds.y);
                    layer.particlesBuffer[j].position = pos;
                }

                layer.targetParticleSystem.SetParticles(layer.particlesBuffer, aliveParticlesCount);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            float camHeight = 0f;
            if (mainCamera.orthographic)
            {
                camHeight = mainCamera.orthographicSize * 2f;
            }
            else
            {
                float distance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                camHeight = 2.0f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            float camWidth = camHeight * mainCamera.aspect;
            
            // 기즈모 그릴 때도 캐싱된 패딩값 사용
            Vector3 currentWrapBounds = new Vector3(camWidth + currentWrapPadding, camHeight + currentWrapPadding, 0f);

            Gizmos.color = Color.cyan;
            if (layers != null)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    ParallaxLayerSetting layer = layers[i];
                    if (layer.targetParticleSystem != null && layer.mapType == currentMapType)
                    {
                        Gizmos.DrawWireCube(layer.targetParticleSystem.transform.position, currentWrapBounds);
                    }
                }
            }
        }
    }
}