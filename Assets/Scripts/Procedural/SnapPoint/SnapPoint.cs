using UnityEngine;

/// Componente que se añade al prefab de pared/puerta.
/// Define un punto de conexión con dirección.
/// El forward del SnapPoint indica hacia dónde "sale" la conexión.
public class SnapPoint : MonoBehaviour
{
    public enum SnapType { Wall, Door, Any }
    public SnapType Type = SnapType.Wall;
    public bool IsOccupied = false;

    private void OnDrawGizmos()
    {
        // Esfera verde = libre, rojo = ocupado
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawSphere(transform.position, 0.15f);

        // Línea amarilla mostrando el forward del snap
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position,
                        transform.position + transform.forward * 0.5f);
    }

    /// Alinea esta pieza para que este SnapPoint
    /// encaje exactamente con el SnapPoint destino.
    /// El forward de entrada debe ser el negativo del forward de salida.
    public void AlignTo(SnapPoint target, Transform pieceRoot)
    {
        // 1. Rotar la pieza para que este snap mire opuesto al target
        Quaternion rotOffset = Quaternion.FromToRotation(
            transform.forward,
            -target.transform.forward);
        pieceRoot.rotation = rotOffset * pieceRoot.rotation;

        // 2. Mover la pieza para que las posiciones coincidan exactamente
        Vector3 posOffset = target.transform.position - transform.position;
        pieceRoot.position += posOffset;
    }
}