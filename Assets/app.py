
from __future__ import annotations

import os
from typing import Any, Dict, List, Optional, Tuple
from datetime import datetime

import pandas as pd
from fastapi import FastAPI, Query, HTTPException
from fastapi.middleware.cors import CORSMiddleware


DATA_PATH_DEFAULT = r"C:\Users\MSI\Downloads\parking_events_updated_1_16.csv"


def parse_dt(s: str) -> datetime:
    # CSV đang có format kiểu "2026-03-05 06:28:28"
    return datetime.fromisoformat(str(s).strip())


def compute_slot_xyz(slot_id: int, cols: int = 4, levels: int = 1) -> Dict[str, int]:
    """
    Map slot_id -> tọa độ giả lập để test 3D.
    Mặc định: 16 slot => grid 4x4, 1 tầng.
    Nếu bạn muốn nhiều tầng: tăng levels.
    """
    if cols <= 0 or levels <= 0:
        cols, levels = 4, 1

    # Tổng slot mỗi tầng (giả định chia đều)
    # rows_per_level được suy ra từ max_slot và cols
    # Với 16 slot, cols=4 => rows_total=4
    # Nếu levels>1 thì chia đều theo tầng (nếu không chia đều thì vẫn chạy được nhưng layout mang tính "test")
    return {"x": (slot_id - 1) % cols, "y": (slot_id - 1) // (cols * max(1, (16 // max(1, cols * levels)))), "z": (slot_id - 1) // max(1, (16 // levels))}


def load_events(csv_path: str) -> pd.DataFrame:
    if not os.path.exists(csv_path):
        raise FileNotFoundError(f"CSV not found at: {csv_path}")

    df = pd.read_csv(csv_path)

    required = {"event_id", "timestamp", "gate", "event_type", "vehicle_id", "vehicle_type", "plate", "slot_id", "group"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"CSV missing columns: {sorted(list(missing))}")

    df = df.copy()
    df["timestamp_dt"] = df["timestamp"].apply(parse_dt)
    df = df.sort_values(["timestamp_dt", "event_id"]).reset_index(drop=True)
    return df


def build_state_until(df: pd.DataFrame, until: datetime) -> Dict[str, Any]:
    """
    Duyệt event theo thời gian đến 'until' để dựng trạng thái bãi:
    occupancy_by_slot: slot_id -> vehicle info
    slot_by_vehicle: vehicle_id -> slot_id
    """
    occ_by_slot: Dict[int, Dict[str, Any]] = {}
    slot_by_vehicle: Dict[str, int] = {}

    sub = df[df["timestamp_dt"] <= until]

    for _, r in sub.iterrows():
        et = str(r["event_type"]).upper().strip()
        vehicle_id = str(r["vehicle_id"])
        slot_id = int(r["slot_id"])

        vehicle_info = {
            "vehicle_id": vehicle_id,
            "vehicle_type": r["vehicle_type"],
            "plate": r["plate"],
            "group": r["group"],
            "gate": r["gate"],
            "last_event_time": r["timestamp"],
            "slot_id": slot_id,
        }

        if et == "ENTER":
            # Nếu xe đang ở slot khác (dữ liệu lỗi), thì overwrite theo event mới
            old_slot = slot_by_vehicle.get(vehicle_id)
            if old_slot is not None and old_slot != slot_id:
                occ_by_slot.pop(old_slot, None)

            # Nếu slot đã có xe (dữ liệu lỗi), overwrite để test vẫn chạy
            occ_by_slot[slot_id] = vehicle_info
            slot_by_vehicle[vehicle_id] = slot_id

        elif et == "EXIT":
            # Ưu tiên clear theo vehicle_id nếu biết xe đang ở đâu
            cur_slot = slot_by_vehicle.get(vehicle_id)

            if cur_slot is not None:
                occ_by_slot.pop(cur_slot, None)
                slot_by_vehicle.pop(vehicle_id, None)
            else:
                # fallback: clear theo slot_id trong event
                occ = occ_by_slot.get(slot_id)
                if occ and occ.get("vehicle_id") == vehicle_id:
                    occ_by_slot.pop(slot_id, None)

        else:
            # Event lạ: bỏ qua
            continue

    return {
        "as_of": until.isoformat(sep=" "),
        "occupied_count": len(occ_by_slot),
        "occupancy_by_slot": occ_by_slot,  # key int
    }


app = FastAPI(
    title="Parking 3D Test API",
    version="1.0.0",
    description="API đọc parking_events CSV và cung cấp endpoints để test mô phỏng bãi đỗ xe 3D",
)

# CORS để bạn gọi từ web / 3D frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

CSV_PATH = os.getenv("PARKING_CSV", DATA_PATH_DEFAULT)
DF = load_events(CSV_PATH)


@app.get("/health")
def health():
    return {"ok": True, "csv_path": CSV_PATH, "rows": int(DF.shape[0])}


@app.get("/events")
def list_events(
    limit: int = Query(50, ge=1, le=1000),
    offset: int = Query(0, ge=0),
    vehicle_id: Optional[str] = None,
    slot_id: Optional[int] = None,
    event_type: Optional[str] = None,
    start: Optional[str] = Query(None, description="ISO datetime e.g. 2026-03-05 06:30:00"),
    end: Optional[str] = Query(None, description="ISO datetime e.g. 2026-03-05 08:00:00"),
):
    df = DF

    if vehicle_id:
        df = df[df["vehicle_id"].astype(str) == str(vehicle_id)]
    if slot_id is not None:
        df = df[df["slot_id"].astype(int) == int(slot_id)]
    if event_type:
        df = df[df["event_type"].astype(str).str.upper() == str(event_type).upper()]

    if start:
        sdt = parse_dt(start)
        df = df[df["timestamp_dt"] >= sdt]
    if end:
        edt = parse_dt(end)
        df = df[df["timestamp_dt"] <= edt]

    total = int(df.shape[0])
    page = df.iloc[offset : offset + limit]

    # Trả về JSON-friendly
    items = []
    for _, r in page.iterrows():
        items.append(
            {
                "event_id": int(r["event_id"]),
                "timestamp": r["timestamp"],
                "gate": r["gate"],
                "event_type": r["event_type"],
                "vehicle_id": r["vehicle_id"],
                "vehicle_type": r["vehicle_type"],
                "plate": r["plate"],
                "slot_id": int(r["slot_id"]),
                "group": r["group"],
            }
        )

    return {"total": total, "limit": limit, "offset": offset, "items": items}


@app.get("/events/{event_id}")
def get_event(event_id: int):
    row = DF[DF["event_id"].astype(int) == int(event_id)]
    if row.empty:
        raise HTTPException(status_code=404, detail="event_id not found")
    r = row.iloc[0]
    return {
        "event_id": int(r["event_id"]),
        "timestamp": r["timestamp"],
        "gate": r["gate"],
        "event_type": r["event_type"],
        "vehicle_id": r["vehicle_id"],
        "vehicle_type": r["vehicle_type"],
        "plate": r["plate"],
        "slot_id": int(r["slot_id"]),
        "group": r["group"],
    }


@app.get("/slots")
def list_slots(
    cols: int = Query(4, ge=1, le=50),
    levels: int = Query(1, ge=1, le=20),
):
    slots = sorted(set(int(x) for x in DF["slot_id"].unique()))
    # Tọa độ giả lập để FE 3D có thể đặt xe vào vị trí
    items = []
    for sid in slots:
        items.append({"slot_id": sid, "pos": compute_slot_xyz(sid, cols=cols, levels=levels)})

    return {
        "slot_count": len(items),
        "cols": cols,
        "levels": levels,
        "items": items,
    }


@app.get("/state")
def state_at(
    at: Optional[str] = Query(None, description="ISO datetime. Nếu không truyền, lấy thời điểm mới nhất trong dữ liệu."),
    cols: int = Query(4, ge=1, le=50),
    levels: int = Query(1, ge=1, le=20),
):
    if at:
        dt = parse_dt(at)
    else:
        dt = DF["timestamp_dt"].max().to_pydatetime()

    state = build_state_until(DF, dt)

    # convert occupancy keys int -> string để JSON ổn định
    occ = state["occupancy_by_slot"]
    occ_out: Dict[str, Any] = {}
    for slot_id, vinfo in occ.items():
        occ_out[str(slot_id)] = {
            **vinfo,
            "pos": compute_slot_xyz(int(slot_id), cols=cols, levels=levels),
        }

    return {
        "as_of": state["as_of"],
        "occupied_count": state["occupied_count"],
        "occupancy_by_slot": occ_out,
    }


@app.get("/timeline")
def timeline(
    start: Optional[str] = Query(None, description="ISO datetime"),
    end: Optional[str] = Query(None, description="ISO datetime"),
    step_seconds: int = Query(30, ge=1, le=3600),
    cols: int = Query(4, ge=1, le=50),
    levels: int = Query(1, ge=1, le=20),
):
    df = DF
    min_dt = df["timestamp_dt"].min().to_pydatetime()
    max_dt = df["timestamp_dt"].max().to_pydatetime()

    sdt = parse_dt(start) if start else min_dt
    edt = parse_dt(end) if end else max_dt

    if edt < sdt:
        raise HTTPException(status_code=400, detail="end must be >= start")

    frames = []
    cur = sdt
    # dựng frame trạng thái theo từng bước thời gian
    from datetime import timedelta
    while cur <= edt:
        st = build_state_until(df, cur)
        occ_out = {}
        for slot_id, vinfo in st["occupancy_by_slot"].items():
            occ_out[str(slot_id)] = {**vinfo, "pos": compute_slot_xyz(int(slot_id), cols=cols, levels=levels)}
        frames.append(
            {
                "t": cur.isoformat(sep=" "),
                "occupied_count": st["occupied_count"],
                "occupancy_by_slot": occ_out,
            }
        )
        cur += timedelta(seconds=step_seconds)

    return {
        "start": sdt.isoformat(sep=" "),
        "end": edt.isoformat(sep=" "),
        "step_seconds": step_seconds,
        "frames": frames,
    }