#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// The Genesis Duel Center: Genesis City's big tournament hall and player trading hub, in the spirit of
    /// Tokyo's convention halls (glass front, a crown of four inverted pyramids on the roof). Built entirely
    /// from our own geometry and materials, so there is nothing to license.
    ///
    /// World > 8 scans the whole map for the best open lot (flat, empty, close to the plaza, front on a
    /// street), clears small props (lamps, trees, benches, parked cars) off it, and builds the hall:
    ///   - walk-in entrance under a canopy, no loading screen,
    ///   - a championship stage with a hanging jumbotron and bleachers,
    ///   - a tournament floor of playable duel tables,
    ///   - a trading hub of booths and trade tables,
    ///   - a pack counter (the nine boosters) in the lobby.
    /// Re-running it removes the old hall first.
    /// </summary>
    public static class GenesisDuelCenter
    {
        public const string RootName = "Genesis Duel Center";
        private const string MatFolder = "Assets/Art/Generated/DuelCenter";

        private const float MinFromHub = 28f, MaxFromHub = 230f, Apron = 8f;

        private static readonly Vector3[] Sizes =   // width (front), depth, wall height
        {
            new Vector3(40f, 32f, 13f),
            new Vector3(34f, 28f, 12f),
            new Vector3(28f, 24f, 11f),
        };

        private static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.3f, 0.8f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.3f);

        // ------------------------------------------------------------------ menu

        [MenuItem("Duel Genesis/World/8. Build Genesis Duel Center (Tournaments + Trading Hub)")]
        public static void BuildMenu()
        {
            GameObject city = GameObject.Find(GenesisWorldBuilder.CityRootName);
            if (city == null) { EditorUtility.DisplayDialog(RootName, "Build Genesis City first (World > 3).", "OK"); return; }

            Transform old = city.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();

            var log = new StringBuilder();
            Lot lot = null;
            Vector3 size = Sizes[0];
            try
            {
                foreach (Vector3 s in Sizes)
                {
                    lot = FindLot(city.transform, s, log);
                    if (lot != null) { size = s; break; }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (lot == null)
            {
                Debug.LogWarning("Duel: Genesis found no open lot for the Duel Center.\n" + log);
                EditorUtility.DisplayDialog(RootName, "No open lot big enough was found. See the Console.", "OK");
                return;
            }

            int cleared = 0;
            foreach (GameObject prop in lot.props.Where(p => p != null).Distinct().ToList())
            {
                Object.DestroyImmediate(prop);
                cleared++;
            }
            Physics.SyncTransforms();

            GameObject hall = Build(city.transform, lot, size, log);
            log.Insert(0, $"Lot at {lot.centre} (yaw {lot.yaw:0}, {lot.fromHub:0} m from the plaza, street run {lot.frontRun:0} m); cleared {cleared} small props.\n");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = hall;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"Duel: Genesis built the {RootName} ({size.x:0} x {size.y:0} m).\n{log}");
        }

        [MenuItem("Duel Genesis/DEV/Teleport To Genesis Duel Center (Play Mode)")]
        public static void TeleportToCenter()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Duel: Genesis: enter Play mode first."); return; }
            var player = Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonPlayerController>();
            GameObject spot = GameObject.Find(RootName + " - Arrival Spot");
            if (player == null || spot == null) return;
            player.Teleport(spot.transform.position, spot.transform.rotation);
            Object.FindFirstObjectByType<DuelGenesis.Player.ThirdPersonCamera>()?.SnapBehind(spot.transform.forward);
        }

        // ------------------------------------------------------------------ lot search

        private sealed class Lot
        {
            public Vector3 centre;        // footprint centre on the ground
            public float yaw;             // local +Z (the entrance) points at the street
            public float fromHub, frontRun, score;
            public List<GameObject> props = new List<GameObject>();
        }

        /// <summary>
        /// Every 5 m across the map and every 15 degrees of rotation: the footprint (plus a 1 m margin) must
        /// be flat ground of the city map with nothing standing on it except small movable props, and one
        /// long side must open onto a street. The best lot is near the plaza with a long street in front.
        /// </summary>
        private static Lot FindLot(Transform city, Vector3 size, StringBuilder log)
        {
            Transform map = city.Find("Map");
            if (map == null) { log.AppendLine("No Map under the city root."); return null; }
            Bounds mb = GenesisWorldBuilder.RendererBounds(map.gameObject);

            float w = size.x + 2f, d = size.y + 2f, h = size.z;
            float x0 = Mathf.Max(mb.min.x, -MaxFromHub), x1 = Mathf.Min(mb.max.x, MaxFromHub);
            float z0 = Mathf.Max(mb.min.z, -MaxFromHub), z1 = Mathf.Min(mb.max.z, MaxFromHub);

            // 1. Ground heights on a 2.5 m grid, once (NaN = not open ground: a building, a wall, off the map).
            float[,] ground = GroundGrid(map, x0, z0, x1, z1, out int gx, out int gz);
            if (ground == null) return null;
            float Height(Vector3 p)
            {
                int i = Mathf.RoundToInt((p.x - x0) / GridCell), j = Mathf.RoundToInt((p.z - z0) / GridCell);
                return i < 0 || j < 0 || i >= gx || j >= gz ? float.NaN : ground[i, j];
            }

            var candidates = new List<Lot>();
            int tested = 0, flatFails = 0, blocked = 0, noStreet = 0;
            var hits = new Collider[96];
            int su = Mathf.CeilToInt(w / GridCell), sv = Mathf.CeilToInt(d / GridCell);
            for (float x = x0; x <= x1; x += 5f)
            {
                if (EditorUtility.DisplayCancelableProgressBar(RootName, $"Looking for a {size.x:0} x {size.y:0} m lot...", (x - x0) / Mathf.Max(1f, x1 - x0)))
                    return null;
                for (float z = z0; z <= z1; z += 5f)
                {
                    float fromHub = new Vector2(x, z).magnitude;
                    if (fromHub < MinFromHub + size.y * 0.5f || fromHub > MaxFromHub) continue;
                    if (float.IsNaN(Height(new Vector3(x, 0f, z)))) continue;

                    for (float yaw = 0f; yaw < 180f; yaw += 15f)
                    {
                        tested++;
                        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                        Vector3 c = new Vector3(x, 0f, z);

                        float gMax = float.MinValue, gMin = float.MaxValue;
                        bool flat = true;
                        for (int iu = 0; iu <= su && flat; iu++)
                        for (int iv = 0; iv <= sv && flat; iv++)
                        {
                            float g = Height(c + q * new Vector3((iu / (float)su - 0.5f) * w, 0f, (iv / (float)sv - 0.5f) * d));
                            if (float.IsNaN(g)) { flat = false; break; }
                            gMax = Mathf.Max(gMax, g);
                            gMin = Mathf.Min(gMin, g);
                            if (gMax - gMin > 0.45f) flat = false;
                        }
                        if (!flat) { flatFails++; continue; }

                        Vector3 boxCentre = new Vector3(x, gMax + 0.35f + h * 0.5f, z);
                        int n = Physics.OverlapBoxNonAlloc(boxCentre, new Vector3(w * 0.5f, h * 0.5f, d * 0.5f), hits, q, ~0, QueryTriggerInteraction.Ignore);
                        var props = new List<GameObject>();
                        bool ok = n < hits.Length;
                        for (int i = 0; i < n && ok; i++)
                        {
                            if (hits[i].name.Contains("Test_Ground")) continue;
                            GameObject prop = MovableProp(hits[i], map);
                            if (prop == null) ok = false;
                            else props.Add(prop);
                        }
                        if (!ok) { blocked++; continue; }

                        // The entrance goes on the long side that opens onto the longer street (ties: towards the plaza).
                        float bestScore = float.MinValue, bestRun = 0f;
                        Vector3 bestDir = Vector3.forward;
                        Vector3 toHub = new Vector3(-x, 0f, -z).normalized;
                        foreach (float side in new[] { 1f, -1f })
                        {
                            Vector3 dir = q * Vector3.forward * side;
                            Vector3 right = q * Vector3.right;
                            float run = float.MaxValue;
                            foreach (float off in new[] { -0.3f, 0f, 0.3f })
                            {
                                Vector3 o = new Vector3(x, gMax + 1.6f, z) + dir * (d * 0.5f + 0.2f) + right * (off * w);
                                float r = Physics.Raycast(o, dir, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore) && MovableProp(hit.collider, map) == null ? hit.distance : 90f;
                                run = Mathf.Min(run, r);
                            }
                            float sc = Mathf.Min(run, 40f) + Vector3.Dot(dir, toHub) * 3f;
                            if (sc > bestScore) { bestScore = sc; bestRun = run; bestDir = dir; }
                        }
                        if (bestRun < 10f) { noStreet++; continue; }

                        var lot = new Lot
                        {
                            centre = new Vector3(x, gMax, z),
                            yaw = Quaternion.LookRotation(bestDir, Vector3.up).eulerAngles.y,
                            fromHub = fromHub,
                            frontRun = bestRun,
                            props = props,
                        };
                        // Close to the plaza matters most; a long street in front and few props to clear help.
                        lot.score = -fromHub * 0.35f + Mathf.Min(bestRun, 40f) * 0.4f - props.Count * 0.5f + Vector3.Dot(bestDir, toHub) * 4f;
                        candidates.Add(lot);
                    }
                }
            }

            log.AppendLine($"{size.x:0} x {size.y:0} m: {tested} placements tested, {flatFails} not flat open ground, {blocked} blocked, {noStreet} with no street in front, {candidates.Count} possible lots.");
            if (candidates.Count == 0) return null;
            foreach (Lot l in candidates.OrderByDescending(l => l.score).Take(6))
                log.AppendLine($"   candidate {l.centre} yaw {l.yaw:0}: {l.fromHub:0} m from plaza, street {l.frontRun:0} m, {l.props.Count} props, score {l.score:0.0}");
            return candidates.OrderByDescending(l => l.score).First();
        }

        private const float GridCell = 2.5f;
        private static float[,] _grid;
        private static Vector4 _gridKey;

        private static float[,] GroundGrid(Transform map, float x0, float z0, float x1, float z1, out int nx, out int nz)
        {
            nx = Mathf.FloorToInt((x1 - x0) / GridCell) + 1;
            nz = Mathf.FloorToInt((z1 - z0) / GridCell) + 1;
            var key = new Vector4(x0, z0, x1, z1);
            if (_grid != null && _gridKey == key) return _grid;
            var g = new float[nx, nz];
            for (int i = 0; i < nx; i++)
            {
                if (i % 8 == 0 && EditorUtility.DisplayCancelableProgressBar(RootName, "Mapping the city's open ground...", i / (float)nx))
                    return null;
                for (int j = 0; j < nz; j++)
                    g[i, j] = GroundAt(map, new Vector3(x0 + i * GridCell, 0f, z0 + j * GridCell), out float y) ? y : float.NaN;
            }
            _grid = g;
            _gridKey = key;
            return g;
        }

        /// <summary>Height of the city map's ground at <paramref name="p"/> (roads, pavements, squares).</summary>
        private static bool GroundAt(Transform map, Vector3 p, out float y)
        {
            y = 0f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 250f, p.z), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.name.Contains("Test_Ground")) continue;
                if (MovableProp(hit.collider, map) != null) continue;   // a car roof or bench is not the ground
                if (!hit.collider.transform.IsChildOf(map) || hit.normal.y < 0.9f || hit.point.y > 3f || hit.point.y < -8f) return false;
                y = hit.point.y;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The small dressing object this collider belongs to (street lamp, tree, bench, bin, parked car),
        /// which may be cleared to make room; null for the map's buildings and for gameplay objects.
        /// </summary>
        private static GameObject MovableProp(Collider c, Transform map)
        {
            if (c == null || c.transform.IsChildOf(map)) return null;
            Transform t = c.transform;
            // Walk up to the object directly under a dressing group or the city root.
            while (t.parent != null && t.parent.parent != null && t.parent.name != "Hub Dressing" && !t.parent.name.StartsWith("Traffic") &&
                   t.parent.name != GenesisWorldBuilder.CityRootName && !t.parent.name.StartsWith("Street"))
                t = t.parent;
            string n = t.name;
            if (n.StartsWith("Kame Game Shop") || n.Contains("Duel Table") || n.StartsWith("Player") || n.StartsWith("Genesis") ||
                n.StartsWith("Hub Duel") || n.StartsWith("DG ") || n.Contains("Billboard"))
                return null;
            Bounds b = GenesisWorldBuilder.RendererBounds(t.gameObject);
            if (b.size == Vector3.zero) b = c.bounds;
            return Mathf.Max(b.size.x, b.size.z) <= 9f && b.size.y <= 12f ? t.gameObject : null;
        }

        // ------------------------------------------------------------------ the building

        private static GameObject Build(Transform city, Lot lot, Vector3 size, StringBuilder log)
        {
            float W = size.x, D = size.y, H = size.z;
            float hx = W * 0.5f - 0.5f, hz = D * 0.5f - 0.5f;   // inner half extents
            const float F = 0.2f;                                // floor top above the street

            var root = new GameObject(RootName).transform;
            root.SetParent(city, false);
            root.SetPositionAndRotation(lot.centre, Quaternion.Euler(0f, lot.yaw, 0f));

            Material concrete = Mat("DC Concrete", new Color(0.8f, 0.8f, 0.82f), smooth: 0.25f);
            Material concreteDark = Mat("DC Concrete Dark", new Color(0.36f, 0.37f, 0.42f), smooth: 0.3f);
            Material metal = Mat("DC Dark Metal", new Color(0.1f, 0.11f, 0.14f), metallic: 0.8f, smooth: 0.6f);
            Material silver = Mat("DC Silver", new Color(0.72f, 0.74f, 0.78f), metallic: 0.85f, smooth: 0.72f);
            Material glass = GlassMat();
            Material floor = Mat("DC Hall Floor", new Color(0.13f, 0.15f, 0.21f), smooth: 0.35f);
            Material paving = Mat("DC Plaza Paving", new Color(0.55f, 0.56f, 0.6f), smooth: 0.35f);
            Material stageMat = Mat("DC Stage", new Color(0.16f, 0.08f, 0.28f), smooth: 0.7f);
            Material seat = Mat("DC Seat Red", new Color(0.62f, 0.08f, 0.12f), smooth: 0.4f);
            Material cyan = Mat("DC Glow Cyan", Cyan, emission: Cyan * 2.2f);
            Material magenta = Mat("DC Glow Magenta", Magenta, emission: Magenta * 2f);
            Material gold = Mat("DC Glow Gold", Gold, emission: Gold * 1.6f);
            Material ceilingLight = Mat("DC Ceiling Light", Color.white, emission: new Color(1f, 0.96f, 0.9f) * 1.8f);
            Material counterMat = Mat("DC Counter", new Color(0.93f, 0.93f, 0.95f), smooth: 0.6f);
            Material boothBoard = Mat("DC Booth Board", new Color(0.05f, 0.06f, 0.1f), smooth: 0.5f);
            Material logo = LogoMat();

            // ---- ground: hall floor slab and a paved plaza in front of the entrance
            Box(root, "Floor", new Vector3(0f, F * 0.5f, 0f), new Vector3(W, F, D), floor);
            Box(root, "Foundation", new Vector3(0f, -0.5f, 0f), new Vector3(W + 0.1f, 1f, D + 0.1f), concreteDark, collider: false);
            float apron = Mathf.Clamp(lot.frontRun - 3f, 4f, Apron);
            Box(root, "Entrance Plaza", new Vector3(0f, F * 0.5f - 0.02f, D * 0.5f + apron * 0.5f), new Vector3(W + 4f, F - 0.04f, apron), paving);
            for (int i = -1; i <= 1; i += 2)
                Box(root, "Plaza Light Line", new Vector3(i * 5.5f, F + 0.005f, D * 0.5f + apron * 0.5f), new Vector3(0.18f, 0.01f, apron - 0.6f), cyan, collider: false);

            // A gentle ramp if the street in front sits lower than the plaza (the player can't climb tall steps).
            Physics.SyncTransforms();
            Vector3 beyond = root.TransformPoint(new Vector3(0f, 3f, D * 0.5f + apron + 0.6f));
            if (Physics.Raycast(beyond, Vector3.down, out RaycastHit street, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                float dh = lot.centre.y + F - street.point.y;
                if (dh > 0.18f && dh < 1.2f)
                {
                    float L = Mathf.Max(1.5f, dh * 5f);
                    GameObject ramp = Box(root, "Entrance Ramp", new Vector3(0f, F - dh * 0.5f - 0.05f, D * 0.5f + apron + L * 0.5f - 0.05f),
                        new Vector3(W * 0.6f, 0.1f, Mathf.Sqrt(L * L + dh * dh)), paving);
                    ramp.transform.localRotation = Quaternion.Euler(Mathf.Atan2(dh, L) * Mathf.Rad2Deg, 0f, 0f);
                }
            }

            // ---- shell: concrete sides and back, glass front
            Box(root, "Back Wall", new Vector3(0f, F + H * 0.5f, -D * 0.5f + 0.25f), new Vector3(W, H, 0.5f), concrete);
            Box(root, "Left Wall", new Vector3(-W * 0.5f + 0.25f, F + H * 0.5f, 0f), new Vector3(0.5f, H, D), concrete);
            Box(root, "Right Wall", new Vector3(W * 0.5f - 0.25f, F + H * 0.5f, 0f), new Vector3(0.5f, H, D), concrete);
            // A dark plinth band and glowing trims wrap the building.
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(root, "Side Plinth", new Vector3(sx * (W * 0.5f + 0.05f), F + 0.6f, 0f), new Vector3(0.12f, 1.2f, D), concreteDark, collider: false);
                Box(root, "Side Trim Cyan", new Vector3(sx * (W * 0.5f + 0.06f), F + H - 1.1f, 0f), new Vector3(0.1f, 0.35f, D), cyan, collider: false);
                Box(root, "Side Trim Magenta", new Vector3(sx * (W * 0.5f + 0.06f), F + 5.2f, 0f), new Vector3(0.1f, 0.12f, D), magenta, collider: false);
            }
            Box(root, "Back Trim Cyan", new Vector3(0f, F + H - 1.1f, -D * 0.5f - 0.06f), new Vector3(W, 0.35f, 0.1f), cyan, collider: false);

            const float doorW = 9f, doorH = 4.6f;
            float zf = D * 0.5f - 0.1f;
            float sideW = (W - doorW) * 0.5f;
            Box(root, "Glass Front Left", new Vector3(-(doorW * 0.5f + sideW * 0.5f), F + H * 0.5f, zf), new Vector3(sideW, H, 0.12f), glass);
            Box(root, "Glass Front Right", new Vector3(doorW * 0.5f + sideW * 0.5f, F + H * 0.5f, zf), new Vector3(sideW, H, 0.12f), glass);
            Box(root, "Glass Over Entrance", new Vector3(0f, F + doorH + (H - doorH) * 0.5f, zf), new Vector3(doorW, H - doorH, 0.12f), glass);
            for (float mx = -W * 0.5f + 0.15f; mx <= W * 0.5f; mx += 4f)
            {
                bool inDoor = Mathf.Abs(mx) < doorW * 0.5f - 0.2f;
                float bottom = inDoor ? doorH : 0f;
                Box(root, "Mullion", new Vector3(mx, F + bottom + (H - bottom) * 0.5f, zf + 0.1f), new Vector3(0.18f, H - bottom, 0.22f), metal, collider: !inDoor);
            }
            foreach (float ex in new[] { -doorW * 0.5f, doorW * 0.5f })
                Box(root, "Entrance Post", new Vector3(ex, F + doorH * 0.5f, zf + 0.1f), new Vector3(0.3f, doorH, 0.3f), metal);
            Box(root, "Entrance Header", new Vector3(0f, F + doorH + 0.15f, zf + 0.1f), new Vector3(doorW + 0.3f, 0.3f, 0.3f), metal);
            Box(root, "Transom", new Vector3(0f, F + doorH + 0.15f, zf + 0.1f), new Vector3(W, 0.16f, 0.2f), metal, collider: false);

            // ---- roof, with a glowing edge
            Box(root, "Roof", new Vector3(0f, F + H + 0.35f, 0f), new Vector3(W + 1.4f, 0.7f, D + 1.4f), concreteDark);
            Box(root, "Roof Edge Glow", new Vector3(0f, F + H + 0.35f, D * 0.5f + 0.72f), new Vector3(W + 1.4f, 0.22f, 0.06f), cyan, collider: false);

            // ---- canopy over the entrance, with the name on its face
            float canopyY = F + doorH + 1.2f, canopyD = Mathf.Min(7f, apron - 0.5f);
            Box(root, "Canopy", new Vector3(0f, canopyY, D * 0.5f + canopyD * 0.5f), new Vector3(16f, 0.45f, canopyD), silver);
            Box(root, "Canopy Glow", new Vector3(0f, canopyY - 0.24f, D * 0.5f + canopyD * 0.5f), new Vector3(14.5f, 0.03f, canopyD - 1f), ceilingLight, collider: false);
            Box(root, "Canopy Face", new Vector3(0f, canopyY - 0.3f, D * 0.5f + canopyD + 0.02f), new Vector3(16f, 1.7f, 0.08f), metal, collider: false);
            foreach (float px in new[] { -7f, 7f })
                Box(root, "Canopy Pillar", new Vector3(px, (F + canopyY) * 0.5f, D * 0.5f + canopyD - 0.6f), new Vector3(0.4f, canopyY - F, 0.4f), silver);
            Sign(root, "GENESIS DUEL CENTER", new Vector3(0f, canopyY - 0.05f, D * 0.5f + canopyD + 0.1f), 180f, 0.09f, Color.Lerp(Cyan, Color.white, 0.5f));
            Sign(root, "TOURNAMENTS  \u2022  TRADING  \u2022  BOOSTER PACKS", new Vector3(0f, canopyY - 0.8f, D * 0.5f + canopyD + 0.1f), 180f, 0.035f, Gold);

            // ---- the round Duel Genesis logo on the glass above the canopy, and the name on the roofline
            float logoSize = Mathf.Min(H - doorH - 2.2f, 7.5f);
            if (logo != null)
                Quad(root, "Front Logo", new Vector3(0f, F + H - logoSize * 0.5f - 0.6f, D * 0.5f + 0.25f), new Vector2(logoSize * 1.075f, logoSize), 180f, logo);

            // ---- crown: four inverted pyramids on a core, the hall's landmark silhouette
            Mesh pyramid = InvertedPyramidMesh();
            float crownZ = D * 0.5f - Mathf.Min(8f, D * 0.28f);
            float ps = Mathf.Min(9f, W * 0.22f);
            Box(root, "Crown Core", new Vector3(0f, F + H + 2.2f, crownZ), new Vector3(ps * 1.1f, 3.6f, ps * 1.1f), concrete);
            int pi = 0;
            foreach (float cx in new[] { -1f, 1f })
            foreach (float cz in new[] { -1f, 1f })
            {
                var p = new GameObject("Crown Pyramid " + (++pi));
                p.transform.SetParent(root, false);
                p.transform.localPosition = new Vector3(cx * ps * 0.52f, F + H + 3.2f, crownZ + cz * ps * 0.52f);
                p.transform.localScale = new Vector3(ps, ps * 0.8f, ps);
                p.AddComponent<MeshFilter>().sharedMesh = pyramid;
                p.AddComponent<MeshRenderer>().sharedMaterial = silver;
                p.AddComponent<MeshCollider>().sharedMesh = pyramid;
                float top = ps * 0.8f;
                foreach (var (o, s) in new[] { (new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.03f, 0.03f)), (new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0.03f, 0.03f)),
                                               (new Vector3(0.5f, 0f, 0f), new Vector3(0.03f, 0.03f, 1f)), (new Vector3(-0.5f, 0f, 0f), new Vector3(0.03f, 0.03f, 1f)) })
                    Box(p.transform, "Rim Glow", new Vector3(o.x, 1f, o.z), s, cyan, collider: false);
            }

            var beacon = new GameObject("Beacon - DUEL CENTER");
            beacon.transform.SetParent(root, false);
            beacon.transform.localPosition = new Vector3(0f, F + H + 3.2f + ps * 0.8f, crownZ);
            var gb = beacon.AddComponent<DuelGenesis.Core.GenesisBeacon>();
            gb.label = "GENESIS DUEL CENTER";
            gb.color = Cyan;

            // ---- interior: ceiling light strips and lights
            for (float lz = -hz + 3f; lz < hz - 1f; lz += 5f)
                Box(root, "Ceiling Light Strip", new Vector3(0f, F + H - 0.1f, lz), new Vector3(W - 4f, 0.08f, 0.5f), ceilingLight, collider: false);
            int lights = 0;
            foreach (float lx in new[] { -W * 0.3f, 0f, W * 0.3f })
            foreach (float lz in new[] { -D * 0.25f, D * 0.2f })
            {
                PointLight(root, "Hall Light", new Vector3(lx, F + H - 2.2f, lz), Mathf.Max(W, D) * 0.6f, 28f, new Color(1f, 0.95f, 0.88f));
                lights++;
            }

            // ---- walls inside: a dark wainscot with a glowing rail, and booster-art banners up high
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(root, "Wainscot", new Vector3(sx * (hx - 0.03f), F + 0.6f, 0f), new Vector3(0.06f, 1.2f, D - 1f), concreteDark, collider: false);
                Box(root, "Wainscot Rail", new Vector3(sx * (hx - 0.05f), F + 1.22f, 0f), new Vector3(0.06f, 0.07f, D - 1f), sx < 0f ? cyan : magenta, collider: false);
            }
            Box(root, "Back Wainscot", new Vector3(0f, F + 0.6f, -hz + 0.03f), new Vector3(W - 1f, 1.2f, 0.06f), concreteDark, collider: false);
            string[] banners = { "dark", "light", "fire", "water", "wind", "earth", "monster", "spell", "trap" };
            int bannerCount = Mathf.Clamp(Mathf.FloorToInt((D - 6f) / 5f), 2, 5);
            for (int i = 0; i < bannerCount * 2; i++)
            {
                string id = banners[i % banners.Length];
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{id}.jpg");
                if (tex == null) continue;
                bool left = i < bannerCount;
                int k = left ? i : i - bannerCount;
                float bz = -hz + 3f + k * (D - 6f) / Mathf.Max(1, bannerCount - 1);
                float bx = left ? -hx + 0.08f : hx - 0.08f;
                Quad(root, "Banner " + id, new Vector3(bx, F + H * 0.58f, bz), new Vector2(2.4f, 3.6f), left ? 270f : 90f,
                    Mat("DC Pack " + id, Color.white, tex: tex, emission: Color.white * 0.35f));
            }
            if (logo != null)
                Quad(root, "Back Wall Logo", new Vector3(0f, F + H * 0.62f, -hz + 0.08f), new Vector2(4.2f * 1.075f, 4.2f), 180f, logo);
            Sign(root, "GENESIS CHAMPIONSHIP", new Vector3(0f, F + H * 0.62f - 2.8f, -hz + 0.1f), 180f, 0.06f, Gold);

            // ---- championship stage, bleachers and jumbotron (centre-back)
            float R = Mathf.Min(5.5f, D * 0.17f);
            const float bleacherRows = 4, rowDepth = 1.1f, rowRise = 0.45f;
            float stageZ = -hz + bleacherRows * rowDepth + 1.6f + R;
            Vector3 stage = new Vector3(0f, F, stageZ);
            Disc(root, "Stage Glow Ring", stage + Vector3.up * 0.01f, R + 1.25f, 0.02f, cyan, collider: false);
            Disc(root, "Stage Step", stage + Vector3.up * 0.13f, R + 0.8f, 0.26f, concreteDark);
            Disc(root, "Stage", stage + Vector3.up * 0.26f, R, 0.52f, stageMat);
            DuelTable(root, "Championship Table", stage + Vector3.up * 0.52f, 90f, 700, dealCards: true);

            float bw = R * 2f + 5f;
            for (int row = 0; row < bleacherRows; row++)
            {
                float z = -hz + (bleacherRows - row - 0.5f) * rowDepth;
                float y = F + (row + 1) * rowRise;
                Box(root, "Bleacher Row " + (row + 1), new Vector3(0f, y * 0.5f + F * 0.5f, z), new Vector3(bw, y - F, rowDepth), concrete);
                Box(root, "Bleacher Seats " + (row + 1), new Vector3(0f, y + 0.08f, z + 0.1f), new Vector3(bw - 0.6f, 0.16f, 0.5f), seat, collider: false);
            }

            Vector3 jumbo = new Vector3(0f, F + H - 3.6f, stageZ);
            Box(root, "Jumbotron", jumbo, new Vector3(5.2f, 2.8f, 5.2f), metal, collider: false);
            for (int e = 0; e < 4; e++)
            {
                Quaternion r = Quaternion.Euler(0f, e * 90f, 0f);
                Vector3 edge = r * new Vector3(0f, 0f, 2.64f);
                Vector3 len = r * new Vector3(5.36f, 0f, 0.1f);
                Vector3 rim = new Vector3(Mathf.Abs(len.x) + 0.1f, 0.12f, Mathf.Abs(len.z) + 0.1f);
                Box(root, "Jumbotron Rim Top", jumbo + edge + Vector3.up * 1.4f, rim, cyan, collider: false);
                Box(root, "Jumbotron Rim Bottom", jumbo + edge - Vector3.up * 1.4f, rim, magenta, collider: false);
            }
            if (logo != null)
                for (int i = 0; i < 4; i++)
                {
                    Quaternion r = Quaternion.Euler(0f, i * 90f, 0f);
                    Quad(root, "Jumbotron Screen", jumbo + r * new Vector3(0f, 0f, 2.62f), new Vector2(2.6f * 1.075f, 2.6f), i * 90f + 180f, logo);
                }
            Box(root, "Jumbotron Cable", jumbo + Vector3.up * ((F + H - jumbo.y) * 0.5f + 1.4f), new Vector3(0.12f, F + H - jumbo.y - 1.4f, 0.12f), metal, collider: false);
            int si = 0;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                Vector3 at = stage + new Vector3(sx * (R + 5f), H - 1.5f, sz * (R + 3f));
                SpotLight(root, "Stage Spot " + (++si), at, stage + Vector3.up * 1f, si % 2 == 0 ? Cyan : Magenta);
            }
            PointLight(root, "Stage Light", stage + Vector3.up * 4f, R * 2.5f, 20f, new Color(1f, 0.9f, 0.8f));

            // ---- tournament floor (left): two columns of playable duel tables
            int tables = 0;
            float lobbyZ = hz - 7f;
            foreach (float tx in new[] { -hx + 2.8f, -hx + 7.6f })
                for (float tz = -hz + 2.6f; tz <= lobbyZ - 1.4f; tz += 3.8f)
                {
                    if (Mathf.Abs(tx) - 1.4f < R + 1.4f && Mathf.Abs(tz - stageZ) < R + 2.5f) continue;
                    tables++;
                    DuelTable(root, "Tournament Table " + tables, new Vector3(tx, F, tz), 90f, 710 + tables, dealCards: false);
                }
            HangingSign(root, "TOURNAMENT FLOOR", new Vector3(-hx + 5.2f, F + H - 3f, 0f), metal, cyan);

            // ---- trading hub (right): booths along the wall, trade tables in front of them
            int booths = 0;
            GameObject stool = LoadKenney("stoolBarSquare") ?? LoadKenney("stoolBar") ?? LoadKenney("chair");
            for (float bz = -hz + 2.2f; bz <= lobbyZ - 1.4f; bz += 3.6f)
            {
                booths++;
                Booth(root, booths, new Vector3(hx - 0.3f, F, bz), counterMat, boothBoard, booths % 2 == 0 ? magenta : cyan, stool);
            }
            int sets = 0;
            GameObject kTable = LoadKenney("tableRound"), kChair = LoadKenney("chairCushion");
            if (kTable != null && kChair != null)
                for (float tz = -hz + 3.5f; tz <= lobbyZ - 1.5f; tz += 4.4f)
                {
                    float tx = hx - 7.2f;
                    if (tx - 1.4f < R + 1.5f && Mathf.Abs(tz - stageZ) < R + 2.5f) continue;
                    sets++;
                    TradeTable(root, "Trade Table " + sets, new Vector3(tx, F, tz), kTable, kChair);
                }
            HangingSign(root, "TRADING HUB", new Vector3(hx - 4.5f, F + H - 3f, 0f), metal, magenta);

            // ---- lobby: the pack counter on the left, a welcome screen on the right
            Vector3 counterAt = new Vector3(-hx + 2.4f, F, hz - 3.6f);
            Box(root, "Pack Counter", counterAt + new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 1.1f, 5f), counterMat);
            Box(root, "Pack Counter Glow", counterAt + new Vector3(0.56f, 0.85f, 0f), new Vector3(0.02f, 0.08f, 4.8f), gold, collider: false);
            var terminal = new GameObject("Pack Counter Terminal");
            terminal.transform.SetParent(root, false);
            terminal.transform.localPosition = counterAt + new Vector3(0f, 1.1f, 0f);
            var tcol = terminal.AddComponent<BoxCollider>();
            tcol.center = new Vector3(0f, 0.4f, 0f);
            tcol.size = new Vector3(1.4f, 0.8f, 5f);
            var shop = terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>();
            shop.shopName = "Genesis Duel Center Pack Counter";
            PackWall(root, new Vector3(-hx + 0.05f, F, hz - 3.6f), 90f, boothBoard);
            HangingSign(root, "BOOSTER PACKS", new Vector3(-hx + 2.4f, F + 4.2f, hz - 3.6f), metal, gold, yaw: 90f, width: 5.2f);

            if (logo != null)
            {
                Vector3 kiosk = new Vector3(hx - 3.5f, F, hz - 3.2f);
                Box(root, "Welcome Screen Stand", kiosk + new Vector3(0f, 1.3f, 0f), new Vector3(0.3f, 2.6f, 0.3f), metal);
                Box(root, "Welcome Screen", kiosk + new Vector3(0f, 2.9f, 0f), new Vector3(3.4f, 2.2f, 0.18f), metal, collider: false);
                Quad(root, "Welcome Screen Logo", kiosk + new Vector3(0f, 2.9f, 0.1f), new Vector2(2.1f, 1.95f), 180f, logo);
                Quad(root, "Welcome Screen Logo Back", kiosk + new Vector3(0f, 2.9f, -0.1f), new Vector2(2.1f, 1.95f), 0f, logo);
            }

            // Potted plants by the entrance, if the Kenney kit is in the project.
            GameObject plant = LoadKenney("pottedPlant");
            if (plant != null)
                foreach (float px in new[] { -doorW * 0.5f - 1.4f, doorW * 0.5f + 1.4f })
                {
                    PlaceSized(root, plant, "Entrance Plant", new Vector3(px, F, hz - 0.9f), 0f, 1.3f);
                    PlaceSized(root, plant, "Entrance Plant", new Vector3(px, F, D * 0.5f + 1.2f), 0f, 1.3f);
                }

            // ---- where players arrive: on the plaza, facing the doors
            var arrive = new GameObject(RootName + " - Arrival Spot");
            arrive.transform.SetParent(city, false);
            arrive.transform.position = root.TransformPoint(new Vector3(0f, F + 0.05f, D * 0.5f + apron - 1.5f));
            arrive.transform.rotation = Quaternion.LookRotation(-root.forward, Vector3.up);
            arrive.transform.SetParent(root, true);

            log.AppendLine($"Stage with the championship table, {tables} tournament tables, {booths} trade booths, {sets} trade tables, pack counter, {lights} hall lights.");
            return root.gameObject;
        }

        // ------------------------------------------------------------------ pieces

        private static void DuelTable(Transform root, string name, Vector3 local, float yaw, int seed, bool dealCards)
        {
            var table = new GameObject(name);
            table.transform.SetParent(root, false);
            table.transform.localPosition = local;
            table.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var decor = table.AddComponent<DuelGenesis.Dueling.AmbientDuelTable>();
            decor.seed = seed;
            decor.dealCards = dealCards;
            var col = table.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.38f, 0f);
            col.size = new Vector3(1.24f, 0.76f, 0.94f);
            var duel = table.AddComponent<DuelGenesis.Dueling.CityDuelTable>();
            duel.opponentSeed = seed * 37 + 11;
            duel.tableName = "Duel Center " + name;
        }

        /// <summary>A trade booth against the right wall: back board with its number, counter, a stool each side.</summary>
        private static void Booth(Transform root, int number, Vector3 wallFoot, Material counter, Material board, Material glow, GameObject stool)
        {
            var b = new GameObject("Trade Booth " + number).transform;
            b.SetParent(root, false);
            b.localPosition = wallFoot;
            b.localRotation = Quaternion.Euler(0f, -90f, 0f);   // local +Z faces into the hall (-X of the hall)
            Box(b, "Back Board", new Vector3(0f, 1.6f, 0.1f), new Vector3(3f, 2.6f, 0.15f), board);
            Box(b, "Back Board Glow", new Vector3(0f, 2.85f, 0.19f), new Vector3(3f, 0.1f, 0.03f), glow, collider: false);
            Sign(b, "TRADE BOOTH " + number, new Vector3(0f, 2.45f, 0.2f), 180f, 0.028f, Color.white, local: true);
            Box(b, "Counter", new Vector3(0f, 0.53f, 1.7f), new Vector3(2.6f, 1.06f, 0.7f), counter);
            Box(b, "Counter Glow", new Vector3(0f, 0.8f, 2.06f), new Vector3(2.4f, 0.06f, 0.02f), glow, collider: false);
            if (stool != null)
            {
                PlaceSized(b, stool, "Seller Stool", new Vector3(0f, 0f, 0.95f), 0f, 0.75f, local: true);
                PlaceSized(b, stool, "Buyer Stool", new Vector3(0f, 0f, 2.55f), 180f, 0.75f, local: true);
            }
        }

        private static void TradeTable(Transform root, string name, Vector3 local, GameObject table, GameObject chair)
        {
            var set = new GameObject(name).transform;
            set.SetParent(root, false);
            set.localPosition = local;
            set.localRotation = Quaternion.identity;
            GameObject t = PlaceSized(set, table, "Table", Vector3.zero, 0f, 0.76f, local: true);
            Bounds tb = GenesisWorldBuilder.RendererBounds(t);
            float radius = Mathf.Max(tb.extents.x, tb.extents.z);
            float scale = t.transform.localScale.x;
            Vector3 facing = ChairFacing(chair);
            for (int i = 0; i < 4; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                var c = (GameObject)PrefabUtility.InstantiatePrefab(chair, set);
                c.name = "Chair " + (i + 1);
                c.transform.localScale *= scale;
                c.transform.localRotation = Quaternion.Euler(0f, Quaternion.FromToRotation(facing, -outward).eulerAngles.y, 0f);
                Physics.SyncTransforms();
                Bounds cb = GenesisWorldBuilder.RendererBounds(c);
                float depth = Mathf.Max(cb.extents.x, cb.extents.z);
                Vector3 target = set.TransformPoint(outward * (radius + depth * 0.55f));
                c.transform.position += new Vector3(target.x - cb.center.x, set.position.y - cb.min.y, target.z - cb.center.z);
            }
            foreach (Renderer r in set.GetComponentsInChildren<Renderer>())
                if (r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>() != null) r.gameObject.AddComponent<MeshCollider>();
        }

        private static void PackWall(Transform root, Vector3 wallFoot, float yaw, Material board)
        {
            var wall = new GameObject("Pack Wall").transform;
            wall.SetParent(root, false);
            wall.localPosition = wallFoot;
            wall.localRotation = Quaternion.Euler(0f, yaw, 0f);   // local +Z faces into the hall
            Box(wall, "Board", new Vector3(0f, 1.9f, 0.06f), new Vector3(5f, 2.4f, 0.1f), board);
            string[] ids = { "monster", "spell", "trap", "dark", "light", "earth", "fire", "water", "wind" };
            for (int i = 0; i < ids.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/DuelGenesis/Packs/pack_{ids[i]}.jpg");
                if (tex == null) continue;
                int row = i < 5 ? 0 : 1, col = i < 5 ? i : i - 5;
                float count = row == 0 ? 5 : 4;
                Quad(wall, "Pack " + ids[i], new Vector3((col - (count - 1) * 0.5f) * 0.9f, row == 0 ? 2.5f : 1.35f, 0.12f), new Vector2(0.72f, 1.08f), 180f,
                    Mat("DC Pack " + ids[i], Color.white, tex: tex, emission: Color.white * 0.35f));
            }
        }

        private static void HangingSign(Transform root, string text, Vector3 at, Material frame, Material glow, float yaw = 0f, float width = 7f)
        {
            var s = new GameObject("Hanging Sign - " + text).transform;
            s.SetParent(root, false);
            s.localPosition = at;
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(s, "Panel", Vector3.zero, new Vector3(width, 1.1f, 0.2f), frame, collider: false);
            Box(s, "Glow", new Vector3(0f, -0.6f, 0f), new Vector3(width, 0.08f, 0.22f), glow, collider: false);
            Color c = glow.GetColor("_BaseColor");
            Sign(s, text, new Vector3(0f, 0f, 0.12f), 180f, 0.05f, Color.Lerp(c, Color.white, 0.4f), local: true);
            Sign(s, text, new Vector3(0f, 0f, -0.12f), 0f, 0.05f, Color.Lerp(c, Color.white, 0.4f), local: true);
        }

        // ------------------------------------------------------------------ primitives and materials

        private static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Disc(Transform parent, string name, Vector3 centre, float radius, float height, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // the capsule is the wrong shape for a disc
            if (collider) go.AddComponent<MeshCollider>().convex = true;
        }

        private static void Quad(Transform parent, string name, Vector3 local, Vector2 size, float yaw, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);   // a quad faces -Z; yaw 180 turns it to face +Z
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A text sign, drawn with the depth-tested world-text material (hidden behind walls).</summary>
        private static void Sign(Transform parent, string text, Vector3 at, float yaw, float charSize, Color colour, bool flatOnFloor = false, bool local = true)
        {
            var go = new GameObject("DC Text - " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(flatOnFloor ? 90f : 0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 96;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = charSize;
            tm.color = colour;
            go.AddComponent<DuelGenesis.Core.GenesisWorldText>();   // depth-tested font material
        }

        private static void PointLight(Transform root, string name, Vector3 local, float range, float intensity, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = colour;
            l.shadows = LightShadows.None;
        }

        private static void SpotLight(Transform root, string name, Vector3 local, Vector3 target, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.LookRotation(target - local, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 30f;
            l.spotAngle = 38f;
            l.intensity = 120f;
            l.color = colour;
            l.shadows = LightShadows.None;
        }

        private static GameObject LoadKenney(string model) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(GenesisAssetDownloads.FurnitureFolder + "/" + model + ".fbx");

        /// <summary>Instantiates a model scaled to <paramref name="height"/>, its base centred on <paramref name="foot"/>.</summary>
        private static GameObject PlaceSized(Transform parent, GameObject prefab, string name, Vector3 foot, float yaw, float height, bool local = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Physics.SyncTransforms();
            Bounds b = GenesisWorldBuilder.RendererBounds(go);
            if (b.size.y > 0.01f) go.transform.localScale *= height / b.size.y;
            Physics.SyncTransforms();
            b = GenesisWorldBuilder.RendererBounds(go);
            Vector3 target = parent.TransformPoint(foot);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                if (r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>() != null) r.gameObject.AddComponent<MeshCollider>();
            return go;
        }

        private static Vector3 ChairFacing(GameObject chairPrefab)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter mf in chairPrefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = chairPrefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices) points.Add(m.MultiplyPoint3x4(v));
            }
            if (points.Count == 0) return Vector3.forward;
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            Vector3 centre = new Vector3(points.Average(p => p.x), 0f, points.Average(p => p.z));
            var top = points.Where(p => p.y > minY + (maxY - minY) * 0.75f).ToList();
            Vector3 back = new Vector3(top.Average(p => p.x), 0f, top.Average(p => p.z)) - centre;
            return back.sqrMagnitude < 1e-6f ? Vector3.forward : -back.normalized;
        }

        /// <summary>Unit inverted pyramid: apex at the origin, a 1 x 1 square top at y = 1. Flat-shaded.</summary>
        private static Mesh InvertedPyramidMesh()
        {
            string path = MatFolder + "/DC Inverted Pyramid.asset";
            EnsureFolder();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            Vector3 a = Vector3.zero;
            Vector3[] t = { new Vector3(-0.5f, 1f, -0.5f), new Vector3(0.5f, 1f, -0.5f), new Vector3(0.5f, 1f, 0.5f), new Vector3(-0.5f, 1f, 0.5f) };
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int s = v.Count;
                v.Add(a); v.Add(t[(i + 1) % 4]); v.Add(t[i]);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            int c = v.Count;
            v.AddRange(t);
            tris.AddRange(new[] { c, c + 2, c + 1, c, c + 3, c + 2 });
            mesh = new Mesh { name = "DC Inverted Pyramid", vertices = v.ToArray(), triangles = tris.ToArray() };
            // Face winding check: make every face point away from the pyramid's centre.
            Vector3 centreP = new Vector3(0f, 0.7f, 0f);
            int[] tr = mesh.triangles;
            Vector3[] vv = mesh.vertices;
            for (int i = 0; i < tr.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(vv[tr[i + 1]] - vv[tr[i]], vv[tr[i + 2]] - vv[tr[i]]);
                Vector3 mid = (vv[tr[i]] + vv[tr[i + 1]] + vv[tr[i + 2]]) / 3f;
                if (Vector3.Dot(n, mid - centreP) < 0f) { int tmp = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = tmp; }
            }
            mesh.triangles = tr;
            mesh.uv = vv.Select(p => new Vector2(p.x + 0.5f, p.z + 0.5f + p.y)).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated")) AssetDatabase.CreateFolder("Assets/Art", "Generated");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "DuelCenter");
        }

        private static Material Mat(string name, Color colour, float metallic = 0f, float smooth = 0.5f, Color? emission = null, Texture tex = null)
        {
            EnsureFolder();
            string path = $"{MatFolder}/{name}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (emission.HasValue && emission.Value.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material GlassMat()
        {
            Material m = Mat("DC Glass", new Color(0.45f, 0.7f, 0.9f, 0.32f), metallic: 0.1f, smooth: 0.95f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LogoMat()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/DuelGenesis/DuelGenesisLogo.png");
            if (tex == null) return null;
            Material m = Mat("DC Logo", Color.white, tex: tex, emission: Color.white * 1.1f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.35f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif
