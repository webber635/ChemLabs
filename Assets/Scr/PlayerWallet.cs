using UnityEngine;
using TMPro;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance;

    [Header("Saldo Koin")]
    public int currentCoins = 0;

    [Header("Referensi UI (Opsional)")]
    public TextMeshProUGUI coinDisplayText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateWalletUI();
    }

    public void AddCoins(int amount)
    {
        currentCoins += amount;
        UpdateWalletUI();
    }

    public bool SpendCoins(int amount)
    {
        if (currentCoins < amount) return false;
        currentCoins -= amount;
        UpdateWalletUI();
        return true;
    }

    private void UpdateWalletUI()
    {
        if (coinDisplayText != null)
        {
            coinDisplayText.text = $"Koin: {currentCoins}";
        }
    }
}