using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 로그라이크 기록 파일(최근 1판 / 클리어 랭킹 / 언리밋 랭킹)의 읽기·쓰기. 기획서 3-6.
///
/// [본편 세이브와 분리]
/// 본편 세이브(GameSaveData)에 넣지 않고 같은 폴더의 별도 파일을 쓴다.
///   - 본편 "새로하기"(DeleteSaveData)가 지워도 로그라이크 기록은 남는다.
///   - 로그라이크 세션은 본편 저장이 전부 막혀 있으므로(SaveManager) 본편 경로로는 애초에 쓸 수 없다.
///   - HasSaveData(이어하기 버튼)는 본편 세이브 파일만 보므로 이 파일이 생겨도 영향이 없다.
/// 쓰기는 임시 파일에 먼저 쓰고 교체한다(SettingsRepository와 같은 방식). 저장 도중 꺼져도 기존 파일은 멀쩡하다.
///
/// [Steam 클라우드] 지금은 동기화하지 않는다. SteamCloudSaveService가 본편 세이브 파일 하나만 다루기 때문이다.
/// </summary>
public static class RoguelikeRecordRepository
{
    private const string FILE_NAME = "RoguelikeRecords.json";
    private const string TEMP_SUFFIX = ".tmp";

    public static string FilePath => Path.Combine(GamePaths.SaveFolder, FILE_NAME);

    /// <summary>기록을 읽는다. 파일이 없거나 깨졌으면 빈 기록을 돌려준다(게임을 막지 않는다).</summary>
    public static RoguelikeRecordData Load()
    {
        string path = FilePath;

        try
        {
            if (false == File.Exists(path)) return new RoguelikeRecordData();

            if (false == SafeFileIO.TryReadAllText(path, out string text, out Exception error))
            {
                Debug.LogWarning(GamePaths.Redact($"[RoguelikeRecordRepository] 기록을 읽지 못했습니다: {error?.Message}"));
                return new RoguelikeRecordData();
            }

            RoguelikeRecordData data = JsonUtility.FromJson<RoguelikeRecordData>(text);
            if (null == data) return new RoguelikeRecordData();

            data.EnsureValid();
            // 도감 기록이 생기기 전 파일이면 도전과제 · 최근 · 랭킹 기록에서 소급한다(저장은 다음 기존 저장 시점에 함께 남는다).
            data.BackfillCollection(RoguelikeCollection.CardIds);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning(GamePaths.Redact($"[RoguelikeRecordRepository] 기록 파일이 손상되어 빈 기록으로 시작합니다: {e.Message}"));
            return new RoguelikeRecordData();
        }
    }

    /// <summary>
    /// 도감 창을 닫을 때 부른다(기획서 v3.3 8-4). 기록을 읽고 → 이번에 앞면으로 보여 준 카드(_seenIds)를 본 카드 목록에 더하고
    /// 최초 완성 축하를 보여 줬으면(_bCelebrated) 표시한 뒤 저장한다. 도감에 없는 id는 무시한다. 바뀐 것이 없으면 저장하지 않는다.
    /// </summary>
    public static void SaveCollectionSeen(List<string> _seenIds, bool _bCelebrated)
    {
        RoguelikeRecordData data = Load();
        bool bChanged = false;

        for (int i = 0; null != _seenIds && i < _seenIds.Count; i++)
        {
            string cardId = _seenIds[i];
            if (string.IsNullOrEmpty(cardId) || data.collectionSeenCardIds.Contains(cardId)) continue;

            // 도감 정본 목록에 있고 실제로 고른 적 있는 카드만 본 카드로 센다.
            if (false == data.collectionCardIds.Contains(cardId)) continue;

            data.collectionSeenCardIds.Add(cardId);
            bChanged = true;
        }

        if (true == _bCelebrated && false == data.collectionCompleteCelebrated)
        {
            data.collectionCompleteCelebrated = true;
            bChanged = true;
        }

        if (true == bChanged) Save(data);
    }

    public static void Save(RoguelikeRecordData _data)
    {
        if (null == _data) return;

        string path = FilePath;
        string tempPath = path + TEMP_SUFFIX;

        try
        {
            Directory.CreateDirectory(GamePaths.SaveFolder);
        }
        catch (Exception e)
        {
            Debug.LogError(GamePaths.Redact($"[RoguelikeRecordRepository] 저장 폴더를 만들지 못했습니다: {e.Message}"));
            return;
        }

        _data.version = RoguelikeRecordData.CurrentVersion;

        if (SafeFileIO.TryWriteAllText(tempPath, JsonUtility.ToJson(_data, true), out Exception error)
            && SafeFileIO.TryReplaceOrMove(tempPath, path, null, out error))
        {
            return;
        }

        Debug.LogError(GamePaths.Redact($"[RoguelikeRecordRepository] 기록 저장에 실패했습니다: {error?.Message}"));
        SafeFileIO.CleanUpTempFile(tempPath);
    }
}
