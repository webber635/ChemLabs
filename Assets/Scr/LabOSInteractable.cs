using UnityEngine;

public class LabOSInteractable : MonoBehaviour
{
    [Header("Referensi Kamera & Layar")]
    public Transform monitorCameraAnchor; // Titik kamera berdiri menghadap lurus ke layar monitor
    public float cameraTransitionSpeed = 10f;

    [Header("Referensi Controller Pemain")]
    public DesktopLabController playerController;

    private bool isUsingComputer = false;
    private Camera mainCam;
    private Vector3 savedCamLocalPos;
    private Quaternion savedCamLocalRot;

    public bool IsInUse => isUsingComputer;

    void Start()
    {
        if (playerController != null)
            mainCam = playerController.playerCamera;
        else
            mainCam = Camera.main;
    }

    void Update()
    {
        if (!isUsingComputer) return;

        // Transisi halus kamera menghadap layar monitor
        if (mainCam != null && monitorCameraAnchor != null)
        {
            mainCam.transform.position = Vector3.Lerp(
                mainCam.transform.position,
                monitorCameraAnchor.position,
                Time.deltaTime * cameraTransitionSpeed
            );
            mainCam.transform.rotation = Quaternion.Slerp(
                mainCam.transform.rotation,
                monitorCameraAnchor.rotation,
                Time.deltaTime * cameraTransitionSpeed
            );
        }

        // Tekan Escape atau Tab untuk keluar dari komputer
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            ExitComputer();
        }
    }

    public void EnterComputer()
    {
        if (isUsingComputer || mainCam == null) return;

        isUsingComputer = true;

        // Simpan posisi kamera FPS sebelum dikunci
        savedCamLocalPos = mainCam.transform.localPosition;
        savedCamLocalRot = mainCam.transform.localRotation;

        // Matikan sementara pergerakan & raycast FPS agar kursor bebas mengklik Canvas
        if (playerController != null)
        {
            playerController.enabled = false;
            if (playerController.crosshairHintText != null)
                playerController.crosshairHintText.text = "[ESC] Keluar dari LabOS";
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitComputer()
    {
        if (!isUsingComputer) return;

        isUsingComputer = false;

        if (mainCam != null)
        {
            mainCam.transform.localPosition = savedCamLocalPos;
            mainCam.transform.localRotation = savedCamLocalRot;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}