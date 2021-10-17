using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
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

        public bool showConsole;
        public int maxHistoryCount = 20;
        public List<string> history = new List<string>();
        public GameObject uiParent;
        public InputField inputField;
        public Text autoCompleteField;

        private MethodInfo[] commands;
        private int currentIndex => inputField.text.Count(x => x == ' ');
        private string[] parameters => inputField.text.Split(' ');

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
                if (autoCompleteCommand.Any())
                {
                    if (currentIndex > 0)
                    {
                        var options = GetOptions(autoCompleteCommand[0], currentIndex);
                        if (options.Count > 0)
                            inputField.text = $"{autoCompleteCommand[0].Name} {options[0]}";
                    }
                    else
                    {
                        inputField.text = autoCompleteCommand[0].Name;
                    }

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
                if (autoCompleteCommand.Any())
                {
                    inputField.text = autoCompleteCommand[0].Name;
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
                    ParameterInfo[] parameterInfos = command.GetParameters();
                    if (currentIndex == 0)
                    {
                        autoCompleteString += $"{command.Name}";
                        for (int i = 0; i < parameterInfos.Length; i++)
                        {
                            autoCompleteString += $" {parameterInfos[i].Name.ToUpper()}";
                        }
                    }
                    else
                    {
                        List<string> options = GetOptions(command, currentIndex);
                        if (options.Count > 0)
                        {
                            foreach (string option in options)
                            {
                                autoCompleteString += $"{command.Name} {option}";
                                for (int k = currentIndex; k < parameterInfos.Length; k++)
                                    autoCompleteString += $" {parameterInfos[k].Name.ToUpper()}";
                                autoCompleteString += "\n";
                            }
                        }
                        else
                        {
                            autoCompleteString += $"{command.Name}";
                            for (int i = 0; i < parameterInfos.Length; i++)
                            {
                                autoCompleteString += $" {parameterInfos[i].Name.ToUpper()}";
                            }
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

        public List<string> GetOptions(MethodInfo methodInfo, int index)
        {
            if (currentIndex > methodInfo.GetParameters().Length - 1) return new List<string>();
            Attribute[] attributes = methodInfo.GetCustomAttributes().ToArray();
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i] is ConsoleCommandAttribute consoleCommandAttribute)
                {
                    if (consoleCommandAttribute.autoCompleteOptions.Length < currentIndex - 1 && consoleCommandAttribute.autoCompleteOptions[currentIndex - 1] == null)
                        return new List<string> { methodInfo.GetParameters()[currentIndex].Name };
                    string parameterText = parameters.Length < currentIndex ? string.Empty : parameters[currentIndex];
                    if (parameterText == string.Empty)
                        return consoleCommandAttribute.autoCompleteOptions[currentIndex - 1].ToList();

                    return consoleCommandAttribute.autoCompleteOptions[currentIndex - 1]?.Where(x => x.StartsWith(parameterText, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return new List<string>();
        }

        public void Test(string text)
        {
            Debug.Log(text);
        }
    }
}