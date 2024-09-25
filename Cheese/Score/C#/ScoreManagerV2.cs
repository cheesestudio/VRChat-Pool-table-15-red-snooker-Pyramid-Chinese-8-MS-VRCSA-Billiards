﻿
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ScoreManagerV2 : UdonSharpBehaviour
{
    [SerializeField] public RankingSystem RankingSystem;
    [Header("NameText")]
    [SerializeField] public TextMeshProUGUI RedNameTMP;
    [SerializeField] public TextMeshProUGUI BlueNameTMP;
    [Header("Score")]
    [SerializeField] public TextMeshProUGUI RedScoreTMP;
    [SerializeField] public TextMeshProUGUI BlueScoreTMP;

    [UdonSynced]
    private int BlueScore = 0;
    [UdonSynced]
    private int RedScore = 0;

    [UdonSynced]
    private string Redplayer = "";
    [UdonSynced]
    private string BluePlayer = "";


    private void Reflash()
    {

        RedNameTMP.text = Redplayer;
        BlueNameTMP.text = BluePlayer;

        if ( RedScoreTMP != null && BlueScoreTMP != null)
        {
            RedScoreTMP.text = RedScore.ToString();
            BlueScoreTMP.text = BlueScore.ToString();
        }

        RankingSystem.UpdateCopyData(Redplayer, BluePlayer, Convert.ToString(RedScore), Convert.ToString(BlueScore));
    }

    


    public void AddScore(int L_PlayerID1, int L_PlayerID2, int Winner)
    {
        VRCPlayerApi player1 = VRCPlayerApi.GetPlayerById(L_PlayerID1);
        VRCPlayerApi player2 = VRCPlayerApi.GetPlayerById(L_PlayerID2);
        VRCPlayerApi winplayer = VRCPlayerApi.GetPlayerById(Winner);

        if (player1 != null && player2 != null && winplayer != null)
        {
            if(Networking.LocalPlayer == winplayer)
            {
                if (!Networking.IsOwner(gameObject))
                    Networking.SetOwner(Networking.LocalPlayer,gameObject);
                //下面分两种情况讨论
                //1.未设置初始值,按队伍进行正常分配(另一种可能是是不是前两个人)
                if((Redplayer==null && BluePlayer ==null) || (player1.displayName != Redplayer && player2.displayName != BluePlayer) || (player1.displayName != BluePlayer && player2.displayName != Redplayer))
                {
                    if (L_PlayerID1 == Winner) RedScore = 1; BlueScore = 0;
                    if (L_PlayerID2 == Winner) RedScore = 0; BlueScore = 1;
                    Redplayer = player1.displayName;
                    BluePlayer = player2.displayName;
                }
                else//2.保持名字顺序不变,正常加分
                {
                    if (winplayer.displayName == Redplayer) RedScore++;
                    if (winplayer.displayName == BluePlayer) BlueScore++;

                }

                Reflash();
                RequestSerialization();

            }

        }
    }

    public void M_Score_Reset()
    {

        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        Redplayer = "";
        BluePlayer = "";

        RedScore = 0;
        BlueScore = 0;

        Reflash();

        RequestSerialization();
    }

    public void Score_BlueAdd()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == Redplayer || Networking.LocalPlayer.displayName == BluePlayer)
        {
            BlueScore++;
            RequestSerialization();
        }
    }

    public void Score_RedAdd()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == Redplayer || Networking.LocalPlayer.displayName == BluePlayer)
        {
            RedScore++;
            RequestSerialization();
        }
    }
    public void Score_BlueMinus()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == Redplayer || Networking.LocalPlayer.displayName == BluePlayer)
        {
            BlueScore--;
            RequestSerialization();
        }
    }

    public void Score_RedMinus()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == Redplayer || Networking.LocalPlayer.displayName == BluePlayer)
        {
            RedScore--;
            RequestSerialization();
        }
    }

    public override void OnDeserialization()
    {
        Reflash();
    }

}