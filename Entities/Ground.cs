using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace RoboScapeSimulator.Entities
{
    /// <summary>
    /// A static planar surface
    /// </summary>
    class Ground : StaticEntity
    {
        private static uint ID = 0;

        public Ground(Room room, float xsize = 200, float zsize = 100, Vector3? position = null, float thickness = 0.1f, VisualInfo visualInfo = default)
        {
            Name = $"ground_{ID++}";
            VisualInfo = visualInfo;
            var simulationInstance = room.SimInstance;
            var groundHandle = simulationInstance.Simulation.Statics.Add(new StaticDescription(position ?? new Vector3(0, -thickness / 2, 0), new CollidableDescription(simulationInstance.Simulation.Shapes.Add(new Box(xsize, thickness, zsize)), 0.1f)));
            StaticReference = simulationInstance.Simulation.Statics.GetStaticReference(groundHandle);

            room.SimInstance.NamedStatics.Add(Name, StaticReference);
            room.SimInstance.Entities.Add(this);
        }
    }
}