using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum AllowedInput
{
    Both,
    MKOnly,      // Mouse & Keyboard Only
    PadOnly      // Gamepad Only
}

[ToolbarSectionAttribute("Input")]
public class InputToolbar : IEditorToolbar
{
    private const string AllowedInputPrefKey = "InputToolbar.AllowedInput";
    
    private readonly string[] _allowedInputOptions = { "Both", "M&K Only", "Pad Only" };
    private AllowedInput _allowedInput = AllowedInput.Both;
    
    private class InputEvent
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        
        public InputEvent(string type, string description)
        {
            Type = type;
            Description = description;
            Timestamp = DateTime.Now;
        }
    }
    
    private readonly Queue<InputEvent> _lastInputs = new Queue<InputEvent>(3);
    private HashSet<KeyCode> _lastKeysPressed = new HashSet<KeyCode>();
    private Vector2 _lastMousePosition = Vector2.zero;
    
    public InputToolbar()
    {
        _allowedInput = (AllowedInput)EditorPrefs.GetInt(AllowedInputPrefKey, (int)AllowedInput.Both);
    }
    
    public bool ShouldShow()
    {
        return true;
    }
    
    public void OnGUI()
    {
        // Check input during OnGUI (Event.current is only available here)
        CheckInput();
        
        // Allowed Input Selector
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Allowed:", GUILayout.Width(60));
        
        int currentIndex = (int)_allowedInput;
        int newIndex = EditorGUILayout.Popup(currentIndex, _allowedInputOptions, GUILayout.Width(90));
        
        if (newIndex != currentIndex)
        {
            _allowedInput = (AllowedInput)newIndex;
            EditorPrefs.SetInt(AllowedInputPrefKey, (int)_allowedInput);
            Debug.Log($"[InputToolbar] Allowed input changed to: {_allowedInputOptions[newIndex]}");
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // Last 3 Inputs
        EditorGUILayout.LabelField("Last 3 Inputs:", EditorStyles.miniLabel);
        
        if (_lastInputs.Count == 0)
        {
            EditorGUILayout.LabelField("  None", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            // Display in reverse order (most recent first)
            var inputsArray = _lastInputs.ToArray();
            Array.Reverse(inputsArray);
            
            foreach (var input in inputsArray)
            {
                if (input == null) continue;
                
                string timeAgo = GetTimeAgoString(input.Timestamp);
                string displayText = $"{input.Type}: {input.Description} ({timeAgo})";
                
                EditorGUILayout.LabelField(displayText, EditorStyles.miniLabel);
            }
        }
    }
    
    private void CheckInput()
    {
        Event currentEvent = Event.current;
        if (currentEvent == null)
            return;
        
        CheckMouseInput(currentEvent);
        CheckKeyboardInput(currentEvent);
        
        // Gamepad input check works in both edit and play mode
        CheckGamepadInput();
    }
    
    private void CheckMouseInput(Event currentEvent)
    {
        if (_allowedInput == AllowedInput.PadOnly)
            return;
        
        // Check for mouse buttons
        if (currentEvent.type == EventType.MouseDown)
        {
            string buttonName = GetMouseButtonName(currentEvent.button);
            AddInputEvent("Mouse", buttonName);
            _lastMousePosition = currentEvent.mousePosition;
        }
        
        // Check for mouse scroll
        if (currentEvent.type == EventType.ScrollWheel)
        {
            string direction = currentEvent.delta.y > 0 ? "Scroll Up" : "Scroll Down";
            AddInputEvent("Mouse", direction);
        }
    }
    
    private void CheckKeyboardInput(Event currentEvent)
    {
        if (_allowedInput == AllowedInput.PadOnly)
            return;
        
        // Check for key down events
        if (currentEvent.type == EventType.KeyDown)
        {
            KeyCode keyCode = currentEvent.keyCode;
            
            // Skip joystick keys
            if (keyCode >= KeyCode.JoystickButton0 && keyCode <= KeyCode.JoystickButton19)
                return;
            
            // Skip if already tracked
            if (_lastKeysPressed.Contains(keyCode))
                return;
            
            _lastKeysPressed.Add(keyCode);
            
            string keyName = GetKeyName(keyCode);
            AddInputEvent("Keyboard", keyName);
        }
        else if (currentEvent.type == EventType.KeyUp)
        {
            // Remove from tracked keys when released
            _lastKeysPressed.Remove(currentEvent.keyCode);
        }
    }
    
    private void CheckGamepadInput()
    {
        if (_allowedInput == AllowedInput.MKOnly)
            return;
        
        // Check for gamepad connection
        string[] joystickNames = Input.GetJoystickNames();
        bool gamepadConnected = joystickNames.Length > 0 && 
                                joystickNames.Any(name => !string.IsNullOrEmpty(name));
        
        if (!gamepadConnected)
            return;
        
        // Check for gamepad button presses (works in editor too)
        for (int i = 0; i < 20; i++)
        {
            KeyCode joystickButton = (KeyCode)((int)KeyCode.JoystickButton0 + i);
            if (Input.GetKeyDown(joystickButton))
            {
                string buttonName = GetGamepadButtonName(i);
                AddInputEvent("Gamepad", buttonName);
                return;
            }
        }
        
        // Check for joystick axis movement (only works in play mode)
        if (EditorApplication.isPlaying)
        {
            const float deadzone = 0.3f; // Higher deadzone to avoid constant triggering
            Vector2 leftStick = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            Vector2 rightStick = new Vector2(Input.GetAxis("3rd axis"), Input.GetAxis("4th axis"));
            
            if (leftStick.magnitude > deadzone)
            {
                AddInputEvent("Gamepad", "Left Stick");
                return;
            }
            
            if (rightStick.magnitude > deadzone)
            {
                AddInputEvent("Gamepad", "Right Stick");
                return;
            }
            
            // Check for triggers
            float lt = Input.GetAxis("5th axis");
            float rt = Input.GetAxis("6th axis");
            
            if (lt > deadzone)
            {
                AddInputEvent("Gamepad", "LT");
                return;
            }
            
            if (rt > deadzone)
            {
                AddInputEvent("Gamepad", "RT");
                return;
            }
        }
    }
    
    private string GetKeyName(KeyCode keyCode)
    {
        // Convert key code to readable name
        string keyName = keyCode.ToString();
        
        // Handle special cases
        if (keyName.StartsWith("Alpha"))
            return keyName.Replace("Alpha", "");
        
        if (keyName.StartsWith("Keypad"))
            return keyName.Replace("Keypad", "Num");
        
        return keyName;
    }
    
    private string GetGamepadButtonName(int buttonIndex)
    {
        // Common gamepad button names
        return buttonIndex switch
        {
            0 => "A",
            1 => "B",
            2 => "X",
            3 => "Y",
            4 => "LB",
            5 => "RB",
            6 => "Back",
            7 => "Start",
            8 => "Left Stick",
            9 => "Right Stick",
            _ => $"Button {buttonIndex}"
        };
    }
    
    private string GetMouseButtonName(int button)
    {
        return button switch
        {
            0 => "Left Click",
            1 => "Right Click",
            2 => "Middle Click",
            _ => $"Button {button}"
        };
    }
    
    private void AddInputEvent(string type, string description)
    {
        // Check if this input type is allowed
        if (_allowedInput == AllowedInput.MKOnly && type == "Gamepad")
            return;
        
        if (_allowedInput == AllowedInput.PadOnly && (type == "Mouse" || type == "Keyboard"))
            return;
        
        // Avoid duplicate events that are too close together
        if (_lastInputs.Count > 0)
        {
            var lastEvent = _lastInputs.Last();
            if (lastEvent != null && 
                lastEvent.Type == type && 
                lastEvent.Description == description &&
                (DateTime.Now - lastEvent.Timestamp).TotalSeconds < 0.2f)
            {
                return; // Skip if same event within 200ms
            }
        }
        
        InputEvent inputEvent = new InputEvent(type, description);
        
        _lastInputs.Enqueue(inputEvent);
        
        // Keep only last 3
        while (_lastInputs.Count > 3)
        {
            _lastInputs.Dequeue();
        }
    }
    
    private string GetTimeAgoString(DateTime timestamp)
    {
        TimeSpan elapsed = DateTime.Now - timestamp;
        
        if (elapsed.TotalSeconds < 1)
            return "now";
        
        if (elapsed.TotalSeconds < 60)
            return $"{(int)elapsed.TotalSeconds}s ago";
        
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        
        return $"{(int)elapsed.TotalHours}h ago";
    }
}

