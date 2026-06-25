using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MapParticleMapping
{
    public MapType mapType;
    public ParticleSystem particlePrefab;
}

public class EnvironmentParticleSystem : MonoBehaviour
{
    // 외부 노출 설정
    [Header("Map Particle Pooling")]
    public List<MapParticleMapping> mapParticleMappings;

    [Header("Dynamic Auto Generation")]
    public int autoGenerateLayerCount = 3;
    public float autoWrapPadding = 2.0f;
    public Vector2 minParallaxFactor = new Vector2(0.2f, 0.2f);
    public Vector2 maxParallaxFactor = new Vector2(0.8f, 0.8f);

    [Tooltip("가장 먼 층(0번)의 파티클 크기 배율")]
    public float minParticleSizeMultiplier = 0.5f;
    [Tooltip("가장 가까운 층(마지막)의 파티클 크기 배율")]
    public float maxParticleSizeMultiplier = 1.2f;

    [Header("HDR Scaling")]
    [Tooltip("가장 먼 층(0번)의 HDR 강도")]
    public float minHDRIntensity = 1.0f;
    [Tooltip("가장 가까운 층(마지막)의 HDR 강도")]
    public float maxHDRIntensity = 3.0f;

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

    public void Initialize()
    {
        SetupCamera();

        // 예상 최대 용량 기반 List 초기화 (단편화 방지 최적화)
        int expectedCapacity = mapParticleMappings != null ? mapParticleMappings.Count * autoGenerateLayerCount : 10;
        layers = new List<ParallaxLayerSetting>(expectedCapacity);
        
        propertyBlock = new MaterialPropertyBlock();
    }

    public void SetupCamera()
    {
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
    }

    public void ChangeMap(MapType _targetMapType)
    {
        if (currentMapType == _targetMapType) return;
        SetupCamera();
        
        bool isAlreadyPooled = false;

        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayerSetting layer = layers[i];
            if (layer.targetParticleSystem == null) continue;

            if (layer.mapType != _targetMapType)
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

        if (_targetMapType == MapType.None) return;
        if (isAlreadyPooled) return;

        ParticleSystem foundPrefab = null;
        for (int i = 0; i < mapParticleMappings.Count; i++)
        {
            if (mapParticleMappings[i].mapType == _targetMapType)
            {
                foundPrefab = mapParticleMappings[i].particlePrefab;
                break;
            }
        }

        if (foundPrefab == null)
        {
            Debug.LogWarning($"[EnvironmentParticleSystem] MapType '{_targetMapType}'에 매핑된 프리팹이 없습니다.");
            return;
        }

        for (int i = 0; i < autoGenerateLayerCount; i++)
        {
            ParticleSystem instance = Instantiate(foundPrefab, transform);
            instance.name = $"{_targetMapType}_Layer_{i}";
            instance.transform.localPosition = Vector3.zero;

            float t = autoGenerateLayerCount > 1 ? (float)i / (autoGenerateLayerCount - 1) : 1f;
            Vector2 factor = Vector2.Lerp(minParallaxFactor, maxParallaxFactor, t);
            float sizeMult = Mathf.Lerp(minParticleSizeMultiplier, maxParticleSizeMultiplier, t);
            float hdrInt = Mathf.Lerp(minHDRIntensity, maxHDRIntensity, t);

            var renderer = instance.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(HdrIntensityPropertyId, hdrInt);
                renderer.SetPropertyBlock(propertyBlock);
            }

            var mainModule = instance.main;
            mainModule.startSizeMultiplier *= sizeMult;

            if (mainModule.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
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
        if (cameraTransform == null) return;

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

        Vector2 currentWrapBounds = new Vector2(camWidth + autoWrapPadding, camHeight + autoWrapPadding);

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
        Vector3 currentWrapBounds = new Vector3(camWidth + autoWrapPadding, camHeight + autoWrapPadding, 0f);

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