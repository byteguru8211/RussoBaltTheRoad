using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RussoBalt/Track", fileName = "Track")]
public class TrackData : ScriptableObject
{
    public string id;
    public string displayName;
    public string chapter;
    public string description;
    public Vector3[] waypoints;
    public float roadWidth = 9f;
    public int laps = 1;
    public int aiCount = 4;
    public float surfaceGrip = 0.85f;
    public float timeLimit = 300f;
    public int reward = 500;
    public Color roadColor = new Color(0.34f, 0.25f, 0.15f);
    public Color groundColor = new Color(0.20f, 0.27f, 0.16f);
    public Color curbColor = new Color(0.72f, 0.68f, 0.55f);
    public string storyTitle;
    [TextArea] public string storyText;
    public string previewResource = "UI/Hero";
    public string scenery = "forest";
}
