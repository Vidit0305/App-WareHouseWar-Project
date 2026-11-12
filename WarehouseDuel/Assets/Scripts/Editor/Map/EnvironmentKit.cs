using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseDuel.Core;
using Id = WarehouseDuel.EditorTools.Map.EnvironmentPalette.Id;

namespace WarehouseDuel.EditorTools.Map
{
    internal enum Piece
    {
        ContainerRed, ContainerBlue, ContainerGreen, ContainerOrange,
        Crate, CrateSmall, BarrelRed, BarrelBlue,
        StorageRack, Machine, MachineSmall, Pillar, JerseyBarrier,
        HangingLamp, HangingLampUnlit, RollUpDoor, Catwalk,
    }

    /// <summary>
    /// Builds the modular low-poly environment kit as prefabs in Assets/Prefabs/Environment.
    /// Every prop is assembled from primitives (visual children without colliders) plus ONE simple
    /// collider on the root or a dedicated child, so collisions stay cheap and predictable.
    /// Re-running overwrites the prefabs in place (GUIDs are kept, scene instances stay linked).
    /// To use art from Blender later: open a prefab and replace its visual children; keep the collider.
    /// </summary>
    internal static class EnvironmentKit
    {
        public const string PrefabFolder = "Assets/Prefabs/Environment";

        // Shared dimensions (metres) used by both the kit and the layout.
        public const float ContainerWidth = 2.44f, ContainerHeight = 2.6f, ContainerLength = 6.06f;
        public const float CatwalkWidth = 3f, CatwalkRise = 3.2f, CatwalkRun = 6f, CatwalkDeckLength = 16f;

        public static Dictionary<Piece, GameObject> BuildAll(SetupReport report)
        {
            FolderStructure.EnsureFolder(PrefabFolder);
            var kit = new Dictionary<Piece, GameObject>
            {
                [Piece.ContainerRed] = Save(Container("ContainerRed", Id.ContainerRed), Piece.ContainerRed, true),
                [Piece.ContainerBlue] = Save(Container("ContainerBlue", Id.ContainerBlue), Piece.ContainerBlue, true),
                [Piece.ContainerGreen] = Save(Container("ContainerGreen", Id.ContainerGreen), Piece.ContainerGreen, true),
                [Piece.ContainerOrange] = Save(Container("ContainerOrange", Id.ContainerOrange), Piece.ContainerOrange, true),
                [Piece.Crate] = Save(Crate("Crate", 1.2f), Piece.Crate, false),
                [Piece.CrateSmall] = Save(Crate("CrateSmall", 0.8f), Piece.CrateSmall, false),
                [Piece.BarrelRed] = Save(Barrel("BarrelRed", Id.BarrelRed), Piece.BarrelRed, false),
                [Piece.BarrelBlue] = Save(Barrel("BarrelBlue", Id.BarrelBlue), Piece.BarrelBlue, false),
                [Piece.StorageRack] = Save(StorageRack(), Piece.StorageRack, true),
                [Piece.Machine] = Save(Machine(), Piece.Machine, true),
                [Piece.MachineSmall] = Save(MachineSmall(), Piece.MachineSmall, true),
                [Piece.Pillar] = Save(Pillar(), Piece.Pillar, false),
                [Piece.JerseyBarrier] = Save(JerseyBarrier(), Piece.JerseyBarrier, false),
                [Piece.HangingLamp] = Save(Lamp("HangingLamp", true), Piece.HangingLamp, false),
                [Piece.HangingLampUnlit] = Save(Lamp("HangingLampUnlit", false), Piece.HangingLampUnlit, false),
                [Piece.RollUpDoor] = Save(RollUpDoor(), Piece.RollUpDoor, false),
                [Piece.Catwalk] = Save(Catwalk(), Piece.Catwalk, false),
            };
            report.Pass($"Environment kit: {kit.Count} prefabs written to {PrefabFolder}.");
            return kit;
        }

        // ------------------------------------------------------------------ pieces

        private static GameObject Container(string name, Id color)
        {
            const float w = ContainerWidth, h = ContainerHeight, l = ContainerLength;
            var root = new GameObject(name);
            Transform t = root.transform;

            Box(t, "Body", new Vector3(0f, h / 2f, 0f), new Vector3(w - 0.08f, h - 0.1f, l - 0.1f), color);

            // Corrugation ribs along both long sides.
            for (int i = 0; i < 10; i++)
            {
                float z = -2.7f + i * 0.6f;
                NoShadow(Box(t, "Rib", new Vector3(-(w / 2f - 0.02f), h / 2f, z), new Vector3(0.06f, h - 0.35f, 0.14f), color));
                NoShadow(Box(t, "Rib", new Vector3(w / 2f - 0.02f, h / 2f, z), new Vector3(0.06f, h - 0.35f, 0.14f), color));
            }

            // Steel frame: corner posts + top/bottom rails.
            foreach (float x in new[] { -(w / 2f - 0.07f), w / 2f - 0.07f })
            {
                foreach (float z in new[] { -(l / 2f - 0.07f), l / 2f - 0.07f })
                {
                    Box(t, "CornerPost", new Vector3(x, h / 2f, z), new Vector3(0.14f, h, 0.14f), Id.Steel);
                }
                Box(t, "RailBottom", new Vector3(x, 0.07f, 0f), new Vector3(0.12f, 0.14f, l), Id.Steel);
                Box(t, "RailTop", new Vector3(x, h - 0.07f, 0f), new Vector3(0.12f, 0.14f, l), Id.Steel);
            }
            foreach (float z in new[] { -(l / 2f - 0.06f), l / 2f - 0.06f })
            {
                Box(t, "EndRailBottom", new Vector3(0f, 0.07f, z), new Vector3(w, 0.14f, 0.12f), Id.Steel);
                Box(t, "EndRailTop", new Vector3(0f, h - 0.07f, z), new Vector3(w, 0.14f, 0.12f), Id.Steel);
            }

            // Door lock rods on the +Z end.
            for (int k = 0; k < 4; k++)
            {
                NoShadow(Box(t, "LockRod", new Vector3(-0.85f + k * 0.57f, h / 2f, l / 2f + 0.02f), new Vector3(0.05f, h - 0.3f, 0.05f), Id.Steel));
            }

            AddBoxCollider(root, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, l + 0.04f));
            return root;
        }

        private static GameObject Crate(string name, float s)
        {
            var root = new GameObject(name);
            Transform t = root.transform;
            float e = 0.12f * s / 1.2f; // frame plank thickness
            float o = s / 2f - e / 2f;

            Box(t, "Body", new Vector3(0f, s / 2f, 0f), Vector3.one * (s - 0.06f), Id.Wood);

            foreach (float x in new[] { -o, o })
            {
                foreach (float z in new[] { -o, o })
                {
                    Box(t, "EdgeV", new Vector3(x, s / 2f, z), new Vector3(e, s, e), Id.WoodDark);
                }
            }
            foreach (float y in new[] { e / 2f, s - e / 2f })
            {
                foreach (float z in new[] { -o, o }) Box(t, "EdgeX", new Vector3(0f, y, z), new Vector3(s, e, e), Id.WoodDark);
                foreach (float x in new[] { -o, o }) Box(t, "EdgeZ", new Vector3(x, y, 0f), new Vector3(e, e, s), Id.WoodDark);
            }

            // Diagonal braces on two faces.
            foreach (float z in new[] { -s / 2f, s / 2f })
            {
                NoShadow(Box(t, "Brace", new Vector3(0f, s / 2f, z), new Vector3(e * 0.8f, s * 1.2f, 0.04f), Id.WoodDark, false, Quaternion.Euler(0f, 0f, 45f)));
            }

            AddBoxCollider(root, new Vector3(0f, s / 2f, 0f), Vector3.one * s);
            return root;
        }

        private static GameObject Barrel(string name, Id color)
        {
            var root = new GameObject(name);
            Transform t = root.transform;
            Cylinder(t, "Body", new Vector3(0f, 0.45f, 0f), 0.6f, 0.9f, color, collider: true);
            NoShadow(Cylinder(t, "RingLow", new Vector3(0f, 0.22f, 0f), 0.63f, 0.05f, Id.Steel));
            NoShadow(Cylinder(t, "RingHigh", new Vector3(0f, 0.68f, 0f), 0.63f, 0.05f, Id.Steel));
            NoShadow(Cylinder(t, "Lid", new Vector3(0f, 0.905f, 0f), 0.5f, 0.02f, Id.Steel));
            return root;
        }

        private static GameObject StorageRack()
        {
            const float l = 6f, d = 1.1f, h = 4f;
            var root = new GameObject("StorageRack");
            Transform t = root.transform;

            foreach (float x in new[] { -(d / 2f - 0.05f), d / 2f - 0.05f })
            {
                foreach (float z in new[] { -2.95f, -1f, 1f, 2.95f })
                {
                    Box(t, "Upright", new Vector3(x, h / 2f, z), new Vector3(0.1f, h, 0.1f), Id.RackBlue);
                }
            }

            float[] levels = { 0.15f, 1.45f, 2.75f };
            // (z centre, width along z, height, depth along x)
            var cartons = new[]
            {
                new Vector4(-2.2f, 1.1f, 0.7f, 0.8f), new Vector4(-0.9f, 0.9f, 0.9f, 0.8f),
                new Vector4(0.6f, 1.3f, 0.8f, 0.85f), new Vector4(2.1f, 1.0f, 0.6f, 0.8f),
            };

            for (int level = 0; level < levels.Length; level++)
            {
                float y = levels[level];
                foreach (float x in new[] { -(d / 2f - 0.05f), d / 2f - 0.05f })
                {
                    Box(t, "Beam", new Vector3(x, y, 0f), new Vector3(0.08f, 0.12f, l), Id.RackOrange);
                }
                NoShadow(Box(t, "Shelf", new Vector3(0f, y + 0.08f, 0f), new Vector3(d - 0.1f, 0.04f, l - 0.1f), Id.Grating));

                for (int i = 0; i < cartons.Length; i++)
                {
                    if ((i + level) % 3 == 2) continue; // leave gaps so racks don't look copy-pasted
                    Vector4 c = cartons[(i + level) % cartons.Length];
                    float z = c.x + (level - 1) * 0.25f;
                    Box(t, "Carton", new Vector3(0f, y + 0.1f + c.z / 2f, z), new Vector3(c.w, c.z, c.y), Id.Cardboard);
                }
            }
            foreach (float x in new[] { -(d / 2f - 0.05f), d / 2f - 0.05f })
            {
                Box(t, "TopBeam", new Vector3(x, h - 0.06f, 0f), new Vector3(0.08f, 0.12f, l), Id.RackOrange);
            }

            AddBoxCollider(root, new Vector3(0f, h / 2f, 0f), new Vector3(d, h, l));
            return root;
        }

        private static GameObject Machine()
        {
            var root = new GameObject("Machine");
            Transform t = root.transform;

            Box(t, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(4f, 0.3f, 4f), Id.MachineDark);
            foreach (float s in new[] { -1f, 1f })
            {
                NoShadow(Box(t, "HazardEdge", new Vector3(0f, 0.31f, s * 1.9f), new Vector3(4.02f, 0.04f, 0.2f), Id.Hazard));
                NoShadow(Box(t, "HazardEdge", new Vector3(s * 1.9f, 0.31f, 0f), new Vector3(0.2f, 0.04f, 4.02f), Id.Hazard));
            }
            Box(t, "Body", new Vector3(0f, 1.4f, 0f), new Vector3(3.2f, 2.2f, 3.2f), Id.Machine);
            Box(t, "Top", new Vector3(0f, 2.8f, 0f), new Vector3(2.2f, 0.6f, 2.2f), Id.MachineDark);
            Cylinder(t, "Exhaust", new Vector3(0.6f, 3.5f, 0.6f), 0.5f, 0.8f, Id.Steel);

            foreach (float x in new[] { -1.75f, 1.75f })
            {
                foreach (float z in new[] { -1.75f, 1.75f })
                {
                    Cylinder(t, "Pipe", new Vector3(x, 1.6f, z), 0.25f, 2.6f, Id.Steel);
                }
            }
            foreach (float z in new[] { -1.75f, 1.75f })
            {
                Cylinder(t, "PipeCross", new Vector3(0f, 2.75f, z), 0.2f, 3.5f, Id.Steel, false, Quaternion.Euler(0f, 0f, 90f));
            }

            // Control panels on both ends (the machine sits on the map's symmetry centre).
            foreach (float s in new[] { -1f, 1f })
            {
                float z = s * 1.66f;
                Box(t, "Panel", new Vector3(0f, 1.5f, z), new Vector3(1.4f, 0.9f, 0.12f), Id.MachineDark);
                float lz = s * 1.73f;
                NoShadow(Box(t, "LightGreen", new Vector3(-0.4f, 1.7f, lz), new Vector3(0.15f, 0.15f, 0.05f), Id.PanelGreen));
                NoShadow(Box(t, "LightRed", new Vector3(0f, 1.7f, lz), new Vector3(0.15f, 0.15f, 0.05f), Id.PanelRed));
                NoShadow(Box(t, "LightGreen", new Vector3(0.4f, 1.7f, lz), new Vector3(0.15f, 0.15f, 0.05f), Id.PanelGreen));
                NoShadow(Box(t, "Screen", new Vector3(0f, 1.35f, lz), new Vector3(1.0f, 0.3f, 0.05f), Id.PanelGreen));
            }

            AddBoxCollider(root, new Vector3(0f, 1.55f, 0f), new Vector3(4f, 3.1f, 4f));
            return root;
        }

        private static GameObject MachineSmall()
        {
            var root = new GameObject("MachineSmall");
            Transform t = root.transform;

            Box(t, "Base", new Vector3(0f, 0.125f, 0f), new Vector3(3f, 0.25f, 2f), Id.MachineDark);
            NoShadow(Box(t, "HazardEdge", new Vector3(0f, 0.26f, 0.9f), new Vector3(3.02f, 0.04f, 0.2f), Id.Hazard));
            NoShadow(Box(t, "HazardEdge", new Vector3(0f, 0.26f, -0.9f), new Vector3(3.02f, 0.04f, 0.2f), Id.Hazard));
            Box(t, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(2.6f, 1.7f, 1.6f), Id.Machine);
            Cylinder(t, "Vent", new Vector3(0f, 2.2f, 0f), 0.6f, 2.2f, Id.Steel, false, Quaternion.Euler(0f, 0f, 90f));

            foreach (float s in new[] { -1f, 1f })
            {
                Box(t, "Panel", new Vector3(0.5f, 1.2f, s * 0.84f), new Vector3(1.0f, 0.6f, 0.08f), Id.MachineDark);
                NoShadow(Box(t, "Light", new Vector3(0.25f, 1.3f, s * 0.89f), new Vector3(0.12f, 0.12f, 0.04f), Id.PanelGreen));
                NoShadow(Box(t, "Light", new Vector3(0.75f, 1.3f, s * 0.89f), new Vector3(0.12f, 0.12f, 0.04f), Id.PanelRed));
            }

            AddBoxCollider(root, new Vector3(0f, 1.25f, 0f), new Vector3(3f, 2.5f, 2f));
            return root;
        }

        private static GameObject Pillar()
        {
            const float h = 9f;
            var root = new GameObject("Pillar");
            Transform t = root.transform;

            Box(t, "Web", new Vector3(0f, h / 2f, 0f), new Vector3(0.12f, h, 0.46f), Id.Steel);
            Box(t, "Flange", new Vector3(0f, h / 2f, 0.23f), new Vector3(0.5f, h, 0.07f), Id.Steel);
            Box(t, "Flange", new Vector3(0f, h / 2f, -0.23f), new Vector3(0.5f, h, 0.07f), Id.Steel);
            Box(t, "Guard", new Vector3(0f, 0.55f, 0f), new Vector3(0.62f, 1.1f, 0.62f), Id.Hazard);
            NoShadow(Box(t, "GuardStripe", new Vector3(0f, 0.3f, 0f), new Vector3(0.63f, 0.15f, 0.63f), Id.Steel));
            NoShadow(Box(t, "GuardStripe", new Vector3(0f, 0.75f, 0f), new Vector3(0.63f, 0.15f, 0.63f), Id.Steel));

            AddBoxCollider(root, new Vector3(0f, h / 2f, 0f), new Vector3(0.62f, h, 0.62f));
            return root;
        }

        private static GameObject JerseyBarrier()
        {
            var root = new GameObject("JerseyBarrier");
            Transform t = root.transform;
            Box(t, "Base", new Vector3(0f, 0.175f, 0f), new Vector3(2.4f, 0.35f, 0.7f), Id.Concrete);
            Box(t, "Top", new Vector3(0f, 0.675f, 0f), new Vector3(2.4f, 0.65f, 0.36f), Id.Concrete);
            NoShadow(Box(t, "Stripe", new Vector3(0f, 0.9f, 0f), new Vector3(2.42f, 0.08f, 0.37f), Id.Hazard));
            AddBoxCollider(root, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 0.7f));
            return root;
        }

        private static GameObject Lamp(string name, bool withLight)
        {
            var root = new GameObject(name);
            Transform t = root.transform;
            NoShadow(Box(t, "Rod", new Vector3(0f, 0.6f, 0f), new Vector3(0.04f, 1.2f, 0.04f), Id.Steel));
            NoShadow(Cylinder(t, "Shade", Vector3.zero, 1.1f, 0.25f, Id.Steel));
            NoShadow(Cylinder(t, "Glow", new Vector3(0f, -0.13f, 0f), 0.85f, 0.04f, Id.LampGlow));

            if (withLight)
            {
                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(t, false);
                lightObject.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 18f;
                light.intensity = 2.4f;
                light.color = new Color(1f, 0.92f, 0.78f);
                light.shadows = LightShadows.None; // mobile budget: only the sun casts shadows
            }
            return root;
        }

        private static GameObject RollUpDoor()
        {
            // Decorative door mounted flat on an outer wall; the wall behind it provides collision.
            var root = new GameObject("RollUpDoor");
            Transform t = root.transform;
            Box(t, "Panel", new Vector3(0f, 2.5f, 0.04f), new Vector3(4.5f, 5f, 0.08f), Id.MetalWallRib);
            for (int i = 0; i < 9; i++)
            {
                NoShadow(Box(t, "Slat", new Vector3(0f, 0.3f + i * 0.55f, 0.09f), new Vector3(4.5f, 0.05f, 0.04f), Id.Steel));
            }
            Box(t, "FrameLeft", new Vector3(-2.375f, 2.6f, 0.1f), new Vector3(0.25f, 5.2f, 0.2f), Id.Hazard);
            Box(t, "FrameRight", new Vector3(2.375f, 2.6f, 0.1f), new Vector3(0.25f, 5.2f, 0.2f), Id.Hazard);
            Box(t, "FrameTop", new Vector3(0f, 5.1f, 0.1f), new Vector3(5f, 0.3f, 0.2f), Id.Hazard);
            return root;
        }

        /// <summary>
        /// Elevated walkway along a wall. Local space: walkway runs along +Z, x ∈ [-1.5, 1.5].
        /// Ramp from z = 0 (floor) up to z = 6 (3.2 m), deck from z = 6 to z = 22.
        /// The open (inner) side is -X and has a railing; +X is meant to sit against a wall.
        /// </summary>
        private static GameObject Catwalk()
        {
            const float w = CatwalkWidth, rise = CatwalkRise, run = CatwalkRun, deckLength = CatwalkDeckLength;
            const float rampThickness = 3f; // thick enough that no gap opens under the ramp
            const float railX = -w / 2f + 0.05f;

            var root = new GameObject("Catwalk");
            Transform t = root.transform;

            float slopeLength = Mathf.Sqrt(run * run + rise * rise);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Quaternion rampRotation = Quaternion.Euler(-angle, 0f, 0f);
            var rampTopCentre = new Vector3(0f, rise / 2f, run / 2f);

            Box(t, "Ramp", rampTopCentre - rampRotation * Vector3.up * (rampThickness / 2f),
                new Vector3(w, rampThickness, slopeLength), Id.Grating, true, rampRotation);
            Box(t, "Deck", new Vector3(0f, rise - 0.1f, run + deckLength / 2f), new Vector3(w, 0.2f, deckLength), Id.Grating, true);
            NoShadow(Box(t, "DeckEdge", new Vector3(railX, rise + 0.01f, run + deckLength / 2f), new Vector3(0.1f, 0.04f, deckLength), Id.Hazard));

            foreach (float z in new[] { run + 2f, run + 8f, run + 14f })
            {
                Box(t, "Support", new Vector3(-1.4f, (rise - 0.2f) / 2f, z), new Vector3(0.2f, rise - 0.2f, 0.2f), Id.Steel, true);
            }
            Box(t, "UnderBeam", new Vector3(-1.4f, rise - 0.3f, run + deckLength / 2f), new Vector3(0.15f, 0.2f, deckLength), Id.Steel);

            // Deck railing (visual) + one blocking collider.
            for (float z = run; z <= run + deckLength + 0.01f; z += 2f)
            {
                NoShadow(Box(t, "RailPost", new Vector3(railX, rise + 0.5f, z), new Vector3(0.06f, 1f, 0.06f), Id.Hazard));
            }
            NoShadow(Box(t, "RailTop", new Vector3(railX, rise + 1f, run + deckLength / 2f), new Vector3(0.06f, 0.06f, deckLength), Id.Hazard));
            NoShadow(Box(t, "RailMid", new Vector3(railX, rise + 0.5f, run + deckLength / 2f), new Vector3(0.05f, 0.05f, deckLength), Id.Hazard));
            ColliderOnly(t, "RailBlocker", new Vector3(railX, rise + 0.55f, run + deckLength / 2f), Quaternion.identity, new Vector3(0.1f, 1.1f, deckLength));

            // Ramp railing follows the slope.
            Vector3 rampRailCentre = new Vector3(railX, 0f, 0f) + rampTopCentre;
            NoShadow(Box(t, "RampRailTop", rampRailCentre + rampRotation * Vector3.up * 1f, new Vector3(0.06f, 0.06f, slopeLength), Id.Hazard, false, rampRotation));
            NoShadow(Box(t, "RampRailMid", rampRailCentre + rampRotation * Vector3.up * 0.5f, new Vector3(0.05f, 0.05f, slopeLength), Id.Hazard, false, rampRotation));
            ColliderOnly(t, "RampRailBlocker", rampRailCentre + rampRotation * Vector3.up * 0.55f, rampRotation, new Vector3(0.1f, 1.1f, slopeLength));

            return root;
        }

        // ------------------------------------------------------------------ helpers (also used by the map builder)

        public static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Id material,
            bool collider = false, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentPalette.Get(material);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Unity's cylinder is 1 m wide and 2 m tall; this scales it to the requested size.</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 centre, float diameter, float height, Id material,
            bool collider = false, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentPalette.Get(material);

            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (collider)
            {
                var meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                meshCollider.convex = true;
            }
            return go;
        }

        public static GameObject NoShadow(GameObject go)
        {
            if (go.TryGetComponent(out MeshRenderer renderer)) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static void ColliderOnly(Transform parent, string name, Vector3 centre, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation;
            go.AddComponent<BoxCollider>().size = size;
        }

        private static void AddBoxCollider(GameObject root, Vector3 centre, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = centre;
            collider.size = size;
        }

        /// <summary>Puts every object on the Environment layer and marks it static (batching + occlusion).</summary>
        public static void MakeStaticEnvironment(GameObject root, bool occluder)
        {
            int layer = LayerMask.NameToLayer(GameConstants.Layers.Environment);
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (occluder) flags |= StaticEditorFlags.OccluderStatic;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (layer >= 0) child.gameObject.layer = layer;
                if (child.GetComponent<Light>() == null) GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }

        private static GameObject Save(GameObject root, Piece piece, bool occluder)
        {
            MakeStaticEnvironment(root, occluder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{piece}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
