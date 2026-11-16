#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using TMPro;

namespace SkyGate.EditorTools
{
    /// <summary>
    /// Generates stylized cartoon 2D UI sprites (wooden signpost, notched planks, stone logo crest,
    /// heralds banner, toadstool mushrooms, parchment modal) and TMP cartoon SDF font assets
    /// for the Main Menu overhaul.
    /// </summary>
    public static class SkyGateMenuArtGenerator
    {
        private const string UiDir = "Assets/Art/UI/MainMenu";

        public static void EnsureMenuArtGenerated(out TMP_FontAsset luckiestFont, out TMP_FontAsset fredokaFont, out Material menuParticleMat)
        {
            Directory.CreateDirectory(UiDir);

            GenerateWoodPostSprite($"{UiDir}/SPR_WoodPost.png");
            GenerateSignPlankSprite($"{UiDir}/SPR_SignPlank_Right.png", true);
            GenerateSignPlankSprite($"{UiDir}/SPR_SignPlank_Left.png", false);
            GenerateStoneCrestSprite($"{UiDir}/SPR_LogoCrest_Stone.png");
            GenerateBannerFlagSprite($"{UiDir}/SPR_LogoFlag.png");
            GenerateMushroomSprite($"{UiDir}/SPR_MushroomProp.png");
            GenerateGrassTuftSprite($"{UiDir}/SPR_GrassTuft.png");
            GenerateWoodBoardSprite($"{UiDir}/SPR_WoodBoardPanel.png");
            GeneratePillButtonSprite($"{UiDir}/SPR_WoodButtonPill.png");
            Texture2D particleTex = GenerateSoftGlowTexture($"{UiDir}/SPR_ParticleSoftGlow.png");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureSpriteImport($"{UiDir}/SPR_WoodPost.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_SignPlank_Right.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_SignPlank_Left.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_LogoCrest_Stone.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_LogoFlag.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_MushroomProp.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_GrassTuft.png", Vector4.zero);
            ConfigureSpriteImport($"{UiDir}/SPR_WoodBoardPanel.png", new Vector4(48f, 48f, 48f, 48f));
            ConfigureSpriteImport($"{UiDir}/SPR_WoodButtonPill.png", new Vector4(28f, 20f, 28f, 20f));
            ConfigureParticleTexImport($"{UiDir}/SPR_ParticleSoftGlow.png");

            particleTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{UiDir}/SPR_ParticleSoftGlow.png");
            menuParticleMat = CreateParticleMaterial("Assets/Materials/MAT_Menu_Particle.mat", particleTex);

            luckiestFont = EnsureCartoonFontAsset(
                "Assets/Art/Fonts/LuckiestGuy-Regular.ttf",
                "Assets/Art/Fonts/LuckiestGuy-Regular SDF.asset",
                "Assets/Art/Fonts/LuckiestGuy-Regular SDF - Cartoon.mat",
                0.26f, 0.25f, new Color(0.16f, 0.11f, 0.07f, 1f), new Color(0.10f, 0.07f, 0.04f, 0.72f));

            fredokaFont = EnsureCartoonFontAsset(
                "Assets/Art/Fonts/Fredoka-Bold.ttf",
                "Assets/Art/Fonts/Fredoka-Bold SDF.asset",
                "Assets/Art/Fonts/Fredoka-Bold SDF - Cartoon.mat",
                0.15f, 0.12f, new Color(0.14f, 0.11f, 0.08f, 1f), new Color(0.06f, 0.05f, 0.04f, 0.55f));

            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset EnsureCartoonFontAsset(
            string ttfPath,
            string assetPath,
            string presetMatPath,
            float outlineWidth,
            float dilate,
            Color outlineColor,
            Color underlayColor)
        {
            Font srcFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (srcFont == null) return null;

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(srcFont);
                if (fontAsset == null) return null;

                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.TryAddCharacters(
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?:-+()[]{}/|'%\"•©_&=",
                    out _);

                AssetDatabase.CreateAsset(fontAsset, assetPath);
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = Path.GetFileNameWithoutExtension(assetPath) + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = Path.GetFileNameWithoutExtension(assetPath) + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                EditorUtility.SetDirty(fontAsset);
            }

            // Create/Update cartoon outline material preset paired cleanly with this font atlas
            Material presetMat = AssetDatabase.LoadAssetAtPath<Material>(presetMatPath);
            if (presetMat == null && fontAsset.material != null)
            {
                presetMat = new Material(fontAsset.material);
                AssetDatabase.CreateAsset(presetMat, presetMatPath);
            }
            if (presetMat != null && fontAsset.material != null)
            {
                presetMat.shader = fontAsset.material.shader;
                presetMat.SetTexture("_MainTex", fontAsset.atlasTexture);
                presetMat.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
                presetMat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
                presetMat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                presetMat.EnableKeyword("OUTLINE_ON");
                presetMat.EnableKeyword("UNDERLAY_ON");
                presetMat.SetColor(ShaderUtilities.ID_UnderlayColor, underlayColor);
                presetMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
                presetMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
                presetMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
                presetMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
                EditorUtility.SetDirty(presetMat);
            }

            return fontAsset;
        }

        private static Material CreateParticleMaterial(string path, Texture2D tex)
        {
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader == null) pShader = Shader.Find("Particles/Standard Unlit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(pShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (pShader != null)
            {
                mat.shader = pShader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // Additive glow
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureSpriteImport(string path, Vector4 border)
        {
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            if (border.sqrMagnitude > 0.1f)
            {
                ti.spriteBorder = border;
            }
            ti.SaveAndReimport();
        }

        private static void ConfigureParticleTexImport(string path)
        {
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        private static void SavePng(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateWoodPostSprite(string path)
        {
            const int w = 220;
            const int h = 680;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#2E2115");
            Color woodLeft = Hex("#A48862");
            Color woodMid = Hex("#83694A");
            Color woodRight = Hex("#634E36");
            Color ringCap = Hex("#C3A982");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    // Organic tapered timber trunk slightly thicker at bottom, oval wooden cap at top
                    float halfW = Mathf.Lerp(0.32f, 0.26f, ny) + Mathf.Sin(ny * 7.5f) * 0.012f;
                    float dx = (nx - 0.5f) / halfW;

                    bool inside = false;
                    bool inCap = false;
                    float edgeDist = 0f;

                    if (ny <= 0.90f)
                    {
                        float d = Mathf.Abs(dx);
                        inside = d <= 1.0f;
                        edgeDist = (1f - d) * (halfW * w);
                    }
                    else
                    {
                        // Oval cut-top cap at y in [0.88..0.98]
                        float cy = (ny - 0.90f) / 0.08f;
                        float d = Mathf.Sqrt(dx * dx + cy * cy);
                        inside = d <= 1.0f;
                        inCap = inside && d <= 0.96f && ny >= 0.895f;
                        edgeDist = (1f - d) * (halfW * w * 0.85f);
                    }

                    // Also include cylinder arc overlap at top neck
                    if (!inside && ny < 0.91f && ny > 0.84f)
                    {
                        float d = Mathf.Abs(dx);
                        if (d <= 1.0f)
                        {
                            inside = true;
                            edgeDist = (1f - d) * (halfW * w);
                        }
                    }

                    if (!inside) continue;

                    if (edgeDist < 5.5f || ny < 0.01f)
                    {
                        px[y * w + x] = outline;
                        continue;
                    }

                    Color col;
                    if (inCap)
                    {
                        float cy = (ny - 0.90f) / 0.075f;
                        float rd = Mathf.Sqrt(dx * dx + cy * cy);
                        float rings = Mathf.Sin(rd * 18f) * 0.5f + 0.5f;
                        col = Color.Lerp(ringCap, woodMid, rings * 0.35f);
                        if (rd > 0.82f) col = outline;
                    }
                    else
                    {
                        float u = Mathf.Clamp01(dx * 0.5f + 0.5f);
                        col = u < 0.45f
                            ? Color.Lerp(woodLeft, woodMid, u / 0.45f)
                            : Color.Lerp(woodMid, woodRight, (u - 0.45f) / 0.55f);

                        // Subtle vertical wood grain & knots
                        float grain = Mathf.Sin((nx * 36f) + Mathf.Sin(ny * 10f) * 1.6f);
                        if (grain > 0.82f) col = Color.Lerp(col, woodRight, 0.35f);
                        else if (grain < -0.88f) col = Color.Lerp(col, woodLeft, 0.25f);

                        // Top-cap underside shadow
                        if (ny > 0.82f && ny <= 0.90f)
                            col = Color.Lerp(col, woodRight, 0.45f * ((ny - 0.82f) / 0.08f));
                    }

                    float aa = Mathf.Clamp01(edgeDist / 2.2f);
                    col.a = aa;
                    px[y * w + x] = col;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateSignPlankSprite(string path, bool pointRight)
        {
            const int w = 680;
            const int h = 196;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#2B1F14");
            Color topBevel = Hex("#B89C74");
            Color woodMain = Hex("#907451");
            Color woodDark = Hex("#6B5439");
            Color nailDark = Hex("#3A2E24");
            Color nailHi = Hex("#C6B296");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                float vDistCenter = Mathf.Abs(ny - 0.5f) * 2f; // 0 at center, 1 at top/bottom

                for (int x = 0; x < w; x++)
                {
                    float rawNx = x / (float)(w - 1);
                    float nx = pointRight ? rawNx : (1f - rawNx); // 0 = back tail, 1 = arrow tip

                    // Back tail notched V-cleft (x in 0..0.10); front tip pointed arrow (x in 0.82..0.97)
                    float yWave = Mathf.Sin(nx * 14f) * 0.015f;
                    float topY = 0.86f + yWave;
                    float botY = 0.14f - yWave * 0.6f;

                    float halfSpan = (topY - botY) * 0.5f;
                    float midY = (topY + botY) * 0.5f;
                    float normY = Mathf.Abs(ny - midY) / Mathf.Max(0.05f, halfSpan);

                    // Compute signed distance inside plank polygon (pixels approx)
                    float dLeft;
                    if (nx < 0.09f)
                    {
                        float tailEdgeX = 0.03f + (1f - Mathf.Min(1f, normY)) * 0.055f;
                        dLeft = (nx - tailEdgeX) * w;
                    }
                    else
                    {
                        dLeft = (nx - 0.03f) * w;
                    }

                    float dRight;
                    if (nx > 0.82f)
                    {
                        float maxNormY = Mathf.Clamp01((0.97f - nx) / 0.15f);
                        dRight = (maxNormY - normY) * halfSpan * h;
                    }
                    else
                    {
                        dRight = (0.97f - nx) * w;
                    }

                    float dVert = (1f - normY) * halfSpan * h;
                    float minEdgePx = Mathf.Min(dLeft, Mathf.Min(dRight, dVert));

                    if (minEdgePx < 0f) continue;

                    if (minEdgePx < 6.0f)
                    {
                        float aaOut = Mathf.Clamp01(minEdgePx / 1.8f);
                        Color o = outline;
                        o.a = aaOut;
                        px[y * w + x] = o;
                        continue;
                    }

                    // Inner wood fill with top sun bevel, bottom shadow, and painted grain
                    Color col = Color.Lerp(woodDark, woodMain, Mathf.Clamp01((ny - 0.16f) / 0.45f));
                    if (ny > 0.72f)
                        col = Color.Lerp(col, topBevel, Mathf.Clamp01((ny - 0.72f) / 0.16f));

                    float grain = Mathf.Sin((ny * 28f) + Mathf.Sin(nx * 9f) * 1.4f);
                    if (grain > 0.78f) col = Color.Lerp(col, woodDark, 0.25f);
                    else if (grain < -0.85f) col = Color.Lerp(col, topBevel, 0.20f);

                    // Subtle diagonal notch / chip marks matching hand-drawn cartoon planks
                    if ((nx > 0.28f && nx < 0.305f && ny > 0.77f) || (nx > 0.64f && nx < 0.665f && ny < 0.23f))
                        col = Color.Lerp(col, outline, 0.65f);

                    // Two wooden/iron nail heads tucked cleanly near the notched back-tail (away from plank lettering)
                    Vector2 uv = new Vector2(nx, ny);
                    Vector2 nailA = new Vector2(0.14f, 0.65f);
                    Vector2 nailB = new Vector2(0.14f, 0.35f);
                    float aspect = w / (float)h;
                    foreach (Vector2 nPos in new[] { nailA, nailB })
                    {
                        Vector2 delta = uv - nPos;
                        delta.x *= aspect;
                        float dist = delta.magnitude;
                        if (dist < 0.072f)
                        {
                            if (dist > 0.050f) col = nailDark;
                            else
                            {
                                col = Color.Lerp(nailHi, nailDark, Mathf.Clamp01(dist / 0.055f));
                                if ((delta - new Vector2(-0.014f, 0.014f)).magnitude < 0.015f)
                                    col = Color.white;
                            }
                        }
                    }

                    col.a = 1f;
                    px[y * w + x] = col;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateStoneCrestSprite(string path)
        {
            const int w = 940;
            const int h = 430;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#24221E");
            Color stoneHi = Hex("#99958B");
            Color stoneMid = Hex("#757168");
            Color stoneDark = Hex("#524F48");
            Color recess = Hex("#3A3834");
            Color moss = Hex("#79C732");
            Color mushRed = Hex("#DB313E");
            Color mushCream = Hex("#F5EBD6");
            Color crystalCyan = Hex("#4CE0FF");
            Color woodSpear = Hex("#8A6238");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float cx = nx - 0.5f;
                    float cy = (ny - 0.44f) * 1.15f;
                    float angle = Mathf.Atan2(cy, cx);
                    float r = Mathf.Sqrt(cx * cx + cy * cy);

                    // Jagged faceted rocky shield radius
                    float rockLim = 0.41f
                        + 0.032f * Mathf.Cos(angle * 7f)
                        + 0.018f * Mathf.Sin(angle * 13f + 0.5f)
                        + 0.015f * Mathf.Cos(angle * 3f);

                    // Top crystal/spear spire & left toadstool bump silhouettes behind/on main stone
                    bool inCrystal = nx > 0.46f && nx < 0.56f && ny > 0.72f && ny < 0.95f &&
                        Mathf.Abs(nx - 0.51f) < (0.95f - ny) * 0.22f;
                    bool inSpearLeft = nx > 0.21f && nx < 0.34f && ny > 0.68f && ny < 0.93f &&
                        Mathf.Abs((nx - 0.28f) + (ny - 0.79f) * 0.45f) < 0.048f * (1f - (ny - 0.68f) * 1.9f);
                    bool inMainRock = r <= rockLim && Mathf.Abs(cx) < 0.45f && ny > 0.06f && ny < 0.84f;

                    // Cute toadstool dome sitting on upper-left crest rim (drawn over stone edge)
                    float mx = (nx - 0.395f) / 0.046f;
                    float my = (ny - 0.805f) / 0.048f;
                    bool inMushroom = (mx * mx + my * my) <= 1f && ny >= 0.785f;

                    if (!inMainRock && !inMushroom && !inCrystal && !inSpearLeft)
                        continue;

                    if (inMushroom)
                    {
                        float mEdge = 1f - Mathf.Sqrt(mx * mx + my * my);
                        if (mEdge < 0.20f || ny < 0.796f)
                        {
                            px[y * w + x] = outline;
                        }
                        else
                        {
                            bool spot1 = ((nx - 0.378f) * (nx - 0.378f) + (ny - 0.822f) * (ny - 0.822f)) < 0.00014f;
                            bool spot2 = ((nx - 0.412f) * (nx - 0.412f) + (ny - 0.816f) * (ny - 0.816f)) < 0.00012f;
                            px[y * w + x] = (spot1 || spot2) ? mushCream : mushRed;
                        }
                    }
                    else if (inMainRock)
                    {
                        float edgePx = Mathf.Min((rockLim - r) * w * 0.85f, (0.45f - Mathf.Abs(cx)) * w);
                        edgePx = Mathf.Min(edgePx, Mathf.Min((ny - 0.06f) * h, (0.84f - ny) * h));
                        if (edgePx < 6.5f)
                        {
                            Color o = outline;
                            o.a = Mathf.Clamp01(edgePx / 2.0f);
                            px[y * w + x] = o;
                            continue;
                        }

                        // Stone bevel shading + organic mossy patches + carved lower TACTICS bar recess
                        Color c = Color.Lerp(stoneDark, stoneMid, Mathf.Clamp01((ny - 0.08f) / 0.55f));
                        if (ny > 0.64f) c = Color.Lerp(c, stoneHi, (ny - 0.64f) / 0.22f);

                        // Angular stone facet cracks
                        if (Mathf.Abs(Mathf.Sin(nx * 24f + ny * 9f)) > 0.965f)
                            c = Color.Lerp(c, stoneDark, 0.30f);

                        // Organic mottled mossy crust along shoulders & lower edge
                        float mossNoise = Mathf.Sin(nx * 29f + 1.2f) * Mathf.Cos(ny * 34f - 0.6f);
                        if (mossNoise > 0.58f && (r > 0.29f || ny < 0.20f))
                            c = Color.Lerp(c, moss, 0.58f);

                        // Rounded carved bottom trough for "TACTICS"
                        float rx = Mathf.Abs(nx - 0.5f) / 0.275f;
                        float ry = Mathf.Abs(ny - 0.23f) / 0.105f;
                        float rDist = Mathf.Max(rx, ry) + Mathf.Pow(rx * rx + ry * ry, 2f) * 0.04f;
                        if (rDist < 1.0f)
                            c = rDist > 0.89f ? outline : recess;

                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                    else if (inCrystal)
                    {
                        float halfW = (0.95f - ny) * 0.22f;
                        float cDist = (halfW - Mathf.Abs(nx - 0.51f)) * w;
                        float tipDist = (0.95f - ny) * h;
                        px[y * w + x] = (cDist < 5.5f || tipDist < 5.5f)
                            ? outline
                            : Color.Lerp(crystalCyan, Color.white, Mathf.Clamp01((ny - 0.72f) * 1.6f));
                    }
                    else
                    {
                        float halfS = 0.048f * (1f - (ny - 0.68f) * 1.9f);
                        float sDist = (halfS - Mathf.Abs((nx - 0.28f) + (ny - 0.79f) * 0.45f)) * w;
                        float topS = (0.93f - ny) * h;
                        px[y * w + x] = (sDist < 5.0f || topS < 5.5f) ? outline : woodSpear;
                    }
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateBannerFlagSprite(string path)
        {
            const int w = 310;
            const int h = 250;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#2B1F14");
            Color poleCol = Hex("#704E2E");
            Color clothMain = Hex("#D8A64A");
            Color clothHi = Hex("#EDC56B");
            Color ringOuter = Hex("#2C78C4");
            Color ringMid = Hex("#F5EFE0");
            Color ringCenter = Hex("#29D0FF");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);

                    // Left vertical/slightly angled flagpole
                    float poleX = 0.09f + (1f - ny) * 0.05f;
                    float poleDist = Mathf.Abs(nx - poleX) * w;
                    if (poleDist < 9f && ny > 0.04f && ny < 0.94f)
                    {
                        px[y * w + x] = poleDist > 5f ? outline : poleCol;
                        continue;
                    }

                    // Waving tapered pennant extending right from pole (x in 0.10..0.92)
                    if (nx < poleX + 0.01f || nx > 0.92f) continue;
                    float u = (nx - poleX) / (0.92f - poleX);
                    float wave = Mathf.Sin(u * 4.5f) * 0.055f;
                    float top = 0.88f - u * 0.14f + wave;
                    float bot = 0.16f + u * 0.08f + wave * 0.8f;

                    // Swinging tail notch on far right
                    if (ny < bot || ny > top) continue;

                    float dTop = (top - ny) * h;
                    float dBot = (ny - bot) * h;
                    float dRight = (0.92f - nx) * w;
                    float dPole = (nx - poleX) * w;
                    float minEdge = Mathf.Min(Mathf.Min(dTop, dBot), Mathf.Min(dRight, dPole));

                    if (minEdge < 5.5f)
                    {
                        Color o = outline;
                        o.a = Mathf.Clamp01(minEdge / 1.8f);
                        px[y * w + x] = o;
                        continue;
                    }

                    // Concentric heraldic emblem target on flag cloth
                    float tx = (nx - 0.50f) * (w / (float)h);
                    float ty = ny - (top + bot) * 0.5f;
                    float tr = Mathf.Sqrt(tx * tx + ty * ty);

                    Color c = Color.Lerp(clothMain, clothHi, Mathf.Clamp01((ny - bot) / (top - bot)));
                    if (tr < 0.21f && tr > 0.185f) c = outline;
                    else if (tr <= 0.185f && tr > 0.125f) c = ringOuter;
                    else if (tr <= 0.125f && tr > 0.065f) c = ringMid;
                    else if (tr <= 0.065f) c = ringCenter;

                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateMushroomSprite(string path)
        {
            const int w = 180;
            const int h = 160;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#2C1C16");
            Color capRed = Hex("#D62C3B");
            Color capDark = Hex("#9F1C28");
            Color cream = Hex("#F7EBD5");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);

                    // Stalk
                    float stalkDx = Mathf.Abs(nx - 0.5f);
                    if (ny >= 0.06f && ny <= 0.52f && stalkDx <= Mathf.Lerp(0.18f, 0.12f, ny))
                    {
                        bool edge = stalkDx > Mathf.Lerp(0.18f, 0.12f, ny) - 0.032f || ny < 0.09f;
                        px[y * w + x] = edge ? outline : (nx < 0.52f ? cream : Color.Lerp(cream, Hex("#C8B8A2"), 0.5f));
                    }

                    // Rounded cap (y >= 0.44)
                    float cx = (nx - 0.5f) / 0.41f;
                    float cy = (ny - 0.45f) / 0.44f;
                    float cd = Mathf.Sqrt(cx * cx + cy * cy);
                    if (ny >= 0.43f && cd <= 1.0f)
                    {
                        bool edge = cd > 0.89f || ny < 0.46f;
                        if (edge)
                        {
                            px[y * w + x] = outline;
                        }
                        else
                        {
                            Color c = ny > 0.62f ? capRed : Color.Lerp(capDark, capRed, (ny - 0.45f) / 0.2f);
                            // White cartoon dots
                            float d1 = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.32f, 0.66f));
                            float d2 = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.55f, 0.74f));
                            float d3 = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.70f, 0.60f));
                            if (d1 < 0.072f || d2 < 0.085f || d3 < 0.062f)
                                c = cream;
                            px[y * w + x] = c;
                        }
                    }
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateGrassTuftSprite(string path)
        {
            const int w = 220;
            const int h = 120;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#24420E");
            Color grassBright = Hex("#84E52B");
            Color grassDeep = Hex("#52AD17");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    bool hit = false;
                    bool edge = false;
                    float shade = 0f;

                    // 4 arched cartoon blades
                    float[] bases = { 0.22f, 0.42f, 0.60f, 0.78f };
                    float[] leans = { -0.14f, -0.05f, 0.06f, 0.15f };
                    float[] heights = { 0.72f, 0.92f, 0.85f, 0.68f };
                    for (int b = 0; b < 4; b++)
                    {
                        if (ny > heights[b]) continue;
                        float u = ny / heights[b];
                        float center = bases[b] + leans[b] * (u * u);
                        float halfW = (1f - u) * 0.095f;
                        float d = Mathf.Abs(nx - center);
                        if (d <= halfW)
                        {
                            hit = true;
                            edge = d > halfW - 0.018f || ny < 0.05f;
                            shade = u;
                            break;
                        }
                    }

                    if (!hit) continue;
                    px[y * w + x] = edge ? outline : Color.Lerp(grassDeep, grassBright, shade);
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GenerateWoodBoardSprite(string path)
        {
            const int w = 512;
            const int h = 384;
            Color32[] px = new Color32[w * h];
            Color outline = Hex("#2A1D12");
            Color frameLight = Hex("#9D7B52");
            Color frameDark = Hex("#6D5234");
            Color parchment = Hex("#F3EAD3");
            Color parchmentEdge = Hex("#DFD0B0");

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float dEdgePx = RoundedBoxDist(nx, ny, w, h, 26f);
                    if (dEdgePx < 0f) continue;

                    if (dEdgePx < 6f)
                    {
                        Color o = outline;
                        o.a = Mathf.Clamp01(dEdgePx / 2f);
                        px[y * w + x] = o;
                        continue;
                    }

                    if (dEdgePx < 24f)
                    {
                        // Wooden frame border
                        Color fc = ny > 0.5f ? frameLight : Color.Lerp(frameDark, frameLight, ny);
                        if (dEdgePx > 20f) fc = outline;
                        px[y * w + x] = fc;
                    }
                    else
                    {
                        float innerEdge = dEdgePx - 24f;
                        Color pc = Color.Lerp(parchmentEdge, parchment, Mathf.Clamp01(innerEdge / 18f));
                        px[y * w + x] = pc;
                    }
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static void GeneratePillButtonSprite(string path)
        {
            const int w = 256;
            const int h = 88;
            Color32[] px = new Color32[w * h];
            Color outline = new Color(0.18f, 0.14f, 0.10f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float dEdgePx = RoundedBoxDist(nx, ny, w, h, 22f);
                    if (dEdgePx < 0f) continue;

                    if (dEdgePx < 4.5f)
                    {
                        Color o = outline;
                        o.a = Mathf.Clamp01(dEdgePx / 1.8f);
                        px[y * w + x] = o;
                        continue;
                    }

                    // High-value grayscale bevel base so Button Image.color tints crisply to any hue
                    float shade = ny > 0.74f ? 1.0f : Mathf.Lerp(0.76f, 0.94f, ny);
                    px[y * w + x] = new Color(shade, shade, shade, 1f);
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
        }

        private static Texture2D GenerateSoftGlowTexture(string path)
        {
            const int s = 64;
            Color32[] px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                float dy = ((y + 0.5f) / s) * 2f - 1f;
                for (int x = 0; x < s; x++)
                {
                    float dx = ((x + 0.5f) / s) * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = d * d * (3f - 2f * d);
                    px[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            SavePng(tex, path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float RoundedBoxDist(float nx, float ny, int w, int h, float radiusPx)
        {
            float px = Mathf.Abs(nx - 0.5f) * w;
            float py = Mathf.Abs(ny - 0.5f) * h;
            float bx = (w * 0.5f) - radiusPx - 2f;
            float by = (h * 0.5f) - radiusPx - 2f;
            float qx = Mathf.Max(px - bx, 0f);
            float qy = Mathf.Max(py - by, 0f);
            float outDist = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px - bx, py - by), 0f) - radiusPx;
            return -outDist;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
        }
    }
}
#endif
