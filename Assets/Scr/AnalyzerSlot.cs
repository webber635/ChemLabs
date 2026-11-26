using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AnalyzerSlot : MonoBehaviour
{
    public enum SlotState { Empty, Processing, CoinReady, BeakerReady }
    public SlotState currentState = SlotState.Empty;

    [Header("Referensi Titik & Animasi Mesin")]
    public Transform slotPlatform;         // Transform dudukan tempat Beaker berdiri (opsional, jika null menggerakkan Beaker langsung)
    public Transform coinSpawnOffset;      // Titik munculnya koin di dalam/atas Beaker (opsional)
    public GameObject coinPrefab;          // Prefab koin (jika kosong, otomatis dibuat silinder emas)
    public float sinkDepth = 0.35f;        // Jarak turun ke dalam mesin
    public float animDuration = 1.0f;      // Durasi turun & durasi naik

    [Header("Hook VR (Opsional)")]
    public UnityEvent<BeakerContainer> onSampleInserted;

    private BeakerContainer activeBeaker;
    private GameObject spawnedCoinObj;
    private int pendingCoinReward = 0;

    public bool IsBusyOrOccupied => currentState != SlotState.Empty;

    public void InsertSample(BeakerContainer beaker, Vector3 seatedWorldPos, Quaternion seatedWorldRot)
    {
        if (beaker == null || IsBusyOrOccupied) return;

        activeBeaker = beaker;
        onSampleInserted?.Invoke(beaker);

        // Kunci fisika & collider Beaker selama diproses mesin
        if (activeBeaker.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        foreach (var col in activeBeaker.GetComponentsInChildren<Collider>()) col.enabled = false;

        StartCoroutine(TransmutationRoutine(seatedWorldPos, seatedWorldRot));
    }

    // Overload kompatibilitas jika dipanggil langsung oleh VR XRSocketInteractor
    public void InsertSample(BeakerContainer beaker)
    {
        if (beaker == null) return;
        InsertSample(beaker, beaker.transform.position, beaker.transform.rotation);
    }

    private IEnumerator TransmutationRoutine(Vector3 topPos, Quaternion topRot)
    {
        currentState = SlotState.Processing;
        Vector3 bottomPos = topPos - (Vector3.up * sinkDepth);

        // 1. Beaker tenggelam masuk ke dalam mesin Analyzer
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            activeBeaker.transform.position = Vector3.Lerp(topPos, bottomPos, t);
            yield return null;
        }
        activeBeaker.transform.position = bottomPos;

        // 2. Hitung akurasi & harga, kosongkan cairan, munculkan koin di dalam Beaker
        if (RecipeLabManager.Instance != null)
        {
            pendingCoinReward = RecipeLabManager.Instance.EvaluateAccuracyAndPayout(activeBeaker, out _);
        }

        activeBeaker.ClearBeaker();
        SpawnCoinInsideBeaker();

        yield return new WaitForSeconds(0.25f);

        // 3. Beaker berisi koin naik kembali ke atas
        elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            activeBeaker.transform.position = Vector3.Lerp(bottomPos, topPos, t);
            yield return null;
        }
        activeBeaker.transform.position = topPos;
        activeBeaker.transform.rotation = topRot;

        // Nyalakan kembali collider agar bisa di-raycast tombol [E]
        foreach (var col in activeBeaker.GetComponentsInChildren<Collider>()) col.enabled = true;

        currentState = pendingCoinReward > 0 ? SlotState.CoinReady : SlotState.BeakerReady;
    }

    private void SpawnCoinInsideBeaker()
    {
        if (pendingCoinReward <= 0) return;

        Vector3 spawnPos = coinSpawnOffset != null
            ? coinSpawnOffset.position
            : activeBeaker.transform.position + Vector3.up * 0.08f;

        if (coinPrefab != null)
        {
            spawnedCoinObj = Instantiate(coinPrefab, spawnPos, Quaternion.Euler(90f, 0f, 0f), activeBeaker.transform);
        }
        else
        {
            // Buat koin primitif emas otomatis jika prefab belum dipasang
            spawnedCoinObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spawnedCoinObj.name = "TransmutedCoin";
            Destroy(spawnedCoinObj.GetComponent<Collider>());
            spawnedCoinObj.transform.SetParent(activeBeaker.transform);
            spawnedCoinObj.transform.position = spawnPos;
            spawnedCoinObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spawnedCoinObj.transform.localScale = new Vector3(0.08f, 0.012f, 0.08f);

            Renderer rend = spawnedCoinObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(1f, 0.84f, 0f); // Emas
            }
        }
    }

    void Update()
    {
        // Putar perlahan koin saat siap diambil
        if (currentState == SlotState.CoinReady && spawnedCoinObj != null)
        {
            spawnedCoinObj.transform.Rotate(Vector3.forward, 90f * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>
    /// Interaksi E ke-1: Mengambil koin.
    /// </summary>
    public void CollectCoin()
    {
        if (currentState != SlotState.CoinReady) return;

        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddCoins(pendingCoinReward);
        }

        if (spawnedCoinObj != null)
        {
            Destroy(spawnedCoinObj);
            spawnedCoinObj = null;
        }

        pendingCoinReward = 0;
        currentState = SlotState.BeakerReady;
    }

    /// <summary>
    /// Interaksi E ke-2: Melepas Beaker kembali ke tangan pemain.
    /// </summary>
    public BeakerContainer RetrieveBeaker()
    {
        if (currentState != SlotState.BeakerReady) return null;

        BeakerContainer retrieved = activeBeaker;
        activeBeaker = null;
        currentState = SlotState.Empty;
        return retrieved;
    }

    public string GetInteractionHint()
    {
        switch (currentState)
        {
            case SlotState.Processing:
                return "Mesin sedang menganalisis sampel...";
            case SlotState.CoinReady:
                return $"[E] Ambil {pendingCoinReward} Koin";
            case SlotState.BeakerReady:
                return "[E] Ambil Kembali Gelas Beaker";
            default:
                return "";
        }
    }
}