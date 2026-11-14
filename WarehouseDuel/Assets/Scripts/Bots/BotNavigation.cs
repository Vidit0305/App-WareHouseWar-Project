using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using WarehouseWar.Core;
using WarehouseWar.Environment;

namespace WarehouseWar.Bots
{
    /// <summary>
    /// Builds a navigation mesh for the loaded map at runtime (practice only), from the Environment layer's colliders.
    /// Built once per map load; bots query it with NavMesh.CalculatePath / SamplePosition.
    /// </summary>
    public static class BotNavigation
    {
        private static NavMeshDataInstance instance;
        private static NavMeshData data;
        private static int builtForScene = -1;

        /// <summary>True when a navigation mesh exists for the current map.</summary>
        public static bool Ready => instance.valid;

        public static void EnsureBuilt()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (instance.valid && builtForScene == scene.handle) return;
            Clear();

            try
            {
                Bounds bounds = MapBounds.Current != null ? MapBounds.Current.PlayableArea : new Bounds(Vector3.zero, new Vector3(120f, 30f, 120f));
                bounds.Expand(new Vector3(2f, 8f, 2f));

                int mask = LayerMask.GetMask(GameConstants.Layers.Environment);
                if (mask == 0) mask = ~0;
                var sources = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);

                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.45f;
                settings.agentHeight = 1.8f;
                settings.agentClimb = 0.4f;
                settings.agentSlope = 45f;
                settings.overrideVoxelSize = true;
                settings.voxelSize = 0.15f;

                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data != null) instance = NavMesh.AddNavMeshData(data);
                builtForScene = scene.handle;
                if (!instance.valid) Debug.LogWarning("[Bots] Navigation mesh could not be built; bots will steer directly.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bots] Navigation mesh build failed: {e.Message}");
            }
        }

        public static void Clear()
        {
            if (instance.valid) instance.Remove();
            instance = default;
            if (data != null) Object.Destroy(data);
            data = null;
            builtForScene = -1;
        }

        /// <summary>Snaps <paramref name="point"/> onto the navigation mesh (within <paramref name="radius"/>).</summary>
        public static bool Snap(Vector3 point, float radius, out Vector3 result)
        {
            result = point;
            if (!Ready) return false;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas)) return false;
            result = hit.position;
            return true;
        }

        /// <summary>A random reachable-looking point inside the map, or false.</summary>
        public static bool RandomPoint(out Vector3 point)
        {
            point = Vector3.zero;
            MapBounds map = MapBounds.Current;
            Bounds area = map != null ? map.PlayableArea : new Bounds(Vector3.zero, new Vector3(50f, 10f, 50f));
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var candidate = new Vector3(
                    Random.Range(area.min.x + 1f, area.max.x - 1f),
                    area.min.y + 1f,
                    Random.Range(area.min.z + 1f, area.max.z - 1f));
                if (Snap(candidate, 4f, out point)) return true;
            }
            return false;
        }
    }
}
