using UnityEngine;

public class RecipeLabManager : MonoBehaviour
{
    public static RecipeLabManager Instance;

    [Header("Resep Aktif dari LabOS")]
    public ChemicalRecipeSO activeRecipe;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Dipanggil otomatis oleh LabOSManager saat pemain menekan tombol "PILIH PRODUK" atau "TINGGALKAN".
    /// </summary>
    public void SetActiveRecipe(ChemicalRecipeSO recipe)
    {
        activeRecipe = recipe;
    }

    /// <summary>
    /// Pengecekan real-time saat cairan dituang ke Beaker.
    /// Jika belum ada pesanan aktif, bahan asing masuk, atau volume bahan melewati batas toleransi -> langsung menguap.
    /// </summary>
    public bool ValidateRealTimePour(string incomingChem, float projectedChemVolume, float projectedTotalVolume, out string failReason)
    {
        failReason = "";

        if (activeRecipe == null)
        {
            failReason = "Belum memilih produk di LabOS!";
            return false;
        }

        RecipeIngredient target = activeRecipe.ingredients.Find(i => i.chemicalName == incomingChem);
        if (target == null)
        {
            failReason = $"{incomingChem} tidak ada di resep!";
            return false;
        }

        float maxAllowedPercent = target.targetPercentage + target.percentTolerance;
        float maxAllowedMl = (maxAllowedPercent / 100f) * activeRecipe.targetTotalVolume + 1.0f;

        if (projectedChemVolume > maxAllowedMl)
        {
            failReason = $"{incomingChem} melebihi batas ({maxAllowedPercent:F0}%)!";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluasi akurasi campuran di mesin Analyzer dan hitung harga koinnya.
    /// </summary>
    public int EvaluateAccuracyAndPayout(BeakerContainer beaker, out float accuracyPercent)
    {
        if (activeRecipe == null || beaker == null || beaker.currentVolume <= 1f)
        {
            accuracyPercent = 0f;
            return 0;
        }

        // 1. Hitung akurasi tiap komposisi bahan (bobot 70%)
        float totalChemScore = 0f;

        foreach (var ing in activeRecipe.ingredients)
        {
            float actualPercent = beaker.GetPercentageOf(ing.chemicalName);
            float diff = Mathf.Abs(actualPercent - ing.targetPercentage);

            float itemAccuracy = Mathf.Clamp01(1f - (diff / 25f));
            totalChemScore += itemAccuracy;
        }

        float avgChemAccuracy = activeRecipe.ingredients.Count > 0
            ? (totalChemScore / activeRecipe.ingredients.Count)
            : 0f;

        // 2. Hitung akurasi total volume (bobot 30%)
        float volDiff = Mathf.Abs(beaker.currentVolume - activeRecipe.targetTotalVolume);
        float volAccuracy = Mathf.Clamp01(1f - (volDiff / 50f));

        // 3. Akurasi akhir & kalkulasi koin
        float finalRatio = (avgChemAccuracy * 0.7f) + (volAccuracy * 0.3f);
        accuracyPercent = Mathf.Round(finalRatio * 1000f) / 10f;

        int payout = Mathf.Max(5, Mathf.RoundToInt(activeRecipe.baseSellPrice * finalRatio));
        return payout;
    }
}