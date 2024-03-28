using EnergySmartBridge.MQTT;
using EnergySmartBridge.WebService;
using log4net;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace EnergySmartBridge.Modules
{
    public class MQTTModule : IModule
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private WebServerModule WebServer { get; set; }
        private IManagedMqttClient MqttClient { get; set; }

        private readonly Regex regexTopic = new Regex(Global.mqtt_prefix + "/([A-F0-9]+)/(.*)", RegexOptions.Compiled);

        private readonly ConcurrentDictionary<string, Queue<WaterHeaterOutput>> connectedModules = new ConcurrentDictionary<string, Queue<WaterHeaterOutput>>();

        private readonly AutoResetEvent trigger = new AutoResetEvent(false);

        public MQTTModule(WebServerModule webServer)
        {
            WebServer = webServer;
            // Energy Smart module posts to this URL
            WebServer.RegisterPrefix(ProcessRequest, new string[] { "/~branecky/postAll.php" } );
        }

        public void Startup()
        {
            MqttApplicationMessage lastwill = new MqttApplicationMessage()
            {
                Topic = $"{Global.mqtt_prefix}/status",
                Payload = Encoding.UTF8.GetBytes("offline"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Retain = true
            };

            MqttClientOptionsBuilder options = new MqttClientOptionsBuilder()
                .WithTcpServer(Global.mqtt_server)
                .WithWillMessage(lastwill);

            if (!string.IsNullOrEmpty(Global.mqtt_username))
                options = options
                    .WithCredentials(Global.mqtt_username, Global.mqtt_password);

            ManagedMqttClientOptions manoptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options.Build())
                .Build();

            MqttClient = new MqttFactory().CreateManagedMqttClient();
            MqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
            {
                log.Debug("Connected");

                // Clear cache so we publish config on next check-in
                connectedModules.Clear();

                log.Debug("Publishing controller online");
                PublishAsync($"{Global.mqtt_prefix}/status", "online");
            });
            MqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate((e) => log.Debug("Error connecting " + e.Exception.Message));
            MqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate((e) => log.Debug("Disconnected"));

            MqttClient.StartAsync(manoptions);

            MqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnAppMessage);

            // Subscribe to notifications for these command topics
            List<Topic> toSubscribe = new List<Topic>()
            {
                Topic.updaterate_command,
                Topic.mode_command,
                Topic.setpoint_command
            };

            toSubscribe.ForEach((command) => MqttClient.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic($"{Global.mqtt_prefix}/+/{command}").Build()));

            // Wait until shutdown
            trigger.WaitOne();

            log.Debug("Publishing controller offline");
            PublishAsync($"{Global.mqtt_prefix}/status", "offline");

            MqttClient.StopAsync();
        }

        protected virtual void OnAppMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            Match match = regexTopic.Match(e.ApplicationMessage.Topic);

            if (!match.Success)
                return;

            if (!Enum.TryParse(match.Groups[2].Value, true, out Topic topic))
                return;

            string id = match.Groups[1].Value;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            log.Debug($"Received: Id: {id}, Command: {topic}, Value: {payload}");

            if(connectedModules.ContainsKey(id))
            {
                if (topic == Topic.updaterate_command && 
                    int.TryParse(payload, out int updateRate) && updateRate >= 30 && updateRate <= 300)
                {
                    log.Debug($"Queued {id} UpdateRate: {updateRate}");
                    connectedModules[id].Enqueue(new WaterHeaterOutput()
                    {
                        UpdateRate = updateRate.ToString()
                    });
                }
                else if (topic == Topic.mode_command)
                {
                    string heater_mode = payload switch
                    {
                        "heat_pump" => "Efficiency",
                        "eco" => "Hybrid",
                        "electric" => "Electric",
                        "off" => "Vacation",
                        _ => null,
                    };

                    log.Debug($"Queued {id} Mode: {heater_mode}");

                    connectedModules[id].Enqueue(new WaterHeaterOutput()
                    {
                        Mode = heater_mode
                    });
                }
                else if (topic == Topic.setpoint_command &&
                    double.TryParse(payload, out double setPoint) && setPoint >= 80 && setPoint <= 150)
                {
                    log.Debug($"Queued {id} SetPoint: {((int)setPoint)}");
                    connectedModules[id].Enqueue(new WaterHeaterOutput()
                    {
                        SetPoint = ((int)setPoint).ToString()
                    });
                }
            }
        }

        public void Shutdown()
        {
            trigger.Set();
        }

        private object ProcessRequest(HttpListenerRequest request)
        {
            string content = new System.IO.StreamReader(request.InputStream).ReadToEnd();

            log.Debug($"URL: {request.RawUrl}\n{content}");

            WaterHeaterInput waterHeater = HttpUtility.ParseQueryString(content).ToObject<WaterHeaterInput>();

            if(!connectedModules.ContainsKey(waterHeater.DeviceText))
            {
                log.Debug($"Publishing water heater config {waterHeater.DeviceText}");
                PublishWaterHeater(waterHeater);
                connectedModules.TryAdd(waterHeater.DeviceText, new Queue<WaterHeaterOutput>());
            }

            log.Debug($"Publishing water heater state {waterHeater.DeviceText}");
            PublishWaterHeaterState(waterHeater);

            if (connectedModules[waterHeater.DeviceText].Count > 0)
            {
                object response = connectedModules[waterHeater.DeviceText].Dequeue();
                log.Debug($"Sent queued command {waterHeater.DeviceText} {JsonConvert.SerializeObject(response)}");
                return response;
            }
            else
            {
                return new WaterHeaterOutput() { };
            }
        }

        private void PublishWaterHeater(WaterHeaterInput waterHeater)
        {
            PublishAsync($"{Global.mqtt_discovery_prefix}/water_heater/{waterHeater.DeviceText}/config",
                JsonConvert.SerializeObject(waterHeater.ToThermostatConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/heating/config",
                JsonConvert.SerializeObject(waterHeater.ToInHeatingConfig()));
            
            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/grid/config",
                JsonConvert.SerializeObject(waterHeater.ToGridConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/airfilterstatus/config",
                JsonConvert.SerializeObject(waterHeater.ToAirFilterStatusConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/condensepumpfail/config",
                JsonConvert.SerializeObject(waterHeater.ToCondensePumpFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/leakdetect/config",
                JsonConvert.SerializeObject(waterHeater.ToLeakDetectConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/ecoerror/config",
                JsonConvert.SerializeObject(waterHeater.ToEcoErrorConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/rawmode/config",
                JsonConvert.SerializeObject(waterHeater.ToRawModeConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/hotwatervol/config",
                JsonConvert.SerializeObject(waterHeater.ToHotWaterVolConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/uppertemp/config",
                JsonConvert.SerializeObject(waterHeater.ToUpperTempConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/lowertemp/config",
                JsonConvert.SerializeObject(waterHeater.ToLowerTempConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/number/{waterHeater.DeviceText}/updaterate/config",
                JsonConvert.SerializeObject(waterHeater.ToUpdateRateConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/dryfire/config",
                JsonConvert.SerializeObject(waterHeater.ToDryFireConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/elementfail/config",
                JsonConvert.SerializeObject(waterHeater.ToElementFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/tanksensorfail/config",
                JsonConvert.SerializeObject(waterHeater.ToTankSensorFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/leak/config",
                JsonConvert.SerializeObject(waterHeater.ToLeakConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/masterdispfail/config",
                JsonConvert.SerializeObject(waterHeater.ToMasterDispFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/compsensorfail/config",
                JsonConvert.SerializeObject(waterHeater.ToCompSensorFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/syssensorfail/config",
                JsonConvert.SerializeObject(waterHeater.ToSysSensorFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/binary_sensor/{waterHeater.DeviceText}/systemfail/config",
                JsonConvert.SerializeObject(waterHeater.ToSystemFailConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/faultcodes/config",
                JsonConvert.SerializeObject(waterHeater.ToFaultCodesConfig()));

            PublishAsync($"{Global.mqtt_discovery_prefix}/sensor/{waterHeater.DeviceText}/signalstrength/config",
                JsonConvert.SerializeObject(waterHeater.ToSignalStrengthConfig()));
        }

        private void PublishWaterHeaterState(WaterHeaterInput waterHeater)
        {
            PublishAsync(waterHeater.ToTopic(Topic.maxsetpoint_state), waterHeater.MaxSetPoint.ToString());
            PublishAsync(waterHeater.ToTopic(Topic.setpoint_state), waterHeater.SetPoint.ToString());

            string ha_mode = waterHeater.Mode switch
            {
                "Efficiency" => "heat_pump",
                "Hybrid" => "eco",
                "Electric" => "electric",
                "Vacation" => "off",
                _ => "unknown",
            };

            PublishAsync(waterHeater.ToTopic(Topic.mode_state), ha_mode);

            PublishAsync(waterHeater.ToTopic(Topic.systeminheating_state), waterHeater.SystemInHeating ? "ON" : "OFF");
            PublishAsync(waterHeater.ToTopic(Topic.hotwatervol_state), waterHeater.HotWaterVol);

            PublishAsync(waterHeater.ToTopic(Topic.uppertemp_state), waterHeater.UpperTemp.ToString());
            PublishAsync(waterHeater.ToTopic(Topic.lowertemp_state), waterHeater.LowerTemp.ToString());

            PublishAsync(waterHeater.ToTopic(Topic.updaterate_state), waterHeater.UpdateRate.ToString());

            PublishAsync(waterHeater.ToTopic(Topic.dryfire_state), waterHeater.DryFire.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.elementfail_state), waterHeater.ElementFail.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.tanksensorfail_state), waterHeater.TankSensorFail.OffIfNone());

            PublishAsync(waterHeater.ToTopic(Topic.faultcodes_state), waterHeater.FaultCodes);

            PublishAsync(waterHeater.ToTopic(Topic.signalstrength_state), waterHeater.SignalStrength);

            PublishAsync(waterHeater.ToTopic(Topic.raw_mode_state), waterHeater.Mode);

            PublishAsync(waterHeater.ToTopic(Topic.grid_state), waterHeater.Grid == "Disabled" ? "OFF" : "ON");
            PublishAsync(waterHeater.ToTopic(Topic.air_filter_status_state), waterHeater.AirFilterStatus == "OK" ? "OFF" : "ON");
            PublishAsync(waterHeater.ToTopic(Topic.condense_pump_fail_state), waterHeater.CondensePumpFail ? "ON" : "OFF");
            PublishAsync(waterHeater.ToTopic(Topic.leak_detect_state), waterHeater.LeakDetect == "NotDetected" ? "OFF" : "ON");
            PublishAsync(waterHeater.ToTopic(Topic.eco_error_state), waterHeater.EcoError ? "ON" : "OFF" );

            PublishAsync(waterHeater.ToTopic(Topic.leak_state), waterHeater.Leak.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.master_disp_fail_state), waterHeater.MasterDispFail.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.comp_sensor_fail_state), waterHeater.CompSensorFail.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.sys_sensor_fail_state), waterHeater.SysSensorFail.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.system_fail_state), waterHeater.SystemFail.OffIfNone());
            PublishAsync(waterHeater.ToTopic(Topic.fault_codes_state), waterHeater.FaultCodes.ToString());
        }

        private Task PublishAsync(string topic, string payload)
        {
            return MqttClient.PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtMostOnce, true);
        }
    }
}
