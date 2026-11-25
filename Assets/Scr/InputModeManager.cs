using UnityEngine;
using UnityEngine.Events;

public class InputModeManager : MonoBehaviour
{
    [Header("Player Rigs")]
    public GameObject desktopPlayerRig;
    public GameObject vrOriginRig; // Biarkan kosong sementara; isi saat menambahkan VR di akhir

    [Header("Pengaturan Awal")]
    public bool startInVRMode = false;

    [Header("Event Pergantian Mode")]
    public UnityEvent<bool> onInputModeChanged; // true = VR, false = Desktop

    private bool isVRMode;

    void Start()
    {
        // Otomatis jatuh ke Desktop jika vrOriginRig belum dipasang
        bool useVR = startInVRMode && (vrOriginRig != null);
        SetInputMode(useVR);
    }

    void Update()
    {
        // F1 hanya berpindah ke VR jika vrOriginRig sudah terpasang di scene
        if (Input.GetKeyDown(KeyCode.F1) && vrOriginRig != null)
        {
            SetInputMode(!isVRMode);
        }
    }

    public void SetInputMode(bool useVR)
    {
        isVRMode = useVR && (vrOriginRig != null);

        if (desktopPlayerRig != null) desktopPlayerRig.SetActive(!isVRMode);
        if (vrOriginRig != null) vrOriginRig.SetActive(isVRMode);

        Cursor.lockState = isVRMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isVRMode;

        onInputModeChanged?.Invoke(isVRMode);
        Debug.Log($"Mode Input Aktif: {(isVRMode ? "VR" : "Desktop")}");
    }

    public void SwitchToDesktop() => SetInputMode(false);
    public void SwitchToVR() => SetInputMode(true);
}