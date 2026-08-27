using System;
using UnityEngine;

/// <summary>
/// One background stretch in the run path. Duration controls how long that
/// stretch takes to scroll past the camera at the path's scroll speed.
/// </summary>
[Serializable]
public class ScenarioDefinition
{
    public string displayName = "Scenario";
    public Color color = Color.white;

    [Min(0.1f)]
    [Tooltip("Seconds this scenario takes to scroll fully past the camera.")]
    public float durationSeconds = 8f;
}
