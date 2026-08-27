using UnityEditor;
using UnityEditor.DeviceSimulation;

/// <summary>
/// Fires as soon as the simulated device changes so Portrait lock re-applies immediately.
/// </summary>
public sealed class PortraitLockSimulatorPlugin : DeviceSimulatorPlugin
{
    public override string title => "Portrait Lock";

    public override void OnCreate()
    {
        deviceSimulator.deviceChanged += OnDeviceChanged;
        OnDeviceChanged();
    }

    public override void OnDestroy()
    {
        if (deviceSimulator != null)
        {
            deviceSimulator.deviceChanged -= OnDeviceChanged;
        }
    }

    static void OnDeviceChanged()
    {
        // Device definitions may apply their own rotation after this callback.
        DeviceSimulatorPortraitGuard.AlignNow();
        EditorApplication.delayCall += DeviceSimulatorPortraitGuard.AlignNow;
    }
}
