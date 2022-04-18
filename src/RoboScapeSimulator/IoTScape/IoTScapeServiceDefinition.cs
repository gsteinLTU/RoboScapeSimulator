using System.Text.Json.Serialization;

namespace RoboScapeSimulator.IoTScape
{
    [Serializable]
    public class IoTScapeServiceDefinition
    {
        [JsonInclude]
        public string name = "";

        [JsonInclude]
        public IoTScapeServiceDescription service;

        [JsonInclude]
        public string id = "";

        [JsonInclude]
        public Dictionary<string, IoTScapeMethodDescription> methods = new();

        [JsonInclude]
        public Dictionary<string, IoTScapeEventDescription> events = new();

        public IoTScapeServiceDefinition()
        {
            service = new IoTScapeServiceDescription();
        }

        public IoTScapeServiceDefinition(IoTScapeServiceDefinition other)
        {
            name = other.name;
            service = other.service;
            id = other.id;
            methods = other.methods;
            events = other.events;
        }

        public IoTScapeServiceDefinition(string name, IoTScapeServiceDescription service, string id, Dictionary<string, IoTScapeMethodDescription> methods, Dictionary<string, IoTScapeEventDescription> events)
        {
            this.name = name;
            this.service = service;
            this.id = id;
            this.methods = methods;
            this.events = events;
        }
    }
}