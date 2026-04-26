using UnityEngine;

public class CoinsManager : MonoSingleton<CoinsManager>
{
    [SerializeField] int startingCoins = 100;
    public int CurrentCoins;

    public const string COINS_PREFS = "PlayerCoins";

    private void Awake()
    {
        CurrentCoins = PlayerPrefs.GetInt(COINS_PREFS, startingCoins);
        PlayerPrefs.SetInt(COINS_PREFS, CurrentCoins);
        UIManager.Instance.UpdateCoinsText();

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void AddCoins(int amount)
    {
        CurrentCoins += amount;
        PlayerPrefs.SetInt(COINS_PREFS, CurrentCoins);

        UIManager.Instance.UpdateCoinsText();
    }

    public bool RemoveCoins(int amount)
    {
        if (CurrentCoins < amount)
        {
            UIManager.Instance.ShowToastMessage("Not Enough Coins Available.");
            return false;
        }

        CurrentCoins -= amount;
        PlayerPrefs.SetInt(COINS_PREFS, CurrentCoins);

        UIManager.Instance.UpdateCoinsText();

        return true;
    }
}