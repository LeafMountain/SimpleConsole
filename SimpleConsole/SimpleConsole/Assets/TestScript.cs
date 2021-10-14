using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    public string testCommand;

    [ConsoleCommand]
    public static void PrintToLog(string text = "HELLO")
    {
        Debug.Log(text);
    }

    [ContextMenu("Start")]
    private void Start()
    {
        List<object> parameters = new List<object>();
        foreach (string parameter in testCommand.Split(' '))
        {
            parameters.Add(parameter);
        }
        var commands = GetCommands();
        var matchingCommand = commands.Find(x => x.name == (string)parameters[0]);
        if (matchingCommand != null)
        {
            parameters.RemoveAt(0);
            matchingCommand.methodInfo.Invoke(null, parameters.ToArray());
        }
    }

    public static List<ConsoleCommandHolder> GetCommands()
    {
        Assembly assembly = Assembly.GetAssembly(typeof(TestScript));
        Dictionary<string, MethodInfo> methods = assembly
            .GetTypes()
            .SelectMany(x => x.GetMethods())
            .Where(y => y.GetCustomAttributes().OfType<ConsoleCommandAttribute>().Any())
            .ToDictionary(z => z.Name);

        List<ConsoleCommandHolder> commands = new List<ConsoleCommandHolder>();

        foreach (var method in methods)
        {
            var newCommand = new ConsoleCommandHolder();
            // newCommand.name = method.Key;
            
            // Debug.Log(method.Key);
            var parameters = method.Value.GetParameters();
            if (parameters.Any())
            {
                foreach (ParameterInfo parameterInfo in parameters)
                {
                    newCommand.parameters.Add((parameterInfo.ParameterType, parameterInfo.DefaultValue));
                }
            }

            newCommand.methodInfo = method.Value;
            commands.Add(newCommand);
        }

        return commands;

        // foreach (var command in commands)
        // {
        //     // Debug.Log($"{command.name} ({command.parameters[0].type} {command.parameters[0].value})");
        //     command.methodInfo.Invoke(null, command.parameters.Select(x => x.value).ToArray());
        // }
    }
}

public class ConsoleCommandHolder
{
    public MethodInfo methodInfo;
    public string name => methodInfo.Name;
    public List<(Type type, object value)> parameters = new List<(Type type, object value)>();
}