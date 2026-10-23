using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CricketVR.Editor
{
    /// <summary>
    /// Generates a realistic-looking bat (owner request, 2026-10-23) as mesh assets, so no third-party model is needed.
    /// Everything is in the bat frame of <see cref="BatGeometry"/>: +Y up the handle, +Z out of the hitting face,
    /// grip point at the origin. The flat hitting face sits at z = +thickness/2, exactly where the physics proxy's
    /// face is, so the visible face and the colliding face agree. The back (spine) is deeper than the proxy box;
    /// M5 decides whether the collider needs to follow it.
    /// Shape values are visual only and based on typical adult bats within the Laws (width <= 108 mm, depth <= 67 mm,
    /// edges <= 40 mm).
    /// </summary>
    static class BatMeshGenerator
    {
        public const string ModelsFolder = "Assets/CricketVR/Models";
        public const string GrainTexturePath = "Assets/CricketVR/Materials/WillowGrain.png";

        // Blade profile (metres).
        const float EdgeDepth = 0.038f;        // thickness at the edges
        const float ShoulderEdgeDepth = 0.033f;
        const float SpineDepthToe = 0.044f;    // centre thickness at the toe
        const float SpineDepthPeak = 0.062f;   // thickest point (the "middle")
        const float SpinePeakFromToe = 0.20f;  // where the peak sits, measured up from the toe
        const float SpineDepthShoulder = 0.034f;
        const float ShoulderDrop = 0.04f;      // how far the shoulders slope down from the centre to the edges
        const float ToeCornerRise = 0.007f;    // rounding of the toe corners

        // Handle (metres).
        const float HandleRadius = 0.0165f;
        const float HandleOvalDepth = 0.88f;   // front-to-back radius relative to side-to-side
        const float GripRidgePitch = 0.011f;
        const float GripRidgeHeight = 0.0007f;
        const float HandleInsert = 0.03f;      // handle continues this far into the blade (hidden)

        public struct BatMeshes
        {
            public Mesh blade, handle, splice, sticker, stickerAccent;
        }

        public static BatMeshes Generate(BatGeometry g)
        {
            Directory.CreateDirectory(ModelsFolder);
            return new BatMeshes
            {
                blade = Save(BuildBlade(g), "BatBlade"),
                handle = Save(BuildHandle(g), "BatHandle"),
                splice = Save(BuildDecal(g, SplicePatch, bothSides: true), "BatSplice"),
                sticker = Save(BuildDecal(g, StickerPatch, bothSides: false), "BatSticker"),
                stickerAccent = Save(BuildDecal(g, AccentPatch, bothSides: false), "BatStickerAccent"),
            };
        }

        // ---------- Blade shape functions ----------

        static float FaceZ(BatGeometry g) => g.bladeThickness * 0.5f;
        static float TopY(BatGeometry g, float x) => -g.gripToBlade - ShoulderDrop * Mathf.Pow(Mathf.Abs(2f * x / g.bladeWidth), 1.6f);
        static float ToeY(BatGeometry g, float x) => -g.gripToBlade - g.bladeLength + ToeCornerRise * Mathf.Pow(Mathf.Abs(2f * x / g.bladeWidth), 6f);

        /// <summary>Thickness (face to back) at a point on the blade.</summary>
        static float Depth(BatGeometry g, float x, float y)
        {
            float fromToe = y - (-g.gripToBlade - g.bladeLength);
            float t = Mathf.Clamp01(fromToe / g.bladeLength);
            float peakT = SpinePeakFromToe / g.bladeLength;
            float spine = t < peakT
                ? Mathf.Lerp(SpineDepthToe, SpineDepthPeak, Mathf.SmoothStep(0f, 1f, t / peakT))
                : Mathf.Lerp(SpineDepthPeak, SpineDepthShoulder, Mathf.SmoothStep(0f, 1f, (t - peakT) / (1f - peakT)));
            float edge = Mathf.Lerp(EdgeDepth, ShoulderEdgeDepth, Mathf.SmoothStep(0.6f, 1f, t));
            float across = Mathf.Abs(2f * x / g.bladeWidth);
            // Rounded hump: full spine depth at the centre line, easing to the edge depth.
            float hump = Mathf.Pow(Mathf.Clamp01(1f - across * across), 0.75f);
            return edge + (spine - edge) * hump;
        }

        static Vector3 FacePoint(BatGeometry g, float u, float v)
        {
            float x = u * g.bladeWidth * 0.5f;
            return new Vector3(x, Mathf.Lerp(ToeY(g, x), TopY(g, x), v), FaceZ(g));
        }

        static Vector3 BackPoint(BatGeometry g, float u, float v, float lift = 0f)
        {
            var p = FacePoint(g, u, v);
            p.z = FaceZ(g) - Depth(g, p.x, p.y) - lift;
            return p;
        }

        // ---------- Blade ----------

        static Mesh BuildBlade(BatGeometry g)
        {
            const int nu = 18, nv = 56;
            var b = new MeshBuilder();

            // Face (flat) and back (curved) as separate grids so the edge between them stays crisp.
            int face = b.Grid(nu, nv, (u, v) => FacePoint(g, u, v), (u, v) => new Vector2((u + 1f) * 0.5f, v), _ => Vector3.forward);
            int back = b.Grid(nu, nv, (u, v) => BackPoint(g, u, v), (u, v) => new Vector2((u + 1f) * 0.5f, v),
                p => p - new Vector3(0f, p.y, FaceZ(g) - EdgeDepth * 0.5f));

            // Edges (sides), toe, and shoulders join the face grid to the back grid.
            b.Strip(face, back, nu, nv, StripKind.LeftEdge, Vector3.left);
            b.Strip(face, back, nu, nv, StripKind.RightEdge, Vector3.right);
            b.Strip(face, back, nu, nv, StripKind.Toe, Vector3.down);
            b.Strip(face, back, nu, nv, StripKind.Top, Vector3.up);
            return b.ToMesh("BatBlade");
        }

        // ---------- Handle ----------

        static Mesh BuildHandle(BatGeometry g)
        {
            const int rings = 140, sides = 20;
            float bottom = -g.gripToBlade - HandleInsert;
            float top = g.handleLength - g.gripToBlade;
            float cap = HandleRadius; // rounded top
            float gripStart = -g.gripToBlade + 0.015f;

            var b = new MeshBuilder();
            int start = b.Count;
            for (int i = 0; i <= rings; i++)
            {
                float y = Mathf.Lerp(bottom, top, i / (float)rings);
                float r = HandleRadius;
                if (y > gripStart)
                    r += GripRidgeHeight * (0.5f + 0.5f * Mathf.Sin((y - gripStart) / GripRidgePitch * Mathf.PI * 2f));
                if (y > top - cap)
                {
                    float k = (y - (top - cap)) / cap;
                    r *= Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)) * 0.85f + 0.15f * (1f - k);
                }
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    b.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * HandleOvalDepth),
                        new Vector2(s / (float)sides, (y - bottom) / (top - bottom) * 8f));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * (sides + 1) + s;
                    int c = a + sides + 1;
                    var mid = (b.Pos(a) + b.Pos(c + 1)) * 0.5f;
                    b.Quad(a, a + 1, c + 1, c, mid - new Vector3(0f, mid.y, 0f));
                }
            return b.ToMesh("BatHandle");
        }

        // ---------- Decals (splice, sticker) lying just above the surface ----------

        delegate bool Patch(BatGeometry g, float u, float v, out float uMin, out float uMax);

        /// <summary>V-shaped splice where the handle joins the blade.</summary>
        static bool SplicePatch(BatGeometry g, float u, float v, out float uMin, out float uMax)
        {
            float spliceDepth = 0.11f / g.bladeLength;
            float k = (v - (1f - spliceDepth)) / spliceDepth; // 0 at the point of the V, 1 at the shoulder
            float half = 0.16f * Mathf.Clamp01(k);
            uMin = -half;
            uMax = half;
            return k > 0f;
        }

        static bool StickerPatch(BatGeometry g, float u, float v, out float uMin, out float uMax)
        {
            uMin = -0.86f;
            uMax = 0.86f;
            return v > 0.62f && v < 0.80f;
        }

        static bool AccentPatch(BatGeometry g, float u, float v, out float uMin, out float uMax)
        {
            uMin = -0.86f;
            uMax = 0.86f;
            return v > 0.575f && v < 0.61f;
        }

        static Mesh BuildDecal(BatGeometry g, Patch patch, bool bothSides)
        {
            var b = new MeshBuilder();
            AddDecal(b, g, patch, onFace: false);
            if (bothSides)
                AddDecal(b, g, patch, onFace: true);
            return b.ToMesh("BatDecal");
        }

        static void AddDecal(MeshBuilder b, BatGeometry g, Patch patch, bool onFace)
        {
            // 240 rows so thin bands (the accent stripe is ~2 cm) still get at least two rows of vertices.
            const int nu = 16, nv = 240;
            const float lift = 0.0006f; // just above the surface to avoid z-fighting
            var rows = new List<int>();
            for (int j = 0; j <= nv; j++)
            {
                float v = j / (float)nv;
                if (!patch(g, 0f, v, out float uMin, out float uMax))
                {
                    rows.Add(-1);
                    continue;
                }
                rows.Add(b.Count);
                for (int i = 0; i <= nu; i++)
                {
                    float u = Mathf.Lerp(uMin, uMax, i / (float)nu);
                    var p = onFace ? FacePoint(g, u, v) + new Vector3(0f, 0f, lift) : BackPoint(g, u, v, lift);
                    b.Add(p, new Vector2(i / (float)nu, v));
                }
            }
            for (int j = 0; j < nv; j++)
            {
                if (rows[j] < 0 || rows[j + 1] < 0) continue;
                for (int i = 0; i < nu; i++)
                {
                    int a = rows[j] + i, c = rows[j + 1] + i;
                    b.Quad(a, a + 1, c + 1, c, onFace ? Vector3.forward : Vector3.back);
                }
            }
        }

        // ---------- Willow grain texture ----------

        /// <summary>Cream willow with fine lengthwise grain. Mipmapped so the grain doesn't shimmer on Quest.</summary>
        public static Texture2D EnsureGrainTexture()
        {
            if (!File.Exists(GrainTexturePath))
            {
                const int w = 64, h = 512;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                var rng = new System.Random(20261023); // fixed seed: identical texture on every machine
                var lines = new float[w];
                for (int x = 0; x < w; x++)
                    lines[x] = (float)rng.NextDouble();
                var pixels = new Color[w * h];
                var baseColor = new Color(0.93f, 0.84f, 0.64f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        // Grain lines run along the length (v), wandering slightly.
                        int sx = (x + (int)(Mathf.Sin(y * 0.031f + x) * 1.2f) + w) % w;
                        float grain = lines[sx] > 0.82f ? 0.86f : lines[sx] > 0.55f ? 0.95f : 1f;
                        float shade = 0.97f + 0.03f * Mathf.PerlinNoise(x * 0.15f, y * 0.01f);
                        pixels[y * w + x] = baseColor * grain * shade;
                    }
                tex.SetPixels(pixels);
                Directory.CreateDirectory(Path.GetDirectoryName(GrainTexturePath));
                File.WriteAllBytes(GrainTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(GrainTexturePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(GrainTexturePath);
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GrainTexturePath);
        }

        // ---------- Asset helpers ----------

        static Mesh Save(Mesh mesh, string name)
        {
            mesh.name = name;
            var path = $"{ModelsFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Overwrite in place so the asset GUID (and scene references) stay stable.
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        enum StripKind { LeftEdge, RightEdge, Toe, Top }

        /// <summary>Small helper that builds triangles with a guaranteed outward winding.</summary>
        class MeshBuilder
        {
            readonly List<Vector3> _pos = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _tri = new List<int>();

            public int Count => _pos.Count;
            public Vector3 Pos(int i) => _pos[i];

            public int Add(Vector3 p, Vector2 uv)
            {
                _pos.Add(p);
                _uv.Add(uv);
                return _pos.Count - 1;
            }

            /// <summary>Adds an (nu+1) x (nv+1) grid over u in [-1,1], v in [0,1]; returns the first vertex index.</summary>
            public int Grid(int nu, int nv, System.Func<float, float, Vector3> point, System.Func<float, float, Vector2> uv, System.Func<Vector3, Vector3> outward)
            {
                int start = Count;
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float u = -1f + 2f * i / nu, v = j / (float)nv;
                        Add(point(u, v), uv(u, v));
                    }
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        int a = start + j * (nu + 1) + i, c = a + nu + 1;
                        var mid = (Pos(a) + Pos(c + 1)) * 0.5f;
                        Quad(a, a + 1, c + 1, c, outward(mid));
                    }
                return start;
            }

            /// <summary>Joins the matching border of the face and back grids with new (hard-edged) vertices.</summary>
            public void Strip(int face, int back, int nu, int nv, StripKind kind, Vector3 outward)
            {
                int n = kind == StripKind.LeftEdge || kind == StripKind.RightEdge ? nv : nu;
                int start = Count;
                for (int k = 0; k <= n; k++)
                {
                    int idx = kind switch
                    {
                        StripKind.LeftEdge => k * (nu + 1),
                        StripKind.RightEdge => k * (nu + 1) + nu,
                        StripKind.Toe => k,
                        _ => nv * (nu + 1) + k,
                    };
                    Add(Pos(face + idx), new Vector2(k / (float)n, 0f));
                    Add(Pos(back + idx), new Vector2(k / (float)n, 1f));
                }
                for (int k = 0; k < n; k++)
                {
                    int a = start + k * 2;
                    Quad(a, a + 2, a + 3, a + 1, outward);
                }
            }

            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            void Tri(int a, int b, int c, Vector3 outward)
            {
                // Unity treats cross(b - a, c - a) as the front-face normal; flip if it points inward.
                var n = Vector3.Cross(_pos[b] - _pos[a], _pos[c] - _pos[a]);
                if (n.sqrMagnitude < 1e-14f) return; // degenerate (e.g. the point of the splice V)
                if (Vector3.Dot(n, outward) < 0f) (b, c) = (c, b);
                _tri.Add(a);
                _tri.Add(b);
                _tri.Add(c);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_pos);
                m.SetUVs(0, _uv);
                m.SetTriangles(_tri, 0);
                m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
