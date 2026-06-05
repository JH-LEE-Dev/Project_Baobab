using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizationMapping", menuName = "Localization/Mapping")]
public class LocalizationMapping : ScriptableObject
{
    [Serializable]
    public struct MappingEntry
    {
        public string enumTypeName;  // "ForestType" 등
        public string enumValueName; // "DeepForest" 등
        public int enumIntValue;     // Enum의 정수 값
        public int compositeKey;     // Localization Key (비트 연산값)
    }

    // //외부 의존성
    public List<MappingEntry> mappings = new List<MappingEntry>();
}
