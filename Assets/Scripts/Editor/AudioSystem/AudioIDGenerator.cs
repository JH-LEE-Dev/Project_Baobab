using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public class AudioIDGenerator
{
    private const string SoundAssetsRoot = "Assets/Sounds";
    private const string MixerAssetsRoot = "Assets/Sounds/Mixer";
    private const string ExportPath = "Assets/Scripts/Application Layer/AudioSystem/AudioSystemUsingData.cs";

    // 믹서 애셋 안에서 이 이름을 가진 그룹을 찾아 MixerID를 만든다 (Master → BGM/SFX/UI/Ambience 구조 기준).
    private static readonly string[] MixerGroupNames = { "Master", "BGM", "SFX", "UI", "Ambience" };

    [MenuItem("Tools/Audio/Generate Sound IDs")]
    public static void Generate()
    {
        if (!Directory.Exists(SoundAssetsRoot))
        {
            Debug.LogError($"[AudioIDGenerator] Sound assets root folder not found: {SoundAssetsRoot}");
            return;
        }

        HashSet<string> uniqueSoundNames = new HashSet<string>();
        Dictionary<string, UnityEngine.Object> soundNameToAsset = new Dictionary<string, UnityEngine.Object>();
        Dictionary<string, int> soundNameToHash = new Dictionary<string, int>();

        HashSet<string> uniqueMixerNames = new HashSet<string>();
        Dictionary<string, AudioMixerGroup> mixerNameToAsset = new Dictionary<string, AudioMixerGroup>();
        Dictionary<string, int> mixerNameToHash = new Dictionary<string, int>();

        // 1. Sound ID 스캔
        ScanSoundAssets(uniqueSoundNames, soundNameToAsset, soundNameToHash);

        // 2. Mixer ID 스캔 및 에셋 매핑
        ScanMixerAssets(uniqueMixerNames, mixerNameToAsset, mixerNameToHash);

        // 3. Enum 파일 생성
        GenerateEnumFile(soundNameToHash, mixerNameToHash);

        // 4. AudioDatabase 자동 갱신 (믹서 리스트 포함)
        UpdateAudioDatabases(soundNameToHash, soundNameToAsset, mixerNameToHash, mixerNameToAsset);

        AssetDatabase.Refresh();
        Debug.Log($"[AudioIDGenerator] Sound IDs and Mixer IDs generated successfully.");
    }

    private static void ScanSoundAssets(HashSet<string> _uniqueNames, Dictionary<string, UnityEngine.Object> _assetMap, Dictionary<string, int> _hashMap)
    {
        string[] directories = Directory.GetDirectories(SoundAssetsRoot, "*", SearchOption.AllDirectories);
        List<string> allTargetDirs = new List<string>(directories) { SoundAssetsRoot };

        foreach (string dirPath in allTargetDirs)
        {
            string normalizedPath = dirPath.Replace('\\', '/');
            if (normalizedPath.Contains(MixerAssetsRoot)) continue;

            string folderName = Path.GetFileName(normalizedPath);

            if (folderName.EndsWith("_Cue", StringComparison.OrdinalIgnoreCase))
            {
                string[] guids = AssetDatabase.FindAssets("t:AudioCueData", new[] { normalizedPath });
                if (guids.Length > 0)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    AudioCueData cue = AssetDatabase.LoadAssetAtPath<AudioCueData>(assetPath);
                    RegisterID(cue.name, cue, _uniqueNames, _assetMap, _hashMap);
                }
            }
            else if (folderName.EndsWith("_Clip", StringComparison.OrdinalIgnoreCase) || normalizedPath == SoundAssetsRoot)
            {
                string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { normalizedPath });
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(assetPath).Replace('\\', '/') != normalizedPath) continue;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    RegisterID(clip.name, clip, _uniqueNames, _assetMap, _hashMap);
                }
            }
        }
    }

    private static void ScanMixerAssets(HashSet<string> _uniqueNames, Dictionary<string, AudioMixerGroup> _assetMap, Dictionary<string, int> _hashMap)
    {
        if (!Directory.Exists(MixerAssetsRoot)) return;

        string[] guids = AssetDatabase.FindAssets("t:AudioMixer", new[] { MixerAssetsRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (mixer == null) continue;

            // 믹서 파일 하나가 아니라, 그 안의 각 그룹(Master/BGM/SFX/UI/Ambience)마다 MixerID를 만든다.
            foreach (string groupName in MixerGroupNames)
            {
                if (_uniqueNames.Contains(groupName)) continue;

                AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
                if (groups.Length == 0) continue;

                _uniqueNames.Add(groupName);
                _assetMap.Add(groupName, groups[0]);
                _hashMap.Add(groupName, GetStableHashCode(groupName));
            }
        }
    }

    private static void RegisterID(string _name, UnityEngine.Object _asset, HashSet<string> _uniqueNames, Dictionary<string, UnityEngine.Object> _assetMap, Dictionary<string, int> _hashMap)
    {
        if (string.IsNullOrEmpty(_name) || _uniqueNames.Contains(_name)) return;
        _uniqueNames.Add(_name);
        _assetMap.Add(_name, _asset);
        _hashMap.Add(_name, GetStableHashCode(_name));
    }

    private static void GenerateEnumFile(Dictionary<string, int> _soundMap, Dictionary<string, int> _mixerMap)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        
        sb.AppendLine("public enum SoundID");
        sb.AppendLine("{");
        sb.AppendLine("    None = 0,");
        foreach (var kvp in _soundMap) sb.AppendLine($"    {SanitizeName(kvp.Key)} = {kvp.Value},");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("public enum MixerID");
        sb.AppendLine("{");
        sb.AppendLine("    None = 0,");
        foreach (var kvp in _mixerMap) sb.AppendLine($"    {SanitizeName(kvp.Key)} = {kvp.Value},");
        sb.AppendLine("}");

        string dir = Path.GetDirectoryName(ExportPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(ExportPath, sb.ToString(), Encoding.UTF8);
    }

    private static void UpdateAudioDatabases(Dictionary<string, int> _soundIdMap, Dictionary<string, UnityEngine.Object> _soundAssetMap, Dictionary<string, int> _mixerIdMap, Dictionary<string, AudioMixerGroup> _mixerAssetMap)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioDatabase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioDatabase database = AssetDatabase.LoadAssetAtPath<AudioDatabase>(path);
            if (database == null) continue;

            bool isDirty = false;

            // 1. Mixer 리스트 갱신
            database.mixers = new List<MixerMapping>();
            foreach (var kvp in _mixerIdMap)
            {
                database.mixers.Add(new MixerMapping { id = (MixerID)kvp.Value, group = _mixerAssetMap[kvp.Key] });
                isDirty = true;
            }

            // 2. Sound 리스트 갱신 및 믹서 자동 연결
            if (database.sounds == null) database.sounds = new List<AudioData>();
            
            Dictionary<int, AudioData> existingSlots = new Dictionary<int, AudioData>();
            foreach (var sound in database.sounds)
            {
                int idVal = (int)sound.id;
                if (!existingSlots.ContainsKey(idVal)) existingSlots.Add(idVal, sound);
            }

            foreach (var kvp in _soundIdMap)
            {
                string originalName = kvp.Key;
                int hash = kvp.Value;
                UnityEngine.Object asset = _soundAssetMap[originalName];

                if (!existingSlots.TryGetValue(hash, out AudioData data))
                {
                    data = new AudioData { id = (SoundID)hash };
                    database.sounds.Add(data);
                    isDirty = true;
                }

                // 에셋 자동 할당
                if (asset is AudioClip clip && data.clip == null && data.cueData == null) { data.clip = clip; isDirty = true; }
                else if (asset is AudioCueData cue && data.cueData == null && data.clip == null) { data.cueData = cue; isDirty = true; }

                // 믹서 그룹 자동 연결: 사용자가 선택한 MixerID에 해당하는 그룹을 직접 할당
                if (data.mixerId != MixerID.None && data.mixerGroup == null)
                {
                    foreach (var mapping in database.mixers)
                    {
                        if (mapping.id == data.mixerId && mapping.group != null)
                        {
                            data.mixerGroup = mapping.group;
                            isDirty = true;
                            break;
                        }
                    }
                }
            }

            if (isDirty) EditorUtility.SetDirty(database);
        }
        
        if (guids.Length > 0) AssetDatabase.SaveAssets();
    }

    private static int GetStableHashCode(string str)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;
            for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0') break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }
            return hash1 + (hash2 * 1566083941);
        }
    }

    private static string SanitizeName(string _input)
    {
        if (string.IsNullOrEmpty(_input)) return string.Empty;
        string result = Regex.Replace(_input, @"[^a-zA-Z0-9]", " ");
        string[] words = result.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder sb = new StringBuilder();
        foreach (string word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpper(word[0]));
                if (word.Length > 1) sb.Append(word.Substring(1));
            }
        }
        return sb.ToString();
    }
}
