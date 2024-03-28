using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EnergySmartBridge.MQTT
{
    public class BinarySensor : Device
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum DeviceClass
        {
            heat,
            problem,
            moisture,
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceClass? device_class { get; set; }
    }
}
