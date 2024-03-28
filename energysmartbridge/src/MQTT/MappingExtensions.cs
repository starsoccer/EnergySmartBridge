using EnergySmartBridge.WebService;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnergySmartBridge.MQTT
{
    public static class MappingExtensions
    {
        public static string ToTopic(this WaterHeaterInput waterHeater, Topic topic)
        {
            return $"{Global.mqtt_prefix}/{waterHeater.DeviceText}/{topic}";
        }

        public static string ToMacAddress(this string rawMac)
        {
            return string.Join(":", Regex.Split(rawMac, @"(?<=\G[0-9A-Fa-f]{2})(?!$)"));
        }

        public static string OffIfNone(this string v)
        {
            return (v == "None") ? "OFF" : "ON";
        }

        public static string GetDisplayName(this WaterHeaterInput waterHeater)
        {
            return waterHeater.DeviceText.Substring(waterHeater.DeviceText.Length - 4) + " Water Heater";
        }

        public static T Init<T>(this T device, WaterHeaterInput waterHeater, string name = null)
            where T : Device
        {
            string unique_id_slug = (name ?? "__default__").ToLowerInvariant().Replace(' ', '_');

            device.name = name;
            device.unique_id = $"energysmart:{waterHeater.DeviceText}:{unique_id_slug}";
            device.device = new DeviceRegistry()
            {
                name = waterHeater.GetDisplayName(),

                identifiers = new string[]
                {
                    $"energysmart:{waterHeater.DeviceText}",
                },
                connections = new string[,]
                {
                    {"mac", waterHeater.DeviceText.ToMacAddress()},
                },

                manufacturer = "EnergySmart",
                model = waterHeater.MasterModelId,

                hw_version = waterHeater.MasterFwVer,
                sw_version = waterHeater.WifiFwVer,
            };

            return device;
        }

        public static Climate ToThermostatConfig(this WaterHeaterInput waterHeater)
        {
            return new Climate
            {
                action_template = "{% if value == 'ON' %} heating {%- else -%} off {%- endif %}",
                action_topic = waterHeater.ToTopic(Topic.systeminheating_state),
                current_temperature_topic = waterHeater.ToTopic(Topic.uppertemp_state),

                temperature_state_topic = waterHeater.ToTopic(Topic.setpoint_state),
                temperature_command_topic = waterHeater.ToTopic(Topic.setpoint_command),

                max_temp = waterHeater.MaxSetPoint.ToString(),

                mode_state_topic = waterHeater.ToTopic(Topic.mode_state),
                mode_command_topic = waterHeater.ToTopic(Topic.mode_command),
                modes = new List<string> { "eco", "heat_pump", "electric", "off" },
            }.Init(waterHeater);
        }

        public static BinarySensor ToInHeatingConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.systeminheating_state),
                device_class = BinarySensor.DeviceClass.heat,
            }.Init(waterHeater, "Element");
        }

        public static Sensor ToRawModeConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.raw_mode_state),
                device_class = Sensor.DeviceClass.@enum,
            }.Init(waterHeater, "Raw Mode");
        }

        public static BinarySensor ToGridConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.grid_state),
                entity_category = Device.EntityCategory.diagnostic,
            }.Init(waterHeater, "Grid Control Enabled");
        }

        public static BinarySensor ToAirFilterStatusConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.air_filter_status_state),
                device_class = BinarySensor.DeviceClass.problem,
                entity_category = Device.EntityCategory.diagnostic,
            }.Init(waterHeater, "Air Filter Status");
        }

        public static BinarySensor ToCondensePumpFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.condense_pump_fail_state),
                device_class = BinarySensor.DeviceClass.problem,
                entity_category = Device.EntityCategory.diagnostic,
            }.Init(waterHeater, "Condensate Pump Fail");
        }

        public static BinarySensor ToLeakDetectConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.leak_detect_state),
                device_class = BinarySensor.DeviceClass.moisture,
                entity_category = Device.EntityCategory.diagnostic,
            }.Init(waterHeater, "Leak Detected");
        }

        public static Sensor ToHotWaterVolConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.hotwatervol_state),
                device_class = Sensor.DeviceClass.@enum,
            }.Init(waterHeater, "Hot Water Volume");
        }

        public static Sensor ToUpperTempConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.uppertemp_state),
                device_class = Sensor.DeviceClass.temperature,
                unit_of_measurement = "°" + waterHeater.Units,
            }.Init(waterHeater, "Upper Temperature");
        }

        public static Sensor ToLowerTempConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.lowertemp_state),
                device_class = Sensor.DeviceClass.temperature,
                unit_of_measurement = "°" + waterHeater.Units,
            }.Init(waterHeater, "Lower Temperature");
        }

        public static Number ToUpdateRateConfig(this WaterHeaterInput waterHeater)
        {
            return new Number
            {
                state_topic = waterHeater.ToTopic(Topic.updaterate_state),
                command_topic = waterHeater.ToTopic(Topic.updaterate_command),
                min = 30,
                max = 300,
                mode = Number.DisplayMode.box,
                unit_of_measurement = "s",
                entity_category = Device.EntityCategory.config,
            }.Init(waterHeater, "Update Rate");
        }

        public static BinarySensor ToDryFireConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.dryfire_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Dry Fire");
        }

        public static BinarySensor ToElementFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.elementfail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Element Fail");
        }

        public static BinarySensor ToTankSensorFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.tanksensorfail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Tank Sensor Fail");
        }

        public static BinarySensor ToEcoErrorConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.eco_error_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Energy Cut-Off Error");
        }

        public static BinarySensor ToLeakConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.leak_state),
                entity_category = Device.EntityCategory.diagnostic,
		device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Leak");
        }

        public static BinarySensor ToMasterDispFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.master_disp_fail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Master Display Fail");
        }

        public static BinarySensor ToCompSensorFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.comp_sensor_fail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "Compressor Sensor Fail");
        }

        public static BinarySensor ToSysSensorFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.sys_sensor_fail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "System Sensor Fail");
        }

        public static BinarySensor ToSystemFailConfig(this WaterHeaterInput waterHeater)
        {
            return new BinarySensor
            {
                state_topic = waterHeater.ToTopic(Topic.system_fail_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = BinarySensor.DeviceClass.problem,
            }.Init(waterHeater, "System Fail");
        }

        public static Sensor ToFaultCodesConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.fault_codes_state),
                entity_category = Device.EntityCategory.diagnostic,
            }.Init(waterHeater, "Fault Codes");
        }

        public static Sensor ToSignalStrengthConfig(this WaterHeaterInput waterHeater)
        {
            return new Sensor
            {
                state_topic = waterHeater.ToTopic(Topic.signalstrength_state),
                entity_category = Device.EntityCategory.diagnostic,
                device_class = Sensor.DeviceClass.signal_strength,
                unit_of_measurement = "dBm",
            }.Init(waterHeater, "Signal Strength");
        }

    }
}
