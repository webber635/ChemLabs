using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RecipeIngredient
{
    public string chemicalName;       // Contoh: "Akuades", "Etanol 96%", dll.
    public float targetPercentage;    // Contoh: 70f
    public float percentTolerance = 5f;
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "ChemLabs/Chemical Recipe")]
public class ChemicalRecipeSO : ScriptableObject
{
    [Header("Identitas Produk")]
    public string recipeID;
    public string productName;
    [TextArea(2, 3)]
    public string description;

    [Header("Ekonomi & Lisensi")]
    public bool isUnlockedByDefault = false;
    public int licenseCost = 150;     // Harga beli lisensi di LabOS
    public int baseSellPrice = 100;   // Harga jual dasar di Analyzer (100% akurasi)

    [Header("Formula Larutan")]
    public float targetTotalVolume = 100f;
    public float volumeTolerance = 5f;
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();
}