using System;

[Serializable]
public class ParkingEvent
{
    public int eventId;
    public DateTime timestamp;
    public string gate;
    public string eventType;   // ENTER / EXIT
    public string vehicleId;
    public string vehicleType;
    public string plate;
    public string slotId;      // "1".."16"
    public string group;
}