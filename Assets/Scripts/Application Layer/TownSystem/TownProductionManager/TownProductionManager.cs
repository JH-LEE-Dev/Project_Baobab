using System;
using System.Collections;
using UnityEngine;

public class TownProductionManager : MonoBehaviour
{
    public event Action OffroadDriveEndEvent;

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;
    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
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

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
        }
        characterRideCoroutine = StartCoroutine(CharacterRideRoutine());
    }

    private IEnumerator CharacterRideRoutine()
    {
        yield return new WaitForSeconds(0.15f);

        if (character == null || characterRidePoint == null) yield break;

        inputManager.Pause(true);

        Vector3 startPos = character.transform.position;
        float duration = 0.5f;
        float height = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 선형 보간 (X, Y 좌표 - 지면 위치 기준)
            Vector3 currentPos = Vector3.Lerp(startPos, characterRidePoint.position, t);

            // 포물선 높이 계산 및 적용
            float arc = Mathf.Sin(t * Mathf.PI) * height;

            // CustomSortable의 소팅 보정(Y - Height)을 위해 Transform Y에 높이를 더하고 SetHeight에도 전달
            currentPos.y += arc;
            character.SetHeight(arc);

            character.transform.position = currentPos;
            yield return null;
        }

        character.transform.position = characterRidePoint.position;
        character.SetHeight(0f);
        character.gameObject.SetActive(false);

        // 캐릭터 도착 시 차량 착륙 임팩트 연출 실행
        if (offroadVehicleObj != null)
        {
            yield return offroadVehicleObj.VehicleLandingImpactSequence();
        }

        characterRideCoroutine = null;
    }

    private void DriveEnd()
    {
        OffroadDriveEndEvent?.Invoke();
        inputManager.Pause(false);
    }
}
