using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class UI_TentAbilityComponent : MonoBehaviour
{
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 3f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;



    private ISkillSystemProvider skillSystemProvider;
    private Canvas rootCanvas;
    private bool isDragging;
    private Vector2 previousMousePosition;
    private float currentZoom = DefaultZoom;
    private float targetZoom = DefaultZoom;




    private readonly Dictionary<SkillType, AbilityNodeDefinitionJson> nodeDefinitionMap = new Dictionary<SkillType, AbilityNodeDefinitionJson>();
    private readonly List<SkillType> nodeBuildOrder = new List<SkillType>();
    private readonly Dictionary<string, Sprite> pictureSpriteMap = new Dictionary<string, Sprite>();
    private readonly List<AbilityNode> spawnedNodes = new List<AbilityNode>();
    private bool hasBuiltTestNodes;



    [Header("UI References")]
    [SerializeField] private RectTransform abilityBackground;
    [SerializeField] private RectTransform moveTarget;

    [Header("Ability Node Setup")]
    [SerializeField] private AbilityNode abilityNodePrefab;
    [SerializeField] private TextAsset abilityNodeJson;
    private float gridCellSize = 32f;
    [SerializeField] private List<AbilityPictureBinding> pictureBindings = new List<AbilityPictureBinding>();

    // 추후 특성 UI 구현에 사용할 의존성을 초기화한다.
    public void Initialize(ISkillSystemProvider _skillSystemProvider)
    {
        skillSystemProvider = _skillSystemProvider;
        rootCanvas = GetComponentInParent<Canvas>();
        CachePictureBindings();
        LoadNodeDefinitions();
        Close();
    }

    // 인스펙터에서 연결한 그림 키와 스프라이트 참조를 캐시한다.
    private void CachePictureBindings()
    {
        pictureSpriteMap.Clear();

        for (int i = 0; i < pictureBindings.Count; i++)
        {
            AbilityPictureBinding binding = pictureBindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.pictureKey) || binding.sprite == null)
                continue;

            pictureSpriteMap[binding.pictureKey] = binding.sprite;
        }
    }

    // 인스펙터에서 연결한 JSON 텍스트를 읽어 SkillType 기준 노드 정의 맵을 만든다.
    private void LoadNodeDefinitions()
    {
        nodeDefinitionMap.Clear();
        nodeBuildOrder.Clear();

        if (abilityNodeJson == null || string.IsNullOrWhiteSpace(abilityNodeJson.text))
            return;

        AbilityNodeDatabaseJson databaseJson = JsonUtility.FromJson<AbilityNodeDatabaseJson>(abilityNodeJson.text);
        if (databaseJson == null || databaseJson.nodes == null)
            return;

        for (int i = 0; i < databaseJson.nodes.Length; i++)
        {
            AbilityNodeDefinitionJson nodeDefinition = databaseJson.nodes[i];
            if (nodeDefinition == null)
                continue;

            if (Enum.TryParse(nodeDefinition.skillType, true, out SkillType parsedSkillType) == false)
                continue;

            nodeDefinitionMap[parsedSkillType] = nodeDefinition;
            nodeBuildOrder.Add(parsedSkillType);
        }
    }



    // 능력 버튼을 눌렀을 때 호출될 특성 UI 열기 진입점이다.
    public void Open()
    {
        if (abilityBackground == null)
            return;

        abilityBackground.gameObject.SetActive(true);
        BuildNodesIfNeeded();
        ResetView();
    }

    // 테스트용으로 JSON에 정의된 노드 중 지정한 SkillType 목록을 한 번만 생성한다.
    private void BuildNodesIfNeeded()
    {
        if (hasBuiltTestNodes || moveTarget == null || abilityNodePrefab == null)
            return;

        for (int i = 0; i < nodeBuildOrder.Count; i++)
        {
            CreateNode(nodeBuildOrder[i]);
        }

        hasBuiltTestNodes = true;
    }


    // 테스트용 텍스처의 위치와 확대 값을 초기화한다.
    private void ResetView()
    {
        if (moveTarget == null)
            return;

        isDragging = false;
        currentZoom = DefaultZoom;
        targetZoom = DefaultZoom;
        moveTarget.anchoredPosition = Vector2.zero;
        moveTarget.localScale = Vector3.one * currentZoom;
    }

    // 능력 UI를 닫고 드래그 상태를 초기화한다.
    public void Close()
    {
        isDragging = false;

        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);
    }

    // 능력 UI가 열려 있는 동안 드래그 이동과 줌을 처리한다.
    public void Tick()
    {
        if (abilityBackground == null || abilityBackground.gameObject.activeSelf == false || moveTarget == null)
            return;

        HandlePan();
        HandleZoom();
        UpdateZoomAnimation();
    }

    // 좌, 우, 휠 클릭 드래그로 테스트용 텍스처를 이동시킨다.
    private void HandlePan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool canDrag =
            mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed;

        Vector2 currentMousePosition = mouse.position.ReadValue();

        if (canDrag == false)
        {
            isDragging = false;
            return;
        }

        if (isDragging == false)
        {
            isDragging = true;
            previousMousePosition = currentMousePosition;
            return;
        }

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;

        float scaleFactor = 1f;
        if (rootCanvas != null)
            scaleFactor = Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f);

        moveTarget.anchoredPosition += delta / scaleFactor;
    }

    // 마우스 휠로 테스트용 텍스처를 확대 및 축소한다.
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
            return;

        targetZoom += Mathf.Sign(scrollY) * ZoomStep;
        targetZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
    }

    // 목표 줌 값을 향해 현재 줌을 빠르게 보간한다.
    private void UpdateZoomAnimation()
    {
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-ZoomFollowSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
            currentZoom = targetZoom;

        moveTarget.localScale = Vector3.one * currentZoom;
    }







    // 생성관련

    // SkillType 하나를 기준으로 JSON 정의를 조회하고 노드 프리팹을 생성한다.
    private AbilityNode CreateNode(SkillType _skillType)
    {
        if (nodeDefinitionMap.TryGetValue(_skillType, out AbilityNodeDefinitionJson nodeDefinition) == false)
            return null;

        AbilityNode node = Instantiate(abilityNodePrefab, moveTarget);
        node.gameObject.name = $"AbilityNode_{_skillType}";
        node.ApplyDefinition(nodeDefinition, _skillType, ResolvePicture(nodeDefinition.pictureKey), gridCellSize);
        spawnedNodes.Add(node);

        return node;
    }

    /// 그림 키를 기반으로 인스펙터에 연결된 스프라이트를 찾아 반환한다.
    private Sprite ResolvePicture(string _pictureKey)
    {
        if (string.IsNullOrWhiteSpace(_pictureKey))
            return null;

        if (pictureSpriteMap.TryGetValue(_pictureKey, out Sprite sprite))
            return sprite;

        return null;
    }
}
