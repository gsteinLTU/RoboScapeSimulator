using System.Diagnostics;
using System.Numerics;
using RoboScapeSimulator.Entities;
using RoboScapeSimulator.Entities.Robots;
using RoboScapeSimulator.IoTScape;
using RoboScapeSimulator.IoTScape.Devices;

namespace RoboScapeSimulator.Environments;

class WaypointNavigationEnvironment : EnvironmentConfiguration
{
    public WaypointNavigationEnvironment()
    {
        Name = "Waypoint Navigation";
        ID = "WaypointNavigation";
        Description = "Robot must find waypoints";
    }

    public override object Clone()
    {
        return new WaypointNavigationEnvironment();
    }

    public override void Setup(Room room)
    {
        Trace.WriteLine($"Setting up {Name} environment");
        Random rng = new();

        // Ground
        _ = new Ground(room, visualInfo: new VisualInfo() { Color = "#222" });

        // robot
        var robot = new ParallaxRobot(room, new Vector3(0, 0.25f, 0), Quaternion.Identity, debug: false);

        // Waypoints
        List<Vector3> waypoints = new()
        {
            rng.PointOnLine(new(-3f, 0, 2), new(2, 0, 2)),
            rng.PointOnLine(new(-2, 0, 4), new(3f, 0, 4)),
            rng.PointOnLine(new(-2.5f, 0, 6), new(2.5f, 0, 6))
        };

        int waypoint_idx = 0;

        List<VisualOnlyEntity> Markers = new();

        // Waypoint trigger
        var waypoint = new Trigger(room, waypoints[0], Quaternion.Identity, 0.2f, 0.1f, 0.2f);
        var waypoint_X_1 = new VisualOnlyEntity(room, initialPosition: waypoint.Position, initialOrientation: Quaternion.CreateFromAxisAngle(Vector3.UnitY, 45), width: 0.1f, height: 0.05f, depth: 0.5f, visualInfo: new VisualInfo() { Color = "#633" });
        var waypoint_X_2 = new VisualOnlyEntity(room, initialPosition: waypoint.Position, initialOrientation: Quaternion.CreateFromAxisAngle(Vector3.UnitY, -45), width: 0.1f, height: 0.05f, depth: 0.5f, visualInfo: new VisualInfo() { Color = "#633" });

        waypoint.OnTriggerEnter += (o, e) =>
        {
            // Move waypoint
            if (waypoint_idx <= waypoints.Count)
            {
                if (Markers.Count <= waypoint_idx)
                {
                    Markers.Add(new VisualOnlyEntity(room, initialPosition: waypoint.Position, initialOrientation: Quaternion.Identity, width: 0.25f, height: 0.1f, depth: 0.25f, visualInfo: new VisualInfo() { Color = "#363" }));
                }
                else
                {
                    Markers[waypoint_idx].Position = waypoint.Position;
                }

                if (waypoint_idx < waypoints.Count - 1)
                {
                    waypoint_idx++;
                    waypoint.Position = waypoints[waypoint_idx];
                    waypoint_X_1.Position = waypoints[waypoint_idx];
                    waypoint_X_2.Position = waypoints[waypoint_idx];
                }
                else
                {
                    waypoint_idx++;
                }

            }
        };

        // IoTScape setup
        IoTScapeServiceDefinition waypointServiceDefinition = new(
            "WaypointList",
            new IoTScapeServiceDescription() { version = "1" },
            "",
            new Dictionary<string, IoTScapeMethodDescription>()
            {
                {"getNextWaypoint", new IoTScapeMethodDescription(){
                    documentation = "Get the next waypoint to navigate to",
                    paramsList = new List<IoTScapeMethodParams>(),
                    returns = new IoTScapeMethodReturns(){type = new List<string>(){
                        "number","number","number"
                    }}
                }}
            },
            new Dictionary<string, IoTScapeEventDescription>());

        IoTScapeObject waypointService = new(waypointServiceDefinition, robot.ID);
        waypointService.Methods["getNextWaypoint"] = (string[] args) =>
        {
            return new string[] { waypoint.Position.X.ToString(), waypoint.Position.Y.ToString(), waypoint.Position.Z.ToString() };
        };
        waypointService.Setup(room);

        PositionSensor positionSensor = new(robot);
        positionSensor.Setup(room);

        room.OnReset += (room, _) =>
        {
            waypoints.Clear();

            waypoints.Add(rng.PointOnLine(new(-3f, 0, 2), new(2, 0, 2)));
            waypoints.Add(rng.PointOnLine(new(-2, 0, 4), new(3f, 0, 4)));
            waypoints.Add(rng.PointOnLine(new(-2.5f, 0, 6), new(2.5f, 0, 6)));

            waypoint_idx = 0;
            waypoint.Position = waypoints[waypoint_idx];
            waypoint_X_1.Position = waypoint.Position;
            waypoint_X_2.Position = waypoint.Position;

            Markers.ForEach(marker =>
            {
                marker.Position = -Vector3.UnitY;
            });
        };
    }
}