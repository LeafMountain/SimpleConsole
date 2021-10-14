using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Method)]
public class ConsoleCommandAttribute : System.Attribute
{
    // public ConsoleCommandAttribute(string customText, [CallerMemberName] string propName = null)
    // {
    //     text = customText;
    //     this.propName = propName;
    // }
}
