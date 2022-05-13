using System.Diagnostics;
using System.Numerics;
using RoboScapeSimulator.Entities;
using RoboScapeSimulator.Entities.Robots;
using RoboScapeSimulator.IoTScape;
using RoboScapeSimulator.IoTScape.Devices;
namespace RoboScapeSimulator.Environments
{
    class IoTScapeExampleEnvironment2 : EnvironmentConfiguration
    {
        public IoTScapeExampleEnvironment2()
        {
            Name = "IoTScapeExampleEnvironment2";
            ID = "iotscape2";
            Description = "RadiationSensor Demo Environment";
        }

        IoTScapeObject? radiationSensor;

        PositionSensor? locationSensor;

        public override object Clone()
        {
            return new IoTScapeExampleEnvironment2();
        }

        public override void Setup(Room room)
        {
            Trace.WriteLine("Setting up IoTScape Example 2 environment");

            // Ground
            var ground = new Ground(room);

            // Walls
            float wallsize = 15;
            var wall1 = new Cube(room, wallsize, 1, 1, new Vector3(0, 0.5f, -wallsize / 2), Quaternion.Identity, true, nameOverride: "wall1");
            var wall2 = new Cube(room, wallsize, 1, 1, new Vector3(0, 0.5f, wallsize / 2), Quaternion.Identity, true, nameOverride: "wall2");
            var wall3 = new Cube(room, 1, 1, wallsize + 1, new Vector3(-wallsize / 2, 0.5f, 0), Quaternion.Identity, true, nameOverride: "wall3");
            var wall4 = new Cube(room, 1, 1, wallsize + 1, new Vector3(wallsize / 2, 0.5f, 0), Quaternion.Identity, true, nameOverride: "wall4");

            // Demo robot 
            var robot = new ParallaxRobot(room, debug: false);

            var barrel = new Cube(room, initialPosition: new Vector3(3, 0, 3), initialOrientation: Quaternion.Identity, visualInfo: new VisualInfo() { ModelName = "barrel.gltf", ModelScale = 0.4f }, isKinematic: true);

            locationSensor = new(robot);
            locationSensor.Setup(room);

            IoTScapeServiceDefinition radiationSensorDefinition = new(
                "RadiationSensor",
                new IoTScapeServiceDescription() { version = "1" },
                "",
                new Dictionary<string, IoTScapeMethodDescription>()
                {
                {"getIntensity", new IoTScapeMethodDescription(){
                    documentation = "Get radiation reading at current location",
                    paramsList = new List<IoTScapeMethodParams>(){},
                    returns = new IoTScapeMethodReturns(){type = new List<string>(){
                        "number"
                    }}
                }}
                },
                new Dictionary<string, IoTScapeEventDescription>());

            var sensorBody = robot.BodyReference;
            var targetBody = barrel.BodyReference;
            var targetIntensity = 100;

            radiationSensor = new(radiationSensorDefinition, robot.ID);
            radiationSensor.Methods["getIntensity"] = (string[] args) =>
            {
                float intensity = 0;
                float distance = (sensorBody.Pose.Position - targetBody.Pose.Position).LengthSquared();
                intensity = targetIntensity / distance;

                return new string[] { intensity.ToString() };
            };

            radiationSensor.Setup(room);

        }
    }
}