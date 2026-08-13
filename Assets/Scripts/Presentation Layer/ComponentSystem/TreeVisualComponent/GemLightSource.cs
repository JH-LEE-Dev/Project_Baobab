using UnityEngine;

/// <summary>
/// 보석 나무 셰이더(Custom/Custom-Sprite-Default-Tree-Gem)가 광원으로 삼을 월드 위치를
/// 전역 셰이더 값으로 밀어넣는다.
///
/// 유니티 라이트나 프로젝트의 조명 시스템과는 무관하다. 이 프로젝트는 실제 조명을 쓰지 않으며,
/// 여기서 넘기는 위치는 보석 셰이더가 면(facet)의 밝기를 계산할 때만 쓰는 가상 광원이다.
/// 캐릭터에 붙이면 캐릭터가 나무 주위를 돌 때 빛나는 면도 따라 돈다.
/// </summary>
[DisallowMultipleComponent]
public class GemLightSource : MonoBehaviour
{
    private static readonly int GemLightWorldPosId = Shader.PropertyToID("_GemLightWorldPos");

    [Tooltip("광원 위치를 오브젝트 피봇에서 얼마나 띄울지. 보통 캐릭터 몸통 높이 정도가 자연스럽다.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.5f);

    private Transform cachedTransform;

    private void Awake()
    {
        cachedTransform = transform;
    }

    private void LateUpdate()
    {
        Vector2 lightPos = (Vector2)cachedTransform.position + offset;

        // w = 1은 "이 위치가 유효하다"는 플래그다. 셰이더는 w가 0이면 시간 기반 회전 광원으로 되돌아간다.
        Shader.SetGlobalVector(GemLightWorldPosId, new Vector4(lightPos.x, lightPos.y, 0f, 1f));
    }

    private void OnDisable()
    {
        // 캐릭터가 사라진 뒤에도 마지막 위치가 광원으로 굳어버리지 않도록 플래그를 내린다.
        // 셰이더는 곧바로 시간 기반 회전 광원으로 되돌아간다.
        Shader.SetGlobalVector(GemLightWorldPosId, Vector4.zero);
    }
}
