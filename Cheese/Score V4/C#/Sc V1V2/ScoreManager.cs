 
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;
using System;

public class ScoreManager : UdonSharpBehaviour
{
    [SerializeField] public RankingSystem RankingSystem;
    [Header("NameText")]
    [SerializeField] public TextMeshProUGUI red;
    [SerializeField] public TextMeshProUGUI blue;
    [Header("ScoreText(not must)")]
    [SerializeField] public TextMeshProUGUI red_score;
    [SerializeField] public TextMeshProUGUI blue_score;

    private int BlueScore = 0;
    private int RedScore = 0;

    private bool isScoreOn = false;

    [Header("一代计分板,不用保持为空")]
    public SkinnedMeshRenderer l_skr;

    [UdonSynced]
    private int R_BlueScore = 0;
    [UdonSynced]
    private int R_RedScore = 0;

    [UdonSynced]
    private string R_Player1 = "";
    [UdonSynced]
    private string R_Player2 = "";

    [UdonSynced]
    private bool R_isScoreOn = false;

    private void l_reflash()
    {
        isScoreOn = R_isScoreOn;

        RedScore = R_RedScore;
        BlueScore = R_BlueScore;

        red.text = R_Player1;
        blue.text = R_Player2;

        if (red_score != null && blue_score != null)
        {
            red_score.text = R_RedScore.ToString();
            blue_score.text = R_BlueScore.ToString();
        }
        ReflashDisplay();
    }

    public void toggleScoreOn(int ID1, int ID2)
    {
        if (R_isScoreOn) return;

        VRCPlayerApi player1 = VRCPlayerApi.GetPlayerById(ID1);
        VRCPlayerApi player2 = VRCPlayerApi.GetPlayerById(ID2);

        if (!Utilities.IsValid(player1) || !Utilities.IsValid(player2))
            return;

        if (player1.displayName != Networking.LocalPlayer.displayName)
            return;

        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(player1, gameObject);

        R_Player1 = player1.displayName;
        R_Player2 = player2.displayName;

        Debug.Log("{player1.displayName} is owner");

        R_isScoreOn = true;
        isScoreOn =true;
    }

    void Start()
    {
        if(Networking.IsOwner(gameObject))
        {
            R_BlueScore = 0;
            R_RedScore = 0;
            RequestSerialization();
        }
        //l_skr = GetComponentInChildren<SkinnedMeshRenderer>();
        l_reflash();
    }



    public void AddScore(int L_PlayerID1, int L_PlayerID2, int Winner,uint gamemodelocal)
    {
        VRCPlayerApi player1 = VRCPlayerApi.GetPlayerById(L_PlayerID1);
        VRCPlayerApi player2 = VRCPlayerApi.GetPlayerById(L_PlayerID2);
        VRCPlayerApi winplayer = VRCPlayerApi.GetPlayerById(Winner);

        if (!Utilities.IsValid(player1) || !Utilities.IsValid(player2) || !Utilities.IsValid(winplayer))
            return;

        if (isScoreOn == false)
        {
            toggleScoreOn(L_PlayerID1, L_PlayerID2);
        }
        else if (winplayer == Networking.LocalPlayer && !Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(winplayer, gameObject);
            //Debug.Log("1" + winplayer.displayName);
        }
        //Debug.Log("2" + Networking.LocalPlayer);
        if (winplayer.displayName == R_Player1)
        {
            R_RedScore++;
        }
        else if (winplayer.displayName == R_Player2)
        {
            R_BlueScore++;
        }
        else
        {
            //RESET

            R_isScoreOn = false;

            R_Player1 = player1.displayName;
            R_Player2 = player2.displayName;

            R_RedScore = 0;
            R_BlueScore = 0;

            //shit 
            if (L_PlayerID1 == Winner)
            {
                R_RedScore++;
            }
            else if (L_PlayerID2 == Winner)
            {
                R_BlueScore++;
            }
            toggleScoreOn(L_PlayerID1, L_PlayerID2);

        }
        l_reflash();

        if (R_BlueScore != R_RedScore)
            RankingSystem.UpdateCopyData(player1.displayName, player2.displayName, Convert.ToString(R_RedScore), Convert.ToString(R_BlueScore));


        RequestSerialization();
  
    }

    public void ReflashDisplay()
    {
        if (l_skr == null) return;
        for (int i = 0; i < 40; i++)
        {
            l_skr.SetBlendShapeWeight(i, 0);
        }
        //l_skr.SetBlendShapeWeight(10, 1.0f);

        l_skr.SetBlendShapeWeight(BlueScore / 10 * 4, 100);
        l_skr.SetBlendShapeWeight(BlueScore % 10 * 4 + 1, 100);
        l_skr.SetBlendShapeWeight(RedScore / 10 * 4 + 2, 100);
        l_skr.SetBlendShapeWeight(RedScore % 10 * 4 + 3, 100);
    }

    public void M_Score_Reset()
    {

        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        R_isScoreOn = false;

        R_Player1 = "";
        R_Player2 = "";

        R_RedScore = 0;
        R_BlueScore = 0;

        l_reflash();

        RequestSerialization();
    }

    public void Score_BlueAdd()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == R_Player1 || Networking.LocalPlayer.displayName == R_Player2)
        {
            R_BlueScore++;
            RequestSerialization();
        }
    }

    public void Score_RedAdd()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == R_Player1 || Networking.LocalPlayer.displayName == R_Player2)
        {
            R_RedScore++;
            RequestSerialization();
        }
}
    public void Score_BlueMinus()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == R_Player1 || Networking.LocalPlayer.displayName == R_Player2)
        {
            R_BlueScore--;
            RequestSerialization();
        }
    }

    public void Score_RedMinus()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (Networking.LocalPlayer.displayName == R_Player1 || Networking.LocalPlayer.displayName == R_Player2)
        {
            R_RedScore--;
            RequestSerialization();
        }
    }

    public override void OnDeserialization()
    {
        //Sync
        //Reflash
        l_reflash();
    }

}
