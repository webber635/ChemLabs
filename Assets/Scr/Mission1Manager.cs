using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[Serializable]
public class ChemicalTarget
{
    public string chemicalName;     // Contoh: "Aquades"
    public float targetPercentage;  // Contoh: 70f (%)
    public float percentTolerance = 3f; // Toleransi +/- 3%
}

public class Mission1Manager : MonoBehaviour
{
    public static Mission1Manager Instance;

    [Header("Target Misi 1")]
    public string productName = "Cairan Pembersih Kaca (Eco Glass Cleaner)";
    public float targetTotalVolume = 100f;
    public float volumeTolerance = 5f; // Sah jika 95 ml - 105 ml
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
            analyzerTitleText.text = $"MISI 1: {productName}";

        if (analyzerRecipeText != null)
        {
            string info = $"Target Volume: <b>{targetTotalVolume} ml</b> (±{volumeTolerance} ml)\nKomposisi:\n";
            foreach (var item in recipeTargets)
            {
                info += $"• {item.chemicalName}: <b>{item.targetPercentage}%</b> (±{item.percentTolerance}%)\n";
            }
            analyzerRecipeText.text = info;
        }

        if (analyzerResultText != null)
            analyzerResultText.text = "<color=white>Status: Menunggu sampel gelas ukur...</color>";
    }

    public void CheckBeakerSample(BeakerContainer beaker)
    {
        if (beaker.currentVolume <= 1f)
        {
            analyzerResultText.text = "<color=yellow>Gelas masih kosong! Silakan racik larutan terlebih dahulu.</color>";
            return;
        }

        // 1. Cek Total Volume
        float volDiff = Mathf.Abs(beaker.currentVolume - targetTotalVolume);
        bool isVolumeValid = volDiff <= volumeTolerance;

        // 2. Cek Persentase tiap Bahan & pastikan tidak ada bahan asing
        bool isCompositionValid = true;
        List<string> allowedNames = new List<string>();
        string detailReport = "";

        foreach (var target in recipeTargets)
        {
            allowedNames.Add(target.chemicalName);
            float actualPercent = beaker.GetPercentageOf(target.chemicalName);
            float percentDiff = Mathf.Abs(actualPercent - target.targetPercentage);

            if (percentDiff > target.percentTolerance)
            {
                isCompositionValid = false;
            }
            detailReport += $"{target.chemicalName}: {actualPercent:F1}% (Target {target.targetPercentage}%)\n";
        }

        bool hasForeignChem = beaker.HasOtherChemicals(allowedNames);

        // 3. Keputusan Akhir Mesin Analyzer
        if (isVolumeValid && isCompositionValid && !hasForeignChem)
        {
            analyzerResultText.text = $"<color=green><b>LULUS UJI MUTU!</b>\nProduk {productName} berhasil dibuat.</color>\n{detailReport}";
        }
        else
        {
            string reason = hasForeignChem ? "Tercampur bahan kimia di luar resep!" :
                            (!isVolumeValid ? $"Total volume {beaker.currentVolume:F1} ml tidak sesuai target {targetTotalVolume} ml!" :
                            "Rasio persentase komposisi meleset!");

            analyzerResultText.text = $"<color=red><b>REAKSI GAGAL / MELEDAK!</b>\n{reason}</color>";
            beaker.TriggerReactionExplosion();
        }
    }
}