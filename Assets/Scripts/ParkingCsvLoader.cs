using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ParkingCsvLoader
{
    // header:
    // event_id,timestamp,gate,event_type,vehicle_id,vehicle_type,plate,slot_id,group
    public static List<ParkingEvent> LoadFromStreamingAssets(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError("CSV not found: " + path);
            return new List<ParkingEvent>();
        }

        var list = new List<ParkingEvent>();
        string[] lines = File.ReadAllLines(path);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // file của bạn không có dấu phẩy trong field => split đơn giản ok
            string[] c = line.Split(',');
            if (c.Length < 9) continue;

            var e = new ParkingEvent();
            e.eventId = int.Parse(c[0]);

            // format trong file: yyyy-MM-dd HH:mm:ss
            e.timestamp = DateTime.ParseExact(
                c[1],
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            );

            e.gate = c[2];
            e.eventType = c[3];
            e.vehicleId = c[4];
            e.vehicleType = c[5];
            e.plate = c[6];
            e.slotId = c[7];
            e.group = c[8];

            list.Add(e);
        }

        list.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
        return list;
    }
}