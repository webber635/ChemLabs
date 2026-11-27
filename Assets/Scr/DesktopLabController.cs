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
    public Transform holdPoint;              // Posisi tangan kanan di depan kamera
    public Transform pourAnchor;             // Titik jangkar botol saat menuang (akan diposisikan otomatis di atas Beaker)
    public GameObject pourBluePreview;       // Visual siluet biru di atas Beaker saat mau menuang
    public Transform analyzerAnchor;         // Titik dudukan Beaker di dalam slot Analyzer

    [Header("Pengaturan Kamera & Posisi Tuang Dinamis")]
    public float pourBottleHeightOffset = 0.28f; // Tinggi mulut botol di atas titik dasar Beaker
    public float pourBottleSideOffset = 0.12f;   // Geser botol sedikit ke kanan agar tidak menutupi layar
    public float pourCamDistance = 0.55f;        // Jarak kamera dari Beaker saat mode tuang
    public float pourCamHeight = 0.35f;          // Tinggi kamera dari dasar Beaker saat mode tuang
    public float cameraLockSpeed = 10f;

    [Header("Pengaturan Put State (Dynamic Ghost Preview)")]
    public Material previewMaterial;         // Masukkan Mat_BluePreview di sini
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

    // Tambahkan di bagian variabel private atas:
    private bool isReturningCamera = false;
    private CharacterController characterController;
    private Vector3 verticalVelocity;

    private GameObject heldObject;
    private Rigidbody heldObjRb;
    private Collider[] heldObjColliders;
    private BeakerContainer heldBeakerComponent;
    private BeakerContainer targetPourBeaker; // Beaker yang sedang dibidik untuk dituangi
    private DesktopState previousHoldingState;

    private GameObject dynamicGhostPreview;
    private float heldBottomOffset = 0f;

    private float cameraPitch = 0f;
    private float currentTiltAngle = 0f;

    // Variabel kunci kamera saat Pouring
    private Vector3 savedCamLocalPos;
    private Quaternion savedCamLocalRot;
    private Vector3 targetPourCamWorldPos;
    private Quaternion targetPourCamWorldRot;

    private bool hasValidPlacement = false;
    private bool isSnappedToAnalyzer = false;
    private Vector3 targetPlacePos;
    private Quaternion targetPlaceRot;
    private AnalyzerSlot detectedAnalyzerSlot;

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

            if (isReturningCamera)
            {
                HandleCameraReturnLerp();
            }
            else
            {
                HandleMouseLook();
            }
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

    private void HandleCameraReturnLerp()
    {
        if (playerCamera == null)
        {
            isReturningCamera = false;
            return;
        }

        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            savedCamLocalPos,
            Time.deltaTime * cameraLockSpeed
        );

        playerCamera.transform.localRotation = Quaternion.Slerp(
            playerCamera.transform.localRotation,
            savedCamLocalRot,
            Time.deltaTime * cameraLockSpeed
        );

        // Hentikan lerp dan kembalikan kontrol mouse penuh saat posisi & rotasi sudah rapat
        if (Vector3.Distance(playerCamera.transform.localPosition, savedCamLocalPos) < 0.005f &&
            Quaternion.Angle(playerCamera.transform.localRotation, savedCamLocalRot) < 0.5f)
        {
            playerCamera.transform.localPosition = savedCamLocalPos;
            playerCamera.transform.localRotation = savedCamLocalRot;
            isReturningCamera = false;
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
        if (playerCamera == null) return;

        if (pourBluePreview != null) pourBluePreview.SetActive(false);
        DestroyGhostPreview();
        if (crosshairHintText != null) crosshairHintText.text = "";

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider == null) return;

            // 1. Cek interaksi jika mengarah ke Slot Analyzer (atau Beaker yang sedang berada di dalam Slot)
            AnalyzerSlot hitSlot = hit.collider.GetComponent<AnalyzerSlot>() ??
                                   hit.collider.GetComponentInParent<AnalyzerSlot>() ??
                                   hit.collider.GetComponentInChildren<AnalyzerSlot>();

            BeakerContainer hitBeaker = hit.collider.GetComponent<BeakerContainer>() ??
                                        hit.collider.GetComponentInParent<BeakerContainer>();

            if (hitSlot == null && hitBeaker != null && analyzerSlot != null && analyzerSlot.IsBusyOrOccupied)
            {
                if (analyzerAnchor != null && Vector3.Distance(hitBeaker.transform.position, analyzerAnchor.position) < 0.5f)
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

            // 2. Cek interaksi dengan Monitor Komputer LabOS
            LabOSInteractable computer = hit.collider.GetComponent<LabOSInteractable>() ??
                                         hit.collider.GetComponentInParent<LabOSInteractable>();
            if (computer != null)
            {
                if (crosshairHintText != null)
                    crosshairHintText.text = "[E] Buka Komputer LabOS";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    computer.EnterComputer();
                }
                return;
            }

            // 3. Cek Botol Kimia (mendukung Collider pada parent maupun child)
            PourDetector detector = hit.collider.GetComponent<PourDetector>() ??
                                    hit.collider.GetComponentInParent<PourDetector>();
            if (detector != null)
            {
                if (crosshairHintText != null)
                    crosshairHintText.text = $"[E] Ambil {detector.chemicalName}";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    PickUpObject(detector.gameObject, DesktopState.HoldingBottle);
                }
                return;
            }

            // 4. Cek Gelas Beaker di meja (mendukung Collider pada parent maupun child)
            if (hitBeaker != null)
            {
                if (crosshairHintText != null)
                    crosshairHintText.text = "[E] Ambil Gelas Beaker | [R] Buang Isi";

                if (Input.GetKeyDown(KeyCode.E))
                {
                    heldBeakerComponent = hitBeaker;
                    PickUpObject(hitBeaker.gameObject, DesktopState.HoldingBeaker);
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    hitBeaker.ClearBeaker();
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

        targetPourBeaker = null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            targetPourBeaker = hit.collider.GetComponent<BeakerContainer>() ??
                               hit.collider.GetComponentInParent<BeakerContainer>();
        }

        bool aimingAtBeaker = targetPourBeaker != null;

        if (aimingAtBeaker)
        {
            // Hitung posisi dinamis pourAnchor & preview berdasarkan arah datang pemain
            CalculateDynamicPourPose(targetPourBeaker, out Vector3 anchorPos, out Quaternion anchorRot, out _, out _);

            if (pourAnchor != null)
            {
                pourAnchor.position = anchorPos;
                pourAnchor.rotation = anchorRot;
            }

            if (pourBluePreview != null)
            {
                pourBluePreview.SetActive(true);
                pourBluePreview.transform.position = anchorPos;
                pourBluePreview.transform.rotation = anchorRot;
            }

            if (crosshairHintText != null)
                crosshairHintText.text = "[Klik Kanan] Masuk Mode Tuang | [Q] Letakkan";

            if (Input.GetMouseButtonDown(1))
            {
                EnterPouringState();
            }
        }
        else
        {
            if (pourBluePreview != null) pourBluePreview.SetActive(false);
        }
    }

    /// <summary>
    /// Menghitung posisi botol dan kamera secara dinamis dari sisi pemain mana pun menghadap Beaker.
    /// </summary>
    private void CalculateDynamicPourPose(
        BeakerContainer beaker,
        out Vector3 bottleWorldPos,
        out Quaternion bottleWorldRot,
        out Vector3 camWorldPos,
        out Quaternion camWorldRot)
    {
        Vector3 beakerBasePos = beaker.transform.position;

        // Vektor arah horizontal dari Beaker menuju posisi pemain saat ini
        Vector3 dirToPlayer = Vector3.ProjectOnPlane(transform.position - beakerBasePos, Vector3.up);
        if (dirToPlayer.sqrMagnitude < 0.001f)
            dirToPlayer = -transform.forward;
        else
            dirToPlayer.Normalize();

        // Arah kanan dari sudut pandang pemain saat melihat Beaker
        Vector3 rightFromPlayerView = Vector3.Cross(Vector3.up, -dirToPlayer).normalized;

        // Posisi botol: di atas Beaker & sedikit di sisi kanan layar pemain
        bottleWorldPos = beakerBasePos + (Vector3.up * pourBottleHeightOffset) + (rightFromPlayerView * pourBottleSideOffset);
        // Rotasi botol menghadap ke arah Beaker (-dirToPlayer) sehingga miring Z positif selalu menuang ke kiri (ke dalam Beaker)
        bottleWorldRot = Quaternion.LookRotation(-dirToPlayer, Vector3.up);

        // Posisi kamera: di sisi pemain menghadap titik tengah antara Beaker dan mulut botol
        camWorldPos = beakerBasePos + (dirToPlayer * pourCamDistance) + (Vector3.up * pourCamHeight);
        Vector3 lookTarget = beakerBasePos + (Vector3.up * (pourBottleHeightOffset * 0.6f));
        camWorldRot = Quaternion.LookRotation((lookTarget - camWorldPos).normalized, Vector3.up);
    }

    private void HandlePouringState()
    {
        if (crosshairHintText != null)
            crosshairHintText.text = "[Scroll Wheel] Miringkan Botol | [Klik Kanan / E] Selesai Tuang";

        // Transisi halus & kunci kamera ke posisi fokus tuang
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
            currentTiltAngle = Mathf.Clamp(currentTiltAngle + (scroll * scrollTiltSpeed * Time.deltaTime * 10f), 0f, 110f);
            // Miringkan ke sumbu Z positif (ke arah kiri/dalam Beaker relatif terhadap kamera)
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
        if (targetPourBeaker == null) return;

        isReturningCamera = false;

        CalculateDynamicPourPose(
            targetPourBeaker,
            out Vector3 bottleWorldPos,
            out Quaternion bottleWorldRot,
            out targetPourCamWorldPos,
            out targetPourCamWorldRot
        );

        if (pourAnchor != null)
        {
            pourAnchor.position = bottleWorldPos;
            pourAnchor.rotation = bottleWorldRot;
            heldObject.transform.SetParent(pourAnchor);
        }
        else
        {
            heldObject.transform.SetParent(null);
            heldObject.transform.position = bottleWorldPos;
            heldObject.transform.rotation = bottleWorldRot;
        }

        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        currentTiltAngle = 0f;

        if (playerCamera != null)
        {
            savedCamLocalPos = playerCamera.transform.localPosition;
            savedCamLocalRot = playerCamera.transform.localRotation;
        }

        currentState = DesktopState.Pouring;
    }

    private void ExitPouringState()
    {
        currentTiltAngle = 0f;
        heldObject.transform.SetParent(holdPoint);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;

        // Mulai transisi lerp halus kembali ke posisi kepala FPS
        isReturningCamera = true;
        currentState = DesktopState.HoldingBottle;
    }
}