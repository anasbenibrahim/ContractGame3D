using UnityEngine;

public class UIAssetBootstrap : MonoBehaviour
{
    private void Start()
    {
        // Find the Global Volume object and add the UI asset creator component
        var globalVolume = GameObject.Find("Global Volume");
        if (globalVolume != null)
        {
            globalVolume.AddComponent<RuntimeUIAssetCreator>();
        }
        else
        {
            Debug.LogWarning("[UIAssetBootstrap] Global Volume not found, UI assets will not be generated.");
        }
    }
}