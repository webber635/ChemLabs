using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BeakerContainer : MonoBehaviour
{
    [Header("Referensi Visual & UI Gelas")]
    public Transform liquidPivot;
    public Renderer liquidRenderer;
    public TextMeshProUGUI beakerInfoText; // UI kecil melayang di sebelah gelas

    [Header("Kapasitas & Batas Luapan (ml)")]
    public float maxVisualVolume = 100f;
    public float overflowLimit = 105f; // Melewati batas ini -> cairan menguap habis
    public float currentVolume = 0f;

    private Dictionary<string, float> liquidVolumes = new Dictionary<string, float>();
    private Dictionary<string, Color> liquidColors = new Dictionary<string, Color>();

    void Start()
    {
        UpdateLiquidVisual();
        UpdateBeakerUI();
    }

    public void AddLiquid(string chemName, float amount, Color addedColor)
    {
        currentVolume += amount;

        // Cek efek gagal 1: Jika melebihi batas (Over-volume), cairan menguap habis
        if (currentVolume > overflowLimit)
        {
            TriggerEvaporation();
            return;
        }

        if (liquidVolumes.ContainsKey(chemName))
        {
            liquidVolumes[chemName] += amount;
        }
        else
        {
            liquidVolumes.Add(chemName, amount);
            liquidColors.Add(chemName, addedColor);
        }

        UpdateLiquidVisual();
        UpdateBeakerUI();
    }

    // Menghitung persentase komposisi bahan tertentu (%)
    public float GetPercentageOf(string chemName)
    {
        if (currentVolume <= 0.001f || !liquidVolumes.ContainsKey(chemName)) return 0f;
        return (liquidVolumes[chemName] / currentVolume) * 100f;
    }

    // Mengecek apakah ada bahan asing di luar daftar resep
    public bool HasOtherChemicals(List<string> allowedChemicals)
    {
        foreach (var key in liquidVolumes.Keys)
        {
            if (!allowedChemicals.Contains(key) && liquidVolumes[key] > 0.5f)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateBeakerUI()
    {
        if (beakerInfoText == null) return;

        if (currentVolume <= 0.01f)
        {
            beakerInfoText.text = "Volume: 0 ml\n(Kosong)";
            return;
        }

        string display = $"<b>Total: {currentVolume:F1} ml</b>\n";
        foreach (var entry in liquidVolumes)
        {
            float percent = (entry.Value / currentVolume) * 100f;
            display += $"• {entry.Key}: {percent:F1}%\n";
        }
        beakerInfoText.text = display;
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
            float targetScaleY = Mathf.Lerp(0.01f, 0.9f, fillPercentage);
            liquidPivot.localScale = new Vector3(1f, targetScaleY, 1f);
        }

        if (liquidRenderer != null)
        {
            Color mixed = CalculateMixedColor();
            liquidRenderer.material.SetColor("_BaseColor", mixed);
            liquidRenderer.material.color = mixed;
        }
    }

    // Efek Gagal 1: Cairan menguap habis saat terlalu penuh
    public void TriggerEvaporation()
    {
        Debug.LogWarning("OVER-VOLUME! Cairan meluap dan menguap habis.");
        ClearBeaker();
        if (beakerInfoText != null)
        {
            beakerInfoText.text = "<color=yellow>MENGUAP HABIS!\n(Melebihi 105 ml)</color>";
        }
    }

    // Efek Gagal 2: Reaksi gagal saat diuji di Analyzer
    public void TriggerReactionExplosion()
    {
        Debug.LogWarning("REAKSI GAGAL! Komposisi tidak sesuai target.");
        ClearBeaker();
        if (beakerInfoText != null)
        {
            beakerInfoText.text = "<color=red>REAKSI GAGAL!\n(Komposisi Salah)</color>";
        }
    }

    public void ClearBeaker()
    {
        currentVolume = 0f;
        liquidVolumes.Clear();
        liquidColors.Clear();
        UpdateLiquidVisual();
        UpdateBeakerUI();
    }
}