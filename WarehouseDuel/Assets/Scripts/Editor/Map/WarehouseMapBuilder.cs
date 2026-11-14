using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WarehouseWar.Core;
using WarehouseWar.Environment;
using Id = WarehouseWar.EditorTools.Map.EnvironmentPalette.Id;

namespace WarehouseWar.EditorTools.Map
{
    /// <summary>
    /// Generates the playable warehouse inside Assets/Scenes/WarehouseMap.unity.
    ///
    /// Layout (top view, metres; interior x ∈ [-18, 18], z ∈ [-28, 28]):
    ///   - Spawn A (slot 0) at (0, -25) facing +Z, Spawn B (slot 1) at (0, 25) facing -Z; slots 2–7 (free-for-all) at
    ///     the side walls (∓16.5, 0) and the four back corners, see <see cref="WarehouseSpawns"/>.
    ///   - Every prop in the southern half is mirrored by a 180° rotation about the centre, so both
    ///     players get exactly the same geometry (point symmetry = fair map).
    ///   - Partition walls at z = ±12 with three doorways each + a passage under the catwalk → 4 routes.
    ///   - Elevated catwalk along one side wall per half; containers, racks, machinery, crates for cover.
    /// Only the "Environment", "Spawn Points" and "Lighting" roots are regenerated; other objects are kept.
    /// </summary>
    internal static class WarehouseMapBuilder
    {
        public const float HalfWidth = 18f;     // interior x extent
        public const float HalfLength = 28f;    // interior z extent
        public const float WallHeight = 9f;
        /// <summary>Low golden-hour sun (also used to angle the skylight sunbeams).</summary>
        public static readonly Quaternion SunRotation = Quaternion.Euler(42f, -32f, 0f);
        public const float PartitionZ = 12f;
        public const float PartitionHeight = 3f;
        public static readonly Vector3 SpawnA = new Vector3(0f, 0f, -25f);

        /// <summary>
        /// All 8 spawns as (x, z, facing yaw); index = slot. Slots 0/1 are <see cref="SpawnA"/> and its mirror (unchanged
        /// from the 1v1 layout); every other pair is point-symmetric too, so any two opposite slots get the same geometry.
        /// </summary>
        private static readonly Vector3[] WarehouseSpawns =
        {
            new Vector3(0f, -25f, 0f), new Vector3(0f, 25f, 180f),
            new Vector3(-16.5f, 0f, 90f), new Vector3(16.5f, 0f, 270f),
            new Vector3(-14f, -25.5f, 35f), new Vector3(14f, 25.5f, 215f),
            new Vector3(12.5f, -25.5f, 325f), new Vector3(-12.5f, 25.5f, 145f),
        };

        private const string EnvironmentRoot = "Environment";
        private const string SpawnRoot = "Spawn Points";
        private const string LightingRoot = "Lighting";

        // Partition wall segments for the southern half (x ranges); mirrored to the north.
        private static readonly Vector2[] PartitionSegments =
        {
            new Vector2(-18f, -13f), new Vector2(-9f, -2.5f), new Vector2(2.5f, 9f), new Vector2(12.5f, 14.5f),
        };

        private readonly struct Placement
        {
            public readonly Piece Piece;
            public readonly Vector3 Position;
            public readonly float RotationY;

            public Placement(Piece piece, float x, float y, float z, float rotationY = 0f)
            {
                Piece = piece;
                Position = new Vector3(x, y, z);
                RotationY = rotationY;
            }
        }

        /// <summary>Southern-half props. Each one is also placed rotated 180° about the map centre.</summary>
        private static readonly Placement[] HalfLayout =
        {
            // Spawn cover: a container lying across the spawn's line of sight.
            new Placement(Piece.ContainerBlue, 0f, 0f, -19f, 90f),
            new Placement(Piece.Crate, -5f, 0f, -23f),
            new Placement(Piece.Crate, -5f, 0f, -24.2f),
            new Placement(Piece.CrateSmall, -6.3f, 0f, -23.6f),
            new Placement(Piece.BarrelRed, 4.6f, 0f, -24.5f),
            new Placement(Piece.BarrelBlue, 5.25f, 0f, -24.2f),
            // Crate stacks along the back wall: block the sight lines from spawn 0 to the corner spawns 4 and 6.
            new Placement(Piece.Crate, -8f, 0f, -25.2f),
            new Placement(Piece.Crate, -8f, 1.2f, -25.2f, 15f),
            new Placement(Piece.Crate, 7.5f, 0f, -25.3f),
            new Placement(Piece.Crate, 7.5f, 1.2f, -25.3f, -12f),

            // Left lane: storage racks (one forms a narrow corridor against the wall).
            new Placement(Piece.StorageRack, -17.2f, 0f, -21f),
            new Placement(Piece.StorageRack, -15f, 0f, -5f),
            new Placement(Piece.MachineSmall, -7f, 0f, -15.5f),
            new Placement(Piece.Crate, -11f, 0f, -15f),

            // Right lane: container + catwalk (ramp starts near the back wall).
            new Placement(Piece.ContainerOrange, 11.5f, 0f, -18f),
            new Placement(Piece.Catwalk, 16.5f, 0f, -26f),
            new Placement(Piece.BarrelRed, 11.5f, 0f, -8.5f),
            new Placement(Piece.BarrelBlue, 12.15f, 0f, -8.8f),

            // Mid field.
            new Placement(Piece.JerseyBarrier, 0f, 0f, -14.5f),
            new Placement(Piece.ContainerRed, -6f, 0f, -5f),
            new Placement(Piece.Crate, 4.6f, 0f, -7f),
            new Placement(Piece.Crate, 5.8f, 0f, -7f),
            new Placement(Piece.CrateSmall, 5.2f, 1.2f, -7f, 20f),
            new Placement(Piece.BarrelRed, -3f, 0f, -9f),
            new Placement(Piece.BarrelRed, -3.65f, 0f, -9.3f),
            new Placement(Piece.BarrelBlue, -3.2f, 0f, -9.95f),
            new Placement(Piece.JerseyBarrier, -11f, 0f, 0f),

            // Structure.
            new Placement(Piece.Pillar, -9f, 0f, -18f),
            new Placement(Piece.Pillar, 9f, 0f, -18f),
            new Placement(Piece.Pillar, -9f, 0f, -6f),
            new Placement(Piece.Pillar, 9f, 0f, -6f),

            // Decorative doors on the outer walls (flush; the wall provides collision).
            new Placement(Piece.RollUpDoor, -9f, 0f, -HalfLength, 0f),
            new Placement(Piece.RollUpDoor, 7f, 0f, -HalfLength, 0f),
            new Placement(Piece.RollUpDoor, -HalfWidth, 0f, -12f + 3.5f, 90f),

            // Lights.
            new Placement(Piece.HangingLamp, -9f, 7.8f, -14f),
            new Placement(Piece.HangingLamp, 9f, 7.8f, -14f),
            new Placement(Piece.HangingLampUnlit, 0f, 7.8f, -22f),
            new Placement(Piece.HangingLampUnlit, -12f, 7.8f, -4f),
        };

        /// <summary>Placed once; these sit on the centre of symmetry.</summary>
        private static readonly Placement[] CentreLayout =
        {
            new Placement(Piece.Machine, 0f, 0f, 0f),
            new Placement(Piece.HangingLamp, 0f, 7.8f, 0f),
        };

        [MenuItem("Tools/Warehouse War/Phase 2 - Build Warehouse Map", priority = 30)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new SetupReport();
            Build(report);
            report.PrintToConsole("Phase 2 warehouse build");
            EditorUtility.DisplayDialog("Warehouse map", report.Summary + "\n\nDetails are in the Console.", "OK");
        }

        [MenuItem("Tools/Warehouse War/Phase 2 - Validate Warehouse Map", priority = 31)]
        public static void ValidateFromMenu()
        {
            var report = new SetupReport();
            if (OpenMapScene(report)) MapValidator.Run(report);
            report.PrintToConsole("Warehouse map validation");
            EditorUtility.DisplayDialog("Warehouse map validation", report.Summary + "\n\nDetails are in the Console.", "OK");
        }

        public static void Build(SetupReport report)
        {
            if (!OpenMapScene(report)) return;
            Scene scene = SceneManager.GetActiveScene();

            EnvironmentPalette.ClearCache();
            Dictionary<Piece, GameObject> kit = EnvironmentKit.BuildAll(report);

            Transform environment = ResetRoot(scene, EnvironmentRoot);
            Transform structure = Child(environment, "Structure");
            Transform props = Child(environment, "Props");
            Transform paint = Child(environment, "Floor Paint");

            BuildShell(structure);
            BuildPartitions(structure);
            BuildFloorPaint(paint);

            int count = 0;
            foreach (Placement p in HalfLayout)
            {
                Place(kit[p.Piece], props, p.Position, p.RotationY);
                Place(kit[p.Piece], props, Mirror(p.Position), p.RotationY + 180f);
                count += 2;
            }
            foreach (Placement p in CentreLayout)
            {
                Place(kit[p.Piece], props, p.Position, p.RotationY);
                count++;
            }

            EnvironmentKit.MakeStaticEnvironment(structure.gameObject, occluder: true);
            EnvironmentKit.MakeStaticEnvironment(paint.gameObject, occluder: false);

            var bounds = environment.gameObject.AddComponent<MapBounds>();
            bounds.Configure(new Bounds(new Vector3(0f, WallHeight / 2f, 0f), new Vector3(HalfWidth * 2f, WallHeight, HalfLength * 2f)), -5f);

            BuildSpawnPoints(scene);
            BuildLighting(scene);
            PositionOverviewCamera(scene);

            report.Pass($"Warehouse built: {count} props, {WarehouseSpawns.Length} spawns, {PartitionSegments.Length * 2} partition walls.");

            MapValidator.Run(report);

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene)) report.Pass($"Saved {scene.path}");
            else report.Error($"Could not save {scene.path}");
            LightingBaker.BakeEnvironment(scene, report);
        }

        private static bool OpenMapScene(SetupReport report)
        {
            string path = GameConstants.Scenes.PathOf(GameConstants.Scenes.WarehouseMap);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                report.Error($"{path} does not exist. Run 'Phase 1 - Run Project Setup' first.");
                return false;
            }
            if (SceneManager.GetActiveScene().path != path) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        // ------------------------------------------------------------------ structure

        private static void BuildShell(Transform parent)
        {
            const float t = 0.5f; // wall thickness
            float w = HalfWidth, l = HalfLength, h = WallHeight;

            EnvironmentKit.Box(parent, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(w * 2f + t * 2f, 0.5f, l * 2f + t * 2f), Id.ConcreteFloor, true);

            EnvironmentKit.Box(parent, "Wall South", new Vector3(0f, h / 2f, -l - t / 2f), new Vector3(w * 2f + t * 2f, h + 1f, t), Id.MetalWall, true);
            EnvironmentKit.Box(parent, "Wall North", new Vector3(0f, h / 2f, l + t / 2f), new Vector3(w * 2f + t * 2f, h + 1f, t), Id.MetalWall, true);
            EnvironmentKit.Box(parent, "Wall West", new Vector3(-w - t / 2f, h / 2f, 0f), new Vector3(t, h + 1f, l * 2f), Id.MetalWall, true);
            EnvironmentKit.Box(parent, "Wall East", new Vector3(w + t / 2f, h / 2f, 0f), new Vector3(t, h + 1f, l * 2f), Id.MetalWall, true);

            // Roof: collides (keeps players in) but casts no shadow, so the sun lights the interior like skylights.
            GameObject roof = EnvironmentKit.Box(parent, "Roof", new Vector3(0f, h + 0.2f, 0f), new Vector3(w * 2f + t * 2f, 0.4f, l * 2f + t * 2f), Id.Roof, true);
            EnvironmentKit.NoShadow(roof);

            // Wall detail: vertical ribs and a concrete kick plate (visual only).
            for (float x = -w + 1.5f; x <= w - 1.4f; x += 3f)
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Rib", new Vector3(x, h / 2f, -l + 0.06f), new Vector3(0.25f, h, 0.12f), Id.MetalWallRib));
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Rib", new Vector3(x, h / 2f, l - 0.06f), new Vector3(0.25f, h, 0.12f), Id.MetalWallRib));
            }
            for (float z = -l + 1f; z <= l - 0.9f; z += 3f)
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Rib", new Vector3(-w + 0.06f, h / 2f, z), new Vector3(0.12f, h, 0.25f), Id.MetalWallRib));
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Rib", new Vector3(w - 0.06f, h / 2f, z), new Vector3(0.12f, h, 0.25f), Id.MetalWallRib));
            }
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "KickPlate", new Vector3(0f, 0.6f, -l + 0.05f), new Vector3(w * 2f, 1.2f, 0.1f), Id.ConcreteDark));
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "KickPlate", new Vector3(0f, 0.6f, l - 0.05f), new Vector3(w * 2f, 1.2f, 0.1f), Id.ConcreteDark));
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "KickPlate", new Vector3(-w + 0.05f, 0.6f, 0f), new Vector3(0.1f, 1.2f, l * 2f), Id.ConcreteDark));
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "KickPlate", new Vector3(w - 0.05f, 0.6f, 0f), new Vector3(0.1f, 1.2f, l * 2f), Id.ConcreteDark));

            // Skylights: glowing daylight strips in the roof (visual only; the roof slab still seals the map).
            foreach (float x in new[] { -10.5f, 0f, 10.5f })
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Skylight", new Vector3(x, h - 0.07f, 0f), new Vector3(3.2f, 0.04f, l * 2f - 6f), Id.Skylight));
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "SkylightFrame", new Vector3(x, h - 0.03f, 0f), new Vector3(3.5f, 0.03f, l * 2f - 5.6f), Id.Steel));
                // Warm sunbeam falling from the skylight (additive sheet, purely visual).
                Vector3 sunDirection = SunRotation * Vector3.forward;
                float beamLength = (h - 0.1f) / Mathf.Max(0.2f, -sunDirection.y);
                AtmosphereKit.LightShaft(parent, "Sunbeam", new Vector3(x, h - 0.1f, 0f), sunDirection, Vector3.forward,
                    beamLength, l * 2f - 8f, new Color(0.11f, 0.08f, 0.05f));
            }

            // Roof trusses: cast striped shadows across the floor.
            for (float z = -l + 2f; z <= l - 1.9f; z += 6f)
            {
                EnvironmentKit.Box(parent, "Truss", new Vector3(0f, h - 0.4f, z), new Vector3(w * 2f, 0.35f, 0.25f), Id.Steel);
            }
            foreach (float x in new[] { -6f, 6f })
            {
                EnvironmentKit.Box(parent, "Purlin", new Vector3(x, h - 0.7f, 0f), new Vector3(0.25f, 0.3f, l * 2f), Id.Steel);
            }
        }

        private static void BuildPartitions(Transform parent)
        {
            foreach (Vector2 segment in PartitionSegments)
            {
                PartitionSegment(parent, segment.x, segment.y, -PartitionZ);
                PartitionSegment(parent, -segment.y, -segment.x, PartitionZ); // 180° mirror
            }
        }

        private static void PartitionSegment(Transform parent, float x0, float x1, float z)
        {
            const float thickness = 0.3f;
            float length = x1 - x0, centre = (x0 + x1) / 2f, h = PartitionHeight;

            EnvironmentKit.Box(parent, "Partition", new Vector3(centre, h / 2f, z), new Vector3(length, h, thickness), Id.MetalWall, true);
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "PartitionCap", new Vector3(centre, h + 0.05f, z), new Vector3(length + 0.02f, 0.1f, thickness + 0.04f), Id.Hazard));
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "PartitionKick", new Vector3(centre, 0.2f, z), new Vector3(length, 0.4f, thickness + 0.06f), Id.ConcreteDark));
            EnvironmentKit.Box(parent, "PartitionPost", new Vector3(x0 + 0.15f, (h + 0.1f) / 2f, z), new Vector3(0.3f, h + 0.1f, 0.42f), Id.Steel);
            EnvironmentKit.Box(parent, "PartitionPost", new Vector3(x1 - 0.15f, (h + 0.1f) / 2f, z), new Vector3(0.3f, h + 0.1f, 0.42f), Id.Steel);
        }

        private static void BuildFloorPaint(Transform parent)
        {
            // Spawn pads (blue = slot 0, red = slot 1) and a hazard square around the centre machine.
            SpawnPad(parent, SpawnA, Id.SpawnBlue);
            SpawnPad(parent, Mirror(SpawnA), Id.SpawnRed);
            for (int slot = 2; slot < WarehouseSpawns.Length; slot++)
            {
                SpawnPad(parent, new Vector3(WarehouseSpawns[slot].x, 0f, WarehouseSpawns[slot].y), Id.FloorPaintWhite);
            }

            const float half = 3.6f, line = 0.15f, y = 0.006f;
            foreach (float s in new[] { -1f, 1f })
            {
                Paint(parent, new Vector3(0f, y, s * half), new Vector3(half * 2f + line, 0.012f, line), Id.FloorPaint);
                Paint(parent, new Vector3(s * half, y, 0f), new Vector3(line, 0.012f, half * 2f + line), Id.FloorPaint);
            }

            // Walkway lines through the side doorways.
            foreach (float x in new[] { -11f, 11f })
            {
                Paint(parent, new Vector3(x - 2f, y, 0f), new Vector3(line, 0.012f, 40f), Id.FloorPaintWhite);
                Paint(parent, new Vector3(x + 2f, y, 0f), new Vector3(line, 0.012f, 40f), Id.FloorPaintWhite);
            }
        }

        private static void SpawnPad(Transform parent, Vector3 centre, Id colour)
        {
            const float size = 3f, line = 0.18f, y = 0.008f;
            foreach (float s in new[] { -1f, 1f })
            {
                Paint(parent, centre + new Vector3(0f, y, s * size / 2f), new Vector3(size + line, 0.014f, line), colour);
                Paint(parent, centre + new Vector3(s * size / 2f, y, 0f), new Vector3(line, 0.014f, size + line), colour);
            }
        }

        private static void Paint(Transform parent, Vector3 centre, Vector3 size, Id colour) =>
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Paint", centre, size, colour));

        // ------------------------------------------------------------------ spawns, lighting, camera

        private static void BuildSpawnPoints(Scene scene)
        {
            Transform root = ResetRoot(scene, SpawnRoot);
            CreateSpawn(root, "Spawn A (slot 0)", SpawnA, 0f, 0);
            CreateSpawn(root, "Spawn B (slot 1)", Mirror(SpawnA), 180f, 1);
            for (int slot = 2; slot < WarehouseSpawns.Length; slot++)
            {
                Vector3 s = WarehouseSpawns[slot];
                CreateSpawn(root, $"Spawn {slot} (slot {slot})", new Vector3(s.x, 0f, s.y), s.z, slot);
            }
        }

        private static void CreateSpawn(Transform parent, string name, Vector3 position, float rotationY, int slot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
            go.AddComponent<SpawnPoint>().Configure(slot);
        }

        private static void BuildLighting(Scene scene)
        {
            Transform root = ResetRoot(scene, LightingRoot);

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = SunRotation;
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.84f, 0.64f);   // golden hour
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;

            // Cheap three-colour ambient instead of baked GI: cool sky light, warm bounce from the floor.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.56f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.42f, 0.34f);

            // Warm dusty haze.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.56f, 0.46f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 110f;

            AtmosphereKit.AddGlobalVolume(AtmosphereKit.Look.Gameplay).transform.SetParent(root, false);
        }

        private static void PositionOverviewCamera(Scene scene)
        {
            GameObject cam = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Overview Camera");
            if (cam == null) return;
            cam.transform.position = new Vector3(-15f, 7.5f, -25.5f);
            cam.transform.LookAt(new Vector3(0f, 0.5f, 0f));
        }

        // ------------------------------------------------------------------ helpers

        public static Vector3 Mirror(Vector3 p) => new Vector3(-p.x, p.y, -p.z);

        private static void Place(GameObject prefab, Transform parent, Vector3 position, float rotationY)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
        }

        private static Transform ResetRoot(Scene scene, string name)
        {
            foreach (GameObject existing in scene.GetRootGameObjects().Where(g => g.name == name))
            {
                Object.DestroyImmediate(existing);
            }
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
