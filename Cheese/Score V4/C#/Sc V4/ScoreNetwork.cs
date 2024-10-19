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
    [HideInInspector][UdonSynced] public string PlayerA;
    [HideInInspector][UdonSynced] public string PlayerB;

    [HideInInspector][UdonSynced] public int PlayerAScore;
    [HideInInspector][UdonSynced] public int PlayerBScore;

    #region Info
    /*
api 
lobby start        _LobbyOpen +  string[] name
Game start       _GameStarted + string[] name
Playr chenge     _PlayerChanged   + string[] name
game ended     _GameEnd    + INT playerID
game reset       _GameReset


0 ide

1 join

2 start

3 ended

  Lobby open
0    ------>  1 (Reset Score,Name)

  on player join (Playr chenge)
1 -------------> 1 (Reset Score,Name)

  all player leave (Playr chenge)
1 ----------> 0 (Reset Score,Name)

  on game start (_GameStarted)
1 ----------->  2  

  on game chenge palyer (Playr chenge) 
2 -----------------------> 1 (Reset Score,Name) (Error Messge)

  on game ended (_GameEnd)
1 -----------------> 0 (Reset Score,Name)(Clear Error Messge)

  on game ended 
2 -----------------> 3 (_GameEnd)

  on player chenge in 3 (Playr chenge)   和 on player join (Playr chenge) 合并
3 -----------------------> 1 (Reset Score,Name)

  on game start  (_GameStarted)
3 ----------------> 2 

  on game reset 
* ------------------> 0  (Reset Score,Name)
*/
    #endregion
    [HideInInspector][UdonSynced] public byte State;
    [HideInInspector][UdonSynced] public bool MessagesState = false;

    // 游戏开始时玩家ID
    [HideInInspector][UdonSynced] public string PlayerAStart;
    [HideInInspector][UdonSynced] public string PlayerBStart;

    // 远程调用函数,存储函数ID
    #region List
    //VRC 不让我同步数组，我谢谢它的木琴(红温)
    [HideInInspector][UdonSynced] public byte funcStack0;
    [HideInInspector][UdonSynced] public byte funcStack1;
    [HideInInspector][UdonSynced] public byte funcStack2;
    [HideInInspector][UdonSynced] public byte funcStack3;
    [HideInInspector][UdonSynced] public byte funcStack4;
    [HideInInspector][UdonSynced] public byte funcStack5;
    [HideInInspector][UdonSynced] public byte funcStack6;
    [HideInInspector][UdonSynced] public byte funcStack7;
    #endregion
    [HideInInspector] public byte[] funcStack = new byte[8];

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
        funcStack4 = funcStack[4];
        funcStack5 = funcStack[5];
        funcStack6 = funcStack[6];
        funcStack7 = funcStack[7];
    }

    void BytesToArry()
    {
        funcStack[0] = funcStack0;
        funcStack[1] = funcStack1;
        funcStack[2] = funcStack2;
        funcStack[3] = funcStack3;
        funcStack[4] = funcStack4;
        funcStack[5] = funcStack5;
        funcStack[6] = funcStack6;
        funcStack[7] = funcStack7;
    }
}
