using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace RoboScapeSimulator.API;

/// <summary>
/// API module providing information about this server 
/// </summary>
public class ServerModule : WebApiModule
{

    public ServerModule(string baseRoute) : base(baseRoute)
    {
        AddHandler(HttpVerbs.Get, RouteMatcher.Parse("/status", false), (context, route) =>
        {
            return context.SendAsJSON(new ServerStatus
            {
                activeRooms = Program.Rooms.Count(kvp => !kvp.Value.Hibernating),
                hibernatingRooms = Program.Rooms.Count(kvp => kvp.Value.Hibernating),
                maxRooms = SettingsManager.MaxRooms
            });
        });
    }

    [Serializable]
    public struct ServerStatus
    {
        public int activeRooms;
        public int hibernatingRooms;
        public int maxRooms;
    }
}