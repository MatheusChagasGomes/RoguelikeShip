using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;

/// <summary>
/// Keeps Device Simulator upright (0°) and Screen in Portrait so Simulator matches Game.
/// Device switches reset the simulator chrome rotation; without this, content appears upside-down.
/// </summary>
[InitializeOnLoad]
static class DeviceSimulatorPortraitGuard
{
    const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    const int PortraitDeviceRotationDegrees = 0;
    const double AlignIntervalSeconds = 0.15d;

    static readonly Type SimulatorWindowType;
    static readonly FieldInfo MainField;
    static readonly FieldInfo UserInterfaceField;
    static readonly PropertyInfo UserInterfaceProperty;
    static PropertyInfo _rotationProperty;

    static double _nextAlignTime;
    static bool _loggedReflectionFailure;

    static DeviceSimulatorPortraitGuard()
    {
        try
        {
            Assembly assembly = typeof(DeviceSimulator).Assembly;
            SimulatorWindowType = assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
            Type mainType = assembly.GetType("UnityEditor.DeviceSimulation.DeviceSimulatorMain");

            if (SimulatorWindowType != null)
            {
                MainField = SimulatorWindowType.GetField("m_Main", InstanceFlags);
            }

            if (mainType != null)
            {
                UserInterfaceField = mainType.GetField("m_UserInterface", InstanceFlags);
                UserInterfaceProperty = mainType.GetProperty("userInterface", InstanceFlags);
            }
        }
        catch (Exception)
        {
            // Editor internals can change between Unity versions.
        }

        EditorApplication.delayCall += AlignSimulatorOrientation;
        EditorApplication.playModeStateChanged += _ =>
            EditorApplication.delayCall += AlignSimulatorOrientation;
        EditorApplication.update += Tick;
    }

    [MenuItem("RoguelikeShip/Align Device Simulator To Portrait")]
    static void AlignSimulatorOrientationMenu()
    {
        AlignSimulatorOrientation();
        Debug.Log("Device Simulator forced to Portrait (0°).");
    }

    /// <summary>Called by PortraitLockSimulatorPlugin when the simulated device changes.</summary>
    internal static void AlignNow()
    {
        _nextAlignTime = 0d;
        AlignSimulatorOrientation();
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextAlignTime)
        {
            return;
        }

        _nextAlignTime = now + AlignIntervalSeconds;
        AlignSimulatorOrientation();
    }

    static void AlignSimulatorOrientation()
    {
        Screen.orientation = ScreenOrientation.Portrait;

        if (SimulatorWindowType == null || MainField == null)
        {
            return;
        }

        UnityEngine.Object[] windows;
        try
        {
            windows = Resources.FindObjectsOfTypeAll(SimulatorWindowType);
        }
        catch (Exception)
        {
            return;
        }

        foreach (UnityEngine.Object windowObject in windows)
        {
            if (windowObject == null)
            {
                continue;
            }

            try
            {
                object main = MainField.GetValue(windowObject);
                if (main == null)
                {
                    continue;
                }

                object userInterface = UserInterfaceField?.GetValue(main)
                    ?? UserInterfaceProperty?.GetValue(main);
                if (userInterface == null)
                {
                    continue;
                }

                PropertyInfo rotationProperty = GetRotationProperty(userInterface.GetType());
                if (rotationProperty == null || !rotationProperty.CanWrite)
                {
                    continue;
                }

                object current = rotationProperty.GetValue(userInterface);
                int currentDegrees = ConvertToDegrees(current);
                if (currentDegrees == PortraitDeviceRotationDegrees)
                {
                    continue;
                }

                object valueToSet = Convert.ChangeType(
                    PortraitDeviceRotationDegrees,
                    rotationProperty.PropertyType);
                rotationProperty.SetValue(userInterface, valueToSet);

                if (windowObject is EditorWindow editorWindow)
                {
                    editorWindow.Repaint();
                }
            }
            catch (Exception ex)
            {
                if (!_loggedReflectionFailure)
                {
                    _loggedReflectionFailure = true;
                    Debug.LogWarning(
                        "DeviceSimulatorPortraitGuard could not reset Simulator rotation: "
                        + ex.Message);
                }
            }
        }
    }

    static PropertyInfo GetRotationProperty(Type userInterfaceType)
    {
        if (_rotationProperty != null && _rotationProperty.DeclaringType == userInterfaceType)
        {
            return _rotationProperty;
        }

        _rotationProperty = userInterfaceType.GetProperty("Rotation", InstanceFlags)
            ?? userInterfaceType.GetProperty("DeviceRotation", InstanceFlags);
        return _rotationProperty;
    }

    static int ConvertToDegrees(object value)
    {
        if (value == null)
        {
            return PortraitDeviceRotationDegrees;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return int.MinValue;
        }
    }
}
