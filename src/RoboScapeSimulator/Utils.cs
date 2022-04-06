using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace RoboScapeSimulator
{
    public static class Utils
    {

        public static void ExtractYawPitchRoll(this Quaternion r, out float yaw, out float pitch, out float roll)
        {
            yaw = MathF.Atan2(2.0f * (r.Y * r.W + r.X * r.Z), 1.0f - 2.0f * (r.X * r.X + r.Y * r.Y));
            pitch = MathF.Asin(2.0f * (r.X * r.W - r.Y * r.Z));
            roll = MathF.Atan2(2.0f * (r.X * r.Y + r.Z * r.W), 1.0f - 2.0f * (r.X * r.X + r.Z * r.Z));
        }

        public static Vector3 PointOnCircle(this Random rng, float radius = 1, float height = 0)
        {
            return new(radius * MathF.Cos(rng.NextSingle() * 2 * MathF.PI), height, radius * MathF.Sin(rng.NextSingle() * 2 * MathF.PI));
        }

        /// <summary>
        /// Helper function to print a JToken
        /// </summary>
        public static void printJSON(JToken token)
        {
            if (token != null)
            {
                Debug.WriteLine(JsonConvert.SerializeObject(token, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, Converters = new List<JsonConverter>() { new SmallerFloatFormatConverter() } }));
            }
        }

        /// <summary>
        /// Helper function to print a JToken
        /// </summary>
        public static void printJSONArray(JToken[] tokens)
        {
            Array.ForEach(tokens, printJSON);
        }

        public static void sendAsJSON<T>(Node.Socket socket, string eventName, T data)
        {
            if (data != null)
            {
                try
                {
                    socket.Emit(eventName, JsonConvert.SerializeObject(data, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, Converters = new List<JsonConverter>() { new SmallerFloatFormatConverter() } }));
                }
                catch (System.Exception e)
                {
                    if (data is IDictionary<string, object> dict)
                    {
                        foreach (var entry in dict)
                        {
                            Console.WriteLine("\t" + entry.Key + ": " + entry.Value.ToString());
                        }
                    }
                    else
                    {
                        Trace.TraceError("Data: " + data.ToString());
                    }
                    Trace.TraceError(e.ToString());
                }
            }
            else
            {
                socket.Emit(eventName);
            }
        }

        public unsafe static bool QuickRayCast(Simulation simulation, Vector3 origin, Vector3 direction, float maxRange = 300)
        {
            int intersectionCount = 0;
            simulation.BufferPool.Take(1, out Buffer<RayHit> results);

            HitHandler hitHandler = new()
            {
                Hits = results,
                IntersectionCount = &intersectionCount
            };

            simulation.RayCast(origin, direction, maxRange / 100f, ref hitHandler);
            simulation.BufferPool.Return(ref results);
            return intersectionCount > 0;
        }

        private class SmallerFloatFormatConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(float) || objectType == typeof(double) || objectType == typeof(Half);
            }

            public override void WriteJson(JsonWriter writer, object? value,
                                           JsonSerializer serializer)
            {
                if (value is float f)
                {
                    if (MathF.Abs(f) < 0.001f)
                    {
                        writer.WriteValue("0");
                    }
                    else
                    {
                        writer.WriteValue(string.Format("{0:G" + (int)Math.Max(4, Math.Round(4 + MathF.Log10(MathF.Abs(f)))) + "}", value));
                    }
                }
            }

            public override bool CanRead
            {
                get { return false; }
            }

            public override object ReadJson(JsonReader reader, Type objectType,
                                         object? existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public struct RayHit
    {
        public Vector3 Normal;
        public float T;
        public CollidableReference Collidable;
        public bool Hit;
    }

    public unsafe struct HitHandler : IRayHitHandler
    {
        public Buffer<RayHit> Hits;
        public int* IntersectionCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
        {
            maximumT = t;
            ref var hit = ref Hits[ray.Id];
            // if (t < hit.T)
            // {
            //     if (hit.T != float.MaxValue)
            ++*IntersectionCount;
            hit.Normal = normal;
            hit.T = t;
            hit.Collidable = collidable;
            hit.Hit = true;
            // }
        }
    }
}