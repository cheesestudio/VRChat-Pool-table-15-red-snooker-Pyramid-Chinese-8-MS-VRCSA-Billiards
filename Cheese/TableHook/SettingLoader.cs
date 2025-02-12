using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

public class SettingLoader : UdonSharpBehaviour
{
    public TableHook tablehook;

    [Header("URL")]
    [SerializeField] public VRCUrl[] url;

    private int reloadStep = 0;

    private string[] Name = null;
    private string[] SettingString = null;

    private bool isStringInit = false;

    void Start()
    {
        VRCStringDownloader.LoadUrl(url[0], (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        string[] ListTmp = result.Result.Split(';', StringSplitOptions.RemoveEmptyEntries);

        Name = new string[ListTmp.Length];
        SettingString = new string[ListTmp.Length];

        if(Name != null && SettingString!=null)
        {
            isStringInit = true;
        }

        for (int i = 0; i < ListTmp.Length; i++)
        {
            if (ListTmp[i] != null)
            {
                string[] ColorTmp = ListTmp[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                //Debug.Log("Name:" + ColorTmp.Length);
                if (ColorTmp.Length == 2)
                {
                    //Debug.Log("Name:" + ColorTmp[0] + "," + "Color:" + ColorTmp[1]);
                    Name[i] = ColorTmp[0];
                    SettingString[i] = ColorTmp[1];
                }
            }
        }

        tablehook.LoadFromNetwork();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (reloadStep < url.Length - 1)
        {
            SendCustomEventDelayedSeconds("_AutoReloadColor", 10);
            reloadStep++;
        }
        else
        {
            reloadStep = 0;
            SendCustomEventDelayedSeconds("_AutoReloadColor", 10);
        }
    }

    public void _AutoReloadColor()
    {
        VRCStringDownloader.LoadUrl(url[reloadStep], (IUdonEventReceiver)this);
    }

    public string GetSettingString(string name)
    {
        if (name != null && isStringInit == true)
        {
            for (int i = 0; i < Name.Length; i++)
            {
                if (Name[i] == name)
                {
                    return SettingString[i];
                }
            }
        }
        return null;
    }
}
