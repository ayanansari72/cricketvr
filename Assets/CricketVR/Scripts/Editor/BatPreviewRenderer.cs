using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CricketVR.Editor
{
    /// <summary>
    /// Renders the bat visual from three angles into Builds/Preview/Bat_*.png (gitignored), so its look can be checked without a headset.
    /// Uses an isolated preview scene; the open scene is not modified.
    /// </summary>
    public static class BatPreviewRenderer
    {
        [MenuItem("CricketVR/Debug/Render Bat Preview")]
        public static void Render()
        {
            EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);
            var source = GameObject.Find("Main/Player/Bat/BatVisual");
            if (source == null)
            {
                Debug.LogError("[CricketVR] BatVisual not found; build the Main scene first.");
                return;
            }

            foreach (var mf in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                int plus = 0, minus = 0;
                foreach (var n in m.normals) { if (n.z > 0.9f) plus++; else if (n.z < -0.9f) minus++; }
                Debug.Log($"[CricketVR] Preview mesh {mf.name} ({m.name}, active {mf.gameObject.activeInHierarchy}): {m.vertexCount} verts, normals +Z {plus}, -Z {minus}, bounds {m.bounds}");
            }

            var preview = EditorSceneManager.NewPreviewScene();
            var bat = Object.Instantiate(source);
            EditorSceneManager.MoveGameObjectToScene(bat, preview);
            bat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var lightGo = new GameObject("Light");
            EditorSceneManager.MoveGameObjectToScene(lightGo, preview);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            var camGo = new GameObject("Camera");
            EditorSceneManager.MoveGameObjectToScene(camGo, preview);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = preview;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.45f, 0.30f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;

            var rt = new RenderTexture(512, 1024, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var centre = new Vector3(0f, -0.2f, 0f);
            // Warm-up render: the first render in a fresh preview scene can use stale light data.
            Shoot(cam, rt, light, centre, new Vector3(0.35f, 0.15f, 1.9f), "Face");
            Shoot(cam, rt, light, centre, new Vector3(-0.35f, 0.15f, -1.9f), "Back");
            Shoot(cam, rt, light, centre, new Vector3(1.9f, 0.1f, 0.25f), "Side");
            Shoot(cam, rt, light, centre, new Vector3(0.35f, 0.15f, 1.9f), "Face");

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        static void Shoot(Camera cam, RenderTexture rt, Light light, Vector3 target, Vector3 offset, string label)
        {
            cam.transform.position = target + offset;
            cam.transform.LookAt(target);
            // Key light from above and beside the camera, so the side being inspected is lit.
            light.transform.rotation = Quaternion.LookRotation(target - (cam.transform.position + Vector3.up * 1.2f + cam.transform.right * 0.8f));
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory("Builds/Preview");
            var path = $"Builds/Preview/Bat_{label}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[CricketVR] Wrote " + Path.GetFullPath(path));
        }
    }
}
