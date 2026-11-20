using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class DesktopLabController : MonoBehaviour
{
    public enum DesktopState { Idle, HoldingBottle, Pouring, HoldingBeaker }
    public DesktopState currentState = DesktopState.Idle;

    [Header("Referensi Kamera & Titik Jangkar")]
    public Camera playerCamera;
    public Transform holdPoint;              // Posisi tangan kanan di depan kamera
    public Transform pourAnchor;             // Titik statis di atas Beaker saat menuang
    public GameObject pourBluePreview;       // Visual siluet biru di atas Beaker
    public Transform analyzerAnchor;         // Titik dudukan Beaker di dalam slot Analyzer
    public GameObject analyzerBluePreview;   // Visual siluet biru di slot Analyzer (opsional)

    [Header("Referensi Objek Lab")]
    public AnalyzerSlot analyzerSlot;

    [Header("Pengaturan Gerak & Kamera")]
    public float moveSpeed = 3.0f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 2.0f;

    [Header("Pengaturan Interaksi")]
    public float interactRange = 3.0f;
    public float scrollTiltSpeed = 120f;
    public TextMeshProUGUI crosshairHintText;

    private CharacterController characterController;
    private Vector3 verticalVelocity;

    private GameObject heldObject;
    private Rigidbody heldObjRb;
    private XRGrabInteractable heldObjXR;
    private Vector3 objOriginPos;
    private Quaternion objOriginRot;
    private BeakerContainer heldBeakerComponent;

    private float cameraPitch = 0f;
    private float currentTiltAngle = 0f;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Start()
    {
        if (pourBluePreview != null) pourBluePreview.SetActive(false);
        if (analyzerBluePreview != null) analyzerBluePreview.SetActive(false);
    }

    void Update()
    {
        // Pemain hanya bisa berjalan dan menoleh saat tidak sedang fokus menuang
        if (currentState != DesktopState.Pouring)
        {
            HandleMovement();
            HandleMouseLook();
        }

        switch (currentState)
        {
            case DesktopState.Idle:
                HandleIdleState();
                break;
            case DesktopState.HoldingBottle:
                HandleHoldingBottleState();
                break;
            case DesktopState.Pouring:
                HandlePouringState();
                break;
            case DesktopState.HoldingBeaker:
                HandleHoldingBeakerState();
                break;
        }
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal"); // A & D
        float moveZ = Input.GetAxis("Vertical");   // W & S

        Vector3 move = (transform.right * moveX) + (transform.forward * moveZ);
        characterController.Move(move * moveSpeed * Time.deltaTime);

        // Terapkan gravitasi ringan agar karakter tetap menapak di lantai lab
        if (characterController.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -75f, 75f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleIdleState()
    {
        if (pourBluePreview != null) pourBluePreview.SetActive(false);
        if (analyzerBluePreview != null) analyzerBluePreview.SetActive(false);
        crosshairHintText.text = "";

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // 1. Arahkan ke Botol Kimia -> [E] Ambil
            if (hit.collider.TryGetComponent(out PourDetector detector))
            {
                crosshairHintText.text = $"[E] Ambil {detector.chemicalName}";
                if (Input.GetKeyDown(KeyCode.E))
                {
                    PickUpObject(detector.gameObject, DesktopState.HoldingBottle);
                }
            }
            // 2. Arahkan ke Gelas Beaker -> [E] Ambil Beaker | [R] Kosongkan Isi
            else if (hit.collider.TryGetComponent(out BeakerContainer beaker))
            {
                crosshairHintText.text = "[E] Ambil Gelas Beaker | [R] Buang Isi";
                if (Input.GetKeyDown(KeyCode.E))
                {
                    heldBeakerComponent = beaker;
                    PickUpObject(beaker.gameObject, DesktopState.HoldingBeaker);
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    beaker.ClearBeaker();
                }
            }
        }
    }

    private void HandleHoldingBottleState()
    {
        crosshairHintText.text = "[Q] Kembalikan Botol ke Meja";

        if (Input.GetKeyDown(KeyCode.Q))
        {
            ReturnObjectToOrigin();
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        bool aimingAtBeaker = Physics.Raycast(ray, out RaycastHit hit, interactRange) &&
                              hit.collider.GetComponent<BeakerContainer>() != null;

        if (pourBluePreview != null) pourBluePreview.SetActive(aimingAtBeaker);

        if (aimingAtBeaker)
        {
            crosshairHintText.text = "[Klik Kanan] Masuk Mode Tuang";
            if (Input.GetMouseButtonDown(1))
            {
                EnterPouringState();
            }
        }
    }

    private void HandlePouringState()
    {
        crosshairHintText.text = "[Scroll Wheel] Miringkan Botol | [Klik Kanan / E] Selesai Tuang";

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentTiltAngle = Mathf.Clamp(currentTiltAngle + (scroll * scrollTiltSpeed * Time.deltaTime * 10f), 0f, 120f);
            heldObject.transform.localRotation = Quaternion.Euler(0f, 0f, currentTiltAngle);
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
        {
            ExitPouringState();
        }
    }

    private void HandleHoldingBeakerState()
    {
        crosshairHintText.text = "[Q] Kembalikan Beaker ke Meja";

        if (Input.GetKeyDown(KeyCode.Q))
        {
            heldBeakerComponent = null;
            ReturnObjectToOrigin();
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Gunakan QueryTriggerInteraction.Collide agar Collider Trigger (seperti Socket VR) tetap terbaca
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            // Cek AnalyzerSlot di objek yang kena hit, induknya, atau anak-anaknya
            AnalyzerSlot detectedSlot = hit.collider.GetComponent<AnalyzerSlot>() ??
                                        hit.collider.GetComponentInParent<AnalyzerSlot>() ??
                                        hit.collider.GetComponentInChildren<AnalyzerSlot>();

            if (detectedSlot != null)
            {
                if (analyzerBluePreview != null) analyzerBluePreview.SetActive(true);
                crosshairHintText.text = "[Klik Kanan / E] Taruh ke Slot Analyzer";

                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
                {
                    analyzerSlot = detectedSlot;
                    PlaceBeakerInAnalyzer();
                }
                return;
            }
            else
            {
                // Log sementara untuk melihat objek apa yang menghalangi crosshair
                Debug.Log("Crosshair mengenai: " + hit.collider.gameObject.name);
            }
        }

        if (analyzerBluePreview != null) analyzerBluePreview.SetActive(false);
    }

    private Collider[] heldObjColliders; // Tambahkan variabel ini di bagian atas bersama heldObjRb

    private void PickUpObject(GameObject targetObj, DesktopState nextState)
    {
        heldObject = targetObj;
        objOriginPos = targetObj.transform.position;
        objOriginRot = targetObj.transform.rotation;

        if (heldObject.TryGetComponent(out heldObjRb)) heldObjRb.isKinematic = true;
        if (heldObject.TryGetComponent(out heldObjXR)) heldObjXR.enabled = false;

        // Matikan semua collider objek yang dipegang agar tidak menghalangi Raycast crosshair
        heldObjColliders = heldObject.GetComponentsInChildren<Collider>();
        foreach (var col in heldObjColliders)
        {
            col.enabled = false;
        }

        heldObject.transform.SetParent(holdPoint);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;

        currentState = nextState;
    }

    private void PlaceBeakerInAnalyzer()
    {
        if (analyzerBluePreview != null) analyzerBluePreview.SetActive(false);

        if (analyzerAnchor != null)
        {
            heldObject.transform.SetParent(analyzerAnchor);
            heldObject.transform.localPosition = Vector3.zero;
            heldObject.transform.localRotation = Quaternion.identity;
        }

        // Nyalakan kembali collider Beaker setelah ditaruh di Analyzer
        if (heldObjColliders != null)
        {
            foreach (var col in heldObjColliders) col.enabled = true;
        }

        if (analyzerSlot != null && heldBeakerComponent != null)
        {
            analyzerSlot.TriggerDesktopAnalysis(heldBeakerComponent);
        }

        if (heldObjXR != null) heldObjXR.enabled = true;
        heldObject = null;
        heldBeakerComponent = null;
        currentState = DesktopState.Idle;
    }

    private void ReturnObjectToOrigin()
    {
        heldObject.transform.SetParent(null);
        heldObject.transform.position = objOriginPos;
        heldObject.transform.rotation = objOriginRot;

        // Nyalakan kembali collider saat dikembalikan ke meja
        if (heldObjColliders != null)
        {
            foreach (var col in heldObjColliders) col.enabled = true;
        }

        if (heldObjRb != null) heldObjRb.isKinematic = false;
        if (heldObjXR != null) heldObjXR.enabled = true;

        heldObject = null;
        if (pourBluePreview != null) pourBluePreview.SetActive(false);
        if (analyzerBluePreview != null) analyzerBluePreview.SetActive(false);
        currentState = DesktopState.Idle;
    }

    private void EnterPouringState()
    {
        if (pourBluePreview != null) pourBluePreview.SetActive(false);

        heldObject.transform.SetParent(pourAnchor);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        currentTiltAngle = 0f;

        currentState = DesktopState.Pouring;
    }

    private void ExitPouringState()
    {
        currentTiltAngle = 0f;
        heldObject.transform.SetParent(holdPoint);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;

        currentState = DesktopState.HoldingBottle;
    }
}