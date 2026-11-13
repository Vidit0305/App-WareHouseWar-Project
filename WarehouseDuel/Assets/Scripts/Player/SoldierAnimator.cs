using UnityEngine;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.Player
{
    /// <summary>
    /// Procedural "showroom" animation for the soldier in the main menu and lobby:
    /// breathing, weight shift, looking around, weapon sway, periodic weapon inspection (hands move with the gun),
    /// a lower-swap-raise animation when the weapon changes, and an aiming stance when the player is ready.
    /// Pure presentation; no networking.
    /// </summary>
    public sealed class SoldierAnimator : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Transform hips;
        [SerializeField] private Transform spine;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armsRig;
        [SerializeField] private Transform weaponSocket;

        [Header("Weapon")]
        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private int startWeapon;

        private Vector3 spineBasePos, hipsBasePos;
        private Quaternion spineBaseRot, headBaseRot, armsBaseRot, hipsBaseRot;
        private Vector2 lookTarget, look, lookVelocity;
        private float nextLookChange, nextInspect = 3f, inspectStart = -10f;
        private float swapStart = -10f;
        private int pendingWeapon = -1, shownWeapon = -1;
        private GameObject weaponModel;
        private float readyBlend;
        private bool ready;
        private float seed;

        private const float InspectDuration = 2.4f;
        private const float SwapDuration = 0.7f;

        public bool Ready { get => ready; set => ready = value; }

        private void Start()
        {
            seed = Random.value * 10f;
            if (spine != null) { spineBasePos = spine.localPosition; spineBaseRot = spine.localRotation; }
            if (hips != null) { hipsBasePos = hips.localPosition; hipsBaseRot = hips.localRotation; }
            if (head != null) headBaseRot = head.localRotation;
            if (armsRig != null) armsBaseRot = armsRig.localRotation;
            if (shownWeapon < 0 && pendingWeapon < 0) ShowWeapon(startWeapon); // a screen may already have picked one
        }

        /// <summary>Plays the weapon-inspection move now (e.g. when a new character is shown).</summary>
        public void PlayInspect()
        {
            if (ready) return;
            inspectStart = Time.time;
            nextInspect = Time.time + InspectDuration + Random.Range(5f, 9f);
        }

        /// <summary>Plays the lower/swap/raise animation and switches the held weapon.</summary>
        public void SetWeapon(int index)
        {
            if (index == shownWeapon || index == pendingWeapon) return;
            if (weaponModel == null) { ShowWeapon(index); return; }
            pendingWeapon = index;
            swapStart = Time.time;
        }

        private void Update()
        {
            float t = Time.time + seed;
            float dt = Time.deltaTime;
            readyBlend = Mathf.MoveTowards(readyBlend, ready ? 1f : 0f, dt * 3f);

            // Breathing + weight shift.
            float breath = Mathf.Sin(t * 1.7f);
            if (spine != null)
            {
                spine.localPosition = spineBasePos + Vector3.up * (breath * 0.006f);
                spine.localRotation = spineBaseRot * Quaternion.Euler(breath * 1.2f - readyBlend * 4f, Mathf.Sin(t * 0.45f) * 3f, 0f);
            }
            if (hips != null)
            {
                hips.localRotation = hipsBaseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.45f) * 1.5f);
                hips.localPosition = hipsBasePos + Vector3.up * (Mathf.Abs(breath) * 0.003f);
            }

            // Looking around: pick a new point of interest every few seconds.
            if (Time.time > nextLookChange)
            {
                nextLookChange = Time.time + Random.Range(1.8f, 4f);
                lookTarget = ready ? new Vector2(Random.Range(-6f, 6f), Random.Range(-3f, 2f))
                                   : new Vector2(Random.Range(-28f, 28f), Random.Range(-10f, 8f));
            }
            look = Vector2.SmoothDamp(look, lookTarget, ref lookVelocity, 0.35f);
            if (head != null) head.localRotation = headBaseRot * Quaternion.Euler(-look.y, look.x, 0f);

            // Weapon: sway, inspection, swap, ready stance.
            Quaternion arms = armsBaseRot * Quaternion.Euler(Mathf.Sin(t * 1.1f) * 1.5f, Mathf.Sin(t * 0.7f) * 2f, Mathf.Sin(t * 0.9f) * 1f);

            if (!ready && Time.time > nextInspect && Time.time - swapStart > SwapDuration)
            {
                inspectStart = Time.time;
                nextInspect = Time.time + InspectDuration + Random.Range(5f, 9f);
            }
            float inspect = Bell((Time.time - inspectStart) / InspectDuration);
            arms *= Quaternion.Euler(-14f * inspect, -32f * inspect, 38f * inspect);

            float swap = (Time.time - swapStart) / SwapDuration;
            if (swap >= 0f && swap <= 1f)
            {
                arms *= Quaternion.Euler(Bell(swap) * 55f, 0f, Bell(swap) * -12f);
                if (swap >= 0.5f && pendingWeapon >= 0)
                {
                    ShowWeapon(pendingWeapon);
                    pendingWeapon = -1;
                }
            }

            arms *= Quaternion.Euler(-10f * readyBlend, 0f, 0f); // shoulder the weapon when ready
            if (armsRig != null) armsRig.localRotation = arms;
        }

        private void ShowWeapon(int index)
        {
            if (catalog == null || weaponSocket == null) return;
            WeaponDefinition def = catalog.Get(index);
            if (def == null || def.worldModelPrefab == null) return;
            if (weaponModel != null) Destroy(weaponModel);
            weaponModel = Instantiate(def.worldModelPrefab, weaponSocket, false);
            shownWeapon = index;
        }

        /// <summary>0 → 1 → 0 smooth bump over t ∈ [0, 1].</summary>
        private static float Bell(float t) => t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * Mathf.PI);
    }
}
