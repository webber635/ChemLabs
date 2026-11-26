using UnityEngine;

public class LabOSInteractable : MonoBehaviour
{
    [Header("Referensi Kamera & Layar")]
    public Transform monitorCameraAnchor;
    public float cameraTransitionSpeed = 10f;

    [Header("Referensi Controller Pemain")]
    public DesktopLabController playerController;

    private bool isUsingComputer = false;
    private bool isExitingComputer = false;
    private Camera mainCam;
    private Vector3 savedCamLocalPos;
    private Quaternion savedCamLocalRot;

    public bool IsInUse => isUsingComputer || isExitingComputer;

    void Start()
    {
        if (playerController != null)
            mainCam = playerController.playerCamera;
        else
            mainCam = Camera.main;
    }

    void Update()
    {
        // 1. Fase masuk & menggunakan komputer
        if (isUsingComputer)
        {
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

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
            {
                ExitComputer();
            }
            return;
        }

        // 2. Fase keluar dari komputer (Lerp mundur halus kembali ke posisi FPS)
        if (isExitingComputer && mainCam != null)
        {
            mainCam.transform.localPosition = Vector3.Lerp(
                mainCam.transform.localPosition,
                savedCamLocalPos,
                Time.deltaTime * cameraTransitionSpeed
            );
            mainCam.transform.localRotation = Quaternion.Slerp(
                mainCam.transform.localRotation,
                savedCamLocalRot,
                Time.deltaTime * cameraTransitionSpeed
            );

            if (Vector3.Distance(mainCam.transform.localPosition, savedCamLocalPos) < 0.005f &&
                Quaternion.Angle(mainCam.transform.localRotation, savedCamLocalRot) < 0.5f)
            {
                mainCam.transform.localPosition = savedCamLocalPos;
                mainCam.transform.localRotation = savedCamLocalRot;
                isExitingComputer = false;

                if (playerController != null)
                {
                    playerController.enabled = true;
                }
            }
        }
    }

    public void EnterComputer()
    {
        if (isUsingComputer || isExitingComputer || mainCam == null) return;

        isUsingComputer = true;

        savedCamLocalPos = mainCam.transform.localPosition;
        savedCamLocalRot = mainCam.transform.localRotation;

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
        isExitingComputer = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}