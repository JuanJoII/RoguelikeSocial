using UnityEngine;

public enum DoorDirection { North, South, East, West }

[System.Serializable]
public class DoorSocket
{
    public DoorDirection Direction;
    public Vector3 WorldPosition;
    public bool IsConnected;
    public RoomData ConnectedRoom;
}