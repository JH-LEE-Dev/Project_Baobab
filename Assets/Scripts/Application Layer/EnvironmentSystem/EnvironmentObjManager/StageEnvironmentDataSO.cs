using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StageEnvironmentData", menuName = "ScriptableObjects/StageEnvironmentData", order = 2)]
public class StageEnvironmentDataSO : ScriptableObject
{
    [Header("Pool Settings")]
    [SerializeField] private List<EnvironmentObj> envObjPrefabs;

    [Header("Cloud Settings")]
    [SerializeField] private List<Sprite> cloudSprites;
    [SerializeField] private Color cloudColor = Color.white;

    // // 퍼블릭 프로퍼티
    public List<EnvironmentObj> EnvObjPrefabs => envObjPrefabs;
    public List<Sprite> CloudSprites => cloudSprites;
    public Color CloudColor => cloudColor;
}
