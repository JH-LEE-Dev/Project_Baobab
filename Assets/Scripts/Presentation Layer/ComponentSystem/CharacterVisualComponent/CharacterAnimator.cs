using UnityEngine;
using System.Collections.Generic;

public class CharacterAnimator : MonoBehaviour
{
    // 외부 의존성
    // (현재 클래스에서는 외부 의존성을 직접 수입(Inject)받지 않음)

    // 내부 의존성 (컴포넌트 및 오브젝트)
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer baseSR;
    [SerializeField] private SpriteRenderer faceSR;
    [SerializeField] private SpriteRenderer faceBlinkSR;
    [SerializeField] private SpriteRenderer onWaterBaseSR;
    [SerializeField] private SpriteRenderer onWaterFaceSR;
    [SerializeField] private SpriteRenderer onWaterFaceBlinkSR;
    [SerializeField] private SpriteRenderer shadowSR;

    [Space]
    [Header("InTown Base Animation Sprites")]
    [SerializeField] private List<Sprite> base_IdleR;
    [SerializeField] private List<Sprite> base_IdleD;
    [SerializeField] private List<Sprite> base_IdleRD;
    [SerializeField] private List<Sprite> base_IdleRU;
    [SerializeField] private List<Sprite> base_IdleU;
    [SerializeField] private List<Sprite> base_RunR;
    [SerializeField] private List<Sprite> base_RunD;
    [SerializeField] private List<Sprite> base_RunRD;
    [SerializeField] private List<Sprite> base_RunRU;
    [SerializeField] private List<Sprite> base_RunU;

    [Space]
    [Header("InDungeon Base Animation Sprites")]
    [SerializeField] private List<Sprite> InDungeon_base_IdleR;
    [SerializeField] private List<Sprite> InDungeon_base_IdleD;
    [SerializeField] private List<Sprite> InDungeon_base_IdleRD;
    [SerializeField] private List<Sprite> InDungeon_base_IdleRU;
    [SerializeField] private List<Sprite> InDungeon_base_IdleU;
    [SerializeField] private List<Sprite> InDungeon_base_RunR;
    [SerializeField] private List<Sprite> InDungeon_base_RunD;
    [SerializeField] private List<Sprite> InDungeon_base_RunRD;
    [SerializeField] private List<Sprite> InDungeon_base_RunRU;
    [SerializeField] private List<Sprite> InDungeon_base_RunU;

    [Space]
    [Header("Dead Sprites")]
    [SerializeField] private List<Sprite> deadStartSprites;
    [SerializeField] private List<Sprite> deadLoopSprites;

    [Space]
    [Header("InTown Face Animation Sprites")]
    [SerializeField] private List<Sprite> face_IdleR;
    [SerializeField] private List<Sprite> face_IdleD;
    [SerializeField] private List<Sprite> face_IdleRD;
    [SerializeField] private List<Sprite> face_RunR;
    [SerializeField] private List<Sprite> face_RunD;
    [SerializeField] private List<Sprite> face_RunRD;

    [Space]
    [Header("InDungeon Face Animation Sprites")]
    [SerializeField] private List<Sprite> inDungeon_Face_IdleR;
    [SerializeField] private List<Sprite> inDungeon_Face_IdleD;
    [SerializeField] private List<Sprite> inDungeon_Face_IdleRD;
    [SerializeField] private List<Sprite> inDungeon_Face_RunR;
    [SerializeField] private List<Sprite> inDungeon_Face_RunD;
    [SerializeField] private List<Sprite> inDungeon_Face_RunRD;

    [Space]
    [Header("InTown Face Blink Animation Sprites")]
    [SerializeField] private List<Sprite> blink_IdleR;
    [SerializeField] private List<Sprite> blink_IdleD;
    [SerializeField] private List<Sprite> blink_IdleRD;
    [SerializeField] private List<Sprite> blink_RunR;
    [SerializeField] private List<Sprite> blink_RunD;
    [SerializeField] private List<Sprite> blink_RunRD;

    [Space]
    [Header("InDungeon Face Blink Animation Sprites")]
    [SerializeField] private List<Sprite> inDungone_Blink_IdleR;
    [SerializeField] private List<Sprite> inDungone_Blink_IdleD;
    [SerializeField] private List<Sprite> inDungone_Blink_IdleRD;
    [SerializeField] private List<Sprite> inDungone_Blink_RunR;
    [SerializeField] private List<Sprite> inDungone_Blink_RunD;
    [SerializeField] private List<Sprite> inDungone_Blink_RunRD;

    [Space]
    [Header("Animation Speed")]
    [SerializeField] private float idleSample = 5;
    [SerializeField] private float runSample = 10;

    [Space]
    [Header("VFX Settings")]
    [SerializeField] private float dustOffsetDistance = 0.15f;

    // 상태 데이터
    private float frameTimer = 0f;
    private int currentFrameIndex = 0;
    private bool isDeadStartFinished = false;
    
    private bool prevIsMoving = false;
    private bool prevBInHub = true;
    private bool prevIsDead = false;
    private int prevDirIndex = -1;
    private int prevShadowDirIndex = -1;

    private Vector3 lastPosition;
    // 방향 전환 등에 의한 currentFrameIndex 리셋과 무관하게, Run Sample 속도에 맞춰 독립적으로 흐르는 발소리 타이머.
    private float footstepTimer = 0f;

    #region Public Methods

    private VFXComponent vfxComponent;
    private ITilemapDataProvider tilemapDataProvider;

    public void Initialize(VFXComponent _vfxComponent, ITilemapDataProvider _tilemapDataProvider = null)
    {
        vfxComponent = _vfxComponent;
        tilemapDataProvider = _tilemapDataProvider;

        frameTimer = 0f;
        currentFrameIndex = 0;
        isDeadStartFinished = false;
        footstepTimer = 0f;
        lastPosition = transform.position;
    }

    // Town/Dungeon 전환 시 실제 타일맵을 들고 있는 쪽(TownSystem/InDungeonSystem)이 직접 갈아끼운다.
    public void SetTilemapDataProvider(ITilemapDataProvider _tilemapDataProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;
    }

    public void UpdateAnimation(float _deltaTime, bool _isMoving, bool _bInHub, float _facingAngle, float _shadowAngle, bool _isBlinking, bool _isDead)
    {
        // 1. 방향 및 기본 상태 인덱스화
        int dirIndex = Mathf.RoundToInt(_facingAngle / 45f) % 8;

        // 그림자 방향 인덱스 결정
        float normalizedShadowAngle = _shadowAngle % 360;
        if (normalizedShadowAngle < 0) normalizedShadowAngle += 360;

        int shadowDirIndex = 0;
        if (normalizedShadowAngle <= 22.5f || normalizedShadowAngle >= 337.5f)
        {
            shadowDirIndex = 0;
        }
        else if (normalizedShadowAngle >= 157.5f && normalizedShadowAngle <= 202.5f)
        {
            shadowDirIndex = 4;
        }
        else
        {
            float lightPerspectiveAngle = _facingAngle - _shadowAngle + 90f;
            Vector2 lightViewDir = new Vector2(
                Mathf.Cos(lightPerspectiveAngle * Mathf.Deg2Rad),
                Mathf.Sin(lightPerspectiveAngle * Mathf.Deg2Rad)
            );
            float angle = Mathf.Atan2(lightViewDir.y, lightViewDir.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            shadowDirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        }

        // 2. 상태 전환 시 프레임 리셋
        // 눈 깜빡임(_isBlinking)은 몸통 걷기 스프라이트(baseSprites)와 무관하므로 리셋 조건에서 제외한다.
        // (블링크/얼굴 프레임은 currentFrameIndex를 그대로 참조해 별도로 모듈로 계산되므로 리셋이 필요 없다)
        if (_isMoving != prevIsMoving || _bInHub != prevBInHub || _isDead != prevIsDead || dirIndex != prevDirIndex)
        {
            // 캐릭터가 죽는 그 순간(살아있다가 죽은 상태로 전환되는 시점)에 재생.
            if (_isDead && !prevIsDead)
            {
                Sound.Play(SoundID.CharacterDie, transform.position);
            }

            currentFrameIndex = 0;
            frameTimer = 0f;
            isDeadStartFinished = false;

            prevIsMoving = _isMoving;
            prevBInHub = _bInHub;
            prevIsDead = _isDead;
            prevDirIndex = dirIndex;
            prevShadowDirIndex = shadowDirIndex;
        }

        // 3. 스프라이트 리스트 가져오기
        List<Sprite> baseSprites = null;
        bool baseFlipX = false;
        List<Sprite> faceSprites = null;
        bool faceFlipX = false;
        bool isFaceActive = false;
        List<Sprite> blinkSprites = null;
        bool blinkFlipX = false;

        if (_isDead)
        {
            if (!isDeadStartFinished && deadStartSprites != null && deadStartSprites.Count > 0)
            {
                baseSprites = deadStartSprites;
            }
            else
            {
                baseSprites = deadLoopSprites;
            }
            baseFlipX = false;
        }
        else
        {
            baseSprites = GetBaseSprites(_isMoving, _bInHub, dirIndex, out baseFlipX);
            faceSprites = GetFaceSprites(_isMoving, _bInHub, dirIndex, out faceFlipX, out isFaceActive);
            blinkSprites = GetBlinkSprites(_isMoving, _bInHub, dirIndex, out blinkFlipX, out _);
        }

        // 4. 애니메이션 프레임 계산
        if (baseSprites != null && baseSprites.Count > 0)
        {
            float sampleRate = 5f;
            if (_isDead)
            {
                sampleRate = !isDeadStartFinished ? 10f : 5f;
            }
            else
            {
                sampleRate = _isMoving ? runSample : idleSample;
            }
            float frameTime = sampleRate > 0f ? 1f / sampleRate : 0.2f;

            frameTimer += _deltaTime;
            if (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                if (_isDead)
                {
                    if (!isDeadStartFinished && deadStartSprites != null && deadStartSprites.Count > 0)
                    {
                        if (currentFrameIndex < baseSprites.Count - 1)
                        {
                            currentFrameIndex++;

                            // Retire_8(9번째 스프라이트): 풀밭에 쓰러지는 프레임 - BodyDrop 소리
                            // Retire_9(10번째 스프라이트): 도끼가 땅에 박히는 프레임 - AxeDrop 소리
                            if (currentFrameIndex == 8)
                            {
                                Sound.Play(SoundID.CharacterDieBodyDrop, transform.position);
                            }
                            else if (currentFrameIndex == 9)
                            {
                                Sound.Play(SoundID.CharacterDieAxeDrop, transform.position);
                            }
                        }
                        else
                        {
                            isDeadStartFinished = true;
                            currentFrameIndex = 0;
                            baseSprites = deadLoopSprites;
                        }
                    }
                    else
                    {
                        if (baseSprites != null && baseSprites.Count > 0)
                        {
                            currentFrameIndex = (currentFrameIndex + 1) % baseSprites.Count;
                        }
                    }
                }
                else
                {
                    currentFrameIndex = (currentFrameIndex + 1) % baseSprites.Count;
                }
            }

            // 인덱스 초과 방지 안전망
            if (baseSprites != null && currentFrameIndex >= baseSprites.Count)
            {
                currentFrameIndex = Mathf.Max(0, baseSprites.Count - 1);
            }

            // 5. 스프라이트 설정
            if (baseSprites != null && baseSprites.Count > 0)
            {
                Sprite currentBaseSprite = baseSprites[currentFrameIndex];
                
                if (baseSR != null)
                {
                    baseSR.sprite = currentBaseSprite;
                    Vector3 scale = baseSR.transform.localScale;
                    scale.x = baseFlipX ? -1f : 1f;
                    baseSR.transform.localScale = scale;
                }

                if (onWaterBaseSR != null)
                {
                    onWaterBaseSR.sprite = currentBaseSprite;
                    Vector3 scale = onWaterBaseSR.transform.localScale;
                    scale.x = baseFlipX ? 1f : -1f;
                    onWaterBaseSR.transform.localScale = scale;
                }
            }
        }

        // 얼굴 스프라이트 처리
        if (!_isDead && isFaceActive)
        {
            if (!_isBlinking)
            {
                // 눈을 깜빡이지 않을 때: faceSR 활성화, faceBlinkSR 비활성화
                if (faceSR != null && faceSprites != null && faceSprites.Count > 0)
                {
                    int faceFrame = currentFrameIndex % faceSprites.Count;
                    faceSR.enabled = true;
                    faceSR.sprite = faceSprites[faceFrame];
                    Vector3 scale = faceSR.transform.localScale;
                    scale.x = faceFlipX ? -1f : 1f;
                    faceSR.transform.localScale = scale;
                }
                if (onWaterFaceSR != null && faceSprites != null && faceSprites.Count > 0)
                {
                    int faceFrame = currentFrameIndex % faceSprites.Count;
                    onWaterFaceSR.enabled = true;
                    onWaterFaceSR.sprite = faceSprites[faceFrame];
                    Vector3 scale = onWaterFaceSR.transform.localScale;
                    scale.x = faceFlipX ? 1f : -1f;
                    onWaterFaceSR.transform.localScale = scale;
                }
                if (faceBlinkSR != null) faceBlinkSR.enabled = false;
                if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.enabled = false;
            }
            else
            {
                // 눈을 깜빡일 때: faceBlinkSR 활성화, faceSR 비활성화
                if (faceBlinkSR != null && blinkSprites != null && blinkSprites.Count > 0)
                {
                    int blinkFrame = currentFrameIndex % blinkSprites.Count;
                    faceBlinkSR.enabled = true;
                    faceBlinkSR.sprite = blinkSprites[blinkFrame];
                    Vector3 scale = faceBlinkSR.transform.localScale;
                    scale.x = blinkFlipX ? -1f : 1f;
                    faceBlinkSR.transform.localScale = scale;
                }
                if (onWaterFaceBlinkSR != null && blinkSprites != null && blinkSprites.Count > 0)
                {
                    int blinkFrame = currentFrameIndex % blinkSprites.Count;
                    onWaterFaceBlinkSR.enabled = true;
                    onWaterFaceBlinkSR.sprite = blinkSprites[blinkFrame];
                    Vector3 scale = onWaterFaceBlinkSR.transform.localScale;
                    scale.x = blinkFlipX ? 1f : -1f;
                    onWaterFaceBlinkSR.transform.localScale = scale;
                }
                if (faceSR != null) faceSR.enabled = false;
                if (onWaterFaceSR != null) onWaterFaceSR.enabled = false;
            }
        }
        else
        {
            if (faceSR != null) faceSR.enabled = false;
            if (onWaterFaceSR != null) onWaterFaceSR.enabled = false;
            if (faceBlinkSR != null) faceBlinkSR.enabled = false;
            if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.enabled = false;
        }

        // 그림자 스프라이트 처리
        if (!_isDead)
        {
            List<Sprite> shadowSprites = GetBaseSprites(_isMoving, _bInHub, shadowDirIndex, out bool shadowFlipX);
            if (shadowSprites != null && shadowSprites.Count > 0)
            {
                int shadowFrameIndex = currentFrameIndex % shadowSprites.Count;
                Sprite currentShadowSprite = shadowSprites[shadowFrameIndex];

                if (shadowSR != null)
                {
                    shadowSR.sprite = currentShadowSprite;
                    Vector3 scale = shadowSR.transform.localScale;
                    scale.x = shadowFlipX ? -1f : 1f;
                    shadowSR.transform.localScale = scale;
                }
            }
        }

        // 발소리: Run Sample 속도를 그대로 따라가되, 방향 전환 등으로 currentFrameIndex가 리셋되어도
        // 끊기지 않도록 별도 타이머로 독립 진행시킨다 (4프레임 사이클 기준 절반 = 접지 2회).
        if (_isMoving && !_isDead && baseSprites != null && baseSprites.Count > 0)
        {
            float frameDuration = runSample > 0f ? 1f / runSample : 0.1f;
            float contactInterval = frameDuration * (baseSprites.Count / 2f);

            footstepTimer += _deltaTime;
            if (footstepTimer >= contactInterval)
            {
                footstepTimer -= contactInterval;
                PlayFootstepEffects(_facingAngle);
            }
        }
        else
        {
            footstepTimer = 0f;
        }

        lastPosition = transform.position;
    }

    // 발이 바닥에 닿는 애니메이션 프레임에 맞춰 먼지 VFX와 발소리를 함께 재생한다.
    private void PlayFootstepEffects(float _facingAngle)
    {
        if (vfxComponent != null)
        {
            Vector3 moveDelta = transform.position - lastPosition;
            Vector3 moveDir;
            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                moveDir = moveDelta.normalized;
            }
            else
            {
                float angleRad = _facingAngle * Mathf.Deg2Rad;
                moveDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
            }
            Vector3 spawnPosition = lastPosition - moveDir * dustOffsetDistance;

            ParticleSystem effect = vfxComponent.Play("Dust", spawnPosition, Quaternion.identity);
            if (effect != null && baseSR != null)
            {
                vfxComponent.SetSortingSettings(effect, baseSR.sortingLayerName, baseSR.sortingOrder);
            }
        }

        bool isGrass = tilemapDataProvider != null && tilemapDataProvider.IsGrassTile(tilemapDataProvider.WorldToCell(transform.position));
        Sound.Play(isGrass ? SoundID.GrassFootstep : SoundID.GroundFootstep, transform.position);
    }

    #endregion

    #region Private Methods

    private List<Sprite> GetBaseSprites(bool _isMoving, bool _bInHub, int _dirIndex, out bool _flipX)
    {
        _flipX = false;
        if (_bInHub)
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return base_RunR;
                    case 1: return base_RunRU;
                    case 2: return base_RunU;
                    case 3: _flipX = true; return base_RunRU;
                    case 4: _flipX = true; return base_RunR;
                    case 5: _flipX = true; return base_RunRD;
                    case 6: return base_RunD;
                    case 7: return base_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return base_IdleR;
                    case 1: return base_IdleRU;
                    case 2: return base_IdleU;
                    case 3: _flipX = true; return base_IdleRU;
                    case 4: _flipX = true; return base_IdleR;
                    case 5: _flipX = true; return base_IdleRD;
                    case 6: return base_IdleD;
                    case 7: return base_IdleRD;
                }
            }
        }
        else
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return InDungeon_base_RunR;
                    case 1: return InDungeon_base_RunRU;
                    case 2: return InDungeon_base_RunU;
                    case 3: _flipX = true; return InDungeon_base_RunRU;
                    case 4: _flipX = true; return InDungeon_base_RunR;
                    case 5: _flipX = true; return InDungeon_base_RunRD;
                    case 6: return InDungeon_base_RunD;
                    case 7: return InDungeon_base_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return InDungeon_base_IdleR;
                    case 1: return InDungeon_base_IdleRU;
                    case 2: return InDungeon_base_IdleU;
                    case 3: _flipX = true; return InDungeon_base_IdleRU;
                    case 4: _flipX = true; return InDungeon_base_IdleR;
                    case 5: _flipX = true; return InDungeon_base_IdleRD;
                    case 6: return InDungeon_base_IdleD;
                    case 7: return InDungeon_base_IdleRD;
                }
            }
        }
        return null;
    }

    private List<Sprite> GetFaceSprites(bool _isMoving, bool _bInHub, int _dirIndex, out bool _flipX, out bool _isFaceActive)
    {
        _flipX = false;
        _isFaceActive = (_dirIndex == 0 || _dirIndex == 4 || _dirIndex == 5 || _dirIndex == 6 || _dirIndex == 7);

        if (!_isFaceActive)
        {
            return null;
        }

        if (_bInHub)
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return face_RunR;
                    case 4: _flipX = true; return face_RunR;
                    case 5: _flipX = true; return face_RunRD;
                    case 6: return face_RunD;
                    case 7: return face_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return face_IdleR;
                    case 4: _flipX = true; return face_IdleR;
                    case 5: _flipX = true; return face_IdleRD;
                    case 6: return face_IdleD;
                    case 7: return face_IdleRD;
                }
            }
        }
        else
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return inDungeon_Face_RunR;
                    case 4: _flipX = true; return inDungeon_Face_RunR;
                    case 5: _flipX = true; return inDungeon_Face_RunRD;
                    case 6: return inDungeon_Face_RunD;
                    case 7: return inDungeon_Face_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return inDungeon_Face_IdleR;
                    case 4: _flipX = true; return inDungeon_Face_IdleR;
                    case 5: _flipX = true; return inDungeon_Face_IdleRD;
                    case 6: return inDungeon_Face_IdleD;
                    case 7: return inDungeon_Face_IdleRD;
                }
            }
        }

        return null;
    }

    private List<Sprite> GetBlinkSprites(bool _isMoving, bool _bInHub, int _dirIndex, out bool _flipX, out bool _isFaceActive)
    {
        _flipX = false;
        _isFaceActive = (_dirIndex == 0 || _dirIndex == 4 || _dirIndex == 5 || _dirIndex == 6 || _dirIndex == 7);

        if (!_isFaceActive)
        {
            return null;
        }

        if (_bInHub)
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return blink_RunR;
                    case 4: _flipX = true; return blink_RunR;
                    case 5: _flipX = true; return blink_RunRD;
                    case 6: return blink_RunD;
                    case 7: return blink_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return blink_IdleR;
                    case 4: _flipX = true; return blink_IdleR;
                    case 5: _flipX = true; return blink_IdleRD;
                    case 6: return blink_IdleD;
                    case 7: return blink_IdleRD;
                }
            }
        }
        else
        {
            if (_isMoving)
            {
                switch (_dirIndex)
                {
                    case 0: return inDungone_Blink_RunR;
                    case 4: _flipX = true; return inDungone_Blink_RunR;
                    case 5: _flipX = true; return inDungone_Blink_RunRD;
                    case 6: return inDungone_Blink_RunD;
                    case 7: return inDungone_Blink_RunRD;
                }
            }
            else
            {
                switch (_dirIndex)
                {
                    case 0: return inDungone_Blink_IdleR;
                    case 4: _flipX = true; return inDungone_Blink_IdleR;
                    case 5: _flipX = true; return inDungone_Blink_IdleRD;
                    case 6: return inDungone_Blink_IdleD;
                    case 7: return inDungone_Blink_IdleRD;
                }
            }
        }

        return null;
    }

    #endregion
}
