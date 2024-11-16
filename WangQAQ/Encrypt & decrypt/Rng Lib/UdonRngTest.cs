
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using WangQAQ.ED;

public class UdonRngTest : UdonSharpBehaviour
{
    public string Rng;

    void Start()
    {
        
    }

    public void Test()
    {
        Rng = UdonRng.GetRngSha256S();
    }
}
