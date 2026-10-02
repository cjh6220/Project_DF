using UnityEngine;
using UnityEngine.UI;

namespace Proto.UI
{
    /// <summary>
    /// 던전 HUD 겉모습 — 씬 빌더가 만든 막대·미니맵에 새 UI 그림을 입힌다.
    /// 구조(이름)는 그대로 두고 그림만 바꾸므로 씬을 다시 만들 필요가 없다.
    ///
    ///   피로도·체력·채굴 막대  BarFrame(왼쪽 비스듬한 끝) + BarFill(사선 줄무늬, 색은 원래 색을 곱한다)
    ///   보스 체력바           BossBarFrame(양끝 금색 촉) — 원래 어두운 판 위에 얹는다
    ///   미니맵               MinimapFrame + 유리 바탕
    ///   획득물 줄            InfoStrip(오른쪽으로 사라지는 띠)
    /// 그림이 없으면 아무것도 하지 않는다.
    /// </summary>
    public static class HudSkin
    {
        static readonly Color Back = new Color(0.03f, 0.04f, 0.06f, 0.78f);

        public static void Apply(Transform canvas)
        {
            if (canvas == null) return;
            var frame = UiKit.Ui("BarFrame");
            var fill = UiKit.Ui("BarFill");
            if (frame == null || fill == null) return;

            Bar(canvas, "StaminaBar", frame, fill);
            Bar(canvas, "HealthBar", frame, fill);
            Bar(canvas, "MineFill", frame, fill);
            BossBar(canvas, fill);
            Minimap(canvas);
            LootStrip(canvas);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>막대 판(bg) + 안쪽 Fill. 판은 어두운 유리, 테두리는 위에 얹는다.</summary>
        static void Bar(Transform root, string name, Sprite frame, Sprite fill)
        {
            var bg = Find(root, name);
            if (bg == null || bg.Find("Skin") != null) return;
            var bgImg = bg.GetComponent<Image>();
            var rt = (RectTransform)bg;
            float h = rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y;

            if (bgImg != null) { bgImg.color = Back; }
            var f = bg.Find("Fill");
            if (f != null)
            {
                var fi = f.GetComponent<Image>();
                fi.sprite = fill;
                var c = fi.color;
                fi.color = new Color(Mathf.Min(1f, c.r * 1.15f), Mathf.Min(1f, c.g * 1.15f), Mathf.Min(1f, c.b * 1.15f), 1f);
                var frt = (RectTransform)f;
                frt.offsetMin = new Vector2(Mathf.Max(3f, h * 0.35f), 2f);   // 왼쪽 비스듬한 끝에 안 겹치게
                frt.offsetMax = new Vector2(-Mathf.Max(3f, h * 0.25f), -2f);
            }

            var skin = UiKit.Image(bg, "Skin", Color.white, frame);
            skin.type = Image.Type.Sliced;
            float pad = Mathf.Max(3f, h * 0.25f);
            float mult = frame.rect.height / (h + pad * 2f);
            skin.pixelsPerUnitMultiplier = mult;
            var s = skin.rectTransform;
            s.anchorMin = Vector2.zero; s.anchorMax = Vector2.one;
            s.offsetMin = new Vector2(-pad * 1.4f, -pad); s.offsetMax = new Vector2(pad, pad);
        }

        static void BossBar(Transform root, Sprite fill)
        {
            var spr = UiKit.Ui("BossBarFrame");
            Transform bar = null;
            var boss = Find(root, "BossHud");
            if (boss != null) foreach (var t in boss.GetComponentsInChildren<Transform>(true)) if (t.name == "Bar") { bar = t; break; }
            if (bar == null || spr == null || bar.Find("Skin") != null) return;

            var f = bar.Find("Fill");
            if (f != null) f.GetComponent<Image>().sprite = fill;
            var back = bar.Find("Back");
            if (back != null) back.GetComponent<Image>().color = new Color(0.10f, 0.05f, 0.06f, 0.95f);

            var rt = (RectTransform)bar;
            float h = rt.sizeDelta.y;
            var skin = UiKit.Image(bar, "Skin", Color.white, spr);
            skin.type = Image.Type.Sliced;
            float mult = spr.rect.height / (h + 18f);
            skin.pixelsPerUnitMultiplier = mult;
            var s = skin.rectTransform;
            s.anchorMin = Vector2.zero; s.anchorMax = Vector2.one;
            float capX = 38f / mult;
            s.offsetMin = new Vector2(-capX, -9f); s.offsetMax = new Vector2(capX, 9f);
            // 이름·상태 글자보다는 아래에 둔다
            var name = bar.Find("Name");
            skin.transform.SetSiblingIndex(name != null ? name.GetSiblingIndex() : bar.childCount - 1);
        }

        /// <summary>미니맵은 칸을 다시 그릴 때 자식을 전부 지운다 — 테두리는 형제로 붙인다.</summary>
        static void Minimap(Transform root)
        {
            var spr = UiKit.Ui("MinimapFrame");
            var map = Find(root, "Minimap") as RectTransform;
            if (spr == null || map == null || map.parent.Find("MinimapSkin") != null) return;
            var bg = UiKit.Image(map.parent, "MinimapSkinBack", UiKit.GlassFill, UiArt.RoundRectFill);
            bg.type = Image.Type.Sliced;
            Match(bg.rectTransform, map, 12f);
            bg.transform.SetSiblingIndex(map.GetSiblingIndex());
            var skin = UiKit.Image(map.parent, "MinimapSkin", Color.white, spr);
            skin.type = Image.Type.Sliced;
            skin.pixelsPerUnitMultiplier = 2f;
            Match(skin.rectTransform, map, 18f);
            skin.transform.SetSiblingIndex(map.GetSiblingIndex() + 1);
            skin.raycastTarget = false;
        }

        /// <summary>target과 같은 자리·크기, 사방으로 pad만큼 크게.</summary>
        static void Match(RectTransform rt, RectTransform target, float pad)
        {
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax; rt.pivot = target.pivot;
            rt.sizeDelta = target.sizeDelta + new Vector2(pad * 2f, pad * 2f);
            rt.anchoredPosition = target.anchoredPosition + new Vector2((0.5f - target.pivot.x) * 0f + (target.pivot.x - 0.5f) * -0f, 0f)
                                  + new Vector2((target.pivot.x * 2f - 1f) * pad, (target.pivot.y * 2f - 1f) * pad);
        }

        static void LootStrip(Transform root)
        {
            var spr = UiKit.Ui("InfoStrip");
            var loot = Find(root, "LootText");
            if (spr == null || loot == null || loot.parent.Find("LootStrip") != null) return;
            var lrt = (RectTransform)loot;
            var strip = UiKit.Image(loot.parent, "LootStrip", Color.white, spr);
            var s = strip.rectTransform;
            s.anchorMin = s.anchorMax = lrt.anchorMin;
            s.pivot = new Vector2(0f, 0.5f);
            s.anchoredPosition = lrt.anchoredPosition + new Vector2(-14f, -lrt.sizeDelta.y * (lrt.pivot.y - 0.5f));
            s.sizeDelta = new Vector2(620f, 40f);
            strip.transform.SetSiblingIndex(loot.GetSiblingIndex());
        }
    }
}
