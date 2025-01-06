
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class ColorNameMag : UdonSharpBehaviour
{

    /// <summary>
    /// 2024/9/27
    /// By WangQAQ
    /// </summary>

    /// <summary>
    /// API 提供到台球彩色名称
    /// </summary>
    [HideInInspector] public String inOwner;
    [HideInInspector] public String outColor;

    //用于本地添加名称和颜色
    [Header("Override")]
    [SerializeField] public string[] ColorName = null;
    [SerializeField] public string[] ColorList = null;

    //指向ColorDownload获取彩色名称
    public ColorDownload ColorDOW;

    void Start()
    {
        
    }

    //API 提供到台球彩色名称 （获取彩名）
    public void _GetNameColor()
    {
        //从ColorDownload获取的颜色代码
        string colorList = "";

        if (ColorDOW != null)
        {
            colorList = ColorDOW.GetColorColor(inOwner);
        }
        else
        {
            //尝试从世界查找Download 脚本
            ColorDOW = GameObject.Find("ColorDownloader").GetComponent<ColorDownload>();

            //如果找到则从ColorDownload获取
            if (ColorDOW != null)
            {
                colorList = ColorDOW.GetColorColor(inOwner);
            }
        }

        //查询在线彩色名称

        if (colorList != null && colorList != "")
        {
            outColor = colorList;
            return;
        }

        //查询本地彩色名称

        for (int i = 0; i < ColorName.Length; i++)
        {
            if (inOwner == ColorName[i])
            {
                //查找本地彩色名称中是否含有颜色参数
                if (ColorList != null && ColorList.Length > i)
                {
                    if (ColorList[i] != null)
                    {
                        outColor = ColorList[i];
                        return;
                    }
                }

                outColor = "rainbow";
                return;
            }
        }

        //查询不到，返回白色
        outColor = "FFFFFF";
    }

}
