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

    // 상태 데이터
    private float frameTimer = 0f;
    private int currentFrameIndex = 0;
    private bool isDeadStartFinished = false;
    
    private bool prevIsMoving = false;
    private bool prevBInHub = true;
    private bool prevIsBlinking = false;
    private bool prevIsDead = false;
    private int prevDirIndex = -1;
    private int prevShadowDirIndex = -1;

    #region Public Methods

    public void Initialize()
    {
        frameTimer = 0f;
        currentFrameIndex = 0;
        isDeadStartFinished = false;
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
        if (_isMoving != prevIsMoving || _bInHub != prevBInHub || _isBlinking != prevIsBlinking || _isDead != prevIsDead || dirIndex != prevDirIndex)
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            isDeadStartFinished = false;

            prevIsMoving = _isMoving;
            prevBInHub = _bInHub;
            prevIsBlinking = _isBlinking;
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
