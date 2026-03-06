using System;
using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public event Action<Character> CharacterSpawnedEvent;

    //외부 의존성
    private InputManager inputManager;
    private IEnvironmentProvider environmentProvider;


    //내부 의존성
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private Character characterPrefab;

    private Character character;

    public void Initialize(InputManager _inputManager, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;
    }

    public void SpawnCharacter()
    {
        if(characterSpawnPoint == null || characterPrefab == null || character != null)
            return;

        character = Instantiate(characterPrefab,transform);
        character.transform.position = characterSpawnPoint.position;
        character.Initialize(inputManager, environmentProvider);

        CharacterSpawnedEvent?.Invoke(character);
    }
}
