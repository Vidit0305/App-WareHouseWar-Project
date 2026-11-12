using Fusion;
using UnityEngine;
using WarehouseDuel.Audio;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.Player
{
    /// <summary>
    /// Player sounds on every peer: footsteps, jump/land, weapon fire/reload/switch, hurt and death.
    /// The local player's own sounds are 2D; the opponent's are 3D at their position so you can hear them coming.
    /// Hit markers and elimination stings play only for the shooter.
    /// </summary>
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float strideLength = 2.1f;

        private PlayerCharacter character;
        private WeaponController weapons;
        private Vector3 lastPosition;
        private float distanceSinceStep;
        private bool wasGrounded = true;
        private float airTime;

        private AudioLibrary Library => AudioManager.Library;

        private void Awake()
        {
            character = GetComponent<PlayerCharacter>();
            weapons = GetComponent<WeaponController>();
        }

        private void OnEnable()
        {
            weapons.ShotFired += OnShot;
            weapons.Reloaded += OnReload;
            weapons.Equipped += OnEquip;
            weapons.HitConfirmed += OnHitConfirmed;
            weapons.DryFired += OnDryFire;
            character.Damaged += OnDamaged;
            PlayerCharacter.AnyDied += OnAnyDied;
            PlayerCharacter.AnySpawned += OnAnySpawned;
        }

        private void OnDisable()
        {
            weapons.ShotFired -= OnShot;
            weapons.Reloaded -= OnReload;
            weapons.Equipped -= OnEquip;
            weapons.HitConfirmed -= OnHitConfirmed;
            weapons.DryFired -= OnDryFire;
            character.Damaged -= OnDamaged;
            PlayerCharacter.AnyDied -= OnAnyDied;
            PlayerCharacter.AnySpawned -= OnAnySpawned;
        }

        private void Play(AudioClip clip, float volume = 1f, float pitchVariance = 0.04f)
        {
            if (clip == null) return;
            if (character.IsLocal) AudioManager.Instance.Play2D(clip, volume * 0.8f, pitchVariance);
            else AudioManager.Instance.PlayAt(clip, transform.position + Vector3.up, volume, pitchVariance);
        }

        private void OnShot(WeaponDefinition weapon, bool predicted)
        {
            if (weapon != null) Play(weapon.fireSound, 1f, 0.05f);
        }

        private void OnReload(WeaponDefinition weapon)
        {
            if (weapon != null) Play(weapon.reloadSound, 0.8f, 0f);
        }

        private void OnEquip(WeaponDefinition weapon)
        {
            if (Library != null) Play(Library.weaponSwitch, 0.7f, 0f);
        }

        private void OnDryFire()
        {
            if (weapons.Current != null) Play(weapons.Current.dryFireSound, 0.8f, 0f);
        }

        private void OnHitConfirmed(bool headshot, bool killed)
        {
            if (Library == null || !character.IsLocal) return;
            AudioManager.Instance.Play2D(headshot ? Library.headshotMarker : Library.hitMarker, 0.9f);
        }

        private void OnDamaged()
        {
            if (Library != null && character.IsLocal) AudioManager.Instance.Play2D(Library.hurt, 0.8f, 0.08f);
        }

        private void OnAnyDied(PlayerCharacter victim, PlayerRef killer)
        {
            if (Library == null) return;
            if (victim == character) Play(Library.death, 1f, 0f);
            // Elimination sting for the local killer.
            if (character.IsLocal && killer == character.Owner && victim != character) AudioManager.Instance.Play2D(Library.elimination, 1f);
        }

        private void OnAnySpawned(PlayerCharacter spawned)
        {
            if (spawned != character) return;
            lastPosition = transform.position;
            if (Library != null) Play(Library.spawn, 0.6f, 0f);
        }

        private void Update()
        {
            if (character.Object == null || !character.Object.IsValid || Library == null) return;

            bool grounded = character.Motor.Grounded;
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (horizontal > 3f) horizontal = 0f; // teleport

            if (!grounded) airTime += Time.deltaTime;

            if (grounded && !wasGrounded && airTime > 0.35f) Play(Library.land, 0.7f);
            if (!grounded && wasGrounded && delta.y > 0.01f) Play(Library.jump, 0.6f);
            if (grounded) airTime = 0f;
            wasGrounded = grounded;

            if (!grounded || !character.IsAlive) return;
            distanceSinceStep += horizontal;
            float stride = character.IsSprinting ? strideLength * 1.25f : strideLength;
            if (distanceSinceStep >= stride)
            {
                distanceSinceStep = 0f;
                Play(AudioManager.Pick(Library.footsteps), character.IsSprinting ? 0.6f : 0.4f, 0.1f);
            }
        }
    }
}
