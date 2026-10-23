using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CricketVR.Editor
{
    /// <summary>
    /// M4: ball, bowling machine, physics materials, ball collision layers, debug markers and the debug readout.
    /// Adds missing pieces only; references, colliders' physics materials and the collision matrix are re-applied
    /// each run because GameConfig and the material assets are their source of truth.
    /// </summary>
    static class BowlingBuilder
    {
        public const string BallLayer = "Ball";
        const string PhysicsFolder = "Assets/CricketVR/Settings/Physics";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        public static void Build(GameObject main, GameObject environment, GameConfig config, List<string> created)
        {
            PracticeNetBuilder.EnsureLayers(created, BallLayer);
            ConfigureCollisionMatrix();
            ApplyPhysicsMaterials(environment, created);

            var gameplay = Child(main.transform, "Gameplay", created);
            var ball = EnsureBall(gameplay, config, created);
            var markers = Child(gameplay, "DebugMarkers", created);
            var target = EnsureMarker(markers, "TargetMarker", new Color(0.2f, 0.85f, 1f), 0.12f, created);
            var bounce = EnsureMarker(markers, "BounceMarker", new Color(1f, 0.25f, 0.2f), 0.08f, created);

            var machineGo = Child(gameplay, "BowlingMachine", created).gameObject;
            var machine = GetOrAdd<BowlingMachine>(machineGo, created);
            var debug = GetOrAdd<BowlingDebugControls>(machineGo, created);

            machine.Configure(config, ball, environment.transform.Find("LauncherAnchor"), target, bounce);
            debug.Configure(config, machine, EnsureReadout(main, created));
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(debug);
            EditorUtility.SetDirty(config); // persists newly added GameConfig fields with their defaults
        }

        // ---------- Physics ----------

        /// <summary>The ball only touches the pitch/outfield, nets, stumps and the bat proxy; the bat proxy only touches the ball.</summary>
        static void ConfigureCollisionMatrix()
        {
            int ball = LayerMask.NameToLayer(BallLayer);
            int bat = LayerMask.NameToLayer(BattingBuilder.BatProxyLayer);
            var ballHits = new HashSet<int>
            {
                LayerMask.NameToLayer(PracticeNetBuilder.PitchLayer),
                LayerMask.NameToLayer(PracticeNetBuilder.BoundaryLayer),
                LayerMask.NameToLayer(PracticeNetBuilder.StumpsLayer),
                bat,
            };
            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(ball, layer, !ballHits.Contains(layer));
                if (layer != ball)
                    Physics.IgnoreLayerCollision(bat, layer, true);
            }
            Physics.IgnoreLayerCollision(bat, ball, false);
        }

        static void ApplyPhysicsMaterials(GameObject environment, List<string> created)
        {
            // "Maximum" combine on the surfaces + zero on the ball: the surface's values decide each bounce.
            // Bounciness 0.72 / friction 0.2 tuned with the owner (E-018): full ~0.5 m, good ~0.9 m, short ~1.2 m at the crease.
            var pitch = Material("PitchSurface", 0.72f, 0.20f, 0.30f, PhysicsMaterialCombine.Maximum, created);
            var net = Material("NetBoundary", 0.10f, 0.80f, 0.90f, PhysicsMaterialCombine.Maximum, created);
            var stumps = Material("StumpWood", 0.30f, 0.30f, 0.30f, PhysicsMaterialCombine.Maximum, created);

            Assign(environment.transform.Find("Pitch/Surface"), pitch);
            Assign(environment.transform.Find("Floor"), pitch);
            Assign(environment.transform.Find("Stumps"), stumps);
            var walls = environment.transform.Find("NetBoundaryColliders");
            if (walls != null)
                foreach (var c in walls.GetComponentsInChildren<Collider>())
                    c.sharedMaterial = net;
        }

        static PhysicsMaterial Material(string name, float bounce, float dynamicFriction, float staticFriction, PhysicsMaterialCombine combine, List<string> created)
        {
            var path = $"{PhysicsFolder}/{name}.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m != null)
                return m;
            Directory.CreateDirectory(PhysicsFolder);
            m = new PhysicsMaterial(name)
            {
                bounciness = bounce,
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounceCombine = combine,
                frictionCombine = combine,
            };
            AssetDatabase.CreateAsset(m, path);
            created.Add("physics material " + name);
            return m;
        }

        static void Assign(Transform t, PhysicsMaterial m)
        {
            if (t != null && t.TryGetComponent<Collider>(out var c))
                c.sharedMaterial = m;
        }

        // ---------- Ball ----------

        static BallController EnsureBall(Transform gameplay, GameConfig config, List<string> created)
        {
            var t = gameplay.Find("Ball");
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); // keeps its SphereCollider (radius 0.5, scaled)
                go.name = "Ball";
                go.transform.SetParent(gameplay, false);
                go.transform.position = FieldDimensions.DefaultReleasePoint;
                go.AddComponent<Rigidbody>();
                go.AddComponent<TrailRenderer>();
                go.AddComponent<BallController>();
                created.Add("Ball");
                t = go.transform;
            }
            var ballGo = t.gameObject;
            ballGo.layer = LayerMask.NameToLayer(BallLayer);
            t.localScale = Vector3.one * config.delivery.ballRadius * 2f;

            var renderer = ballGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PracticeNetBuilder.Mat("BallWhite", new Color(0.98f, 0.98f, 0.95f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false; // parked (hidden) until launched; BallController shows it on launch

            var body = ballGo.GetComponent<Rigidbody>();
            body.mass = config.delivery.ballMass;
            body.linearDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = true; // parked until launched

            ballGo.GetComponent<SphereCollider>().sharedMaterial =
                Material("Ball", 0f, 0f, 0f, PhysicsMaterialCombine.Minimum, created);

            var trail = ballGo.GetComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, 0f));
            trail.sharedMaterial = TrailMaterial();
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;

            var controller = ballGo.GetComponent<BallController>();
            controller.Configure(config, trail, renderer);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Material TrailMaterial()
        {
            const string path = "Assets/CricketVR/Materials/BallTrail.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Transform EnsureMarker(Transform parent, string name, Color color, float diameter, List<string> created)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;
            var go = PracticeNetBuilder.CreateVisual(PrimitiveType.Cylinder, name, parent, Vector3.zero,
                new Vector3(diameter, 0.001f, diameter), PracticeNetBuilder.Mat(name, color));
            go.SetActive(false);
            created.Add(name);
            return go.transform;
        }

        // ---------- Readout ----------

        static TMP_Text EnsureReadout(GameObject main, List<string> created)
        {
            if (!EnsureTmpEssentials(created))
                return null;
            var board = main.transform.Find("UI/LiveScoreboard");
            if (board == null)
                return null;
            var t = board.Find("DebugReadout");
            if (t == null)
            {
                var go = new GameObject("DebugReadout");
                go.transform.SetParent(board, false);
                go.AddComponent<TextMeshPro>();
                created.Add("DebugReadout");
                t = go.transform;
            }
            // The board is a scaled cube; undo its scale so the text isn't stretched. Its -Z face points at the player.
            var s = board.localScale;
            t.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            t.localPosition = new Vector3(0f, 0f, -0.6f);
            t.localRotation = Quaternion.identity;
            var text = t.GetComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(s.x * 0.92f, s.y * 0.88f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 0.6f;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.text = "DEBUG BOWLING";
            EditorUtility.SetDirty(text);
            return text;
        }

        /// <summary>
        /// TextMeshPro needs its essential resources (default font, shaders) in Assets/TextMesh Pro, committed to Git.
        /// ImportPackage is asynchronous, so in batch mode it never finishes before quitting; use the command line
        /// -importPackage flag instead (see README).
        /// </summary>
        static bool EnsureTmpEssentials(List<string> created)
        {
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
                return true;
            if (!Application.isBatchMode)
                AssetDatabase.ImportPackage(Path.GetFullPath(TmpEssentials), false);
            Debug.LogError("[CricketVR] TextMeshPro essential resources are missing. In the Editor: Window > TextMeshPro > " +
                           "Import TMP Essential Resources, then re-run this step. From Terminal: Unity -batchmode -quit " +
                           "-projectPath . -importPackage \"<ugui package>/Package Resources/TMP Essential Resources.unitypackage\"");
            return false;
        }

        // ---------- Helpers ----------

        static Transform Child(Transform parent, string name, List<string> created)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            created.Add(name);
            return t;
        }

        static T GetOrAdd<T>(GameObject go, List<string> created) where T : Component
        {
            if (go.TryGetComponent<T>(out var c))
                return c;
            created.Add(typeof(T).Name);
            return go.AddComponent<T>();
        }
    }
}
