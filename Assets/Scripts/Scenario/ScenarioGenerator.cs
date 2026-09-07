using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure procedural factory for scenario definitions (kind, colors, seeds).
/// Keeps generation policy out of the path/scroll runner.
/// </summary>
public sealed class ScenarioGenerator
{
    readonly System.Random _rng;
    readonly float _durationSeconds;
    readonly int _loopIndex;

    public ScenarioGenerator(System.Random rng, float durationSeconds, int loopIndex)
    {
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _durationSeconds = Mathf.Max(0.1f, durationSeconds);
        _loopIndex = Mathf.Max(0, loopIndex);
    }

    public List<ScenarioDefinition> GenerateLoop(int count)
    {
        count = Mathf.Max(1, count);
        var list = new List<ScenarioDefinition>(count);

        // Randomize which kind starts this loop, then keep alternating.
        ScenarioKind kind = _rng.Next(0, 2) == 0 ? ScenarioKind.Planetary : ScenarioKind.Space;

        for (int i = 0; i < count; i++)
        {
            list.Add(CreateScenario(kind, i));
            kind = kind == ScenarioKind.Planetary ? ScenarioKind.Space : ScenarioKind.Planetary;
        }

        return list;
    }

    ScenarioDefinition CreateScenario(ScenarioKind kind, int indexInLoop)
    {
        bool useGradient = _rng.NextDouble() < 0.62;
        Color primary = RollBackgroundColor(kind);
        Color secondary = RollBackgroundColor(kind);

        if (useGradient)
        {
            int guard = 0;
            while (ColorsTooClose(primary, secondary) && guard++ < 8)
            {
                secondary = RollBackgroundColor(kind);
            }

            if (ColorsTooClose(primary, secondary))
            {
                Color.RGBToHSV(primary, out float h, out float s, out float v);
                secondary = Color.HSVToRGB(
                    (h + 0.18f + (float)_rng.NextDouble() * 0.25f) % 1f,
                    Mathf.Clamp01(s * 0.85f),
                    Mathf.Clamp01(kind == ScenarioKind.Space ? v * 0.55f : v * 1.15f));
            }
        }

        return new ScenarioDefinition
        {
            displayName = kind == ScenarioKind.Planetary
                ? $"Planeta {_loopIndex}-{indexInLoop}"
                : $"Espaco {_loopIndex}-{indexInLoop}",
            kind = kind,
            color = primary,
            useGradient = useGradient,
            gradientColor = secondary,
            durationSeconds = _durationSeconds,
            seed = _rng.Next(1, int.MaxValue),
        };
    }

    Color RollBackgroundColor(ScenarioKind kind)
    {
        float hue = (float)_rng.NextDouble();

        if (kind == ScenarioKind.Planetary)
        {
            float sat = Lerp(0.45f, 1f);
            float val = Lerp(0.42f, 0.95f);

            double style = _rng.NextDouble();
            if (style < 0.18)
            {
                sat = Lerp(0.15f, 0.4f);
                val = Lerp(0.75f, 1f);
            }
            else if (style < 0.36)
            {
                sat = Lerp(0.85f, 1f);
                val = Lerp(0.55f, 1f);
            }

            return Color.HSVToRGB(hue, sat, val);
        }

        float spaceSat = Lerp(0.35f, 1f);
        float spaceVal = Lerp(0.04f, 0.28f);

        double spaceStyle = _rng.NextDouble();
        if (spaceStyle < 0.2)
        {
            spaceSat = Lerp(0.2f, 0.7f);
            spaceVal = Lerp(0.02f, 0.1f);
        }
        else if (spaceStyle < 0.4)
        {
            spaceSat = Lerp(0.7f, 1f);
            spaceVal = Lerp(0.16f, 0.38f);
        }

        return Color.HSVToRGB(hue, spaceSat, spaceVal);
    }

    float Lerp(float a, float b)
    {
        return a + (float)_rng.NextDouble() * (b - a);
    }

    static bool ColorsTooClose(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return (dr * dr + dg * dg + db * db) < 0.045f;
    }
}
