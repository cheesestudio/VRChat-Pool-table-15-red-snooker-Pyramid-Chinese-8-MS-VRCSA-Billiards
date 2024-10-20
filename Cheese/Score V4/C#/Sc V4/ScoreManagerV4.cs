/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  新计分器系统
 *  API : ScoreManagerHook
 */


using TMPro;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ScoreManagerV4 : UdonSharpBehaviour
{
    #region Plug
    [Header("NameText")]
    [SerializeField] public TextMeshProUGUI RedNameTMP = null;
    [SerializeField] public TextMeshProUGUI BlueNameTMP = null;
    [Header("Score")]
    [SerializeField] public TextMeshProUGUI RedScoreTMP = null;
    [SerializeField] public TextMeshProUGUI BlueScoreTMP = null;
    [Header("Elo")]
    [SerializeField] public TextMeshProUGUI Elo1 = null;
    [SerializeField] public TextMeshProUGUI Elo2 = null;
    [Header("Info")]
    [SerializeField] public GameObject Messages = null;
    [Header("Plug")]
    [SerializeField] public ScoreNetwork Network = null;
    [SerializeField] public RankingSystem RankingSystem = null;
    [SerializeField] public EloDownload EloAPI = null;
    #endregion

    //APIs   ONLY use in Local!!!!! [HideInInspector] 
    #region APIs
    public string[] lobbyPlayerList = null;
    public string[] nowPlayerList = null;
    public string[] startPlayerList = null;
    public uint winningTeamLocal = 0xFFFFFFFF;
    #endregion

    private void Start()
    {
        //  判空
        if (
            RedNameTMP == null ||
            BlueNameTMP == null ||
            RedScoreTMP == null ||
            BlueScoreTMP == null ||
            Elo1 == null ||
            Elo2 == null ||
            Messages == null ||
            Network == null ||
            RankingSystem == null ||
            EloAPI == null
          )
        {
            this.enabled = false;
            return;
        }

        //Init
        Network._Init(this);
        RankingSystem._Init(this);
    }

    private void Update()
    {
        // 查看Buffer是否需要同步
        Network._FlushBuffer();
    }

    #region LogicFuncion

    private void _Reflash()
    {
        RedNameTMP.text = Network.PlayerA;
        BlueNameTMP.text = Network.PlayerB;
        RedScoreTMP.text = Network.PlayerAScore.ToString();
        BlueScoreTMP.text = Network.PlayerBScore.ToString();

        Messages.SetActive(Network.MessagesState);

        //ELO 
    }

    private void _ResetValue()
    {
        Network.PlayerA = "";
        Network.PlayerB = "";
        Network.PlayerAScore = 0;
        Network.PlayerBScore = 0;

        Network.isInvert = false;
    }

    private void _SetName(string[] name)
    {
        if (name == null)
            return;

        Network.PlayerA = name[0];
        Network.PlayerB = name[1];
    }

    private void _ReflashEloScore()
    {
        Elo1.text = EloAPI.GetElo(Network.PlayerA).ToString();
        Elo2.text = EloAPI.GetElo(Network.PlayerB).ToString();
    }

    #endregion

    // 用于处理TMP界面跟新
    #region Remote

    public void _OnRemoteDeserialization()
    {

        Debug.Log("[SCM]" + Network.State);
        // 调用栈
        for (int i = 0; i <= Network.funcStackTop; i++)
        {

            int inFunc = Network.funcStack[i];

            switch (inFunc)
            {
                case 0:
                    lobbyOpenRemote();
                    break;
                case 1:
                    playerChangedRemote();
                    break;
                case 2:
                    gameStartedRemote();
                    break;
                case 3:
                    gameEndRemote();
                    break;
                case 4:
                    gameResetRemote();
                    break;
                case 0XFF:
                    break;
            }
        }

        // 使用完成后清理栈
        for (int i = 0; i < Network.funcStack.Length; i++)
        {
            Network.funcStack[i] = 0XFF;
        }
        Network.funcStackTop = 0;
    }

    //ID0
    public void lobbyOpenRemote()
    {
        Debug.Log("[SCM] lobbyOpenRemote");
        _ReflashEloScore();
        _Reflash();
    }

    // ID1
    public void playerChangedRemote()
    {
        Debug.Log("[SCM] playerChangedRemote");
        _ReflashEloScore();
        _Reflash();
    }

    // ID2
    public void gameStartedRemote()
    {
        Debug.Log("[SCM] gameStartedRemote");
        _ReflashEloScore();
        _Reflash();
    }

    // ID3
    public void gameEndRemote()
    {
        Debug.Log("[SCM] gameEndRemote");
        _ReflashEloScore();
        _Reflash();
    }

    // ID4
    public void gameResetRemote()
    {
        Debug.Log("[SCM] gameResetRemote");
        _ReflashEloScore();
        _Reflash();
    }

    #endregion

    // 用于处理需要同步的数值
    #region Locals

    // ID0
    public void lobbyOpenLocal()
    {
        Debug.Log("[SCM] LobbyOpened");

        if (
        ((lobbyPlayerList[0] != Network.PlayerA &&
        lobbyPlayerList[0] != Network.PlayerB)  &&  
        Network.State == 3)                     ||
        Network.State == 0
        )
        {
            _ResetValue();

            if (lobbyPlayerList == null)
                return;

            // 赋值玩家名
            _SetName(lobbyPlayerList);

            Network.State = 1;
        }

        // 释放数组
        lobbyPlayerList = null;

        // 跟新状态
        if (Network.funcStackTop < 8)
        {
            Network.funcStack[Network.funcStackTop] = 0;
            Network.funcStackTop++;
        }
        Network._SetBufferStatus();
    }

    // ID1
    public void playerChangedLocal()
    {
        Debug.Log("[SCM] playerChanged");

        if (nowPlayerList == null)
            return;

        // 跟新状态
        if (
            string.IsNullOrEmpty(nowPlayerList[0]) &&
            string.IsNullOrEmpty(nowPlayerList[1]) &&
            Network.State == 1
            )
        {
            Debug.Log("[SCM] Empty");

            _ResetValue();

            // 赋值玩家名
            //_SetName(nowPlayerList);
            Network.State = 0;
        }
        else if (Network.State == 2)
        {
            _ResetValue();

            // 赋值玩家名
            _SetName(nowPlayerList);

            Network.MessagesState = true;
            Network.State = 1;
        }
        else
        {
            if (Network.State == 3)
            {
                Debug.Log("[SCM] get" + nowPlayerList[0] + ";" + nowPlayerList[1]);
                if (
                    (nowPlayerList[0] == Network.PlayerA    ||
                    nowPlayerList[0] == Network.PlayerB     ||
                    string.IsNullOrEmpty(nowPlayerList[0])) &&
                    (nowPlayerList[1] == Network.PlayerA    ||
                    nowPlayerList[1] == Network.PlayerB     ||
                    string.IsNullOrEmpty(nowPlayerList[1]))
                    )
                {
                    Network.State = 3;
                }
                else
                {
                    Network.State = 1;
                    _ResetValue();
                    _SetName(nowPlayerList);
                }
            }
            else
            {
                Network.State = 1;
                _ResetValue();
                _SetName(nowPlayerList);
            }
        }

        // 释放数组
        nowPlayerList = null;

        if (Network.funcStackTop < 8)
        {
            Network.funcStack[Network.funcStackTop] = 1;
            Network.funcStackTop++;
        }
        Network._SetBufferStatus();
    }

    // ID2
    public void gameStartedLocal()
    {
        if (Network.State == 3 || Network.State == 1)
        {
            //是否反转
            if (startPlayerList[0] == Network.PlayerB || startPlayerList[1] == Network.PlayerA)
            {
                Network.isInvert = true;
            }
            else
            {
                Network.isInvert = false;
            }

            // 同步开局玩家名到本地变量(废弃)
            //Network.PlayerAStart = Network.PlayerA;
            //Network.PlayerBStart = Network.PlayerB;

            Network.State = 2;
        }

        startPlayerList = null;
        if (Network.funcStackTop < 8)
        {
            Network.funcStack[Network.funcStackTop] = 2;
            Network.funcStackTop++;
        }
        Network._SetBufferStatus();
    }

    // ID3
    public void gameEndLocal()
    {
        Debug.Log("[SCM] gameEndLocal");

        if (winningTeamLocal == 0xFFFFFFFF)
            return;

        if (Network.State == 1)
        {
            _ResetValue();
            Network.MessagesState = false;
            Network.State = 0;
        }
        else if (Network.State == 2)
        {
            if (Network.isInvert)
                winningTeamLocal = (uint)(winningTeamLocal == 1 ? 0 : 1);

            if (winningTeamLocal == 0)
                Network.PlayerAScore++;
            else if (winningTeamLocal == 1)
                Network.PlayerBScore++;

            RankingSystem.UpdateCopyData(Network.PlayerA, Network.PlayerB, Network.PlayerAScore.ToString(), Network.PlayerBScore.ToString());
            Network.State = 3;
        }

        if (Network.funcStackTop < 8)
        {
            Network.funcStack[Network.funcStackTop] = 3;
            Network.funcStackTop++;
        }

        winningTeamLocal = 0xFFFFFFFF;
        Network._SetBufferStatus();
    }

    // ID4
    public void gameResetLocal()
    {
        Debug.Log("[SCM] ResetSC");

        if (Network.funcStackTop < 8)
        {
            Network.funcStack[Network.funcStackTop] = 4;
            Network.funcStackTop++;
        }
        Network.MessagesState = false;
        _ResetValue();
        Network.State = 0;
        Network._SetBufferStatus();
    }

    #endregion

    #region HookAPIs

    //API lobbyPlayerList
    public void _LobbyOpen()
    {
        lobbyOpenLocal();
    }

    //API nowPlayerList
    public void _PlayerChanged()
    {
        playerChangedLocal();
    }

    //API startPlayerList
    public void _GameStarted()
    {
        gameStartedLocal();
    }

    //API winningTeamLocal
    public void _GameEnd()
    {
        gameEndLocal();
    }

    //NoAPI Value
    public void _GameReset()
    {
        gameResetLocal();
    }

    #endregion

}
