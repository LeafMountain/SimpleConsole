using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript2 : MonoBehaviour
{
    [ConsoleCommand]
    public static void TestaTestaHello(int myValue = 1337)
    {
        Debug.Log("YO");
    }
}
