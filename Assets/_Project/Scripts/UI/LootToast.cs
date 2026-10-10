using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 던전 화면 오른쪽 가운데 — 장비를 주울 때마다 한 줄씩 올라왔다가 사라진다.
    ///   "[희귀] 정교한 카타나"   ·   주워서 가방이 꽉 차면 "가방이 가득 찼습니다"를 한 번 더 띄운다
    /// 씬에 따로 둘 필요 없다. 게임이 시작되면 DungeonHud 아래에 스스로 붙는다.
    /// </summary>
    public class LootToast : MonoBehaviour
    {
        class Row { public RectTransform Rt; public TextMeshProUGUI Text; public Image Bg, Bar; public float Age; }

        const int Max = 5;
        const float Life = 3.2f, RowH = 44f;
        readonly List<Row> _rows = new();
        RunManager _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var hud = FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) return;
            var root = hud.transform.Find("DungeonHud");
            if (root == null || root.GetComponent<LootToast>() != null) return;
            root.gameObject.AddComponent<LootToast>();
        }

        void Awake() => _run = FindFirstObjectByType<RunManager>();
        void OnEnable() { if (_run != null) _run.ItemLooted += OnLoot; }
        void OnDisable()
        {
            if (_run != null) _run.ItemLooted -= OnLoot;
            foreach (var r in _rows) if (r.Rt != null) Destroy(r.Rt.gameObject);
            _rows.Clear();
        }

        void OnLoot(Item it)
        {
            if (_rows.Count >= Max) { Destroy(_rows[0].Rt.gameObject); _rows.RemoveAt(0); }
            var col = ItemDb.RarityColor(it.rarity);
            var bg = UiKit.Image(transform, "Toast", new Color(0.045f, 0.055f, 0.085f, 0.82f), UiArt.RoundRectFill);
            bg.type = Image.Type.Sliced;
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(420f, RowH - 6f);
            var bar = UiKit.Image(rt, "Bar", col);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(5f, -8f);
            bar.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            var t = UiKit.Text(rt, "Text", "", 20, TextAlignmentOptions.Left);
            UiKit.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(20f, 0f);
            t.rectTransform.offsetMax = new Vector2(-12f, 0f);
            t.richText = true;
            t.overflowMode = TextOverflowModes.Ellipsis;
            string head = $"<color=#{ItemDb.Hex(it.rarity)}>[{ItemDb.RarityName(it.rarity)}] {it.name}</color>";
            t.text = _run.Inventory.Full ? $"{head}  <color=#ff8a7a>· 가방 가득</color>" : head;
            _rows.Add(new Row { Rt = rt, Text = t, Bg = bg, Bar = bar });
        }

        void Update()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                var r = _rows[i];
                r.Age += Time.unscaledDeltaTime;
                if (r.Age >= Life) { Destroy(r.Rt.gameObject); _rows.RemoveAt(i); }
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                float slide = Mathf.Clamp01(r.Age / 0.2f);
                float fade = 1f - Mathf.Clamp01((r.Age - (Life - 0.5f)) / 0.5f);
                float y = 60f + (_rows.Count - 1 - i) * RowH;
                r.Rt.anchoredPosition = new Vector2(-24f + 40f * (1f - slide), y);
                r.Text.alpha = fade * slide;
                var c = r.Bg.color; c.a = 0.82f * fade * slide; r.Bg.color = c;
                var b = r.Bar.color; b.a = fade * slide; r.Bar.color = b;
            }
        }
    }
}
