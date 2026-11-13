using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WarehouseDuel.Core;
using WarehouseDuel.EditorTools.UI;
using WarehouseDuel.Match;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Player;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools.Gameplay
{
    /// <summary>
    /// Generates the networked prefabs: Player (character + hitboxes + body + camera rig), RoomState,
    /// MatchManager, and the non-networked SessionService prefab that references them.
    /// Also creates the MatchRules asset. Fusion registers prefabs with a NetworkObject automatically.
    /// </summary>
    internal static class GameplayPrefabBuilder
    {
        public const string PlayerFolder = "Assets/Prefabs/Players";
        public const string NetworkFolder = "Assets/Prefabs/Multiplayer";
        public const string RulesPath = "Assets/ScriptableObjects/GameSettings/MatchRules.asset";
        public const string SessionServicePath = NetworkFolder + "/SessionService.prefab";
        private const string MaterialFolder = "Assets/Art/Materials/Characters";

        public static SessionService SessionServicePrefab => AssetDatabase.LoadAssetAtPath<SessionService>(SessionServicePath);

        public static void BuildAll(SetupReport report, WeaponCatalog catalog)
        {
            FolderStructure.EnsureFolder(PlayerFolder);
            FolderStructure.EnsureFolder(NetworkFolder);
            FolderStructure.EnsureFolder(MaterialFolder);
            FolderStructure.EnsureFolder("Assets/ScriptableObjects/GameSettings");

            MatchRules rules = AssetDatabase.LoadAssetAtPath<MatchRules>(RulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<MatchRules>();
                AssetDatabase.CreateAsset(rules, RulesPath);
            }

            GameObject player = BuildPlayer(rules, catalog);
            GameObject room = BuildSimple("RoomState", go => go.AddComponent<RoomState>());
            GameObject match = BuildSimple("MatchManager", go => UiFactory.Wire(go.AddComponent<MatchManager>(), "rules", rules));

            var serviceRoot = new GameObject("SessionService");
            SessionService service = serviceRoot.AddComponent<SessionService>();
            UiFactory.Wire(service, "roomStatePrefab", room.GetComponent<NetworkObject>());
            UiFactory.Wire(service, "matchManagerPrefab", match.GetComponent<NetworkObject>());
            UiFactory.Wire(service, "playerPrefab", player.GetComponent<NetworkObject>());
            PrefabUtility.SaveAsPrefabAsset(serviceRoot, SessionServicePath);
            Object.DestroyImmediate(serviceRoot);

            report.Pass("Gameplay prefabs: Player, RoomState, MatchManager, SessionService and MatchRules generated.");
        }

        private static GameObject BuildSimple(string name, System.Action<GameObject> configure)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            configure(root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{NetworkFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ player

        private static GameObject BuildPlayer(MatchRules rules, WeaponCatalog catalog)
        {
            int playerLayer = LayerMask.NameToLayer(GameConstants.Layers.Player);
            int hitboxLayer = LayerMask.NameToLayer(GameConstants.Layers.Hitbox);
            int environmentLayer = LayerMask.NameToLayer(GameConstants.Layers.Environment);

            var root = new GameObject("Player") { layer = playerLayer };

            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkCharacterController>();

            PlayerCharacter character = root.AddComponent<PlayerCharacter>();
            UiFactory.Wire(character, "rules", rules);

            WeaponController weapons = root.AddComponent<WeaponController>();
            UiFactory.Wire(weapons, "catalog", catalog);
            UiFactory.SetLayerMask(weapons, "hitMask", (1 << environmentLayer) | (1 << hitboxLayer));
            UiFactory.SetLayerMask(weapons, "cosmeticMask", (1 << environmentLayer) | (1 << playerLayer));

            HitboxRoot hitboxRoot = root.AddComponent<HitboxRoot>();

            // --- body: voxel soldier (same model as the menu/lobby character), pivot at the feet
            SoldierModelBuilder.Rig soldier = SoldierModelBuilder.Build(root.transform, "Body");
            Transform body = soldier.Root;

            // --- hitboxes (Fusion lag-compensated; not physics colliders)
            Transform hitboxes = Child(root.transform, "Hitboxes", Vector3.zero);
            Hitbox head = MakeHitbox(hitboxes, "Head", new Vector3(0f, 1.62f, 0f), hitboxLayer, HitboxRegion.Region.Head, 1f);
            head.Type = HitboxTypes.Sphere;
            head.SphereRadius = 0.2f;
            Hitbox chest = MakeHitbox(hitboxes, "Body", new Vector3(0f, 1.13f, 0f), hitboxLayer, HitboxRegion.Region.Body, 1f);
            chest.Type = HitboxTypes.Box;
            chest.BoxExtents = new Vector3(0.28f, 0.34f, 0.2f);
            Hitbox legs = MakeHitbox(hitboxes, "Legs", new Vector3(0f, 0.43f, 0f), hitboxLayer, HitboxRegion.Region.Legs, 0.75f);
            legs.Type = HitboxTypes.Box;
            legs.BoxExtents = new Vector3(0.24f, 0.43f, 0.16f);
            hitboxRoot.Hitboxes = new[] { head, chest, legs };
            hitboxRoot.BroadRadius = 1.2f;
            hitboxRoot.Offset = new Vector3(0f, 0.9f, 0f);

            // --- spawn protection bubble (shown to the opponent)
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "ProtectionBubble";
            Object.DestroyImmediate(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            bubble.transform.localScale = new Vector3(1.3f, 2.1f, 1.3f);
            var bubbleRenderer = bubble.GetComponent<MeshRenderer>();
            bubbleRenderer.sharedMaterial = BubbleMaterial();
            bubbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bubble.SetActive(false);

            // --- first-person camera rig (enabled only for the local player)
            Transform rigRoot = Child(root.transform, "CameraRig", new Vector3(0f, PlayerCharacter.EyeHeight, 0f));
            PlayerCameraRig rig = rigRoot.gameObject.AddComponent<PlayerCameraRig>();
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(rigRoot, false);
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 72f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            Transform viewSocket = Child(cameraObject.transform, "ViewModelSocket", new Vector3(0.13f, -0.13f, 0.2f));
            UiFactory.Wire(rig, "playerCamera", cam);
            UiFactory.Wire(rig, "viewModelSocket", viewSocket);
            UiFactory.Wire(rig, "handPrefab", SoldierModelBuilder.BuildFirstPersonHandPrefab($"{PlayerFolder}/FP_Hand.prefab"));

            // --- presentation components
            PlayerVisuals visuals = root.AddComponent<PlayerVisuals>();
            UiFactory.Wire(visuals, "bodyRoot", body);
            UiFactory.Wire(visuals, "legLeft", soldier.LegL);
            UiFactory.Wire(visuals, "legRight", soldier.LegR);
            UiFactory.Wire(visuals, "armPivot", soldier.ArmsRig);
            UiFactory.Wire(visuals, "weaponSocket", soldier.WeaponSocket);
            UiFactory.WireArray(visuals, "teamColored", soldier.TeamColored.ToArray());
            UiFactory.Wire(visuals, "protectionBubble", bubble);
            root.AddComponent<PlayerAudio>();

            // Body parts live on the Player layer (visible, not hit by lag-compensated rays).
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = playerLayer;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PlayerFolder}/Player.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Hitbox MakeHitbox(Transform parent, string name, Vector3 position, int layer, HitboxRegion.Region region, float multiplier)
        {
            Transform t = Child(parent, name, position);
            t.gameObject.layer = layer;
            Hitbox hitbox = t.gameObject.AddComponent<Hitbox>();
            t.gameObject.AddComponent<HitboxRegion>().Configure(region, multiplier);
            return hitbox;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static Renderer Cube(Transform parent, Vector3 position, Vector3 size, Material material) =>
            Primitive(PrimitiveType.Cube, parent, position, size, material);

        private static Renderer Sphere(Transform parent, Vector3 position, Vector3 size, Material material) =>
            Primitive(PrimitiveType.Sphere, parent, position, size, material);

        private static Renderer Primitive(PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Material CharacterMaterial(string name, Color color, float smoothness, bool emissive = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BubbleMaterial()
        {
            string path = $"{MaterialFolder}/M_Char_ProtectionBubble.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("WarehouseDuel/Additive Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.25f, 0.6f, 1f, 0.22f));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
