using UnityEngine;
using UnityEngine.XR;

public class InputModeManager : MonoBehaviour
{
    [Header("Player Rigs")]
    public GameObject vrOriginRig;
    public GameObject desktopPlayerRig;

    [Header("Pengaturan Mode (Pilih Salah Satu atau Kosongkan untuk Auto)")]
    public bool forceDesktopMode = false;
    public bool forceVRMode = false; // Centang ini saat tes XR Device Simulator di laptop

    [Header("Opsional: Simulator VR di Laptop")]
    public GameObject xrDeviceSimulator;

    private bool isVRMode;

    void Start()
    {
        bool headsetDetected = XRSettings.isDeviceActive;

        if (forceDesktopMode)
        {
            SetInputMode(false);
        }
        else if (forceVRMode || headsetDetected)
        {
            SetInputMode(true);
        }
        else
        {
            SetInputMode(false);
        }
    }

    void Update()
    {
        // Tekan F1 kapan saja saat Play untuk berpindah antara VR dan Desktop
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SetInputMode(!isVRMode);
        }
    }

    public void SetInputMode(bool useVR)
    {
        isVRMode = useVR;

        if (vrOriginRig != null) vrOriginRig.SetActive(isVRMode);
        if (desktopPlayerRig != null) desktopPlayerRig.SetActive(!isVRMode);

        // Nyalakan XR Device Simulator hanya jika di mode VR dan tidak pakai headset fisik
        if (xrDeviceSimulator != null)
        {
            xrDeviceSimulator.SetActive(isVRMode && !XRSettings.isDeviceActive);
        }

        Cursor.lockState = isVRMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isVRMode;

        Debug.Log($"Mode Input Aktif: {(isVRMode ? "VR" : "Desktop")}");
    }

    public void SwitchToDesktop() => SetInputMode(false);
    public void SwitchToVR() => SetInputMode(true);
}