using System;
using UdonSharp;
using UnityEngine;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;

public class TableHook : UdonSharpBehaviour
{
    [SerializeField] public Texture2D[] cueSkins;
    //Slider
    [SerializeField] public UdonBehaviour TableColorSlider;
    [SerializeField] public UdonBehaviour TableColorLightnessSlider;
    [HideInInspector] public float TableColor;
    [HideInInspector] public float TableColorLightness;

    [HideInInspector] public int inOwner;
    [HideInInspector]public int outCanUse;
    private int outCanUseTmp = 0;
    [HideInInspector] public int ball;
    public int DefaultCue;
    public bool keepRotating = false;
    private int isRotating;
    [NonSerialized] private int maxRotation=120;
    private Renderer renderer;
    void Start()
    {
        outCanUse = 0;
        ball = 0;
        outCanUseTmp = DefaultCue;
        isRotating = maxRotation;
        keepRotating = false;
        renderer = this.transform.Find("body/render").GetComponent<Renderer>();
    }

    public void _CanUseCueSkin()
    {
            outCanUse = outCanUseTmp;
    }

    public void _ChangeKeepRotating()
    { 
        keepRotating = !keepRotating;
    }
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
        if (isRotating < maxRotation || keepRotating)
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
}