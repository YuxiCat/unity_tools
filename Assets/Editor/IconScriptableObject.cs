using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
[System.Serializable] 
public class IconScriptableObject : ScriptableObject {
    
    public string exportpath;
    public int imageWidth = 512;
	public int imageHeight = 512;
    public float lightRotation;
    public int lightingMode;
    public int imageFormat;
    public List<PrefabSettings> settings = new List<PrefabSettings>();
}
