using UnityEngine;

/// <summary>
/// Hides the dev/overview cameras when the scene enters play mode so only the
/// player's camera renders. Re-enables them automatically when play mode exits.
/// </summary>
public class DevCameraManager : MonoBehaviour
{
    private string[] devCameraNames = { "Main Camera", "OverviewCamera" };
    private GameObject[] devCams;
    private bool[] wasActive;

    void OnEnable()
    {
        RefreshDevCameras();
    }

    void RefreshDevCameras()
    {
        devCams = new GameObject[devCameraNames.Length];
        wasActive = new bool[devCameraNames.Length];
        for (int i = 0; i < devCameraNames.Length; i++)
        {
            var go = GameObject.Find(devCameraNames[i]);
            devCams[i] = go;
            wasActive[i] = go != null && go.activeInHierarchy;
        }
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            for (int i = 0; i < devCams.Length; i++)
                if (devCams[i] != null && devCams[i].activeInHierarchy)
                    devCams[i].SetActive(false);
        }
        else
        {
            for (int i = 0; i < devCams.Length; i++)
                if (devCams[i] != null && !devCams[i].activeInHierarchy && wasActive[i])
                    devCams[i].SetActive(true);
        }
    }
}
