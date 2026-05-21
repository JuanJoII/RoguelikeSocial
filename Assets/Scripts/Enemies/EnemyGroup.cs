using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cerebro invisible de una oleada.
/// Un EnemyGroup existe por cada oleada activa en escena.
///
/// RESPONSABILIDADES:
/// - Mantener la lista de miembros activos
/// - Calcular GroupCenter cada frame para la cohesión
/// - Recibir notificaciones de muerte de sus miembros
/// - Notificar al RoomManager cuando todos murieron
///
/// CICLO DE VIDA:
/// WaveManager lo inicializa → registra miembros → Update calcula centro
/// → miembros van muriendo → ReportDeath actualiza lista
/// → lista vacía → grupo notifica y se devuelve al pool
/// </summary>
public class EnemyGroup : MonoBehaviour
{
    public Vector3 GroupCenter { get; private set; }

    private List<EnemyAI> _members = new List<EnemyAI>();
    private bool _active;

    public void Initialize(List<EnemyAI> members)
    {
        _members.Clear();
        _members.AddRange(members);
        _active = true;

        // Calculamos el centro inicial de inmediato
        // para que los miembros tengan un valor válido desde el frame 0
        UpdateGroupCenter();
    }

    private void Update()
    {
        if (!_active || _members.Count == 0) return;
        UpdateGroupCenter();
    }

    private void UpdateGroupCenter()
    {
        if (_members.Count == 0) return;

        Vector3 sum = Vector3.zero;
        foreach (EnemyAI member in _members)
        {
            if (member != null)
                sum += member.transform.position;
        }

        GroupCenter = sum / _members.Count;
    }

    /// <summary>
    /// Llamado por EnemyAI cuando muere.
    /// No devolvemos el EnemyAI al pool aquí — él mismo lo hace antes de llamarnos.
    /// </summary>
    public void ReportDeath(EnemyAI enemy)
    {
        _members.Remove(enemy);

        if (_members.Count == 0)
            OnGroupDefeated();
    }

    private void OnGroupDefeated()
    {
        _active = false;
        // WaveManager escucha esto para saber que la oleada fue derrotada
        // y decidir si lanzar la siguiente
        WaveManager.OnGroupDefeated?.Invoke(this);
        gameObject.SetActive(false);
    }
}