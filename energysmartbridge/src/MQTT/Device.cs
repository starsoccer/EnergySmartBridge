using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EnergySmartBridge.MQTT
{
    public class Device
    {
        public string name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state_topic { get; set; }

        public string availability_topic { get; set; } = $"{Global.mqtt_prefix}/status";

        public string unique_id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum EntityCategory
        {
            config,
            diagnostic,
        }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public EntityCategory? entity_category { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DeviceRegistry device { get; set; }
    }
}
