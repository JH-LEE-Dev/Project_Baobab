using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PresentationLayer.UISystem
{
    /// <summary>
    /// TextMeshPro 문자에 글자별 애니메이션 및 색상을 적용하는 컴포넌트입니다.
    /// [GEMINI.md] 컨벤션 및 최적화 원칙을 준수합니다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TMPAnimation : MonoBehaviour
    {
        public enum AnimType
        {
            None,
            Wave,
            Jump,
            Earthquake
        }

        private struct CharState
        {
            public AnimType animType;
            public Color32 color;
        }

        // //외부 의존성
        [SerializeField] private TMP_Text tmpText;

        // //내부 의존성
        [Header("Animation Settings")]
        [SerializeField] private float waveSpeed = 2.0f;
        [SerializeField] private float waveAmplitude = 10.0f;
        [SerializeField] private float jumpSpeed = 5.0f;
        [SerializeField] private float jumpAmplitude = 15.0f;
        [SerializeField] private float earthquakeIntensity = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool isDebug = false;

        private List<CharState> charStates = new List<CharState>(128);
        private System.Text.StringBuilder cleanTextBuilder = new System.Text.StringBuilder(128);
        private Stack<AnimType> animStack = new Stack<AnimType>();
        private Stack<Color32> colorStack = new Stack<Color32>();
        
        private string originalText;
        private bool isInitialized = false;
        private TMP_MeshInfo[] cachedMeshInfo;

        private static readonly Dictionary<string, Color32> colorMap = new Dictionary<string, Color32>(8)
        {
            { "Red", Color.red },
            { "Blue", Color.blue },
            { "Green", Color.green },
            { "Yellow", Color.yellow },
            { "White", Color.white }
        };

        // //퍼블릭 초기화 및 제어 메서드
        
        /// <summary>
        /// 컴포넌트를 초기화하고 텍스트 파싱을 시작합니다.
        /// </summary>
        public void Initialize(string _text)
        {
            if (tmpText == null)
            {
                tmpText = GetComponent<TMP_Text>();
            }

            originalText = _text;
            ParseText(_text);
            isInitialized = true;

            if (isDebug)
            {
                Debug.Log($"[TMPAnimation] Initialized: {tmpText.text}");
            }
        }

        /// <summary>
        /// 외부(JSON 등)에서 새로운 텍스트를 설정할 때 사용합니다.
        /// </summary>
        public void SetText(string _text)
        {
            Initialize(_text);
        }

        private void ParseText(string _input)
        {
            charStates.Clear();
            cleanTextBuilder.Clear();
            animStack.Clear();
            colorStack.Clear();
            
            AnimType _currentAnim = AnimType.None;
            Color32 _currentColor = Color.white;

            int _i = 0;
            while (_i < _input.Length)
            {
                if (_input[_i] == '[')
                {
                    int _endBracket = _input.IndexOf(']', _i);
                    if (_endBracket != -1)
                    {
                        string _tag = _input.Substring(_i + 1, _endBracket - _i - 1);
                        if (HandleTag(_tag, ref _currentAnim, ref _currentColor))
                        {
                            _i = _endBracket + 1;
                            continue;
                        }
                    }
                }

                cleanTextBuilder.Append(_input[_i]);
                charStates.Add(new CharState { animType = _currentAnim, color = _currentColor });
                _i++;
            }

            tmpText.text = cleanTextBuilder.ToString();
            tmpText.ForceMeshUpdate();
            
            // 애니메이션을 위한 원본 정점 데이터 캐싱 (GC 할당 방지)
            cachedMeshInfo = tmpText.textInfo.CopyMeshInfoVertexData();
        }

        private bool HandleTag(string _tag, ref AnimType _currAnim, ref Color32 _currColor)
        {
            if (_tag.StartsWith("Color:"))
            {
                string _colorName = _tag.Substring(6);
                colorStack.Push(_currColor);
                if (colorMap.TryGetValue(_colorName, out Color32 _newColor))
                {
                    _currColor = _newColor;
                }
                return true;
            }
            
            if (_tag == "/Color")
            {
                if (colorStack.Count > 0)
                {
                    _currColor = colorStack.Pop();
                }
                return true;
            }

            if (_tag.StartsWith("Anim:"))
            {
                string _animName = _tag.Substring(5);
                animStack.Push(_currAnim);
                if (System.Enum.TryParse(_animName, out AnimType _newAnim))
                {
                    _currAnim = _newAnim;
                }
                return true;
            }

            if (_tag == "/Anim")
            {
                if (animStack.Count > 0)
                {
                    _currAnim = animStack.Pop();
                }
                return true;
            }

            return false;
        }

        private void AnimateMesh()
        {
            if (cachedMeshInfo == null)
            {
                return;
            }

            tmpText.ForceMeshUpdate();
            TMP_TextInfo _textInfo = tmpText.textInfo;

            for (int _i = 0; _i < _textInfo.characterCount; _i++)
            {
                TMP_CharacterInfo _charInfo = _textInfo.characterInfo[_i];
                if (!_charInfo.isVisible || _i >= charStates.Count)
                {
                    continue;
                }

                int _materialIndex = _charInfo.materialReferenceIndex;
                int _vertexIndex = _charInfo.vertexIndex;

                Vector3[] _sourceVertices = cachedMeshInfo[_materialIndex].vertices;
                Vector3[] _destinationVertices = _textInfo.meshInfo[_materialIndex].vertices;
                Color32[] _destinationColors = _textInfo.meshInfo[_materialIndex].colors32;

                Vector3 _offset = Vector3.zero;
                CharState _state = charStates[_i];

                switch (_state.animType)
                {
                    case AnimType.Wave:
                        _offset.y = Mathf.Sin(Time.time * waveSpeed + _i * 0.5f) * waveAmplitude;
                        break;
                    case AnimType.Jump:
                        _offset.y = Mathf.Abs(Mathf.Sin(Time.time * jumpSpeed + _i * 0.2f)) * jumpAmplitude;
                        break;
                    case AnimType.Earthquake:
                        _offset.x = Random.Range(-earthquakeIntensity, earthquakeIntensity);
                        _offset.y = Random.Range(-earthquakeIntensity, earthquakeIntensity);
                        break;
                    default:
                        break;
                }

                for (int _j = 0; _j < 4; _j++)
                {
                    _destinationVertices[_vertexIndex + _j] = _sourceVertices[_vertexIndex + _j] + _offset;
                    _destinationColors[_vertexIndex + _j] = _state.color;
                }
            }

            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        // //유니티 이벤트 함수
        
        private void Awake()
        {
            if (tmpText == null)
            {
                tmpText = GetComponent<TMP_Text>();
            }

            if (!string.IsNullOrEmpty(tmpText.text) && !isInitialized)
            {
                Initialize(tmpText.text);
            }
        }

        private void Update()
        {
            if (!isInitialized || tmpText == null)
            {
                return;
            }

            AnimateMesh();
        }
    }
}
