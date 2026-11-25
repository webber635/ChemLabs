using UnityEngine;
using UnityEngine.Events;
using TMPro;



[RequireComponent(typeof(CharacterController))]
public class DesktopLabController : MonoBehaviour
{
    public enum DesktopState { Idle, HoldingBottle, Pouring, HoldingBeaker, Placing }
    public DesktopState currentState = DesktopState.Idle;

    [Header("Referensi Kamera & Titik Jangkar")]
    public Camera playerCamera;
    public Transform holdPoint;
    public Transform pourAnchor;
    public GameObject pourBluePreview;
    public Transform analyzerAnchor;

    [Header("Pengaturan Put State (Dynamic Ghost Preview)")]
    public Material previewMaterial;
    public LayerMask placeableSurfaceMask = ~0;
    [Range(0.5f, 1f)]
    public float minSurfaceUpNormal = 0.75f;

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

    [Header("Hook Kompatibilitas VR (Opsional)")]
    public UnityEvent<GameObject> onDesktopGrab;
    public UnityEvent<GameObject> onDesktopRelease;

    private CharacterController characterController;
    private Vector3 verticalVelocity;

    private GameObject heldObject;
    private Rigidbody heldObjRb;
    private Collider[] heldObjColliders;
    private BeakerContainer heldBeakerComponent;
    private DesktopState previousHoldingState;

    private GameObject dynamicGhostPreview;
    private float heldBottomOffset = 0f;

    private float cameraPitch = 0f;
    private float currentTiltAngle = 0f;

    private bool hasValidPlacement = false;
    private bool isSnappedToAnalyzer = false;
    private Vector3 targetPlacePos;
    private Quaternion targetPlaceRot;
    private AnalyzerSlot detectedAnalyzerSlot;

    [Header("Kamera Fokus Mode Tuang")]
    public Transform pouringCameraAnchor; // Opsional: titik kamera tetap menghadap Beaker (jika kosong, otomatis membidik Beaker)
    public float cameraLockSpeed = 8f;

    private Vector3 savedCamLocalPos;
    private Quaternion savedCamLocalRot;
    private Vector3 targetPourCamWorldPos;
    private Quaternion targetPourCamWorldRot;
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Start()
    {
        if (pourBluePreview != null) pourBluePreview.SetActive(false);
    }

    void Update()
    {
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
            case DesktopState.Placing:
                HandlePlacingState();
                break;
        }
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = (transform.right * moveX) + (transform.forward * moveZ);
        characterController.Move(move * moveSpeed * Time.deltaTime);

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
        DestroyGhostPreview();
        if (crosshairHintText != null) crosshairHintText.text = "";

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            // 1. Cek interaksi jika mengarah ke Slot Analyzer (atau Beaker yang sedang ada di dalam Slot)
            AnalyzerSlot hitSlot = hit.collider.GetComponent<AnalyzerSlot>() ??
                                   hit.collider.GetComponentInParent<AnalyzerSlot>() ??
                                   hit.collider.GetComponentInChildren<AnalyzerSlot>();

            // Jika mengenai Beaker yang berada di Analyzer, arahkan interaksi ke analyzerSlot
            if (hitSlot == null && hit.collider.GetComponent<BeakerContainer>() != null &&
                analyzerSlot != null && analyzerSlot.IsBusyOrOccupied)
            {
                // Cek apakah Beaker tersebut berada tepat di dekat analyzerAnchor
                if (analyzerAnchor != null && Vector3.Distance(hit.collider.transform.position, analyzerAnchor.position) < 0.5f)
                {
                    hitSlot = analyzerSlot;
                }
            }

            if (hitSlot != null && hitSlot.IsBusyOrOccupied)
            {
                if (crosshairHintText != null)
                    crosshairHintText.text = hitSlot.GetInteractionHint();

                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (hitSlot.currentState == AnalyzerSlot.SlotState.CoinReady)
                    {
                        hitSlot.CollectCoin();
                    }
                    else if (hitSlot.currentState == AnalyzerSlot.SlotState.BeakerReady)
                    {
                        BeakerContainer beaker = hitSlot.RetrieveBeaker();
                        if (beaker != null)
                        {
                            heldBeakerComponent = beaker;
                            PickUpObject(beaker.gameObject, DesktopState.HoldingBeaker);
                        }
                    }
                }
                return;
            }

            // 2. Cek Botol Kimia
            if (hit.collider.TryGetComponent(out PourDetector detector))
            {
                if (crosshairHintText != null)
                    crosshairHintText.text = $"[E] Ambil {detector.chemicalName}";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    PickUpObject(detector.gameObject, DesktopState.HoldingBottle);
                }
            }
            // 3. Cek Gelas Beaker di meja
            else if (hit.collider.TryGetComponent(out BeakerContainer beaker))
            {
                if (crosshairHintText != null)
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
        if (crosshairHintText != null)
            crosshairHintText.text = "[Q] Mode Letakkan Barang";

        if (Input.GetKeyDown(KeyCode.Q))
        {
            EnterPlacingState(DesktopState.HoldingBottle);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        bool aimingAtBeaker = Physics.Raycast(ray, out RaycastHit hit, interactRange) &&
                              hit.collider.GetComponent<BeakerContainer>() != null;

        if (pourBluePreview != null) pourBluePreview.SetActive(aimingAtBeaker);

        if (aimingAtBeaker)
        {
            if (crosshairHintText != null)
                crosshairHintText.text = "[Klik Kanan] Masuk Mode Tuang | [Q] Letakkan";

            if (Input.GetMouseButtonDown(1))
            {
                EnterPouringState();
            }
        }
    }

    private void HandlePouringState()
    {
        if (crosshairHintText != null)
            crosshairHintText.text = "[Scroll Wheel] Miringkan Botol | [Klik Kanan / E] Selesai Tuang";

        // Kunci dan arahkan kamera secara halus ke posisi fokus penuangan
        if (playerCamera != null)
        {
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position,
                targetPourCamWorldPos,
                Time.deltaTime * cameraLockSpeed
            );
            playerCamera.transform.rotation = Quaternion.Slerp(
                playerCamera.transform.rotation,
                targetPourCamWorldRot,
                Time.deltaTime * cameraLockSpeed
            );
        }

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
        if (crosshairHintText != null)
            crosshairHintText.text = "[Q] Mode Letakkan Barang";

        if (Input.GetKeyDown(KeyCode.Q))
        {
            EnterPlacingState(DesktopState.HoldingBeaker);
        }
    }

    private void EnterPlacingState(DesktopState fromState)
    {
        previousHoldingState = fromState;
        if (pourBluePreview != null) pourBluePreview.SetActive(false);

        CreateGhostPreviewFromHeldObject();
        currentState = DesktopState.Placing;
    }

    private void CreateGhostPreviewFromHeldObject()
    {
        DestroyGhostPreview();
        if (heldObject == null) return;

        dynamicGhostPreview = Instantiate(heldObject);
        dynamicGhostPreview.name = "DynamicGhostPreview";
        dynamicGhostPreview.transform.SetParent(null);
        dynamicGhostPreview.transform.localScale = heldObject.transform.lossyScale;

        foreach (var col in dynamicGhostPreview.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (var rb in dynamicGhostPreview.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        foreach (var canvas in dynamicGhostPreview.GetComponentsInChildren<Canvas>(true))
            Destroy(canvas.gameObject);

        foreach (var mb in dynamicGhostPreview.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);

        if (previewMaterial != null)
        {
            foreach (var mr in dynamicGhostPreview.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = previewMaterial;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        dynamicGhostPreview.SetActive(false);
    }

    private void DestroyGhostPreview()
    {
        if (dynamicGhostPreview != null)
        {
            Destroy(dynamicGhostPreview);
            dynamicGhostPreview = null;
        }
    }

    private void HandlePlacingState()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            DestroyGhostPreview();
            currentState = previousHoldingState;
            return;
        }

        hasValidPlacement = false;
        isSnappedToAnalyzer = false;
        detectedAnalyzerSlot = null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // 1. Jika membawa Beaker & diarahkan ke mesin Analyzer yang kosong
        if (heldBeakerComponent != null &&
            Physics.Raycast(ray, out RaycastHit triggerHit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            AnalyzerSlot slot = triggerHit.collider.GetComponent<AnalyzerSlot>() ??
                                triggerHit.collider.GetComponentInParent<AnalyzerSlot>() ??
                                triggerHit.collider.GetComponentInChildren<AnalyzerSlot>();

            if (slot != null && !slot.IsBusyOrOccupied)
            {
                isSnappedToAnalyzer = true;
                hasValidPlacement = true;
                detectedAnalyzerSlot = slot;

                if (dynamicGhostPreview != null)
                {
                    dynamicGhostPreview.SetActive(true);
                    if (analyzerAnchor != null)
                    {
                        dynamicGhostPreview.transform.position = analyzerAnchor.position + Vector3.up * heldBottomOffset;
                        dynamicGhostPreview.transform.rotation = analyzerAnchor.rotation;
                    }
                }

                if (crosshairHintText != null)
                    crosshairHintText.text = "[Klik Kiri] Masukkan ke Analyzer | [Q] Batal";

                if (Input.GetMouseButtonDown(0))
                {
                    ConfirmPlacement();
                }
                return;
            }
        }

        // 2. Letakkan di permukaan meja mana pun yang valid
        if (Physics.Raycast(ray, out RaycastHit surfaceHit, interactRange, placeableSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (surfaceHit.normal.y >= minSurfaceUpNormal)
            {
                hasValidPlacement = true;
                targetPlacePos = surfaceHit.point + (Vector3.up * heldBottomOffset);

                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                targetPlaceRot = flatForward.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(flatForward, Vector3.up)
                    : Quaternion.identity;

                if (dynamicGhostPreview != null)
                {
                    dynamicGhostPreview.SetActive(true);
                    dynamicGhostPreview.transform.position = targetPlacePos;
                    dynamicGhostPreview.transform.rotation = targetPlaceRot;
                }

                if (crosshairHintText != null)
                    crosshairHintText.text = "[Klik Kiri] Letakkan di Sini | [Q] Batal";

                if (Input.GetMouseButtonDown(0))
                {
                    ConfirmPlacement();
                }
                return;
            }
        }

        if (dynamicGhostPreview != null) dynamicGhostPreview.SetActive(false);
        if (crosshairHintText != null)
            crosshairHintText.text = "<color=yellow>Arahkan ke permukaan meja</color> | [Q] Batal";
    }

    private void ConfirmPlacement()
    {
        DestroyGhostPreview();

        if (isSnappedToAnalyzer && heldBeakerComponent != null)
        {
            analyzerSlot = detectedAnalyzerSlot;
            Vector3 seatedPos = analyzerAnchor != null ? analyzerAnchor.position + Vector3.up * heldBottomOffset : heldObject.transform.position;
            Quaternion seatedRot = analyzerAnchor != null ? analyzerAnchor.rotation : Quaternion.identity;

            heldObject.transform.SetParent(null);
            heldObject.transform.position = seatedPos;
            heldObject.transform.rotation = seatedRot;

            if (analyzerSlot != null)
            {
                analyzerSlot.InsertSample(heldBeakerComponent, seatedPos, seatedRot);
            }

            onDesktopRelease?.Invoke(heldObject);
            heldObject = null;
            heldBeakerComponent = null;
            currentState = DesktopState.Idle;
            return;
        }

        if (hasValidPlacement && heldObject != null)
        {
            heldObject.transform.SetParent(null);
            heldObject.transform.position = targetPlacePos;
            heldObject.transform.rotation = targetPlaceRot;

            if (heldObjColliders != null)
            {
                foreach (var col in heldObjColliders) col.enabled = true;
            }

            if (heldObjRb != null)
            {
                heldObjRb.isKinematic = false;
                heldObjRb.velocity = Vector3.zero;
                heldObjRb.angularVelocity = Vector3.zero;
            }

            onDesktopRelease?.Invoke(heldObject);
            heldObject = null;
            heldBeakerComponent = null;
            currentState = DesktopState.Idle;
        }
    }

    private void PickUpObject(GameObject targetObj, DesktopState nextState)
    {
        heldObject = targetObj;

        if (heldObject.TryGetComponent(out heldObjRb)) heldObjRb.isKinematic = true;

        heldObject.transform.SetParent(null);
        heldObject.transform.rotation = Quaternion.identity;

        heldObjColliders = heldObject.GetComponentsInChildren<Collider>();
        CalculateBottomOffset();

        foreach (var col in heldObjColliders)
        {
            col.enabled = false;
        }

        onDesktopGrab?.Invoke(heldObject);

        heldObject.transform.SetParent(holdPoint);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;

        currentState = nextState;
    }

    private void CalculateBottomOffset()
    {
        heldBottomOffset = 0f;
        float lowestY = float.MaxValue;
        bool foundMesh = false;

        MeshRenderer[] renderers = heldObject.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in renderers)
        {
            if (!mr.enabled) continue;
            if (mr.bounds.min.y < lowestY)
            {
                lowestY = mr.bounds.min.y;
                foundMesh = true;
            }
        }

        if (foundMesh)
        {
            heldBottomOffset = Mathf.Max(0f, heldObject.transform.position.y - lowestY);
        }
    }

    

    private void EnterPouringState()
    {
        if (pourBluePreview != null) pourBluePreview.SetActive(false);

        // Simpan posisi & rotasi lokal kamera sebelum dikunci
        if (playerCamera != null)
        {
            savedCamLocalPos = playerCamera.transform.localPosition;
            savedCamLocalRot = playerCamera.transform.localRotation;

            if (pouringCameraAnchor != null)
            {
                targetPourCamWorldPos = pouringCameraAnchor.position;
                targetPourCamWorldRot = pouringCameraAnchor.rotation;
            }
            else if (pourAnchor != null)
            {
                // Otomatis posisikan kamera sedikit di belakang & atas pourAnchor menghadap area tuang/Beaker
                Vector3 viewOffset = (-transform.forward * 0.45f) + (Vector3.up * 0.15f);
                targetPourCamWorldPos = pourAnchor.position + viewOffset;
                Vector3 lookTarget = pourAnchor.position - (Vector3.up * 0.12f);
                targetPourCamWorldRot = Quaternion.LookRotation((lookTarget - targetPourCamWorldPos).normalized, Vector3.up);
            }
        }

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

        // Kembalikan kamera persis ke posisi & sudut pandang FPS semula
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = savedCamLocalPos;
            playerCamera.transform.localRotation = savedCamLocalRot;
        }

        currentState = DesktopState.HoldingBottle;
    }
}