using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class PopulateKeyIconDatabase
{
    [MenuItem("Tools/Populate Key Icon Database")]
    public static void Populate()
    {
        string _assetPath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
        KeyIconDatabase _db = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(_assetPath);
        
        if (null == _db)
        {
            Debug.LogError($"Could not find KeyIconDatabase at {_assetPath}");
            return;
        }

        // 1. Keyboard Sprites
        string _kbSpriteSheetPath = "Assets/Graphics/HUD/Keyboard/kb_light_all.png";
        Sprite[] _kbSprites = AssetDatabase.LoadAllAssetsAtPath(_kbSpriteSheetPath).OfType<Sprite>().ToArray();

        // 2. Mouse Sprites
        string _mouseSpriteSheetPath = "Assets/Graphics/HUD/Keyboard/mouse.png";
        Sprite[] _mouseSprites = AssetDatabase.LoadAllAssetsAtPath(_mouseSpriteSheetPath).OfType<Sprite>().ToArray();

        // 3. Gamepad Sprites (tilemap.png)
        string _gamepadSpriteSheetPath = "Assets/Graphics/HUD/GamePad/tilemap.png";
        Sprite[] _gamepadSprites = AssetDatabase.LoadAllAssetsAtPath(_gamepadSpriteSheetPath).OfType<Sprite>().ToArray();

        List<KeyIconDatabase.KeyIconEntry> _sharedEntries = new List<KeyIconDatabase.KeyIconEntry>();
        List<KeyIconDatabase.KeyIconEntry> _xboxEntries = new List<KeyIconDatabase.KeyIconEntry>();
        List<KeyIconDatabase.KeyIconEntry> _psEntries = new List<KeyIconDatabase.KeyIconEntry>();
        
        // --- Keyboard Entries (a-z) ---
        for (char c = 'a'; c <= 'z'; c++)
        {
            string _letter = c.ToString().ToUpper();
            if ('V' == _letter[0])
            {
                AddEntryWithFallback(_sharedEntries, _kbSprites, $"<Keyboard>/{c}", "V", "kb_light_all_316", "V_Outline");
            }
            else
            {
                AddEntryWithFallback(_sharedEntries, _kbSprites, $"<Keyboard>/{c}", _letter, _letter + "_Outline");
            }
        }
        
        // --- Keyboard Entries (0-9) ---
        for (int i = 0; i <= 9; i++)
        {
            AddEntryWithFallback(_sharedEntries, _kbSprites, $"<Keyboard>/{i}", i.ToString(), i.ToString() + "_Outline");
        }
        
        // --- Function Keys (F1-F12) ---
        for (int i = 1; i <= 12; i++)
        {
            AddEntryWithFallback(_sharedEntries, _kbSprites, $"<Keyboard>/f{i}", $"F{i}", $"F{i}_Outline");
        }
        
        // --- Special Keys ---
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/escape", "ESC", "ESC_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/tab", "Tab", "Tab_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/space", "Space", "Space_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/enter", "Enter", "Enter_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/numpadEnter", "Enter", "Enter_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/shift", "Shift", "Shift_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/leftShift", "Shift", "Shift_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/rightShift", "Shift", "Shift_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/ctrl", "Ctrl", "Ctrl_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/leftCtrl", "Ctrl", "Ctrl_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/rightCtrl", "Ctrl", "Ctrl_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/alt", "Alt", "Alt_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/leftAlt", "Alt", "Alt_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/rightAlt", "Alt", "Alt_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/pageUp", "PageUp", "PageUp_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/pageDown", "PageDown", "PageDown_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/home", "Home", "Home_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/end", "End", "End_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/insert", "Insert", "Insert_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/slash", "Slash", "Slash_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/upArrow", "Up", "Up_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/downArrow", "Down", "Down_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/leftArrow", "Left", "Left_Outline");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/rightArrow", "Right", "Right_Outline");

        // --- Punctuation marks (Outline versions) ---
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#>", "Greaterthan_Outline", "Greaterthan");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#<", "Lessthan_Outline", "Lessthan");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/quote", "SingleQuote_Outline", "SingleQuote");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#'", "SingleQuote_Outline", "SingleQuote");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/period", "Dot_Outline", "Dot");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#.", "Dot_Outline", "Dot");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/semicolon", "Colon_Outline", "Colon");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#;", "Colon_Outline", "Colon");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#:", "Colon_Outline", "Colon");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/leftBracket", "LeftBracket_Outline", "LeftBracket");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#[", "LeftBracket_Outline", "LeftBracket");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/rightBracket", "RightBracket_Outline", "RightBracket");
        AddEntryWithFallback(_sharedEntries, _kbSprites, "<Keyboard>/#]", "RightBracket_Outline", "RightBracket");

        // --- Mouse Entries ---
        AddEntryWithFallback(_sharedEntries, _mouseSprites, "<Mouse>/leftButton", "mouse_0", "mouse_left");
        AddEntryWithFallback(_sharedEntries, _mouseSprites, "<Mouse>/rightButton", "mouse_1", "mouse_right");
        AddEntryWithFallback(_sharedEntries, _mouseSprites, "<Mouse>/middleButton", "mouse_2", "mouse_middle");
        AddEntryWithFallback(_sharedEntries, _mouseSprites, "<Mouse>/delta", "mouse_3", "mouse_move");

        // --- Xbox Entries ---
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/buttonSouth", "XBox_A", "Pad_A");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/buttonEast", "XBox_B", "Pad_B");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/buttonWest", "XBox_X", "Pad_X");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/buttonNorth", "XBox_Y", "Pad_Y");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftShoulder", "Pad_LB", "LB");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightShoulder", "Pad_RB", "RB");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftTrigger", "Pad_LT", "LT");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightTrigger", "Pad_RT", "RT");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStick", "LStick_Full", "LStick_None", "PadLStick_Full");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStick/up", "LStick_Up", "PadLStick_Up");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStick/down", "LStick_Down", "PadLStick_Down");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStick/left", "LStick_Left", "PadLStick_Left");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStick/right", "LStick_Right", "PadLStick_Right");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/leftStickPress", "LStick_Click", "PadLStick_Click");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStick", "RStick_Full", "RStick_None", "PadRStick_Full");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStick/up", "RStick_Up", "PadRStick_Up");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStick/down", "RStick_Down", "PadRStick_Down");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStick/left", "RStick_Left", "PadRStick_Left");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStick/right", "RStick_Right", "PadRStick_Right");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/rightStickPress", "RStick_Click", "PadRStick_Click");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/start", "XBox_Start", "Pad_Start");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/select", "XBox_Back", "Pad_Back");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/dpad", "DPad_Full", "DPad_None");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/dpad/up", "DPad_Up");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/dpad/down", "DPad_Down");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/dpad/left", "DPad_Left");
        AddEntryWithFallback(_xboxEntries, _gamepadSprites, "<Gamepad>/dpad/right", "DPad_Right");

        // --- PlayStation Entries ---
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/buttonSouth", "PS_A", "PS_Cross");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/buttonEast", "PS_B", "PS_Circle");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/buttonWest", "PS_X", "PS_Square");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/buttonNorth", "PS_Y", "PS_Triangle");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftShoulder", "PS_L1", "L1");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightShoulder", "PS_R1", "R1");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftTrigger", "PS_L2", "L2");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightTrigger", "PS_R2", "R2");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStick", "LStick_Full", "LStick_None", "PadLStick_Full");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStick/up", "LStick_Up", "PadLStick_Up");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStick/down", "LStick_Down", "PadLStick_Down");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStick/left", "LStick_Left", "PadLStick_Left");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStick/right", "LStick_Right", "PadLStick_Right");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/leftStickPress", "LStick_Click", "PadLStick_Click");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStick", "RStick_Full", "RStick_None", "PadRStick_Full");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStick/up", "RStick_Up", "PadRStick_Up");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStick/down", "RStick_Down", "PadRStick_Down");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStick/left", "RStick_Left", "PadRStick_Left");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStick/right", "RStick_Right", "PadRStick_Right");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/rightStickPress", "RStick_Click", "PadRStick_Click");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/start", "PS_Option", "PS_Start");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/select", "PS_Share", "PS_Select");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/dpad", "DPad_Full", "DPad_None");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/dpad/up", "DPad_Up");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/dpad/down", "DPad_Down");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/dpad/left", "DPad_Left");
        AddEntryWithFallback(_psEntries, _gamepadSprites, "<Gamepad>/dpad/right", "DPad_Right");
        
        // Update Database directly
        _db.SetEntriesForEditor(_sharedEntries.ToArray(), _xboxEntries.ToArray(), _psEntries.ToArray());
        
        EditorUtility.SetDirty(_db);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Successfully populated KeyIconDatabase: Shared={_sharedEntries.Count}, Xbox={_xboxEntries.Count}, PS={_psEntries.Count}");
    }

    private static void AddEntryWithFallback(List<KeyIconDatabase.KeyIconEntry> _entries, Sprite[] _sprites, string _path, params string[] _spriteNames)
    {
        if (null == _sprites || null == _spriteNames) return;
        for (int i = 0; i < _spriteNames.Length; i++)
        {
            string _name = _spriteNames[i];
            Sprite _sprite = _sprites.FirstOrDefault(s => null != s && s.name == _name);
            if (null != _sprite)
            {
                _entries.Add(new KeyIconDatabase.KeyIconEntry { bindingPath = _path, icon = _sprite });
                return;
            }
        }
        Debug.LogWarning($"Could not find any sprite named {string.Join(", ", _spriteNames)} for path {_path}");
    }
}
