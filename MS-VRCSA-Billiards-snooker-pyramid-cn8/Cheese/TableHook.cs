using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TableHook : UdonSharpBehaviour
{
    [HideInInspector] public int inOwner;
    [HideInInspector] public int outCanUse;
    private int outCanUseTmp = 0;
    [SerializeField] private BilliardsModule[] table;
    private int TableColor = 0;
    public bool keepRotating = false;
    private int isRotating;
    [SerializeField] private int maxRotation=120;
    private Renderer renderer;
    void Start()
    {
        outCanUse = 0;
        outCanUseTmp = 0;
        //maxRotation = 120;
        isRotating = maxRotation;
        keepRotating = false;
        //BilliardsModule[] table =UnityEngine.Object.FindObjectsOfType<BilliardsModule>();
        renderer = this.transform.Find("body/render").GetComponent<Renderer>();
    }

    public void _CanUseCueSkin()
    {
        //VRCPlayerApi ownerPlayer = Networking.LocalPlayer;
        //if (ReferenceEquals(null, ownerPlayer))
        //{
        //    return;
        //}
        //int owner = ownerPlayer.playerId;

        //if (owner == inOwner)
        //{
        //    outCanUse = outCanUseTmp;
        //}
    }

    public void _ChangeKeepRotating()
    { 
        keepRotating = !keepRotating;
        //Debug.Log("rotating changed");
        //Debug.Log(keepRotating);
    }
    private void ChangeMaterial()
    {
        if (table != null)
        {
            for (int i = 0; i < table.Length; i++)
            {
                renderer.materials[1].SetTexture("_MainTex", table[i].cueSkins[outCanUseTmp]);
            }
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
}