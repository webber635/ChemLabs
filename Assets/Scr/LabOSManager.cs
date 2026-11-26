using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LabOSManager : MonoBehaviour
{
    public static LabOSManager Instance;

    [Header("Database Resep (Isi hingga 10 Resep)")]
    public List<ChemicalRecipeSO> allRecipes = new List<ChemicalRecipeSO>();

    [Header("Tab Kiri: Daftar Resep")]
    public Transform recipeListContainer;     // Objek Content di dalam ScrollView panel kiri
    public GameObject listItemButtonPrefab;   // Prefab Button dengan Image & TextMeshProUGUI

    [Header("Warna Tombol di Tab Kiri")]
    public Color unlockedButtonColor = new Color(0.2f, 0.25f, 0.3f, 1f);
    public Color lockedButtonColor = new Color(0.65f, 0.15f, 0.15f, 1f);   // Merah untuk resep terkunci
    public Color activeButtonColor = new Color(0.15f, 0.55f, 0.25f, 1f);   // Hijau untuk produk yang sedang dipilih
    public Color disabledOtherColor = new Color(0.15f, 0.15f, 0.15f, 0.5f); // Redup saat produk lain sedang aktif

    [Header("Tab Kanan: Informasi & Aksi")]
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailPriceText;
    public TextMeshProUGUI detailRecipeText;
    public TextMeshProUGUI lockedWarningText; // Tulisan merah khusus saat klik resep yang terkunci
    public TextMeshProUGUI osCoinText;

    [Header("Tombol Aksi di Tab Kanan")]
    public Button mainActionButton;           // Tombol dinamis: "PILIH" / "BELI" / "TINGGALKAN"
    public TextMeshProUGUI mainActionText;
    public Color selectBtnColor = new Color(0.15f, 0.55f, 0.25f, 1f);
    public Color buyBtnColor = new Color(0.75f, 0.2f, 0.2f, 1f);
    public Color abandonBtnColor = new Color(0.75f, 0.45f, 0.1f, 1f);

    private ChemicalRecipeSO previewedRecipe;
    private ChemicalRecipeSO activeSelectedRecipe;

    // Menyimpan referensi tombol yang di-spawn agar bisa di-lock/unlock tanpa respawn ulang
    private Dictionary<ChemicalRecipeSO, Button> spawnedButtons = new Dictionary<ChemicalRecipeSO, Button>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        BuildRecipeList();

        if (allRecipes.Count > 0)
        {
            PreviewRecipe(allRecipes[0]);
        }
        else
        {
            ClearRightPanel();
        }
    }

    void Update()
    {
        if (osCoinText != null && PlayerWallet.Instance != null)
        {
            osCoinText.text = $"Koin: {PlayerWallet.Instance.currentCoins}";
        }
    }

    /// <summary>
    /// Membuat seluruh tombol resep di Tab Kiri.
    /// </summary>
    public void BuildRecipeList()
    {
        if (recipeListContainer == null || listItemButtonPrefab == null) return;

        foreach (Transform child in recipeListContainer)
        {
            Destroy(child.gameObject);
        }
        spawnedButtons.Clear();

        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;

            GameObject btnObj = Instantiate(listItemButtonPrefab, recipeListContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI label = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
            {
                label.text = recipe.isUnlockedByDefault
                    ? recipe.productName
                    : $"{recipe.productName} [TERKUNCI]";
            }

            if (btn != null)
            {
                ChemicalRecipeSO capturedRecipe = recipe;
                btn.onClick.AddListener(() => PreviewRecipe(capturedRecipe));
                spawnedButtons.Add(capturedRecipe, btn);
            }
        }

        RefreshLeftTabStates();
    }

    /// <summary>
    /// Mengatur warna dan kunci (interactable) semua tombol di Tab Kiri.
    /// Jika 1 produk sudah dipilih, tombol produk lain dikunci sampai pemain menekan "Tinggalkan".
    /// </summary>
    private void RefreshLeftTabStates()
    {
        bool hasActiveProduct = (activeSelectedRecipe != null);

        foreach (var kvp in spawnedButtons)
        {
            ChemicalRecipeSO recipe = kvp.Key;
            Button btn = kvp.Value;
            if (btn == null) continue;

            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            Image btnImage = btn.GetComponent<Image>();

            if (label != null)
            {
                if (activeSelectedRecipe == recipe)
                    label.text = $"{recipe.productName} [AKTIF]";
                else if (!recipe.isUnlockedByDefault)
                    label.text = $"{recipe.productName} [TERKUNCI]";
                else
                    label.text = recipe.productName;
            }

            if (hasActiveProduct)
            {
                // Hanya tombol produk yang sedang dipilih yang tetap aktif; produk lain dikunci
                bool isThisActive = (recipe == activeSelectedRecipe);
                btn.interactable = isThisActive;

                if (btnImage != null)
                {
                    btnImage.color = isThisActive ? activeButtonColor : disabledOtherColor;
                }
            }
            else
            {
                // Tidak ada produk aktif -> semua tombol bisa diklik untuk dilihat infonya
                btn.interactable = true;

                if (btnImage != null)
                {
                    btnImage.color = recipe.isUnlockedByDefault ? unlockedButtonColor : lockedButtonColor;
                }
            }
        }
    }

    /// <summary>
    /// Menampilkan informasi resep di Tab Kanan tanpa langsung menjadikannya pesanan aktif.
    /// </summary>
    public void PreviewRecipe(ChemicalRecipeSO recipe)
    {
        previewedRecipe = recipe;
        if (recipe == null)
        {
            ClearRightPanel();
            return;
        }

        if (detailNameText != null)
            detailNameText.text = recipe.productName;

        if (detailPriceText != null)
            detailPriceText.text = $"Nilai Jual Dasar: <b>{recipe.baseSellPrice} Koin</b> (Target: {recipe.targetTotalVolume} ml)";

        if (detailRecipeText != null)
        {
            string info = $"{recipe.description}\n\n<b>Komposisi Larutan:</b>\n";
            foreach (var ing in recipe.ingredients)
            {
                info += $"• {ing.chemicalName}: <b>{ing.targetPercentage}%</b> (±{ing.percentTolerance}%)\n";
            }
            detailRecipeText.text = info;
        }

        // Tampilkan tulisan merah jika resep masih terkunci
        // Tampilkan tulisan merah singkat jika resep masih terkunci
        if (lockedWarningText != null)
        {
            if (!recipe.isUnlockedByDefault)
            {
                lockedWarningText.gameObject.SetActive(true);
                lockedWarningText.text = "<color=red>Resep Terkunci</color>";
            }
            else
            {
                lockedWarningText.gameObject.SetActive(false);
            }
        }

        UpdateRightActionButton();
    }

    private void UpdateRightActionButton()
    {
        if (mainActionButton == null || previewedRecipe == null) return;

        mainActionButton.onClick.RemoveAllListeners();
        Image actionBg = mainActionButton.GetComponent<Image>();

        // 1. Jika resep yang dilihat adalah produk yang sedang aktif dipilih -> Munculkan tombol TINGGALKAN
        if (activeSelectedRecipe == previewedRecipe)
        {
            mainActionButton.interactable = true;
            if (mainActionText != null) mainActionText.text = "TINGGALKAN";
            if (actionBg != null) actionBg.color = abandonBtnColor;

            mainActionButton.onClick.AddListener(AbandonCurrentProduct);
        }
        // 2. Jika resep masih terkunci -> Munculkan tombol BELI
        else if (!previewedRecipe.isUnlockedByDefault)
        {
            mainActionButton.interactable = true;
            if (mainActionText != null) mainActionText.text = $"BELI ({previewedRecipe.licenseCost} Koin)";
            if (actionBg != null) actionBg.color = buyBtnColor;

            mainActionButton.onClick.AddListener(() => BuyRecipeLicense(previewedRecipe));
        }
        // 3. Jika resep sudah terbuka & belum ada produk aktif -> Munculkan tombol PILIH
        else
        {
            mainActionButton.interactable = (activeSelectedRecipe == null);
            if (mainActionText != null) mainActionText.text = "PILIH PRODUK";
            if (actionBg != null) actionBg.color = selectBtnColor;

            mainActionButton.onClick.AddListener(() => SelectProductToCraft(previewedRecipe));
        }
    }

    private void BuyRecipeLicense(ChemicalRecipeSO recipe)
    {
        if (PlayerWallet.Instance == null) return;

        if (PlayerWallet.Instance.SpendCoins(recipe.licenseCost))
        {
            recipe.isUnlockedByDefault = true;
            RefreshLeftTabStates();
            PreviewRecipe(recipe);
        }
        else if (lockedWarningText != null)
        {
            lockedWarningText.gameObject.SetActive(true);
            lockedWarningText.text = "<color=red>Koin Tidak Cukup</color>";
        }
    }

    private void SelectProductToCraft(ChemicalRecipeSO recipe)
    {
        if (recipe == null || !recipe.isUnlockedByDefault) return;

        activeSelectedRecipe = recipe;

        // Kirim target resep ke RecipeLabManager (Analyzer & Real-time Pouring)
        if (RecipeLabManager.Instance != null)
        {
            RecipeLabManager.Instance.SetActiveRecipe(recipe);
        }

        RefreshLeftTabStates();
        PreviewRecipe(recipe);
    }

    public void AbandonCurrentProduct()
    {
        ChemicalRecipeSO abandoned = activeSelectedRecipe;
        activeSelectedRecipe = null;

        // Kosongkan target aktif di RecipeLabManager
        if (RecipeLabManager.Instance != null)
        {
            RecipeLabManager.Instance.SetActiveRecipe(null);
        }

        RefreshLeftTabStates();
        if (abandoned != null) PreviewRecipe(abandoned);
    }

    private void ClearRightPanel()
    {
        if (detailNameText != null) detailNameText.text = "-";
        if (detailPriceText != null) detailPriceText.text = "";
        if (detailRecipeText != null) detailRecipeText.text = "Pilih resep di panel kiri.";
        if (lockedWarningText != null) lockedWarningText.gameObject.SetActive(false);
        if (mainActionButton != null) mainActionButton.interactable = false;
    }
}