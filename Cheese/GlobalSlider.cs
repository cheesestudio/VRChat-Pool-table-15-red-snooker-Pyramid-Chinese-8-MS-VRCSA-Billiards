
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class GlobalSlider : UdonSharpBehaviour
{
    public Material mat;
	private float localValue;
	private Slider slider;
	private VRCPlayerApi localPlayer;

    private void Start()
    {
        slider = transform.GetComponent<Slider>();
    }


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
