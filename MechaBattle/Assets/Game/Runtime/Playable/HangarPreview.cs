using System;
using MechaBattle.Construction;
using UnityEngine;

namespace MechaBattle.Playable
{
    // A bounded off-screen studio; only its RenderTexture is shown in the frontend.
    public sealed class HangarPreview : IDisposable
    {
        public const int PreviewLayer = 31;
        public const int PreviewMask = 1 << PreviewLayer;
        public const int BattleMask = ~PreviewMask;
        private readonly GameObject root;
        private readonly MechaMeshLibrary library;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private GameObject model;
        private bool disposed;
        public RenderTexture Texture => texture;
        public Camera Camera => camera;
        public GameObject Model => model;
        public bool Visible => root.activeSelf;

        public HangarPreview(Transform parent, MechaMeshLibrary library)
        {
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            root = new GameObject("HangarPreviewStudio"); root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0, -1000, 0);
            texture = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32) { name = "HangarPreviewTexture", antiAliasing = 1 };
            texture.Create();
            camera = new GameObject("HangarPreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.targetTexture = texture; camera.rect = new Rect(0, 0, 1, 1);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.045f, 0.075f, 0.105f);
            camera.cullingMask = PreviewMask; camera.nearClipPlane = 0.1f; camera.farClipPlane = 30;
            camera.fieldOfView = 32; camera.aspect = 4f / 3f; camera.useOcclusionCulling = false;
            AddLight("HangarKey", new Vector3(35, -30, 0), 1.8f, Color.white);
            AddLight("HangarFill", new Vector3(20, 145, 0), 0.8f, new Color(0.55f, 0.75f, 1));
            root.SetActive(false);
        }
        private void AddLight(string name, Vector3 rotation, float intensity, Color color)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>(); light.transform.SetParent(root.transform, false);
            light.transform.localRotation = Quaternion.Euler(rotation); light.type = LightType.Directional;
            light.intensity = intensity; light.color = color; light.cullingMask = PreviewMask; light.shadows = LightShadows.None;
        }
        public void SetBuild(ResolvedBuild build, int team, int weaponIndex)
        {
            if (model != null) { model.SetActive(false); UnityEngine.Object.Destroy(model); }
            model = library.Create(build, team, root.transform, false, weaponIndex);
            model.transform.localRotation = Quaternion.Euler(0, 15, 0);
            SetLayer(model.transform);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Preview has no model renderers.");
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // A sphere stays within frame while the model slowly rotates.
            var center = new Vector3(root.transform.position.x, bounds.center.y, root.transform.position.z);
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude + (bounds.center - center).magnitude);
            float halfFov = Mathf.Min(camera.fieldOfView * Mathf.Deg2Rad * 0.5f, Mathf.Atan(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * camera.aspect));
            float distance = radius * 1.12f / Mathf.Sin(halfFov);
            camera.transform.position = center + new Vector3(0.45f, 0.12f, 1).normalized * distance;
            camera.transform.LookAt(center);
            camera.farClipPlane = Mathf.Max(30, distance + radius * 3);
        }
        private static void SetLayer(Transform node)
        {
            node.gameObject.layer = PreviewLayer; foreach (Transform child in node) SetLayer(child);
        }
        public void SetVisible(bool visible) { if (visible && !texture.IsCreated()) texture.Create(); root.SetActive(visible); }
        public void Update(float deltaTime)
        {
            if (Visible && model != null) model.transform.Rotate(Vector3.up, deltaTime * 16, Space.World);
        }
        public void Clear()
        {
            if (model != null) { model.SetActive(false); UnityEngine.Object.Destroy(model); model = null; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (root != null) root.SetActive(false); if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
