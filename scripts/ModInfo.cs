using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModInfo
{
    public bool isEnabled = true;
    public bool isHumanoid = false;
    public bool autoSelectSource = true;
    public int? genericSourceObjectID;
    public Dictionary<int, int> humanoidRig;
}
