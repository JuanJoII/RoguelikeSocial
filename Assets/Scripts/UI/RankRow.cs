using UnityEngine;
using TMPro; // Asegúrate de usar TextMeshPro

public class RankRow : MonoBehaviour
{
    [SerializeField] private TMP_Text positionText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text levelText;

    /// <summary>
    /// Asigna los valores visuales a los componentes de texto de esta fila.
    /// </summary>
    public void SetRowData(int position, string username, int score, int maxLevel)
    {
        if (positionText != null) positionText.text = position.ToString();
        if (usernameText != null) usernameText.text = username;
        if (scoreText != null) scoreText.text = score.ToString("N0"); // Formato con separador de miles
        if (levelText != null) levelText.text = maxLevel.ToString();
    }
}