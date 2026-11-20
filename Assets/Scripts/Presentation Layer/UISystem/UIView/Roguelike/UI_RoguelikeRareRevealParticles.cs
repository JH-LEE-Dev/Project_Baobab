using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 희귀 카드 등장 연출(UI_RoguelikeRareRevealFx)의 픽셀 입자·부드러운 빛 점을 그리는 UGUI 그래픽.
/// 호출 측이 BeginQuads → Rect / Soft → Commit 순서로 채운다. Commit만 SetVerticesDirty를 부른다(호출 측 틱이 바뀔 때만).
///
/// 두 가지로 쓴다(같은 클래스, 머티리얼·텍스처만 다르다).
///  - 픽셀 입자: 머티리얼 UIPixelGlow, softTexture 없음. Rect = 정수 칸 사각형(1 캔버스 단위 = 게임 1px).
///  - 부드러운 빛 점: 머티리얼 UIGlow 가산(M_UILootGlowAdd), softTexture = 빛 점 알파 텍스처. Soft = 중심·반지름의 정사각형.
/// 사각형 배열은 미리 잡아 재사용하므로 재생 중 힙 할당이 없다. 정점 색은 호출 측이 감마 rgb + 발광 알파로 넣는다.
/// 좌표는 부모(층 루트) 기준 px, y는 위쪽이 +.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UI_RoguelikeRareRevealParticles : MaskableGraphic
{
    private const int QuadCapacity = 768;

    private struct ParticleQuad
    {
        public float x0;
        public float y0;
        public float x1;
        public float y1;
        public Color32 color;
    }

    //외부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("부드러운 빛 점 텍스처(알파만 쓴다). 비어 있으면 픽셀 입자용(텍스처 없음)이다.")]
    private Texture softTexture;

    //상태 변수
    private ParticleQuad[] quads = new ParticleQuad[QuadCapacity];
    private int quadCount;
    private int committedCount = -1;

    public override Texture mainTexture => null != softTexture ? softTexture : s_WhiteTexture;

    // 퍼블릭 초기화 및 제어 메서드

    public void BeginQuads()
    {
        quadCount = 0;
    }

    /// <summary>[_x0, _x1) x [_y0, _y1) 사각형(부모 기준 px)을 쌓는다.</summary>
    public void Rect(float _x0, float _y0, float _x1, float _y1, Color32 _color)
    {
        if (_x1 <= _x0 || _y1 <= _y0) return;

        // 꽉 차면 사각형을 버리지 않고 배열을 두 배로 늘린다
        if (quads.Length <= quadCount) System.Array.Resize(ref quads, quads.Length * 2);

        ref ParticleQuad q = ref quads[quadCount];
        q.x0 = _x0;
        q.y0 = _y0;
        q.x1 = _x1;
        q.y1 = _y1;
        q.color = _color;
        quadCount++;
    }

    /// <summary>중심 (_x, _y)·반지름 _radius의 정사각형 빛 점.</summary>
    public void Soft(float _x, float _y, float _radius, Color32 _color)
    {
        Rect(_x - _radius, _y - _radius, _x + _radius, _y + _radius, _color);
    }

    /// <summary>그린 내용을 메쉬에 반영한다. 지난번도 비어 있었고 이번에도 비어 있으면 아무것도 하지 않는다.</summary>
    public void Commit()
    {
        if (0 == quadCount && 0 == committedCount) return;

        committedCount = quadCount;
        SetVerticesDirty();
    }

    /// <summary>내용을 비우고 메쉬에서도 지운다.</summary>
    public void ClearQuads()
    {
        quadCount = 0;
        Commit();
    }

    // 일반 비즈니스 로직 및 내부 메서드

    protected override void OnPopulateMesh(VertexHelper _vh)
    {
        _vh.Clear();

        // UV는 사각형 전체(0~1). 픽셀 입자용(UIPixelGlow)은 텍스처를 읽지 않으므로 값이 무관하다.
        for (int i = 0; i < quadCount; i++)
        {
            ref ParticleQuad q = ref quads[i];
            int v = _vh.currentVertCount;
            _vh.AddVert(new Vector3(q.x0, q.y0, 0.0f), q.color, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            _vh.AddVert(new Vector3(q.x0, q.y1, 0.0f), q.color, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
            _vh.AddVert(new Vector3(q.x1, q.y1, 0.0f), q.color, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
            _vh.AddVert(new Vector3(q.x1, q.y0, 0.0f), q.color, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            _vh.AddTriangle(v, v + 1, v + 2);
            _vh.AddTriangle(v, v + 2, v + 3);
        }
    }

    // 유니티 라이프사이클 이벤트 함수

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }
}
