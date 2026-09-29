using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>피로도 바, HP, 이번 판 획득물, 채굴 진행도.</summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] Proto.Player.PlayerController player;

        [Header("Bars")]
        [SerializeField] Image staminaFill;
        [SerializeField] Image healthFill;
        [SerializeField] GameObject mineBarRoot;
        [SerializeField] Image mineFill;

        [Header("Text")]
        [SerializeField] TMP_Text staminaText;
        [SerializeField] TMP_Text lootText;

        Health _hp;

        void Start()
        {
            _hp = player.GetComponent<Health>();

            run.Stamina.Changed += (cur, max) =>
            {
                if (staminaFill != null) staminaFill.fillAmount = max <= 0 ? 0 : cur / max;
                if (staminaText != null) staminaText.text = Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);
            };

            _hp.Changed += (cur, max) =>
            {
                if (healthFill != null) healthFill.fillAmount = max <= 0 ? 0 : cur / max;
            };

            run.RunLoot.Changed += (a, b) => RefreshLoot();
            run.RunLoot.Cleared += RefreshLoot;

            player.MineProgressChanged += p =>
            {
                bool mining = p >= 0f;
                if (mineBarRoot != null) mineBarRoot.SetActive(mining);
                if (mining && mineFill != null) mineFill.fillAmount = p;
            };

            RefreshLoot();
        }

        void RefreshLoot()
        {
            if (lootText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in run.RunLoot.All)
                sb.Append(Label(kv.Key)).Append(' ').Append(kv.Value).Append("   ");
            lootText.text = sb.Length == 0 ? "획득물 없음" : sb.ToString();
        }

        static string Label(ResourceId id) => id switch
        {
            ResourceId.Gold => "골드",
            ResourceId.Ore => "원석",
            ResourceId.Crystal => "결정",
            ResourceId.Alien => "이질체",
            ResourceId.Essence => "정수",
            _ => "핵"
        };
    }
}
