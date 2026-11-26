using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BeakerContainer : MonoBehaviour
{
    [Header("Referensi Visual & Teks Melayang (3D TextMeshPro)")]
    public Transform liquidPivot;
    public Renderer liquidRenderer;
    public TMP_Text floatingPercentText; // Gunakan 3D TextMeshPro (bukan Canvas UI)

    [Header("Pengaturan Posisi & Tampilan Persentase")]
    public float hideDelayAfterPour = 0.12f;
    public float textSideRadius = 0.16f;   // Jarak horizontal dari tengah gelas agar pasti di luar dinding kaca
    public float textHeightOffset = 0.14f; // Ketinggian angka dari alas bawah Beaker

    [Header("Kalibrasi Tinggi Visual Cairan")]
    public float minLiquidScaleY = 0.005f;
    public float maxLiquidScaleY = 0.45f;

    [Header("Kapasitas & Batas Luapan (ml)")]
    public float maxVisualVolume = 100f;
    public float overflowLimit = 105f;
    public float currentVolume = 0f;

    

    private Dictionary<string, float> liquidVolumes = new Dictionary<string, float>();
    private Dictionary<string, Color> liquidColors = new Dictionary<string, Color>();
    private float hideTimer = 0f;

    void Start()
    {
        UpdateLiquidVisual();

        if (floatingPercentText != null)
        {
            floatingPercentText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Sembunyikan angka otomatis begitu cairan berhenti masuk
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f && floatingPercentText != null)
            {
                floatingPercentText.gameObject.SetActive(false);
            }
        }
    }

    void LateUpdate()
    {
        if (floatingPercentText != null && floatingPercentText.gameObject.activeSelf && Camera.main != null)
        {
            Transform camTransform = Camera.main.transform;
            Vector3 beakerBasePos = transform.position;

            // 1. Hitung arah horizontal dari Beaker menuju kamera pemain
            Vector3 dirToCam = Vector3.ProjectOnPlane(camTransform.position - beakerBasePos, Vector3.up);
            if (dirToCam.sqrMagnitude < 0.001f)
                dirToCam = -camTransform.forward;
            else
                dirToCam.Normalize();

            // 2. Hitung arah KIRI relatif terhadap pandangan pemain (berlawanan dengan botol yang ada di sisi kanan)
            Vector3 leftFromPlayerView = Vector3.Cross(Vector3.up, dirToCam).normalized;

            // 3. Tempatkan teks mengorbit di sisi kiri luar gelas + sedikit maju ke arah kamera agar tidak pernah tertutup cairan
            Vector3 targetTextPos = beakerBasePos
                                  + (Vector3.up * textHeightOffset)
                                  + (leftFromPlayerView * textSideRadius)
                                  + (dirToCam * 0.04f);

            floatingPercentText.transform.position = targetTextPos;

            // 4. Hadapkan teks tegak lurus ke kamera pemain
            Vector3 lookDir = floatingPercentText.transform.position - camTransform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                floatingPercentText.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    public void AddLiquid(string chemName, float amount, Color addedColor)
    {
        float projectedTotal = currentVolume + amount;
        float existingChemVol = liquidVolumes.ContainsKey(chemName) ? liquidVolumes[chemName] : 0f;
        float projectedChemVol = existingChemVol + amount;

        // 1. Cek batas maksimal total volume (105 ml)
        if (projectedTotal > overflowLimit)
        {
            TriggerEvaporation("Melebihi batas 105 ml!");
            return;
        }

        // 2. Cek batas toleransi resep secara real-time saat penuangan
        if (RecipeLabManager.Instance != null)
        {
            if (!RecipeLabManager.Instance.ValidateRealTimePour(chemName, projectedChemVol, projectedTotal, out string failReason))
            {
                TriggerEvaporation(failReason);
                return;
            }
        }

        currentVolume = projectedTotal;

        if (liquidVolumes.ContainsKey(chemName))
            liquidVolumes[chemName] += amount;
        else
        {
            liquidVolumes.Add(chemName, amount);
            liquidColors.Add(chemName, addedColor);
        }

        UpdateLiquidVisual();
        ShowLivePercentage();
    }

    /// <summary>
    /// Memunculkan angka persentase total volume gelas hanya selama cairan sedang dituang.
    /// </summary>
    private void ShowLivePercentage()
    {
        if (floatingPercentText == null) return;

        float fillPercent = Mathf.Clamp((currentVolume / maxVisualVolume) * 100f, 0f, 110f);
        floatingPercentText.text = $"{fillPercent:F1}%";

        if (!floatingPercentText.gameObject.activeSelf)
        {
            floatingPercentText.gameObject.SetActive(true);
        }

        // Reset timer selama cairan masih terus masuk
        hideTimer = hideDelayAfterPour;
    }

    public float GetPercentageOf(string chemName)
    {
        if (currentVolume <= 0.001f || !liquidVolumes.ContainsKey(chemName)) return 0f;
        return (liquidVolumes[chemName] / currentVolume) * 100f;
    }

    public bool HasOtherChemicals(List<string> allowedChemicals)
    {
        foreach (var key in liquidVolumes.Keys)
        {
            if (!allowedChemicals.Contains(key) && liquidVolumes[key] > 0.5f)
                return true;
        }
        return false;
    }

    private Color CalculateMixedColor()
    {
        if (currentVolume <= 0.001f) return Color.clear;

        float totalC = 0f, totalM = 0f, totalY = 0f, totalA = 0f;

        foreach (var entry in liquidVolumes)
        {
            float weight = entry.Value / currentVolume;
            Color rgb = liquidColors[entry.Key];

            totalC += (1f - rgb.r) * weight;
            totalM += (1f - rgb.g) * weight;
            totalY += (1f - rgb.b) * weight;
            totalA += rgb.a * weight;
        }

        float finalR = Mathf.Clamp01(1f - totalC);
        float finalG = Mathf.Clamp01(1f - totalM);
        float finalB = Mathf.Clamp01(1f - totalY);

        Color.RGBToHSV(new Color(finalR, finalG, finalB), out float h, out float s, out float v);
        Color vividColor = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.15f), Mathf.Clamp(v * 1.1f, 0.4f, 1f));
        vividColor.a = totalA;

        return vividColor;
    }

    private void UpdateLiquidVisual()
    {
        if (liquidPivot != null)
        {
            float fillPercentage = Mathf.Clamp01(currentVolume / maxVisualVolume);
            float targetScaleY = Mathf.Lerp(minLiquidScaleY, maxLiquidScaleY, fillPercentage);

            Vector3 currentScale = liquidPivot.localScale;
            liquidPivot.localScale = new Vector3(currentScale.x, targetScaleY, currentScale.z);
        }

        if (liquidRenderer != null)
        {
            Color mixed = CalculateMixedColor();
            liquidRenderer.material.SetColor("_BaseColor", mixed);
            liquidRenderer.material.color = mixed;
        }
    }

    public void TriggerEvaporation(string reason = "Melebihi batas!")
    {
        Debug.LogWarning($"MENGUAP HABIS! {reason}");
        ClearBeaker();
    }

    public void ClearBeaker()
    {
        currentVolume = 0f;
        liquidVolumes.Clear();
        liquidColors.Clear();
        hideTimer = 0f;

        if (floatingPercentText != null)
        {
            floatingPercentText.gameObject.SetActive(false);
        }

        UpdateLiquidVisual();
    }
}