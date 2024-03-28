namespace EnergySmartBridge.MQTT
{
    public class DeviceRegistry
    {
        public string[,] connections { get; set; }
        public string hw_version { get; set; }
        public string[] identifiers { get; set; }
        public string name { get; set; }
        public string sw_version { get; set; }
        public string model { get; set; }
        public string manufacturer { get; set; }
    }
}
