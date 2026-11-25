using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[Serializable]
public class ChemicalTarget
{
    public string chemicalName;       // Contoh: "Akuades"
    public float targetPercentage;    // Contoh: 70f (%)
    public float percentTolerance = 5f; // Toleransi +/- %
}

public class Mission1Manager : MonoBehaviour
{
    public static Mission1Manager Instance;

    [Header("Target Misi 1")]
    public string productName = "Cairan Pembersih Kaca (Eco Glass Cleaner)";
    public float targetTotalVolume = 100f;
    public float volumeTolerance = 5f;
    public int basePrice = 100; // Harga dasar untuk akurasi 100%
    public List<ChemicalTarget> recipeTargets;

    [Header("Referensi UI Monitor Analyzer")]
    public TextMeshProUGUI analyzerTitleText;
    public TextMeshProUGUI analyzerRecipeText;
    public TextMeshProUGUI analyzerResultText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        SetupMissionUI();
    }

    private void SetupMissionUI()
    {
        if (analyzerTitleText != null)
            analyzerTitleText.text = $"ANALYZER: {productName}";

        if (analyzerRecipeText != null)
        {
            string info = $"Target Volume: <b>{targetTotalVolume} ml</b> (±{volumeTolerance} ml)\n" +
                          $"Harga Dasar: <b>{basePrice} Koin</b>\nKomposisi:\n";
            foreach (var item in recipeTargets)
            {
                info += $"• {item.chemicalName}: <b>{item.targetPercentage}%</b> (±{item.percentTolerance}%)\n";
            }
            analyzerRecipeText.text = info;
        }

        if (analyzerResultText != null)
            analyzerResultText.text = "<color=white>Status: Menunggu sampel gelas ukur...</color>";
    }

    /// <summary>
    /// Dipanggil secara real-time oleh BeakerContainer saat cairan dituang.
    /// Jika bahan asing masuk atau volume suatu bahan melewati batas maksimum toleransi, langsung menguap.
    /// </summary>
    public bool ValidateRealTimePour(string incomingChem, float projectedChemVolume, float projectedTotalVolume, out string failReason)
    {
        failReason = "";

        ChemicalTarget target = recipeTargets.Find(t => t.chemicalName == incomingChem);
        if (target == null)
        {
            failReason = $"{incomingChem} tidak ada di resep!";
            return false;
        }

        // Hitung batas maksimal ml berdasarkan target total volume (100 ml) + toleransi persentase
        // Contoh: Target 70% +/- 5% dari 100 ml -> maksimal 75 ml (+ sedikit buffer 1 ml untuk presisi tuang)
        float maxAllowedPercent = target.targetPercentage + target.percentTolerance;
        float maxAllowedMl = (maxAllowedPercent / 100f) * targetTotalVolume + 1.0f;

        if (projectedChemVolume > maxAllowedMl)
        {
            failReason = $"{incomingChem} melebihi batas toleransi ({maxAllowedPercent:F0}%)!";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Mengevaluasi akurasi campuran dan menghitung bayaran koin (tidak ada gagal di Analyzer).
    /// </summary>
    public int EvaluateAccuracyAndPayout(BeakerContainer beaker, out float accuracyPercent)
    {
        if (beaker == null || beaker.currentVolume <= 1f)
        {
            accuracyPercent = 0f;
            if (analyzerResultText != null)
                analyzerResultText.text = "<color=yellow>Gelas kosong! Tidak ada nilai jual.</color>";
            return 0;
        }

        // 1. Hitung skor akurasi persentase tiap bahan (bobot 70%)
        float totalChemScore = 0f;
        string detailReport = "";

        foreach (var target in recipeTargets)
        {
            float actualPercent = beaker.GetPercentageOf(target.chemicalName);
            float diff = Mathf.Abs(actualPercent - target.targetPercentage);

            // Skor 1.0 jika selisih 0, berkurang proporsional hingga 0 jika selisih >= 25%
            float itemAccuracy = Mathf.Clamp01(1f - (diff / 25f));
            totalChemScore += itemAccuracy;

            detailReport += $"• {target.chemicalName}: {actualPercent:F1}% (Target {target.targetPercentage}%)\n";
        }

        float avgChemAccuracy = recipeTargets.Count > 0 ? (totalChemScore / recipeTargets.Count) : 0f;

        // 2. Hitung skor akurasi total volume terhadap 100 ml (bobot 30%)
        float volDiff = Mathf.Abs(beaker.currentVolume - targetTotalVolume);
        float volAccuracy = Mathf.Clamp01(1f - (volDiff / 50f));

        // Gabungkan akurasi akhir (0% - 100%)
        float finalRatio = (avgChemAccuracy * 0.7f) + (volAccuracy * 0.3f);
        accuracyPercent = Mathf.Round(finalRatio * 1000f) / 10f; // 1 angka di belakang koma

        // Hitung harga koin (minimal 5 koin jika berisi larutan)
        int payout = Mathf.Max(5, Mathf.RoundToInt(basePrice * finalRatio));

        if (analyzerResultText != null)
        {
            string gradeColor = accuracyPercent >= 85f ? "green" : (accuracyPercent >= 60f ? "yellow" : "orange");
            analyzerResultText.text =
                $"<color={gradeColor}><b>HASIL ANALISIS SELESAI</b>\n" +
                $"Akurasi Mutu: <b>{accuracyPercent:F1}%</b>\n" +
                $"Nilai Tukar: <b>{payout} Koin</b></color>\n{detailReport}";
        }

        return payout;
    }
}