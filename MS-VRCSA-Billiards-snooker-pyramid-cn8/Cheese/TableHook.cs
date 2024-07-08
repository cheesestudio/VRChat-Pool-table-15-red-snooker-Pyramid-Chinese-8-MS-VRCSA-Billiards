using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TableHook : UdonSharpBehaviour
{

    [HideInInspector] public int outCanUse = 0;
    [SerializeField] private BilliardsModule[] table;
    private int TableColor = 0;
    public bool keepRotating = false;
    private int isRotating;
    [SerializeField] private int maxRotation=120;
    private Renderer renderer;
    void Start()
    {
        outCanUse = 0;
        //maxRotation = 120;
        isRotating = maxRotation;

        //BilliardsModule[] table =UnityEngine.Object.FindObjectsOfType<BilliardsModule>();
        renderer = this.transform.Find("body/render").GetComponent<Renderer>();
    }

    public void _CanUseCueSkin()
    {

    }

    private void ChangeMaterial()
    {
        if (table != null)
        {
            for (int i = 0; i < table.Length; i++)
            {
                renderer.materials[1].SetTexture("_MainTex", table[i].cueSkins[outCanUse]);
            }
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
    }
    
      
    public void _Cue0()
    {
        outCanUse = 0;
        ChangeMaterial();
    }

    public void _Cue1()
    {
        outCanUse = 1;
        ChangeMaterial();
    }
    public void _Cue2()
    {
        outCanUse = 2;
        ChangeMaterial();
    }

    public void _Cue3()
    {
        outCanUse = 3;
        ChangeMaterial();
    }
    public void _Cue4()
    {
        outCanUse = 4;
        ChangeMaterial();
    }

    public void _Cue5()
    {
        outCanUse = 5;
        ChangeMaterial();
    }
    public void _Cue6()
    {
        outCanUse = 6;
        ChangeMaterial();
    }

    public void _Cue7()
    {
        outCanUse = 7;
        ChangeMaterial();
    }
}