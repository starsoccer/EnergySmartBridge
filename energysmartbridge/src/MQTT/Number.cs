using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EnergySmartBridge.MQTT
{
    public class Number : Device
    {
        public string command_topic { get; set; }

        public int? min { get; set; }
        public int? max { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum DisplayMode
        {
            auto,
            box,
            slider,
        }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DisplayMode? mode { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string unit_of_measurement { get; set; }
    }
}
