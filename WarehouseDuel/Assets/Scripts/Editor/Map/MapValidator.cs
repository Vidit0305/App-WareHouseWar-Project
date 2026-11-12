using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.Environment;

namespace WarehouseDuel.EditorTools.Map
{
    /// <summary>
    /// Automated checks on the currently open map scene, using real physics queries against its colliders:
    /// spawn clearance, floor-level connectivity between spawns, number of routes through each partition line,
    /// no direct sight line between spawns, and that the map is sealed (no way out).
    /// </summary>
    internal static class MapValidator
    {
        private const float PlayerRadius = 0.4f;
        private const float PlayerHeight = 1.8f;
        private const float Cell = 0.5f;

        public static void Run(SetupReport report)
        {
            Physics.SyncTransforms();

            int mask = LayerMask.GetMask(GameConstants.Layers.Environment);
            if (mask == 0)
            {
                report.Error("Environment layer is missing; run the Phase 1 setup.");
                return;
            }

            MapBounds bounds = Object.FindFirstObjectByType<MapBounds>();
            SpawnPoint[] spawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).OrderBy(s => s.Slot).ToArray();
            if (bounds == null) { report.Error("No MapBounds in the scene."); return; }
            if (spawns.Length != 2 || spawns[0].Slot != 0 || spawns[1].Slot != 1)
            {
                report.Error($"Expected exactly two spawn points (slots 0 and 1), found {spawns.Length}.");
                return;
            }

            CheckSpawns(report, spawns, mask);
            CheckConnectivity(report, bounds.PlayableArea, spawns, mask);
            CheckSealed(report, bounds.PlayableArea, mask);
        }

        private static void CheckSpawns(SetupReport report, SpawnPoint[] spawns, int mask)
        {
            foreach (SpawnPoint spawn in spawns)
            {
                if (IsStandable(spawn.Position, mask, out _)) report.Pass($"Spawn {spawn.Slot} is clear and on the floor.");
                else report.Error($"Spawn {spawn.Slot} at {spawn.Position} is blocked or not on the floor.");

                Vector3 toCentre = (Vector3.zero - spawn.Position).normalized;
                if (Vector3.Dot(spawn.transform.forward, toCentre) < 0.5f) report.Warn($"Spawn {spawn.Slot} is not facing the map centre.");
            }

            float d0 = spawns[0].Position.magnitude, d1 = spawns[1].Position.magnitude;
            if (Mathf.Abs(d0 - d1) < 0.01f) report.Pass("Spawns are equidistant from the centre.");
            else report.Error($"Spawns are not symmetric ({d0:F2} m vs {d1:F2} m from centre).");

            Vector3 eye = Vector3.up * 1.6f;
            if (Physics.Linecast(spawns[0].Position + eye, spawns[1].Position + eye, mask, QueryTriggerInteraction.Ignore))
                report.Pass("No direct line of sight between the two spawns.");
            else
                report.Error("The spawns can see each other directly (spawn-to-spawn sniping).");
        }

        private static void CheckConnectivity(SetupReport report, Bounds area, SpawnPoint[] spawns, int mask)
        {
            int nx = Mathf.FloorToInt(area.size.x / Cell);
            int nz = Mathf.FloorToInt(area.size.z / Cell);
            var origin = new Vector3(area.min.x + Cell / 2f, 0f, area.min.z + Cell / 2f);

            var walkable = new bool[nx, nz];
            int walkableCount = 0;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    walkable[ix, iz] = IsStandable(origin + new Vector3(ix * Cell, 0f, iz * Cell), mask, out _);
                    if (walkable[ix, iz]) walkableCount++;
                }
            }

            (int x, int z) Index(Vector3 p) => (Mathf.Clamp(Mathf.RoundToInt((p.x - origin.x) / Cell), 0, nx - 1),
                                                Mathf.Clamp(Mathf.RoundToInt((p.z - origin.z) / Cell), 0, nz - 1));

            var start = Index(spawns[0].Position);
            var goal = Index(spawns[1].Position);
            var reached = new bool[nx, nz];
            var queue = new Queue<(int x, int z)>();
            if (walkable[start.x, start.z]) { reached[start.x, start.z] = true; queue.Enqueue(start); }

            int reachedCount = 0;
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                reachedCount++;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ax = x + dx, az = z + dz;
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz || reached[ax, az] || !walkable[ax, az]) continue;
                    reached[ax, az] = true;
                    queue.Enqueue((ax, az));
                }
            }

            if (reached[goal.x, goal.z]) report.Pass("Spawn B is reachable from spawn A on foot.");
            else report.Error("Spawn B cannot be reached from spawn A at floor level.");

            int isolated = walkableCount - reachedCount;
            float coverage = 100f * reachedCount / Mathf.Max(1, walkableCount);
            if (isolated <= 8) report.Pass($"Floor connectivity: {coverage:F1}% of open floor reachable ({walkableCount} cells of {Cell} m).");
            else report.Warn($"{isolated} open floor cells ({isolated * Cell * Cell:F1} m²) are unreachable pockets — players could never stand there.");

            foreach (float lineZ in new[] { -WarehouseMapBuilder.PartitionZ, WarehouseMapBuilder.PartitionZ })
            {
                int row = Index(new Vector3(0f, 0f, lineZ)).z;
                int routes = 0;
                bool inRun = false;
                for (int ix = 0; ix < nx; ix++)
                {
                    bool open = reached[ix, row];
                    if (open && !inRun) routes++;
                    inRun = open;
                }
                if (routes >= 3) report.Pass($"{routes} separate routes cross z = {lineZ}.");
                else report.Error($"Only {routes} route(s) cross z = {lineZ}; the layout needs at least 3.");
            }
        }

        private static void CheckSealed(SetupReport report, Bounds area, int mask)
        {
            Bounds tolerance = area;
            tolerance.Expand(new Vector3(2f, 4f, 2f));
            var probes = new[]
            {
                new Vector3(0f, 1.5f, -20f), new Vector3(0f, 1.5f, 20f), new Vector3(-12f, 1.5f, 0f), new Vector3(12f, 1.5f, 0f),
                new Vector3(16.5f, 4.5f, -12f), new Vector3(-16.5f, 4.5f, 12f), new Vector3(0f, 6f, 0f),
            };

            int leaks = 0;
            foreach (Vector3 probe in probes)
            {
                for (int i = 0; i < 36; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 10f, 0f) * Vector3.forward;
                    if (!RayStaysInside(probe, dir, mask, tolerance)) leaks++;
                }
                if (!RayStaysInside(probe, Vector3.up, mask, tolerance)) leaks++;
                if (!RayStaysInside(probe, Vector3.down, mask, tolerance)) leaks++;
            }

            if (leaks == 0) report.Pass($"Map is sealed ({probes.Length * 38} boundary rays all hit walls, floor or roof).");
            else report.Error($"{leaks} boundary ray(s) escaped the map — there is a hole in the walls/floor/roof.");
        }

        private static bool RayStaysInside(Vector3 origin, Vector3 direction, int mask, Bounds tolerance) =>
            Physics.Raycast(origin, direction, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore)
            && tolerance.Contains(hit.point);

        /// <summary>True if a standing player capsule fits at <paramref name="p"/> with floor (y ≈ 0) beneath it.</summary>
        private static bool IsStandable(Vector3 p, int mask, out Vector3 foot)
        {
            foot = p;
            if (!Physics.Raycast(new Vector3(p.x, 0.6f, p.z), Vector3.down, out RaycastHit hit, 1.2f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y) > 0.1f) return false;

            foot = hit.point;
            Vector3 bottom = foot + Vector3.up * (PlayerRadius + 0.06f);
            Vector3 top = foot + Vector3.up * (PlayerHeight - PlayerRadius);
            return !Physics.CheckCapsule(bottom, top, PlayerRadius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
