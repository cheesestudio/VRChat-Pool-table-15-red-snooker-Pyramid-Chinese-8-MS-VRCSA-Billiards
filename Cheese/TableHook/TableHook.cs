﻿using System;
using UdonSharp;
using UnityEngine;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;
using VRC.SDK3.Data;
using Unity.Mathematics;

public class TableHook : UdonSharpBehaviour
{
    //Data
    [SerializeField] public Texture2D[] cueSkins;
    [SerializeField] public TextMeshProUGUI PlayerID;
    //Slider
    [SerializeField] public GlobalSlider TableColorSlider;
    [SerializeField] public GlobalSlider TableColorLightnessSlider;
    [HideInInspector] public float TableColor;
    [HideInInspector] public float TableColorLightness;

    // Cue Skin & Ball Skin
    [HideInInspector] public int inOwner;
    [HideInInspector] public byte outCanUse;
    private byte outCanUseTmp = 0;
    [HideInInspector] public byte ball;
    public byte DefaultCue;
    private int isRotating;
    [NonSerialized] private int maxRotation=130;
    private Renderer renderer;

    //Save & Load
    public TMP_InputField inputField;
    //public InputField inputField;
    [SerializeField] public SettingLoader SettingLoader;

    public Translations hookTranslation;
    private DataList table = new DataList();
    private DataList Translations = new DataList();

    void OnEnable()
    {
        Translations.Add(hookTranslation);

        PlayerID.text = Networking.LocalPlayer.displayName;

        outCanUse = 0;
        ball = 0;
        outCanUseTmp = DefaultCue;
        isRotating = maxRotation;
        renderer = this.transform.Find("body/render").GetComponent<Renderer>();

        //Load PlayerSettings from my server
        //string LocalData = EncodeLocalData();
        //inputField.text=SettingLoader.GetSettingString(Networking.LocalPlayer.displayName);
    }

    public void Reset()
    {
        _Cue0();
        outCanUse = 0;
        outCanUseTmp = 0;
        ball=0;
        TableColorSlider.slider.value = 0;
        TableColorLightnessSlider.slider.value = 1;
        TableColorSlider.mat.SetFloat("_ClothHue", 0);
        TableColorSlider.mat.SetFloat("_ClothSaturation", 1);
        TableColor = 0;
        TableColorLightness = 1;

        cueSizeSlider.value = 10;
        cueSmoothingSlider.value = 10;
        setCueSize();
        setCueSmoothing();

    }
    public void _CanUseCueSkin()
    {
            outCanUse = outCanUseTmp;
    }

    //public void _ChangeKeepRotating()
    //{ 
    //    keepCueRotating = !keepCueRotating;
    //}
    private void ChangeMaterial()
    {
        if (cueSkins[outCanUseTmp] != null)
        {
                renderer.materials[1].SetTexture("_MainTex", cueSkins[outCanUseTmp]);
        }
        isRotating = 0;
    }
    void Update()
    {
        if (isRotating < maxRotation)
        {
            renderer.transform.Rotate(new Vector3(1, 0.05f, 0.05f), Mathf.Clamp(maxRotation-isRotating,0,3), Space.Self);
            isRotating++;
        }
        TableColor=GetTableColor();
        TableColorLightness=GetTableLightness();
    }

    public float GetTableColor()
    {
        return (float)TableColorSlider.GetProgramVariable("localValue");
    }
    public float GetTableLightness()
    {
        return (float)TableColorLightnessSlider.GetProgramVariable("localValue");
    }

    public void AddTranslation(Translations translations)
    {
        Translations.Add(translations);
    }
    public void AddBilliardsModule(BilliardsModule module)
    {
        table.Add(module);
    }

    public Slider cueSizeSlider;
    public TextMeshProUGUI cueSizeText;
    public void setCueSize()
    {
        float newScale = cueSizeSlider.value / 10f;
        renderer.transform.localScale = new Vector3(0.3f * newScale,0.3f * newScale,newScale * 0.3f);
        foreach (var tabletmp in table.ToArray())
        {
            ((BilliardsModule)tabletmp.Reference).cueControllers[0].setScale(newScale);
            ((BilliardsModule)tabletmp.Reference).cueControllers[1].setScale(newScale);
            ((BilliardsModule)tabletmp.Reference).menuManager.cueSizeSlider.value = cueSizeSlider.value;
            ((BilliardsModule)tabletmp.Reference).menuManager.cueSizeText.text = newScale.ToString("F1");
            cueSizeText.text = newScale.ToString("F1");
        }
    }
    public Slider cueSmoothingSlider;
    public TextMeshProUGUI cueSmoothingText;
    public void setCueSmoothing()
    {
        float newSmoothing = cueSmoothingSlider.value / 10f;
        foreach (var tabletmp in table.ToArray())
        {
            ((BilliardsModule)tabletmp.Reference).cueControllers[0].setSmoothing(newSmoothing);
            ((BilliardsModule)tabletmp.Reference).cueControllers[1].setSmoothing(newSmoothing);
            ((BilliardsModule)tabletmp.Reference).menuManager.cueSmoothingSlider.value = cueSmoothingSlider.value;
            ((BilliardsModule)tabletmp.Reference).menuManager.cueSmoothingText.text = newSmoothing.ToString("F1");
            cueSmoothingText.text = newSmoothing.ToString("F1");
        }
    }
    //Sava and load system
    #region ConvertFunction
    private void floatToBytes(byte[] data, int pos, float v)
    {
        byte[] bytes = BitConverter.GetBytes(v);
        Array.Copy(bytes, 0, data, pos, 4);
    }

    public float bytesToFloat(byte[] data, int pos)
    {
        byte[] floatBytes = new byte[4];
        Array.Copy(data, pos, floatBytes, 0, 4);
        return BitConverter.ToSingle(floatBytes, 0);
    }

    private bool isInvalidBase64Char(char value)
    {
        var intValue = (int)value;

        // 1 - 9
        if (intValue >= 48 && intValue <= 57)
            return false;

        // A - Z
        if (intValue >= 65 && intValue <= 90)
            return false;

        // a - z
        if (intValue >= 97 && intValue <= 122)
            return false;

        // + or /
        return intValue != 43 && intValue != 47;
    }

    private bool isValidBase64(string value)
    {
        if (value == null || value.Length == 0 || value.Length % 4 != 0
            || value.Contains(" ") || value.Contains("\t") || value.Contains("\r") || value.Contains("\n"))
            return false;
        var index = value.Length - 1;

        if (value[index] == '=')
            index--;

        if (value[index] == '=')
            index--;

        for (var i = 0; i <= index; i++)
            if (isInvalidBase64Char(value[i]))
                return false;

        return true;
    }
    #endregion

    #region Save & Load
    // I Call it : Cheese Version ,for short CV,rewrite from "NetworingManagers" 
    uint LocalDataLength = 11;
    private string EncodeLocalData()
    {
        byte[] gameState = new byte[LocalDataLength];
        int encodePos = 0;
        gameState[encodePos] = outCanUseTmp;
        encodePos += 1;
        gameState[encodePos] = ball;
        encodePos += 1;
        floatToBytes(gameState, encodePos,TableColor);
        encodePos += 4;
        floatToBytes(gameState, encodePos, TableColorLightness);
        encodePos += 4;

        // find gameStateLength
        //Debug.Log("gameStateLength = " + (encodePos + 1));
        
        return "CV:"+Convert.ToBase64String(gameState);

        //Debug.Log("CV:" + Convert.ToBase64String(gameState));
    }

    private void LoadLocalDataV0(string gameStateStr)
    {
        if (!isValidBase64(gameStateStr)) return;

        byte[] gameState = Convert.FromBase64String(gameStateStr);
        if (gameState.Length != LocalDataLength) return;

        int encoodePos = 0;

        outCanUseTmp = gameState[encoodePos];
        encoodePos += 1;
        ball = gameState[encoodePos];
        encoodePos += 1;
        TableColor = bytesToFloat(gameState, encoodePos);
        encoodePos += 4;
        TableColorLightness = bytesToFloat(gameState,encoodePos);
        encoodePos += 4;

        ChangeMaterial();
    }

    private void LoadLocalData(string gameStateStr)
    {
        if (gameStateStr.StartsWith("CV:"))
        {
            LoadLocalDataV0(gameStateStr.Substring(3));
        }
    }
    public void OnSaveButtonPushed()
    {

        if (ReferenceEquals(null, inputField))
        {
            Debug.Log("Table Hook::OnSaveButtonPushed() inputField property is not set !");
            return;
        }

        inputField.text = EncodeLocalData();
    }

    public void OnLoadButtonPushed()
    {

        if (ReferenceEquals(null, inputField))
        {
            Debug.Log("Table Hook::OnSaveButtonPushed() inputField property is not set !");
            return;
        }

        if (string.IsNullOrEmpty(inputField.text))
        {
            return;
        }

        //if (!_IsPlayer(Networking.LocalPlayer)) return; //not load on others game

        LoadLocalData(inputField.text);

    }

    public void LoadFromNetwork()
    {
        inputField.text = SettingLoader.GetSettingString(Networking.LocalPlayer.displayName);
        LoadLocalData(inputField.text);
    }
    #endregion

    #region Cue & Ball
    public void _Cue0()
    {
        outCanUseTmp = 0;
        ChangeMaterial();
    }

    public void _Cue1()
    {
        outCanUseTmp = 1;
        ChangeMaterial();
    }
    public void _Cue2()
    {
        outCanUseTmp = 2;
        ChangeMaterial();
    }

    public void _Cue3()
    {
        outCanUseTmp = 3;
        ChangeMaterial();
    }
    public void _Cue4()
    {
        outCanUseTmp = 4;
        ChangeMaterial();
    }

    public void _Cue5()
    {
        outCanUseTmp = 5;
        ChangeMaterial();
    }
    public void _Cue6()
    {
        outCanUseTmp = 6;
        ChangeMaterial();
    }

    public void _Cue7()
    {
        outCanUseTmp = 7;
        ChangeMaterial();
    }
    public void _Cue8()
    {
        outCanUseTmp = 8;
        ChangeMaterial();
    }
    public void _Cue9()
    {
        outCanUseTmp = 9;
        ChangeMaterial();
    }

    public void _Cue10()
    {
        outCanUseTmp = 10;
        ChangeMaterial();
    }
    public void _Cue11()
    {
        outCanUseTmp = 11;
        ChangeMaterial();
    }
    public void _Cue12()
    {
        outCanUseTmp = 12;
        ChangeMaterial();
    }

    public void _Cue13()
    {
        outCanUseTmp = 13;
        ChangeMaterial();
    }
    public void _Cue14()
    {
        outCanUseTmp = 14;
        ChangeMaterial();
    }
    public void _Cue15()
    {
        outCanUseTmp = 15;
        ChangeMaterial();
    }
    public void _Cue16()
    {
        outCanUseTmp = 16;
        ChangeMaterial();
    }
    public void _Cue17()
    {
        outCanUseTmp = 17;
        ChangeMaterial();
    }
    public void _Cue18()
    {
        outCanUseTmp = 18;
        ChangeMaterial();
    }
    public void _Cue19()
    {
        outCanUseTmp = 19;
        ChangeMaterial();
    }
    public void _Cue20()
    {
        outCanUseTmp = 20;
        ChangeMaterial();
    }
    public void _Ball0()
    {
        ball = 0;
    }
    public void _Ball1()
    {
        ball = 4;
    }
    public void _Ball2()
    {
        ball = 5;
    }
    public void _Ball3()
    {
        ball = 6;
    }
    #endregion

#region Language


    public void SetLanguage(string language)
    {
        foreach(var translate in Translations.ToArray()) ((Translations)translate.Reference).SetLanguage(language);
    }

    public void zh() { SetLanguage("zh"); }
    public void en() { SetLanguage("en"); }
    public void ja() { SetLanguage("ja"); }
#endregion
}