using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class PopulateKeyIconDatabase
{
    [MenuItem("Tools/Populate Key Icon Database")]
    public static void Populate()
    {
        string assetPath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
        KeyIconDatabase db = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(assetPath);
        
        if (db == null)
        {
            Debug.LogError($"Could not find KeyIconDatabase at {assetPath}");
            return;
        }

        string spriteSheetPath = "Assets/Graphics/HUD/Keyboard/kb_light_all.png";
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
        
        // Filter out the texture itself and keep only sprites
        Sprite[] sprites = allAssets.OfType<Sprite>().ToArray();

        List<KeyIconDatabase.KeyIconEntry> entries = new List<KeyIconDatabase.KeyIconEntry>();
        
        // a-z
        for (char c = 'a'; c <= 'z'; c++)
        {
            AddEntry(entries, sprites, $"<Keyboard>/{c}", c.ToString().ToUpper());
        }
        
        // 0-9
        for (int i = 0; i <= 9; i++)
        {
            AddEntry(entries, sprites, $"<Keyboard>/{i}", i.ToString());
        }
        
        // F1-F12
        for (int i = 1; i <= 12; i++)
        {
            AddEntry(entries, sprites, $"<Keyboard>/f{i}", $"F{i}");
        }
        
        // escape
        AddEntry(entries, sprites, "<Keyboard>/escape", "ESC");
        
        // Set entries via reflection or serialized object
        SerializedObject so = new SerializedObject(db);
        SerializedProperty entriesProp = so.FindProperty("entries");
        
        entriesProp.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("bindingPath").stringValue = entries[i].bindingPath;
            element.FindPropertyRelative("icon").objectReferenceValue = entries[i].icon;
        }
        
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Successfully populated KeyIconDatabase with {entries.Count} entries.");
    }
    
    private static void AddEntry(List<KeyIconDatabase.KeyIconEntry> entries, Sprite[] sprites, string path, string spriteName)
    {
        Sprite sprite = sprites.FirstOrDefault(s => s.name == spriteName);
        if (sprite != null)
        {
            entries.Add(new KeyIconDatabase.KeyIconEntry { bindingPath = path, icon = sprite });
        }
        else
        {
            Debug.LogWarning($"Could not find sprite named {spriteName}");
        }
    }
}
