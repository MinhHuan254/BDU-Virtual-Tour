using System;
using System.Collections.Generic;

[Serializable]
public class DeviceResponse
{
    public List<IoTDevice> devices;
}

[Serializable]
public class IoTDevice
{
    public string device_id;
    public string ten_thiet_bi;
    public string loai_thiet_bi;
    public string trang_thai;
    public long last_seen;
    public int phong_id;

    public Dictionary<string, DeviceDataItem> data;
}

[Serializable]
public class DeviceDataItem
{
    public object value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}