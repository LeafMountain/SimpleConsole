using System;
namespace SimpleConsole
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string[][] autoCompleteOptions;
        public ConsoleCommandAttribute() { }

        public ConsoleCommandAttribute(string[] autoCompleteOptions)
        {
            this.autoCompleteOptions = new string[1][];
            this.autoCompleteOptions[0] = autoCompleteOptions;
        }

        /// <summary>
        /// Use enum as options
        /// </summary>
        /// <param name="autoCompleteOptions"></param>
        public ConsoleCommandAttribute(Type autoCompleteOptions)
        {
            this.autoCompleteOptions = new string[1][];
            this.autoCompleteOptions[0] = Enum.GetNames(autoCompleteOptions);
        }
    }
}