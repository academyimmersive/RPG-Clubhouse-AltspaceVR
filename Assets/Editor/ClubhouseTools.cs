using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// Dev bridge: write "Audit" or "Fix" into Temp/clubhouse-cmd.txt, read Temp/clubhouse-report.txt.
[InitializeOnLoad]
public static class ClubhouseTools
{
    static readonly string Cmd = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/clubhouse-cmd.txt");
    static readonly string Rep = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/clubhouse-report.txt");

    static ClubhouseTools() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!File.Exists(Cmd)) return;
        string c = File.ReadAllText(Cmd).Trim();
        File.Delete(Cmd);
        var sb = new StringBuilder();
        try
        {
            if (c.StartsWith("Shot ")) { Shot(c.Substring(5), sb); File.WriteAllText(Rep, sb.ToString()); return; }
            if (c == "ClearLM")
            {
                Lightmapping.lightingDataAsset = null;
                LightmapSettings.lightmaps = new LightmapData[0];
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                var a = RenderSettings.ambientSkyColor; var e = RenderSettings.ambientEquatorColor; var gc = RenderSettings.ambientGroundColor;
                sb.AppendLine("cleared. ambient mode=" + RenderSettings.ambientMode + " sky=" + a + " eq=" + e + " ground=" + gc + " intensity=" + RenderSettings.ambientIntensity);
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "BakeInfo")
            {
                var ls = Lightmapping.lightingSettings;
                sb.AppendLine("lightingSettings asset: " + (ls ? AssetDatabase.GetAssetPath(ls) : "NONE"));
                if (ls) sb.AppendLine("lightmapper=" + ls.lightmapper + " maxSize=" + ls.lightmapMaxSize + " res=" + ls.lightmapResolution + " directSamples=" + ls.directSampleCount + " indirect=" + ls.indirectSampleCount + " bounces=" + ls.maxBounces + " bakedGI=" + ls.bakedGI + " compress=" + ls.lightmapCompression + " AO=" + ls.ao + " mixedMode=" + ls.mixedBakeMode + " denoiser=" + ls.denoiserTypeDirect);
                var gos = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(false)).ToList();
                int st = gos.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI));
                int lm = gos.Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < 65534);
                sb.AppendLine("active MeshRenderers=" + gos.Count + " ContributeGI=" + st + " withLightmapIndex=" + lm);
                var ls2 = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(false)).ToList();
                sb.AppendLine("active lights=" + ls2.Count + " baked=" + ls2.Count(l => l.lightmapBakeType == LightmapBakeType.Baked));
                sb.AppendLine("SystemInfo gpu=" + SystemInfo.graphicsDeviceName + " vram=" + SystemInfo.graphicsMemorySize + " cpu=" + SystemInfo.processorCount);
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Bake")
            {
                var ls = Lightmapping.lightingSettings;
                ls.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
                ls.lightmapMaxSize = 1024;
                ls.lightmapCompression = LightmapCompression.NormalQuality;
                ls.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
                ls.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
                ls.filteringMode = LightingSettings.FilterMode.Auto;
                EditorUtility.SetDirty(ls); AssetDatabase.SaveAssets();
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                bool ok = Lightmapping.BakeAsync();
                sb.AppendLine("saved scene + lighting settings (CPU, 1024 max, OIDN). BakeAsync started=" + ok);
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "BakeStatus")
            {
                sb.AppendLine("isRunning=" + Lightmapping.isRunning + " progress=" + Lightmapping.buildProgress.ToString("0.00") + " lightmaps=" + LightmapSettings.lightmaps.Length + " dirty=" + SceneManager.GetActiveScene().isDirty);
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Recover")
            {
                const string sp = "Assets/_RPGClubhouse/Scenes/RPGClubhouse.unity";
                AssetDatabase.Refresh();
                var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sp, UnityEditor.SceneManagement.OpenSceneMode.Single);
                sb.AppendLine("opened " + sc.path + " valid=" + sc.IsValid() + " loaded=" + sc.isLoaded + " roots=" + sc.rootCount);
                foreach (var g in sc.GetRootGameObjects().Where(g => g.name.StartsWith("__Greenfield")).ToList()) { sb.AppendLine("deleting " + g.name); UnityEngine.Object.DestroyImmediate(g); }
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(sp, true) };
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
                sb.AppendLine("saved. roots=" + sc.rootCount + " lightmaps=" + LightmapSettings.lightmaps.Length + " lightingData=" + (Lightmapping.lightingDataAsset != null));
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Warnings")
            {
                // 1. SDK layers and tags (the Setup panel's own fix)
                Type init = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { try { init = a.GetTypes().FirstOrDefault(t => t.Name == "InitialiseOnLoad" && t.GetMethod("SetupLayersAndTags") != null); } catch { } if (init != null) break; }
                if (init != null) { var r = init.GetMethod("SetupLayersAndTags").Invoke(null, null); sb.AppendLine("SDK SetupLayersAndTags -> " + r); }
                else sb.AppendLine("SDK InitialiseOnLoad not found");
                // 2. textures > 2048 on Android
                foreach (var p in new[] { "Assets/_RPGClubhouse/Textures/Sound_Blue.png", "Assets/_RPGClubhouse/Textures/Rock001_normal.tif", "Assets/_RPGClubhouse/Skybox/E_SB_DaySunPeakSummersky_T_A.png", "Assets/_RPGClubhouse/Textures/Sound_White.png", "Assets/_RPGClubhouse/Textures/Sound_Blue_Border.png" })
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) { sb.AppendLine("no importer " + p); continue; }
                    var ps = ti.GetPlatformTextureSettings("Android"); ps.overridden = true; ps.maxTextureSize = 2048; ti.SetPlatformTextureSettings(ps);
                    ti.SaveAndReimport(); sb.AppendLine("tex Android max 2048: " + Path.GetFileName(p));
                }
                // 3. audio
                foreach (var kv in new[] { ("Assets/_RPGClubhouse/Audio/Helo_Idle_SFX.ogg", AudioClipLoadType.CompressedInMemory), ("Assets/_RPGClubhouse/Audio/Yacht_Ambience.ogg", AudioClipLoadType.Streaming) })
                {
                    var ai = AssetImporter.GetAtPath(kv.Item1) as AudioImporter; if (ai == null) { sb.AppendLine("no audio importer " + kv.Item1); continue; }
                    var s = ai.defaultSampleSettings; s.loadType = kv.Item2; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.5f; ai.defaultSampleSettings = s;
                    ai.SaveAndReimport(); sb.AppendLine("audio " + kv.Item2 + ": " + Path.GetFileName(kv.Item1));
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Checkers") { SetupCheckers(sb); File.WriteAllText(Rep, sb.ToString()); return; }
            if (c.StartsWith("SetYaw "))
            {
                var seatT = FindType("BSSeat");
                var seats = UnityEngine.Object.FindObjectsByType(seatT, FindObjectsSortMode.None).Cast<Component>().ToDictionary(s => s.gameObject.name, s => s.transform);
                foreach (var kv in c.Substring(7).Split(';'))
                {
                    var p = kv.Split('=');
                    if (seats.TryGetValue(p[0], out var tr)) { tr.rotation = Quaternion.Euler(0, float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), 0); sb.AppendLine("yaw " + p[0] + " = " + p[1]); }
                    else sb.AppendLine("MISSING " + p[0]);
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Seats") { ReplaceSeats(sb); File.WriteAllText(Rep, sb.ToString()); return; }
            if (c == "SeatInfo")
            {
                var seatT = FindType("BSSeat");
                foreach (var s in UnityEngine.Object.FindObjectsByType(seatT, FindObjectsSortMode.None).Cast<Component>().OrderBy(s => Path_(s.gameObject)))
                {
                    var t = s.transform; var par = t.parent;
                    var rs = par ? par.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
                    Bounds b = new Bounds(par ? par.position : t.position, Vector3.zero); bool any = false;
                    foreach (var r in rs) { if (r.transform.IsChildOf(t)) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                    var bc = s.GetComponent<BoxCollider>();
                    string rel = any ? ("seatY-boundsMin=" + (t.position.y - b.min.y).ToString("0.00") + " of height " + b.size.y.ToString("0.00") + " fwdDotToCenter=" + Vector3.Dot(t.forward, Vector3.ProjectOnPlane(b.center - t.position, Vector3.up).normalized).ToString("0.00") + " horizOffsetToCenter=" + Vector3.ProjectOnPlane(b.center - t.position, Vector3.up).magnitude.ToString("0.00")) : "no renderers";
                    sb.AppendLine(Path_(s.gameObject) + " | pos=" + t.position.ToString("0.00") + " yaw=" + t.eulerAngles.y.ToString("0") + " box=" + (bc ? bc.center.ToString("0.00") + "/" + bc.size.ToString("0.00") : "none") + " | " + rel);
                }
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "IconCheck")
            {
                foreach (var gi in GizmoUtility.GetGizmoInfo().Where(g => g.name.StartsWith("BS")).Take(12))
                    sb.AppendLine(gi.name + " icon=" + gi.iconEnabled + " gizmo=" + gi.gizmoEnabled);
                var au = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
                foreach (var pn in new[] { "showSelectionOutline", "showSelectionWire", "use3dGizmos", "iconSize", "fadeGizmoSize" })
                { var pi = au.GetProperty(pn, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public); sb.AppendLine(pn + "=" + (pi != null ? pi.GetValue(null) : "n/a")); }
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "IconsOff")
            {
                var au = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
                var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                au.GetProperty("use3dGizmos", f)?.SetValue(null, false);
                SceneView.RepaintAll();
                sb.AppendLine("3D icons off");
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Icons")
            {
                int n = 0;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] ts; try { ts = a.GetTypes(); } catch { continue; }
                    foreach (var t in ts)
                        if (t.Name.StartsWith("BS") && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract)
                        { GizmoUtility.SetIconEnabled(t, false); n++; }
                }
                SceneView.RepaintAll();
                sb.AppendLine("BS icons disabled for " + n + " component types");
                File.WriteAllText(Rep, sb.ToString()); return;
            }
            if (c == "Reorg") { Reorg(sb); File.WriteAllText(Rep, sb.ToString()); return; }
            if (c == "Save") { UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets(); sb.AppendLine("saved"); File.WriteAllText(Rep, sb.ToString()); return; }
            switch (c)
            {
                case "Audit": Audit(sb); break;
                case "Fix": Fix(sb); break;
                default: sb.AppendLine("unknown " + c); break;
            }
        }
        catch (Exception e) { sb.AppendLine("EXCEPTION " + e); }
        File.WriteAllText(Rep, sb.ToString());
    }

    static string Path_(GameObject g)
    {
        var s = g.name; var t = g.transform.parent;
        while (t != null) { s = t.name + "/" + s; t = t.parent; }
        return s;
    }

    static bool IsPink(Material m)
    {
        if (m == null || m.shader == null) return true;
        var n = m.shader.name;
        if (n.Contains("InternalErrorShader")) return true;
        if (!m.shader.isSupported) return true;
        return false;
    }

    static bool UrpOk(Shader s)
    {
        if (s == null) return false;
        var n = s.name;
        if (n.StartsWith("Universal Render Pipeline") || n.StartsWith("Shader Graphs") || n.StartsWith("Banter") || n.StartsWith("Skybox")
            || n.StartsWith("Sprites") || n.StartsWith("UI/") || n.StartsWith("Hidden") || n.StartsWith("TextMeshPro")) return true;
        return false;
    }

    static void Audit(StringBuilder sb)
    {
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("SCENE " + scene.path + " dirty=" + scene.isDirty);
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
        sb.AppendLine("GameObjects: " + all.Count);

        // missing scripts
        int miss = 0;
        foreach (var g in all)
        {
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g);
            if (n > 0) { miss += n; sb.AppendLine("  MISSING x" + n + "  " + Path_(g) + "  [other comps: " + string.Join(",", g.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)) + "]"); }
        }
        sb.AppendLine("Missing scripts total: " + miss);

        // components inventory
        var inv = new Dictionary<string, int>();
        foreach (var g in all) foreach (var c in g.GetComponents<Component>()) { if (c == null) continue; var k = c.GetType().Name; inv[k] = inv.ContainsKey(k) ? inv[k] + 1 : 1; }
        sb.AppendLine("COMPONENTS: " + string.Join(", ", inv.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));

        // materials
        var badMats = new Dictionary<Material, List<string>>();
        foreach (var r in all.SelectMany(g => g.GetComponents<Renderer>()))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { sb.AppendLine("  NULL material on " + Path_(r.gameObject)); continue; }
                if (IsPink(m) || !UrpOk(m.shader))
                {
                    if (!badMats.ContainsKey(m)) badMats[m] = new List<string>();
                    badMats[m].Add(Path_(r.gameObject));
                }
            }
        sb.AppendLine("Non-URP/pink materials in use: " + badMats.Count);
        foreach (var kv in badMats)
            sb.AppendLine("  MAT " + kv.Key.name + "  shader=" + kv.Key.shader.name + "  supported=" + kv.Key.shader.isSupported + "  users=" + kv.Value.Count + "  e.g. " + kv.Value[0] + "  path=" + AssetDatabase.GetAssetPath(kv.Key));

        // cameras / audio listeners / lights
        foreach (var c in all.SelectMany(g => g.GetComponents<Camera>())) sb.AppendLine("CAMERA " + Path_(c.gameObject) + " enabled=" + c.enabled);
        foreach (var c in all.SelectMany(g => g.GetComponents<AudioListener>())) sb.AppendLine("AUDIOLISTENER " + Path_(c.gameObject));
        var lights = all.SelectMany(g => g.GetComponents<Light>()).ToList();
        sb.AppendLine("LIGHTS: " + lights.Count + " (dir=" + lights.Count(l => l.type == LightType.Directional) + " point=" + lights.Count(l => l.type == LightType.Point) + " spot=" + lights.Count(l => l.type == LightType.Spot) + " area=" + lights.Count(l => l.type == LightType.Rectangle || l.type == LightType.Disc) + ")");
        foreach (var l in lights.Where(l => l.type == LightType.Directional)) sb.AppendLine("  DIR " + Path_(l.gameObject) + " mode=" + l.lightmapBakeType + " shadows=" + l.shadows + " enabled=" + l.enabled);
        sb.AppendLine("  light modes: " + string.Join(", ", lights.GroupBy(l => l.lightmapBakeType).Select(g => g.Key + "=" + g.Count())));
        sb.AppendLine("LightmapSettings.lightmaps: " + LightmapSettings.lightmaps.Length + "  lightingDataAsset=" + (Lightmapping.lightingDataAsset != null));
        sb.AppendLine("RenderSettings: skybox=" + (RenderSettings.skybox ? RenderSettings.skybox.name + "/" + RenderSettings.skybox.shader.name : "none") + " ambientMode=" + RenderSettings.ambientMode + " fog=" + RenderSettings.fog + " reflectionMode=" + RenderSettings.defaultReflectionMode);
        sb.AppendLine("Render pipeline: " + (GraphicsSettings.defaultRenderPipeline ? GraphicsSettings.defaultRenderPipeline.name : "NONE (built-in)") + "  quality asset: " + (QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : "none"));

        // Banter / SDK objects
        var banter = all.SelectMany(g => g.GetComponents<Component>()).Where(c => c != null && c.GetType().FullName.StartsWith("Banter")).GroupBy(c => c.GetType().Name).Select(g => g.Key + "=" + g.Count());
        sb.AppendLine("BANTER comps: " + string.Join(", ", banter));

        // layers
        sb.AppendLine("LAYERS used: " + string.Join(", ", all.GroupBy(g => g.layer).OrderBy(g => g.Key).Select(g => g.Key + ":" + LayerMask.LayerToName(g.Key) + "=" + g.Count())));

        // triangles
        long tris = 0;
        foreach (var mf in all.SelectMany(g => g.GetComponents<MeshFilter>())) if (mf.sharedMesh && mf.gameObject.activeInHierarchy) tris += mf.sharedMesh.triangles.Length / 3;
        sb.AppendLine("Approx active triangles: " + tris);

        // empty leaf objects
        foreach (var g in all) if (g.transform.childCount == 0 && g.GetComponents<Component>().Length == 1) sb.AppendLine("  EMPTY LEAF " + Path_(g));
    }

    // "px,py,pz,tx,ty,tz,fov,outPath"
    static void Shot(string arg, StringBuilder sb)
    {
        var p = arg.Split(',');
        float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
        var go = new GameObject("~shot"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        cam.transform.position = new Vector3(F(0), F(1), F(2));
        cam.transform.LookAt(new Vector3(F(3), F(4), F(5)));
        cam.fieldOfView = F(6); cam.nearClipPlane = 0.1f; cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render();
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(p[7], tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        sb.AppendLine("shot " + p[7]);
    }

    static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0; var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-8f) return false;
        float inv = 1f / det; var s = o - a; float u = Vector3.Dot(s, p) * inv; if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1); float v = Vector3.Dot(d, q) * inv; if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv; return t > 0;
    }

    static void SetupCheckers(StringBuilder sb)
    {
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
        var brbT = FindType("BSRigidbody"); var grabT = FindType("BSGrababble"); var syncT = FindType("BSSyncedObject");
        sb.AppendLine("types: BSRigidbody=" + (brbT != null) + " BSGrababble=" + (grabT != null) + " BSSyncedObject=" + (syncT != null));
        if (brbT == null || grabT == null || syncT == null) return;

        // board: box collider instead of mesh collider
        var board = all.FirstOrDefault(g => g.name == "Board" && g.transform.parent && g.transform.parent.name == "E_ChessBoard_P");
        if (board != null)
        {
            var mf = board.GetComponent<MeshFilter>(); var mc = board.GetComponent<MeshCollider>();
            if (mc) UnityEngine.Object.DestroyImmediate(mc);
            var bc = board.GetComponent<BoxCollider>(); if (bc == null) bc = board.AddComponent<BoxCollider>();
            var b = mf.sharedMesh.bounds; bc.center = b.center; bc.size = b.size; bc.isTrigger = false;
            board.layer = 0;
            sb.AppendLine("Board BoxCollider center=" + b.center.ToString("0.000") + " size=" + b.size.ToString("0.000") + " worldSize=" + bc.bounds.size.ToString("0.000") + " worldTopY=" + bc.bounds.max.y.ToString("0.000"));
        }
        else sb.AppendLine("Board not found");

        // pieces
        int n = 0;
        foreach (var g in all.Where(g => g.name == "CheckerSingle"))
        {
            var mc = g.GetComponent<MeshCollider>(); if (mc) mc.convex = true;
            g.layer = 20;
            var rb = g.GetComponent<Rigidbody>(); if (rb == null) rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.1f; rb.useGravity = false; rb.isKinematic = false;
            rb.linearDamping = 4f; rb.angularDamping = 4f;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            var brb = g.GetComponent(brbT); if (brb == null) brb = g.AddComponent(brbT);
            var so = new SerializedObject(brb);
            void SetB(string f, bool v) { var p = so.FindProperty(f); if (p != null) p.boolValue = v; else sb.AppendLine("  no prop " + f); }
            SetB("freezePositionY", true); SetB("freezePositionX", false); SetB("freezePositionZ", false);
            SetB("freezeRotationX", true); SetB("freezeRotationY", true); SetB("freezeRotationZ", true); SetB("useGravity", false);
            var pm = so.FindProperty("mass"); if (pm != null) pm.floatValue = 0.1f;
            var pd = so.FindProperty("drag"); if (pd != null) pd.floatValue = 4f;
            var pa = so.FindProperty("angularDrag"); if (pa != null) pa.floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (g.GetComponent(grabT) == null) g.AddComponent(grabT);
            if (g.GetComponent(syncT) == null) g.AddComponent(syncT);
            g.layer = 20; // BSGrababble sets it too; make sure
            n++;
        }
        sb.AppendLine("Pieces set up: " + n);

        var bnd = all.FirstOrDefault(g => g.name == "Checkers_boundry");
        if (bnd) { var bc = bnd.GetComponent<BoxCollider>(); var r = bnd.GetComponent<MeshRenderer>(); sb.AppendLine("Checkers_boundry: layer=" + bnd.layer + " trigger=" + (bc && bc.isTrigger) + " rendererEnabled=" + (r && r.enabled) + " worldSize=" + (bc ? bc.bounds.size.ToString("0.00") : "-")); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("(scene dirty, not saved)");
    }

    static void ReplaceSeats(StringBuilder sb)
    {
        var prefab = Resources.Load<GameObject>("CreatorConveniences/Prefabs/Seat");
        if (prefab == null) { sb.AppendLine("Seat prefab not found"); return; }
        var scene = SceneManager.GetActiveScene();
        var olds = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
            .Where(g => g.name.StartsWith("DefaultSitTrigger") || g.name.StartsWith("LVR_SitTriggerPrefab")).ToList();
        int n = 0, miss = 0, noBack = 0;
        foreach (var old in olds)
        {
            var ot = old.transform;
            // furniture = nearest ancestor with renderers outside this trigger
            Transform fur = ot.parent; List<MeshFilter> mfs = null;
            while (fur != null)
            {
                mfs = fur.GetComponentsInChildren<MeshFilter>().Where(m => !m.transform.IsChildOf(ot) && m.sharedMesh != null && m.GetComponent<MeshRenderer>() != null && m.GetComponent<MeshRenderer>().enabled).ToList();
                if (mfs.Count > 0) break; fur = fur.parent;
            }
            var pos = ot.position; float yaw = ot.eulerAngles.y; string note = "";
            if (mfs != null && mfs.Count > 0)
            {
                var verts = new List<Vector3>(); float bestY = float.NegativeInfinity; bool hit = false;
                var samples = new[] { Vector3.zero, new Vector3(0.1f, 0, 0), new Vector3(-0.1f, 0, 0), new Vector3(0, 0, 0.1f), new Vector3(0, 0, -0.1f) };
                var ys = new List<float>();
                foreach (var m in mfs)
                {
                    var mesh = m.sharedMesh; var vs = mesh.vertices; var tr = mesh.triangles; var l2w = m.transform.localToWorldMatrix;
                    var wv = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) wv[i] = l2w.MultiplyPoint3x4(vs[i]);
                    verts.AddRange(wv);
                    foreach (var so in samples)
                    {
                        var o = pos + so + Vector3.up * 1.6f;
                        float bt = float.PositiveInfinity;
                        for (int i = 0; i < tr.Length; i += 3) if (RayTri(o, Vector3.down, wv[tr[i]], wv[tr[i + 1]], wv[tr[i + 2]], out var t) && t < bt) bt = t;
                        if (!float.IsInfinity(bt)) ys.Add(o.y - bt);
                    }
                }
                if (ys.Count > 0) { ys.Sort(); bestY = ys[ys.Count / 2]; hit = true; }
                if (hit && bestY > pos.y - 0.05f && bestY < pos.y + 1.5f) { pos.y = bestY; note += "cushionY=" + bestY.ToString("0.00"); }
                else { miss++; note += hit ? "hitY=" + bestY.ToString("0.00") + " rejected" : "no surface hit"; if (pos.y < 0.3f) pos.y += 0.45f; }
                // backrest direction
                Vector3 acc = Vector3.zero; int cnt = 0;
                foreach (var v in verts)
                {
                    var d = new Vector3(v.x - pos.x, 0, v.z - pos.z);
                    if (d.magnitude < 0.7f && v.y > pos.y + 0.15f && v.y < pos.y + 0.9f) { acc += d; cnt++; }
                }
                if (cnt >= 20)
                {
                    var off = acc / cnt;
                    if (off.magnitude > 0.12f) { yaw = Quaternion.LookRotation(-off.normalized, Vector3.up).eulerAngles.y; note += " backrest@" + off.magnitude.ToString("0.00") + " yaw " + ot.eulerAngles.y.ToString("0") + "->" + yaw.ToString("0"); }
                    else { noBack++; note += " no backrest (keep yaw " + yaw.ToString("0") + ")"; }
                }
                else { noBack++; note += " no backrest (keep yaw " + yaw.ToString("0") + ")"; }
            }
            else { note += "no furniture meshes; kept position, raised? " + pos; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(ot.parent, true);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var ls = go.transform.parent ? go.transform.parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            go.name = "Seat_" + (fur ? fur.name : "free") + "_" + n;
            UnityEngine.Object.DestroyImmediate(old);
            sb.AppendLine(go.name + " : " + note);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("replaced=" + n + " surfaceMiss=" + miss + " noBackrest=" + noBack + " (scene dirty, not saved)");
    }

    static string Ensure(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return folder;
        var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        return folder;
    }

    static int moved, failed;
    static void Mv(string from, string toFolder, StringBuilder sb, string newName = null)
    {
        Ensure(toFolder);
        var to = toFolder + "/" + (newName ?? Path.GetFileName(from));
        if (from == to) return;
        if (AssetDatabase.LoadMainAssetAtPath(to) != null || File.Exists(to)) to = AssetDatabase.GenerateUniqueAssetPath(to);
        var err = AssetDatabase.MoveAsset(from, to);
        if (string.IsNullOrEmpty(err)) moved++; else { failed++; sb.AppendLine("  FAIL " + from + " : " + err); }
    }

    static void Reorg(StringBuilder sb)
    {
        const string root = "Assets/_RPGClubhouse", grave = "Assets/ENGAGE-GRAVE", old = "Assets/ENGAGE_Clubhouse";
        moved = failed = 0;
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        string scenePath = SceneManager.GetActiveScene().path;
        string sceneDir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
        string lightDir = sceneDir + "/" + Path.GetFileNameWithoutExtension(scenePath);

        // clean up empty duplicate folders from a previous failed run
        foreach (var top in new[] { "Assets" }.SelectMany(t => AssetDatabase.GetSubFolders(t)).Where(f => f.StartsWith("Assets/_RPGClubhouse") || f.StartsWith("Assets/ENGAGE-GRAVE")).ToList())
        {
            foreach (var sub in AssetDatabase.GetSubFolders(top).ToList()) if (System.Text.RegularExpressions.Regex.IsMatch(sub, @" \d+$")) PruneEmpty(sub);
            if (System.Text.RegularExpressions.Regex.IsMatch(top, @" \d+$")) PruneEmpty(top);
        }
        foreach (var d in new[] { "Anim", "Audio", "Materials", "Models", "Prefabs", "Scenes", "Scenes/RPGClubhouse", "Shaders", "Skybox", "Textures" }) Ensure(root + "/" + d);
        Ensure(grave);

        AssetDatabase.StartAssetEditing();
        try
        {
            // skybox materials and their textures first
            var all = AssetDatabase.FindAssets("", new[] { old }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();
            var sky = new HashSet<string>();
            foreach (var p in all.Where(p => p.EndsWith(".mat")))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null && m.shader != null && m.shader.name.StartsWith("Skybox/"))
                { sky.Add(p); foreach (var d in AssetDatabase.GetDependencies(p, false)) if (d.StartsWith(old) && !d.EndsWith(".mat") && !d.EndsWith(".shader") && !d.EndsWith(".cs")) sky.Add(d); }
            }
            foreach (var p in sky) Mv(p, root + "/Skybox", sb);
            sb.AppendLine("Skybox files: " + sky.Count);
            all = all.Where(p => !sky.Contains(p)).ToList();

            // scene + its lighting folder
            Ensure(root + "/Scenes");
            var lightFiles = all.Where(p => p.StartsWith(lightDir + "/")).ToList();
            foreach (var p in lightFiles) Mv(p, root + "/Scenes/RPGClubhouse", sb);
            all = all.Where(p => !lightFiles.Contains(p)).ToList();
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Mv(scenePath, root + "/Scenes", sb, "RPGClubhouse.unity");
        AssetDatabase.Refresh();

        AssetDatabase.StartAssetEditing();
        try
        {
            var rest = AssetDatabase.FindAssets("", new[] { old }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();
            foreach (var p in rest)
            {
                string e = Path.GetExtension(p).ToLowerInvariant(), dest;
                if (p.Contains("/engage_scene_gte_clubhouse/engage_scene_gte_clubhouse/")) { Mv(p, root + "/Scenes/RPGClubhouse", sb); continue; }
                switch (e)
                {
                    case ".fbx": case ".obj": dest = "Models"; break;
                    case ".mat": dest = "Materials"; break;
                    case ".png": case ".tga": case ".tif": case ".jpg": case ".jpeg": case ".exr": case ".psd": dest = "Textures"; break;
                    case ".prefab": dest = "Prefabs"; break;
                    case ".anim": case ".controller": dest = "Anim"; break;
                    case ".ogg": case ".wav": case ".mp3": dest = "Audio"; break;
                    case ".shader": dest = "Shaders"; break;
                    case ".lighting": dest = "Scenes"; break;
                    case ".asset": Mv(p, grave, sb); continue;
                    default: Mv(p, grave, sb); continue;
                }
                Mv(p, root + "/" + dest, sb);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        // template scene out of the build list; only the clubhouse is a build scene
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(root + "/Scenes/RPGClubhouse.unity", true) };

        // remove folders left empty (no files besides .meta)
        int empties = 0;
        foreach (var f in AssetDatabase.GetSubFolders(old).Concat(new[] { old }))
            empties += PruneEmpty(f);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("moved=" + moved + " failed=" + failed + " emptyFoldersRemoved=" + empties + " scene=" + SceneManager.GetActiveScene().path + " dirty=" + SceneManager.GetActiveScene().isDirty);
    }

    static int PruneEmpty(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return 0;
        int n = 0;
        foreach (var s in AssetDatabase.GetSubFolders(folder)) n += PruneEmpty(s);
        var real = Path.Combine(Path.GetDirectoryName(Application.dataPath), folder);
        bool empty = Directory.Exists(real) && !Directory.EnumerateFileSystemEntries(real).Any(x => !x.EndsWith(".meta"));
        if (empty && AssetDatabase.DeleteAsset(folder)) n++;
        return n;
    }

    static Type FindType(string name)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var t in a.GetTypes()) if (t.Name == name && typeof(Component).IsAssignableFrom(t)) return t;
        return null;
    }

    static void Transparent(Material m, bool additive)
    {
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", additive ? 1 : 10); m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    static void Retarget(Material m, string urpShader, StringBuilder sb, Action<Material, Texture, Color> post)
    {
        Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
        Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        string was = m.shader.name;
        m.shader = Shader.Find(urpShader);
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        post?.Invoke(m, tex, col);
        EditorUtility.SetDirty(m);
        sb.AppendLine("  MAT " + m.name + ": " + was + " -> " + urpShader + (tex ? " tex=" + tex.name : ""));
    }

    static void Fix(StringBuilder sb)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        Func<List<GameObject>> allObjs = () => roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
        var all = allObjs();

        // 1. seats from ENGAGE sit triggers (before stripping, by name)
        var seatType = FindType("BSSeat");
        int seats = 0;
        foreach (var g in all.Where(g => g.name.StartsWith("DefaultSitTrigger") || g.name.StartsWith("LVR_SitTriggerPrefab")))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
            var bc = g.GetComponent<Collider>(); if (bc) bc.isTrigger = true;
            g.layer = 5;
            if (seatType != null && g.GetComponent(seatType) == null) { g.AddComponent(seatType); seats++; }
        }
        sb.AppendLine("Seats created (BSSeat on ENGAGE sit triggers): " + seats + (seatType == null ? "  [BSSeat type not found]" : ""));

        // 2. delete ENGAGE-only subtrees
        string[] killRoots = { "OutfitZones", "Portals", "LocationBoundary(One Per Scene)", "NetworkStateManager(One Per Scene)", "SceneVariables" };
        int killed = 0;
        foreach (var g in allObjs().Where(g => killRoots.Contains(g.name) || g.name == "EditorOnlyRenderer").ToList())
            if (g) { sb.AppendLine("  DELETE " + Path_(g)); UnityEngine.Object.DestroyImmediate(g); killed++; }

        // 3. strip missing scripts, delete bare leaves that only existed to hold them
        var hadMissing = allObjs().Where(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) > 0).ToList();
        int removed = 0;
        foreach (var g in allObjs()) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
        sb.AppendLine("Missing scripts removed: " + removed + ", subtrees deleted: " + killed);
        foreach (var g in hadMissing)
            if (g && g.transform.childCount == 0 && g.GetComponents<Component>().Length == 1 && g.layer != 5)
            { sb.AppendLine("  DELETE bare leaf " + Path_(g)); UnityEngine.Object.DestroyImmediate(g); }

        // 4. materials
        var dir = "Assets/ENGAGE_Clubhouse/Materials_Normalized";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/ENGAGE_Clubhouse", "Materials_Normalized");
        var defMat = AssetDatabase.LoadAssetAtPath<Material>(dir + "/Clubhouse_Default.mat");
        if (defMat == null)
        {
            defMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); defMat.name = "Clubhouse_Default";
            defMat.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f, 1f));
            AssetDatabase.CreateAsset(defMat, dir + "/Clubhouse_Default.mat");
        }
        all = allObjs();
        var mats = all.SelectMany(g => g.GetComponents<Renderer>()).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
        foreach (var m in mats)
        {
            string s = m.shader.name;
            switch (s)
            {
                case "Unlit/Texture": Retarget(m, "Universal Render Pipeline/Unlit", sb, null); break;
                case "Unlit/Color": Retarget(m, "Universal Render Pipeline/Unlit", sb, null); break;
                case "Unlit/UnlitShaderTransition": Retarget(m, "Universal Render Pipeline/Unlit", sb, null); break;
                case "Engage/Water_Ver_0.4":
                    Retarget(m, "Universal Render Pipeline/Lit", sb, (mm, t, c) =>
                    {
                        mm.SetColor("_BaseColor", new Color(0.12f, 0.35f, 0.5f, 0.65f));
                        mm.SetFloat("_Smoothness", 0.95f); mm.SetFloat("_Metallic", 0f);
                        if (mm.HasProperty("_BaseMap")) mm.SetTexture("_BaseMap", null);
                        Transparent(mm, false);
                    }); break;
                case "Custom/DecalMat_Surface (bake compat)":
                    Retarget(m, "Universal Render Pipeline/Lit", sb, null); break;
                case "Legacy Shaders/Particles/Additive":
                case "Mobile/Particles/Additive":
                    Retarget(m, "Universal Render Pipeline/Particles/Unlit", sb, (mm, t, c) => Transparent(mm, true)); break;
            }
        }
        int nulls = 0, defs = 0;
        var flame = mats.FirstOrDefault(m => m.name == "LargeFlame01");
        foreach (var r in all.SelectMany(g => g.GetComponents<Renderer>()))
        {
            var ms = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null) { ms[i] = r.gameObject.name.Contains("Flame") && flame ? flame : defMat; nulls++; ch = true; }
                else if (ms[i].name == "Default-Material") { ms[i] = defMat; defs++; ch = true; }
            }
            if (ch) r.sharedMaterials = ms;
        }
        sb.AppendLine("Null slots filled: " + nulls + ", Default-Material replaced: " + defs);

        // 5. layers: 24 (hand colliders) -> UI so the canvases are clickable; 26+ belong to the client -> Default
        int lay = 0;
        foreach (var g in all) { if (g.layer == 24) { g.layer = 5; lay++; } else if (g.layer >= 25) { g.layer = 0; lay++; } }
        sb.AppendLine("Layers fixed: " + lay);

        // 6. spawn point
        var start = all.FirstOrDefault(g => g.name == "PlayerStartPosition");
        var spawnType = FindType("BSSpawn");
        if (start && spawnType != null && start.GetComponent(spawnType) == null) { start.AddComponent(spawnType); sb.AppendLine("BSSpawn added on PlayerStartPosition at " + start.transform.position); }
        else sb.AppendLine("Spawn: start=" + (start != null) + " type=" + (spawnType != null));

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("Done (scene marked dirty, NOT saved). Re-run Audit.");
    }
}
