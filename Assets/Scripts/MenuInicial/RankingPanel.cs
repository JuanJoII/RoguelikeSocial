using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de ranking. Se activa desde el menú de pausa del lobby.
/// Solicita los datos al DataManager y los muestra en una lista.
///
/// SETUP:
/// Cada entrada del ranking es un prefab con tres TextMeshPro:
/// posición, nombre de usuario y puntaje total.
/// El Content del ScrollRect es el padre donde se instancian.
/// </summary>
public class RankingPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Transform rankingContent;   // Content del ScrollRect
    [SerializeField] private GameObject rankingEntryPrefab; // prefab de una fila
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI loadingText;

    // Estructura de una entrada de ranking
    // Debe coincidir con lo que devuelve DataManager
    [System.Serializable]
    public class RankingEntry
    {
        public string username;
        public int totalScore;
        public int maxUnlockedLevel;
    }

    private void Start()
    {
        closeButton?.onClick.AddListener(() => Hide(instant: false));
        Hide(instant: true);
    }

    public void Show()
    {
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        // Limpiamos entradas anteriores
        foreach (Transform child in rankingContent)
            Destroy(child.gameObject);

        loadingText.gameObject.SetActive(true);

        // Pedimos el ranking al DataManager
        DataManager.Instance.FetchRanking(OnRankingReceived);
    }

    public void Hide(bool instant = false)
    {
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
    }

    private void OnRankingReceived(List<RankingEntry> entries)
    {
        loadingText.gameObject.SetActive(false);

        if (entries == null || entries.Count == 0)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "No hay jugadores aún.";
            return;
        }

        // Ordenamos por score descendente
        entries.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));

        for (int i = 0; i < entries.Count; i++)
        {
            GameObject entryObj = Instantiate(rankingEntryPrefab, rankingContent);

            // El prefab debe tener estos tres TextMeshPro como hijos
            // en ese orden exacto
            TextMeshProUGUI[] texts = entryObj.GetComponentsInChildren<TextMeshProUGUI>();

            if (texts.Length >= 3)
            {
                texts[0].text = $"#{i + 1}";
                texts[1].text = entries[i].username;
                texts[2].text = entries[i].totalScore.ToString("N0");
            }
        }
    }
}