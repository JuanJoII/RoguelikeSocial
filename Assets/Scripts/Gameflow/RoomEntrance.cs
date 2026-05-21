using UnityEngine;

/// <summary>
/// Trigger invisible en el umbral de entrada de cada sala.
/// Cuando el jugador lo atraviesa, activa la sala siguiente.
///
/// SETUP:
/// 1. Crea un GameObject vacío en el umbral del pasillo
/// 2. Agrégale un BoxCollider en modo Is Trigger
/// 3. Asigna la referencia al RoomContext de la sala a la que da acceso
/// 4. El jugador debe estar en el layer "Player" para que el trigger funcione
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomEntrance : MonoBehaviour
{
    [SerializeField] private RoomContext targetRoom;

    private bool _activated;

    private void OnTriggerEnter(Collider other)
    {
        // Solo se activa una vez — evitamos doble trigger si el jugador
        // retrocede y vuelve a cruzar el umbral
        if (_activated) return;
        if (!other.CompareTag("Player")) return;
        if (targetRoom == null)
        {
            Debug.LogError("[RoomEntrance] No hay RoomContext asignado.");
            return;
        }

        _activated = true;
        RoomManager.Instance.ActivateRoom(targetRoom);
    }
}