using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<Game>() != null) return;
            new GameObject("SHEEPTATOR").AddComponent<Game>();
        }
    }

    public partial class Game : MonoBehaviour
    {
        public const string Title = "SHEEPTATOR";
        public const string Subtitle = "A Sheep Management Tragedy";
        public static Game I;

        public const float DayLength = 24f;
        public const int DaysPerSeason = 10;
        public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };

        public System.Random rng = new System.Random();
        public float simTime;
        public int dayIndex;
        public float Day { get { return simTime / DayLength; } }
        public int Season { get { return (dayIndex / DaysPerSeason) % 4; } }
        public int Year { get { return dayIndex / (DaysPerSeason * 4) + 1; } }
        public int DayOfSeason { get { return dayIndex % DaysPerSeason + 1; } }

        public int speed = 1;
        public static readonly float[] Speeds = { 0f, 1f, 2f, 4f };
        public bool started;   // title dismissed
        public bool gameWon;

        public double[] res = new double[ResInfo.Count];
        public double[] totalMade = new double[ResInfo.Count];
        public double[] totalSold = new double[ResInfo.Count];
        public float[] rate = new float[ResInfo.Count];
        double[] lastRes = new double[ResInfo.Count];
        public float happinessRate;
        float lastHappy = -1;

        public List<Sheep> sheep = new List<Sheep>();
        public List<DeadRecord> graveyard = new List<DeadRecord>();
        public List<Building> buildings = new List<Building>();
        public HashSet<string> tech = new HashSet<string>();
        public HashSet<string> flags = new HashSet<string>();
        public HashSet<string> usedNames = new HashSet<string>();
        public string founderName;
        int nextId = 1;

        public int wagePolicy; // 0 none, 1 carrots, 2 coins
        public int deaths, accidents, births, lastAccidentDay = 0;

        Camera cam;
        public int pixelScale = 4;
        Vector3 camTarget;
        Vector3 dragOrigin; bool dragging;
        public Sheep selSheep; public Building selBuilding; public Sheep followSheep;
        public Sheep hoverSheep; public Building hoverBuilding;
        bool mouseOverUI;

        void Awake()
        {
            I = this;
            useGUILayout = false;
            Application.targetFrameRate = 60;
            PixelFont.Init();
            Sfx.Init(gameObject);
            SetupCamera();
            InitWorld();
            if (!TryApplyPendingLoad()) NewGame();
            if (skipTitle) { skipTitle = false; StartGame(); }
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.C("1e2a18");
            cam.nearClipPlane = -50; cam.farClipPlane = 100;
            cam.transform.rotation = Quaternion.identity;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            pixelScale = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 220f), 2, 8);
        }

        void Update()
        {
            float rdt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            UpdateUIScale();
            HandleInput(rdt);
            UpdateLeader(rdt);
            UpdateFliers(rdt);
            bool paused = !started || currentEvent != null || speed == 0 || launchSeq > 0 || LeaderModal;
            float dt = paused ? 0f : rdt * Speeds[speed];
            // substep for stability at 4x
            int steps = Mathf.CeilToInt(dt / 0.05f);
            for (int i = 0; i < steps; i++) Simulate(dt / steps);
            SimLaunch(rdt);
            UpdateVisuals(rdt, dt);
            UpdateFx(rdt, dt);
            UpdateCamera(rdt);
        }

        // ---------------- time ----------------
        void Simulate(float dt)
        {
            if (dt <= 0) return;
            simTime += dt;
            int di = Mathf.FloorToInt(Day);
            while (dayIndex < di) { dayIndex++; OnNewDay(); }

            UpdateGrass(dt);
            ComputeGlobals();
            CountPens();
            ProcessRebirths();
            for (int i = sheep.Count - 1; i >= 0; i--) SimSheep(sheep[i], dt);
            for (int i = buildings.Count - 1; i >= 0; i--) SimBuilding(buildings[i], dt);
            SimBelts(dt);
            SimWolves(dt);
            SimEvents(dt);
            ClampStorage();
            UpdateRates(dt);
            CheckObjectives();
        }

        void UpdateRates(float dt)
        {
            float k = Mathf.Clamp01(dt / 12f);
            for (int i = 0; i < ResInfo.Count; i++)
            {
                float inst = (float)((res[i] - lastRes[i]) / dt);
                rate[i] = Mathf.Lerp(rate[i], inst, k);
                lastRes[i] = res[i];
            }
            float h = AvgMood();
            if (lastHappy < 0) lastHappy = h;
            happinessRate = Mathf.Lerp(happinessRate, (h - lastHappy) / dt, k);
            lastHappy = h;
        }

        public float AvgMood()
        {
            float m = 0; int n = 0;
            foreach (var s in sheep) if (s.Alive) { m += s.mood; n++; }
            return n == 0 ? 0 : m / n;
        }

        public int Population { get { int n = 0; foreach (var s in sheep) if (!s.dead) n++; return n; } }

        public int NextId() { return nextId++; }

        // ---------------- input ----------------
        void HandleInput(float rdt)
        {
            mouseOverUI = PointerOverUI();
            if (!started)
            {
                if (showHelp)
                {
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Return)) showHelp = false;
                    return;
                }
                if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1)) { showHelp = true; return; }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) { if (HasSave && SaveVersion() >= 4) LoadGame(); else StartGame(); }
                return;
            }
            if (currentEvent != null || launchSeq > 0 || LeaderModal) return;

            if (Input.GetKeyDown(KeyCode.Space)) { if (speed == 0) speed = prevSpeed; else { prevSpeed = speed; speed = 0; } }
            if (Input.GetKeyDown(KeyCode.Alpha1)) speed = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) speed = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) speed = 3;
            if (Input.GetKeyDown(KeyCode.B) && UiOn("build")) TogglePanel(Panel.Build);
            if (Input.GetKeyDown(KeyCode.F) && UiOn("flock")) TogglePanel(Panel.Sheep);
            if (Input.GetKeyDown(KeyCode.O) && UiOn("jobs")) TogglePanel(Panel.Jobs); // the FACTORY overview
            if (Input.GetKeyDown(KeyCode.N) && tech.Contains("genetics")) TogglePanel(Panel.Genetics);
            if (Input.GetKeyDown(KeyCode.L)) { showPlates = !showPlates; Toast(showPlates ? "Name plates ON (L)" : "Name plates OFF (L)"); }
            if (Input.GetKeyDown(KeyCode.R) && !beltTool && UiOn("research")) TogglePanel(Panel.Research);
            if (Input.GetKeyDown(KeyCode.C) && UiOn("belts")) { if (beltTool) beltTool = false; else BeginBeltTool(); }
            if (Input.GetKeyDown(KeyCode.G) && UiOn("gate"))
            {
                var gp = selBuilding != null && IsRoom(selBuilding) ? selBuilding : selSheep != null && selSheep.pen != null && buildings.Contains(selSheep.pen) ? selSheep.pen : FirstPen();
                if (gp != null) ToggleMainDoor(gp);
            }
            if (Input.GetKeyDown(KeyCode.F5)) SaveGame(false);
            if (Input.GetKeyDown(KeyCode.F9)) LoadGame();
            if (Input.GetKeyDown(KeyCode.T) && UiOn("market")) TogglePanel(Panel.Market);
            if (Input.GetKeyDown(KeyCode.M) && UiOn("map")) TogglePanel(Panel.Map);
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (placing != null || pathTool || beltTool) { placing = null; pathTool = false; beltTool = false; clearTool = false; }
                else if (showHelp) showHelp = false;
                else if (selSheep != null || selBuilding != null) { selSheep = null; selBuilding = null; followSheep = null; }
                else panel = Panel.None;
            }

            // camera pan
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
            if (move != Vector3.zero) { camTarget += move.normalized * rdt * (60f / pixelScale) * 1.4f; followSheep = null; }

            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) { dragOrigin = Input.mousePosition; dragging = false; }
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - dragOrigin;
                if (delta.sqrMagnitude > 16 || dragging)
                {
                    dragging = true;
                    camTarget -= delta / (16f * pixelScale);
                    cam.transform.position -= delta / (16f * pixelScale);
                    dragOrigin = Input.mousePosition;
                    followSheep = null;
                }
            }
            if (Input.GetMouseButtonUp(1) && !dragging && !mouseOverUI)
            {
                if (placing != null || pathTool || beltTool) { placing = null; pathTool = false; beltTool = false; clearTool = false; }
                else { selSheep = null; selBuilding = null; followSheep = null; }
            }

            float wheel = Input.mouseScrollDelta.y;
            if (!mouseOverUI && Mathf.Abs(wheel) > 0.01f)
            {
                int ns = Mathf.Clamp(pixelScale + (wheel > 0 ? 1 : -1), 1, 10);
                if (ns != pixelScale)
                {
                    // zoom towards mouse
                    Vector3 before = mw;
                    pixelScale = ns;
                    ApplyCamera();
                    Vector3 after = cam.ScreenToWorldPoint(Input.mousePosition);
                    camTarget += before - after;
                    cam.transform.position += before - after;
                }
            }

            mw = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 m2 = new Vector2(mw.x, mw.y);
            hoverSheep = null; hoverBuilding = null;
            if (!mouseOverUI)
            {
                hoverSheep = SheepAt(m2);
                if (hoverSheep == null) hoverBuilding = BuildingAt(m2);
            }

            if (beltTool) { UpdateBeltTool(m2); return; }
            if (placing != null || pathTool) { UpdatePlacement(m2); return; }

            if (Input.GetMouseButtonDown(0) && !mouseOverUI)
            {
                if (hoverSheep != null) { Select(hoverSheep); }
                else if (hoverBuilding != null) { selBuilding = hoverBuilding; selSheep = null; followSheep = null; Sfx.Play("click"); }
                else { selSheep = null; selBuilding = null; followSheep = null; }
            }
        }
        int prevSpeed = 1;

        public void Select(Sheep s)
        {
            selSheep = s; selBuilding = null;
            if (s != null) { Sfx.Baa(0.5f); followSheep = null; }
        }

        public void FocusOn(Vector2 p)
        {
            camTarget = new Vector3(p.x, p.y, camTarget.z);
        }

        Sheep SheepAt(Vector2 p)
        {
            Sheep best = null; float bd = 0.55f;
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden || s.go == null) continue;
                Vector2 c = s.pos + new Vector2(0, s.Lamb ? 0.35f : 0.55f);
                float d = Vector2.Distance(c, p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        Building BuildingAt(Vector2 p)
        {
            for (int i = buildings.Count - 1; i >= 0; i--)
            {
                var b = buildings[i];
                if (p.x >= b.x && p.x < b.x + b.def.w && p.y >= b.y && p.y < b.y + b.def.h + 0.8f) return b;
            }
            return null;
        }

        void ApplyCamera()
        {
            cam.orthographicSize = Screen.height / (2f * 16f * pixelScale);
        }

        void UpdateCamera(float rdt)
        {
            ApplyCamera();
            if (followSheep != null && !followSheep.dead && followSheep.act != Act.Hidden)
                camTarget = new Vector3(followSheep.pos.x, followSheep.pos.y + 0.5f, camTarget.z);
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            camTarget.x = Mathf.Clamp(camTarget.x, Mathf.Min(halfW - 2, MapW / 2f), Mathf.Max(MapW - halfW + 2, MapW / 2f));
            camTarget.y = Mathf.Clamp(camTarget.y, Mathf.Min(halfH - 2, MapH / 2f), Mathf.Max(MapH - halfH + 2, MapH / 2f));
            Vector3 p = Vector3.Lerp(cam.transform.position, camTarget, 1 - Mathf.Exp(-rdt * 10f));
            p.z = -10;
            // snap to pixel grid for crisp pixels
            float unit = 1f / (16f * pixelScale);
            if (shake > 0) { p.x += Random.Range(-shake, shake) * 0.15f; p.y += Random.Range(-shake, shake) * 0.15f; shake = Mathf.Max(0, shake - rdt * 3); }
            p.x = Mathf.Round(p.x / unit) * unit; p.y = Mathf.Round(p.y / unit) * unit;
            cam.transform.position = p;
        }
        public float shake;

        public void StartGame()
        {
            started = true;
            Sfx.Baa(0.8f);
            if (!tutDone && tutStep < 0) BeginTutorial();
        }

        // Sorting helpers
        public static int SortY(float y) { return Mathf.RoundToInt(-y * 32f); }
    }
}
