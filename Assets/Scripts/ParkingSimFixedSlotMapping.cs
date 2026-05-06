using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ParkingSimFixedSlotMapping : MonoBehaviour
{
    [Header("API")]
    public string apiUrl = "http://192.168.69.72:5000/api/parking/slots";
    public float pollInterval = 3f;

    [Tooltip("Delay trước khi bắt đầu poll lần đầu.")]
    public float initialDelay = 1f;

    [Header("State Confirmation")]
    [Tooltip("Số lần poll liên tiếp cùng 1 trạng thái trước khi xác nhận thay đổi thật.")]
    public int confirmStablePolls = 5;

    [Header("Scene")]
    public Transform carsParent;

    [Header("Debug")]
    public bool debugLogRawJson = false;
    public bool debugLogStateChanges = true;
    public bool debugLogStableCounter = false;

    private readonly string[] carNamesBySlot =
    {
        "Car 3","Car 4","Car 1","Car 13",
        "Car 15","Car 8","Car 9","Car 2","Car 5","Car 11",
        "Car 10","Car 12","Car 16","Car 7","Car 17","Car 18","Car 19",
        "Car 14","Car 6"
    };

    private readonly CarMover[] carsBySlot = new CarMover[19];

    private enum SlotState
    {
        Empty,
        Occupied
    }

    private readonly SlotState[] slotState = new SlotState[19];
    private readonly SlotState[] observedState = new SlotState[19];
    private readonly int[] observedStableCount = new int[19];
    private readonly string[] plateBySlot = new string[19];

    private bool firstSyncDone = false;
    private readonly Queue<int> spawnSlotQueue = new Queue<int>();
    private bool spawningCars = false;

    public int TotalSlots => slotState.Length;
    public int OccupiedSlots { get; private set; }
    public int AvailableSlots => TotalSlots - OccupiedSlots;

    public event Action<int, int, int> OnParkingStatsChanged;

    [Serializable]
    private class SlotDTO
    {
        public string plate;
        public int slot_number;
        public string status;
        public string ticket_id;
        public int vehicle_count;
    }

    [Serializable]
    private class SlotsResponseDTO
    {
        public string last_update;
        public SlotDTO[] slots;
    }

    private void Awake()
    {
        BuildCarsBySlot();

        for (int i = 0; i < slotState.Length; i++)
        {
            slotState[i] = SlotState.Empty;
            observedState[i] = SlotState.Empty;
            observedStableCount[i] = 0;
            plateBySlot[i] = "";
        }

        RecalculateStats(false);
    }

    private IEnumerator Start()
    {
        if (carsParent == null)
            Debug.LogError("[ParkingSim] carsParent is NULL");

        if (confirmStablePolls < 1)
            confirmStablePolls = 1;

        yield return new WaitForSeconds(initialDelay);
        StartCoroutine(PollSlotsLoop());
    }

    private IEnumerator PollSlotsLoop()
    {
        while (true)
        {
            yield return FetchSlots();
            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator FetchSlots()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[API ERROR] " + req.error + " url=" + apiUrl);
                yield break;
            }

            string json = req.downloadHandler.text;

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[API] JSON empty");
                yield break;
            }

            if (debugLogRawJson)
                Debug.Log("[API RAW] " + json);

            SlotsResponseDTO resp = null;

            try
            {
                resp = JsonUtility.FromJson<SlotsResponseDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("[JSON ERROR] " + ex.Message);
                yield break;
            }

            if (resp == null || resp.slots == null)
            {
                Debug.LogWarning("[API] slots null (parse failed or schema mismatch)");
                yield break;
            }

            ApplySlotStates(resp.slots);

            if (!firstSyncDone)
                firstSyncDone = true;
        }
    }

    private void ApplySlotStates(SlotDTO[] slots)
    {
        bool needSpawn = false;
        bool anyCommittedChange = false;
        bool anyPlateChanged = false;

        foreach (var s in slots)
        {
            if (s == null)
                continue;

            if (s.slot_number < 1 || s.slot_number > slotState.Length)
                continue;

            int slotIndex = s.slot_number - 1;
            SlotState rawState = ParseRawState(s.status);

            string newPlate = NormalizePlate(s.plate);
            if (plateBySlot[slotIndex] != newPlate)
            {
                plateBySlot[slotIndex] = newPlate;
                anyPlateChanged = true;
            }

            CarMover mover = carsBySlot[slotIndex];
            if (mover == null)
                continue;

            if (!HasValidWaypoints(mover))
            {
                Debug.LogError($"[ParkingSim] {carNamesBySlot[slotIndex]} points invalid (<2). Hãy setup Waypoints for this car trong Inspector.");
                continue;
            }

            if (debugLogStateChanges)
            {
                string plateText = string.IsNullOrWhiteSpace(plateBySlot[slotIndex]) ? "Trống" : plateBySlot[slotIndex];
                Debug.Log($"[API] slot {s.slot_number} raw={rawState}, plate={plateText}");
            }

            if (!firstSyncDone)
            {
                slotState[slotIndex] = rawState;
                observedState[slotIndex] = rawState;
                observedStableCount[slotIndex] = 1;

                if (rawState == SlotState.Occupied)
                    mover.TeleportToEnd();
                else
                    mover.HideCar();

                anyCommittedChange = true;
                continue;
            }

            if (rawState == observedState[slotIndex])
            {
                observedStableCount[slotIndex]++;
            }
            else
            {
                observedState[slotIndex] = rawState;
                observedStableCount[slotIndex] = 1;
            }

            if (debugLogStableCounter)
            {
                Debug.Log(
                    $"[STABLE] slot {s.slot_number} observed={observedState[slotIndex]} " +
                    $"count={observedStableCount[slotIndex]}/{confirmStablePolls} committed={slotState[slotIndex]}"
                );
            }

            if (observedState[slotIndex] == slotState[slotIndex])
                continue;

            if (observedStableCount[slotIndex] < confirmStablePolls)
                continue;

            if (slotState[slotIndex] == SlotState.Empty &&
                observedState[slotIndex] == SlotState.Occupied)
            {
                slotState[slotIndex] = SlotState.Occupied;
                spawnSlotQueue.Enqueue(slotIndex);
                needSpawn = true;
                anyCommittedChange = true;
            }
            else if (slotState[slotIndex] == SlotState.Occupied &&
                     observedState[slotIndex] == SlotState.Empty)
            {
                slotState[slotIndex] = SlotState.Empty;
                mover.ExitToBeginningAndHide();
                plateBySlot[slotIndex] = "";
                anyPlateChanged = true;
                anyCommittedChange = true;
            }

            observedStableCount[slotIndex] = confirmStablePolls;
        }

        if (anyCommittedChange || anyPlateChanged || !firstSyncDone)
            RecalculateStats(true);

        if (needSpawn && !spawningCars)
            StartCoroutine(SpawnCarsSequential());
    }

    private IEnumerator SpawnCarsSequential()
    {
        spawningCars = true;

        while (spawnSlotQueue.Count > 0)
        {
            int slotIndex = spawnSlotQueue.Dequeue();
            CarMover mover = carsBySlot[slotIndex];

            if (mover != null && HasValidWaypoints(mover))
            {
                mover.ResetToStartHidden();
                yield return new WaitForSeconds(0.25f);
                mover.StartFromBeginning();
            }

            yield return new WaitForSeconds(1.2f);
        }

        spawningCars = false;
    }

    private void RecalculateStats(bool notify)
    {
        int occupied = 0;

        for (int i = 0; i < slotState.Length; i++)
        {
            if (slotState[i] == SlotState.Occupied)
                occupied++;
        }

        bool changed = occupied != OccupiedSlots;
        OccupiedSlots = occupied;

        if (notify && (changed || !firstSyncDone))
            OnParkingStatsChanged?.Invoke(TotalSlots, OccupiedSlots, AvailableSlots);
    }

    public bool IsSlotOccupied(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > slotState.Length)
            return false;

        return slotState[slotNumber - 1] == SlotState.Occupied;
    }

    public string GetPlateBySlot(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > plateBySlot.Length)
            return "Trống";

        return string.IsNullOrWhiteSpace(plateBySlot[slotNumber - 1])
            ? "Trống"
            : plateBySlot[slotNumber - 1];
    }

    public string GetSlotBoxText(int slotNumber)
    {
        return $"Slot {slotNumber}\n{GetPlateBySlot(slotNumber)}";
    }

    private string NormalizePlate(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return "";

        return plate.Trim();
    }

    private SlotState ParseRawState(string status)
    {
        bool occupied =
            !string.IsNullOrEmpty(status) &&
            status.Trim().Equals("occupied", StringComparison.OrdinalIgnoreCase);

        return occupied ? SlotState.Occupied : SlotState.Empty;
    }

    private bool HasValidWaypoints(CarMover mover)
    {
        return mover != null &&
               mover.points != null &&
               mover.points.Length >= 2 &&
               mover.points[0] != null &&
               mover.points[mover.points.Length - 1] != null;
    }

    private void BuildCarsBySlot()
    {
        if (carsParent == null)
        {
            Debug.LogError("[ParkingSim] carsParent NULL");
            return;
        }

        for (int i = 0; i < carNamesBySlot.Length; i++)
        {
            Transform carT = carsParent.Find(carNamesBySlot[i]);

            if (carT == null)
            {
                Debug.LogError("[ParkingSim] Missing car " + carNamesBySlot[i]);
                continue;
            }

            CarMover mover = carT.GetComponent<CarMover>();
            if (mover == null)
                mover = carT.gameObject.AddComponent<CarMover>();

            carsBySlot[i] = mover;
        }
    }
}