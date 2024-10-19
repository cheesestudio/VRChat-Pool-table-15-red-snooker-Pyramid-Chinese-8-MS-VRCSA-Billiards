
using UdonSharp;
using UnityEngine;

public class ResetBotton : UdonSharpBehaviour
{
    [SerializeField] ScoreManagerV2 l_ScoreManager;

    void Start()
    {
        
    }

    public override void Interact()
    {
        if(l_ScoreManager != null)
        {
            l_ScoreManager.M_Score_Reset();
        }
    }
    
}
