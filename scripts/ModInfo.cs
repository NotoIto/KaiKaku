using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModInfo
{
    public bool isEnabled;
    public bool isHumanoid;
    public bool autoSelectSource;
    public int? genericSourceObjectID;
    public Dictionary<int, int> humanoidRig;
}
