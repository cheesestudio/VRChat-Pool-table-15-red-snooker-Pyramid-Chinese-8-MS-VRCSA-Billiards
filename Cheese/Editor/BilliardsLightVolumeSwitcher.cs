using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BilliardsLightVolumeSwitcher
{
    public enum MaterialState
    {
        None,
        Standard,
        LightVolumes,
        Mixed,
    }

    public readonly struct Snapshot
    {
        public Snapshot(bool packageInstalled, int standardCount, int lightVolumeCount)
        {
            PackageInstalled = packageInstalled;
            StandardCount = standardCount;
            LightVolumeCount = lightVolumeCount;
        }

        public bool PackageInstalled { get; }
        public int StandardCount { get; }
        public int LightVolumeCount { get; }
        public int TotalCount => StandardCount + LightVolumeCount;

        public MaterialState State
        {
            get
            {
                if (StandardCount == 0 && LightVolumeCount == 0) return MaterialState.None;
                if (StandardCount > 0 && LightVolumeCount > 0) return MaterialState.Mixed;
                return LightVolumeCount > 0 ? MaterialState.LightVolumes : MaterialState.Standard;
            }
        }
    }

    private const string SearchRoot =
        "Assets/VRChat-Pool-table-15-red-snooker-Pyramid-Chinese-8-MS-VRCSA-Billiards/Modules/BilliardsModule";

    private const string LightVolumesIncludePath =
        "Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc";

    private const string StandardShaderName = "Standard";
    private const string LightVolumeStandardShaderName = "cheese/VRC LV Standard";
    private const string LegacyLightVolumeStandardShaderName = "metaphira/VRC LV Standard";

    private static readonly string[] UiPathMarkers =
    {
        "/Buttons/",
        "/Leaderboard/",
        "/ResetButton",
    };

    private static readonly IReadOnlyDictionary<string, string> StandardToLightVolume =
        new Dictionary<string, string>
        {
            { "metaphira/TableSurface", "cheese/TableSurface VRCLV" },
            { "metaphira/TableSurface (Glass)", "cheese/TableSurface Glass VRCLV" },
            { "metaphira/TableSurface (Quest)", "cheese/TableSurface Quest VRCLV" },
        };

    private static readonly IReadOnlyDictionary<string, string> LightVolumeToStandard =
        StandardToLightVolume.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static bool IsPackageInstalled =>
        AssetDatabase.LoadMainAssetAtPath(LightVolumesIncludePath) != null;

    public static Snapshot GetSnapshot()
    {
        int standardCount = 0;
        int lightVolumeCount = 0;

        foreach (Material material in FindAllMaterials())
        {
            string shaderName = GetShaderName(material);
            if (IsStandardCandidate(material, shaderName) || StandardToLightVolume.ContainsKey(shaderName))
                standardCount++;
            else if (IsLightVolumeShader(shaderName))
                lightVolumeCount++;
        }

        return new Snapshot(IsPackageInstalled, standardCount, lightVolumeCount);
    }

    public static void DrawInspectorGUI()
    {
        Snapshot snapshot = GetSnapshot();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VRC Light Volumes", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Package", snapshot.PackageInstalled ? "Installed" : "Not installed");
        EditorGUILayout.LabelField("Materials", GetStateLabel(snapshot));
        EditorGUILayout.LabelField("Affected", snapshot.TotalCount.ToString());

        if (!snapshot.PackageInstalled)
        {
            EditorGUILayout.HelpBox(
                "VRC Light Volumes is not installed. You can restore the standalone shaders, but cannot enable VRCLV.",
                MessageType.Info);
        }

        switch (snapshot.State)
        {
            case MaterialState.LightVolumes:
                if (GUILayout.Button("Use Standard Lighting"))
                    SetLightVolumesEnabled(false, true);
                break;

            case MaterialState.Mixed:
                EditorGUILayout.HelpBox("The billiards materials currently use a mixture of standard and VRCLV shaders.", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Use Standard Lighting"))
                    SetLightVolumesEnabled(false, true);
                using (new EditorGUI.DisabledScope(!snapshot.PackageInstalled))
                {
                    if (GUILayout.Button("Use VRC Light Volumes"))
                        SetLightVolumesEnabled(true, true);
                }
                EditorGUILayout.EndHorizontal();
                break;

            default:
                using (new EditorGUI.DisabledScope(!snapshot.PackageInstalled || snapshot.TotalCount == 0))
                {
                    if (GUILayout.Button("Use VRC Light Volumes"))
                        SetLightVolumesEnabled(true, true);
                }
                break;
        }
    }

    public static bool SetLightVolumesEnabled(bool enabled, bool confirm)
    {
        if (enabled && !IsPackageInstalled)
        {
            EditorUtility.DisplayDialog(
                "VRC Light Volumes",
                "Install VRC Light Volumes before enabling its shaders.",
                "OK");
            return false;
        }

        if (!TryLoadDestinationShaders(enabled, out Dictionary<string, Shader> destinations, out string error))
        {
            Debug.LogError($"[Billiards VRCLV] {error}");
            EditorUtility.DisplayDialog("VRC Light Volumes", error, "OK");
            return false;
        }

        List<MaterialChange> changes = BuildChanges(enabled, destinations);
        if (changes.Count == 0)
        {
            if (confirm)
                EditorUtility.DisplayDialog("VRC Light Volumes", "No billiards materials need to be changed.", "OK");
            return true;
        }

        if (confirm && !EditorUtility.DisplayDialog(
                "VRC Light Volumes",
                $"Switch {changes.Count} shared billiards materials to {(enabled ? "VRCLV" : "standard lighting")}?\n\n" +
                "This affects every BilliardsModule instance in the project.",
                "Switch",
                "Cancel"))
        {
            return false;
        }

        Material[] materials = changes.Select(change => change.Material).ToArray();
        Undo.RecordObjects(materials, enabled ? "Enable Billiards VRCLV" : "Disable Billiards VRCLV");

        foreach (MaterialChange change in changes)
        {
            change.Material.shader = change.Destination;
            EditorUtility.SetDirty(change.Material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Billiards VRCLV] Switched {changes.Count} materials to " +
                  $"{(enabled ? "VRC Light Volumes" : "standard lighting")}.");
        return true;
    }

    private static List<MaterialChange> BuildChanges(bool enabled, IReadOnlyDictionary<string, Shader> destinations)
    {
        var changes = new List<MaterialChange>();

        foreach (Material material in FindAllMaterials())
        {
            string sourceName = GetShaderName(material);
            string destinationName = GetDestinationName(material, sourceName, enabled);
            if (destinationName == null || !destinations.TryGetValue(destinationName, out Shader destination))
                continue;
            if (material.shader == destination)
                continue;

            changes.Add(new MaterialChange(material, destination));
        }

        return changes;
    }

    private static string GetDestinationName(Material material, string sourceName, bool enabled)
    {
        if (enabled)
        {
            if (IsStandardCandidate(material, sourceName)) return LightVolumeStandardShaderName;
            return StandardToLightVolume.TryGetValue(sourceName, out string destination) ? destination : null;
        }

        if (sourceName == LightVolumeStandardShaderName || sourceName == LegacyLightVolumeStandardShaderName)
            return StandardShaderName;
        return LightVolumeToStandard.TryGetValue(sourceName, out string standardDestination)
            ? standardDestination
            : null;
    }

    private static bool TryLoadDestinationShaders(
        bool enabled,
        out Dictionary<string, Shader> destinations,
        out string error)
    {
        destinations = new Dictionary<string, Shader>();
        IEnumerable<string> names = enabled
            ? StandardToLightVolume.Values.Append(LightVolumeStandardShaderName)
            : StandardToLightVolume.Keys.Append(StandardShaderName);

        foreach (string shaderName in names)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                error = $"Required shader '{shaderName}' was not found. No materials were changed.";
                return false;
            }
            destinations[shaderName] = shader;
        }

        error = null;
        return true;
    }

    private static IEnumerable<Material> FindAllMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { SearchRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                yield return material;
        }
    }

    private static bool IsStandardCandidate(Material material, string shaderName)
    {
        if (shaderName != StandardShaderName || !IsOpaque(material)) return false;
        string path = AssetDatabase.GetAssetPath(material).Replace('\\', '/');
        return !UiPathMarkers.Any(marker => path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsOpaque(Material material) =>
        !material.HasProperty("_Mode") || Mathf.Approximately(material.GetFloat("_Mode"), 0f);

    private static bool IsLightVolumeShader(string shaderName) =>
        shaderName == LightVolumeStandardShaderName ||
        shaderName == LegacyLightVolumeStandardShaderName ||
        LightVolumeToStandard.ContainsKey(shaderName);

    private static string GetShaderName(Material material) =>
        material != null && material.shader != null ? material.shader.name : string.Empty;

    private static string GetStateLabel(Snapshot snapshot)
    {
        switch (snapshot.State)
        {
            case MaterialState.Standard: return $"Standard ({snapshot.StandardCount})";
            case MaterialState.LightVolumes: return $"VRCLV ({snapshot.LightVolumeCount})";
            case MaterialState.Mixed:
                return $"Mixed ({snapshot.StandardCount} standard / {snapshot.LightVolumeCount} VRCLV)";
            default: return "No matching materials";
        }
    }

    private readonly struct MaterialChange
    {
        public MaterialChange(Material material, Shader destination)
        {
            Material = material;
            Destination = destination;
        }

        public Material Material { get; }
        public Shader Destination { get; }
    }
}
