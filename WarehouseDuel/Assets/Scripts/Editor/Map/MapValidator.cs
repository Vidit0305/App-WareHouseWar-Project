using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseDuel.Core;
using WarehouseDuel.Environment;

namespace WarehouseDuel.EditorTools.Map
{
    /// <summary>
    /// Automated checks on a map scene (any of <see cref="GameConstants.Scenes.Maps"/>), using real physics queries
    /// against its colliders: exactly <see cref="SpawnPoint.MaxSlots"/> spawns with unique slots, spawn clearance,
    /// floor-level connectivity between all spawns, close-range sight lines between spawns (warning only), the
    /// warehouse's partition routes, and that the map is sealed (no way out; open-sky maps need tall enough walls).
    /// </summary>
    internal static class MapValidator
    {
        private const float PlayerRadius = 0.4f;
        private const float PlayerHeight = 1.8f;
        private const float Cell = 0.5f;
        /// <summary>Spawn pairs closer than this must not see each other (spawn-to-spawn kills); farther pairs are only counted.</summary>
        private const float CloseRange = 22f;

        /// <summary>Opens every map scene in turn and validates it.</summary>
        public static void RunAllMaps(SetupReport report)
        {
            foreach (string map in GameConstants.Scenes.Maps) RunOnScene(map, report);
        }

        /// <summary>Opens <paramref name="sceneName"/> (if it is not already the active scene) and validates it.</summary>
        public static bool RunOnScene(string sceneName, SetupReport report)
        {
            string path = GameConstants.Scenes.PathOf(sceneName);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                report.Error($"{sceneName}: scene {path} does not exist.");
                return false;
            }
            if (SceneManager.GetActiveScene().path != path) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Run(report);
            return true;
        }

        /// <summary>Validates the currently open map scene.</summary>
        public static void Run(SetupReport report)
        {
            Physics.SyncTransforms();
            string map = SceneManager.GetActiveScene().name;

            int mask = LayerMask.GetMask(GameConstants.Layers.Environment);
            if (mask == 0)
            {
                report.Error("Environment layer is missing; run the Phase 1 setup.");
                return;
            }

            MapBounds bounds = Object.FindFirstObjectByType<MapBounds>();
            SpawnPoint[] spawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).OrderBy(s => s.Slot).ToArray();
            if (bounds == null) { report.Error($"{map}: no MapBounds in the scene."); return; }

            bool slotsOk = spawns.Length == SpawnPoint.MaxSlots;
            for (int i = 0; slotsOk && i < spawns.Length; i++) slotsOk = spawns[i].Slot == i;
            if (!slotsOk)
            {
                string found = string.Join(", ", spawns.Select(s => s.Slot));
                report.Error($"{map}: expected exactly {SpawnPoint.MaxSlots} spawn points with unique slots 0–{SpawnPoint.MaxSlots - 1}, found {spawns.Length} ({found}).");
                return;
            }

            CheckSpawns(report, map, spawns, bounds.PlayableArea.center, mask);
            CheckConnectivity(report, map, bounds.PlayableArea, spawns, mask);
            CheckSealed(report, map, bounds, mask);
        }

        private static void CheckSpawns(SetupReport report, string map, SpawnPoint[] spawns, Vector3 centre, int mask)
        {
            int clear = 0;
            foreach (SpawnPoint spawn in spawns)
            {
                if (IsStandable(spawn.Position, mask, out Vector3 foot)) clear++;
                else report.Error($"{map}: spawn {spawn.Slot} at {spawn.Position} is blocked or not on the floor.");

                // A little elbow room: nothing within ~0.8 m of the player's body.
                if (Physics.CheckBox(foot + Vector3.up * 1.0f, new Vector3(0.8f, 0.85f, 0.8f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                    report.Warn($"{map}: spawn {spawn.Slot} has cover/props within 0.8 m.");

                Vector3 toCentre = Vector3.ProjectOnPlane(centre - spawn.Position, Vector3.up).normalized;
                if (Vector3.Dot(spawn.transform.forward, toCentre) < 0.5f) report.Warn($"{map}: spawn {spawn.Slot} is not facing the map centre.");
            }
            if (clear == spawns.Length) report.Pass($"{map}: all {spawns.Length} spawns are clear and on the floor.");

            // Sight lines between spawns at eye height.
            Vector3 eye = Vector3.up * 1.6f;
            int closeVisible = 0, farVisible = 0, pairs = 0;
            for (int i = 0; i < spawns.Length; i++)
            {
                for (int j = i + 1; j < spawns.Length; j++)
                {
                    pairs++;
                    Vector3 a = spawns[i].Position, b = spawns[j].Position;
                    if (Physics.Linecast(a + eye, b + eye, mask, QueryTriggerInteraction.Ignore)) continue;
                    float distance = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    if (distance < CloseRange)
                    {
                        closeVisible++;
                        report.Warn($"{map}: spawns {spawns[i].Slot} and {spawns[j].Slot} can see each other at {distance:F1} m.");
                    }
                    else farVisible++;
                }
            }
            if (closeVisible == 0) report.Pass($"{map}: no spawn sees another spawn within {CloseRange:F0} m.");
            report.Info($"{map}: {farVisible} of {pairs} spawn pairs have a long-range (≥ {CloseRange:F0} m) sight line.");
        }

        private static void CheckConnectivity(SetupReport report, string map, Bounds area, SpawnPoint[] spawns, int mask)
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

            int unreachable = 0;
            for (int i = 1; i < spawns.Length; i++)
            {
                var goal = Index(spawns[i].Position);
                if (reached[goal.x, goal.z]) continue;
                unreachable++;
                report.Error($"{map}: spawn {spawns[i].Slot} cannot be reached from spawn 0 at floor level.");
            }
            if (unreachable == 0) report.Pass($"{map}: every spawn is reachable from spawn 0 on foot.");

            int isolated = walkableCount - reachedCount;
            float coverage = 100f * reachedCount / Mathf.Max(1, walkableCount);
            if (isolated <= 8) report.Pass($"{map}: floor connectivity {coverage:F1}% of open floor reachable ({walkableCount} cells of {Cell} m).");
            else report.Warn($"{map}: {isolated} open floor cells ({isolated * Cell * Cell:F1} m²) are unreachable pockets — players could never stand there.");

            // The warehouse is split by partition walls: each must be crossable in several places.
            if (map != GameConstants.Scenes.WarehouseMap) return;
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
                if (routes >= 3) report.Pass($"{map}: {routes} separate routes cross z = {lineZ}.");
                else report.Error($"{map}: only {routes} route(s) cross z = {lineZ}; the layout needs at least 3.");
            }
        }

        private static void CheckSealed(SetupReport report, string map, MapBounds bounds, int mask)
        {
            Bounds area = bounds.PlayableArea;
            Bounds tolerance = area;
            tolerance.Expand(new Vector3(2f, 4f, 2f));

            // Probes inside the map; open-sky maps also probe above any reachable height (walls must be tall enough).
            Vector3 c = area.center, e = area.extents;
            float[] heights = bounds.OpenSky ? new[] { 1.5f, 4.5f, 7f } : new[] { 1.5f, 4.5f, 6f };
            var probes = new List<Vector3>();
            foreach (float h in heights)
            {
                probes.Add(new Vector3(c.x, h, c.z));
                probes.Add(new Vector3(c.x, h, c.z - e.z * 0.65f));
                probes.Add(new Vector3(c.x, h, c.z + e.z * 0.65f));
                probes.Add(new Vector3(c.x - e.x * 0.65f, h, c.z));
                probes.Add(new Vector3(c.x + e.x * 0.65f, h, c.z));
            }

            int leaks = 0, rays = 0;
            foreach (Vector3 probe in probes)
            {
                for (int i = 0; i < 36; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 10f, 0f) * Vector3.forward;
                    rays++;
                    if (!RayStaysInside(probe, dir, mask, tolerance)) leaks++;
                }
                rays++;
                if (!RayStaysInside(probe, Vector3.down, mask, tolerance)) leaks++;
                if (bounds.OpenSky) continue;
                rays++;
                if (!RayStaysInside(probe, Vector3.up, mask, tolerance)) leaks++;
            }

            string kind = bounds.OpenSky ? "walls/cliffs or ground" : "walls, floor or roof";
            if (leaks == 0) report.Pass($"{map}: sealed ({rays} boundary rays all hit {kind}).");
            else report.Error($"{map}: {leaks} boundary ray(s) escaped the map — there is a hole in the {kind}.");
        }

        private static bool RayStaysInside(Vector3 origin, Vector3 direction, int mask, Bounds tolerance) =>
            Physics.Raycast(origin, direction, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore)
            && tolerance.Contains(hit.point);

        /// <summary>True if a standing player capsule fits at <paramref name="p"/> with floor (y ≈ 0) beneath it.</summary>
        private static bool IsStandable(Vector3 p, int mask, out Vector3 foot)
        {
            foot = new Vector3(p.x, 0f, p.z);
            if (!Physics.Raycast(new Vector3(p.x, 0.6f, p.z), Vector3.down, out RaycastHit hit, 1.2f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y) > 0.1f) return false;

            foot = hit.point;
            Vector3 bottom = foot + Vector3.up * (PlayerRadius + 0.06f);
            Vector3 top = foot + Vector3.up * (PlayerHeight - PlayerRadius);
            return !Physics.CheckCapsule(bottom, top, PlayerRadius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
