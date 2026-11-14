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
    /// Generates the three outdoor maps (Desert, Forest, Snow) into Assets/Scenes/{DesertMap,ForestMap,SnowMap}.unity.
    ///
    /// Shared layout (top view, metres): playable square x, z ∈ [-30, 30], ground at y = 0 everywhere, sealed by a
    /// ring of 12 m cliffs (one continuous core wall per side + jagged chunks in front of it, all with colliders).
    ///   - 8 spawns on the edges facing the centre: slots 0/1 at (0, ∓26), 2/3 at (∓26, 0), 4/5 at (∓22, ∓22),
    ///     6/7 at (±22, ∓22). Opposite slots are point-symmetric, so a 1v1 (slots 0 and 1) is fair.
    ///   - Between every two neighbouring spawns sits a tall blocker (container, wall, rock or hut) so neighbours
    ///     cannot see each other; in front of every spawn a cover piece breaks the line to the centre.
    ///   - Like the warehouse, every "half" prop is also placed rotated 180° about the centre (point symmetry).
    ///   - Height: desert terraces with ramps, forest rock plateau with two ramps, snow watchtowers with ramps.
    /// Uses the warehouse kit prefabs (containers, crates, barrels, barriers), so WarehouseMapBuilder must have run
    /// first. Box meshes go to one mesh bank per map, so this never touches meshes used by other scenes.
    /// Only the "Environment", "Spawn Points" and "Lighting" roots are regenerated; other roots (HUD...) are kept.
    /// </summary>
    internal static class OutdoorMapBuilder
    {
        public const float HalfSize = 30f;       // playable x/z extent (inner face of the cliffs)
        public const float CliffHeight = 12f;
        private const float GroundSize = 170f;
        private const float BoundsHeight = 20f;

        private const string EnvironmentRoot = "Environment";
        private const string SpawnRoot = "Spawn Points";
        private const string LightingRoot = "Lighting";
        private const string GameplayRoot = "Gameplay";
        private const string CameraName = "Overview Camera";

        private enum Style { Desert, Forest, Snow }

        /// <summary>Layout piece kinds. Kit kinds use the warehouse prefabs; the rest are built here from boxes.</summary>
        private enum K
        {
            Rock, Wall, Sandbags, Fence, Log, Ramp, Platform,
            Container, Crate, CrateSmall, BarrelRed, BarrelBlue, Jersey,
            Palm, DeadTree, Tree, Pine, Bush,
            Ruin, Cabin, Barracks, Hut, Tower,
        }

        /// <summary>
        /// One layout entry. Position is the piece's ground point; A/B/C are kind-specific sizes:
        /// Rock (width, height, depth) · Wall (length, height, thickness) · Sandbags/Fence (length, height) ·
        /// Log (length, diameter) · Ramp (width, rise, run; rises towards local +Z) · Platform (width, height, depth) ·
        /// Container (colour 0 red, 1 blue, 2 green, 3 orange) · Palm/DeadTree/Pine (height) · Tree (trunk height,
        /// crown size) · Bush (size).
        /// </summary>
        private readonly struct Prop
        {
            public readonly K Kind;
            public readonly Vector3 Position;
            public readonly float RotationY;
            public readonly float A, B, C;

            public Prop(K kind, Vector3 position, float rotationY, float a, float b, float c)
            {
                Kind = kind;
                Position = position;
                RotationY = rotationY;
                A = a;
                B = b;
                C = c;
            }
        }

        /// <summary>Axis-aligned wall segment of a composite building (local metres): from (X0, Z0) to (X1, Z1), Y..Y+H.</summary>
        private readonly struct Seg
        {
            public readonly float X0, Z0, X1, Z1, Y, H, T;

            public Seg(float x0, float z0, float x1, float z1, float y, float h, float t)
            {
                X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; Y = y; H = h; T = t;
            }
        }

        /// <summary>Solid block of a composite (local centre, size, yaw).</summary>
        private readonly struct Block
        {
            public readonly Vector3 Centre, Size;
            public readonly float Yaw;

            public Block(Vector3 centre, Vector3 size, float yaw)
            {
                Centre = centre;
                Size = size;
                Yaw = yaw;
            }
        }

        private readonly struct SpawnSpec
        {
            public readonly float X, Z, RotationY;

            public SpawnSpec(float x, float z, float rotationY)
            {
                X = x;
                Z = z;
                RotationY = rotationY;
            }
        }

        private static Prop P(K kind, float x, float z, float rotationY = 0f, float a = 0f, float b = 0f, float c = 0f) =>
            new Prop(kind, new Vector3(x, 0f, z), rotationY, a, b, c);

        /// <summary>Same as <see cref="P"/> but standing on something <paramref name="y"/> metres high.</summary>
        private static Prop Up(K kind, float x, float y, float z, float rotationY = 0f, float a = 0f, float b = 0f, float c = 0f) =>
            new Prop(kind, new Vector3(x, y, z), rotationY, a, b, c);

        private static Seg W(float x0, float z0, float x1, float z1, float y, float h, float t) => new Seg(x0, z0, x1, z1, y, h, t);
        private static Block B(float x, float y, float z, float sx, float sy, float sz, float yaw) =>
            new Block(new Vector3(x, y, z), new Vector3(sx, sy, sz), yaw);
        private static SpawnSpec Sp(float x, float z, float rotationY) => new SpawnSpec(x, z, rotationY);

        // ------------------------------------------------------------------ spawns (index = slot)

        private static readonly SpawnSpec[] Spawns =
        {
            Sp(0f, -26f, 0f), Sp(0f, 26f, 180f),
            Sp(-26f, 0f, 90f), Sp(26f, 0f, 270f),
            Sp(-22f, -22f, 45f), Sp(22f, 22f, 225f),
            Sp(22f, -22f, 315f), Sp(-22f, 22f, 135f),
        };

        // ------------------------------------------------------------------ desert: ruined adobe town

        private static readonly Prop[] DesertCentre =
        {
            P(K.Ruin, 0f, 0f),
        };

        private static readonly Prop[] DesertHalf =
        {
            // Cover in front of each spawn.
            P(K.Wall, 0f, -19.5f, 0f, 5.5f, 2.6f, 0.7f),
            P(K.Wall, -16.3f, -16.3f, 45f, 4.5f, 2.4f, 0.7f),
            P(K.Container, 16.2f, -16.2f, 45f, 3f),
            P(K.Wall, -19.5f, 0f, 90f, 4.5f, 2.6f, 0.7f),
            // Blockers between neighbouring spawns.
            P(K.Container, 9.4f, -23f, 157.5f, 0f),
            P(K.Wall, -9.6f, -23.2f, 112.5f, 6f, 3f, 0.8f),
            P(K.Rock, 22.5f, -9.5f, 20f, 4.5f, 3.4f, 5f),
            P(K.Rock, -22.5f, -9.5f, -25f, 5f, 3.2f, 4.5f),
            // Terrace with a ramp and a parapet.
            P(K.Platform, -11f, -8.5f, 0f, 6f, 2.4f, 5f),
            P(K.Ramp, -12.5f, -16.4f, 0f, 2.4f, 2.4f, 5.4f),
            Up(K.Wall, -11f, 2.4f, -6.2f, 0f, 6f, 0.9f, 0.4f),
            Up(K.Wall, -13.8f, 2.4f, -8.6f, 90f, 4.6f, 0.9f, 0.4f),
            // Ruined corner on the east side.
            P(K.Wall, 17.5f, -6.3f, 0f, 4f, 3f, 0.6f),
            P(K.Wall, 15.8f, -8.3f, 90f, 3.6f, 3f, 0.6f),
            // Containers, crates, barrels, sandbags.
            P(K.Container, 5f, -14.5f, 90f, 2f),
            P(K.Crate, 13f, -3.6f), P(K.Crate, 13f, -4.85f, 8f), P(K.CrateSmall, 14.1f, -4.2f, 30f),
            P(K.Crate, -3f, -2f, 15f),
            P(K.BarrelRed, -15f, -3.5f), P(K.BarrelBlue, -15.65f, -3.9f), P(K.BarrelRed, -15.2f, -4.3f),
            P(K.Sandbags, 6.5f, -10.5f, -20f, 4f, 1.2f),
            P(K.Sandbags, -3.5f, -8.8f, 0f, 3f, 1.2f),
            // Rocks.
            P(K.Rock, 2.8f, -23.4f, 15f, 1.6f, 1.1f, 1.4f),
            P(K.Rock, -17.5f, -24.5f, -10f, 2.2f, 1.6f, 1.8f),
            P(K.Rock, 8.5f, -5f, 35f, 2.2f, 1.7f, 1.8f),
            // Palms and dead trees.
            P(K.Palm, -5.5f, -13f, 0f, 7f), P(K.Palm, 25.5f, -25.5f, 120f, 6.5f),
            P(K.Palm, -26f, -25f, 200f, 7.5f), P(K.Palm, -24.5f, -14f, 60f, 6f),
            P(K.DeadTree, 17f, -25.8f, 0f, 4.5f), P(K.DeadTree, -12f, -26.3f, 50f, 4f),
        };

        // ------------------------------------------------------------------ forest: clearing with cabins

        private static readonly Prop[] ForestCentre =
        {
            P(K.Platform, 0f, 0f, 0f, 6f, 2f, 6f),
        };

        private static readonly Prop[] ForestHalf =
        {
            // Plateau ramp + boulders on the plateau.
            P(K.Ramp, -1.5f, -7.6f, 0f, 2.4f, 2f, 4.6f),
            Up(K.Rock, 1.8f, 2f, -1.8f, 30f, 1.6f, 1.1f, 1.4f),
            // Cover in front of each spawn.
            P(K.Rock, 0f, -19.5f, 0f, 4.5f, 2.2f, 2f),
            P(K.Rock, -16.3f, -16.3f, 45f, 3.5f, 2.1f, 2f),
            P(K.Wall, 16.2f, -16.2f, -45f, 4.5f, 2.2f, 0.25f),
            P(K.Rock, -19.5f, 0f, 90f, 4.5f, 2.3f, 2.2f),
            // Blockers between neighbouring spawns.
            P(K.Rock, 9.4f, -23f, 157.5f, 3.2f, 3f, 5.5f),
            P(K.Rock, -9.6f, -23.2f, -157.5f, 3.4f, 2.8f, 5.5f),
            P(K.Rock, 22.5f, -9.5f, 70f, 3.6f, 3.2f, 5f),
            P(K.Rock, -22.5f, -9.5f, -20f, 4.5f, 3f, 4f),
            // Cabin and its woodpile.
            P(K.Cabin, -12f, -9.5f, 0f),
            P(K.Crate, -8f, -12.8f, 10f), P(K.CrateSmall, -8.9f, -13.1f, 35f),
            P(K.Log, -16.2f, -12.6f, 90f, 3f, 0.6f), P(K.Log, -16.2f, -13.3f, 90f, 3f, 0.6f),
            // Fallen logs.
            P(K.Log, 6f, -9f, 70f, 5f, 0.8f), P(K.Log, 15f, -3f, 10f, 4.5f, 0.7f), P(K.Log, -4f, -15f, 100f, 4f, 0.8f),
            // Trees (trunks collide, crowns are visual).
            P(K.Tree, -4.5f, -11f, 0f, 3.4f, 3.8f), P(K.Tree, 5f, -21.5f, 30f, 3.2f, 3.4f), P(K.Tree, 13.5f, -10.5f, 10f, 3.6f, 4f),
            P(K.Tree, -15.5f, -21f, 0f, 3.2f, 3.6f), P(K.Tree, 25.5f, -18f, 0f, 3.4f, 3.6f), P(K.Tree, -25.5f, -17.5f, 45f, 3.6f, 4.2f),
            P(K.Tree, 10f, -15.5f, 60f, 3.0f, 3.2f), P(K.Tree, -20f, -5f, 20f, 3.4f, 3.6f),
            P(K.Pine, 19f, -25.5f, 0f, 8f), P(K.Pine, -5.5f, -26.5f, 0f, 7.5f), P(K.Pine, 3.5f, -13f, 0f, 7f),
            P(K.Pine, 24.5f, -4.5f, 0f, 8.5f), P(K.Pine, -26f, -26f, 0f, 9f),
            // Bushes (concealment only).
            P(K.Bush, 2f, -17f, 0f, 1.4f), P(K.Bush, -7f, -18f, 30f, 1.2f), P(K.Bush, 11.5f, -13f, 60f, 1.5f),
            P(K.Bush, -14f, -3f, 10f, 1.3f), P(K.Bush, 19f, -13f, 80f, 1.3f), P(K.Bush, 7.5f, -2.5f, 20f, 1.1f),
        };

        // ------------------------------------------------------------------ snow: military outpost

        private static readonly Prop[] SnowCentre =
        {
            P(K.Barracks, 0f, 0f),
        };

        private static readonly Prop[] SnowHalf =
        {
            // Cover in front of each spawn.
            P(K.Container, 0f, -19.5f, 90f, 1f),
            P(K.Rock, -16.3f, -16.3f, 45f, 3.5f, 2.2f, 2f),
            P(K.Wall, 16.2f, -16.2f, -45f, 4.5f, 2.4f, 0.6f),
            P(K.Wall, -19.5f, 0f, 90f, 4.5f, 2.4f, 0.6f),
            // Blockers between neighbouring spawns.
            P(K.Container, 9.4f, -23f, 157.5f, 2f),
            P(K.Hut, -10f, -23.2f, 22.5f),
            P(K.Rock, 22.5f, -9.5f, 70f, 3.6f, 3.4f, 5f),
            P(K.Rock, -22.5f, -9.5f, -20f, 4.5f, 3.2f, 4f),
            // Outpost: watchtower, guard hut, fences, sandbags, barriers.
            P(K.Tower, 12f, -9f, 90f),
            P(K.Hut, -12.5f, -9f, 90f),
            P(K.Fence, -5.5f, -9.5f, 0f, 5f, 1.5f),
            P(K.Fence, 18.5f, -14f, 90f, 4f, 1.5f),
            P(K.Sandbags, -2f, -12.5f, 0f, 4f, 1.2f),
            P(K.Sandbags, 17f, -4f, 90f, 3.5f, 1.2f),
            P(K.Jersey, 8f, -4f, 90f), P(K.Jersey, -7.5f, -5.5f, 0f),
            P(K.Crate, 5.5f, -13f), P(K.Crate, 6.7f, -13f, 5f), Up(K.CrateSmall, 6.1f, 1.2f, -13f, 20f),
            P(K.BarrelBlue, -6f, -3.5f), P(K.BarrelRed, -6.6f, -3.1f), P(K.BarrelBlue, 13f, -17.5f),
            P(K.Container, -6.5f, -16.5f, 0f, 2f),
            // Icy rocks and snowy pines.
            P(K.Rock, 2.8f, -23.4f, 15f, 1.6f, 1.1f, 1.4f),
            P(K.Rock, -17.5f, -24.5f, -10f, 2.2f, 1.6f, 1.8f),
            P(K.Pine, -26f, -26f, 0f, 9f), P(K.Pine, 25.5f, -25.5f, 0f, 8f), P(K.Pine, 5.5f, -26.5f, 0f, 7.5f),
            P(K.Pine, -15f, -26.5f, 0f, 7f), P(K.Pine, -25.5f, -14f, 0f, 8.5f), P(K.Pine, 25.5f, -14.5f, 0f, 8f),
            P(K.Pine, -4f, -19.5f, 0f, 6.5f),
        };

        // ------------------------------------------------------------------ composite buildings (local metres)

        /// <summary>Desert centre: 10 × 8 m roofless adobe ruin, a doorway in each wall, two windows, two collapsed walls.</summary>
        private static readonly Seg[] RuinWalls =
        {
            W(-5.3f, -4f, -1.1f, -4f, 0f, 3.2f, 0.6f),
            W(1.1f, -4f, 2.4f, -4f, 0f, 3.2f, 0.6f),
            W(2.4f, -4f, 3.8f, -4f, 0f, 1f, 0.6f),
            W(2.4f, -4f, 3.8f, -4f, 2.2f, 1f, 0.6f),
            W(3.8f, -4f, 5.3f, -4f, 0f, 3.2f, 0.6f),
            W(-5.3f, 4f, -3.8f, 4f, 0f, 3.2f, 0.6f),
            W(-3.8f, 4f, -2.4f, 4f, 0f, 1f, 0.6f),
            W(-3.8f, 4f, -2.4f, 4f, 2.2f, 1f, 0.6f),
            W(-2.4f, 4f, -1.1f, 4f, 0f, 3.2f, 0.6f),
            W(1.1f, 4f, 5.3f, 4f, 0f, 3.2f, 0.6f),
            W(-5f, -3.7f, -5f, -1.1f, 0f, 2.3f, 0.6f),
            W(-5f, 1.1f, -5f, 3.7f, 0f, 3.2f, 0.6f),
            W(5f, -3.7f, 5f, -1.1f, 0f, 3.2f, 0.6f),
            W(5f, 1.1f, 5f, 3.7f, 0f, 2.3f, 0.6f),
        };

        private static readonly Block[] RuinBlocks =
        {
            B(-5.15f, 1.8f, -4.15f, 0.9f, 3.6f, 0.9f, 0f), B(5.15f, 1.8f, -4.15f, 0.9f, 3.6f, 0.9f, 0f),
            B(-5.15f, 1.8f, 4.15f, 0.9f, 3.6f, 0.9f, 0f), B(5.15f, 1.8f, 4.15f, 0.9f, 3.6f, 0.9f, 0f),
            B(-6.2f, 0.25f, 2.4f, 1.2f, 0.5f, 0.9f, 20f), B(6.2f, 0.25f, -2.4f, 1.2f, 0.5f, 0.9f, 200f),
        };

        /// <summary>Forest: 7 × 5 m log cabin, doors on both long sides, a window in each end.</summary>
        private static readonly Seg[] CabinWalls =
        {
            W(-3.5f, -2.5f, -0.7f, -2.5f, 0f, 2.8f, 0.3f),
            W(0.7f, -2.5f, 3.5f, -2.5f, 0f, 2.8f, 0.3f),
            W(-0.7f, -2.5f, 0.7f, -2.5f, 2.2f, 0.6f, 0.3f),
            W(-3.5f, 2.5f, 1.5f, 2.5f, 0f, 2.8f, 0.3f),
            W(2.9f, 2.5f, 3.5f, 2.5f, 0f, 2.8f, 0.3f),
            W(1.5f, 2.5f, 2.9f, 2.5f, 2.2f, 0.6f, 0.3f),
            W(-3.5f, -2.35f, -3.5f, -0.7f, 0f, 2.8f, 0.3f),
            W(-3.5f, 0.7f, -3.5f, 2.35f, 0f, 2.8f, 0.3f),
            W(-3.5f, -0.7f, -3.5f, 0.7f, 0f, 1f, 0.3f),
            W(-3.5f, -0.7f, -3.5f, 0.7f, 1.9f, 0.9f, 0.3f),
            W(3.5f, -2.35f, 3.5f, -0.7f, 0f, 2.8f, 0.3f),
            W(3.5f, 0.7f, 3.5f, 2.35f, 0f, 2.8f, 0.3f),
            W(3.5f, -0.7f, 3.5f, 0.7f, 0f, 1f, 0.3f),
            W(3.5f, -0.7f, 3.5f, 0.7f, 1.9f, 0.9f, 0.3f),
        };

        /// <summary>Snow centre: 9 × 4.5 m barracks, doors at both ends, two windows per long side.</summary>
        private static readonly Seg[] BarracksWalls =
        {
            W(-4.5f, -2.25f, -4.5f, -0.75f, 0f, 3f, 0.3f),
            W(-4.5f, 0.75f, -4.5f, 2.25f, 0f, 3f, 0.3f),
            W(-4.5f, -0.75f, -4.5f, 0.75f, 2.2f, 0.8f, 0.3f),
            W(4.5f, -2.25f, 4.5f, -0.75f, 0f, 3f, 0.3f),
            W(4.5f, 0.75f, 4.5f, 2.25f, 0f, 3f, 0.3f),
            W(4.5f, -0.75f, 4.5f, 0.75f, 2.2f, 0.8f, 0.3f),
            W(-4.35f, -2.25f, -2.85f, -2.25f, 0f, 3f, 0.3f),
            W(-2.85f, -2.25f, -1.65f, -2.25f, 0f, 1.1f, 0.3f),
            W(-2.85f, -2.25f, -1.65f, -2.25f, 1.9f, 1.1f, 0.3f),
            W(-1.65f, -2.25f, 1.65f, -2.25f, 0f, 3f, 0.3f),
            W(1.65f, -2.25f, 2.85f, -2.25f, 0f, 1.1f, 0.3f),
            W(1.65f, -2.25f, 2.85f, -2.25f, 1.9f, 1.1f, 0.3f),
            W(2.85f, -2.25f, 4.35f, -2.25f, 0f, 3f, 0.3f),
            W(-4.35f, 2.25f, -2.85f, 2.25f, 0f, 3f, 0.3f),
            W(-2.85f, 2.25f, -1.65f, 2.25f, 0f, 1.1f, 0.3f),
            W(-2.85f, 2.25f, -1.65f, 2.25f, 1.9f, 1.1f, 0.3f),
            W(-1.65f, 2.25f, 1.65f, 2.25f, 0f, 3f, 0.3f),
            W(1.65f, 2.25f, 2.85f, 2.25f, 0f, 1.1f, 0.3f),
            W(1.65f, 2.25f, 2.85f, 2.25f, 1.9f, 1.1f, 0.3f),
            W(2.85f, 2.25f, 4.35f, 2.25f, 0f, 3f, 0.3f),
        };

        /// <summary>Snow: 4.5 × 3.5 m guard hut, doors on both long sides, a window in each end.</summary>
        private static readonly Seg[] HutWalls =
        {
            W(-2.25f, -1.75f, -0.6f, -1.75f, 0f, 2.7f, 0.25f),
            W(0.6f, -1.75f, 2.25f, -1.75f, 0f, 2.7f, 0.25f),
            W(-0.6f, -1.75f, 0.6f, -1.75f, 2.2f, 0.5f, 0.25f),
            W(-2.25f, 1.75f, 0.5f, 1.75f, 0f, 2.7f, 0.25f),
            W(1.7f, 1.75f, 2.25f, 1.75f, 0f, 2.7f, 0.25f),
            W(0.5f, 1.75f, 1.7f, 1.75f, 2.2f, 0.5f, 0.25f),
            W(-2.25f, -1.625f, -2.25f, -0.5f, 0f, 2.7f, 0.25f),
            W(-2.25f, 0.5f, -2.25f, 1.625f, 0f, 2.7f, 0.25f),
            W(-2.25f, -0.5f, -2.25f, 0.5f, 0f, 1.1f, 0.25f),
            W(-2.25f, -0.5f, -2.25f, 0.5f, 1.9f, 0.8f, 0.25f),
            W(2.25f, -1.625f, 2.25f, -0.5f, 0f, 2.7f, 0.25f),
            W(2.25f, 0.5f, 2.25f, 1.625f, 0f, 2.7f, 0.25f),
            W(2.25f, -0.5f, 2.25f, 0.5f, 0f, 1.1f, 0.25f),
            W(2.25f, -0.5f, 2.25f, 0.5f, 1.9f, 0.8f, 0.25f),
        };

        /// <summary>Snow: 4 × 4 m watchtower deck at 3 m on posts (walkable underneath), parapet, ramp from local -Z.</summary>
        private static readonly Block[] TowerBlocks =
        {
            B(-1.8f, 1.4f, -1.8f, 0.3f, 2.8f, 0.3f, 0f), B(1.8f, 1.4f, -1.8f, 0.3f, 2.8f, 0.3f, 0f),
            B(-1.8f, 1.4f, 1.8f, 0.3f, 2.8f, 0.3f, 0f), B(1.8f, 1.4f, 1.8f, 0.3f, 2.8f, 0.3f, 0f),
            B(0f, 2.9f, 0f, 4.2f, 0.2f, 4.2f, 0f),
        };

        private static readonly Seg[] TowerWalls =
        {
            W(-2.1f, 2.05f, 2.1f, 2.05f, 3f, 1f, 0.15f),
            W(-2.05f, -1.98f, -2.05f, 1.98f, 3f, 1f, 0.15f),
            W(2.05f, -1.98f, 2.05f, 1.98f, 3f, 1f, 0.15f),
            W(-2.1f, -2.05f, -0.9f, -2.05f, 3f, 1f, 0.15f),
            W(0.9f, -2.05f, 2.1f, -2.05f, 3f, 1f, 0.15f),
        };

        private const float TowerDeck = 3f, TowerRampRun = 6.4f, TowerRampWidth = 1.6f;

        // ------------------------------------------------------------------ themes

        private sealed class Theme
        {
            public Style Style;
            public string Scene;
            public int Seed;
            public Id Ground, GroundPatch, Cliff, CliffAccent, Rock, RockAccent, Wall, WallTrim, Platform, Ramp, DeadWood;
            public bool Snowy;
            public Prop[] Half, Centre;
            public Quaternion SunRotation;
            public Color SunColor;
            public float SunIntensity, ShadowStrength;
            public Color AmbientSky, AmbientEquator, AmbientGround, FogColor;
            public float FogStart, FogEnd;
            public Color SkyTint, SkyGround;
            public float SkyThickness, SkyExposure, SunSize;
            public Vector3 OverviewPosition, OverviewTarget;
        }

        private static Theme[] Themes() => new[]
        {
            new Theme
            {
                Style = Style.Desert, Scene = GameConstants.Scenes.DesertMap, Seed = 101,
                Ground = Id.Sand, GroundPatch = Id.SandDark, Cliff = Id.DesertRock, CliffAccent = Id.Sandstone,
                Rock = Id.DesertRock, RockAccent = Id.Sandstone, Wall = Id.Adobe, WallTrim = Id.Sandstone,
                Platform = Id.Sandstone, Ramp = Id.Sandstone, DeadWood = Id.WoodDark,
                Half = DesertHalf, Centre = DesertCentre,
                SunRotation = Quaternion.Euler(27f, -38f, 0f), SunColor = new Color(1f, 0.8f, 0.58f), SunIntensity = 1.65f, ShadowStrength = 0.8f,
                AmbientSky = new Color(0.74f, 0.68f, 0.62f), AmbientEquator = new Color(0.74f, 0.6f, 0.46f), AmbientGround = new Color(0.58f, 0.44f, 0.31f),
                FogColor = new Color(0.86f, 0.68f, 0.48f), FogStart = 22f, FogEnd = 135f,
                SkyTint = new Color(0.86f, 0.62f, 0.42f), SkyGround = new Color(0.62f, 0.47f, 0.33f), SkyThickness = 1.7f, SkyExposure = 1.25f, SunSize = 0.055f,
                OverviewPosition = new Vector3(-27f, 17f, -27f), OverviewTarget = new Vector3(2f, 0f, 2f),
            },
            new Theme
            {
                Style = Style.Forest, Scene = GameConstants.Scenes.ForestMap, Seed = 202,
                Ground = Id.Grass, GroundPatch = Id.Dirt, Cliff = Id.Rock, CliffAccent = Id.RockMoss,
                Rock = Id.Rock, RockAccent = Id.RockMoss, Wall = Id.Plank, WallTrim = Id.WoodDark,
                Platform = Id.Rock, Ramp = Id.Dirt, DeadWood = Id.Bark,
                Half = ForestHalf, Centre = ForestCentre,
                SunRotation = Quaternion.Euler(46f, 32f, 0f), SunColor = new Color(0.94f, 1f, 0.88f), SunIntensity = 1.3f, ShadowStrength = 0.75f,
                AmbientSky = new Color(0.56f, 0.68f, 0.64f), AmbientEquator = new Color(0.44f, 0.53f, 0.42f), AmbientGround = new Color(0.27f, 0.31f, 0.21f),
                FogColor = new Color(0.62f, 0.72f, 0.64f), FogStart = 14f, FogEnd = 100f,
                SkyTint = new Color(0.46f, 0.62f, 0.56f), SkyGround = new Color(0.33f, 0.4f, 0.3f), SkyThickness = 0.9f, SkyExposure = 1.1f, SunSize = 0.04f,
                OverviewPosition = new Vector3(26f, 18f, -27f), OverviewTarget = new Vector3(-2f, 0f, 2f),
            },
            new Theme
            {
                Style = Style.Snow, Scene = GameConstants.Scenes.SnowMap, Seed = 303, Snowy = true,
                Ground = Id.Snow, GroundPatch = Id.Ice, Cliff = Id.IcyRock, CliffAccent = Id.Rock,
                Rock = Id.IcyRock, RockAccent = Id.Ice, Wall = Id.Concrete, WallTrim = Id.ConcreteDark,
                Platform = Id.Concrete, Ramp = Id.Plank, DeadWood = Id.Bark,
                Half = SnowHalf, Centre = SnowCentre,
                SunRotation = Quaternion.Euler(24f, 148f, 0f), SunColor = new Color(0.82f, 0.9f, 1f), SunIntensity = 1.2f, ShadowStrength = 0.7f,
                AmbientSky = new Color(0.7f, 0.79f, 0.94f), AmbientEquator = new Color(0.64f, 0.71f, 0.82f), AmbientGround = new Color(0.74f, 0.79f, 0.87f),
                FogColor = new Color(0.78f, 0.84f, 0.9f), FogStart = 10f, FogEnd = 90f,
                SkyTint = new Color(0.6f, 0.7f, 0.86f), SkyGround = new Color(0.72f, 0.76f, 0.82f), SkyThickness = 1.25f, SkyExposure = 0.95f, SunSize = 0.035f,
                OverviewPosition = new Vector3(27f, 17f, 27f), OverviewTarget = new Vector3(-2f, 0f, -2f),
            },
        };

        // ------------------------------------------------------------------ entry points

        [MenuItem("Tools/Warehouse War/Phase 2 - Build Outdoor Maps", priority = 32)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new SetupReport();
            Build(report);
            report.PrintToConsole("Outdoor maps build");
            EditorUtility.DisplayDialog("Outdoor maps", report.Summary + "\n\nDetails are in the Console.", "OK");
        }

        [MenuItem("Tools/Warehouse War/Phase 2 - Validate All Maps", priority = 33)]
        public static void ValidateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new SetupReport();
            MapValidator.RunAllMaps(report);
            report.PrintToConsole("Map validation");
            EditorUtility.DisplayDialog("Map validation", report.Summary + "\n\nDetails are in the Console.", "OK");
        }

        /// <summary>Builds, validates, saves and bakes the Desert, Forest and Snow scenes (call after WarehouseMapBuilder.Build).</summary>
        public static void Build(SetupReport report)
        {
            Dictionary<Piece, GameObject> kit = LoadKit(report);
            if (kit == null) return;
            try
            {
                foreach (Theme theme in Themes()) BuildMap(theme, kit, report);
            }
            finally
            {
                BoxMeshes.UseBank(null); // back to the warehouse/menu bank
            }
        }

        /// <summary>
        /// Creates any missing outdoor map scene as an empty skeleton (roots + overview camera) without building it,
        /// so steps that need the scene files (build settings) can run first. Leaves the last created scene open.
        /// </summary>
        public static void EnsureScenes(SetupReport report)
        {
            foreach (string sceneName in new[] { GameConstants.Scenes.DesertMap, GameConstants.Scenes.ForestMap, GameConstants.Scenes.SnowMap })
            {
                string path = GameConstants.Scenes.PathOf(sceneName);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) continue;
                Scene scene = OpenOrCreateScene(path);
                foreach (string root in new[] { EnvironmentRoot, SpawnRoot, GameplayRoot, LightingRoot }) EnsureRoot(scene, root);
                EnsureOverviewCamera(scene);
                if (EditorSceneManager.SaveScene(scene)) report.Pass($"Created scene {path}");
                else report.Error($"Could not save {path}");
            }
        }

        private static Dictionary<Piece, GameObject> LoadKit(SetupReport report)
        {
            var kit = new Dictionary<Piece, GameObject>();
            foreach (Piece piece in new[] { Piece.ContainerRed, Piece.ContainerBlue, Piece.ContainerGreen, Piece.ContainerOrange,
                                            Piece.Crate, Piece.CrateSmall, Piece.BarrelRed, Piece.BarrelBlue, Piece.JerseyBarrier })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentKit.PrefabFolder}/{piece}.prefab");
                if (prefab == null)
                {
                    report.Error($"Outdoor maps need the environment kit ({EnvironmentKit.PrefabFolder}/{piece}.prefab is missing). Build the warehouse map first.");
                    return null;
                }
                kit[piece] = prefab;
            }
            return kit;
        }

        // ------------------------------------------------------------------ one map

        private sealed class Groups
        {
            public Transform Ground, Boundary, Structures, Trees, Foliage, Kit, Backdrop;
        }

        private static void BuildMap(Theme t, Dictionary<Piece, GameObject> kit, SetupReport report)
        {
            string path = GameConstants.Scenes.PathOf(t.Scene);
            Scene scene = OpenOrCreateScene(path);
            foreach (string name in new[] { EnvironmentRoot, SpawnRoot, LightingRoot }) DestroyRoots(scene, name);
            BoxMeshes.ResetBank($"Assets/Art/Meshes/OutdoorBoxes_{t.Style}.asset"); // only this map's meshes

            Transform environment = NewRoot(scene, EnvironmentRoot);
            var g = new Groups
            {
                Ground = Child(environment, "Ground"),
                Boundary = Child(environment, "Boundary"),
                Structures = Child(environment, "Structures"),
                Trees = Child(environment, "Trees"),
                Foliage = Child(environment, "Foliage"),
                Kit = Child(environment, "Kit Props"),
                Backdrop = Child(environment, "Backdrop"),
            };
            EnsureRoot(scene, GameplayRoot);

            BuildGround(t, g.Ground);
            BuildBoundary(t, g.Boundary, g.Backdrop);
            BuildBackdrop(t, g.Backdrop);

            int count = 0;
            foreach (Prop p in t.Half)
            {
                Place(p, t, g, kit);
                Place(Mirror(p), t, g, kit);
                count += 2;
            }
            foreach (Prop p in t.Centre)
            {
                Place(p, t, g, kit);
                count++;
            }

            EnvironmentKit.MakeStaticEnvironment(g.Ground.gameObject, occluder: false);
            EnvironmentKit.MakeStaticEnvironment(g.Boundary.gameObject, occluder: true);
            EnvironmentKit.MakeStaticEnvironment(g.Structures.gameObject, occluder: true);
            EnvironmentKit.MakeStaticEnvironment(g.Trees.gameObject, occluder: false);
            EnvironmentKit.MakeStaticEnvironment(g.Foliage.gameObject, occluder: false);
            EnvironmentKit.MakeStaticEnvironment(g.Backdrop.gameObject, occluder: false);

            var bounds = environment.gameObject.AddComponent<MapBounds>();
            bounds.Configure(new Bounds(new Vector3(0f, BoundsHeight / 2f, 0f), new Vector3(HalfSize * 2f, BoundsHeight, HalfSize * 2f)), -5f, isOpenSky: true);

            BuildSpawnPoints(scene);
            BuildLighting(scene, t);
            Camera overview = EnsureOverviewCamera(scene);
            overview.transform.position = t.OverviewPosition;
            overview.transform.LookAt(t.OverviewTarget);

            int renderers = environment.GetComponentsInChildren<MeshRenderer>(true).Length;
            int colliders = environment.GetComponentsInChildren<Collider>(true).Length;
            report.Pass($"{t.Scene} built: {count} layout pieces, {renderers} renderers, {colliders} colliders, {Spawns.Length} spawns.");

            MapValidator.Run(report);

            AssetDatabase.SaveAssets(); // persist the mesh bank before the scene references it
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene)) report.Pass($"Saved {scene.path}");
            else report.Error($"Could not save {scene.path}");
            LightingBaker.BakeEnvironment(scene, report);
        }

        private static Scene OpenOrCreateScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                return SceneManager.GetActiveScene().path == path ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            FolderStructure.EnsureFolder(GameConstants.Scenes.Folder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        // ------------------------------------------------------------------ placement

        private static Prop Mirror(Prop p) =>
            new Prop(p.Kind, new Vector3(-p.Position.x, p.Position.y, -p.Position.z), p.RotationY + 180f, p.A, p.B, p.C);

        private static void Place(Prop p, Theme t, Groups g, Dictionary<Piece, GameObject> kit)
        {
            Quaternion rotation = Quaternion.Euler(0f, p.RotationY, 0f);
            if (TryKitPiece(p, out Piece piece, out Vector3 snowSize))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(kit[piece], g.Kit);
                instance.transform.SetPositionAndRotation(p.Position, rotation);
                if (t.Snowy && snowSize != Vector3.zero)
                {
                    // Snow dusting on top (separate static box, the prefab stays untouched).
                    EnvironmentKit.NoShadow(EnvironmentKit.Box(g.Structures, "Snow", p.Position + Vector3.up * (snowSize.y + 0.04f),
                        new Vector3(snowSize.x, 0.08f, snowSize.z), Id.Snow, false, rotation));
                }
                return;
            }

            Transform parent = p.Kind switch
            {
                K.Palm => g.Trees,
                K.DeadTree => g.Trees,
                K.Tree => g.Trees,
                K.Pine => g.Trees,
                K.Bush => g.Foliage,
                _ => g.Structures,
            };
            var root = new GameObject(p.Kind.ToString()).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(p.Position, rotation);

            switch (p.Kind)
            {
                case K.Rock: Rock(root, t, p.A, p.B, p.C); break;
                case K.Wall: Wall(root, t, p.A, p.B, p.C); break;
                case K.Sandbags: Sandbags(root, t, p.A, p.B); break;
                case K.Fence: Fence(root, t, p.A, p.B); break;
                case K.Log: Log(root, t, p.A, p.B); break;
                case K.Ramp: Ramp(root, t, p.A, p.B, p.C); break;
                case K.Platform: Platform(root, t, p.A, p.B, p.C); break;
                case K.Palm: Palm(root, p.A); break;
                case K.DeadTree: DeadTree(root, t, p.A); break;
                case K.Tree: Tree(root, p.A, p.B); break;
                case K.Pine: Pine(root, t, p.A, collider: true); break;
                case K.Bush: Bush(root, p.A); break;
                case K.Ruin: Ruin(root, t); break;
                case K.Cabin: Cabin(root, t); break;
                case K.Barracks: Barracks(root, t); break;
                case K.Hut: Hut(root, t); break;
                case K.Tower: Tower(root, t); break;
            }
        }

        /// <summary>Maps kit kinds to warehouse prefabs; <paramref name="snowSize"/> = footprint + height for snow dusting.</summary>
        private static bool TryKitPiece(Prop p, out Piece piece, out Vector3 snowSize)
        {
            snowSize = Vector3.zero;
            switch (p.Kind)
            {
                case K.Container:
                    piece = Mathf.RoundToInt(p.A) switch { 1 => Piece.ContainerBlue, 2 => Piece.ContainerGreen, 3 => Piece.ContainerOrange, _ => Piece.ContainerRed };
                    snowSize = new Vector3(EnvironmentKit.ContainerWidth - 0.1f, EnvironmentKit.ContainerHeight, EnvironmentKit.ContainerLength - 0.15f);
                    return true;
                case K.Crate: piece = Piece.Crate; snowSize = new Vector3(1.1f, 1.2f, 1.1f); return true;
                case K.CrateSmall: piece = Piece.CrateSmall; snowSize = new Vector3(0.72f, 0.8f, 0.72f); return true;
                case K.BarrelRed: piece = Piece.BarrelRed; return true;
                case K.BarrelBlue: piece = Piece.BarrelBlue; return true;
                case K.Jersey: piece = Piece.JerseyBarrier; snowSize = new Vector3(2.3f, 1f, 0.32f); return true;
                default: piece = default; return false;
            }
        }

        // ------------------------------------------------------------------ ground, boundary, backdrop

        private static void BuildGround(Theme t, Transform parent)
        {
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(GroundSize, 0.5f, GroundSize), t.Ground, true));

            // Flat patches of a second surface break up the tiling (visual only, a few mm high).
            for (int i = 0; i < 28; i++)
            {
                float x = Mathf.Lerp(-27f, 27f, R(t.Seed, i * 5));
                float z = Mathf.Lerp(-27f, 27f, R(t.Seed, i * 5 + 1));
                float w = 2f + R(t.Seed, i * 5 + 2) * 6f, l = 2f + R(t.Seed, i * 5 + 3) * 7f;
                float yaw = R(t.Seed, i * 5 + 4) * 180f;
                float y = 0.002f + i * 0.0012f;
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Patch", new Vector3(x, y, z), new Vector3(w, 0.004f, l), t.GroundPatch, false,
                    Quaternion.Euler(0f, yaw, 0f)));
            }
        }

        /// <summary>
        /// Cliffs around the square: a continuous 3 m thick core wall per side (seals the map) plus 12 jagged chunks per
        /// side in front of it (protrude 0.2–1.4 m inwards, 6.5–11.5 m tall), corner boulders and themed caps.
        /// </summary>
        private static void BuildBoundary(Theme t, Transform parent, Transform backdrop)
        {
            float l = HalfSize, h = CliffHeight;
            EnvironmentKit.Box(parent, "Cliff Core South", new Vector3(0f, h / 2f, -(l + 1.5f)), new Vector3(l * 2f + 6f, h, 3f), t.Cliff, true);
            EnvironmentKit.Box(parent, "Cliff Core North", new Vector3(0f, h / 2f, l + 1.5f), new Vector3(l * 2f + 6f, h, 3f), t.Cliff, true);
            EnvironmentKit.Box(parent, "Cliff Core West", new Vector3(-(l + 1.5f), h / 2f, 0f), new Vector3(3f, h, l * 2f + 6f), t.Cliff, true);
            EnvironmentKit.Box(parent, "Cliff Core East", new Vector3(l + 1.5f, h / 2f, 0f), new Vector3(3f, h, l * 2f + 6f), t.Cliff, true);

            Vector2[] normals = { new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 n = normals[side];
                var tangent = new Vector2(-n.y, n.x);
                bool alongX = Mathf.Abs(n.y) > 0.5f;
                for (int i = 0; i < 12; i++)
                {
                    int k = side * 100 + i * 10;
                    float u = -l + 2.5f + i * 5f + (R(t.Seed, k) - 0.5f) * 0.8f;
                    float width = 5.6f + R(t.Seed, k + 1) * 1.6f;
                    float depth = 3f + R(t.Seed, k + 2) * 2f;
                    float protrude = 0.2f + R(t.Seed, k + 3) * 1.2f;
                    float height = 6.5f + R(t.Seed, k + 4) * 5f;
                    float yaw = (R(t.Seed, k + 5) - 0.5f) * 10f;
                    Vector2 c2 = n * (l - protrude + depth / 2f) + tangent * u;
                    var size = alongX ? new Vector3(width, height, depth) : new Vector3(depth, height, width);
                    var centre = new Vector3(c2.x, height / 2f, c2.y);
                    Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                    EnvironmentKit.Box(parent, "Cliff", centre, size, R(t.Seed, k + 6) > 0.6f ? t.CliffAccent : t.Cliff, true, rotation);
                    CliffCap(t, parent, new Vector3(c2.x, height, c2.y), size, rotation);

                    if (R(t.Seed, k + 7) > 0.45f)
                    {
                        // Second, smaller rock on top for a broken skyline (out of reach: no collider).
                        float topHeight = 1.5f + R(t.Seed, k + 8) * 2.5f;
                        Vector2 shift = tangent * ((R(t.Seed, k + 9) - 0.5f) * width * 0.4f) + n * 0.6f;
                        var topSize = new Vector3(size.x * 0.6f, topHeight, size.z * 0.6f);
                        var topCentre = new Vector3(c2.x + shift.x, height + topHeight / 2f - 0.3f, c2.y + shift.y);
                        Quaternion topRotation = Quaternion.Euler(0f, yaw + (R(t.Seed, k + 9) - 0.5f) * 30f, 0f);
                        EnvironmentKit.Box(parent, "Cliff Top", topCentre, topSize, t.Cliff, false, topRotation);
                        CliffCap(t, parent, topCentre + Vector3.up * (topHeight / 2f), topSize, topRotation);
                    }

                    if (t.Style == Style.Forest && i % 2 == 0)
                    {
                        // Pines along the cliff top: the forest continues beyond the map.
                        var pine = new GameObject("Cliff Pine").transform;
                        pine.SetParent(backdrop, false);
                        pine.SetPositionAndRotation(new Vector3(c2.x + n.x * 2.5f, height, c2.y + n.y * 2.5f), Quaternion.Euler(0f, R(t.Seed, k + 8) * 90f, 0f));
                        Pine(pine, t, 7f + R(t.Seed, k + 9) * 4f, collider: false);
                    }
                }
            }

            // Corner boulders hide where the core walls meet.
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var centre = new Vector3(sx * (l - 0.5f), 4.5f, sz * (l - 0.5f));
                    EnvironmentKit.Box(parent, "Corner Boulder", centre, new Vector3(5f, 9f, 5f), t.Cliff, true, Quaternion.Euler(0f, 45f, 0f));
                    CliffCap(t, parent, centre + Vector3.up * 4.5f, new Vector3(5f, 0f, 5f), Quaternion.Euler(0f, 45f, 0f));
                }
            }
        }

        private static void CliffCap(Theme t, Transform parent, Vector3 top, Vector3 size, Quaternion rotation)
        {
            if (t.Snowy)
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Snow Cap", top + Vector3.up * 0.1f, new Vector3(size.x * 0.94f, 0.3f, size.z * 0.94f), Id.Snow, false, rotation));
            else if (t.Style == Style.Forest)
                EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, "Grass Cap", top + Vector3.up * 0.08f, new Vector3(size.x * 0.96f, 0.22f, size.z * 0.96f), Id.Grass, false, rotation));
        }

        /// <summary>Distant scenery outside the cliffs (no colliders, no shadows; fog fades it).</summary>
        private static void BuildBackdrop(Theme t, Transform parent)
        {
            int s = t.Seed + 7;
            switch (t.Style)
            {
                case Style.Desert:
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = (i * 36f + R(s, i) * 20f) * Mathf.Deg2Rad, radius = 50f + R(s, i + 20) * 20f;
                        float height = 10f + R(s, i + 40) * 16f;
                        var size = new Vector3(14f + R(s, i + 60) * 16f, height, 12f + R(s, i + 80) * 12f);
                        var centre = new Vector3(Mathf.Cos(angle) * radius, height / 2f - 1f, Mathf.Sin(angle) * radius);
                        Quaternion rotation = Quaternion.Euler(0f, R(s, i + 100) * 90f, 0f);
                        Scenery(parent, "Mesa", centre, size, i % 2 == 0 ? Id.DesertRock : Id.Sandstone, rotation);
                        Scenery(parent, "Mesa Cap", centre + Vector3.up * (height / 2f + 0.6f), new Vector3(size.x * 0.85f, 1.2f, size.z * 0.85f), Id.Sandstone, rotation);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (i * 45f + 20f) * Mathf.Deg2Rad, radius = 40f + R(s, i + 120) * 8f;
                        var centre = new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);
                        Scenery(parent, "Dune", centre, new Vector3(28f, 5f, 14f), Id.Sand, Quaternion.Euler(8f, i * 45f + 110f, 0f));
                    }
                    break;

                case Style.Forest:
                    for (int i = 0; i < 26; i++)
                    {
                        float angle = (i * (360f / 26f) + R(s, i) * 8f) * Mathf.Deg2Rad, radius = 37f + R(s, i + 30) * 8f;
                        var foot = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        float trunk = 6f + R(s, i + 60) * 4f, crown = 6f + R(s, i + 90) * 3f;
                        Scenery(parent, "Trunk", foot + Vector3.up * (trunk / 2f + 2f), new Vector3(1f, trunk, 1f), Id.Bark, Quaternion.identity);
                        Scenery(parent, "Crown", foot + Vector3.up * (trunk + 2f + crown * 0.3f), new Vector3(crown, crown * 0.8f, crown),
                            i % 3 == 0 ? Id.Pine : i % 3 == 1 ? Id.LeavesDark : Id.Leaves, Quaternion.Euler(0f, R(s, i + 120) * 90f, 0f));
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = (i * 60f + 15f) * Mathf.Deg2Rad;
                        var centre = new Vector3(Mathf.Cos(angle) * 72f, 2f, Mathf.Sin(angle) * 72f);
                        Scenery(parent, "Hill", centre, new Vector3(46f, 14f, 30f), Id.Grass, Quaternion.Euler(0f, i * 60f + 105f, 0f));
                    }
                    break;

                case Style.Snow:
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = (i * 40f + R(s, i) * 18f) * Mathf.Deg2Rad, radius = 64f + R(s, i + 20) * 16f;
                        float size = 28f + R(s, i + 40) * 16f;
                        var centre = new Vector3(Mathf.Cos(angle) * radius, -size * 0.15f, Mathf.Sin(angle) * radius);
                        // A cube standing on a corner reads as a sharp low-poly peak.
                        Quaternion rotation = Quaternion.Euler(0f, R(s, i + 60) * 90f, 0f) * Quaternion.Euler(35.26f, 0f, 45f);
                        Scenery(parent, "Mountain", centre, Vector3.one * size, Id.IcyRock, rotation);
                        float cap = size * 0.5f;
                        Scenery(parent, "Mountain Snow", centre + Vector3.up * ((size - cap) * 0.866f), Vector3.one * cap, Id.Snow, rotation);
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = (i * 22.5f + R(s, i + 80) * 10f) * Mathf.Deg2Rad, radius = 36f + R(s, i + 100) * 6f;
                        var pine = new GameObject("Distant Pine").transform;
                        pine.SetParent(parent, false);
                        pine.SetPositionAndRotation(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius), Quaternion.Euler(0f, i * 37f, 0f));
                        Pine(pine, t, 11f + R(s, i + 120) * 5f, collider: false);
                    }
                    break;
            }

            foreach (MeshRenderer renderer in parent.GetComponentsInChildren<MeshRenderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Scenery(Transform parent, string name, Vector3 centre, Vector3 size, Id id, Quaternion rotation) =>
            EnvironmentKit.NoShadow(EnvironmentKit.Box(parent, name, centre, size, id, false, rotation));

        // ------------------------------------------------------------------ pieces

        private static void Rock(Transform r, Theme t, float a, float b, float c)
        {
            Cube(r, "Rock", 0f, b / 2f, 0f, a, b, c, t.Rock, true);
            Cube(r, "Rock Cap", a * 0.1f, b * 1.12f, -c * 0.05f, a * 0.65f, b * 0.24f, c * 0.6f, t.RockAccent, true, yaw: 23f);
            Cube(r, "Rock Chunk", -a * 0.42f, b * 0.22f, c * 0.35f, a * 0.45f, b * 0.45f, c * 0.4f, t.RockAccent, true, yaw: 40f, roll: 12f);
            if (t.Snowy) Flat(Cube(r, "Snow", a * 0.1f, b * 1.24f + 0.05f, -c * 0.05f, a * 0.6f, 0.1f, c * 0.55f, Id.Snow, yaw: 23f));
            else if (t.Style == Style.Forest) Flat(Cube(r, "Moss", a * 0.1f, b * 1.24f + 0.02f, -c * 0.05f, a * 0.5f, 0.05f, c * 0.45f, Id.Grass, yaw: 23f));
        }

        private static void Wall(Transform r, Theme t, float a, float b, float c)
        {
            Cube(r, "Wall", 0f, b / 2f, 0f, a, b, c, t.Wall, true);
            if (t.Style == Style.Forest)
            {
                // Wooden barricade: planks between log posts.
                int posts = Mathf.Max(2, Mathf.RoundToInt(a / 1.5f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    Cube(r, "Post", -a / 2f + a * i / (posts - 1), (b + 0.3f) / 2f, 0f, 0.26f, b + 0.3f, c + 0.1f, Id.Bark);
                }
                return;
            }
            Cube(r, "Wall Top", a * 0.2f, b + 0.2f, 0f, a * 0.3f, 0.4f, c, t.Wall, true);          // broken masonry step
            Flat(Cube(r, "Wall Base", 0f, 0.2f, 0f, a + 0.06f, 0.4f, c + 0.08f, t.WallTrim));
            if (!t.Snowy) return;
            Flat(Cube(r, "Snow", 0f, b + 0.04f, 0f, a, 0.08f, c + 0.04f, Id.Snow));
            Flat(Cube(r, "Snow", a * 0.2f, b + 0.44f, 0f, a * 0.3f, 0.08f, c + 0.04f, Id.Snow));
        }

        private static void Sandbags(Transform r, Theme t, float a, float b)
        {
            float h = b > 0f ? b : 1.2f;
            Cube(r, "Sandbags", 0f, (h - 0.15f) / 2f, 0f, a, h - 0.15f, 0.9f, Id.Sandbag);
            EnvironmentKit.ColliderOnly(r, "Sandbags Collider", new Vector3(0f, h / 2f, 0f), Quaternion.identity, new Vector3(a, h, 0.9f));
            int bags = Mathf.Max(1, Mathf.RoundToInt(a / 0.6f));
            float step = a / bags;
            for (int i = 0; i < bags; i++)
            {
                Flat(Cube(r, "Bag", -a / 2f + step * (i + 0.5f), h - 0.1f, i % 2 == 0 ? 0.05f : -0.05f, step - 0.05f, 0.22f, 0.62f, Id.Sandbag, yaw: (i % 3 - 1) * 4f));
            }
            if (t.Snowy) Flat(Cube(r, "Snow", 0f, h + 0.03f, 0f, a - 0.1f, 0.07f, 0.5f, Id.Snow));
        }

        private static void Fence(Transform r, Theme t, float a, float b)
        {
            float h = b > 0f ? b : 1.5f;
            Cube(r, "Planks", 0f, h / 2f, 0f, a, h, 0.1f, Id.Plank, true);
            int posts = Mathf.Max(2, Mathf.RoundToInt(a / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Cube(r, "Post", -a / 2f + a * i / (posts - 1), (h + 0.15f) / 2f, 0f, 0.18f, h + 0.15f, 0.18f, Id.WoodDark);
            }
            Flat(Cube(r, "Rail", 0f, h * 0.75f, 0.08f, a, 0.12f, 0.06f, Id.WoodDark));
            Flat(Cube(r, "Rail", 0f, h * 0.25f, 0.08f, a, 0.12f, 0.06f, Id.WoodDark));
            if (t.Snowy) Flat(Cube(r, "Snow", 0f, h + 0.03f, 0f, a, 0.06f, 0.14f, Id.Snow));
        }

        private static void Log(Transform r, Theme t, float a, float b)
        {
            float d = b > 0f ? b : 0.8f;
            Cube(r, "Log", 0f, d / 2f, 0f, d, d, a, Id.Bark, true);
            Flat(Cube(r, "Log End", 0f, d / 2f, a / 2f + 0.01f, d * 0.86f, d * 0.86f, 0.03f, Id.Wood));
            Flat(Cube(r, "Log End", 0f, d / 2f, -a / 2f - 0.01f, d * 0.86f, d * 0.86f, 0.03f, Id.Wood));
            Cube(r, "Branch", d * 0.45f, d * 0.7f, a * 0.15f, 0.18f, 0.18f, 1f, Id.Bark, yaw: 35f, pitch: -20f);
            if (t.Snowy) Flat(Cube(r, "Snow", 0f, d + 0.03f, 0f, d * 0.7f, 0.07f, a * 0.92f, Id.Snow));
            else if (t.Style == Style.Forest) Flat(Cube(r, "Moss", 0f, d + 0.02f, a * 0.1f, d * 0.6f, 0.04f, a * 0.5f, Id.RockMoss));
        }

        /// <summary>Ramp rising towards local +Z: ground level at z = 0, <paramref name="rise"/> at z = <paramref name="run"/>.</summary>
        private static void Ramp(Transform r, Theme t, float width, float rise, float run)
        {
            const float thickness = 2.5f; // thick enough that no gap opens under the slope
            float slope = Mathf.Sqrt(run * run + rise * rise);
            Quaternion q = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0f, 0f);
            var top = new Vector3(0f, rise / 2f, run / 2f);
            EnvironmentKit.Box(r, "Ramp", top - q * Vector3.up * (thickness / 2f), new Vector3(width, thickness, slope), t.Ramp, true, q);
            foreach (float s in new[] { -1f, 1f })
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(r, "Ramp Edge", top + q * new Vector3(s * (width / 2f + 0.05f), -0.12f, 0f),
                    new Vector3(0.1f, 0.4f, slope), t.WallTrim, false, q));
            }
        }

        private static void Platform(Transform r, Theme t, float a, float b, float c)
        {
            Cube(r, "Platform", 0f, b / 2f, 0f, a, b, c, t.Platform, true);
            Flat(Cube(r, "Platform Lip", 0f, b - 0.06f, 0f, a + 0.12f, 0.12f, c + 0.12f, t.WallTrim));
            if (t.Style == Style.Forest) Flat(Cube(r, "Grass Top", 0f, b + 0.02f, 0f, a - 0.3f, 0.04f, c - 0.3f, Id.Grass));
            if (t.Snowy) Flat(Cube(r, "Snow Top", 0f, b + 0.03f, 0f, a - 0.1f, 0.06f, c - 0.1f, Id.Snow));
        }

        private static void Palm(Transform r, float height)
        {
            float h = height > 0f ? height : 7f;
            const int segments = 5;
            float segment = h / segments;
            Vector3 p = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, -(4f + i * 3f)); // leans a little more towards +X each segment
                Vector3 dir = q * Vector3.up;
                float w = 0.42f - 0.03f * i;
                EnvironmentKit.Box(r, "Trunk", p + dir * (segment / 2f), new Vector3(w, segment + 0.06f, w), Id.Bark, false, q);
                p += dir * segment;
            }
            EnvironmentKit.ColliderOnly(r, "Trunk Collider", new Vector3(0.15f, h * 0.25f, 0f), Quaternion.identity, new Vector3(0.5f, h * 0.5f, 0.5f));

            for (int k = 0; k < 7; k++)
            {
                Quaternion q = Quaternion.Euler(28f + (k % 2) * 10f, k * 360f / 7f + 10f, 0f);
                EnvironmentKit.Box(r, "Frond", p + q * Vector3.forward * 1.45f + Vector3.up * 0.05f, new Vector3(0.7f, 0.05f, 3f), Id.PalmLeaf, false, q);
            }
            for (int k = 0; k < 3; k++)
            {
                Vector3 offset = Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0.22f, -0.28f, 0f);
                EnvironmentKit.NoShadow(EnvironmentKit.Box(r, "Coconut", p + offset, Vector3.one * 0.22f, Id.WoodDark));
            }
        }

        private static void DeadTree(Transform r, Theme t, float height)
        {
            float h = height > 0f ? height : 4.5f;
            Cube(r, "Trunk", 0f, h / 2f, 0f, 0.4f, h, 0.4f, t.DeadWood, true);
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 130f + 20f, 0f) * Quaternion.Euler(0f, 0f, -50f + k * 8f);
                var root = new Vector3(0f, h * (0.5f + 0.18f * k), 0f);
                EnvironmentKit.Box(r, "Branch", root + q * Vector3.up * 0.8f, new Vector3(0.16f, 1.6f, 0.16f), t.DeadWood, false, q);
            }
        }

        /// <summary>Voxel broadleaf tree: square trunk (collides) + three crown blocks (visual, above head height).</summary>
        private static void Tree(Transform r, float trunkHeight, float crownSize)
        {
            float h = trunkHeight > 0f ? trunkHeight : 3.4f, s = crownSize > 0f ? crownSize : 3.6f;
            Cube(r, "Trunk", 0f, (h + 0.5f) / 2f, 0f, 0.6f, h + 0.5f, 0.6f, Id.Bark, true);
            Cube(r, "Branch", 0.45f, h * 0.7f, 0f, 0.9f, 0.18f, 0.18f, Id.Bark, roll: 30f);
            Cube(r, "Crown", 0f, h + s * 0.3f, 0f, s, s * 0.8f, s, Id.Leaves);
            Cube(r, "Crown", s * 0.35f, h + s * 0.75f, -s * 0.2f, s * 0.6f, s * 0.5f, s * 0.6f, Id.LeavesDark, yaw: 15f);
            Cube(r, "Crown", -s * 0.3f, h + s * 0.1f, s * 0.3f, s * 0.55f, s * 0.45f, s * 0.55f, Id.LeavesDark, yaw: -20f);
        }

        /// <summary>Voxel pine: trunk + four shrinking needle tiers (lowest above head height), snow-capped on snowy maps.</summary>
        private static void Pine(Transform r, Theme t, float height, bool collider)
        {
            float h = height > 0f ? height : 8f, k = h / 8f;
            Cube(r, "Trunk", 0f, h * 0.4f, 0f, 0.45f * k, h * 0.8f, 0.45f * k, Id.Bark, collider);
            float tier = 1.5f * k, baseY = Mathf.Max(2.3f, 2.4f * k);
            float[] widths = { 3.6f, 2.7f, 1.8f, 0.9f };
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i] * k, y = baseY + tier * (0.5f + 0.8f * i);
                Cube(r, "Needles", 0f, y, 0f, w, tier, w, Id.Pine, yaw: i * 45f + 15f);
                if (t.Snowy) Flat(Cube(r, "Snow", 0f, y + tier / 2f + 0.05f, 0f, w * 0.82f, 0.1f, w * 0.82f, Id.Snow, yaw: i * 45f + 15f));
            }
        }

        private static void Bush(Transform r, float size)
        {
            float s = size > 0f ? size : 1.3f;
            Flat(Cube(r, "Bush", 0f, s * 0.35f, 0f, s, s * 0.7f, s * 0.9f, Id.Leaves));
            Flat(Cube(r, "Bush", s * 0.3f, s * 0.55f, s * 0.15f, s * 0.6f, s * 0.5f, s * 0.6f, Id.LeavesDark, yaw: 25f));
            Flat(Cube(r, "Bush", -s * 0.3f, s * 0.3f, -s * 0.2f, s * 0.5f, s * 0.45f, s * 0.5f, Id.Leaves, yaw: -15f));
        }

        private static void Ruin(Transform r, Theme t)
        {
            Segments(r, RuinWalls, t.Wall, t.Snowy);
            Blocks(r, RuinBlocks, t.WallTrim, true);
            Flat(Cube(r, "Floor Tiles", 0f, 0.01f, 0f, 9.4f, 0.02f, 7.4f, Id.Sandstone));
            // Remains of the roof: beams over the courtyard (out of reach, visual).
            Cube(r, "Beam", 0f, 3.35f, 1.6f, 10.8f, 0.22f, 0.28f, Id.WoodDark);
            Cube(r, "Beam", 0f, 3.35f, -1.6f, 10.8f, 0.22f, 0.28f, Id.WoodDark);
            Cube(r, "Beam", 2.6f, 3.5f, 0f, 0.28f, 0.22f, 8.6f, Id.WoodDark);
            Cube(r, "Fallen Beam", -2f, 1.2f, -2.6f, 0.26f, 0.22f, 3.2f, Id.WoodDark, yaw: 70f, pitch: -38f);
        }

        private static void Cabin(Transform r, Theme t)
        {
            Segments(r, CabinWalls, Id.Plank, false);
            Flat(Cube(r, "Floor", 0f, 0.01f, 0f, 6.6f, 0.02f, 4.6f, Id.Wood));
            foreach (float x in new[] { -3.5f, 3.5f })
            {
                foreach (float z in new[] { -2.5f, 2.5f }) Cube(r, "Corner Log", x, 1.45f, z, 0.36f, 2.9f, 0.36f, Id.Bark);
            }

            // Gable roof (collides: it blocks shots from the plateau) with stepped gable ends.
            const float pitch = 32f, halfSpan = 2.85f;
            float slab = halfSpan / Mathf.Cos(pitch * Mathf.Deg2Rad), rise = halfSpan * Mathf.Tan(pitch * Mathf.Deg2Rad);
            foreach (float s in new[] { -1f, 1f })
            {
                Cube(r, "Roof", 0f, 2.8f + rise / 2f + 0.08f, s * halfSpan / 2f, 7.7f, 0.14f, slab, Id.WoodDark, true, pitch: s * pitch);
            }
            Cube(r, "Ridge", 0f, 2.8f + rise + 0.05f, 0f, 7.8f, 0.18f, 0.18f, Id.Bark);
            foreach (float x in new[] { -3.5f, 3.5f })
            {
                Cube(r, "Gable", x, 3.1f, 0f, 0.3f, 0.6f, 4.4f, Id.Plank);
                Cube(r, "Gable", x, 3.7f, 0f, 0.3f, 0.6f, 2.9f, Id.Plank);
                Cube(r, "Gable", x, 4.2f, 0f, 0.3f, 0.4f, 1.4f, Id.Plank);
            }
            Cube(r, "Chimney", 2.2f, 4f, 1.1f, 0.6f, 1.8f, 0.6f, Id.Rock);
        }

        private static void Barracks(Transform r, Theme t)
        {
            Segments(r, BarracksWalls, Id.MilitaryGreen, false);
            Cube(r, "Roof", 0f, 3.125f, 0f, 9.7f, 0.25f, 5.2f, Id.Roof, true);
            Flat(Cube(r, "Roof Snow", 0f, 3.34f, 0f, 9.6f, 0.18f, 5.1f, Id.Snow));
            Flat(Cube(r, "Floor", 0f, 0.01f, 0f, 8.6f, 0.02f, 4.1f, Id.ConcreteDark));
            Flat(Cube(r, "Base", 0f, 0.15f, 0f, 9.06f, 0.3f, 4.56f, Id.ConcreteDark));
            foreach (float x in new[] { -4.5f, 4.5f })
            {
                Flat(Cube(r, "Door Frame", x, 2.25f, 0f, 0.36f, 0.12f, 1.7f, Id.Steel));
            }
        }

        private static void Hut(Transform r, Theme t)
        {
            Segments(r, HutWalls, Id.MilitaryGreen, false);
            Cube(r, "Roof", 0f, 2.81f, 0f, 5.1f, 0.22f, 4.1f, Id.Roof, true);
            if (t.Snowy) Flat(Cube(r, "Roof Snow", 0f, 3f, 0f, 5f, 0.16f, 4f, Id.Snow));
            Flat(Cube(r, "Floor", 0f, 0.01f, 0f, 4.1f, 0.02f, 3.1f, Id.ConcreteDark));
        }

        private static void Tower(Transform r, Theme t)
        {
            Blocks(r, TowerBlocks, Id.WoodDark, true);
            Segments(r, TowerWalls, Id.Plank, t.Snowy);
            Flat(Cube(r, "Deck Snow", 0f, TowerDeck + 0.03f, 0f, 3.9f, 0.05f, 3.9f, Id.Snow));

            var ramp = new GameObject("Ramp").transform;
            ramp.SetParent(r, false);
            ramp.localPosition = new Vector3(0f, 0f, -2.1f - TowerRampRun);
            Ramp(ramp, t, TowerRampWidth, TowerDeck, TowerRampRun);

            // Lookout roof on thin posts (out of reach: visual only).
            foreach (float x in new[] { -1.95f, 1.95f })
            {
                foreach (float z in new[] { -1.95f, 1.95f }) Cube(r, "Roof Post", x, TowerDeck + 1.15f, z, 0.14f, 2.3f, 0.14f, Id.WoodDark);
            }
            Cube(r, "Roof", 0f, TowerDeck + 2.4f, 0f, 4.8f, 0.2f, 4.8f, Id.Roof);
            if (t.Snowy) Flat(Cube(r, "Roof Snow", 0f, TowerDeck + 2.57f, 0f, 4.7f, 0.14f, 4.7f, Id.Snow));
        }

        private static void Segments(Transform r, Seg[] segments, Id id, bool snowy)
        {
            foreach (Seg s in segments)
            {
                bool alongX = Mathf.Abs(s.X1 - s.X0) >= Mathf.Abs(s.Z1 - s.Z0);
                float length = alongX ? Mathf.Abs(s.X1 - s.X0) : Mathf.Abs(s.Z1 - s.Z0);
                Vector3 size = alongX ? new Vector3(length, s.H, s.T) : new Vector3(s.T, s.H, length);
                var centre = new Vector3((s.X0 + s.X1) / 2f, s.Y + s.H / 2f, (s.Z0 + s.Z1) / 2f);
                EnvironmentKit.Box(r, "Wall", centre, size, id, true);
                if (snowy) Flat(EnvironmentKit.Box(r, "Snow", centre + Vector3.up * (s.H / 2f + 0.035f), new Vector3(size.x, 0.07f, size.z), Id.Snow));
            }
        }

        private static void Blocks(Transform r, Block[] blocks, Id id, bool collider)
        {
            foreach (Block b in blocks)
            {
                EnvironmentKit.Box(r, "Block", b.Centre, b.Size, id, collider, Quaternion.Euler(0f, b.Yaw, 0f));
            }
        }

        // ------------------------------------------------------------------ spawns, lighting, camera

        private static void BuildSpawnPoints(Scene scene)
        {
            Transform root = NewRoot(scene, SpawnRoot);
            for (int slot = 0; slot < Spawns.Length; slot++)
            {
                SpawnSpec s = Spawns[slot];
                var go = new GameObject($"Spawn {slot} (slot {slot})");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(s.X, 0f, s.Z), Quaternion.Euler(0f, s.RotationY, 0f));
                go.AddComponent<SpawnPoint>().Configure(slot);
            }
        }

        private static void BuildLighting(Scene scene, Theme t)
        {
            Transform root = NewRoot(scene, LightingRoot);

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = t.SunRotation;
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = t.SunColor;
            sun.intensity = t.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = t.ShadowStrength;
            RenderSettings.sun = sun;

            RenderSettings.skybox = AtmosphereKit.ProceduralSky($"Sky_{t.Style}", t.SkyTint, t.SkyGround, t.SkyThickness, t.SkyExposure, t.SunSize);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            // Cheap three-colour ambient (no baked GI), matched to the sky and the ground bounce.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = t.AmbientSky;
            RenderSettings.ambientEquatorColor = t.AmbientEquator;
            RenderSettings.ambientGroundColor = t.AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = t.FogColor;
            RenderSettings.fogStartDistance = t.FogStart;
            RenderSettings.fogEndDistance = t.FogEnd;

            AtmosphereKit.AddGlobalVolume(AtmosphereKit.Look.Gameplay).transform.SetParent(root, false);

            ParticleSystem ambience = null;
            switch (t.Style)
            {
                case Style.Desert:
                    ambience = AtmosphereKit.Smoke(new Vector3(0f, 1.8f, 0f), new Vector3(56f, 2.5f, 56f), new Color(0.84f, 0.68f, 0.48f, 0.12f), 1.6f);
                    break;
                case Style.Forest:
                    ambience = AtmosphereKit.Smoke(new Vector3(0f, 1.2f, 0f), new Vector3(56f, 1.5f, 56f), new Color(0.8f, 0.88f, 0.82f, 0.08f), 1.2f);
                    break;
                case Style.Snow:
                    ambience = AtmosphereKit.Snowfall(new Vector3(0f, 14f, 0f), new Vector2(64f, 64f), 14f, 110f);
                    AtmosphereKit.Smoke(new Vector3(0f, 1.2f, 0f), new Vector3(56f, 1.5f, 56f), new Color(0.92f, 0.95f, 1f, 0.08f), 1.2f).transform.SetParent(root, true);
                    break;
            }
            if (ambience != null) ambience.transform.SetParent(root, true);
        }

        private static Camera EnsureOverviewCamera(Scene scene)
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == CameraName);
            if (existing != null && existing.TryGetComponent(out Camera found)) return found;
            if (existing != null) Object.DestroyImmediate(existing);

            // Same setup as the warehouse: used before the local player spawns and behind the results screen.
            var camObject = new GameObject(CameraName) { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(camObject, scene);
            Camera cam = camObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camObject.AddComponent<AudioListener>();
            return cam;
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Cube(Transform parent, string name, float x, float y, float z, float sx, float sy, float sz, Id id,
            bool collider = false, float yaw = 0f, float pitch = 0f, float roll = 0f) =>
            EnvironmentKit.Box(parent, name, new Vector3(x, y, z), new Vector3(sx, sy, sz), id, collider, Quaternion.Euler(pitch, yaw, roll));

        private static GameObject Flat(GameObject go) => EnvironmentKit.NoShadow(go);

        /// <summary>Deterministic 0..1 noise so every rebuild produces the same cliffs and scenery.</summary>
        private static float R(int seed, int i)
        {
            unchecked
            {
                uint h = (uint)(seed * 374761393 + i * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static void DestroyRoots(Scene scene, string name)
        {
            foreach (GameObject existing in scene.GetRootGameObjects().Where(go => go.name == name)) Object.DestroyImmediate(existing);
        }

        private static Transform NewRoot(Scene scene, string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static void EnsureRoot(Scene scene, string name)
        {
            if (scene.GetRootGameObjects().All(go => go.name != name)) NewRoot(scene, name);
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
