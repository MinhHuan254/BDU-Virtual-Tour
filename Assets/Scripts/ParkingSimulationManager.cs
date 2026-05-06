using System;
using System.Collections.Generic;
using UnityEngine;

public class ParkingSimulationManager : MonoBehaviour
{
    [Header("CSV")]
    public string csvFileName = "parking_events_updated_1_16.csv";

    [Header("Simulation")]
    [Tooltip("60 = chạy nhanh gấp 60 lần thời gian dữ liệu")]
    public float timeScale = 1;
    public bool autoStart = true;

    [Header("Scene Refs")]
    public Transform carsParent; // chứa Car1..Car16
    public Transform gateA;
    public Transform gateB;

    // slot 1..16 -> point name
    private readonly string[] slotPointNames = new string[]
    {
        "point5","point7","point9","point32",
        "point34","point24","point25","point33",
        "point21","point12","point14","point29",
        "point27","point31","point26","point23"
    };

    // ===== runtime =====
    private List<ParkingEvent> events = new List<ParkingEvent>();
    private int nextEventIndex = 0;

    private DateTime simTime;
    private DateTime startTime;
    private bool running = false;

    // Không dùng new() rút gọn để tránh lỗi C# version
    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private readonly Dictionary<string, GameObject> activeByVehicleId = new Dictionary<string, GameObject>();
    private readonly List<Transform> slotTargets = new List<Transform>();

    void Awake()
    {
        BuildPool();
        BuildSlotTargets();
    }

    void Start()
    {
        if (gateA == null)
            Debug.LogWarning("gateA chưa gán trong Inspector! Xe sẽ không spawn đúng.");

        events = ParkingCsvLoader.LoadFromStreamingAssets(csvFileName);
        if (events == null || events.Count == 0)
        {
            Debug.LogWarning("Không đọc được CSV hoặc CSV rỗng: " + csvFileName);
            return;
        }

        startTime = events[0].timestamp;
        simTime = startTime;

        running = autoStart;

        Debug.Log("Loaded " + events.Count + " events. StartTime=" + startTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    void Update()
    {
        if (!running || events == null || events.Count == 0) return;

        simTime = simTime.AddSeconds(Time.deltaTime * timeScale);

        while (nextEventIndex < events.Count && events[nextEventIndex].timestamp <= simTime)
        {
            HandleEvent(events[nextEventIndex]);
            nextEventIndex++;
        }
    }

    private void HandleEvent(ParkingEvent e)
    {
        if (e == null) return;

        if (e.eventType == "ENTER") Enter(e);
        else if (e.eventType == "EXIT") Exit(e);
    }

    private void Enter(ParkingEvent e)
    {
        if (activeByVehicleId.ContainsKey(e.vehicleId)) return;

        if (pool.Count == 0)
        {
            Debug.LogWarning("Pool hết xe. Hãy tăng số xe trong carsParent hoặc giảm dữ liệu.");
            return;
        }

        int slotNum;
        if (!int.TryParse(e.slotId, out slotNum) || slotNum < 1 || slotNum > 16)
        {
            Debug.LogWarning("slot_id invalid: " + e.slotId);
            return;
        }

        Transform slotT = slotTargets[slotNum - 1];
        if (slotT == null)
        {
            Debug.LogWarning("Slot target null for slot " + slotNum + " (missing Point?)");
            return;
        }

        Transform gateT = GetGate(e.gate);
        if (gateT == null)
        {
            Debug.LogWarning("Gate transform null. Hãy gán gateA/gateB.");
            return;
        }

        GameObject car = pool.Dequeue();
        car.SetActive(true);
        car.transform.position = gateT.position;
        car.transform.rotation = gateT.rotation;

        activeByVehicleId[e.vehicleId] = car;

        // ✅ Dùng MoverCar
        MoverCar mover = car.GetComponent<MoverCar>();
        if (mover == null) mover = car.AddComponent<MoverCar>();

        // Route: Gate -> SlotPoint
        List<Transform> route = new List<Transform>();
        route.Add(gateT);
        route.Add(slotT);

        mover.MoveAlong(route);
    }

    private void Exit(ParkingEvent e)
    {
        GameObject car;
        if (!activeByVehicleId.TryGetValue(e.vehicleId, out car))
        {
            // EXIT đến nhưng xe chưa từng ENTER (có thể do pool thiếu) -> bỏ qua
            return;
        }

        Transform gateT = GetGate(e.gate);
        if (gateT == null)
        {
            Debug.LogWarning("Gate transform null. Hãy gán gateA/gateB.");
            return;
        }

        // ✅ Dùng MoverCar
        MoverCar mover = car.GetComponent<MoverCar>();
        if (mover == null) mover = car.AddComponent<MoverCar>();

        List<Transform> route = new List<Transform>();
        route.Add(gateT);

        mover.MoveAlong(route, () =>
        {
            activeByVehicleId.Remove(e.vehicleId);
            car.SetActive(false);
            pool.Enqueue(car);
        });
    }

    private Transform GetGate(string gateName)
    {
        if (gateName == "GateB")
            return gateB != null ? gateB : gateA;

        return gateA;
    }

    private void BuildPool()
    {
        pool.Clear();

        if (carsParent == null)
        {
            Debug.LogError("carsParent chưa gán trong Inspector!");
            return;
        }

        for (int i = 0; i < carsParent.childCount; i++)
        {
            GameObject car = carsParent.GetChild(i).gameObject;
            car.SetActive(false);

            // ✅ đảm bảo có MoverCar
            if (car.GetComponent<MoverCar>() == null)
                car.AddComponent<MoverCar>();

            pool.Enqueue(car);
        }

        Debug.Log("Pool cars: " + pool.Count);
    }

    private void BuildSlotTargets()
    {
        slotTargets.Clear();

        for (int i = 0; i < slotPointNames.Length; i++)
        {
            GameObject go = GameObject.Find(slotPointNames[i]);
            Transform t = go != null ? go.transform : null;

            if (t == null)
                Debug.LogError("Không tìm thấy object tên: " + slotPointNames[i] + " (slot " + (i + 1) + ")");

            slotTargets.Add(t);
        }
    }

    // optional controls
    public void Play() { running = true; }
    public void Pause() { running = false; }

    public void ResetSim()
    {
        running = false;
        nextEventIndex = 0;
        simTime = startTime;

        foreach (KeyValuePair<string, GameObject> kv in activeByVehicleId)
        {
            kv.Value.SetActive(false);
            pool.Enqueue(kv.Value);
        }
        activeByVehicleId.Clear();
    }
}