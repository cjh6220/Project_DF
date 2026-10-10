#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Proto.Town;

namespace Proto.EditorTools
{
    /// <summary>
    /// 던전 입구 포털 연출. 메뉴: Proto ▸ Rebuild Portal FX (씬의 문에 바로 다시 입힌다)
    ///
    ///   소용돌이 면    Proto/Portal 셰이더 — 비틀린 노이즈, 숨 쉬는 중심, 일렁이는 빛 테두리, 반짝이
    ///   빨려 드는 빛   파티클이 문 둘레에서 가운데로 돌며 빨려 들어간다
    ///   피어오르는 불티 문 앞 바닥에서 작은 빛이 떠오른다
    ///   바닥 빛       문 앞 땅을 포털 색으로 물들인다
    ///   바닥 마법진   보내 주신 룬 원 — 천천히 돌며 숨 쉬고, 던전을 고르면 번쩍인다
    ///   벚꽃잎       마을 전체에 꽃잎이 흩날린다 (Proto ▸ Rebuild Portal FX 가 같이 다시 만든다)
    ///
    /// 포털 색은 여기서 정하지 않는다. VillageGate가 던전마다 런타임에 칠한다.
    ///
    /// 문(도리이)의 크기와 자리는 기존 PortalBack 판을 기준으로 잡는다 — 손으로 옮겨 둔 위치를 따른다.
    /// </summary>
    public static class PortalFxBuilder
    {
        const string Mat = "Assets/_Project/Materials/";
        const string Spr = "Assets/_Project/Sprites/";

        [MenuItem("Proto/Rebuild Portal FX")]
        static void RebuildInScene()
        {
            var root = GameObject.Find("== PROTOTYPE ==");
            var gate = root != null ? root.transform.Find("VillageWorld/DungeonGate") : null;
            if (gate == null) { Debug.LogWarning("[Proto] DungeonGate를 못 찾음"); return; }
            Apply(gate);
            ApplyPetals(gate.parent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gate.gameObject.scene);
        }

        public static void Apply(Transform gate)
        {
            // 크기 기준: 기존 판 (없으면 도리이 크기에서 계산)
            Vector3 center; float w, h;
            var basis = gate.Find("PortalSurface") ?? gate.Find("PortalBack");
            if (basis != null)
            {
                w = basis.localScale.x; h = basis.localScale.y;
                center = basis.position - new Vector3(0f, h * 0.5f, 0f);   // 바닥 가운데
            }
            else
            {
                var b = Bounds(gate);
                w = b.size.x * 0.5f; h = b.size.y * 0.7f;
                center = new Vector3(b.center.x, b.min.y, b.center.z + 0.3f);
            }

            foreach (var n in new[] { "PortalBack", "PortalSwirl", "PortalSurface", "PortalMotes", "PortalEmbers", "PortalFloor", "PortalRune" })
            {
                var old = gate.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // ── 소용돌이 면 ──
            var surf = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surf.name = "PortalSurface";
            surf.transform.SetParent(gate, true);
            surf.transform.position = center + new Vector3(0f, h * 0.5f, 0.05f);
            surf.transform.rotation = Quaternion.identity;
            surf.transform.localScale = new Vector3(w, h, 1f);
            Object.DestroyImmediate(surf.GetComponent<Collider>());
            var r = surf.GetComponent<Renderer>();
            r.sharedMaterial = PortalMaterial(w / h);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            // ── 빨려 드는 빛 ──
            var motes = Particles(gate, "PortalMotes", surf.transform.position + new Vector3(0f, 0f, -0.08f));
            {
                var ps = motes.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.45f, 1f), new Color(1f, 0.7f, 1f));
                main.maxParticles = 160;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = ps.emission; em.rateOverTime = 45f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = Mathf.Max(w, h) * 0.55f;
                sh.radiusThickness = 0.15f;              // 가장자리에서만 태어난다
                sh.rotation = Vector3.zero;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.radial = new ParticleSystem.MinMaxCurve(-0.9f);          // 가운데로
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.6f);         // 돌면서
                vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
                FadeInOut(ps);
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
                // 원 모양 방출을 문 면(XY)에 눕힌다
                motes.transform.rotation = Quaternion.identity;
                var shape = ps.shape; shape.rotation = new Vector3(0f, 0f, 0f);
                motes.transform.localScale = new Vector3(1f, h / Mathf.Max(w, h) * 1.15f, 1f);
            }

            // ── 피어오르는 불티 ──
            var embers = Particles(gate, "PortalEmbers", center + new Vector3(0f, 0.1f, -0.6f));
            {
                var ps = embers.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.35f, 1f), new Color(1f, 0.5f, 0.9f));
                main.maxParticles = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 14f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(w * 1.1f, 0.05f, 1.2f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.8f;
                FadeInOut(ps);
            }

            // ── 바닥 빛 ──
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "PortalFloor";
            floor.transform.SetParent(gate, true);
            floor.transform.position = center + new Vector3(0f, 0.035f, -0.9f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(w * 1.9f, 2.6f, 1f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var fr = floor.GetComponent<Renderer>();
            fr.sharedMaterial = Additive("PortalFloor", GlowTexture("PortalFloorGlow", 256, 1.6f), new Color(0.75f, 0.35f, 1f, 0.7f));
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ── 바닥 마법진 ──
            // 문 바로 앞, 플레이어가 밟고 지나가는 자리. 카메라가 비스듬해서 타원으로 보인다.
            Renderer runeR = null;
            var runeTex = RuneTexture();
            if (runeTex != null)
            {
                var rune = GameObject.CreatePrimitive(PrimitiveType.Quad);
                rune.name = "PortalRune";
                rune.transform.SetParent(gate, true);
                rune.transform.position = center + new Vector3(0f, 0.05f, -1.75f);
                rune.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                float d = w * 1.15f;
                rune.transform.localScale = new Vector3(d, d, 1f);
                Object.DestroyImmediate(rune.GetComponent<Collider>());
                runeR = rune.GetComponent<Renderer>();
                runeR.sharedMaterial = Additive("PortalRune", runeTex, new Color(1.2f, 0.8f, 1.4f, 1f));
                runeR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                runeR.receiveShadows = false;
            }

            // 예전 회전판은 없어졌다 — 문은 셰이더가 스스로 돈다
            var vg = gate.GetComponentInChildren<VillageGate>(true);
            if (vg != null)
            {
                var so = new SerializedObject(vg);
                so.FindProperty("swirl").objectReferenceValue = null;
                so.FindProperty("glowBase").floatValue = 2.8f;
                so.FindProperty("surface").objectReferenceValue = r;
                so.FindProperty("floorGlow").objectReferenceValue = fr;
                so.FindProperty("rune").objectReferenceValue = runeR;
                var lbl = gate.Find("GateLabel");
                so.FindProperty("label").objectReferenceValue = lbl != null ? lbl.GetComponent<TMPro.TMP_Text>() : null;
                var parts = so.FindProperty("particles");
                parts.arraySize = 2;
                parts.GetArrayElementAtIndex(0).objectReferenceValue = motes.GetComponent<ParticleSystem>();
                parts.GetArrayElementAtIndex(1).objectReferenceValue = embers.GetComponent<ParticleSystem>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var light = gate.Find("PortalLight");
            if (light != null) { var l = light.GetComponent<Light>(); l.color = new Color(0.72f, 0.42f, 1f); l.range = 8f; }
        }

        /// <summary>
        /// 마을 전체에 흩날리는 벚꽃잎. 꽃잎은 빛나지 않으므로 더하기가 아니라 반투명으로 그린다.
        /// 사각 메시로 그려서 3축으로 뒤집히며 떨어진다 — 빌보드는 늘 정면이라 팔랑거림이 안 보인다.
        /// </summary>
        public static void ApplyPetals(Transform world)
        {
            if (world == null) return;
            var old = world.Find("SakuraPetals");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var tex = PetalTexture();
            if (tex == null) { Debug.LogWarning("[Proto] Petal.png 없음 — 꽃잎은 건너뛴다"); return; }

            var gate = world.Find("DungeonGate/PortalSurface");
            float cx = gate != null ? gate.position.x : 0f;

            var go = new GameObject("SakuraPetals");
            go.transform.SetParent(world, false);
            go.transform.position = new Vector3(cx, 9f, -3.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(11f, 15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.92f), new Color(1f, 0.97f, 0.98f));
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(30f, 0.5f, 16f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);    // 바람 — 왼쪽에서 오른쪽으로
            vel.y = new ParticleSystem.MinMaxCurve(-0.85f, -0.55f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var noise = ps.noise;
            noise.enabled = true; noise.strength = 0.55f; noise.frequency = 0.35f; noise.scrollSpeed = 0.25f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            rot.y = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

            // 땅에 닿을 즈음 사라진다 (높이 9m에서 초당 0.7m → 약 13초)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            pr.alignment = ParticleSystemRenderSpace.World;
            pr.sharedMaterial = PetalMaterial(tex);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
        }

        static Material PetalMaterial(Texture2D tex)
        {
            string path = Mat + "SakuraPetal.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1f, 0.92f, 0.95f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);   // Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);    // 뒤집혀도 보이게
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        const string Tex = "Assets/_Project/Textures/";

        static Texture2D RuneTexture() => Imported(Tex + "RuneCircle.png", true);
        static Texture2D PetalTexture() => Imported(Tex + "Petal.png", true);

        /// <summary>보내 주신 텍스처. 가장자리 반복 없이, 밉맵은 켠다 (비스듬히 보면 지글거린다).</summary>
        static Texture2D Imported(string path, bool alpha)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            if (imp.wrapMode != TextureWrapMode.Clamp || !imp.mipmapEnabled || imp.alphaIsTransparency != alpha)
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.alphaIsTransparency = alpha;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ──────────────────────────────────────────────────────────

        static GameObject Particles(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            // 파티클 색은 1을 못 넘는다 — 재질 쪽에서 밝혀 블룸이 걸리게 한다
            pr.sharedMaterial = Additive("PortalMote", GlowTexture("SoftDot", 64, 2.2f), new Color(2.4f, 2.0f, 2.6f, 1f));
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void FadeInOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static Material PortalMaterial(float aspect)
        {
            string path = Mat + "Portal.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Proto/Portal");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            // 다시 빌드할 때마다 셰이더 기본값으로 돌린다 (손으로 맞춘 값은 이 줄을 지우면 남는다)
            foreach (var p in new[] { "_ColorDeep", "_ColorMid", "_ColorCore", "_RimColor" })
                m.SetColor(p, sh.GetPropertyDefaultVectorValue(sh.FindPropertyIndex(p)));
            foreach (var p in new[] { "_Intensity", "_Speed", "_Twist", "_NoiseScale", "_Stars" })
                m.SetFloat(p, sh.GetPropertyDefaultFloatValue(sh.FindPropertyIndex(p)));
            m.SetFloat("_Aspect", aspect);
            // 보내 주신 소용돌이 텍스처가 있으면 쓴다
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PortalSwirl.png");
            m.SetTexture("_SwirlTex", tex);
            m.SetFloat("_UseTex", tex != null ? 1f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Additive(string name, Texture2D tex, Color tint)
        {
            string path = Mat + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);   // Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>가운데가 밝고 바깥으로 부드럽게 사라지는 원. falloff가 클수록 가운데로 모인다.</summary>
        static Texture2D GlowTexture(string name, int size, float falloff)
        {
            string path = Spr + name + ".png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), falloff);
                byte b = (byte)(a * 255);
                px[y * size + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            System.IO.Directory.CreateDirectory(Spr);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
#endif
