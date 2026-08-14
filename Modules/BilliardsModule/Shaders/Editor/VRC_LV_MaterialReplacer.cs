#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Replaces built-in "Standard" materials in the billiards table with the
/// VRC Light Volumes compatible "metaphira/VRC LV Standard" shader.
/// </summary>
public static class VRC_LV_MaterialReplacer
{
    const string LVShaderName = "metaphira/VRC LV Standard";
    const string SearchRoot = "Assets/VRChat-Pool-table-15-red-snooker-Pyramid-Chinese-8-MS-VRCSA-Billiards";

    // Set to true to also replace UI / button / leaderboard Standard materials.
    // They are skipped by default because they are not physical table surfaces.
    const bool IncludeUI = false;

    static readonly string[] UIPathMarkers = { "Buttons", "Leaderboard", "ResetButton" };

    [MenuItem("Tools/VRC LV/List Standard Materials")]
    static void ListStandardMaterials()
    {
        var mats = FindStandardMaterials();
        Debug.Log($"[VRC LV] Found {mats.Count} built-in Standard materials to replace:");
        foreach (var m in mats)
            Debug.Log($"[VRC LV]   [{Category(m)}] {AssetDatabase.GetAssetPath(m)}");
    }

    [MenuItem("Tools/VRC LV/Replace Standard -> VRC LV Standard")]
    static void ReplaceStandardMaterials()
    {
        var lvShader = Shader.Find(LVShaderName);
        if (lvShader == null)
        {
            Debug.LogError($"[VRC LV] Shader '{LVShaderName}' not found. Import the shader first.");
            return;
        }

        var mats = FindStandardMaterials();
        if (mats.Count == 0)
        {
            Debug.Log("[VRC LV] Nothing to replace.");
            return;
        }

        // List everything before replacing.
        var sb = new StringBuilder();
        sb.AppendLine($"[VRC LV] Will replace {mats.Count} Standard materials:");
        foreach (var m in mats)
            sb.AppendLine($"  [{Category(m)}] {AssetDatabase.GetAssetPath(m)}");
        Debug.Log(sb.ToString());

        if (!EditorUtility.DisplayDialog(
            "VRC LV Material Replacer",
            $"Replace {mats.Count} Standard materials with '{LVShaderName}'?\n\nFull list printed to Console.",
            "Replace", "Cancel"))
        {
            return;
        }

        int replaced = 0;
        foreach (var m in mats)
        {
            Undo.RecordObject(m, "Replace Standard with VRC LV Standard");
            m.shader = lvShader;
            EditorUtility.SetDirty(m);
            replaced++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[VRC LV] Replaced {replaced} materials.");
    }

    static List<Material> FindStandardMaterials()
    {
        var result = new List<Material>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { SearchRoot });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null || mat.shader.name != "Standard")
                continue;
            if (!IsOpaque(mat))
                continue;
            if (!IncludeUI && IsUI(path))
                continue;
            result.Add(mat);
        }
        return result;
    }

    // Standard stores its transparency mode in _Mode (0 = Opaque).
    // Transparent/cutout materials are skipped to avoid breaking them.
    static bool IsOpaque(Material mat)
    {
        return !mat.HasProperty("_Mode") || mat.GetFloat("_Mode") == 0f;
    }

    static bool IsUI(string path)
    {
        foreach (var marker in UIPathMarkers)
            if (path.Contains(marker))
                return true;
        return false;
    }

    static string Category(Material mat)
    {
        return IsUI(AssetDatabase.GetAssetPath(mat)) ? "UI" : "实体";
    }
}
#endif
