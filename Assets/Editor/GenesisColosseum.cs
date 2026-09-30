#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// The Genesis Colosseum: the new Genesis Duel Center, the greatest building in the city.
    ///
    /// Everything is an oval ring around the arena pit (the pit ellipse grown outwards by d metres), so every wall,
    /// floor, stair and seat row curves with the building:
    ///   - a stepped stone podium, three stacked arcade storeys that each step back to leave walk-out terraces,
    ///     Roman engaged columns, cornices with light strips, banners;
    ///   - a triumphal portal on the plaza side crowned by a fan of five giant cards (the Hand of Genesis);
    ///   - two curved colonnade arms reaching out to the plaza, their roofs walkable, ending in spiral-stair towers;
    ///   - inside: the ground-floor Grand Concourse and Foyer (pack counter, Genesis Cup, Hall of Legends),
    ///     Level 1 Trading Floor (booths and trade tables), Level 2 Tournament Balcony (duel tables overlooking the
    ///     arena), and the roof-top Champions' Terrace, joined by curved stone staircases;
    ///   - the arena: a pit with the championship stage, player tunnels, a two-tier seating bowl with aisles,
    ///     a spinning centre-hung jumbotron, and a floating saddle-shaped halo roof carried by arched ribs.
    /// All geometry is generated here; textures are Poly Haven (CC0). Local +Z faces the plaza.
    /// </summary>
    public static partial class GenesisDuelCenter
    {
        public static readonly Vector3 ColosseumCentre = new Vector3(0f, 0f, 126f);
        public const float ColosseumYaw = 180f;
        private const string ColFolder = "Assets/Art/Generated/Colosseum";

        // Plan: the arena pit is an ellipse (semi-axes PitA x PitB); every ring is that ellipse offset outwards by d metres.
        private const float PitA = 15f, PitB = 19f;
        // Floor levels (top of slab) and heights.
        private const float L0 = 1.5f, L1 = 6f, L2 = 12.5f, L3 = 18.5f, PitY = 0.15f, SlabT = 0.45f;
        private const float LowerFront = 0.6f, LowerBack = 9.6f, UpperFront = 12.6f, UpperBack = 21.6f;
        private const float Arcade0 = 22f, Facade0 = 31f, Facade1 = 29f, Facade2 = 27.5f, PodiumIn = 32f, PodiumOut = 35f;
        private const int LowerRows = 9, UpperRows = 12;
        private const float CanopyIn = 6f, CanopyOut = 25.5f, CanopyThick = 0.6f, JumboY = 16.5f;

        private static float YIn(float th) => 27.5f + 2.5f * Mathf.Cos(2f * th);    // the halo roof rises at the ends (a saddle)
        private static float YOut(float th) => 23.4f + 1.6f * Mathf.Cos(2f * th);

        private static int _colMesh, _colLights, _colTables;

        /// <summary>True inside the Colosseum's footprint (podium, colonnade arms, towers, forecourt), world XZ.</summary>
        public static bool InColosseum(Vector2 world, float pad = 0f)
        {
            Vector3 l = Quaternion.Euler(0f, -ColosseumYaw, 0f) * new Vector3(world.x - ColosseumCentre.x, 0f, world.y - ColosseumCentre.z);
            float a = PitA + PodiumOut + 2f + pad, b = PitB + PodiumOut + 2f + pad;
            if (l.x * l.x / (a * a) + l.z * l.z / (b * b) <= 1f) return true;
            return Mathf.Abs(l.x) < 46f + pad && l.z > 20f && l.z < 66f + pad;
        }

        // ================================================================== menus

        [MenuItem("Duel Genesis/World/22. Build The Genesis Colosseum (the Duel Center)")]
        public static void BuildColosseumMenu()
        {
            GameObject city = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GenesisWorldBuilder.CityRootName);
            if (city == null || city.transform.Find("Open World Marker") == null) { Debug.LogWarning("Duel: Genesis: build the open city first (World > 9)."); return; }
            var sw = Stopwatch.StartNew();
            BuildColosseum(city.transform, ColosseumCentre, ColosseumYaw);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Duel: Genesis built the Genesis Colosseum in {sw.Elapsed.TotalSeconds:0.0} s: {_colMesh} meshes, {_colLights} lights, {_colTables} duel tables. Re-bake the minimap (World > 17).");
        }

        [MenuItem("Duel Genesis/DEV/Capture Colosseum Screenshots")]
        public static void CaptureColosseum()
        {
            GameObject hall = GameObject.Find(RootName);
            if (hall == null) { Debug.LogWarning("Duel: Genesis: build the Colosseum first."); return; }
            Transform r = hall.transform;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Shots");
            Directory.CreateDirectory(dir);
            Vector3 W(float th, float d, float y) => r.TransformPoint(EF(th * Mathf.Deg2Rad, d, y));
            GenesisShots.Shot(dir, "col_1_plaza", r.TransformPoint(new Vector3(0f, 2f, 92f)), r.TransformPoint(new Vector3(0f, 14f, 20f)), 70f);
            GenesisShots.Shot(dir, "col_2_aerial", r.TransformPoint(new Vector3(70f, 70f, 110f)), r.TransformPoint(new Vector3(0f, 8f, 0f)), 60f);
            GenesisShots.Shot(dir, "col_3_side", r.TransformPoint(new Vector3(95f, 6f, -10f)), r.TransformPoint(new Vector3(0f, 14f, 0f)), 65f);
            GenesisShots.Shot(dir, "col_4_foyer", W(0f, 30f, L0 + 1.7f), W(0f, 14f, L0 + 3f), 80f);
            GenesisShots.Shot(dir, "col_5_arena", W(30f, 20.5f, L2 + 1.7f), new Vector3(r.position.x, 3f, r.position.z), 75f);
            GenesisShots.Shot(dir, "col_6_pit", r.TransformPoint(new Vector3(0f, 1.8f, 13f)), r.TransformPoint(new Vector3(0f, 6f, -10f)), 80f);
            GenesisShots.Shot(dir, "col_7_concourse", W(-20f, 16f, L0 + 1.7f), W(20f, 17f, L0 + 1.8f), 80f);
            GenesisShots.Shot(dir, "col_8_trading", W(-10f, 26f, L1 + 1.7f), W(35f, 24f, L1 + 1.5f), 80f);
            GenesisShots.Shot(dir, "col_9_balcony", W(-60f, 25f, L2 + 1.7f), W(-5f, 23f, L2 + 1f), 80f);
            GenesisShots.Shot(dir, "col_10_terrace", W(-60f, 25f, L3 + 1.7f), W(30f, 25f, L3 + 6f), 80f);
            GenesisShots.Shot(dir, "col_11_arm", r.TransformPoint(new Vector3(8f, 1.7f, 48f)), r.TransformPoint(new Vector3(30f, 4f, 52f)), 75f);
            Debug.Log("Duel: Genesis captured Colosseum screenshots into " + dir);
        }

        // ================================================================== build

        private sealed class ColMats
        {
            public Material Stone, Trim, Floor, Arena, Paving, Metal, Bronze, Concrete, SeatRed, SeatBlue, SeatGold, Glass;
            public Material Cyan, Magenta, Gold, Warm, CanopyUnder, Screen, Board, Wood, Cloth, Logo, Dark;
        }

        public static GameObject BuildColosseum(Transform city, Vector3 centre, float yaw)
        {
            EnsureFolder();
            EnsureFolder(VaultFolder);
            if (!AssetDatabase.IsValidFolder(ColFolder)) AssetDatabase.CreateFolder("Assets/Art/Generated", "Colosseum");
            Transform old = city.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            int cleared = ClearColosseumSite(city, centre, yaw);
            Physics.SyncTransforms();

            _colMesh = 0; _colLights = 0; _colTables = 0;
            var root = new GameObject(RootName).transform;
            root.SetParent(city, false);
            root.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            ColMats m = MakeColMats();

            Transform shell = Child(root, "Structure"), stands = Child(root, "Seating Bowl"), arena = Child(root, "Arena");
            Transform interior = Child(root, "Interior"), outside = Child(root, "Forecourt"), halo = Child(root, "Halo Roof");

            BuildPodiumAndFloors(shell, m, out var portalHalf);
            BuildBowl(stands, arena, m);
            BuildFacades(shell, m, portalHalf);
            BuildPortal(shell, m, portalHalf);
            var flights = BuildStairs(shell, interior, m);
            BuildSlabs(shell, m, portalHalf, flights);
            BuildHalo(halo, arena, m);
            BuildArena(arena, m);
            BuildArms(outside, m);
            BuildForecourt(outside, m);
            BuildInterior(interior, m);
            BuildLighting(root, m);

            var arrive = new GameObject(RootName + " - Arrival Spot");
            arrive.transform.SetParent(root, false);
            arrive.transform.localPosition = new Vector3(0f, 0.1f, 62f);
            arrive.transform.localRotation = Quaternion.LookRotation(Vector3.back);

            Debug.Log($"Duel: Genesis cleared {cleared} buildings, trees and props from the Colosseum site.");
            return root.gameObject;
        }

        private static ColMats MakeColMats()
        {
            var m = new ColMats
            {
                Stone = ColMat("GC Sandstone", "stone", new Color(1f, 0.95f, 0.86f), 0.12f, 3.2f),
                Trim = ColMat("GC Travertine", "trim", new Color(0.98f, 0.94f, 0.86f), 0.25f, 2.2f),
                Floor = ColMat("GC Floor", "floor", new Color(0.93f, 0.91f, 0.88f), 0.55f, 2.4f),
                Arena = ColMat("GC Arena Floor", "floor", new Color(0.2f, 0.19f, 0.26f), 0.8f, 3f),
                Paving = ColMat("GC Paving", "paving", new Color(0.86f, 0.84f, 0.8f), 0.2f, 2.5f),
                Metal = ColMat("GC Titanium", "metal", new Color(0.24f, 0.25f, 0.28f), 0.62f, 2f, 0.85f),
                Bronze = ColMat("GC Bronze", "metal", new Color(0.58f, 0.4f, 0.2f), 0.72f, 1.5f, 0.9f),
                Concrete = ColMat("GC Stand Stone", "trim", new Color(0.66f, 0.65f, 0.68f), 0.2f, 2.5f),
                SeatRed = ColMat("GC Seat Crimson", "fabric", new Color(0.62f, 0.07f, 0.1f), 0.3f, 0.8f),
                SeatBlue = ColMat("GC Seat Midnight", "fabric", new Color(0.1f, 0.15f, 0.42f), 0.3f, 0.8f),
                SeatGold = ColMat("GC Seat Gold", "fabric", new Color(0.8f, 0.58f, 0.16f), 0.35f, 0.8f),
                Glass = GlassMat(),
                Cyan = Mat("DC Glow Cyan", Cyan, emission: Cyan * 2.2f),
                Magenta = Mat("DC Glow Magenta", Magenta, emission: Magenta * 2f),
                Gold = Mat("DC Glow Gold", Gold, emission: Gold * 1.6f),
                Warm = Mat("GC Warm Light", new Color(1f, 0.93f, 0.8f), emission: new Color(1f, 0.88f, 0.7f) * 2.4f),
                CanopyUnder = Mat("GC Halo Underside", new Color(0.92f, 0.93f, 0.97f), smooth: 0.45f, emission: new Color(0.75f, 0.82f, 1f) * 0.35f),
                Screen = Mat("DC Screen", Color.white, smooth: 0.8f, emission: Color.white * 1.4f),
                Board = Mat("DC Booth Board", new Color(0.05f, 0.06f, 0.1f), smooth: 0.5f),
                Wood = TexMat("DC Wood", "wooden_panels", new Color(0.95f, 0.9f, 0.85f), 0.35f, 2f),
                Cloth = Mat("GC Banner Cloth", new Color(0.26f, 0.05f, 0.32f), smooth: 0.2f),
                Logo = LogoMat(),
                Dark = Mat("GC Dark Stone", new Color(0.12f, 0.12f, 0.15f), smooth: 0.6f),
            };
            return m;
        }

        // ================================================================== podium, floors, slabs

        private static void BuildPodiumAndFloors(Transform shell, ColMats m, out (float c, float h) portal)
        {
            portal = GapW(0f, 19f, 33f);
            var gPortal = new[] { portal };

            // Ground floor: a solid stone base from the back of the lower stands out to the podium, marble on top.
            var baseM = new SmoothMesh(2);
            RingSweep(baseM, RectP(LowerBack, PodiumIn, 0f, L0 - 0.04f), PodiumIn, 1.2f, null, new Subs(0));
            RingSweep(baseM, RectP(LowerBack, PodiumIn, L0 - 0.04f, L0), PodiumIn, 1.2f, null, new Subs(0, 1, 0, 0));
            RangeSweep(baseM, RectP(PodiumIn - 0.5f, 33.7f, 0f, L0), 33.7f, 1f, portal.c - portal.h, portal.c + portal.h, new Subs(0, 1, 0, 0));
            Part(shell, "Ground Floor", baseM, new[] { m.Stone, m.Floor }, collider: true);

            // The podium: five stone steps all round (a smooth ramp collider under them), and grand steps up to the portal.
            var steps = new SmoothMesh();
            RingSweep(steps, StepProfile(PodiumIn, L0, 5, 0.6f, 0.3f), PodiumOut, 1.2f, gPortal, new Subs(0), fan: 0);
            RangeSweep(steps, StepProfile(33.7f, L0, 6, 0.6f, 0.25f), 37.3f, 1f, portal.c - portal.h, portal.c + portal.h, new Subs(0), fan: 0);
            Part(shell, "Podium Steps", steps, new[] { m.Trim }, collider: false);
            var ramp = new SmoothMesh();
            RingSweep(ramp, new[] { new Vector2(PodiumOut, 0f), new Vector2(PodiumIn, L0) }, PodiumOut, 1.2f, gPortal, new Subs(0), closed: false);
            RangeSweep(ramp, new[] { new Vector2(37.3f, 0f), new Vector2(33.7f, L0) }, 37.3f, 1f, portal.c - portal.h, portal.c + portal.h, new Subs(0), caps: false, closed: false);
            Part(shell, "Podium Ramp (collider)", ramp, new[] { m.Trim }, collider: true, render: false);

            // Base moulding round the foot of the facade.
            var mould = new SmoothMesh();
            float dF = Facade0 + 0.65f;
            RingSweep(mould, new[] { new Vector2(dF - 0.1f, L0), new Vector2(dF + 0.18f, L0), new Vector2(dF + 0.22f, L0 + 0.1f), new Vector2(dF + 0.12f, L0 + 0.28f), new Vector2(dF - 0.1f, L0 + 0.28f) },
                dF, 0.8f, gPortal, new Subs(0));
            Part(shell, "Base Moulding", mould, new[] { m.Trim }, collider: false);
        }

        /// <summary>Upper floors: bands of slab with holes for the foyer void, stair wells and the portal.</summary>
        private static void BuildSlabs(Transform shell, ColMats m, (float c, float h) portal, Dictionary<int, List<(float c, float h)>> holes)
        {
            (float c, float h) foyer = (0f, 14f * Mathf.Deg2Rad);
            var subs = new Subs(0, 1, 2, 0);   // edges stone, top floor, underside plaster
            var mats = new[] { m.Stone, m.Floor, m.Trim };

            var l1 = new SmoothMesh(3);
            RingSweep(l1, RectP(LowerBack, UpperBack, L1 - SlabT, L1), UpperBack, 1.2f, null, subs);
            RingSweep(l1, RectP(UpperBack, 25f, L1 - SlabT, L1), 25f, 1.2f, new[] { foyer }, subs);
            RingSweep(l1, RectP(25f, 28.6f, L1 - SlabT, L1), 28.6f, 1.2f, holes[0].Append(foyer), subs);
            RingSweep(l1, RectP(28.6f, Facade0 + 0.35f, L1 - SlabT, L1), Facade0, 1.2f, new[] { portal }, subs);
            Part(shell, "Level 1 Floor", l1, mats, collider: true);

            var l2 = new SmoothMesh(3);
            RingSweep(l2, RectP(UpperBack, 23.5f, L2 - SlabT, L2), 23.5f, 1.2f, null, subs);
            RingSweep(l2, RectP(23.5f, 27.2f, L2 - SlabT, L2), 27.2f, 1.2f, holes[1], subs);
            RingSweep(l2, RectP(27.2f, Facade1 + 0.3f, L2 - SlabT, L2), Facade1, 1.2f, null, subs);
            Part(shell, "Level 2 Floor", l2, mats, collider: true);

            var l3 = new SmoothMesh(3);
            RingSweep(l3, RectP(UpperBack, 23f, L3 - SlabT, L3), 23f, 1.2f, null, subs);
            RingSweep(l3, RectP(23f, 26.8f, L3 - SlabT, L3), 26.8f, 1.2f, holes[2], subs);
            RingSweep(l3, RectP(26.8f, Facade2 + 0.4f, L3 - SlabT, L3), Facade2, 1.2f, null, subs);
            Part(shell, "Roof Terrace", l3, mats, collider: true);

            // Parapet round the roof and glass railings on every open edge.
            var par = new SmoothMesh();
            RingSweep(par, RectP(Facade2 - 0.3f, Facade2 + 0.4f, L3, L3 + 1.15f), Facade2, 1f, null, new Subs(0));
            Part(shell, "Roof Parapet", par, new[] { m.Stone }, collider: true);
            Cornice(shell, "Roof Parapet Cap", Facade2 + 0.4f, L3 + 1.15f, 0.35f, null, m);

            Railing(shell, "Roof Inner Railing", UpperBack + 0.2f, L3, null, m);
            foreach (float s in new[] { -1f, 1f })
                RadialRailing(shell, "Foyer Balcony Railing Side", foyer.h * s, UpperBack, Facade1 - 0.5f, L1, m);
        }

        // ================================================================== seating bowl

        private static readonly (float deg, float width)[] LowerTunnels = { (0f, 7f), (90f, 3.6f), (-90f, 3.6f), (180f, 4.2f) };
        private static readonly (float deg, float width)[] Vomitories = { (45f, 3.4f), (-45f, 3.4f), (135f, 3.4f), (-135f, 3.4f) };

        private static float LowerY(int k) => L0 + 0.5f * (k + 1);
        private static float LowerD(int k) => LowerFront + 1f * k;
        private static float UpperY(int k) => L1 + (L2 - L1) / UpperRows * (k + 1);
        private static float UpperD(int k) => UpperFront + 0.75f * k;

        private static void BuildBowl(Transform stands, Transform arena, ColMats m)
        {
            var lowerGaps = LowerTunnels.Select(t => GapW(t.deg, t.width, 4.8f)).ToList();
            var upperGaps = Vomitories.Select(t => GapW(t.deg, t.width, 17f)).ToList();

            // ---- lower tier: pit wall, nine rows, back wall to the concourse (stone), filled to the ground
            var lp = new List<Vector2> { new Vector2(0f, 0f), new Vector2(LowerBack, 0f), new Vector2(LowerBack, L1) };
            for (int k = LowerRows - 1; k >= 0; k--)
            {
                float d = LowerD(k), y = LowerY(k);
                lp.Add(new Vector2(d, y));
                if (k > 0) lp.Add(new Vector2(d, LowerY(k - 1)));
            }
            lp.Add(new Vector2(LowerFront, 1.6f));
            lp.Add(new Vector2(0f, 1.6f));
            var lower = new SmoothMesh(2);
            RingSweep(lower, lp, LowerBack, 0.9f, lowerGaps, new Subs(0, 0, 0, 1), fan: 1);
            Part(stands, "Lower Tier", lower, new[] { m.Concrete, m.Stone }, collider: true);
            var ledLower = new SmoothMesh();
            RingSweep(ledLower, RectP(-0.04f, 0f, 1.25f, 1.38f), 0f, 0.8f, lowerGaps, new Subs(0));
            Part(stands, "Pit Wall Light", ledLower, new[] { m.Cyan }, collider: false, shadows: false);

            // ---- upper tier: twelve steeper rows standing on Level 1, back wall to the Trading Floor (stone)
            var up = new List<Vector2> { new Vector2(UpperFront, L1), new Vector2(UpperBack, L1), new Vector2(UpperBack, L2) };
            for (int k = UpperRows - 1; k >= 0; k--)
            {
                float d = UpperD(k), y = UpperY(k);
                up.Add(new Vector2(d, y));
                up.Add(new Vector2(d, k > 0 ? UpperY(k - 1) : L1));
            }
            up.RemoveAt(up.Count - 1);   // the last point repeats the first
            var upper = new SmoothMesh(2);
            RingSweep(upper, up, UpperBack, 0.9f, upperGaps, new Subs(0, 0, 0, 1), fan: 1);
            Part(stands, "Upper Tier", upper, new[] { m.Concrete, m.Stone }, collider: true);

            // ---- seats and aisles
            BuildSeats(stands, "Lower Seats", lowerGaps, 4.8f, LowerRows, LowerD, LowerY, 1f, k => m.SeatRed, m,
                LowerFront, LowerY(0), LowerD(LowerRows - 1), L1, LowerBack, out var lowerAisles);
            BuildSeats(stands, "Upper Seats", upperGaps, 17f, UpperRows, UpperD, UpperY, 0.75f, k => k < 2 ? m.SeatGold : m.SeatBlue, m,
                UpperFront - 1f, L1, UpperD(UpperRows - 1), L2, UpperBack, out var upperAisles);

            // Railings: Level 1 club edge over the lower tier (open at the aisles), Level 2 edge over the upper tier.
            Railing(stands, "Club Level Railing", LowerBack + 0.15f, L1, lowerAisles.Select(a => GapW(a * Mathf.Rad2Deg, 1.6f, LowerBack)).ToList(), m);
            Railing(stands, "Balcony Railing", UpperBack + 0.15f, L2, upperAisles.Select(a => GapW(a * Mathf.Rad2Deg, 1.6f, UpperBack)).ToList(), m);

            // ---- player tunnels from the concourse down to the pit
            var tunnel = new SmoothMesh();
            var glow = new SmoothMesh();
            foreach (var (c, h) in lowerGaps)
            {
                float Y(float d) => d < LowerFront ? PitY : Mathf.Lerp(PitY, L0, (d - LowerFront) / (LowerBack - LowerFront));
                GridTop(tunnel, c - h, c + h, -0.4f, LowerBack + 0.05f, Y, 8, 14);
                foreach (float e in new[] { c - h * 0.97f, c + h * 0.97f })
                {
                    var pts = new List<Vector3>();
                    for (int i = 0; i <= 12; i++) { float d = Mathf.Lerp(0.2f, LowerBack, i / 12f); pts.Add(EF(e, d, Y(d) + 1.05f)); }
                    PolyTube(glow, 0, pts, 0.05f, 8);
                }
            }
            Part(arena, "Player Tunnels", tunnel, new[] { m.Floor }, collider: true);
            Part(arena, "Tunnel Lights", glow, new[] { m.Gold }, collider: false, shadows: false);
        }

        private static void BuildSeats(Transform parent, string name, List<(float c, float h)> tierGaps, float dMid, int rows, Func<int, float> dOf, Func<int, float> yOf,
            float rowDepth, Func<int, Material> mat, ColMats m, float rampD0, float rampY0, float rampD1, float rampY1, float dBack, out List<float> aisles)
        {
            // Aisles every ~9 m round each section.
            aisles = new List<float>();
            foreach (var (a, b) in Spans(tierGaps))
            {
                var at = new ArcTable(a, b, dMid);
                int n = Mathf.Max(2, Mathf.RoundToInt(at.Total / 9f));
                for (int i = 1; i < n; i++) aisles.Add(at.At(at.Total * i / n));
            }
            var gaps = tierGaps.Concat(aisles.Select(t => (t, 0.7f / Speed(t, dMid)))).ToList();

            var byMat = new Dictionary<Material, SmoothMesh>();
            for (int k = 0; k < rows; k++)
            {
                Material mt = mat(k);
                if (!byMat.TryGetValue(mt, out var sm)) byMat[mt] = sm = new SmoothMesh();
                float d = dOf(k), y = yOf(k), s0 = d + rowDepth * 0.18f, s1 = d + rowDepth * 0.78f, s2 = d + rowDepth * 0.9f;
                var seat = new[] { new Vector2(s0, y), new Vector2(s2, y), new Vector2(s2, y + 0.92f), new Vector2(s1, y + 0.92f), new Vector2(s1, y + 0.42f), new Vector2(s0, y + 0.42f) };
                RingSweep(sm, seat, d, 0.5f, gaps, new Subs(0), fan: 1);
            }
            foreach (var kv in byMat) Part(parent, name + " - " + kv.Key.name, kv.Value, new[] { kv.Key }, collider: false);

            // Aisle ramps (invisible, so the player walks smoothly up and down the steps) and aisle step lights.
            var ramps = new SmoothMesh();
            var lights = new SmoothMesh();
            foreach (float t in aisles)
            {
                float h = 0.72f / Speed(t, dMid);
                float Y(float dd) => dd <= rampD0 ? rampY0 : dd >= rampD1 ? rampY1 : Mathf.Lerp(rampY0, rampY1, (dd - rampD0) / (rampD1 - rampD0));
                GridTop(ramps, t - h, t + h, rampD0 - 0.02f, dBack + 0.05f, Y, 3, 16);
                for (int k = 0; k < rows; k++)
                {
                    float d = dOf(k), y = yOf(k);
                    RangeSweep(lights, RectP(d - 0.03f, d + 0.02f, y - 0.04f, y + 0.004f), d, 0.5f, t - h * 0.8f, t + h * 0.8f, new Subs(0));
                }
            }
            Part(parent, name + " Aisles (collider)", ramps, new[] { m.Concrete }, collider: true, render: false);
            Part(parent, name + " Aisle Lights", lights, new[] { m.Cyan }, collider: false, shadows: false);
        }

        // ================================================================== facades

        private struct Bay { public bool Open, Glazed; public float W, Spring; }

        private static void BuildFacades(Transform shell, ColMats m, (float c, float h) portal)
        {
            var noGaps = new List<(float c, float h)>();
            var gP0 = new List<(float c, float h)> { portal };
            var gP1 = gP0;
            var foyer = new List<(float c, float h)> { (0f, 14f * Mathf.Deg2Rad) };

            // Ground-floor facade: heavy rusticated arcade, open arches (the Colosseum way in from every side).
            var w0 = new SmoothMesh(2);
            List<float> piers0 = ArchWall(w0, null, Facade0, 1.3f, L0, L1 - SlabT, Spans(gP0), false, 6.2f,
                i => new Bay { Open = true, W = 3.4f, Spring = L0 + 2.1f }, 0, 1);
            Part(shell, "Ground Arcade", w0, new[] { m.Stone, m.Trim }, collider: true);

            // Inner arcade between the Grand Concourse and the outer gallery.
            var wi = new SmoothMesh(2);
            ArchWall(wi, null, Arcade0, 0.9f, L0, L1 - SlabT, Spans(foyer), false, 7.4f,
                i => new Bay { Open = true, W = 4f, Spring = L0 + 1.9f }, 0, 1);
            Part(shell, "Concourse Arcade", wi, new[] { m.Stone, m.Trim }, collider: true);

            // Level 1: tall arched windows, every fifth one a door onto the terrace.
            var w1 = new SmoothMesh(2);
            var g1 = new SmoothMesh();
            List<float> piers1 = ArchWall(w1, g1, Facade1, 1f, L1, L2 - SlabT, Spans(gP1), false, 6.1f,
                i => new Bay { Open = true, Glazed = i % 5 != 2, W = 3.2f, Spring = L1 + 3.3f }, 0, 1);
            Part(shell, "Trading Floor Facade", w1, new[] { m.Stone, m.Trim }, collider: true);
            Part(shell, "Trading Floor Windows", g1, new[] { m.Glass }, collider: true, shadows: false);

            // Level 2: smaller arched windows between pilasters, every sixth a door onto the upper terrace.
            var w2 = new SmoothMesh(2);
            var g2 = new SmoothMesh();
            List<float> piers2 = ArchWall(w2, g2, Facade2, 0.8f, L2, L3 - SlabT, Spans(noGaps), true, 5.5f,
                i => new Bay { Open = true, Glazed = i % 6 != 3, W = 2.6f, Spring = L2 + 2.9f }, 0, 1);
            Part(shell, "Balcony Facade", w2, new[] { m.Trim, m.Trim }, collider: true);
            Part(shell, "Balcony Windows", g2, new[] { m.Glass }, collider: true, shadows: false);

            // Engaged columns (Tuscan below, slimmer above) and flat pilasters on the top storey.
            var cols = new SmoothMesh();
            foreach (float t in piers0) Lathe(cols, 0, EF(t, Facade0 + 0.65f + 0.22f, L0), ColumnProfile(0.4f, L1 - SlabT - L0 - 0.05f));
            foreach (float t in piers1) Lathe(cols, 0, EF(t, Facade1 + 0.5f + 0.18f, L1), ColumnProfile(0.33f, L2 - SlabT - L1 - 0.05f));
            Part(shell, "Engaged Columns", cols, new[] { m.Trim }, collider: false);
            var pil = new SmoothMesh();
            foreach (float t in piers2)
            {
                float h = 0.35f / Speed(t, Facade2);
                RangeSweep(pil, RectP(Facade2 + 0.4f, Facade2 + 0.62f, L2, L3 - SlabT), Facade2, 0.2f, t - h, t + h, new Subs(0));
            }
            Part(shell, "Pilasters", pil, new[] { m.Trim }, collider: false);

            // Cornices with a light strip under each, and the terraces' railings.
            Cornice(shell, "Ground Cornice", Facade0 + 0.65f, L1 - 0.55f, 0.6f, gP0, m);
            Cornice(shell, "Level 1 Cornice", Facade1 + 0.5f, L2 - 0.55f, 0.55f, gP1, m);
            Cornice(shell, "Level 2 Cornice", Facade2 + 0.4f, L3 - 0.55f, 0.5f, null, m);
            Railing(shell, "Level 1 Terrace Railing", Facade0 + 0.2f, L1, gP0, m);
            Railing(shell, "Level 2 Terrace Railing", Facade1 + 0.15f, L2, null, m);

            // Banners hanging on every seventh Level 1 pier.
            for (int i = 0; i < piers1.Count; i += 7)
            {
                float t = piers1[i];
                Vector3 n = EOut(t);
                Vector3 at = EF(t, Facade1 + 1.05f, L1 + 3.2f);
                if (Mathf.Abs(Mathf.DeltaAngle(t * Mathf.Rad2Deg, 0f)) < 18f) continue;
                Quad(shell, "Banner", at, new Vector2(1.5f, 4.6f), YawFacing(n), m.Cloth);
                if (m.Logo != null) Quad(shell, "Banner Emblem", at + n * 0.02f + Vector3.up * 1.1f, new Vector2(1.25f, 1.25f), YawFacing(n), m.Logo);
                Box(shell, "Banner Rod", at + Vector3.up * 2.35f + n * 0.02f, new Vector3(1.8f, 0.07f, 0.07f), m.Bronze, collider: false).transform.localRotation = Quaternion.Euler(0f, YawForward(n), 0f);
            }
        }

        // ================================================================== the grand portal and the Hand of Genesis

        private static void BuildPortal(Transform shell, ColMats m, (float c, float h) portal)
        {
            float a = portal.c - portal.h, b = portal.c + portal.h;
            var w = new SmoothMesh(2);
            ArchWall(w, null, 31.25f, 4.5f, L0, 17.5f, new List<(float a, float b)> { (a, b) }, false, 100f,
                i => new Bay { Open = true, W = 8f, Spring = L0 + 6.2f }, 0, 1);
            Part(shell, "Grand Portal", w, new[] { m.Stone, m.Trim }, collider: true);

            // Entablature, cornice and attic trim across the front.
            var ent = new SmoothMesh();
            RangeSweep(ent, RectP(33.5f, 34.1f, 13.9f, 14.9f), 34.1f, 0.6f, a, b, new Subs(0));
            Part(shell, "Portal Entablature", ent, new[] { m.Trim }, collider: false);
            Cornice(shell, "Portal Cornice", 34.1f, 14.9f, 0.55f, null, m, onlyRange: (a, b));
            Cornice(shell, "Portal Attic Cap", 33.5f, 17.5f, 0.45f, null, m, onlyRange: (a, b));

            // Four giant columns, and a gold light line round the arch.
            var at = new ArcTable(-0.6f, 0.6f, 34.2f);
            var cols = new SmoothMesh();
            foreach (float u in new[] { -8.2f, -5.4f, 5.4f, 8.2f })
                Lathe(cols, 0, EF(at.At(at.Total * 0.5f + u), 34.2f, L0), ColumnProfile(0.72f, 13.9f - L0));
            Part(shell, "Portal Columns", cols, new[] { m.Trim }, collider: true);
            var glow = new SmoothMesh();
            var at2 = new ArcTable(-0.6f, 0.6f, 33.55f);
            var arch = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float t = Mathf.PI * i / 32f;
                arch.Add(EF(at2.At(at2.Total * 0.5f - 4f * Mathf.Cos(t)), 33.55f, L0 + 6.2f + 4f * Mathf.Sin(t)));
            }
            PolyTube(glow, 0, arch, 0.09f, 8);
            Part(shell, "Portal Arch Light", glow, new[] { m.Gold }, collider: false, shadows: false);

            Vector3 face = EF(0f, 33.56f, 0f);
            Sign(shell, "GENESIS COLOSSEUM", new Vector3(face.x, 16.2f, face.z + 0.05f), 180f, 0.12f, Gold);
            Sign(shell, "DUEL CENTER", new Vector3(face.x, 12.9f, face.z + 0.05f), 180f, 0.07f, Cyan);

            // The Hand of Genesis: five giant cards fanned over the portal.
            var crest = Child(shell, "Hand of Genesis");
            crest.localPosition = EF(0f, 32.3f, 17.1f);
            crest.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Mesh slab = ColSave(CardSlab(6.2f, 9f, 0.3f, 0.45f), "Crest Card");
            Material back = CardMaterial("Card Back Anime 1", null);
            var cards = new (string art, string frame)[]
            {
                ("Black Luster Soldier", "Ritual Card Background Texture"),
                ("Red-Eyes Black Dragon", "Normal Monster Card Background"),
                ("Blue-Eyes White Dragon", "Normal Monster Card Background"),
                ("Dark Magician", "Normal Monster Card Background"),
                ("Exodia the Forbidden One", "Effect Monster Card Background"),
            };
            for (int i = 0; i < cards.Length; i++)
            {
                float ang = (i - 2) * 17f;
                var pivot = Child(crest, "Card - " + cards[i].art);
                pivot.localRotation = Quaternion.Euler(0f, 0f, -ang);
                var go = new GameObject("Card");
                go.transform.SetParent(pivot, false);
                go.transform.localPosition = new Vector3(0f, 5.6f, -0.34f * Mathf.Abs(i - 2));
                go.AddComponent<MeshFilter>().sharedMesh = slab;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { CardMaterial(cards[i].art, cards[i].frame) ?? m.Gold, back ?? m.Bronze, m.Gold };
            }
            ColPoint(crest, "Crest Wash", new Vector3(0f, 6f, 6f), 16f, 7f, new Color(1f, 0.92f, 0.8f));
        }

        // ================================================================== stairs

        /// <summary>Returns the stair wells to cut in each floor: 0 = Level 1, 1 = Level 2, 2 = roof.</summary>
        private static Dictionary<int, List<(float c, float h)>> BuildStairs(Transform shell, Transform interior, ColMats m)
        {
            var holes = new Dictionary<int, List<(float c, float h)>> { [0] = new List<(float, float)>(), [1] = new List<(float, float)>(), [2] = new List<(float, float)>() };
            var defs = new (int level, float start, float d0, float d1, float y0, float y1, string sign)[]
            {
                (0, 40f, 25f, 28.6f, L0, L1, "TRADING FLOOR"),
                (1, 58f, 23.5f, 27.2f, L1, L2, "TOURNAMENT BALCONY"),
                (2, 20f, 23f, 26.8f, L2, L3, "CHAMPIONS' TERRACE"),
            };
            var steps = new SmoothMesh(2);
            var ramps = new SmoothMesh();
            var rails = new SmoothMesh();
            var brass = new SmoothMesh();
            foreach (var def in defs)
                foreach (var (mirror, dir) in new[] { (1f, 1f), (-1f, -1f), (1f, -1f), (-1f, 1f) })
                {
                    // mirror: +1 front, -1 left; the four copies: front-right, front-left, back-right, back-left
                    float startDeg = def.start * mirror;
                    if ((mirror > 0 && dir < 0) || (mirror < 0 && dir > 0)) startDeg = mirror > 0 ? 180f - def.start : -(180f - def.start);
                    float s = startDeg * Mathf.Deg2Rad;
                    float dMid = (def.d0 + def.d1) * 0.5f;
                    float arc = (def.y1 - def.y0) / Mathf.Tan(31f * Mathf.Deg2Rad);
                    var table = new ArcTable(s, s + Mathf.Sign(dir) * 1.2f, dMid);
                    float e = table.At(arc);
                    Flight(steps, ramps, rails, brass, s, e, def.d0, def.d1, def.y0, def.y1);
                    holes[def.level].Add(((s + e) * 0.5f, Mathf.Abs(e - s) * 0.5f + 0.004f));
                    Vector3 n = EOut(s), at = EF(s, dMid, def.y0 + 3.6f);
                    HangingSign(interior, def.sign, at - ETan(s) * Mathf.Sign(e - s) * 1.2f, m.Metal, m.Gold, yaw: YawForward(ETan(s) * Mathf.Sign(e - s)), width: 6f);
                }
            Part(shell, "Staircases", steps, new[] { m.Trim, m.Gold }, collider: false);
            Part(shell, "Staircase Ramps (collider)", ramps, new[] { m.Trim }, collider: true, render: false);
            Part(shell, "Staircase Glass", rails, new[] { m.Glass }, collider: true, shadows: false);
            Part(shell, "Staircase Handrails", brass, new[] { m.Bronze }, collider: false);
            return holes;
        }

        private static void Flight(SmoothMesh steps, SmoothMesh ramps, SmoothMesh rails, SmoothMesh brass, float s, float e, float d0, float d1, float y0, float y1)
        {
            int n = Mathf.CeilToInt((y1 - y0) / 0.17f);
            for (int j = 0; j < n; j++)
            {
                float a = Mathf.Lerp(s, e, j / (float)n), b = Mathf.Lerp(s, e, (j + 1) / (float)n);
                float top = y0 + (y1 - y0) * (j + 1) / n;
                RangeSweep(steps, RectP(d0, d1, y0, top), d1, 0.6f, Mathf.Min(a, b), Mathf.Max(a, b), new Subs(0));
                // a thin gold line on each nosing
                float nose = a + (b - a) * 0.12f;
                RangeSweep(steps, RectP(d0 + 0.1f, d1 - 0.1f, top, top + 0.006f), d1, 0.6f, Mathf.Min(a, nose), Mathf.Max(a, nose), new Subs(1));
            }
            float Y(float u) => Mathf.Lerp(y0, y1, u);
            ramps.Grid(24, 2, (u, v) => { Vector3 p = EF(Mathf.Lerp(s, e, u), Mathf.Lerp(d0, d1, v), Y(u)); return (p, Vector3.up, new Vector2(p.x, p.z)); });
            // glass balustrade on the open (inner) side, continuing round the stair well upstairs
            float dr = d0 - 0.05f;
            rails.Grid(24, 1, (u, v) => { float th = Mathf.Lerp(s, e, u); Vector3 p = EF(th, dr, Y(u) + v * 1.05f); return (p, -EOut(th), new Vector2(u * 8f, v)); });
            rails.Grid(24, 1, (u, v) => { float th = Mathf.Lerp(s, e, u); Vector3 p = EF(th, dr, y1 + v * 1.05f); return (p, EOut(th), new Vector2(u * 8f, v)); });
            rails.Grid(4, 1, (u, v) => { Vector3 p = EF(s, Mathf.Lerp(d0, d1, u), y1 + v * 1.05f); return (p, -ETan(s) * Mathf.Sign(e - s), new Vector2(u, v)); });
            var hand = new List<Vector3>();
            for (int i = 0; i <= 24; i++) hand.Add(EF(Mathf.Lerp(s, e, i / 24f), dr, Y(i / 24f) + 1.08f));
            PolyTube(brass, 0, hand, 0.035f, 8);
        }

        // ================================================================== halo roof, ribs, jumbotron

        private static void BuildHalo(Transform halo, Transform arena, ColMats m)
        {
            var f = EllFrames(-Mathf.PI, Mathf.PI, CanopyOut, 1.1f);
            const int radial = 12;
            float Yc(float th, float t) => YOut(th) + (YIn(th) - YOut(th)) * Mathf.Pow(1f - t, 1.6f);   // t 0 = inner edge, 1 = outer
            float Dc(float t) => Mathf.Lerp(CanopyIn, CanopyOut, t);
            var sm = new SmoothMesh(2);
            int top = sm.V.Count;
            for (int i = 0; i < f.Count; i++)
                for (int j = 0; j <= radial; j++)
                {
                    float t = j / (float)radial, th = f.Th[i];
                    Vector3 p = EF(th, Dc(t), Yc(th, t));
                    Vector3 pr = EF(th, Dc(Mathf.Min(1f, t + 0.02f)), Yc(th, Mathf.Min(1f, t + 0.02f))) - EF(th, Dc(Mathf.Max(0f, t - 0.02f)), Yc(th, Mathf.Max(0f, t - 0.02f)));
                    Vector3 n = Vector3.Cross(pr, ETan(th)).normalized;
                    if (n.y < 0f) n = -n;
                    sm.Add(p, n, new Vector2(p.x, p.z) * 0.5f);
                }
            int bottom = sm.V.Count;
            for (int i = 0; i < f.Count; i++)
                for (int j = 0; j <= radial; j++)
                {
                    int k = top + i * (radial + 1) + j;
                    sm.Add(sm.V[k] - Vector3.up * CanopyThick, -sm.N[k], sm.UV[k]);
                }
            for (int i = 0; i < f.Count - 1; i++)
                for (int j = 0; j < radial; j++)
                {
                    int a = i * (radial + 1) + j, b = a + 1, c = a + radial + 1, d = c + 1;
                    sm.Tri(top + a, top + b, top + d, 0); sm.Tri(top + a, top + d, top + c, 0);
                    sm.Tri(bottom + a, bottom + b, bottom + d, 1); sm.Tri(bottom + a, bottom + d, bottom + c, 1);
                }
            foreach (int j in new[] { 0, radial })   // inner and outer rims
            {
                int s0 = sm.V.Count;
                for (int i = 0; i < f.Count; i++)
                {
                    Vector3 p = sm.V[top + i * (radial + 1) + j];
                    Vector3 n = EOut(f.Th[i]) * (j == 0 ? -1f : 1f);
                    sm.Add(p, n, new Vector2(i * 1.1f, 0f));
                    sm.Add(p - Vector3.up * CanopyThick, n, new Vector2(i * 1.1f, CanopyThick));
                }
                for (int i = 0; i < f.Count - 1; i++) { int k = s0 + i * 2; sm.Tri(k, k + 2, k + 3, 0); sm.Tri(k, k + 3, k + 1, 0); }
            }
            Part(halo, "Halo Roof", sm, new[] { m.Metal, m.CanopyUnder }, collider: false);

            // A cyan light ring under the inner edge.
            var ring = new SmoothMesh();
            ring.Grid(f.Count - 1, 1, (u, v) =>
            {
                float th = Mathf.Lerp(-Mathf.PI, Mathf.PI, u);
                Vector3 p = EF(th, CanopyIn + 0.1f + v * 0.35f, Yc(th, 0.02f * v) - CanopyThick - 0.02f);
                return (p, Vector3.down, new Vector2(u, v));
            });
            Part(halo, "Halo Light Ring", ring, new[] { m.Cyan }, collider: false, shadows: false);

            // Arched ribs rising from the roof terrace, over the rim and along the top of the halo.
            var ribs = new SmoothMesh();
            var ribAt = new ArcTable(-Mathf.PI, Mathf.PI, Facade2);
            const int ribCount = 28;
            for (int r = 0; r < ribCount; r++)
            {
                float th = ribAt.At(ribAt.Total * r / ribCount);
                var pts = new List<Vector3>();
                Vector3 p0 = new Vector3(27.3f, L3 + 0.1f), p1 = new Vector3(30.2f, YOut(th) + 3.4f), p2 = new Vector3(CanopyOut, YOut(th) + 0.3f);
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    Vector3 q = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                    pts.Add(EF(th, q.x, q.y));
                }
                for (int i = 1; i <= 10; i++)
                {
                    float t = 1f - i / 10f * 0.97f;
                    pts.Add(EF(th, Dc(t), Yc(th, t) + 0.28f));
                }
                PolyTube(ribs, 0, pts, 0.26f, 10);
                Lathe(ribs, 0, EF(th, 27.3f, L3), new[] { new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.55f, 0.35f), new Vector2(0.35f, 0.6f), new Vector2(0f, 0.6f) });
            }
            Part(halo, "Halo Ribs", ribs, new[] { m.Bronze }, collider: false);

            // Centre-hung jumbotron, slowly turning, on four cables.
            var jumbo = Child(arena, "Jumbotron");
            jumbo.localPosition = new Vector3(0f, JumboY, 0f);
            var spin = Child(jumbo, "Screens");
            spin.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 6f, 0f);
            Box(spin, "Core", Vector3.zero, new Vector3(5.6f, 4.8f, 5.6f), m.Metal, collider: false);
            Box(spin, "Top Glow", new Vector3(0f, 2.46f, 0f), new Vector3(5.9f, 0.14f, 5.9f), m.Cyan, collider: false);
            Box(spin, "Bottom Glow", new Vector3(0f, -2.46f, 0f), new Vector3(5.9f, 0.14f, 5.9f), m.Magenta, collider: false);
            Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < 4; i++) VideoWall(spin, "Screen " + (i + 1), dirs[i] * 2.86f, YawFacing(dirs[i]), 4.3f, m.Metal, m.Screen, i * 3);
            var cables = new SmoothMesh();
            foreach (float deg in new[] { 45f, 135f, -45f, -135f })
            {
                float th = deg * Mathf.Deg2Rad;
                Vector3 c = new Vector3(Mathf.Sin(th) * 3.6f, JumboY + 2.4f, Mathf.Cos(th) * 3.6f);
                PolyTube(cables, 0, new List<Vector3> { c, EF(th, CanopyIn + 0.4f, YIn(th) - CanopyThick) }, 0.06f, 6);
            }
            Part(arena, "Jumbotron Cables", cables, new[] { m.Metal }, collider: false);
        }

        // ================================================================== arena

        private static void BuildArena(Transform arena, ColMats m)
        {
            // Pit floor.
            var floor = new SmoothMesh();
            floor.Grid(128, 14, (u, v) =>
            {
                float th = u * Mathf.PI * 2f, r = v * 1.03f;
                var p = new Vector3(PitA * r * Mathf.Sin(th), PitY, PitB * r * Mathf.Cos(th));
                return (p, Vector3.up, new Vector2(p.x, p.z));
            });
            Part(arena, "Arena Floor", floor, new[] { m.Arena }, collider: true);
            var inlay = new SmoothMesh();
            RingSweep(inlay, RectP(-1.7f, -1.45f, PitY, PitY + 0.015f), -1.45f, 0.6f, null, new Subs(0));
            Part(arena, "Arena Inlay", inlay, new[] { m.Cyan }, collider: false, shadows: false);
            if (m.Logo != null)
                foreach (float z in new[] { -11.5f, 11.5f })
                {
                    GameObject q = Quad(arena, "Arena Emblem", new Vector3(0f, PitY + 0.02f, z), new Vector2(8f, 8f), 0f, m.Logo);
                    q.transform.localRotation = Quaternion.Euler(90f, z > 0 ? 180f : 0f, 0f);
                }

            // The championship stage: three round steps, light rings, the table, and spinning holo rings above.
            var stage = new SmoothMesh(2);
            var rings = new SmoothMesh();
            foreach (var (r, top) in new[] { (7.2f, 0.4f), (6.5f, 0.65f), (5.8f, 0.9f) })
            {
                Sweep(stage, CircleFrames(Vector3.zero, -Mathf.PI, Mathf.PI, r, 0.25f), RectP(0f, r, 0f, top), true, false, false, new Subs(0, 1, 0, 0));
                Sweep(rings, CircleFrames(Vector3.zero, -Mathf.PI, Mathf.PI, r, 0.25f), RectP(r - 0.12f, r + 0.01f, top, top + 0.012f), true, false, false, new Subs(0));
            }
            Part(arena, "Championship Stage", stage, new[] { m.Trim, m.Dark }, collider: true);
            Part(arena, "Stage Light Rings", rings, new[] { m.Gold }, collider: false, shadows: false);
            DuelTable(arena, "Championship Table", new Vector3(0f, 0.9f, 0f), 90f, 700, dealCards: true);
            _colTables++;
            foreach (var (r, y, speed, mat) in new[] { (4.6f, 5.4f, 22f, m.Cyan), (5.3f, 6.3f, -16f, m.Magenta) })
            {
                var holo = Child(arena, "Holo Ring");
                holo.localPosition = new Vector3(0f, y, 0f);
                holo.localRotation = Quaternion.Euler(9f, 0f, 0f);
                var spinT = Child(holo, "Spin");
                spinT.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, speed, 0f);
                var ringM = new SmoothMesh();
                Sweep(ringM, CircleFrames(Vector3.zero, -Mathf.PI, Mathf.PI, r, 0.3f), RectP(r - 0.06f, r + 0.06f, -0.06f, 0.06f), true, false, false, new Subs(0));
                Part(spinT, "Ring", ringM, new[] { mat }, collider: false, shadows: false);
            }

            // Tunnel names over each mouth, facing the arena.
            foreach (var (deg, label) in new[] { (0f, "CHAMPION'S WALK"), (90f, "WEST GATE"), (-90f, "EAST GATE"), (180f, "NORTH GATE") })
            {
                float th = deg * Mathf.Deg2Rad;
                Vector3 n = EOut(th);
                Sign(arena, label, EF(th, LowerBack - 0.04f, L1 - 0.28f), YawFacing(-n), 0.035f, Gold);
            }
        }

        // ================================================================== colonnade arms, spiral towers, forecourt

        private static void BuildArms(Transform outside, ColMats m)
        {
            foreach (float side in new[] { 1f, -1f })
            {
                string nm = side > 0 ? "West" : "East";
                var arm = Child(outside, nm + " Colonnade");
                Vector3 p0 = EF(38f * side * Mathf.Deg2Rad, 30.6f), p1 = new Vector3(40f * side, 0f, 50f), p2 = new Vector3(26.5f * side, 0f, 57.5f);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 48; i++)
                {
                    float t = i / 48f;
                    pts.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2);
                }
                Frames f = PathFrames(pts);
                var roof = new SmoothMesh(3);
                Sweep(roof, f, RectP(-3.2f, 3.2f, L1 - SlabT, L1), true, false, false, new Subs(0, 1, 2, 0));
                Part(arm, "Colonnade Roof", roof, new[] { m.Stone, m.Floor, m.Trim }, collider: true);
                foreach (float s in new[] { -1f, 1f })
                {
                    var cor = new SmoothMesh();
                    Sweep(cor, f, CorniceProfile(3.2f, L1 - 0.55f, 0.5f, s), true, true, true, new Subs(0));
                    Part(arm, "Colonnade Cornice", cor, new[] { m.Trim }, collider: false);
                    var led = new SmoothMesh();
                    Sweep(led, f, RectP(s * 3.2f - 0.03f, s * 3.2f + 0.03f, L1 - 0.66f, L1 - 0.6f), true, false, false, new Subs(0));
                    Part(arm, "Colonnade Light", led, new[] { m.Cyan }, collider: false, shadows: false);
                    // glass railing on the roof edge (open where it meets the terrace and the tower)
                    int i0 = 6, i1 = f.Count - 4;
                    var sub = new Frames { O = f.O.Skip(i0).Take(i1 - i0).ToArray(), S = f.S.Skip(i0).Take(i1 - i0).ToArray(), T = f.T.Skip(i0).Take(i1 - i0).ToArray(), Th = f.Th.Skip(i0).Take(i1 - i0).ToArray() };
                    var rail = new SmoothMesh();
                    Sweep(rail, sub, RectP(s * 3.05f - 0.03f, s * 3.05f + 0.03f, L1, L1 + 1.05f), true, true, true, new Subs(0));
                    Part(arm, "Colonnade Railing", rail, new[] { m.Glass }, collider: true, shadows: false);
                    var top = new SmoothMesh();
                    Sweep(top, sub, RectP(s * 3.05f - 0.05f, s * 3.05f + 0.05f, L1 + 1.05f, L1 + 1.12f), true, true, true, new Subs(0));
                    Part(arm, "Colonnade Handrail", top, new[] { m.Bronze }, collider: false);
                }

                // Two rows of columns, every ~3.4 m, starting clear of the facade.
                var cols = new SmoothMesh();
                float run = 0f, next = 4.5f;
                for (int i = 1; i < f.Count; i++)
                {
                    run += Vector3.Distance(f.O[i], f.O[i - 1]);
                    if (run < next || i > f.Count - 3) continue;
                    next += 3.4f;
                    foreach (float s in new[] { -2.6f, 2.6f }) Lathe(cols, 0, f.O[i] + f.S[i] * s, ColumnProfile(0.34f, L1 - SlabT));
                    ColPoint(arm, "Colonnade Light", f.O[i] + Vector3.up * (L1 - 1f), 8f, 1.6f, new Color(1f, 0.88f, 0.7f));
                }
                Part(arm, "Colonnade Columns", cols, new[] { m.Trim }, collider: true);

                Vector3 dirEnd = (p2 - p1).normalized;
                Vector3 tc = p2 + dirEnd * 3.9f;
                Vector3 toArm = p2 - tc;
                SpiralTower(arm, nm + " Spire", tc, Mathf.Atan2(toArm.x, toArm.z) * Mathf.Rad2Deg, L1, 16f, m);
            }
        }

        private static void SpiralTower(Transform parent, string name, Vector3 c, float exitDeg, float exitY, float topY, ColMats m)
        {
            var t = Child(parent, name);
            const float rIn = 1.05f, rOut = 3.2f, pitch = 4.8f;
            float phi0 = exitDeg - exitY / pitch * 360f, phiEnd = phi0 + topY / pitch * 360f;
            float R(float deg) => deg * Mathf.Deg2Rad;
            Vector3 P(float deg, float r, float y) => c + new Vector3(Mathf.Sin(R(deg)) * r, y, Mathf.Cos(R(deg)) * r);
            float Yh(float deg) => topY * (deg - phi0) / (phiEnd - phi0);

            var core = new SmoothMesh();
            Lathe(core, 0, c, ColumnProfile(0.72f, topY + 3.4f));
            Part(t, "Core", core, new[] { m.Trim }, collider: true);

            var steps = new SmoothMesh(2);
            int n = Mathf.CeilToInt((phiEnd - phi0) / 10f);
            for (int j = 0; j < n; j++)
            {
                float a = phi0 + (phiEnd - phi0) * j / n, b = phi0 + (phiEnd - phi0) * (j + 1) / n, y = topY * (j + 1) / n;
                Sweep(steps, CircleFrames(c, R(a), R(b), rOut, 0.5f), RectP(rIn, rOut, y - 0.22f, y), true, true, true, new Subs(0));
                Sweep(steps, CircleFrames(c, R(b - 1.2f), R(b), rOut, 0.5f), RectP(rIn + 0.1f, rOut - 0.1f, y, y + 0.006f), true, false, false, new Subs(1));
            }
            Part(t, "Spiral Steps", steps, new[] { m.Trim, m.Gold }, collider: false);
            var ramp = new SmoothMesh();
            int nr = Mathf.CeilToInt((phiEnd - phi0) / 4f);
            ramp.Grid(nr, 2, (u, v) => { float deg = Mathf.Lerp(phi0, phiEnd, u); Vector3 p = P(deg, Mathf.Lerp(rIn, rOut, v), Yh(deg)); return (p, Vector3.up, new Vector2(p.x, p.z)); });
            Part(t, "Spiral Ramp (collider)", ramp, new[] { m.Trim }, collider: true, render: false);

            // Glass guard on the outside of the spiral (open at the bottom, at the arm and at the top).
            var guard = new SmoothMesh();
            var rail = new SmoothMesh();
            var runs = new List<(float a, float b)> { (phi0 + 45f, exitDeg - 24f), (exitDeg + 24f, phiEnd - 25f) };
            foreach (var (a, b) in runs)
            {
                if (b <= a) continue;
                int k = Mathf.CeilToInt((b - a) / 5f);
                guard.Grid(k, 1, (u, v) => { float deg = Mathf.Lerp(a, b, u); Vector3 p = P(deg, rOut + 0.05f, Yh(deg) + v * 1.1f); return (p, (p - c - Vector3.up * p.y).normalized, new Vector2(u * 4f, v)); });
                var pts = new List<Vector3>();
                for (int i = 0; i <= k; i++) { float deg = Mathf.Lerp(a, b, i / (float)k); pts.Add(P(deg, rOut + 0.05f, Yh(deg) + 1.12f)); }
                PolyTube(rail, 0, pts, 0.04f, 8);
            }
            Part(t, "Spiral Guard", guard, new[] { m.Glass }, collider: true, shadows: false);
            Part(t, "Spiral Handrail", rail, new[] { m.Bronze }, collider: false);

            // Slender columns, ring beams, the bridge to the colonnade roof, the look-out and its cupola.
            var cols = new SmoothMesh();
            for (int i = 0; i < 8; i++) Lathe(cols, 0, P(i * 45f + 22.5f, 3.85f, 0f), ColumnProfile(0.17f, topY + 3.2f));
            Sweep(cols, CircleFrames(c, -Mathf.PI, Mathf.PI, 4.1f, 0.3f), RectP(3.55f, 4.15f, exitY - 0.9f, exitY - SlabT - 0.02f), true, false, false, new Subs(0));
            Part(t, "Columns", cols, new[] { m.Trim }, collider: true);
            var bridge = new SmoothMesh(2);
            Sweep(bridge, CircleFrames(c, R(exitDeg - 24f), R(exitDeg + 24f), 4.5f, 0.3f), RectP(rOut - 0.1f, 4.6f, exitY - SlabT, exitY), true, true, true, new Subs(0, 1, 0, 0));
            Part(t, "Bridge", bridge, new[] { m.Trim, m.Floor }, collider: true);
            var look = new SmoothMesh(2);
            var hole = new List<(float a, float b)> { (R(phiEnd + 2f), R(phiEnd - 110f + 360f)) };
            foreach (var (a, b) in hole) Sweep(look, CircleFrames(c, a, b, 4.1f, 0.3f), RectP(rIn - 0.05f, 4.1f, topY - 0.3f, topY), true, true, true, new Subs(0, 1, 0, 0));
            Part(t, "Look-out", look, new[] { m.Trim, m.Floor }, collider: true);
            var par = new SmoothMesh();
            Sweep(par, CircleFrames(c, -Mathf.PI, Mathf.PI, 4.1f, 0.3f), RectP(3.95f, 4.05f, topY, topY + 1.1f), true, false, false, new Subs(0));
            Part(t, "Look-out Glass", par, new[] { m.Glass }, collider: true, shadows: false);
            var cup = new SmoothMesh(2);
            var dome = new List<Vector2> { new Vector2(0f, topY + 3.2f), new Vector2(4.5f, topY + 3.2f), new Vector2(4.5f, topY + 3.55f) };
            for (int i = 0; i <= 10; i++) { float a = Mathf.PI * 0.5f * i / 10f; dome.Add(new Vector2(4.3f * Mathf.Cos(a), topY + 3.55f + 2.8f * Mathf.Sin(a))); }
            Sweep(cup, CircleFrames(c, -Mathf.PI, Mathf.PI, 4.5f, 0.3f), dome, true, false, false, new Subs(0));
            Sweep(cup, CircleFrames(c, -Mathf.PI, Mathf.PI, 4.5f, 0.3f), RectP(4.46f, 4.56f, topY + 3.25f, topY + 3.33f), true, false, false, new Subs(1));
            PolyTube(cup, 1, new List<Vector3> { c + Vector3.up * (topY + 6.3f), c + Vector3.up * (topY + 9.5f) }, 0.12f, 8);
            Part(t, "Cupola", cup, new[] { m.Metal, m.Gold }, collider: false);
            ColPoint(t, "Look-out Light", c + Vector3.up * (topY + 2.6f), 12f, 4f, new Color(1f, 0.85f, 0.65f));
            ColPoint(t, "Spire Glow", c + Vector3.up * (topY + 7f), 16f, 3f, Cyan);
        }

        private static void BuildForecourt(Transform outside, ColMats m)
        {
            var pave = new SmoothMesh();
            float a = -44f * Mathf.Deg2Rad, b = 44f * Mathf.Deg2Rad;
            RangeSweep(pave, RectP(PodiumOut - 0.2f, 45f, 0f, 0.04f), 45f, 1.2f, a, b, new Subs(0));
            Part(outside, "Forecourt Paving", pave, new[] { m.Paving }, collider: true);
            var lines = new SmoothMesh();
            foreach (float deg in new[] { -24f, -12f, 12f, 24f })
            {
                float th = deg * Mathf.Deg2Rad, h = 0.09f / Speed(th, 40f);
                GridTop(lines, th - h, th + h, 37.5f, 44.8f, d => 0.045f, 1, 6);
            }
            Part(outside, "Forecourt Light Lines", lines, new[] { m.Cyan }, collider: false, shadows: false);

            GameObject[] sakura = GenesisLandmarkModels.Cherries();
            if (sakura.Length > 0)
                foreach (var (x, z, i) in new[] { (15f, 57f, 0), (-15f, 57f, 1), (9f, 45f, 2), (-9f, 45f, 0) })
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(sakura[i % sakura.Length], outside);
                    tree.name = "Forecourt Sakura";
                    tree.transform.localRotation = Quaternion.Euler(0f, i * 97f, 0f);
                    Bounds tb = GenesisWorldBuilder.RendererBounds(tree);
                    if (tb.size.y > 0.01f) tree.transform.localScale = Vector3.one * (7f / tb.size.y);
                    tree.transform.localPosition = new Vector3(x, 0.04f, z);
                }
        }

        // ================================================================== interior

        private static void BuildInterior(Transform interior, ColMats m)
        {
            Transform foyer = Child(interior, "Grand Foyer"), hall = Child(interior, "Hall of Legends");
            Transform trade = Child(interior, "Trading Floor (Level 1)"), tour = Child(interior, "Tournament Balcony (Level 2)"), terrace = Child(interior, "Champions' Terrace");

            // ---- Foyer: the Genesis Cup in the middle, pack counter and bracket board either side, a chandelier.
            Vector3 cup = EF(0f, 25.6f, L0);
            Box(foyer, "Trophy Plinth", cup + new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 1.4f), m.Dark);
            Box(foyer, "Trophy Case", cup + new Vector3(0f, 2f, 0f), new Vector3(1.2f, 1.6f, 1.2f), m.Glass);
            Box(foyer, "Trophy Glow", cup + new Vector3(0f, 1.22f, 0f), new Vector3(1.42f, 0.05f, 1.42f), m.Gold, collider: false);
            var trophy = Child(foyer, "Genesis Cup");
            trophy.localPosition = cup + new Vector3(0f, 1.25f, 0f);
            Disc(trophy, "Cup Base", new Vector3(0f, 0.07f, 0f), 0.26f, 0.14f, m.Metal, collider: false);
            Disc(trophy, "Cup Stem", new Vector3(0f, 0.36f, 0f), 0.06f, 0.44f, m.Gold, collider: false);
            Disc(trophy, "Cup Bowl", new Vector3(0f, 0.82f, 0f), 0.24f, 0.5f, m.Gold, collider: false);
            trophy.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 35f, 0f);
            ColPoint(foyer, "Trophy Light", cup + new Vector3(0f, 3.6f, 0f), 5f, 8f, new Color(1f, 0.9f, 0.7f));
            Sign(foyer, "GENESIS CUP", cup + new Vector3(0f, 0.8f, 0.72f), 180f, 0.02f, Gold);

            {
                Vector3 n = EOut(-12.5f * Mathf.Deg2Rad);
                var desk = Child(foyer, "Pack Counter");
                desk.localPosition = EF(-12.5f * Mathf.Deg2Rad, 27.4f, L0);
                desk.localRotation = Quaternion.Euler(0f, YawForward(-n), 0f);
                Box(desk, "Counter", new Vector3(0f, 0.55f, 0f), new Vector3(5f, 1.1f, 1f), m.Wood);
                Box(desk, "Counter Glow", new Vector3(0f, 0.85f, 0.51f), new Vector3(4.8f, 0.08f, 0.02f), m.Gold, collider: false);
                var terminal = new GameObject("Pack Counter Terminal");
                terminal.transform.SetParent(desk, false);
                terminal.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                var col = terminal.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.4f, 0f);
                col.size = new Vector3(5.2f, 0.8f, 1.3f);
                terminal.AddComponent<DuelGenesis.Shops.CardShopTerminal>().shopName = "Genesis Colosseum Pack Counter";
                PackWall(desk, new Vector3(0f, 0f, -1.3f), 0f, m.Board);
                HangingSign(desk, "BOOSTER PACKS", new Vector3(0f, 4.3f, 0f), m.Metal, m.Gold, width: 5.2f);
            }
            {
                Vector3 n = EOut(12.5f * Mathf.Deg2Rad);
                var board = Child(foyer, "Bracket Board");
                board.localPosition = EF(12.5f * Mathf.Deg2Rad, 28.5f, L0);
                board.localRotation = Quaternion.Euler(0f, YawForward(-n), 0f);
                Box(board, "Board", new Vector3(0f, 3f, 0f), new Vector3(6f, 3.4f, 0.2f), m.Board);
                Box(board, "Frame Glow", new Vector3(0f, 4.72f, 0.11f), new Vector3(6f, 0.06f, 0.02f), m.Cyan, collider: false);
                Sign(board, "GENESIS CUP  •  BRACKET", new Vector3(0f, 4.3f, 0.12f), 180f, 0.032f, Gold);
                for (int r = 0; r < 8; r++)
                    Box(board, "Bracket Line", new Vector3(-2.3f, 3.9f - r * 0.38f, 0.11f), new Vector3(1f, 0.035f, 0.02f), r % 2 == 0 ? m.Cyan : m.Magenta, collider: false);
                for (int r = 0; r < 4; r++)
                    Box(board, "Bracket Line", new Vector3(-0.9f, 3.71f - r * 0.76f, 0.11f), new Vector3(1.2f, 0.035f, 0.02f), m.Magenta, collider: false);
                for (int r = 0; r < 2; r++)
                    Box(board, "Bracket Line", new Vector3(0.5f, 3.33f - r * 1.52f, 0.11f), new Vector3(1.2f, 0.035f, 0.02f), m.Gold, collider: false);
                if (m.Logo != null) Quad(board, "Champion", new Vector3(2.1f, 2.6f, 0.12f), new Vector2(1.2f, 1.1f), 180f, m.Logo);
            }
            // The chandelier: two glowing rings turning under the foyer ceiling.
            var chand = Child(foyer, "Chandelier");
            chand.localPosition = EF(0f, 25.6f, L2 - 1.9f);
            chand.gameObject.AddComponent<DuelGenesis.Core.GenesisSpin>().degreesPerSecond = new Vector3(0f, 10f, 0f);
            var cm = new SmoothMesh(2);
            Sweep(cm, CircleFrames(Vector3.zero, -Mathf.PI, Mathf.PI, 3.8f, 0.3f), RectP(3.6f, 3.85f, 0f, 0.14f), true, false, false, new Subs(0));
            Sweep(cm, CircleFrames(Vector3.zero, -Mathf.PI, Mathf.PI, 2.5f, 0.3f), RectP(2.35f, 2.55f, -0.9f, -0.78f), true, false, false, new Subs(1));
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; PolyTube(cm, 0, new List<Vector3> { new Vector3(Mathf.Sin(a) * 3.7f, 0.1f, Mathf.Cos(a) * 3.7f), new Vector3(0f, 1.6f, 0f) }, 0.025f, 6); }
            Part(chand, "Rings", cm, new[] { m.Gold, m.Cyan }, collider: false, shadows: false);
            ColPoint(foyer, "Chandelier Light", EF(0f, 25.6f, L2 - 2.6f), 16f, 5f, new Color(1f, 0.92f, 0.8f));
            if (m.Logo != null)
            {
                GameObject q = Quad(foyer, "Foyer Floor Emblem", EF(0f, 23f, L0 + 0.012f), new Vector2(3.4f, 3.4f), 0f, m.Logo);
                q.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            }

            // ---- Grand Concourse: the Hall of Legends (giant cards on plinths) and screens on the stand walls.
            Mesh legendSlab = ColSave(CardSlab(2.3f, 3.35f, 0.1f, 0.14f), "Legend Card");
            Material back = CardMaterial("Card Back Anime 1", null);
            var legends = new (float deg, string art, string frame)[]
            {
                (28f, "Blue-Eyes White Dragon", "Normal Monster Card Background"), (-28f, "Dark Magician", "Normal Monster Card Background"),
                (62f, "Red-Eyes Black Dragon", "Normal Monster Card Background"), (-62f, "Black Luster Soldier", "Ritual Card Background Texture"),
                (118f, "Exodia the Forbidden One", "Effect Monster Card Background"), (-118f, "Blue-Eyes White Dragon", "Normal Monster Card Background"),
                (152f, "Dark Magician", "Normal Monster Card Background"), (-152f, "Red-Eyes Black Dragon", "Normal Monster Card Background"),
            };
            foreach (var (deg, art, frame) in legends)
            {
                float th = deg * Mathf.Deg2Rad;
                Vector3 n = EOut(th);
                var spot = Child(hall, "Legend - " + art);
                spot.localPosition = EF(th, 15.8f, L0);
                spot.localRotation = Quaternion.LookRotation(n);
                Box(spot, "Plinth", new Vector3(0f, 0.4f, 0f), new Vector3(2.8f, 0.8f, 1f), m.Dark);
                Box(spot, "Plinth Glow", new Vector3(0f, 0.8f, 0.51f), new Vector3(2.8f, 0.04f, 0.02f), m.Gold, collider: false);
                var card = new GameObject("Card");
                card.transform.SetParent(spot, false);
                card.transform.localPosition = new Vector3(0f, 0.8f + 1.75f, 0f);
                card.AddComponent<MeshFilter>().sharedMesh = legendSlab;
                card.AddComponent<MeshRenderer>().sharedMaterials = new[] { CardMaterial(art, frame) ?? m.Gold, back ?? m.Bronze, m.Gold };
                Sign(spot, art.ToUpperInvariant(), new Vector3(0f, 0.5f, 0.52f), 180f, 0.012f, Color.white);
                ColPoint(spot, "Legend Light", new Vector3(0f, 3.8f, 1.6f), 5f, 3f, new Color(1f, 0.93f, 0.82f));
            }
            int screen = 0;
            foreach (float deg in new[] { 22f, -22f, 68f, -68f, 112f, -112f, 158f, -158f })
            {
                float th = deg * Mathf.Deg2Rad;
                Vector3 n = EOut(th);
                VideoWall(hall, "Concourse Screen", EF(th, LowerBack + 0.12f, L0 + 2.4f), YawFacing(n), 2.5f, m.Metal, m.Screen, screen++ * 2);
            }
            foreach (var (deg, label) in new[] { (0f, "CHAMPION'S WALK  -  ARENA"), (90f, "WEST GATE  -  ARENA"), (-90f, "EAST GATE  -  ARENA"), (180f, "NORTH GATE  -  ARENA") })
            {
                float th = deg * Mathf.Deg2Rad;
                HangingSign(hall, label, EF(th, LowerBack + 1.4f, L0 + 3.3f), m.Metal, m.Cyan, yaw: YawForward(EOut(th)), width: 6.4f);
            }

            // ---- Level 1: the Trading Floor. Booths against the stand wall, round trade tables by the windows.
            int booth = 0;
            foreach (float deg in new[] { 20f, -20f, 30f, -30f, 85f, -85f, 95f, -95f, 155f, -155f, 165f, -165f })
            {
                float th = deg * Mathf.Deg2Rad;
                Vector3 n = EOut(th);
                var holder = Child(trade, "Booth Spot");
                holder.localPosition = EF(th, UpperBack + 0.1f, L1);
                holder.localRotation = Quaternion.Euler(0f, YawForward(n) + 90f, 0f);
                Booth(holder, ++booth, Vector3.zero, m.Wood, m.Board, booth % 2 == 0 ? m.Magenta : m.Cyan, null);
            }
            int tables = 0;
            foreach (float deg in new[] { 25f, -25f, 90f, -90f, 160f, -160f, 180f })
                RoundTradeTable(trade, "Trade Table " + ++tables, EF(deg * Mathf.Deg2Rad, 26.6f, L1), m);
            HangingSign(trade, "TRADING FLOOR", EF(16f * Mathf.Deg2Rad, 25f, L1 + 3.8f), m.Metal, m.Magenta, yaw: YawForward(ETan(16f * Mathf.Deg2Rad)), width: 6f);
            HangingSign(trade, "TRADING FLOOR", EF(-16f * Mathf.Deg2Rad, 25f, L1 + 3.8f), m.Metal, m.Magenta, yaw: YawForward(ETan(-16f * Mathf.Deg2Rad)), width: 6f);

            // ---- Level 2: the Tournament Balcony. Playable duel tables along the rail, looking down on the arena.
            int t = 0;
            foreach (float deg in new[] { 0f, 10f, -10f, 40f, -40f, 50f, -50f, 84f, -84f, 96f, -96f, 134f, -134f, 172f, -172f })
            {
                float th = deg * Mathf.Deg2Rad;
                DuelTable(tour, "Tournament Table " + ++t, EF(th, 24.6f, L2), YawForward(EOut(th)), 710 + t, dealCards: false);
                _colTables++;
            }
            HangingSign(tour, "TOURNAMENT BALCONY", EF(0f, 26.4f, L2 + 3.8f), m.Metal, m.Gold, yaw: YawForward(EOut(0f)), width: 7f);

            // ---- Roof: the Champions' Terrace, stone benches looking over the city.
            foreach (float deg in new[] { 0f, 50f, -50f, 90f, -90f, 130f, -130f, 180f })
            {
                float th = deg * Mathf.Deg2Rad;
                var bench = Child(terrace, "Bench");
                bench.localPosition = EF(th, 26.2f, L3);
                bench.localRotation = Quaternion.Euler(0f, YawForward(ETan(th)), 0f);
                Box(bench, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 0.14f, 0.7f), m.Trim);
                Box(bench, "Leg", new Vector3(-1f, 0.2f, 0f), new Vector3(0.3f, 0.4f, 0.6f), m.Trim);
                Box(bench, "Leg", new Vector3(1f, 0.2f, 0f), new Vector3(0.3f, 0.4f, 0.6f), m.Trim);
            }
        }

        private static void RoundTradeTable(Transform parent, string name, Vector3 at, ColMats m)
        {
            var t = Child(parent, name);
            t.localPosition = at;
            Disc(t, "Top", new Vector3(0f, 0.76f, 0f), 0.75f, 0.05f, m.Wood);
            Disc(t, "Inlay", new Vector3(0f, 0.788f, 0f), 0.62f, 0.004f, m.Cyan, collider: false);
            Disc(t, "Pedestal", new Vector3(0f, 0.38f, 0f), 0.09f, 0.72f, m.Bronze);
            Disc(t, "Foot", new Vector3(0f, 0.03f, 0f), 0.42f, 0.06f, m.Bronze, collider: false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 1.15f;
                Disc(t, "Stool", p + new Vector3(0f, 0.62f, 0f), 0.23f, 0.08f, m.SeatRed);
                Disc(t, "Stool Leg", p + new Vector3(0f, 0.3f, 0f), 0.04f, 0.6f, m.Bronze, collider: false);
            }
        }

        // ================================================================== lighting

        private static void BuildLighting(Transform root, ColMats m)
        {
            var lights = Child(root, "Lights");
            Color warm = new Color(1f, 0.87f, 0.68f);
            // Stage spotlights from the halo's inner edge.
            for (int i = 0; i < 8; i++)
            {
                float th = i * Mathf.PI / 4f + Mathf.PI / 8f;
                ColSpot(lights, "Stage Spot", EF(th, CanopyIn + 0.6f, YIn(th) - CanopyThick - 0.4f), new Vector3(0f, 1f, 0f), i % 2 == 0 ? Color.white : new Color(0.75f, 0.85f, 1f), 60f, 260f, 26f);
            }
            // Wash over the stands.
            var at = new ArcTable(-Mathf.PI, Mathf.PI, 15f);
            for (int i = 0; i < 12; i++)
            {
                float th = at.At(at.Total * i / 12f);
                ColPoint(lights, "Stand Light", EF(th, 15f, 20f), 24f, 5f, new Color(0.95f, 0.93f, 1f));
            }
            // Concourse, galleries on each level.
            foreach (var (d, y, count, range, power) in new[] { (15.8f, L1 - 1f, 14, 13f, 3.2f), (26.5f, L1 - 1.1f, 14, 12f, 3f), (25f, L2 - 1.2f, 14, 12f, 3f), (24.5f, L3 - 1.2f, 14, 12f, 3f) })
            {
                var a2 = new ArcTable(-Mathf.PI, Mathf.PI, d);
                for (int i = 0; i < count; i++)
                {
                    float th = a2.At(a2.Total * (i + 0.5f) / count);
                    ColPoint(lights, "Hall Light", EF(th, d, y), range, power, warm);
                }
            }
            // Floodlights washing the facade from the podium.
            var a3 = new ArcTable(-Mathf.PI, Mathf.PI, PodiumOut);
            for (int i = 0; i < 20; i++)
            {
                float th = a3.At(a3.Total * (i + 0.5f) / 20f);
                ColSpot(lights, "Facade Floodlight", EF(th, PodiumOut + 0.8f, 0.3f), EF(th, Facade0 - 1f, 12f), new Color(1f, 0.82f, 0.6f), 32f, 55f, 55f);
            }
        }

        // ================================================================== site

        private static int ClearColosseumSite(Transform city, Vector3 centre, float yaw)
        {
            Quaternion inv = Quaternion.Euler(0f, -yaw, 0f);
            bool Inside(Bounds b)
            {
                Vector3 l = inv * (b.center - centre);
                float ext = Mathf.Max(b.extents.x, b.extents.z) * 0.8f;
                float a = PitA + PodiumOut + 3f + ext, bb = PitB + PodiumOut + 3f + ext;
                if (l.x * l.x / (a * a) + l.z * l.z / (bb * bb) <= 1f) return true;
                return Mathf.Abs(l.x) < 47f + ext && l.z > 20f && l.z < 66f + ext * 0.3f;
            }
            int n = 0;
            foreach (string rootName in new[] { GenesisCityBlocks.RootName, "Nature", GenesisGarden.BlossomName, "Street Dressing", GenesisGarden.ParkToriiName, GenesisCityBlocks.ChinatownName })
            {
                Transform r = city.Find(rootName);
                if (r == null) continue;
                var doomed = new List<GameObject>();
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t == r || t.parent == null) continue;
                    bool item = rootName == GenesisCityBlocks.RootName ? t.GetComponent<BoxCollider>() != null && t.parent.parent == r
                              : t.parent == r ? t.childCount == 0 || t.GetComponent<LODGroup>() != null || t.GetComponent<Renderer>() != null || t.name.Contains("Tree") || t.name.Contains("Torii")
                              : t.parent.parent == r;
                    if (!item) continue;
                    Bounds b = t.GetComponentInChildren<Renderer>() != null ? GenesisWorldBuilder.RendererBounds(t.gameObject) : new Bounds(t.position, Vector3.one);
                    if (Inside(b)) doomed.Add(t.gameObject);
                }
                foreach (var g in doomed) if (g != null) { Object.DestroyImmediate(g); n++; }
            }
            return n;
        }

        // ================================================================== geometry kit

        private struct Subs
        {
            public int Def, Top, Bottom, Out;
            public Subs(int all) { Def = Top = Bottom = Out = all; }
            public Subs(int def, int top, int bottom, int outward) { Def = def; Top = top; Bottom = bottom; Out = outward; }
        }

        private sealed class Frames { public Vector3[] O, S, T; public float[] Th; public int Count => O.Length; }

        private static Vector3 EBase(float th) => new Vector3(PitA * Mathf.Sin(th), 0f, PitB * Mathf.Cos(th));
        private static Vector3 EOut(float th) => new Vector3(Mathf.Sin(th) / PitA, 0f, Mathf.Cos(th) / PitB).normalized;
        private static Vector3 ETan(float th) => new Vector3(PitA * Mathf.Cos(th), 0f, -PitB * Mathf.Sin(th)).normalized;
        /// <summary>A point on the ring d metres out from the pit edge, at angle th (0 = towards the plaza) and height y.</summary>
        private static Vector3 EF(float th, float d, float y = 0f) => EBase(th) + EOut(th) * d + new Vector3(0f, y, 0f);
        private static float Speed(float th, float d) => Vector3.Distance(EF(th + 1e-3f, d), EF(th - 1e-3f, d)) / 2e-3f;
        /// <summary>A gap in a ring: centred on deg, width metres wide measured at d.</summary>
        private static (float c, float h) GapW(float deg, float width, float d)
        {
            float c = deg * Mathf.Deg2Rad;
            return (c, width * 0.5f / Speed(c, d));
        }
        private static float YawFacing(Vector3 dir) => Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;   // quads, signs and screens show their face along -Z
        private static float YawForward(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        private sealed class ArcTable
        {
            private readonly float[] _th, _len;
            public readonly float Total;
            public ArcTable(float t0, float t1, float d)
            {
                int n = Mathf.Max(64, Mathf.CeilToInt(Mathf.Abs(t1 - t0) * 500f));
                _th = new float[n + 1];
                _len = new float[n + 1];
                Vector3 prev = EF(t0, d);
                for (int i = 0; i <= n; i++)
                {
                    _th[i] = Mathf.Lerp(t0, t1, i / (float)n);
                    Vector3 p = EF(_th[i], d);
                    _len[i] = i == 0 ? 0f : _len[i - 1] + Vector3.Distance(prev, p);
                    prev = p;
                }
                Total = _len[n];
            }
            public float At(float s)
            {
                if (s <= 0f) return _th[0];
                if (s >= Total) return _th[_th.Length - 1];
                int lo = 0, hi = _len.Length - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (_len[mid] < s) lo = mid; else hi = mid; }
                float t = (s - _len[lo]) / Mathf.Max(1e-6f, _len[hi] - _len[lo]);
                return Mathf.Lerp(_th[lo], _th[hi], t);
            }
        }

        private static Frames EllFrames(float t0, float t1, float dRef, float step)
        {
            var at = new ArcTable(t0, t1, dRef);
            int n = Mathf.Max(1, Mathf.CeilToInt(at.Total / step));
            var f = new Frames { O = new Vector3[n + 1], S = new Vector3[n + 1], T = new Vector3[n + 1], Th = new float[n + 1] };
            for (int k = 0; k <= n; k++)
            {
                float th = at.At(at.Total * k / n);
                f.Th[k] = th; f.O[k] = EBase(th); f.S[k] = EOut(th); f.T[k] = ETan(th);
            }
            return f;
        }

        private static Frames CircleFrames(Vector3 c, float t0, float t1, float rRef, float step)
        {
            int n = Mathf.Max(12, Mathf.CeilToInt(Mathf.Abs(t1 - t0) * rRef / step));
            float sg = Mathf.Sign(t1 - t0);
            var f = new Frames { O = new Vector3[n + 1], S = new Vector3[n + 1], T = new Vector3[n + 1], Th = new float[n + 1] };
            for (int k = 0; k <= n; k++)
            {
                float th = Mathf.Lerp(t0, t1, k / (float)n);
                f.Th[k] = th; f.O[k] = c;
                f.S[k] = new Vector3(Mathf.Sin(th), 0f, Mathf.Cos(th));
                f.T[k] = new Vector3(Mathf.Cos(th), 0f, -Mathf.Sin(th)) * sg;
            }
            return f;
        }

        private static Frames PathFrames(IList<Vector3> pts)
        {
            int n = pts.Count;
            var f = new Frames { O = new Vector3[n], S = new Vector3[n], T = new Vector3[n], Th = new float[n] };
            for (int i = 0; i < n; i++)
            {
                Vector3 t = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                t.y = 0f;
                t.Normalize();
                f.O[i] = pts[i]; f.T[i] = t; f.S[i] = new Vector3(t.z, 0f, -t.x); f.Th[i] = i;
            }
            return f;
        }

        /// <summary>Angular spans of a ring between the given gaps (centre, half-angle).</summary>
        private static List<(float a, float b)> Spans(IEnumerable<(float c, float h)> gaps)
        {
            float TwoPi = Mathf.PI * 2f;
            float Wrap(float x) { while (x < 0f) x += TwoPi; while (x >= TwoPi) x -= TwoPi; return x; }
            var g = (gaps ?? Enumerable.Empty<(float c, float h)>()).Select(x => (a: Wrap(x.c - x.h), w: 2f * x.h)).OrderBy(x => x.a).ToList();
            var spans = new List<(float a, float b)>();
            if (g.Count == 0) { spans.Add((-Mathf.PI, Mathf.PI)); return spans; }
            for (int i = 0; i < g.Count; i++)
            {
                float start = g[i].a + g[i].w;
                float end = i + 1 < g.Count ? g[i + 1].a : g[0].a + TwoPi;
                if (end - start > 1e-4f) spans.Add((start, end));
            }
            return spans;
        }

        private static Vector2[] RectP(float d0, float d1, float y0, float y1) =>
            new[] { new Vector2(d0, y0), new Vector2(d1, y0), new Vector2(d1, y1), new Vector2(d0, y1) };

        /// <summary>Solid steps going down outwards from (d0, top) to the ground, n steps of the given depth.</summary>
        private static Vector2[] StepProfile(float d0, float top, int n, float depth, float rise)
        {
            var p = new List<Vector2> { new Vector2(d0, 0f), new Vector2(d0 + n * depth, 0f) };
            for (int k = n - 1; k >= 0; k--)
            {
                float y = top - k * rise;
                p.Add(new Vector2(d0 + (k + 1) * depth, y));
                p.Add(new Vector2(d0 + k * depth, y));
            }
            return p.ToArray();
        }

        private static Vector2[] ColumnProfile(float r, float h) => new[]
        {
            new Vector2(0f, 0f), new Vector2(r * 1.45f, 0f), new Vector2(r * 1.45f, 0.22f), new Vector2(r * 1.25f, 0.3f), new Vector2(r * 1.1f, 0.42f),
            new Vector2(r, 0.5f), new Vector2(r * 0.9f, h - 0.55f), new Vector2(r * 1.05f, h - 0.45f), new Vector2(r * 1.2f, h - 0.3f),
            new Vector2(r * 1.5f, h - 0.18f), new Vector2(r * 1.5f, h), new Vector2(0f, h),
        };

        /// <summary>A moulded cornice on the face dF (side +1 outward, -1 inward), bottom at yb.</summary>
        private static Vector2[] CorniceProfile(float dF, float yb, float reach, float side = 1f)
        {
            var p = new[]
            {
                new Vector2(-0.12f, 0f), new Vector2(0.05f, 0f), new Vector2(reach * 0.33f, 0.12f), new Vector2(reach * 0.5f, 0.28f),
                new Vector2(reach * 0.92f, 0.4f), new Vector2(reach, 0.52f), new Vector2(reach, 0.6f), new Vector2(-0.12f, 0.6f),
            };
            var q = p.Select(v => new Vector2(side * (dF + v.x), yb + v.y)).ToList();
            if (side < 0f) q.Reverse();   // mirroring flips the winding
            return q.ToArray();
        }

        private static void Cornice(Transform parent, string name, float dF, float yb, float reach, List<(float c, float h)> gaps, ColMats m, (float a, float b)? onlyRange = null)
        {
            var sm = new SmoothMesh();
            var led = new SmoothMesh();
            Vector2[] prof = CorniceProfile(dF, yb, reach);
            Vector2[] strip = RectP(dF + 0.01f, dF + 0.07f, yb - 0.1f, yb - 0.03f);
            if (onlyRange.HasValue)
            {
                RangeSweep(sm, prof, dF + reach, 0.6f, onlyRange.Value.a, onlyRange.Value.b, new Subs(0));
                RangeSweep(led, strip, dF, 0.6f, onlyRange.Value.a, onlyRange.Value.b, new Subs(0));
            }
            else
            {
                RingSweep(sm, prof, dF + reach, 0.8f, gaps, new Subs(0));
                RingSweep(led, strip, dF, 0.8f, gaps, new Subs(0));
            }
            Part(parent, name, sm, new[] { m.Trim }, collider: false);
            Part(parent, name + " Light", led, new[] { m.Cyan }, collider: false, shadows: false);
        }

        /// <summary>Glass balustrade with a bronze rail, standing on a floor at y along ring d.</summary>
        private static void Railing(Transform parent, string name, float d, float y, List<(float c, float h)> gaps, ColMats m, (float c, float h)? onlyRange = null)
        {
            var glass = new SmoothMesh();
            var rail = new SmoothMesh();
            Vector2[] g = RectP(d - 0.025f, d + 0.025f, y, y + 1.05f), r = RectP(d - 0.05f, d + 0.05f, y + 1.05f, y + 1.12f);
            if (onlyRange.HasValue)
            {
                float a = onlyRange.Value.c - onlyRange.Value.h, b = onlyRange.Value.c + onlyRange.Value.h;
                RangeSweep(glass, g, d, 0.8f, a, b, new Subs(0));
                RangeSweep(rail, r, d, 0.8f, a, b, new Subs(0));
            }
            else
            {
                RingSweep(glass, g, d, 0.8f, gaps, new Subs(0));
                RingSweep(rail, r, d, 0.8f, gaps, new Subs(0));
            }
            Part(parent, name, glass, new[] { m.Glass }, collider: true, shadows: false);
            Part(parent, name + " Rail", rail, new[] { m.Bronze }, collider: false);
        }

        private static void RadialRailing(Transform parent, string name, float th, float d0, float d1, float y, ColMats m)
        {
            var glass = new SmoothMesh();
            glass.Grid(6, 1, (u, v) => { Vector3 p = EF(th, Mathf.Lerp(d0, d1, u), y + v * 1.05f); return (p, ETan(th), new Vector2(u * (d1 - d0), v)); });
            Part(parent, name, glass, new[] { m.Glass }, collider: true, shadows: false);
            var rail = new SmoothMesh();
            PolyTube(rail, 0, new List<Vector3> { EF(th, d0, y + 1.08f), EF(th, d1, y + 1.08f) }, 0.04f, 8);
            Part(parent, name + " Rail", rail, new[] { m.Bronze }, collider: false);
        }

        private static void RingSweep(SmoothMesh sm, IList<Vector2> prof, float dRef, float step, IEnumerable<(float c, float h)> gaps, Subs s, int fan = 0, bool closed = true)
        {
            var list = gaps?.ToList() ?? new List<(float c, float h)>();
            bool full = list.Count == 0;
            foreach (var (a, b) in Spans(list))
                Sweep(sm, EllFrames(a, b, dRef, step), prof, closed, !full && closed, !full && closed, s, fan);
        }

        private static void RangeSweep(SmoothMesh sm, IList<Vector2> prof, float dRef, float step, float a, float b, Subs s, int fan = 0, bool caps = true, bool closed = true)
            => Sweep(sm, EllFrames(Mathf.Min(a, b), Mathf.Max(a, b), dRef, step), prof, closed, caps && closed, caps && closed, s, fan);

        /// <summary>
        /// Sweeps a cross-section (x = distance along the frame's side vector, y = height; counter-clockwise) along the frames.
        /// Faces are flat-shaded across the profile and smooth along the sweep; walls get along/height UVs, floors planar UVs (metres).
        /// </summary>
        private static void Sweep(SmoothMesh sm, Frames f, IList<Vector2> prof, bool closed, bool capStart, bool capEnd, Subs s, int fan = 0)
        {
            int m = prof.Count, segs = closed ? m : m - 1;
            var cum = new float[m + 1];
            for (int j = 1; j <= m; j++) cum[j] = cum[j - 1] + Vector2.Distance(prof[j - 1], prof[j % m]);
            Vector3 Pos(int i, Vector2 q) => f.O[i] + f.S[i] * q.x + Vector3.up * q.y;
            for (int j = 0; j < segs; j++)
            {
                Vector2 a = prof[j], b = prof[(j + 1) % m], dl = b - a;
                if (dl.sqrMagnitude < 1e-10f) continue;
                Vector2 n2 = new Vector2(dl.y, -dl.x).normalized;
                int sub = n2.y > 0.7f ? s.Top : n2.y < -0.7f ? s.Bottom : n2.x > 0.7f ? s.Out : s.Def;
                bool flat = Mathf.Abs(n2.y) > 0.7f, vertical = Mathf.Abs(n2.x) > 0.95f;
                int start = sm.V.Count;
                float arc = 0f;
                Vector2 mid = (a + b) * 0.5f;
                for (int i = 0; i < f.Count; i++)
                {
                    if (i > 0) arc += Vector3.Distance(Pos(i, mid), Pos(i - 1, mid));
                    Vector3 pa = Pos(i, a), pb = Pos(i, b);
                    Vector3 n = (f.S[i] * n2.x + Vector3.up * n2.y).normalized;
                    Vector2 ua = flat ? new Vector2(pa.x, pa.z) : new Vector2(arc, vertical ? a.y : cum[j]);
                    Vector2 ub = flat ? new Vector2(pb.x, pb.z) : new Vector2(arc, vertical ? b.y : cum[j] + dl.magnitude);
                    sm.Add(pa, n, ua);
                    sm.Add(pb, n, ub);
                }
                for (int i = 0; i < f.Count - 1; i++)
                {
                    int k = start + i * 2;
                    sm.Tri(k, k + 2, k + 3, sub);
                    sm.Tri(k, k + 3, k + 1, sub);
                }
            }
            if (m < 3) return;
            foreach (var (idx, sign, on) in new[] { (0, -1f, capStart), (f.Count - 1, 1f, capEnd) })
            {
                if (!on) continue;
                Vector3 n = f.T[idx] * sign;
                int c0 = sm.V.Count;
                for (int j = 0; j < m; j++) sm.Add(Pos(idx, prof[j]), n, new Vector2(prof[j].x, prof[j].y));
                for (int j = 1; j < m - 1; j++) sm.Tri(c0 + fan, c0 + (fan + j) % m, c0 + (fan + j + 1) % m, s.Def);
            }
        }

        /// <summary>A surface of revolution about the vertical axis through foot (profile x = radius).</summary>
        private static void Lathe(SmoothMesh sm, int sub, Vector3 foot, IList<Vector2> prof) =>
            Sweep(sm, CircleFrames(foot, -Mathf.PI, Mathf.PI, prof.Max(p => p.x), 0.12f), prof, true, false, false, new Subs(sub));

        /// <summary>A walkable top surface over a ring sector, height by distance out.</summary>
        private static void GridTop(SmoothMesh sm, float t0, float t1, float d0, float d1, Func<float, float> yOfD, int nt, int nd)
        {
            sm.Grid(nt, nd, (u, v) =>
            {
                float th = Mathf.Lerp(t0, t1, u), d = Mathf.Lerp(d0, d1, v);
                Vector3 p = EF(th, d, yOfD(d));
                return (p, Vector3.up, new Vector2(p.x, p.z));
            });
        }

        private static void PolyTube(SmoothMesh sm, int sub, IList<Vector3> pts, float r, int sides)
        {
            int start = sm.V.Count;
            float along = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) along += Vector3.Distance(pts[i], pts[i - 1]);
                Vector3 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 refv = Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right;
                Vector3 u = Vector3.Cross(t, refv).normalized, w = Vector3.Cross(t, u);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    Vector3 n = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                    sm.Add(pts[i] + n * r, n, new Vector2(k / (float)sides, along));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                    sm.Tri(a, b, d, sub);
                    sm.Tri(a, d, c, sub);
                }
        }

        /// <summary>
        /// A curved wall along ring dC with arched openings: bays of about bayLen metres, each opening shaped by spec
        /// (semicircular head). Openings can be glazed (glass surface in the wall's middle plane). Returns the pier angles.
        /// </summary>
        private static List<float> ArchWall(SmoothMesh wall, SmoothMesh glass, float dC, float thick, float y0, float y1, List<(float a, float b)> spans, bool full,
            float bayLen, Func<int, Bay> spec, int subWall, int subUnder)
        {
            var piers = new List<float>();
            float dO = dC + thick * 0.5f, dI = dC - thick * 0.5f;
            int bayIndex = 0;
            void Q(int sub, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n0, Vector3 n1, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
            {
                int a = wall.Add(p0, n0, u0), b = wall.Add(p1, n1, u1), c = wall.Add(p2, n1, u2), d = wall.Add(p3, n0, u3);
                wall.Tri(a, b, c, sub);
                wall.Tri(a, c, d, sub);
            }
            foreach (var (sa, sb) in spans)
            {
                var at = new ArcTable(sa, sb, dO);
                int n = Mathf.Max(1, Mathf.RoundToInt(at.Total / bayLen));
                float bl = at.Total / n, half = bl * 0.5f;
                for (int k = 0; k < n; k++)
                {
                    float sc = bl * (k + 0.5f);
                    piers.Add(at.At(bl * k));
                    Bay bay = spec(bayIndex++);
                    var us = new List<(float u, float b)>();
                    float r = Mathf.Min(bay.W * 0.5f, half - 0.2f);
                    if (!bay.Open || r <= 0.1f)
                        for (int q = 0; q <= 4; q++) us.Add((-half + bl * q / 4f, y0));
                    else
                    {
                        int side = Mathf.Max(1, Mathf.CeilToInt((half - r) / 1.2f));
                        for (int q = 0; q <= side; q++) us.Add((-half + (half - r) * q / side, y0));
                        us.Add((-r, bay.Spring));
                        for (int q = 1; q < 18; q++) { float t = Mathf.PI * (1f - q / 18f); us.Add((r * Mathf.Cos(t), bay.Spring + r * Mathf.Sin(t))); }
                        us.Add((r, bay.Spring));
                        for (int q = 0; q <= side; q++) us.Add((r + (half - r) * q / side, y0));
                    }
                    float[] th = us.Select(p => at.At(sc + p.u)).ToArray();
                    for (int i = 0; i < us.Count - 1; i++)
                    {
                        var (u0, b0) = us[i];
                        var (u1, b1) = us[i + 1];
                        float t0 = th[i], t1 = th[i + 1], s0 = sc + u0, s1 = sc + u1;
                        if (Mathf.Abs(u1 - u0) > 1e-4f)
                        {
                            Q(subWall, EF(t0, dO, b0), EF(t1, dO, b1), EF(t1, dO, y1), EF(t0, dO, y1), EOut(t0), EOut(t1), new Vector2(s0, b0), new Vector2(s1, b1), new Vector2(s1, y1), new Vector2(s0, y1));
                            Q(subWall, EF(t0, dI, b0), EF(t1, dI, b1), EF(t1, dI, y1), EF(t0, dI, y1), -EOut(t0), -EOut(t1), new Vector2(s0, b0), new Vector2(s1, b1), new Vector2(s1, y1), new Vector2(s0, y1));
                            Vector3 a0 = EF(t0, dO, y1), a1 = EF(t1, dO, y1), a2 = EF(t1, dI, y1), a3 = EF(t0, dI, y1);
                            Q(subWall, a0, a1, a2, a3, Vector3.up, Vector3.up, new Vector2(a0.x, a0.z), new Vector2(a1.x, a1.z), new Vector2(a2.x, a2.z), new Vector2(a3.x, a3.z));
                        }
                        float du = u1 - u0, dy = b1 - b0, len = Mathf.Sqrt(du * du + dy * dy);
                        if (len < 1e-5f) continue;
                        Vector3 tan = ETan((t0 + t1) * 0.5f);
                        Vector3 nU = (tan * (dy / len) + Vector3.up * (-du / len)).normalized;
                        int sub = Mathf.Abs(b0 - y0) < 1e-3f && Mathf.Abs(b1 - y0) < 1e-3f ? subWall : subUnder;
                        Q(sub, EF(t0, dO, b0), EF(t1, dO, b1), EF(t1, dI, b1), EF(t0, dI, b0), nU, nU, new Vector2(s0, 0f), new Vector2(s1, 0f), new Vector2(s1, thick), new Vector2(s0, thick));
                    }
                    if (glass != null && bay.Open && bay.Glazed && r > 0.1f)
                        for (int i = 0; i < us.Count - 1; i++)
                        {
                            var (u0, b0) = us[i];
                            var (u1, b1) = us[i + 1];
                            if (b0 < bay.Spring - 1e-3f || b1 < bay.Spring - 1e-3f || Mathf.Abs(u1 - u0) < 1e-4f) continue;
                            glass.Grid(1, 1, (u, v) =>
                            {
                                float tt = Mathf.Lerp(th[i], th[i + 1], u), top = Mathf.Lerp(b0, b1, u);
                                return (EF(tt, dC, Mathf.Lerp(y0, top, v)), EOut(tt), new Vector2(Mathf.Lerp(u0, u1, u), Mathf.Lerp(y0, top, v)));
                            });
                        }
                }
                if (!full)
                {
                    piers.Add(sb);
                    foreach (var (t, sg) in new[] { (sa, -1f), (sb, 1f) })
                    {
                        Vector3 n2 = ETan(t) * sg;
                        Q(subWall, EF(t, dO, y0), EF(t, dI, y0), EF(t, dI, y1), EF(t, dO, y1), n2, n2, new Vector2(0f, y0), new Vector2(thick, y0), new Vector2(thick, y1), new Vector2(0f, y1));
                    }
                }
            }
            return piers;
        }

        // ================================================================== objects, meshes, materials

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Part(Transform parent, string name, SmoothMesh sm, Material[] mats, bool collider, bool render = true, bool shadows = true)
        {
            if (sm.V.Count == 0) return null;
            Mesh mesh = ColSave(sm.ToMesh(), name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (render)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static Mesh ColSave(Mesh mesh, string name)
        {
            string safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            string path = $"{ColFolder}/{safe}_{_colMesh++}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = safe;
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = safe;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void ColPoint(Transform parent, string name, Vector3 local, float range, float intensity, Color colour)
        {
            PointLight(parent, name, local, range, intensity, colour);
            _colLights++;
        }

        private static void ColSpot(Transform parent, string name, Vector3 local, Vector3 target, Color colour, float range, float intensity, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.LookRotation(target - local, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.intensity = intensity;
            l.color = colour;
            l.shadows = LightShadows.None;
            _colLights++;
        }

        /// <summary>A textured URP material for a texture role (see Downloads > Poly Haven Textures For The Genesis Colosseum); UVs are in metres.</summary>
        private static Material ColMat(string name, string role, Color tint, float smooth, float tile, float metallic = 0f)
        {
            Material m = Mat(name, tint, metallic, smooth);
            var (diff, nor) = RoleTextures(role);
            m.SetTexture("_BaseMap", diff);
            m.SetTextureScale("_BaseMap", Vector2.one / tile);
            if (nor != null) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            if (diff == null) m.SetColor("_BaseColor", tint * 0.9f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static (Texture2D diff, Texture2D nor) RoleTextures(string role)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string file = Path.Combine(project, GenesisAssetDownloads.ColosseumRolesFile);
            string id = null;
            if (File.Exists(file))
                foreach (string line in File.ReadAllLines(file))
                    if (line.StartsWith(role + "=")) id = line.Substring(role.Length + 1).Trim();
            string ph = GenesisAssetDownloads.PolyHavenFolder;
            if (id != null)
            {
                var d = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ph}/{id}/{id}_diff_2k.jpg");
                if (d != null) return (d, AssetDatabase.LoadAssetAtPath<Texture2D>($"{ph}/{id}/{id}_nor_gl_2k.jpg"));
            }
            string fallback = role switch { "stone" => "concrete_panels", "paving" => "concrete_panels", "trim" => "marble_01", "floor" => "marble_01", _ => null };
            if (fallback == null) return (null, null);
            return (AssetDatabase.LoadAssetAtPath<Texture2D>($"{ph}/{fallback}/{fallback}_diff_1k.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>($"{ph}/{fallback}/{fallback}_nor_gl_1k.jpg"));
        }
    }
}
#endif
