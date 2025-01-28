
using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

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
        [SerializeField] private TextMeshProUGUI TextMiddle;

        public void AddData(string name, string data, string rate = "empty")
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            var index = playersName.IndexOf(name);
            if (index == -1)
            {
                playersName.Add(name);
                playersData.Add(data);
                if (rate != "empty")
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
                            winRate[j + 1] = tempRate;

                            swapped = true;  // 标记发生了交换
                        }
                        // 如果前面的数据等于后面的数据，按 winrate 进行次级排序
                        else if (currentData == nextData)
                        {
                            // 尝试将 winRate[j] 和 winRate[j + 1] 转换为 int
                            if (int.TryParse((string)winRate[j], out int currentWinRate) && int.TryParse((string)winRate[j + 1], out int nextWinRate))
                            {
                                // 如果前面的 winrate 小于后面的 winrate，交换
                                if (currentWinRate < nextWinRate)
                                {
                                    // 交换 winrate
                                    string tempRate = (string)winRate[j];
                                    winRate[j] = winRate[j + 1];
                                    winRate[j + 1] = tempRate;

                                    // 交换 playersData
                                    string tempData = (string)playersData[j];
                                    playersData[j] = playersData[j + 1];
                                    playersData[j + 1] = tempData;

                                    // 交换 playersName
                                    string tempName = (string)playersName[j];
                                    playersName[j] = playersName[j + 1];
                                    playersName[j + 1] = tempName;

                                    swapped = true;  // 标记发生了交换
                                }
                            }
                        }
                    }
                    else
                    {
                        // 处理无法转换为 int 的情况
                        Debug.Log("无法转换数据为 int: " + playersData[j] + " 或 " + playersData[j + 1]);
                    }
                }

                // 如果没有发生交换，提前退出排序
                //if (!swapped)
                //{
                //    break;
                //}
            }




            DataList allData = new DataList();
            allData.Add(playersName);
            allData.Add(playersData);
            allData.Add(winRate);
            if (VRCJson.TrySerializeToJson(allData, JsonExportType.Minify, out DataToken exportToken))
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
            if (Networking.IsOwner(gameObject))
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
            Text.text = " 玩家名\t一杆清台次数\t胜率\n\n"; //+ Networking.GetOwner(gameObject).displayName + syncData + "\n";
            TextMiddle.text = "\n\n";
            StringBuilder sb = new StringBuilder();  // 使用StringBuilder来构建最终的字符串
            StringBuilder sb2 = new StringBuilder();

            for (int i = 0; i < playersData.Count; i++)
            {
                float.TryParse((string)winRate[i], out float win);
                string name = (string)playersName[i];
                name = name.Replace(" ", " ");
                name = (name.Length > 10 ? name.Substring(0, 10) : name);

                win *= 100;
                win = (float)Math.Round(win, 1);
                // 根据行数给每行添加不同的颜色到 sb
                if (i == 0)
                {
                    sb.AppendLine($"<color=#FF0000>{i + 1}.{name}\t\t\t{win}%</color>");  // 红色
                    sb2.AppendLine($"<color=#FF0000>{playersData[i]}</color>");  // 红色
                }
                else if (i == 1)
                {
                    sb.AppendLine($"<color=#0000FF>{i + 1}.{name}\t\t\t{win}%</color>");  // 蓝色
                    sb2.AppendLine($"<color=#0000FF>{playersData[i]}</color>");  // 蓝色
                }
                else if (i == 2)
                {
                    sb.AppendLine($"<color=#FFFF00>{i + 1}.{name}\t\t\t{win}%</color>");  // 黄色
                    sb2.AppendLine($"<color=#FFFF00>{playersData[i]}</color>");  // 黄色
                }
                else
                {
                    sb.AppendLine($"{i + 1}.{name}\t\t\t{win}%");  // 默认颜色
                    sb2.AppendLine($"{playersData[i]}");
                }

            }

            Text.text += sb.ToString();  // 将构建好的字符串设置为Text组件的文本
            TextMiddle.text += sb2.ToString();
        }

        //public void add()
        //{
        //    AddData("test", "2", "0.2222");
        //    AddData("te11st", "23", "0.423123");
        //    AddData("t123123124est", "2", "0.4123123");
        //    AddData("我非常森岛帆高啊啊发科技馆哈额日光古法森岛帆高电饭锅aSDG ", "1", "0.21234");
        //}
        void Enable()
        {
            Text.text = "初始化中";
        }

    }
}
