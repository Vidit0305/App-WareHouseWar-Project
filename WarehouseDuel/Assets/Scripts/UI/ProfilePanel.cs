using TMPro;
using UnityEngine;
using WarehouseDuel.Core;

namespace WarehouseDuel.UI
{
    /// <summary>Player profile: name and lifetime stats stored on this device.</summary>
    public sealed class ProfilePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statsText;

        private void OnEnable()
        {
            int played = GameSettings.MatchesPlayed;
            int won = GameSettings.MatchesWon;
            int kills = GameSettings.TotalKills;
            int deaths = GameSettings.TotalDeaths;
            float ratio = deaths == 0 ? kills : kills / (float)deaths;
            float winRate = played == 0 ? 0f : 100f * won / played;

            nameText.text = GameSettings.PlayerName;
            statsText.text =
                $"Matches played  <b>{played}</b>\n" +
                $"Victories  <b>{won}</b>  ({winRate:0}%)\n" +
                $"Eliminations  <b>{kills}</b>\n" +
                $"Deaths  <b>{deaths}</b>\n" +
                $"K/D ratio  <b>{ratio:0.00}</b>";
        }
    }
}
