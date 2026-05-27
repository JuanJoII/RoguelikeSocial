using UnityEngine;
using System.Collections.Generic;

/// Componente que se añade al prefab raíz de cualquier módulo
/// (pared, puerta, pilar). Lista sus SnapPoints disponibles.
public class RoomModule : MonoBehaviour
{
    [Tooltip("Se auto-rellena en Awake. También puedes asignarlos manualmente.")]
    public List<SnapPoint> SnapPoints = new();

    private void Awake()
    {
        // Auto-detectar todos los SnapPoints hijos si la lista está vacía
        if (SnapPoints.Count == 0)
            SnapPoints.AddRange(GetComponentsInChildren<SnapPoint>());
    }

    /// Devuelve el primer SnapPoint libre del tipo indicado.
    public SnapPoint GetFreeSnap(SnapPoint.SnapType type = SnapPoint.SnapType.Any)
    {
        foreach (var sp in SnapPoints)
        {
            if (sp.IsOccupied) continue;
            if (type == SnapPoint.SnapType.Any || sp.Type == type) return sp;
        }
        return null;
    }

    /// Marca todos los SnapPoints como ocupados.
    public void OccupyAll()
    {
        foreach (var sp in SnapPoints) sp.IsOccupied = true;
    }
}