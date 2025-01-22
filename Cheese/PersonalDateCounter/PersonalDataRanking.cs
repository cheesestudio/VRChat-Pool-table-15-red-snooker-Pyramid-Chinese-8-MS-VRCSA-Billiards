
using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace Cheese
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersonalDataRanking : UdonSharpBehaviour
    {
        [UdonSynced] public string syncData;
        private DataList playersName = new DataList();
        private DataList playersData = new DataList();
        private DataList winRate = new DataList();
        [SerializeField] private TextMeshProUGUI Text;

        public void AddData(string name,string data,string rate  = "empty")
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer,gameObject);

            var index = playersName.IndexOf(name);
            if( string.IsNullOrEmpty(syncData) && VRCPlayerApi.GetPlayerCount() == 1 || index == -1)
            {
                playersName.Add(name);
                playersData.Add(data);
                if(rate != "empty")
                    winRate.Add(rate);
            }
            else
            {
                playersData[index] = data;
                if (rate != "empty")
                    winRate[index] = rate;
            }

            _PreSerialization();
            RequestSerialization();
            FlashData();
        }
        public void _PreSerialization()
        {
            int n = playersData.Count;

            // 冒泡排序算法
            for (int i = 0; i < n - 1; i++)
            {
                bool swapped = false;  // 记录是否有交换发生
                for (int j = 0; j < n - 1 - i; j++)
                {
                    // 尝试将 playersData[j] 转换为 int
                    if (int.TryParse((string)playersData[j], out int currentData) && int.TryParse((string)playersData[j + 1], out int nextData))
                    {
                        // 如果前面的数据小于后面的数据，交换
                        if (currentData < nextData)
                        {
                            // 交换 playersData
                            string tempData = (string)playersData[j];
                            playersData[j] = playersData[j + 1];
                            playersData[j + 1] = tempData;

                            // 交换 playersName
                            string tempName = (string)playersName[j];
                            playersName[j] = playersName[j + 1];
                            playersName[j + 1] = tempName;

                            // 交换 winrate
                            string tempRate = (string)winRate[j];
                            winRate[j] = winRate[j + 1];
                            winRate[j+1] = tempRate;

                            swapped = true;  // 标记发生了交换
                        }
                    }
                    else
                    {
                        // 处理无法转换为 int 的情况
                        Debug.Log("无法转换数据为 int: " + playersData[j] + " 或 " + playersData[j + 1]);
                    }
                }

                // 如果没有发生交换，提前退出循环
                if (!swapped)
                {
                    break;
                }
            }



            DataList allData = new DataList();
            allData.Add(playersName);
            allData.Add(playersData);
            allData.Add(winRate);
            if(VRCJson.TrySerializeToJson(allData,JsonExportType.Minify,out DataToken exportToken))
            {
                syncData = exportToken.ToString();
            }
            else
            {
                Text.text = "TrySerializeToJson失败" + exportToken.ToString();
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if(Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }    
        }
        public override void OnDeserialization()
        {
            if (VRCJson.TryDeserializeFromJson(syncData, out DataToken token))
            {
                playersName = token.DataList[0].DataList;
                playersData = token.DataList[1].DataList;
                winRate = token.DataList[2].DataList;
                FlashData();
            }
            else
            {
                Text.text = "TryDeserializeFromJson失败" + token.ToString();
            }
        }

        public void FlashData()
        {
            Text.text = " 玩家名\t\t\t一杆清台次数\t\t胜率\n"; //+ Networking.GetOwner(gameObject).displayName + syncData + "\n";
            StringBuilder sb = new StringBuilder();  // 使用StringBuilder来构建最终的字符串

            for (int i = 0; i < playersData.Count; i++)
            {
                float.TryParse((string)winRate[i], out float win);
                win *= 100;
                sb.AppendLine($"{i + 1}.{playersName[i]}\t\t{playersData[i]}\t\t{win}%");
            }

            Text.text += sb.ToString();  // 将构建好的字符串设置为Text组件的文本
        }
        void Enable()
        {
            Text.text = "初始化中";
        }

    }
}
