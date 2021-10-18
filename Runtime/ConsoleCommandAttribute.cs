using System;
using System.Linq;

namespace SimpleConsole
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string[][] autoCompleteOptions;
        public ConsoleCommandAttribute() { }

        public ConsoleCommandAttribute(params string[] autoCompleteOptions)
        {
            this.autoCompleteOptions = autoCompleteOptions.Select(x => x.Split(',')).ToArray();
            // this.autoCompleteOptions = autoCompleteOptions;
        }

        /// <summary>
        /// Use enum as options
        /// </summary>
        /// <param name="autoCompleteOptions"></param>
        public ConsoleCommandAttribute(params Type[] autoCompleteOptions)
        {
            this.autoCompleteOptions = new string[autoCompleteOptions.Length][];
            this.autoCompleteOptions = autoCompleteOptions.Select(Enum.GetNames).ToArray();
        }
    }
}