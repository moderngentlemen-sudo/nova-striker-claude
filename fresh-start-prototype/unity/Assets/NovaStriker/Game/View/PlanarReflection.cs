// Reflections on the deck (Settings: Reflections): a second camera mirrors the main one in the floor plane (y = 0)
// and renders the scene into a texture each frame; thin sheets over the open sky's floors (Reflect shader) show it,
// strongest at grazing angles, so the deck takes on the sheen of a polished sky city: characters, lights, the
// white towers and the sky all mirrored in it. High renders it at half size, Ultra at full; Low has none.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class PlanarReflection
    {
        const int SHEET_LAYER = 4;          // (Unity's built-in "Water" layer: the sheets stay out of their own reflection)
        const float PLANE_Y = 0;
        readonly Camera main, cam;
        readonly List<TMesh> sheets = new List<TMesh>();
        RenderTexture rt; int rtW, rtH;
        bool? on;

        public PlanarReflection(View view, Camera main)
        {
            this.main = main;
            var go = new GameObject("reflection-camera");
            cam = go.AddComponent<Camera>();
            cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
            cam.cullingMask = ~((1 << SHEET_LAYER) | (1 << 5) | (1 << 8));   // (not the sheets, not the UI)
            cam.depth = main.depth - 1;
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = false; cd.renderShadows = false; cd.requiresDepthTexture = false; cd.requiresColorTexture = false;
            cd.antialiasing = AntialiasingMode.None;
            RenderPipelineManager.beginCameraRendering += (ctx, c) => { if (c == cam) GL.invertCulling = true; };
            RenderPipelineManager.endCameraRendering += (ctx, c) => { if (c == cam) GL.invertCulling = false; };
            // the sheets: over every flat floor at y = 0 on the open sky's route, a hair above it
            var mat = TMat.Raw(new Material(Templates.Reflect));
            foreach (var b in Level.BOXES)
            {
                if (b.type != 's' || b.y1 != PLANE_Y || b.tag == "bound" || Level.RouteAt(b.x0).id != "skyport" || Level.CurvedSpan(b.x0, b.x1)) continue;
                var m = new TMesh(Geo.Plane((float)(b.x1 - b.x0), 4.4f), mat) { cast = false, receive = false, noOutline = true };
                m.rotation.x = -Mathf.PI / 2; m.position.copy(S.W((b.x0 + b.x1) / 2, PLANE_Y + 0.006, 0));
                m.go.layer = SHEET_LAYER; view.scene.add(m); sheets.Add(m);
            }
        }

        public void Update()
        {
            string q = SETTINGS.quality;
            bool want = SETTINGS.reflections && q != "low";
            if (want != on) { on = want; cam.enabled = want; foreach (var s in sheets) s.visible = want; }
            if (!want) return;
            int div = q == "ultra" ? 2 : 4, w = Mathf.Max(64, Screen.width / div), h = Mathf.Max(64, Screen.height / div);
            if (rt == null || rtW != w || rtH != h)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "deck-reflection" };
                rtW = w; rtH = h; cam.targetTexture = rt;
                Shader.SetGlobalTexture("_NovaReflectionTex", rt);
            }
            // mirror the main camera in the floor
            cam.orthographic = main.orthographic; cam.orthographicSize = main.orthographicSize;
            cam.fieldOfView = main.fieldOfView; cam.aspect = main.aspect; cam.nearClipPlane = main.nearClipPlane; cam.farClipPlane = main.farClipPlane;
            var R = Matrix4x4.identity; R.m11 = -1; R.m13 = 2 * PLANE_Y;
            cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            cam.worldToCameraMatrix = main.worldToCameraMatrix * R;
            // clip everything below the floor (the oblique near plane), so only what stands above it is mirrored
            var wtc = cam.worldToCameraMatrix;
            Vector3 cp = wtc.MultiplyPoint(new Vector3(0, PLANE_Y + 0.02f, 0)), cn = wtc.MultiplyVector(Vector3.up).normalized;
            cam.ResetProjectionMatrix();
            cam.projectionMatrix = cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
        }
    }
}
