using System.Collections.Generic;

/// <summary>UI가 읽기만 하는 런 상태. RoguelikeSystem이 구현한다.</summary>
public interface IRoguelikeRunInfo
{
    ERunPhase Phase { get; }
    WaveSpec CurrentWave { get; }
    int WaveKills { get; }

    int FieldTreeCount { get; }
    int Threshold { get; }
    int SpawnCap { get; }
    EPressureState PressureState { get; }
    float PressureGraceRatio { get; }
    float PressureCooldownRatio { get; }

    bool IsCardSelectionOpen { get; }
    bool IsFirstPick { get; }
    IReadOnlyList<CardOffer> CurrentOffers { get; }
    /// <summary>이번 선택 단계에 리롤이 남아 있는지(선택 단계마다 1회, 기획서 v2.4 4-1).</summary>
    bool CanReroll { get; }

    /// <summary>방금 선택으로 함께 사라져야 하는 배타 카드의 제시 인덱스.</summary>
    IReadOnlyList<int> LastDiscardedOfferIndices { get; }

    RunBuildState Build { get; }
    CardDatabaseSO CardDatabase { get; }
    RoguelikeRecordData Records { get; }

    /// <summary>방금 끝난 판의 기록과 랭킹 진입 순위. 결과 화면이 읽는다.</summary>
    RunRecordEntry LastRunRecord { get; }
    int LastClearRank { get; }
    int LastUnlimitRank { get; }
}
