
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace Cheese
{
    public class PersonalDataCounter : UdonSharpBehaviour
    {

        //还有平均出杆时间等
        [Header("TMP")]
        public TextMeshProUGUI DataText;
        public TextMeshProUGUI CalculatedDataText;
        public TextMeshProUGUI SnookerText;
        [TextArea] public string DataTextFormat = "";
        [TextArea] public string SecDataTextFormat = "";
        [TextArea] public string SnookerTextFormat = "";
        //Datas
        public int gameCount = 0;      //场次
        public int winCount = 0;       //胜场
        public int loseCount = 0;      //败场
        public int pocketCount = 0;    //进球数
        public int inningCount = 0;    //回合数
        public int shotCount = 0;       //出杆数  
        public int scratchCount = 0;    //摔袋数
        public int foulEnd = 0;        //犯规结束
        public int breakFoul = 0;      //开球犯规
        public int foulCount = 0;      //犯规次数
        public int lossOfChange = 0;   //开球失机(开球没下次数)
        public int goldenBreak = 0;     //黄金开球
        public int clearance = 0;      //一杆清台次数
        public int breakClearance = 0; //炸清次数

        public int gameCountSnooker = 0;    //斯诺克场次
        public int pocketCountSnooker = 0;  //斯诺克进球数
        public int shotCountSnooker = 0;    //斯诺克回合数
        public int inningCountSnooker = 0;  //斯诺克击球数
        public int heightBreak = 0;         //单杆最高分

        //Keys to save/load persist data
        private const string GAME_COUNT = "GameCount";
        private const string WIN_COUNT = "WinCount";
        private const string LOSE_COUNT = "LoseCount";
        private const string POCKET_COUNT = "PocketCount";
        private const string INNING_COUNT = "InningCount";
        private const string SHOT_COUNT = "ShotCount";
        private const string SCRATCH_COUNT = "ScratchCount";
        private const string FOUL_END = "FoulEnd";
        private const string BREAK_FOUL = "BreakFoul";
        private const string FOUL_COUNT = "FoulCount";
        private const string LOSS_OF_CHANGE = "LossOfChange";
        private const string GOLDEN_BREAK = "GoldenBreak";
        private const string CLEARANCE = "Clearance";
        private const string BREAK_CLEARANCE = "BreakClearance";

        private const string GAME_COUNT_SNOOKER = "GameCountSnooker";
        private const string POCKET_COUNT_SNOOKER = "PocketCountSnooker";
        private const string SHOT_COUNT_SNOOKER = "ShotCountSnooker";
        private const string INNNING_COUNT_SNOOKER = "InningCountSnooker";
        private const string HEIGHT_BREAK = "HeightBreak";

        void Start()
        {
            UpdateDataText();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!player.isLocal) return;

            if (PlayerData.HasKey(player, GAME_COUNT)) gameCount = PlayerData.GetInt(player, GAME_COUNT);
            if (PlayerData.HasKey(player, WIN_COUNT)) winCount = PlayerData.GetInt(player, WIN_COUNT);
            if (PlayerData.HasKey(player, LOSE_COUNT)) loseCount = PlayerData.GetInt(player, LOSE_COUNT);
            if (PlayerData.HasKey(player, POCKET_COUNT)) pocketCount = PlayerData.GetInt(player, POCKET_COUNT);
            if (PlayerData.HasKey(player, INNING_COUNT)) inningCount = PlayerData.GetInt(player, INNING_COUNT);
            if (PlayerData.HasKey(player, SHOT_COUNT)) shotCount = PlayerData.GetInt(player, SHOT_COUNT);
            if (PlayerData.HasKey(player, SCRATCH_COUNT)) scratchCount = PlayerData.GetInt(player, SCRATCH_COUNT);
            if (PlayerData.HasKey(player, FOUL_END)) foulEnd = PlayerData.GetInt(player, FOUL_END);
            if (PlayerData.HasKey(player, BREAK_FOUL)) breakFoul = PlayerData.GetInt(player, BREAK_FOUL);
            if (PlayerData.HasKey(player, FOUL_COUNT)) foulCount = PlayerData.GetInt(player, FOUL_COUNT);
            if (PlayerData.HasKey(player, LOSS_OF_CHANGE)) lossOfChange = PlayerData.GetInt(player, LOSS_OF_CHANGE);
            if (PlayerData.HasKey(player, GOLDEN_BREAK)) goldenBreak = PlayerData.GetInt(player, GOLDEN_BREAK);
            if (PlayerData.HasKey(player, CLEARANCE)) clearance = PlayerData.GetInt(player, CLEARANCE);
            if (PlayerData.HasKey(player, BREAK_CLEARANCE)) breakClearance = PlayerData.GetInt(player, BREAK_CLEARANCE);
            //snooker
            if (PlayerData.HasKey(player, GAME_COUNT_SNOOKER)) gameCountSnooker = PlayerData.GetInt(player, GAME_COUNT_SNOOKER);
            if (PlayerData.HasKey(player, POCKET_COUNT_SNOOKER)) pocketCountSnooker = PlayerData.GetInt(player, POCKET_COUNT_SNOOKER);
            if (PlayerData.HasKey(player, SHOT_COUNT_SNOOKER)) shotCountSnooker = PlayerData.GetInt(player, SHOT_COUNT_SNOOKER);
            if (PlayerData.HasKey(player, INNNING_COUNT_SNOOKER)) inningCountSnooker = PlayerData.GetInt(player, INNNING_COUNT_SNOOKER);
            if (PlayerData.HasKey(player, HEIGHT_BREAK)) heightBreak = PlayerData.GetInt(player, HEIGHT_BREAK);

            UpdateDataText();
        }

        // Method to save player data
        public void SaveData()
        {
            PlayerData.SetInt(GAME_COUNT, gameCount);
            PlayerData.SetInt(WIN_COUNT, winCount);
            PlayerData.SetInt(LOSE_COUNT, loseCount);
            PlayerData.SetInt(POCKET_COUNT, pocketCount);
            PlayerData.SetInt(INNING_COUNT, inningCount);
            PlayerData.SetInt(SHOT_COUNT, shotCount);
            PlayerData.SetInt(SCRATCH_COUNT, scratchCount);
            PlayerData.SetInt(FOUL_END, foulEnd);
            PlayerData.SetInt(BREAK_FOUL, breakFoul);
            PlayerData.SetInt(FOUL_COUNT, foulCount);
            PlayerData.SetInt(LOSS_OF_CHANGE, lossOfChange);
            PlayerData.SetInt(GOLDEN_BREAK, goldenBreak);
            PlayerData.SetInt(CLEARANCE, clearance);
            PlayerData.SetInt(BREAK_CLEARANCE, breakClearance);
            //snooker
            PlayerData.SetInt(GAME_COUNT_SNOOKER, gameCountSnooker);
            PlayerData.SetInt(POCKET_COUNT_SNOOKER, pocketCountSnooker);
            PlayerData.SetInt(SHOT_COUNT_SNOOKER, shotCountSnooker);
            PlayerData.SetInt(INNNING_COUNT_SNOOKER,inningCountSnooker);
            PlayerData.SetInt(HEIGHT_BREAK, heightBreak);

            UpdateDataText();

        }
        public void UpdateDataText()
        {
            DataText.text = string.Format(DataTextFormat, gameCount, winCount, loseCount, pocketCount, inningCount, shotCount, scratchCount, foulEnd, breakFoul,
                foulCount, lossOfChange, goldenBreak, clearance, breakClearance);

            float victoryRate = (gameCount != 0) ? (float)winCount / gameCount : 0;         //胜率
            float shotAccuracy = (inningCount != 0) ? (float)pocketCount / inningCount : 0; // 击球成功率，避免除数为零
            float potSuccess = (shotCount != 0) ? (float)pocketCount / shotCount : 0;         // 单杆进球率，避免除数为零
            float clearancePer = (gameCount != 0) ? (float)clearance / gameCount : 0;        // 一杆清台率，避免除数为零

            float shotAccuracySnooker = (inningCountSnooker != 0) ? (float)pocketCountSnooker / inningCountSnooker : 0;

            SnookerText.text = string.Format(SnookerTextFormat, gameCountSnooker, pocketCountSnooker, shotCountSnooker, inningCountSnooker,shotAccuracySnooker * 100, heightBreak);
            CalculatedDataText.text = string.Format(SecDataTextFormat, victoryRate * 100, shotAccuracy * 100, potSuccess, clearancePer * 100);
        }
    }

}