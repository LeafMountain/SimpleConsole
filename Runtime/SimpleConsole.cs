using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor.Compilation;
using UnityEngine;
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
        public TMP_InputField inputField;
        public TMP_Text autoCompleteField;

        private MethodInfo[] commands;

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void Start()
        {
            CacheCommands();
        }

        private void Update()
        {
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

                    inputField.stringPosition = inputField.text.Length;
                }
            }
            else if (Input.anyKeyDown)
            {
                PopulateAutoCompleteField();
            }
        }

        private void PopulateAutoCompleteField()
        {
            var autoCompleteCommand = TryAutoComplete(inputField.text);
            string autoCompleteString = string.Empty;
            if (autoCompleteCommand.Any())
            {
                foreach (MethodInfo consoleCommandHolder in autoCompleteCommand)
                {
                    autoCompleteString += $"{consoleCommandHolder.Name}";
                    ParameterInfo[] parameterInfos = consoleCommandHolder.GetParameters();
                    for (int i = 0; i < parameterInfos.Length; i++)
                    {
                        autoCompleteString += $" {parameterInfos[i].Name.ToUpper()}";
                        if (parameterInfos[i].HasDefaultValue)
                            autoCompleteString += $"[{parameterInfos[i].DefaultValue as string}]";
                    }

                    autoCompleteString += "\n";
                }
            }

            autoCompleteField.text = autoCompleteString;
        }

        private List<MethodInfo> TryAutoComplete(string inputFieldText)
        {
            List<MethodInfo> autoCompleteCommands = new List<MethodInfo>();
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i].Name.StartsWith(inputFieldText.Trim(), StringComparison.OrdinalIgnoreCase))
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
                PopulateAutoCompleteField();
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
            // UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies();
            // Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
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
    }
}