
using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[CustomEditor(typeof(BilliardsModule))]
public class UdonTableEditor : Editor
{
    private const string defineTxt = "UDON_CHIPS";
    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        EditorGUILayout.LabelField("UdonChips对应");
        if (CheckDefineSymbol(defineTxt))
        {
            if (GUILayout.Button("UdonChips無効化"))
            {
                RemoveDefineSymbols(defineTxt);
            }
        }
        else
        {
            if (GUILayout.Button("UdonChips有効化"))
            {
                AddDefineSymbols(defineTxt);
            }
        }
        base.OnInspectorGUI();

    }
    bool CheckDefineSymbol(string symbols)
    {
        return getDefineSymbols().Contains(symbols);
    }
    List<string> getDefineSymbols()
    {
        return new List<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';'));
    }
    void RemoveDefineSymbols(string defineSymbol)
    {
        List<string> symbols = getDefineSymbols();
        if (symbols.Contains(defineSymbol))
        {
            symbols.Remove(defineSymbol);
            SetDefineSymbols(symbols);
        }
    }
    void AddDefineSymbols(string defineSymbol)
    {
        List<string> symbols = getDefineSymbols();
        if (!symbols.Contains(defineSymbol))
        {
            symbols.Add(defineSymbol);
            SetDefineSymbols(symbols);
        }
    }
    void SetDefineSymbols(List<string> defineSymbols)
    {
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup,
                string.Join(";", defineSymbols)
            );
    }
}
