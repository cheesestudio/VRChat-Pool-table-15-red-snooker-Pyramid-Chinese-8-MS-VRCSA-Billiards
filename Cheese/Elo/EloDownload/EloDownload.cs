/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  Elo 下载器 Json格式
 */
/*
 * 
 * API格式
 * {
 *     "scores": {
 *        "name" : "value",
 *         ...
 *     }
 *  }
 * 
 */
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

public class EloDownload : UdonSharpBehaviour
{
    // Elo下载URL
    [Header("URL")]
    [SerializeField] public VRCUrl url;

    // Elo字典对象
    [HideInInspector][SerializeField] public DataDictionary _eloData = null;

    void Start()
    {
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    #region URL
    // 字符串下载成功回调
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (VRCJson.TryDeserializeFromJson(result.Result, out var json))
        {
            _eloData = json.DataDictionary["scores"].DataDictionary;
        }
    }

    //字符串下载失败回调
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        SendCustomEventDelayedSeconds("_AutoReload", 60);
    }

    //重新加载字符串函数
    public void _AutoReload()
    {
        //VRC下载API
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    #endregion

    #region API
    // 读取玩家对应Elo分数
    public int GetElo(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        if (_eloData == null)
            return 0;

        string score = _eloData[name].ToString();
        Debug.Log(score);
        if (!string.IsNullOrEmpty(score))
            if(score != "KeyDoesNotExist")
                return (int)Convert.ToSingle(score);

        return 0;
    }
    #endregion
}
