using System;
using UnityEngine;

/// <summary>
/// Runtime description of one procedurally generated scenario stretch.
/// </summary>
[Serializable]
public class ScenarioDefinition
{
    public string displayName = "Scenario";

    [Tooltip("Planetary uses plateaus/craters. Space uses stars.")]
    public ScenarioKind kind = ScenarioKind.Planetary;

    public Color color = Color.white;

    public bool useGradient;
    [Tooltip("Left side when useGradient is enabled. Right side uses color.")]
    public Color gradientColor = Color.white;

    [Min(0.1f)]
    [Tooltip("Seconds this scenario takes to scroll fully past the camera.")]
    public float durationSeconds = 8f;

    [Tooltip("RNG seed used to build this chunk's decorations.")]
    public int seed;
}
