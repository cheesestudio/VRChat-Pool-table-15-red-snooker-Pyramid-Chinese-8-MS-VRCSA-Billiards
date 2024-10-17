
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GlobalSlider : UdonSharpBehaviour
{
    public Material mat;
	private float localValue;
	[HideInInspector]public Slider slider;

    private void Start()
    {
        localValue = 1;
        slider = transform.GetComponent<Slider>();
        mat.SetFloat("_ClothHue", 0);
        mat.SetFloat("_ClothSaturation", 1);
    }

    //public float GetSlideValue()
    //{ 
    //    return localValue;
    //}
    public void SlideUpdate()
    {
    	localValue = slider.value;
        mat.SetFloat("_ClothHue", localValue);
        //Debug.Log(slider.value);

    }
    public void SlideUpdateSaturation()
    {
        localValue = slider.value;
        mat.SetFloat("_ClothSaturation", localValue);
    }

}
