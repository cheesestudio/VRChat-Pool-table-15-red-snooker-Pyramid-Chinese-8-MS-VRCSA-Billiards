using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

public class ColorDownload : UdonSharpBehaviour
{

    /// <summary>
    /// 2024/9/27
    /// By WangQAQ
    /// </summary>

    //彩色名称下载URL
    [Header("URL")]
    [SerializeField] public VRCUrl[] url;

    //用于表示当前加载第几个URL 
    private int reloadStep = 0;

    /// <summary>
    /// 玩家名称颜色集
    /// Name为玩家名数组，Color是玩家颜色，下标一一对应
    /// </summary>
    [HideInInspector] public string[] Name = null;                                                   //玩家名称数组
    [HideInInspector] public string[] Color = null;                                                  //玩家颜色集

    /// <summary>
    /// 新加，用于表示数组是否初始化
    /// </summary>
    private bool isStringInit = false;

    void Start()
    {
        SendCustomEventDelayedSeconds("_AutoReloadColor", 5);
    }

    // 字符串下载成功回调
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        //拆分字符串，按;拆分
        //当前字符串组应该为 "Name","Color"
        string[] ListTmp = result.Result.Split(';', StringSplitOptions.RemoveEmptyEntries);

        //初始化数组
        Name = new string[ListTmp.Length];
        Color = new string[ListTmp.Length];

        //如果内存申请成功，则设置数组初始化变量为true
        if(Name != null && Color != null)
        {
            isStringInit = true;    
        }

        //循环拆分玩家名和彩色代码 O(N)
        for (int i = 0;i < ListTmp.Length; i++)
        {
            //判空
            if (ListTmp[i] != null)
            {
                //按 ， 分割字符串，分割为玩家名和彩色代码
                string[] ColorTmp = ListTmp[i].Split(',', StringSplitOptions.RemoveEmptyEntries);

                //DEBUG
                //Debug.Log("Name:" + ColorTmp.Length);

                //如果长度 == 2 则录入 (Split可能会多一位空数组，unity老bug)
                if (ColorTmp.Length == 2)
                {
                    Name[i] = ColorTmp[0];
                    Color[i] = ColorTmp[1];
                }
            }
        }
    }

    //字符串下载失败回调
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        //循环尝试加载url数组集中的URL
        if (reloadStep < url.Length - 1)
        {
            //如果没有加载到最后一个URL，则加载URL数组集中的下一个URL
            SendCustomEventDelayedSeconds("_AutoReloadColor", 10);
            reloadStep++;
        }
        //else
        //{
        //    //如果到最后一个URL，则从第一个URL开始加载
        //    reloadStep = 0;
        //    SendCustomEventDelayedSeconds("_AutoReloadColor", 10);

        //}
    }

    //重新加载字符串函数
    public void _AutoReloadColor()
    {
        //VRC下载API
        VRCStringDownloader.LoadUrl(url[reloadStep], (IUdonEventReceiver)this);
    }

    //读取玩家对应彩色ID
    public string GetColorColor(string name)
    {
        if(Name != null && isStringInit == true)
        {
            //循环读取彩色状态 O(N)
            for (int i = 0; i < Name.Length; i++)
            {
                if (Name[i] == name)
                {
                    return Color[i];
                }
            }
        }
        return null;
    }
}
