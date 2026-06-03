using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoguelikeSocial.Assets.Scripts.Models; // Necesario para controlar el texto de fallback

public class RankingPanelUI : MonoBehaviour
{
    [Header("Configuración del Contenedor")]
    [SerializeField] private Transform rowsContainer; // El objeto 'Content' del Scroll View
    [SerializeField] private GameObject rowPrefab;     // El Prefab con el script 'RankingRow'

    [Header("Mensaje de Fallback")]
    [SerializeField] private TMP_Text noRankingsText;   // Texto que dice "No hay puntajes aún"

    [Header("Botones")]
    [SerializeField] private Button refreshButton;

    private void OnEnable()
    {
        // Ocultamos el texto de fallback por defecto al iniciar la carga
        if (noRankingsText != null) noRankingsText.gameObject.SetActive(false);

        UpdateRanking();
    }

    private void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(UpdateRanking);
        }
    }

    public void UpdateRanking()
    {
        // 1. Limpiar el contenedor para evitar duplicados
        foreach (Transform child in rowsContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Llamar al DataManager
        DataManager.Instance.FetchRanking(OnRankingDataReceived);
    }

    private void OnRankingDataReceived(List<PlayerProgressModel> rankingData)
    {
        // ── CONTROL DE FALLBACK ─────────────────────────────────────────────
        // Si la lista es nula o no tiene elementos, mostramos el mensaje y salimos
        if (rankingData == null || rankingData.Count == 0)
        {
            if (noRankingsText != null)
            {
                noRankingsText.text = "¡No hay registros aún!\nSé el primero en la tabla.";
                noRankingsText.gameObject.SetActive(true);
            }
            return;
        }

        // Si hay datos, nos aseguramos de que el texto de fallback esté apagado
        if (noRankingsText != null) noRankingsText.gameObject.SetActive(false);
        // ────────────────────────────────────────────────────────────────────

        // 3. Instanciar y llenar cada fila con los datos directos del modelo
        for (int i = 0; i < rankingData.Count; i++)
        {
            var playerData = rankingData[i];

            GameObject rowInstance = Instantiate(rowPrefab, rowsContainer);

            if (rowInstance.TryGetComponent<RankRow>(out var rankingRow))
            {
                rankingRow.SetRowData(
                    i + 1,
                    playerData.Username,
                    playerData.TotalScore,
                    playerData.Level
                );
            }
        }
    }
}