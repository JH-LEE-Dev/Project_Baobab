using System;
using System.Collections;
using UnityEngine;

public class TownProductionManager : MonoBehaviour
{
    public event Action OffroadDriveEndEvent;
    public event Action CharacterRideEndEvent;

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;
    [Header("Character Ride Settings")]
    [SerializeField] private float rideDuration = 0.3f;
    [SerializeField, Tooltip("초기 속도 비율 (0~1 사이, 0이면 0속도로 시작하여 완벽한 등가속)")]
    private float initialSpeedRatio = 0.0f;

    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;
    private SkyCameraProductionComponent skyCameraProductionComponent;
    private bool bCanGetOff = true;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        skyCameraProductionComponent = GetComponent<SkyCameraProductionComponent>();
    }

    public void Release()
    {
        if (offroadVehicleObj != null)
        {
            offroadVehicleObj.OffroadDriveEndEvent -= DriveEnd;
        }

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
            characterRideCoroutine = null;
        }
    }

    public void Offroad_DI(OffroadVehicleObj _offroadVehicleObj)
    {
        offroadVehicleObj = _offroadVehicleObj;

        offroadVehicleObj.OffroadDriveEndEvent -= DriveEnd;
        offroadVehicleObj.OffroadDriveEndEvent += DriveEnd;

        characterRidePoint = offroadVehicleObj.CharacterRidePoint;
    }

    public void Character_DI(Character _character)
    {
        character = _character;
    }

    public void StartDrive()
    {
        offroadVehicleObj.StartDrive(offroadDriveEndPoint);
    }

    public void StartCharacterRide()
    {
        if (character == null) return;

        character.col.enabled = false;

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
        }
        characterRideCoroutine = StartCoroutine(CharacterRideRoutine());
    }

    private IEnumerator CharacterRideRoutine()
    {
        if (character == null || characterRidePoint == null) yield break;

        inputManager.PauseMove(true);
        character.DisableShadow();

        Vector3 startPos = character.transform.position;
        Vector3 startScale = character.transform.localScale; // 탑승 시작 시점의 스케일 저장
        float elapsed = 0f;
        float duration = Mathf.Max(rideDuration, 0.001f);

        // 등가속 공식 유도:
        // 위치 보간 s(t) = v0*t + 0.5*a*t^2 (t = 0 ~ 1)
        // s(1) = 1 이어야 하므로, 1 = v0 + 0.5*a  => 0.5*a = 1 - v0
        // 따라서 s(t) = v0*t + (1 - v0)*t^2 가 성립함.
        // 여기서 v0는 initialSpeedRatio (초기 속도 비율)
        float v0 = Mathf.Clamp01(initialSpeedRatio);
        float oneMinusV0 = 1f - v0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Min(elapsed / duration, 1f); // 0.0 ~ 1.0 비율

            // 프레임이 튀어도 누적 시간 기반으로 비선형(등가속) 비율을 정확하게 계산
            // s(t) = v0*t + (1-v0)*t^2 (감속 구간이 전혀 없고 끝에서 최고 속도를 가짐)
            float t = (v0 * normalizedTime) + (oneMinusV0 * normalizedTime * normalizedTime);

            character.transform.position = Vector3.Lerp(startPos, characterRidePoint.position, t);

            Vector3 lookDir = characterRidePoint.position - character.transform.position;
            character.SetFacingDirection(lookDir);

            // 도착 지점으로 다다를수록 스케일이 0에 가깝게 작아짐 (Linear하게 축소)
            //character.transform.localScale = Vector3.Lerp(startScale, new Vector3(0.25f, 0.25f, 1f), normalizedTime);

            yield return null;
        }

        character.transform.position = characterRidePoint.position;
        character.SetFacingDirection(characterRidePoint.position - startPos);
        character.transform.localScale = startScale; // 다음 탑승 및 평소 상태를 위해 스케일 원상 복구
        character.SetHeight(0f);
        character.gameObject.SetActive(false);

        character.EnableShadow();

        // 캐릭터 도착 시 차량 착륙 임팩트 연출 실행
        if (offroadVehicleObj != null)
        {
            yield return offroadVehicleObj.CharacterRideLandingImpactSequence(() => CharacterRideEndEvent?.Invoke());
        }

        characterRideCoroutine = null;
    }

    private void DriveEnd()
    {
        character.col.enabled = true;
        bCanGetOff = true;
        OffroadDriveEndEvent?.Invoke();
    }

    public void GetOffFromTheVehicle()
    {
        if (character == null || offroadVehicleObj == null || offroadVehicleObj.getOffTransform == null || bCanGetOff == false)
        {
            return;
        }

        // 1. 캐릭터 복구 및 위치 설정
        character.gameObject.SetActive(true);
        character.transform.position = offroadVehicleObj.getOffTransform.position;

        // 2. 캐릭터 컴포넌트 상태 복구
        character.col.enabled = true;
        character.EnableShadow();

        // 3. 탑승 위치에서 내리는 위치를 바라보도록 설정
        if (offroadVehicleObj.CharacterRidePoint != null)
        {
            Vector3 getOffDir = offroadVehicleObj.getOffTransform.position - offroadVehicleObj.CharacterRidePoint.position;
            character.SetFacingDirection(getOffDir);
        }

        // 4. 입력 제어 해제
        if (inputManager != null)
        {
            inputManager.PauseMove(false);
        }
    }

    public void SetbCanGetOff(bool _boolean)
    {
        bCanGetOff = _boolean;
    }
}
