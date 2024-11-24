/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  新计分器系统网络同步模块
 */

using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ScoreNetwork : UdonSharpBehaviour
{
    //Sync Value
    // 玩家名
    [HideInInspector][UdonSynced] public string PlayerA;
    [HideInInspector][UdonSynced] public string PlayerB;

    // 分数
    [HideInInspector][UdonSynced] public int PlayerAScore;
    [HideInInspector][UdonSynced] public int PlayerBScore;

    // 游戏模式
    [HideInInspector][UdonSynced] public uint Mode;

    // 状态量
    [HideInInspector][UdonSynced] public byte State;
    [HideInInspector][UdonSynced] public bool MessagesState = false;

    // 上传时同步日期
    [HideInInspector][UdonSynced] public string Date = "";

    // 反转状态
    [HideInInspector][UdonSynced] public bool isInvert = false;


    // 远程调用函数,存储函数ID
    #region List
    //VRC 不让我同步数组，我谢谢它的木琴(红温)
    [HideInInspector][UdonSynced] public byte funcStack0;
    [HideInInspector][UdonSynced] public byte funcStack1;
    [HideInInspector][UdonSynced] public byte funcStack2;
    [HideInInspector][UdonSynced] public byte funcStack3;
    #endregion
    [HideInInspector] public byte[] funcStack = new byte[4];

    [HideInInspector][UdonSynced] public int funcStackTop = 0;              //栈顶

    // Status (0 = Synced 1= need sync)
    private bool bufferStatus = false;

    // DL
    private ScoreManagerV4 _scoreManager;

    #region dl

    public void _Init(ScoreManagerV4 score)
    {
        _scoreManager = score;

        // 初始化为-1
        for (int i = 0; i < funcStack.Length; i++) 
        {
            funcStack[i] = 0XFF;
        }
    }

    #endregion

    #region Sync

    public void _SetBufferStatus()
    {
        bufferStatus = true;
    }

    public void _FlushBuffer()
    {
        if (!bufferStatus) return;

        bufferStatus = false;

        ArryToBytes();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (!ReferenceEquals(null, localPlayer))
        {
            Networking.SetOwner(localPlayer, this.gameObject);
        }

        this.RequestSerialization();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        BytesToArry();
        _scoreManager._OnRemoteDeserialization();
    }

    #endregion

    void ArryToBytes()
    {
        funcStack0 = funcStack[0];
        funcStack1 = funcStack[1];
        funcStack2 = funcStack[2];
        funcStack3 = funcStack[3];
    }

    void BytesToArry()
    {
        funcStack[0] = funcStack0;
        funcStack[1] = funcStack1;
        funcStack[2] = funcStack2;
        funcStack[3] = funcStack3;
    }
}
