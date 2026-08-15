#if UNITY_EDITOR
using UnityEditor;

public static class VRC_LV_MaterialReplacer
{
    [MenuItem("Tools/VRC LV/Enable For Billiards Materials")]
    private static void Enable()
    {
        BilliardsLightVolumeSwitcher.SetLightVolumesEnabled(true, true);
    }

    [MenuItem("Tools/VRC LV/Use Standard Billiards Materials")]
    private static void Disable()
    {
        BilliardsLightVolumeSwitcher.SetLightVolumesEnabled(false, true);
    }
}
#endif
