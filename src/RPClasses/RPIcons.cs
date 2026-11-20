using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoarstalsRPClasses
{
    public static class RPIcons
    {
        private static readonly Dictionary<RPClassType, Sprite> s_classIcons = new Dictionary<RPClassType, Sprite>();
        private static Sprite s_ineptitudeIcon;
        private static Sprite s_unwaveringIcon;
        private static Sprite s_braveHeartIcon;
        private static Sprite s_shadowAscensionIcon;
        private static Sprite s_falconEyeIcon;
        private static Sprite s_archmageIcon;
        private static Sprite s_rebirthScrollIcon;
        private static Sprite s_hudArmorShieldIcon;
        private static Sprite s_hudWarriorSwordIcon;
        private static Sprite s_hudHunterArrowIcon;
        private static Sprite s_hudHunterEmpoweredArrowIcon;
        private static Sprite s_hudSorcererStaffIcon;
        private static Sprite s_hudSorcererFreeCastIcon;
        private static Sprite s_hudThiefSunIcon;
        private static Sprite s_hudThiefMoonIcon;

        public static Sprite GetClassIcon(RPClassType cls)
        {
            Sprite sp;
            if (s_classIcons.TryGetValue(cls, out sp) && sp != null)
            {
                return sp;
            }
            sp = CreateClassEmblem(cls);
            s_classIcons[cls] = sp;
            return sp;
        }

        public static Sprite GetIneptitudeIcon()
        {
            if (s_ineptitudeIcon == null)
            {
                s_ineptitudeIcon = CreateIneptitudeEmblem();
            }
            return s_ineptitudeIcon;
        }

        public static Sprite GetUnwaveringIcon()
        {
            if (s_unwaveringIcon == null)
            {
                s_unwaveringIcon = CreateUnwaveringEmblem();
            }
            return s_unwaveringIcon;
        }

        public static Sprite GetBraveHeartIcon()
        {
            if (s_braveHeartIcon == null)
            {
                s_braveHeartIcon = CreateBraveHeartEmblem();
            }
            return s_braveHeartIcon;
        }

        public static Sprite GetShadowAscensionIcon()
        {
            if (s_shadowAscensionIcon == null)
            {
                s_shadowAscensionIcon = CreateShadowAscensionEmblem();
            }
            return s_shadowAscensionIcon;
        }

        public static Sprite GetFalconEyeIcon()
        {
            if (s_falconEyeIcon == null)
            {
                s_falconEyeIcon = CreateFalconEyeEmblem();
            }
            return s_falconEyeIcon;
        }

        public static Sprite GetArchmageIcon()
        {
            if (s_archmageIcon == null)
            {
                s_archmageIcon = CreateArchmageEmblem();
            }
            return s_archmageIcon;
        }

        public static Sprite GetRebirthScrollIcon()
        {
            if (s_rebirthScrollIcon == null)
            {
                s_rebirthScrollIcon = CreateScrollEmblem();
            }
            return s_rebirthScrollIcon;
        }

        public static Sprite GetHudArmorShieldIcon()
        {
            if (s_hudArmorShieldIcon == null)
            {
                s_hudArmorShieldIcon = CreateHudArmorShieldSprite();
            }
            return s_hudArmorShieldIcon;
        }

        public static Sprite GetHudWarriorSwordIcon()
        {
            if (s_hudWarriorSwordIcon == null)
            {
                s_hudWarriorSwordIcon = CreateHudWarriorSwordSprite();
            }
            return s_hudWarriorSwordIcon;
        }

        public static Sprite GetHudHunterArrowIcon(bool empowered)
        {
            if (empowered)
            {
                if (s_hudHunterEmpoweredArrowIcon == null)
                {
                    s_hudHunterEmpoweredArrowIcon = CreateHudHunterArrowSprite(true);
                }
                return s_hudHunterEmpoweredArrowIcon;
            }
            if (s_hudHunterArrowIcon == null)
            {
                s_hudHunterArrowIcon = CreateHudHunterArrowSprite(false);
            }
            return s_hudHunterArrowIcon;
        }

        public static Sprite GetHudSorcererCastIcon(bool freeReady)
        {
            if (freeReady)
            {
                if (s_hudSorcererFreeCastIcon == null)
                {
                    s_hudSorcererFreeCastIcon = CreateHudSorcererCastSprite(true);
                }
                return s_hudSorcererFreeCastIcon;
            }
            if (s_hudSorcererStaffIcon == null)
            {
                s_hudSorcererStaffIcon = CreateHudSorcererCastSprite(false);
            }
            return s_hudSorcererStaffIcon;
        }

        public static Sprite GetHudThiefDayNightIcon(bool isNight)
        {
            if (isNight)
            {
                if (s_hudThiefMoonIcon == null)
                {
                    s_hudThiefMoonIcon = CreateHudThiefMoonSprite();
                }
                return s_hudThiefMoonIcon;
            }
            if (s_hudThiefSunIcon == null)
            {
                s_hudThiefSunIcon = CreateHudThiefSunSprite();
            }
            return s_hudThiefSunIcon;
        }

        private static Sprite CreateClassEmblem(RPClassType cls)
        {
            const int size = 256;
            const float k = 4f; // 4x resolution multiplier over 64x64 coordinates for native 4K crispness!
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "RPClassIcon_HD_" + cls;
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color accent = RPClassConfig.GetClassColor(cls);
            Color bgDark = new Color(0.10f, 0.08f, 0.07f, 0.96f);
            Color gold = new Color(0.88f, 0.70f, 0.34f, 1.0f);

            // Base circular Norse medallion with smooth SDF gold rim & colored inner glow
            Vector2 center = new Vector2(127.5f, 127.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 119f)
                    {
                        px[y * size + x] = Color.clear;
                    }
                    else if (dist > 105f)
                    {
                        float edgeAlpha = SmoothStep(119f, 115.5f, dist);
                        Color rim = Color.Lerp(gold * 0.68f, gold, Mathf.Clamp01(1f - Mathf.Abs(dist - 112f) / 7f));
                        rim.a = edgeAlpha;
                        px[y * size + x] = rim;
                    }
                    else
                    {
                        float radial = Mathf.Clamp01(1f - dist / 105f);
                        Color inner = Color.Lerp(bgDark, accent * 0.44f, radial * radial);
                        inner.a = 0.97f;
                        px[y * size + x] = inner;
                    }
                }
            }

            Color bright = Color.Lerp(accent, Color.white, 0.45f);
            bright.a = 1f;
            Color mid = accent;
            mid.a = 1f;

            switch (cls)
            {
                case RPClassType.Warrior:
                    DrawLine(px, size, 16 * k, 16 * k, 47 * k, 47 * k, 3.2f * k, bright);
                    DrawLine(px, size, 19 * k, 31 * k, 31 * k, 19 * k, 2.5f * k, gold);
                    DrawCircleFill(px, size, 15 * k, 15 * k, 3.0f * k, gold);
                    DrawLine(px, size, 47 * k, 16 * k, 16 * k, 47 * k, 2.2f * k, mid);
                    DrawLine(px, size, 33 * k, 19 * k, 45 * k, 31 * k, 2.3f * k, gold);
                    break;

                case RPClassType.Knight:
                    for (int y = 56; y <= 196; y++)
                    {
                        float t = (y - 56f) / 140f;
                        float halfW = Mathf.Lerp(8f, 60f, Mathf.Pow(t, 0.55f));
                        for (int x = 64; x <= 192; x++)
                        {
                            float dx = Mathf.Abs(x - 127.5f);
                            if (dx <= halfW + 1.2f)
                            {
                                float aa = SmoothStep(halfW + 1.2f, halfW - 1.2f, dx);
                                bool border = dx > halfW - 9.2f || y >= 188;
                                Color c = border ? gold : mid;
                                c.a *= aa;
                                BlendPixel(px, size, x, y, c);
                            }
                        }
                    }
                    DrawLine(px, size, 127.5f, 18 * k, 127.5f, 46 * k, 2.2f * k, bright);
                    DrawLine(px, size, 21 * k, 35 * k, 42 * k, 35 * k, 2.2f * k, bright);
                    break;

                case RPClassType.Thief:
                    DrawLine(px, size, 15 * k, 18 * k, 45 * k, 46 * k, 2.3f * k, bright);
                    DrawLine(px, size, 18 * k, 28 * k, 27 * k, 19 * k, 2.0f * k, gold);
                    DrawLine(px, size, 49 * k, 18 * k, 19 * k, 46 * k, 2.3f * k, mid);
                    DrawLine(px, size, 37 * k, 19 * k, 46 * k, 28 * k, 2.0f * k, gold);
                    DrawCircleFill(px, size, 32f * k, 49f * k, 3.2f * k, bright);
                    break;

                case RPClassType.Hunter:
                    for (int a = -130; a <= 130; a++)
                    {
                        float rad = (a * 0.5f) * Mathf.Deg2Rad;
                        float bx = (24f + Mathf.Cos(rad) * 18f) * k;
                        float by = (32f + Mathf.Sin(rad) * 19f) * k;
                        DrawCircleFill(px, size, bx, by, 1.8f * k, gold);
                    }
                    DrawLine(px, size, 31.5f * k, 15f * k, 20f * k, 32f * k, 1.1f * k, Color.white);
                    DrawLine(px, size, 20f * k, 32f * k, 31.5f * k, 49f * k, 1.1f * k, Color.white);
                    DrawLine(px, size, 18f * k, 32f * k, 49f * k, 32f * k, 1.8f * k, bright);
                    DrawLine(px, size, 49f * k, 32f * k, 42f * k, 37f * k, 2.0f * k, bright);
                    DrawLine(px, size, 49f * k, 32f * k, 42f * k, 27f * k, 2.0f * k, bright);
                    break;

                case RPClassType.Sorcerer:
                    DrawLine(px, size, 18 * k, 14 * k, 38 * k, 42 * k, 2.5f * k, gold);
                    DrawCircleFill(px, size, 41f * k, 45f * k, 7.5f * k, new Color(accent.r, accent.g, accent.b, 0.45f));
                    DrawCircleFill(px, size, 41f * k, 45f * k, 4.5f * k, bright);
                    DrawCircleFill(px, size, 41f * k, 45f * k, 2.2f * k, Color.white);
                    DrawCircleFill(px, size, 28f * k, 46f * k, 1.8f * k, bright);
                    DrawCircleFill(px, size, 47f * k, 33f * k, 1.8f * k, bright);
                    break;

                case RPClassType.Beggar:
                    for (int y = 56; y <= 160; y++)
                    {
                        float ny = (y - 104f) / 56f;
                        float halfW = 58f * Mathf.Sqrt(Mathf.Clamp01(1f - ny * ny * 0.75f));
                        if (y > 140) halfW *= 0.65f;
                        for (int x = 60; x <= 196; x++)
                        {
                            float dx = Mathf.Abs(x - 127.5f);
                            if (dx <= halfW + 1.2f)
                            {
                                float aa = SmoothStep(halfW + 1.2f, halfW - 1.2f, dx);
                                bool rim = dx > halfW - 8.0f || y <= 59;
                                Color c = rim ? gold : mid;
                                c.a *= aa;
                                BlendPixel(px, size, x, y, c);
                            }
                        }
                    }
                    DrawLine(px, size, 24 * k, 38 * k, 39 * k, 38 * k, 2.2f * k, bright);
                    DrawLine(px, size, 26 * k, 40 * k, 22 * k, 48 * k, 2.5f * k, mid);
                    DrawLine(px, size, 31.5f * k, 40 * k, 31.5f * k, 49 * k, 2.5f * k, mid);
                    DrawLine(px, size, 37 * k, 40 * k, 41 * k, 48 * k, 2.5f * k, mid);
                    break;
            }

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateShadowAscensionEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_ShadowAscension";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color violet = new Color(0.68f, 0.36f, 0.96f, 1f);
            Color paleLilac = new Color(0.90f, 0.78f, 1.0f, 1f);
            Color dark = new Color(0.08f, 0.05f, 0.14f, 0.96f);
            Color gold = new Color(0.88f, 0.72f, 0.40f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 25.8f) px[y * size + x] = gold;
                    else px[y * size + x] = Color.Lerp(dark, violet * 0.42f, 1f - dist / 25.8f);
                }
            }

            // Crescent Moon + Ascending Shadow Blade
            DrawCircleFill(px, size, 30f, 32f, 14.5f, violet);
            DrawCircleFill(px, size, 36.5f, 34f, 12.2f, dark);
            DrawLine(px, size, 34f, 15f, 34f, 49f, 2.1f, paleLilac);
            DrawLine(px, size, 28f, 23f, 40f, 23f, 1.8f, gold);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateFalconEyeEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_FalconEye";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color emerald = new Color(0.36f, 0.88f, 0.44f, 1f);
            Color bright = new Color(0.85f, 1.0f, 0.82f, 1f);
            Color dark = new Color(0.06f, 0.13f, 0.08f, 0.96f);
            Color gold = new Color(0.92f, 0.78f, 0.36f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 25.8f) px[y * size + x] = gold;
                    else px[y * size + x] = Color.Lerp(dark, emerald * 0.42f, 1f - dist / 25.8f);
                }
            }

            // Swift Wing + Piercing Bolt/Arrow
            DrawLine(px, size, 15f, 20f, 48f, 45f, 2.4f, bright);
            DrawLine(px, size, 48f, 45f, 39f, 45f, 2.2f, gold);
            DrawLine(px, size, 48f, 45f, 46f, 36f, 2.2f, gold);
            // Sweeping falcon wing feathers
            DrawLine(px, size, 16f, 35f, 34f, 46f, 2.0f, emerald);
            DrawLine(px, size, 18f, 28f, 30f, 38f, 1.8f, emerald);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateArchmageEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_Archmage";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color cyan = new Color(0.28f, 0.78f, 1.0f, 1f);
            Color whiteGlow = new Color(0.88f, 0.98f, 1.0f, 1f);
            Color dark = new Color(0.05f, 0.10f, 0.18f, 0.96f);
            Color gold = new Color(0.94f, 0.78f, 0.36f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 25.8f) px[y * size + x] = gold;
                    else px[y * size + x] = Color.Lerp(dark, cyan * 0.45f, 1f - dist / 25.8f);
                }
            }

            // Arcane Diamond Rune + Radiant Eitr Core
            DrawLine(px, size, 31.5f, 49f, 46f, 31.5f, 2.2f, cyan);
            DrawLine(px, size, 46f, 31.5f, 31.5f, 14f, 2.2f, cyan);
            DrawLine(px, size, 31.5f, 14f, 17f, 31.5f, 2.2f, cyan);
            DrawLine(px, size, 17f, 31.5f, 31.5f, 49f, 2.2f, cyan);
            DrawCircleFill(px, size, 31.5f, 31.5f, 6.2f, cyan);
            DrawCircleFill(px, size, 31.5f, 31.5f, 3.2f, whiteGlow);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateBraveHeartEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_BraveHeart";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color crimson = new Color(0.92f, 0.22f, 0.18f, 1f);
            Color ember = new Color(1.0f, 0.56f, 0.20f, 1f);
            Color dark = new Color(0.14f, 0.06f, 0.06f, 0.96f);
            Color gold = new Color(0.94f, 0.76f, 0.34f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 25.8f) px[y * size + x] = gold;
                    else px[y * size + x] = Color.Lerp(dark, crimson * 0.44f, 1f - dist / 25.8f);
                }
            }

            // Blazing Norse Heart (two upper lobes + pointed bottom + inner flame core)
            DrawCircleFill(px, size, 25f, 36f, 8.2f, crimson);
            DrawCircleFill(px, size, 38f, 36f, 8.2f, crimson);
            for (int y = 15; y <= 35; y++)
            {
                float t = (y - 15f) / 20f; // 0 tip -> 1 wide middle
                float halfW = Mathf.Lerp(0.8f, 14.5f, t);
                for (int x = 16; x <= 47; x++)
                {
                    if (Mathf.Abs(x - 31.5f) <= halfW)
                    {
                        BlendPixel(px, size, x, y, crimson);
                    }
                }
            }

            // Glowing inner flame core
            DrawCircleFill(px, size, 31.5f, 31f, 5.5f, ember);
            DrawLine(px, size, 31.5f, 24f, 31.5f, 45f, 2.0f, new Color(1f, 0.90f, 0.55f, 1f));

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateUnwaveringEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_Unwavering";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color steel = new Color(0.62f, 0.78f, 0.96f, 1f);
            Color dark = new Color(0.08f, 0.11f, 0.16f, 0.96f);
            Color gold = new Color(0.92f, 0.76f, 0.36f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 25.8f) px[y * size + x] = gold;
                    else px[y * size + x] = Color.Lerp(dark, steel * 0.42f, 1f - dist / 25.8f);
                }
            }

            // Fortress Bastion / Iron Bulwark Pillar with Winged Shield Crest
            Color brightSteel = new Color(0.86f, 0.94f, 1.0f, 1f);
            for (int y = 16; y <= 46; y++)
            {
                float halfW = (y >= 40) ? 14f : 10.5f;
                if (y >= 43 && ((xMod(y) % 2) == 0)) { }
                for (int x = 16; x <= 47; x++)
                {
                    float dx = Mathf.Abs(x - 31.5f);
                    if (dx <= halfW)
                    {
                        // Battlement notches at very top
                        if (y >= 43 && (dx > 3.5f && dx < 8.5f)) continue;
                        bool edge = dx > halfW - 2.0f || y == 16;
                        BlendPixel(px, size, x, y, edge ? gold : steel);
                    }
                }
            }
            DrawLine(px, size, 31.5f, 20f, 31.5f, 38f, 2.0f, brightSteel);
            DrawCircleFill(px, size, 31.5f, 29f, 3.6f, gold);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static int xMod(int v) { return v; }

        private static Sprite CreateIneptitudeEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_Ineptitude";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Color rust = new Color(0.78f, 0.34f, 0.26f, 1f);
            Color dark = new Color(0.12f, 0.08f, 0.08f, 0.95f);
            Color rimCol = new Color(0.58f, 0.26f, 0.22f, 1f);

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > 29f) px[y * size + x] = Color.clear;
                    else if (dist > 26f) px[y * size + x] = rimCol;
                    else px[y * size + x] = Color.Lerp(dark, rust * 0.35f, 1f - dist / 26f);
                }
            }

            // Cracked Rune / Downward XP Arrow
            Color lightRust = new Color(0.96f, 0.52f, 0.42f, 1f);
            DrawLine(px, size, 31.5f, 48f, 31.5f, 18f, 3.0f, lightRust);
            DrawLine(px, size, 20f, 29f, 31.5f, 16f, 3.0f, lightRust);
            DrawLine(px, size, 43f, 29f, 31.5f, 16f, 3.0f, lightRust);
            // Crack slash across
            DrawLine(px, size, 19f, 43f, 44f, 35f, 1.8f, rimCol);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudThiefSunSprite()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "RPIcon_HudThiefSun_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color outline = new Color(0.10f, 0.07f, 0.04f, 0.96f);
            Color sunGold = new Color(1.00f, 0.78f, 0.24f, 1.00f);
            Color sunCore = new Color(1.00f, 0.94f, 0.68f, 1.00f);
            Vector2 c = new Vector2(127.5f, 127.5f);

            // 8 crisp Nordic sun rays (alternating long & short)
            for (int r = 0; r < 8; r++)
            {
                float ang = (r * 45f) * Mathf.Deg2Rad;
                float dirX = Mathf.Cos(ang);
                float dirY = Mathf.Sin(ang);
                float len = (r % 2 == 0) ? 104f : 88f;
                DrawLine(px, size, c.x + dirX * 52f, c.y + dirY * 52f, c.x + dirX * len, c.y + dirY * len, 11.5f, outline);
            }
            for (int r = 0; r < 8; r++)
            {
                float ang = (r * 45f) * Mathf.Deg2Rad;
                float dirX = Mathf.Cos(ang);
                float dirY = Mathf.Sin(ang);
                float len = (r % 2 == 0) ? 102f : 86f;
                DrawLine(px, size, c.x + dirX * 52f, c.y + dirY * 52f, c.x + dirX * len, c.y + dirY * len, 6.8f, sunGold);
            }

            // Sun disc
            DrawCircleFill(px, size, c.x, c.y, 56f, outline);
            DrawCircleFill(px, size, c.x, c.y, 48f, sunGold);
            DrawCircleFill(px, size, c.x, c.y, 28f, sunCore);

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudThiefMoonSprite()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "RPIcon_HudThiefMoon_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            Vector2 outerC = new Vector2(124f, 128f);
            Vector2 cutC = new Vector2(162f, 146f);
            const float outerR = 88f;
            const float cutR = 74f;

            Color auraViolet = new Color(0.62f, 0.36f, 0.98f, 0.45f);
            Color outline = new Color(0.06f, 0.04f, 0.12f, 0.98f);
            Color moonSilver = new Color(0.84f, 0.78f, 1.00f, 1.00f);
            Color moonBright = new Color(0.96f, 0.95f, 1.00f, 1.00f);

            // Smooth SDF Crescent Moon with night-shadow violet aura!
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dOuter = Vector2.Distance(p, outerC) - outerR;
                    float dCut = cutR - Vector2.Distance(p, cutC);
                    // Signed distance inside crescent: negative = inside crescent!
                    float sd = Mathf.Max(dOuter, dCut);

                    if (sd < 18f)
                    {
                        Color col = Color.clear;
                        if (sd > 5.5f)
                        {
                            // Soft mystical night glow around crescent
                            float glow = 1f - SmoothStep(5.5f, 18f, sd);
                            col = auraViolet;
                            col.a *= glow;
                        }
                        else if (sd > -1.2f)
                        {
                            float edgeAlpha = 1f - SmoothStep(4.0f, 5.8f, sd);
                            col = Color.Lerp(outline, auraViolet, SmoothStep(3.5f, 5.8f, sd));
                            col.a = Mathf.Max(col.a, edgeAlpha * 0.98f);
                        }
                        else if (sd > -7.5f)
                        {
                            col = outline;
                        }
                        else
                        {
                            float innerMix = SmoothStep(-7.5f, -10.5f, sd);
                            float grad = Mathf.Clamp01((y - 50f) / 150f);
                            Color body = Color.Lerp(moonSilver, moonBright, grad);
                            col = Color.Lerp(outline, body, innerMix);
                        }
                        px[y * size + x] = col;
                    }
                    else
                    {
                        px[y * size + x] = Color.clear;
                    }
                }
            }

            // Two crisp twinkling night stars beside the crescent!
            DrawCircleFill(px, size, 190f, 186f, 10.5f, outline);
            DrawCircleFill(px, size, 190f, 186f, 6.5f, moonBright);

            DrawCircleFill(px, size, 162f, 214f, 8.0f, outline);
            DrawCircleFill(px, size, 162f, 214f, 4.5f, moonSilver);

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudSorcererCastSprite(bool freeReady)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = freeReady ? "RPIcon_HudSorcererCast_Free_HD" : "RPIcon_HudSorcererCast_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color outline = new Color(0.05f, 0.07f, 0.12f, 0.98f);
            Color goldStaff = new Color(0.90f, 0.72f, 0.34f, 1.00f);
            Color cyanOrb = new Color(0.32f, 0.82f, 1.00f, 1.00f);

            if (freeReady)
            {
                Color brightAura = new Color(0.28f, 0.92f, 1.00f, 0.62f);
                DrawCircleFill(px, size, 184f, 184f, 54f, brightAura);
            }

            // Diagonal Mage Staff + Crystal Eitr Head
            DrawLine(px, size, 40f, 40f, 174f, 174f, 12.5f, outline);
            DrawLine(px, size, 42f, 42f, 174f, 174f, 7.5f, goldStaff);

            DrawCircleFill(px, size, 186f, 186f, 31f, outline);
            DrawCircleFill(px, size, 186f, 186f, 24f, freeReady ? new Color(1.0f, 0.88f, 0.36f, 1f) : cyanOrb);
            DrawCircleFill(px, size, 186f, 186f, 12f, Color.white);

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudWarriorSwordSprite()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "RPIcon_HudWarriorSword_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color outline = new Color(0.06f, 0.05f, 0.05f, 0.98f);
            Color bladeLight = new Color(0.95f, 0.97f, 1.00f, 1.00f);
            Color bladeMid = new Color(0.72f, 0.77f, 0.85f, 1.00f);
            Color crimsonEdge = new Color(0.96f, 0.30f, 0.24f, 0.92f);
            Color goldGuard = new Color(0.95f, 0.78f, 0.34f, 1.00f);
            Color gripLeather = new Color(0.42f, 0.22f, 0.14f, 1.00f);

            // Diagonal Viking Broadsword (bottom-left pommel -> top-right tip)
            // Soft dark outline pass first
            DrawLine(px, size, 38f, 38f, 76f, 76f, 12f, outline);
            DrawLine(px, size, 52f, 108f, 108f, 52f, 13f, outline);
            DrawLine(px, size, 80f, 80f, 222f, 222f, 18f, outline);
            DrawCircleFill(px, size, 34f, 34f, 15f, outline);

            // Crimson battle-aura edge along blade
            DrawLine(px, size, 82f, 82f, 220f, 220f, 13.5f, crimsonEdge);

            // Steel blade body + bright fuller highlight
            DrawLine(px, size, 80f, 80f, 218f, 218f, 10.5f, bladeMid);
            DrawLine(px, size, 78f, 82f, 216f, 220f, 4.5f, bladeLight);

            // Hilt grip, crossguard & pommel
            DrawLine(px, size, 40f, 40f, 78f, 78f, 7.5f, gripLeather);
            DrawLine(px, size, 55f, 105f, 105f, 55f, 8.5f, goldGuard);
            DrawCircleFill(px, size, 34f, 34f, 10.5f, goldGuard);

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudHunterArrowSprite(bool empowered)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = empowered ? "RPIcon_HudHunterArrow_Empowered_HD" : "RPIcon_HudHunterArrow_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color outline = new Color(0.05f, 0.07f, 0.06f, 0.98f);

            if (empowered)
            {
                // Radiant Golden-Cyan Empowered x2 Broadhead Arrow with glowing aura & twin energy barbs!
                Color outerAura = new Color(0.25f, 0.92f, 1.00f, 0.55f);
                Color innerGold = new Color(1.00f, 0.86f, 0.28f, 1.00f);
                Color brightCore = new Color(1.00f, 0.98f, 0.85f, 1.00f);

                // Radiant aura halo
                DrawLine(px, size, 36f, 36f, 222f, 222f, 24f, outerAura);
                DrawCircleFill(px, size, 198f, 198f, 36f, outerAura);

                // Outline
                DrawLine(px, size, 38f, 38f, 212f, 212f, 11.5f, outline);
                DrawLine(px, size, 224f, 224f, 162f, 206f, 12f, outline);
                DrawLine(px, size, 224f, 224f, 206f, 162f, 12f, outline);
                // Second inner barb pair for fierce empowered silhouette!
                DrawLine(px, size, 194f, 194f, 144f, 180f, 10f, outline);
                DrawLine(px, size, 194f, 194f, 180f, 144f, 10f, outline);

                // Fletching feathers outline
                DrawLine(px, size, 58f, 58f, 24f, 48f, 9.5f, outline);
                DrawLine(px, size, 58f, 58f, 48f, 24f, 9.5f, outline);
                DrawLine(px, size, 78f, 78f, 44f, 68f, 9.5f, outline);
                DrawLine(px, size, 78f, 78f, 68f, 44f, 9.5f, outline);

                // Golden-Cyan Fill
                DrawLine(px, size, 58f, 58f, 26f, 49f, 5.8f, new Color(0.45f, 0.96f, 1.0f, 1f));
                DrawLine(px, size, 58f, 58f, 49f, 26f, 5.8f, new Color(0.45f, 0.96f, 1.0f, 1f));
                DrawLine(px, size, 78f, 78f, 46f, 69f, 5.8f, innerGold);
                DrawLine(px, size, 78f, 78f, 69f, 46f, 5.8f, innerGold);

                DrawLine(px, size, 40f, 40f, 216f, 216f, 7.0f, innerGold);
                DrawLine(px, size, 194f, 194f, 146f, 180f, 6.2f, new Color(0.45f, 0.96f, 1.0f, 1f));
                DrawLine(px, size, 194f, 194f, 180f, 146f, 6.2f, new Color(0.45f, 0.96f, 1.0f, 1f));
                DrawLine(px, size, 224f, 224f, 165f, 205f, 7.8f, innerGold);
                DrawLine(px, size, 224f, 224f, 205f, 165f, 7.8f, innerGold);
                DrawLine(px, size, 50f, 50f, 220f, 220f, 3.0f, brightCore);
            }
            else
            {
                // Crisp Ranger Hunting Arrow (wood shaft, emerald fletching, steel broadhead)
                Color woodShaft = new Color(0.76f, 0.56f, 0.34f, 1.00f);
                Color featherGreen = new Color(0.42f, 0.88f, 0.48f, 1.00f);
                Color steelHead = new Color(0.88f, 0.93f, 0.98f, 1.00f);

                // Outlines
                DrawLine(px, size, 40f, 40f, 210f, 210f, 10.5f, outline);
                DrawLine(px, size, 220f, 220f, 168f, 204f, 11f, outline);
                DrawLine(px, size, 220f, 220f, 204f, 168f, 11f, outline);
                DrawLine(px, size, 62f, 62f, 28f, 52f, 9.5f, outline);
                DrawLine(px, size, 62f, 62f, 52f, 28f, 9.5f, outline);
                DrawLine(px, size, 82f, 82f, 48f, 72f, 9.5f, outline);
                DrawLine(px, size, 82f, 82f, 72f, 48f, 9.5f, outline);

                // Fletching feathers
                DrawLine(px, size, 62f, 62f, 30f, 53f, 5.5f, featherGreen);
                DrawLine(px, size, 62f, 62f, 53f, 30f, 5.5f, featherGreen);
                DrawLine(px, size, 82f, 82f, 50f, 73f, 5.5f, featherGreen);
                DrawLine(px, size, 82f, 82f, 73f, 50f, 5.5f, featherGreen);

                // Wood shaft + Steel arrowhead
                DrawLine(px, size, 42f, 42f, 208f, 208f, 6.0f, woodShaft);
                DrawLine(px, size, 220f, 220f, 170f, 203f, 7.0f, steelHead);
                DrawLine(px, size, 220f, 220f, 203f, 170f, 7.0f, steelHead);
                DrawLine(px, size, 185f, 185f, 220f, 220f, 6.5f, steelHead);
            }

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateHudArmorShieldSprite()
        {
            // High-DPI 256x256 smooth vector-SDF rendered shield with mipmaps & trilinear filtering — zero pixelation!
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "RPIcon_HudArmorShield_HD";
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];

            Color darkEdge = new Color(0.06f, 0.07f, 0.09f, 0.98f);
            Color rimLight = new Color(0.92f, 0.95f, 1.00f, 1.00f);
            Color rimMid = new Color(0.70f, 0.75f, 0.82f, 1.00f);
            Color rimShadow = new Color(0.48f, 0.52f, 0.60f, 1.00f);
            Color bodyTopLeft = new Color(0.34f, 0.39f, 0.46f, 0.97f);
            Color bodyBotRight = new Color(0.18f, 0.21f, 0.26f, 0.97f);
            Color goldRivet = new Color(0.96f, 0.82f, 0.44f, 1.00f);

            for (int y = 0; y < size; y++)
            {
                // Normalized Y in [0..1] across shield height (16..240)
                float ny = (y - 16f) / 224f;
                if (ny < -0.04f || ny > 1.04f)
                {
                    for (int x = 0; x < size; x++) px[y * size + x] = Color.clear;
                    continue;
                }

                float clampedY = Mathf.Clamp01(ny);
                // Smooth heater-shield half-width in pixels (max ~92 px at ny = 0.68)
                float halfW;
                if (clampedY < 0.66f)
                {
                    float u = clampedY / 0.66f;
                    halfW = Mathf.Lerp(2.0f, 92.0f, Mathf.Sin(u * Mathf.PI * 0.5f));
                }
                else
                {
                    float u = (clampedY - 0.66f) / 0.34f;
                    halfW = Mathf.Lerp(92.0f, 84.0f, u * u);
                }

                for (int x = 0; x < size; x++)
                {
                    float sx = x - 127.5f;
                    float dx = Mathf.Abs(sx);

                    // Gently arched/scalloped top crest
                    float topY = 236f - (1f - Mathf.Clamp01(dx / 82f)) * 9f;

                    // Approximate Euclidean signed distance inside shield boundary for razor-smooth AA
                    float sideSlope = (clampedY < 0.66f) ? Mathf.Lerp(1.8f, 1.02f, clampedY / 0.66f) : 1.02f;
                    float distSide = (halfW - dx) / sideSlope;
                    float distTop = topY - y;
                    float distBottom = y - 16f;
                    float d = Mathf.Min(distSide, Mathf.Min(distTop, distBottom));

                    if (d <= -1.5f)
                    {
                        px[y * size + x] = Color.clear;
                        continue;
                    }

                    // Smoothstep outer anti-aliasing over 2.5 high-res pixels
                    float outerAlpha = SmoothStep(-1.4f, 1.2f, d);

                    Color col;
                    if (d < 5.5f)
                    {
                        col = darkEdge;
                    }
                    else if (d < 21.5f)
                    {
                        // Smooth metallic beveled rim with directional sheen + soft AA transitions
                        float bevelGrad = Mathf.Clamp01((-sx / 90f) * 0.5f + (ny - 0.5f) * 0.6f + 0.5f);
                        Color rimCol = bevelGrad > 0.5f
                            ? Color.Lerp(rimMid, rimLight, (bevelGrad - 0.5f) * 2f)
                            : Color.Lerp(rimShadow, rimMid, bevelGrad * 2f);

                        float innerDarkMix = SmoothStep(5.0f, 7.2f, d) * (1f - SmoothStep(19.5f, 21.5f, d));
                        col = Color.Lerp(darkEdge, rimCol, innerDarkMix);
                    }
                    else if (d < 25.0f)
                    {
                        col = darkEdge;
                    }
                    else
                    {
                        // Smooth inner shield face with central ridge highlight & vertical shading
                        float faceShade = Mathf.Clamp01(ny * 0.55f + (sx < 0f ? 0.35f : 0.0f));
                        Color faceCol = Color.Lerp(bodyBotRight, bodyTopLeft, faceShade);

                        // Crisp anti-aliased central heraldic spine
                        float ridgeFactor = 1f - SmoothStep(0.8f, 3.6f, dx);
                        faceCol = Color.Lerp(faceCol, new Color(0.56f, 0.62f, 0.72f, 0.98f), ridgeFactor * 0.65f);

                        float innerEdgeMix = SmoothStep(24.5f, 26.8f, d);
                        col = Color.Lerp(darkEdge, faceCol, innerEdgeMix);
                    }

                    col.a *= outerAlpha;
                    px[y * size + x] = col;
                }
            }

            // Smooth high-res golden rivets on rim corners & bottom point
            DrawCircleFill(px, size, 64f, 210f, 5.5f, darkEdge);
            DrawCircleFill(px, size, 64f, 210f, 4.0f, goldRivet);

            DrawCircleFill(px, size, 191f, 210f, 5.5f, darkEdge);
            DrawCircleFill(px, size, 191f, 210f, 4.0f, goldRivet);

            DrawCircleFill(px, size, 127.5f, 42f, 5.5f, darkEdge);
            DrawCircleFill(px, size, 127.5f, 42f, 4.0f, goldRivet);

            tex.SetPixels(px);
            tex.Apply(true, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Sprite CreateScrollEmblem()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RPIcon_RebirthScroll";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            Color parchment = new Color(0.90f, 0.80f, 0.58f, 1f);
            Color parchmentEdge = new Color(0.68f, 0.54f, 0.34f, 1f);
            Color woodRod = new Color(0.45f, 0.26f, 0.14f, 1f);
            Color goldTip = new Color(0.96f, 0.78f, 0.28f, 1f);
            Color waxSeal = new Color(0.84f, 0.18f, 0.18f, 1f);

            // Rolled parchment body
            for (int y = 14; y <= 50; y++)
            {
                for (int x = 18; x <= 46; x++)
                {
                    bool edge = (x <= 19 || x >= 45);
                    px[y * size + x] = edge ? parchmentEdge : parchment;
                }
            }

            // Top & bottom scroll rolls
            DrawLine(px, size, 14, 50, 50, 50, 3.5f, parchmentEdge);
            DrawLine(px, size, 14, 14, 50, 14, 3.5f, parchmentEdge);
            DrawCircleFill(px, size, 12f, 50f, 3.2f, goldTip);
            DrawCircleFill(px, size, 52f, 50f, 3.2f, goldTip);
            DrawCircleFill(px, size, 12f, 14f, 3.2f, goldTip);
            DrawCircleFill(px, size, 52f, 14f, 3.2f, goldTip);

            // Rune script lines on scroll
            DrawLine(px, size, 23, 42, 41, 42, 1.4f, woodRod);
            DrawLine(px, size, 23, 36, 39, 36, 1.4f, woodRod);
            DrawLine(px, size, 23, 30, 41, 30, 1.4f, woodRod);

            // Hildir crimson & gold seal
            DrawCircleFill(px, size, 36f, 21f, 6.2f, waxSeal);
            DrawCircleFill(px, size, 36f, 21f, 3.2f, goldTip);

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DrawLine(Color[] px, int size, float x0, float y0, float x1, float y1, float radius, Color col)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - radius - 1));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - radius - 1));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + radius + 1));

            Vector2 a = new Vector2(x0, y0);
            Vector2 b = new Vector2(x1, y1);
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = abLenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq) : 0f;
                    float dist = Vector2.Distance(p, a + ab * t);
                    if (dist <= radius + 0.6f)
                    {
                        float alpha = Mathf.Clamp01(radius + 0.6f - dist);
                        Color c = col;
                        c.a *= alpha;
                        BlendPixel(px, size, x, y, c);
                    }
                }
            }
        }

        private static void DrawCircleFill(Color[] px, int size, float cx, float cy, float radius, Color col)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(cx + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(cy + radius + 1));
            Vector2 center = new Vector2(cx, cy);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius + 0.6f)
                    {
                        float alpha = Mathf.Clamp01(radius + 0.6f - dist);
                        Color c = col;
                        c.a *= alpha;
                        BlendPixel(px, size, x, y, c);
                    }
                }
            }
        }

        private static void BlendPixel(Color[] px, int size, int x, int y, Color src)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            Color dst = px[y * size + x];
            float outA = src.a + dst.a * (1f - src.a);
            if (outA <= 0.001f) return;
            Color res = (src * src.a + dst * dst.a * (1f - src.a)) / outA;
            res.a = outA;
            px[y * size + x] = res;
        }
    }
}
