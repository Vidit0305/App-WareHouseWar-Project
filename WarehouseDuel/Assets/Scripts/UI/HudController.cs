using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Audio;
using WarehouseWar.Core;
using WarehouseWar.Match;
using WarehouseWar.Multiplayer;
using WarehouseWar.Player;
using WarehouseWar.Weapons;

namespace WarehouseWar.UI
{
    /// <summary>
    /// In-match HUD. Reads only replicated state (local character, its weapon, RoomState, MatchManager),
    /// so what each player sees is exactly what the host decided.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text myNameText;
        [SerializeField] private TMP_Text myKillsText;
        [SerializeField] private TMP_Text opponentNameText;
        [SerializeField] private TMP_Text opponentKillsText;
        [SerializeField] private TMP_Text pingText;

        [Header("Weapon")]
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text reserveText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private RectTransform reloadFill;
        [SerializeField] private GameObject reloadGroup;

        [Header("Crosshair & feedback")]
        [SerializeField] private RectTransform[] crosshairLines = new RectTransform[4]; // up, down, left, right
        [SerializeField] private GameObject crosshairDot;
        [SerializeField] private CanvasGroup scopeOverlay;
        [SerializeField] private CanvasGroup hitMarker;
        [SerializeField] private Image hitMarkerImage;
        [SerializeField] private CanvasGroup damageVignette;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private CanvasGroup bannerGroup;

        [Header("Centre messages")]
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text centerMessageText;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private TMP_Text deathText;

        [Header("Groups")]
        [SerializeField] private GameObject gameplayGroup;
        [SerializeField] private GameObject touchControls;

        private static readonly Color HealthHigh = new Color(0.35f, 0.9f, 0.45f);
        private static readonly Color HealthLow = new Color(1f, 0.3f, 0.25f);
        private static readonly Color HeadshotColor = new Color(1f, 0.35f, 0.25f);

        private PlayerCharacter subscribedCharacter;
        private WeaponController subscribedWeapons;
        private float crosshairSpread;
        private float bannerUntil;
        private int lastCountdownSecond = -1;
        private string cachedTimer = string.Empty;
        private int cachedSeconds = -1;

        private void OnEnable()
        {
            PlayerCharacter.AnyDied += OnAnyDied;
            MatchManager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            PlayerCharacter.AnyDied -= OnAnyDied;
            MatchManager.PhaseChanged -= OnPhaseChanged;
            Unsubscribe();
        }

        private void Start()
        {
            hitMarker.alpha = 0f;
            damageVignette.alpha = 0f;
            bannerGroup.alpha = 0f;
            deathPanel.SetActive(false);
            countdownText.gameObject.SetActive(false);

            if (AudioManager.Library != null)
            {
                AudioManager.Instance.StopMusic();
                AudioManager.Instance.PlayAmbience(AudioManager.Library.warehouseAmbience, 0.3f);
            }
        }

        private void Update()
        {
            PlayerCharacter me = PlayerCharacter.Local;
            if (me != subscribedCharacter) Subscribe(me);

            MatchManager match = MatchManager.Instance;
            RoomState room = RoomState.Instance;
            bool hasPlayer = me != null && me.Object != null && me.Object.IsValid;

            gameplayGroup.SetActive(hasPlayer);
            UpdateScope(hasPlayer);
            if (LocalInputCollector.Instance != null)
            {
                bool ended = match != null && match.Phase == MatchPhase.Ended;
                LocalInputCollector.Instance.InputEnabled = hasPlayer && !ended && !PauseMenu.IsOpen;
                if (touchControls != null) touchControls.SetActive(!ended && Controls.TouchControlsView.ShouldShow);
            }

            UpdateTimer(match);
            UpdateScores(room, me);
            UpdatePing();
            if (!hasPlayer)
            {
                centerMessageText.text = match == null ? "Loading match…" : "Spawning…";
                return;
            }

            UpdateHealth(me);
            UpdateWeapon(me);
            UpdateCenter(me, match);
            UpdateFeedback(me);
        }

        // ------------------------------------------------------------------ sections

        private void UpdateTimer(MatchManager match)
        {
            float seconds = 0f;
            if (match != null)
            {
                seconds = match.Phase == MatchPhase.Playing ? match.RemainingSeconds
                    : match.Phase == MatchPhase.Ended ? 0f
                    : match.Rules.matchDurationSeconds;
            }

            int whole = Mathf.CeilToInt(seconds);
            if (whole != cachedSeconds)
            {
                cachedSeconds = whole;
                cachedTimer = $"{whole / 60}:{whole % 60:00}";
                timerText.text = cachedTimer;
                timerText.color = match != null && match.Phase == MatchPhase.Playing && whole <= 30 ? HealthLow : Color.white;
            }

            // Countdown numbers.
            bool counting = match != null && match.Phase == MatchPhase.Countdown;
            countdownText.gameObject.SetActive(counting);
            if (counting)
            {
                int left = Mathf.CeilToInt(match.RemainingSeconds);
                if (left != lastCountdownSecond)
                {
                    lastCountdownSecond = left;
                    countdownText.text = left > 0 ? left.ToString() : "GO!";
                    countdownText.transform.localScale = Vector3.one * 1.4f;
                    if (left > 0 && AudioManager.Library != null) AudioManager.Instance.Play2D(AudioManager.Library.countdownTick);
                }
                countdownText.transform.localScale = Vector3.Lerp(countdownText.transform.localScale, Vector3.one, Time.deltaTime * 8f);
            }
        }

        private void UpdateScores(RoomState room, PlayerCharacter me)
        {
            if (room == null || room.Object == null) return;
            PlayerRef local = me != null && me.Object != null ? me.Owner : PlayerRef.None;
            if (local == PlayerRef.None && SessionService.Instance != null && SessionService.Instance.Runner != null)
            {
                local = SessionService.Instance.Runner.LocalPlayer;
            }

            // Free-for-all: my score and place on the left, the best other player on the right.
            PlayerSlot mine = default, theirs = default;
            int players = 0, ahead = 0;
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                PlayerSlot slot = room.Slots[i];
                if (!slot.Occupied) continue;
                players++;
                if (slot.Player == local) mine = slot;
                else if (!theirs.Occupied || slot.Kills > theirs.Kills) theirs = slot;
            }
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                PlayerSlot slot = room.Slots[i];
                if (slot.Occupied && slot.Player != local && slot.Kills > mine.Kills) ahead++;
            }

            myNameText.text = players > 2 ? $"YOU  #{ahead + 1}" : "YOU";
            myKillsText.text = mine.Kills.ToString();
            string rival = theirs.Occupied ? theirs.Name.ToString().ToUpperInvariant() : "—";
            opponentNameText.text = players > 2 && theirs.Occupied ? (theirs.Kills > mine.Kills ? $"1ST  {rival}" : $"BEST  {rival}") : rival;
            opponentKillsText.text = theirs.Occupied ? theirs.Kills.ToString() : "0";
        }

        private void UpdatePing()
        {
            if (pingText == null || SessionService.Instance == null) return;
            SessionService s = SessionService.Instance;
            pingText.text = s.IsPractice ? "PRACTICE" : s.IsHost ? "HOST" : $"{s.RoundTripMilliseconds:0} ms";
        }

        private void UpdateHealth(PlayerCharacter me)
        {
            float fraction = Mathf.Clamp01(me.Health / me.Rules.maxHealth);
            Vector2 max = healthFill.anchorMax;
            max.x = Mathf.Lerp(max.x, fraction, 1f - Mathf.Exp(-12f * Time.deltaTime));
            healthFill.anchorMax = max;
            healthFillImage.color = Color.Lerp(HealthLow, HealthHigh, fraction);
            healthText.text = Mathf.CeilToInt(me.Health).ToString();
        }

        private void UpdateWeapon(PlayerCharacter me)
        {
            WeaponController weapons = subscribedWeapons;
            if (weapons == null || weapons.Current == null) return;
            WeaponDefinition def = weapons.Current;

            if (def.usesAmmo)
            {
                ammoText.text = weapons.CurrentMagazine.ToString();
                bool low = def.magazineSize > 1 ? weapons.CurrentMagazine <= Mathf.Max(1, def.magazineSize / 5) : weapons.CurrentMagazine == 0;
                ammoText.color = low ? HealthLow : Color.white;
                reserveText.text = $"/ {weapons.CurrentReserve}";
            }
            else
            {
                ammoText.text = "∞"; // melee: no ammo
                ammoText.color = Color.white;
                reserveText.text = "";
            }
            weaponNameText.text = def.displayName.ToUpperInvariant();
            if (weaponIcon != null)
            {
                weaponIcon.sprite = def.icon;
                weaponIcon.enabled = def.icon != null;
            }

            bool reloading = weapons.IsReloading;
            reloadGroup.SetActive(reloading);
            if (reloading)
            {
                Vector2 max = reloadFill.anchorMax;
                max.x = weapons.ReloadProgress;
                reloadFill.anchorMax = max;
            }

            // Crosshair opens with weapon spread, movement and jumping; closes when aiming.
            float spread = me.IsAiming ? def.aimSpread : def.hipSpread;
            Vector3 v = me.Motor.Velocity;
            spread += new Vector2(v.x, v.z).magnitude * 0.4f;
            if (!me.Motor.Grounded) spread *= 1.5f;
            crosshairSpread = Mathf.Lerp(crosshairSpread, spread, 1f - Mathf.Exp(-15f * Time.deltaTime));
            float gap = 8f + crosshairSpread * 9f;
            if (crosshairLines.Length == 4)
            {
                crosshairLines[0].anchoredPosition = new Vector2(0f, gap);
                crosshairLines[1].anchoredPosition = new Vector2(0f, -gap);
                crosshairLines[2].anchoredPosition = new Vector2(-gap, 0f);
                crosshairLines[3].anchoredPosition = new Vector2(gap, 0f);
            }
        }

        /// <summary>Sniper scope: fades the lens overlay in while aiming a scoped weapon and hides the crosshair.</summary>
        private void UpdateScope(bool hasPlayer)
        {
            PlayerCameraRig rig = PlayerCameraRig.Local;
            float blend = hasPlayer && rig != null ? rig.ScopeBlend : 0f;
            if (scopeOverlay != null)
            {
                scopeOverlay.alpha = blend;
                bool show = blend > 0.001f;
                if (scopeOverlay.gameObject.activeSelf != show) scopeOverlay.gameObject.SetActive(show);
            }
            bool crosshair = blend < 0.5f;
            foreach (RectTransform line in crosshairLines)
            {
                if (line != null && line.gameObject.activeSelf != crosshair) line.gameObject.SetActive(crosshair);
            }
            if (crosshairDot != null && crosshairDot.activeSelf != crosshair) crosshairDot.SetActive(crosshair);
        }

        private void UpdateCenter(PlayerCharacter me, MatchManager match)
        {
            string message = string.Empty;
            if (match != null && match.Phase == MatchPhase.WaitingForPlayers)
            {
                message = SessionService.Instance != null && SessionService.Instance.IsPractice ? "Get ready…" : "Waiting for players to load…";
            }
            else if (me.IsAlive && me.IsProtected && match != null && match.Phase == MatchPhase.Playing)
            {
                message = "SPAWN PROTECTION";
            }
            centerMessageText.text = message;

            bool dead = !me.IsAlive && match != null && match.Phase == MatchPhase.Playing;
            if (deathPanel.activeSelf != dead) deathPanel.SetActive(dead);
            if (dead)
            {
                float respawn = me.RespawnTimer.RemainingTime(me.Runner) ?? 0f;
                string killer = RoomState.Instance != null ? RoomState.Instance.NameOf(me.LastKiller) : "opponent";
                deathText.text = $"ELIMINATED BY <color=#FF5A3C>{killer.ToUpperInvariant()}</color>\n<size=60%>Respawning in {Mathf.CeilToInt(respawn)}…</size>";
            }
        }

        private void UpdateFeedback(PlayerCharacter me)
        {
            hitMarker.alpha = Mathf.MoveTowards(hitMarker.alpha, 0f, Time.deltaTime * 4f);
            damageVignette.alpha = Mathf.MoveTowards(damageVignette.alpha, me.IsAlive && me.Health < 30f ? 0.25f : 0f, Time.deltaTime * 1.5f);
            if (Time.time > bannerUntil) bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, 0f, Time.deltaTime * 3f);
        }

        // ------------------------------------------------------------------ events

        private void Subscribe(PlayerCharacter me)
        {
            Unsubscribe();
            subscribedCharacter = me;
            if (me == null) return;
            subscribedWeapons = me.GetComponent<WeaponController>();
            me.Damaged += OnDamaged;
            if (subscribedWeapons != null) subscribedWeapons.HitConfirmed += OnHitConfirmed;
        }

        private void Unsubscribe()
        {
            if (subscribedCharacter != null) subscribedCharacter.Damaged -= OnDamaged;
            if (subscribedWeapons != null) subscribedWeapons.HitConfirmed -= OnHitConfirmed;
            subscribedCharacter = null;
            subscribedWeapons = null;
        }

        private void OnDamaged()
        {
            damageVignette.alpha = 0.6f;
            Haptics.Medium();
        }

        private void OnHitConfirmed(bool headshot, bool killed)
        {
            hitMarker.alpha = 1f;
            hitMarkerImage.color = headshot || killed ? HeadshotColor : Color.white;
            hitMarker.transform.localScale = Vector3.one * (killed ? 1.5f : 1f);
        }

        private void OnAnyDied(PlayerCharacter victim, PlayerRef killer)
        {
            PlayerCharacter me = PlayerCharacter.Local;
            if (me == null || RoomState.Instance == null) return;

            string victimName = RoomState.Instance.NameOf(victim.Owner).ToUpperInvariant();
            if (killer == me.Owner && victim != me)
            {
                ShowBanner(victim.LastDeathWasHeadshot ? $"HEADSHOT!  ELIMINATED {victimName}" : $"ELIMINATED {victimName}");
                Haptics.Medium();
            }
            else if (victim == me)
            {
                ShowBanner("YOU WERE ELIMINATED");
                Haptics.Heavy();
            }
        }

        private void OnPhaseChanged(MatchPhase previous, MatchPhase current)
        {
            if (current == MatchPhase.Playing && previous == MatchPhase.Countdown)
            {
                ShowBanner("FIGHT!");
                if (AudioManager.Library != null) AudioManager.Instance.Play2D(AudioManager.Library.countdownGo);
            }
            lastCountdownSecond = -1;
        }

        private void ShowBanner(string text)
        {
            bannerText.text = text;
            bannerGroup.alpha = 1f;
            bannerUntil = Time.time + 2f;
        }
    }
}
