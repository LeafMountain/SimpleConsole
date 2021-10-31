using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using Assembly = System.Reflection.Assembly;

namespace SimpleConsole
{
    public class SimpleConsole : MonoBehaviour
    {
        private static SimpleConsole instance;
        public static SimpleConsole Instance => instance ??= instance = FindObjectOfType<SimpleConsole>();
        public event Action<bool> OnToggleConsole;

        public bool showConsole;
        public int maxHistoryCount = 20;
        public List<string> history = new List<string>();
        public GameObject uiParent;
        public InputField inputField;
        public Text autoCompleteField;

        private MethodInfo[] commands;
        private int currentIndex => inputField.text.Count(x => x == ' ');
        private string[] parameters => inputField.text.Split(' ');

        private string[][] options = new string[10][];

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void Start()
        {
            CacheCommands();
            uiParent.SetActive(showConsole);
            inputField.onValueChanged.AddListener(PopulateAutoCompleteField);
            PopulateAutoCompleteField(string.Empty);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.f1Key.wasReleasedThisFrame)
                ToggleUI();

            if (showConsole == false) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleUI();
            else if (Keyboard.current.enterKey.wasReleasedThisFrame)
            {
                TryExecuteCommand(inputField.text);
                ToggleUI();
            }
            else if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                var autoCompleteCommand = TryAutoComplete(inputField.text);
                string autoCompleteString = AutoComplete(parameters, options);
                if (autoCompleteCommand.Any())
                {
                    inputField.text = autoCompleteString;
                    if (autoCompleteCommand[0].GetParameters().Length > 1)
                        inputField.text += " ";

                    inputField.caretPosition = inputField.text.Length;
                }
            }
#else
            if (Input.GetKeyUp(KeyCode.F1))
                ToggleUI();

            if (showConsole == false) return;

            if (Input.GetKey(KeyCode.Escape))
            {
                ToggleUI();
            }
            else if (Input.GetKeyUp(KeyCode.Return))
            {
                ToggleUI();
                TryExecuteCommand(inputField.text);
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                var autoCompleteCommand = TryAutoComplete(inputField.text);
                string autoCompleteString = AutoComplete(parameters, options);
                if (autoCompleteCommand.Any())
                {
                    inputField.text = autoCompleteString;
                    if (autoCompleteCommand[0].GetParameters().Length > 1)
                        inputField.text += " ";

                    inputField.caretPosition = inputField.text.Length;
               }
            }
#endif
        }

        private void PopulateAutoCompleteField(string text)
        {
            List<MethodInfo> autoCompleteCommands = TryAutoComplete(inputField.text);
            string autoCompleteString = string.Empty;

            if (autoCompleteCommands.Count > 0)
            {
                foreach (MethodInfo command in autoCompleteCommands)
                {
                    if (currentIndex == 0) options[currentIndex] = autoCompleteCommands.Select(x => x.Name).ToArray();
                    else
                    {
                        var consoleCommandAttribute = (ConsoleCommandAttribute)Array.Find(command.GetCustomAttributes().ToArray(), x => x is ConsoleCommandAttribute);
                        if (consoleCommandAttribute.autoCompleteOptions.Length > currentIndex - 1)
                            options[currentIndex] = consoleCommandAttribute.autoCompleteOptions[currentIndex - 1];
                    }

                    // Show structure of command
                    var parameterNames = command.GetParameters().Select(x => x.Name).ToArray();
                    autoCompleteString += $"{command.Name}";
                    for (int i = 0; i < parameterNames.Length; i++)
                    {
                        autoCompleteString += $" {parameterNames[i].ToUpper()} ";
                    }


                    if (currentIndex > 0)
                    {
                        autoCompleteString += "\n\n";
                        var possibleOptions = GetMatchingStrings(parameters[currentIndex], options[currentIndex]);
                        if (possibleOptions != null)
                            for (int i = 0; i < possibleOptions.Length; i++)
                            {
                                autoCompleteString += $"{possibleOptions[i]}\n";
                            }
                    }

                    autoCompleteString += "\n";
                }
            }

            autoCompleteField.text = autoCompleteString.Trim();
        }

        private List<MethodInfo> TryAutoComplete(string text)
        {
            List<MethodInfo> autoCompleteCommands = new List<MethodInfo>();
            string commandName = parameters[0];
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i].Name.StartsWith(commandName, StringComparison.OrdinalIgnoreCase))
                    autoCompleteCommands.Add(commands[i]);
            }

            return autoCompleteCommands;
        }

        private void ToggleUI()
        {
            showConsole = !showConsole;
            uiParent.SetActive(showConsole);
            if (showConsole)
            {
                inputField.ActivateInputField();
                inputField.Select();
            }
            else inputField.DeactivateInputField();

            Instance.OnToggleConsole?.Invoke(showConsole);
        }

        [ContextMenu("Cache Commands")]
        private void CacheCommands()
        {
            commands = GetCommands();
        }

        private void TryExecuteCommand(string input)
        {
            input = input.Trim();
            List<object> parameters = new List<object>();
            foreach (string parameter in input.Split(' '))
                parameters.Add(parameter);
            var matchingCommand = Array.Find(commands, x => x.Name.Equals((string)parameters[0], StringComparison.OrdinalIgnoreCase));
            if (matchingCommand != null)
            {
                parameters.RemoveAt(0);
                ParameterInfo[] parameterInfos = matchingCommand.GetParameters();
                if (parameters.Count < parameterInfos.Length)
                {
                    for (int i = parameters.Count; i < parameterInfos.Length; i++)
                        parameters.Add(parameterInfos[i].DefaultValue);
                }

                matchingCommand.Invoke(this, parameters.ToArray());
                if (history.Contains(input)) history.Remove(input);
                history.Insert(0, input);
                if (history.Count > maxHistoryCount)
                {
                    history.RemoveAt(history.Count - 1);
                }
            }
        }

        public static MethodInfo[] GetCommands()
        {
            string rootPath = Application.dataPath;
            rootPath = rootPath.Substring(0, rootPath.Length - "Assets".Length);
            Assembly assembly = Assembly.LoadFile(rootPath + "Library/ScriptAssemblies/Assembly-CSharp.dll");
            Dictionary<string, MethodInfo> methods = assembly
                .GetTypes()
                .SelectMany(x => x.GetMethods())
                .Where(y => y.GetCustomAttributes().OfType<ConsoleCommandAttribute>().Any())
                .ToDictionary(z => z.Name);
            return methods.Values.ToArray();
        }

        public static string[] GetOptions(MethodInfo methodInfo, int currentIndex, string parameterText)
        {
            if (currentIndex > methodInfo.GetParameters().Length) return null;
            Attribute[] attributes = methodInfo.GetCustomAttributes().ToArray();
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i] is ConsoleCommandAttribute consoleCommandAttribute)
                {
                    if (consoleCommandAttribute.autoCompleteOptions.Length < currentIndex - 1 && consoleCommandAttribute.autoCompleteOptions[currentIndex - 1] == null)
                        return new[] { methodInfo.GetParameters()[currentIndex].Name };
                    return GetMatchingStrings(parameterText, consoleCommandAttribute.autoCompleteOptions[currentIndex - 1]);
                }
            }

            return null;
        }

        public static string[] GetMatchingStrings(string text, IEnumerable<string> options)
        {
            if (options == null) return null;
            if (text == string.Empty) return options.ToArray();
            return options.Where(x => x.StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        public void Test(string text)
        {
            //Debug.Log(text);
        }

        public string AutoComplete(string[] parameters, string[][] options)
        {
            string completed = string.Empty;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (options[i] == null) continue;

                string[] matchingStrings = GetMatchingStrings(parameters[i], options[i]);
                if (matchingStrings != null && matchingStrings.Length > 0)
                    completed += $"{matchingStrings[0]} ";
                else
                    completed += $"{parameters[i]} ";
            }

            return completed.Trim();
        }
    }
}
