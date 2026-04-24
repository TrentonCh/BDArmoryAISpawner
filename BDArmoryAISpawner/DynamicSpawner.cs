using BDArmory;
using BDArmory.Competition;
using BDArmory.Competition.OrchestrationStrategies;
using BDArmory.Control;
using BDArmory.Modules;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.VesselSpawning;
using KSP;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;


namespace BDArmoryAISpawner
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DynamicSpawner : MonoBehaviour
    {
        private ApplicationLauncherButton button;
        private static DynamicSpawner instance;
        private bool showGUI = false;
        private Rect windowRect = new Rect(300, 200, 300, 200);

        private string craftsFolder = "GameData/BDArmoryAISpawner/Ships/";
        private List<CraftSelection> craftSelectionList = new List<CraftSelection>(); 


        private int selectedCraftIndex = 0;
        private bool craftsLoaded = false;
        private int quantityToSpawn = 1;

        private double spawnRange = 50000.0;
        private bool useActiveVessel = true;
        bool useManualCoords = false;

        private string locationList = "GameData/BDArmoryAISpawner/SpawnLocations.txt";
        private List<SpawnLocation> spawnLocations = new List<SpawnLocation>();

        private double manualLat = 0.0;
        private double manualLon = 0.0;

        private bool friendly = false;

        void Awake()
        {
            if (instance != null)
            {
                UnityEngine.Debug.LogError("[BDA-AI] Multiple instances of DynamicSpawner detected! Destroying duplicate.");
                Destroy(this);
                return;
            }
            instance = this;
            DontDestroyOnLoad(this);
            UnityEngine.Debug.Log("[BDA-AI] DynamicSpawner Awake complete.");
        }
        void Start()
        {
            UnityEngine.Debug.Log("DynamicSpawner Plugin loaded!");
            GameEvents.onGUIApplicationLauncherReady.Add(OnToolbarReady);
            GameEvents.onPartDie.Add(DestroyAI);
        }

        void OnToolbarReady()
        {
            if (ApplicationLauncher.Ready && button == null)
            {
                Texture2D icon = GameDatabase.Instance.GetTexture("BDArmoryAISpawner/Textures/icon/icon", false);
                button = ApplicationLauncher.Instance.AddModApplication(OnTrue, OnFalse, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.SPACECENTER, icon);
                UnityEngine.Debug.Log("[BDA-AI] Toolbar button added");
            }
        }

        void OnTrue()
        {
            UnityEngine.Debug.Log("Toolbar button set to true");
            showGUI = !showGUI;
        }
        void OnFalse()
        {
            UnityEngine.Debug.Log("[BDA-AI] Toolbar button set to false!");
            showGUI = !showGUI;
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnToolbarReady);

            if (button != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(button);
                button = null;
                UnityEngine.Debug.Log("[BDA-AI] Toolbar button destroyed!");
            }
        }

        void OnGUI()
        {
            if (!showGUI) return;
            if (!craftsLoaded) LoadCraftsAndLocations();
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Dynamic Spawner");
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Dynamic Spawner Settings");

            useActiveVessel = GUILayout.Toggle(useActiveVessel, "Use Active Vessel Position");

            if (!useActiveVessel)
            {
                
                useManualCoords = GUILayout.Toggle(useManualCoords, "Use Manual Coordinates");
                if (useManualCoords)
                {
                    GUILayout.Label("Manual Latitude:");
                    string latStr = GUILayout.TextField(manualLat.ToString());
                    if (double.TryParse(latStr, out double parsedLat))
                    {
                        manualLat = parsedLat;
                    }
                    GUILayout.Label("Manual Longitude:");
                    string lonStr = GUILayout.TextField(manualLon.ToString());
                    if (double.TryParse(lonStr, out double parsedLon))
                    {
                        manualLon = parsedLon;
                    }
                }

                if (!useManualCoords)
                {
                    foreach (var location in spawnLocations)
                    {
                        GUILayout.BeginHorizontal();
                        location.selected = GUILayout.Toggle(location.selected, location.name);
                        if (location.selected)
                        {
                            foreach (var otherLocation in spawnLocations)
                            {
                                if (otherLocation.selected = true)
                                {
                                    otherLocation.selected = false;
                                }
                            }
                            location.selected = true;
                            manualLat = location.lat;
                            manualLon = location.lon;
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                
            }

            GUILayout.Label("Spawn Range (m): " + spawnRange.ToString("F0"));
            spawnRange = GUILayout.HorizontalSlider((float)spawnRange, 1f, 100000);

            GUILayout.Label("Select Crafts:");

            foreach (var craft in craftSelectionList)
            {
                GUILayout.BeginHorizontal();
                craft.selected = GUILayout.Toggle(craft.selected, craft.name.ToString(), GUILayout.Width(150));

                string qty = GUILayout.TextField(craft.quantity.ToString(), GUILayout.Width(50));
                if (int.TryParse(qty, out int parsedCraftQty))
                {
                    craft.quantity = Mathf.Max(1, parsedCraftQty);
                }

                craft.spawnFlying = GUILayout.Toggle(craft.spawnFlying, "Spawn Flying");
                if (!craft.selected)
                    craft.spawnFlying = false;

                if (craft.spawnFlying)
                {
                    GUILayout.Label("Altitude (m): " + craft.spawnAltitude.ToString("F0"), GUILayout.ExpandWidth(false));
                    GUILayout.Space(5);
                    craft.spawnAltitude = GUILayout.HorizontalSlider((float)craft.spawnAltitude, 10f, 25000f, GUILayout.MaxWidth(100), GUILayout.MinWidth(100));
                    GUILayout.Label("Speed (m/s): " + craft.spawnSpeed.ToString("F0"), GUILayout.ExpandWidth(false));
                    GUILayout.Space(5);
                    craft.spawnSpeed = GUILayout.HorizontalSlider((float)craft.spawnSpeed, 0f, 1000f, GUILayout.MaxWidth(75), GUILayout.MinWidth(75));
                }

                GUILayout.EndHorizontal();
            }

            windowRect.width = 350;
            foreach (var craft in craftSelectionList)
            {
                if (craft.spawnFlying)
                {
                    windowRect.width = 750;
                    break;
                }
            }

            friendly = GUILayout.Toggle(friendly, "Friendly");

            GUILayout.Space(10);

            if (GUILayout.Button("Spawn Craft Randomly"))
            {
                SpawnCraftRandomly();
            }

            if (GUILayout.Button("Close"))
            {
                showGUI = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void SpawnCraftRandomly()
        {
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel != null && vessel.loaded && vessel.isActiveVessel)
                {
                    EditorFacility shipFacility = EditorFacility.SPH;
                    System.Random r = new System.Random();
                    var body = FlightGlobals.currentMainBody;
                    double baseLat;
                    double baseLon;

                    if (!useActiveVessel)
                    {
                        baseLat = manualLat;
                        baseLon = manualLon;
                    }
                    else
                    {
                        baseLat = vessel.latitude;
                        baseLon = vessel.longitude;
                    }

                    Vector3d spawnPos = new Vector3d(baseLat, baseLon, 0);

                    spawnPos.x += 2.5f * r.NextDouble();
                    spawnPos.y += 2.5f * r.NextDouble();

                    double radius = body.Radius;

                    double maxDistance = spawnRange;

                    
                    int attempts = 0;
                    do
                    {
                        attempts++;

                        double distance = maxDistance * Math.Sqrt(r.NextDouble());
                        double angle = 2 * Math.PI * r.NextDouble();

                        double dLat = (distance / radius) * (180.0 / Math.PI);
                        double dLon = (distance / (radius * Math.Cos(baseLat * Math.PI / 180.0))) * (180.0 / Math.PI);

                        spawnPos.x = baseLat + dLat * Math.Cos(angle);
                        spawnPos.y = baseLon + dLon * Math.Sin(angle);

                        if (attempts > 100)
                        {
                            UnityEngine.Debug.LogWarning("[BDA-AI] Dynamic Spawner Failed to find land!");
                            return;
                        }
                    } while (body.TerrainAltitude(spawnPos.x, spawnPos.y) <= 0.5);
                    

                    foreach (var craft in craftSelectionList)
                    {
                        if (!craft.selected)
                            continue;

                        if (craft.spawnFlying)
                        {
                            spawnPos.z = craft.spawnAltitude + FlightGlobals.currentMainBody.TerrainAltitude(spawnPos.x, spawnPos.y);
                        }
                        else
                        {
                            spawnPos.z = 0.2f + FlightGlobals.currentMainBody.TerrainAltitude(spawnPos.x, spawnPos.y);
                        }

                        for (int i = 0; i < craft.quantity; i++)
                        {
                            Vessel spawnedVessel = VesselSpawner.SpawnVesselFromCraftFile(craftsFolder + craft.name + ".craft", spawnPos, 0f, 0f, 0f, out shipFacility);
                            spawnedVessel.easingInToSurface = false;
                            StartCoroutine(SetupSpawnVessel(spawnedVessel, craft));
                            spawnPos.x += 0.006f;
                        }
                    }
                        
                    UnityEngine.Debug.Log("Spawned vessel at: " + spawnPos + " : " + FlightGlobals.currentMainBody);
                }
            }
        }

        void LoadCraftsAndLocations()
        {
            craftSelectionList.Clear();
            
            string fullPath = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, craftsFolder);

            if (System.IO.Directory.Exists(fullPath))
            {
                var files = System.IO.Directory.GetFiles(fullPath, "*.craft");
                foreach (var file in files)
                {
                    craftSelectionList.Add(new CraftSelection(System.IO.Path.GetFileNameWithoutExtension(file)));
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[BDA-AI] Dynamic Spawner: Crafts folder not found at " + fullPath);
            }
            
            if (craftSelectionList.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[BDA-AI] Dynamic Spawner: No craft files found in " + fullPath);
            }
            craftsLoaded = true;


            string locationPath = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, locationList);
            if (System.IO.File.Exists(locationPath))
            {
                foreach (var line in System.IO.File.ReadAllLines(locationPath))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] parts = line.Split(',');

                        if (parts.Length != 3)
                        {
                            UnityEngine.Debug.LogWarning("[BDA-AI] Dynamic Spawner: Invalid line in spawn locations file: " + line);
                            continue;
                        }

                        string name = parts[0].Trim();
                        if (double.TryParse(parts[1].Trim(), out double lat) && double.TryParse(parts[2].Trim(), out double lon))
                        {
                            spawnLocations.Add(new SpawnLocation(name, lat, lon));
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[BDA-AI] Dynamic Spawner: Invalid coordinates in spawn locations file: " + line);

                        }
                    }
                }
            }
        }

        IEnumerator SetupSpawnVessel(Vessel vessel, CraftSelection craft)
        {
            if (vessel == null)
                yield break;

            yield return new WaitForSeconds(2.5f);



            if (craft.spawnFlying)
            {

                vessel.SetPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, craft.spawnAltitude));

                Vector3d forward = vessel.transform.up;
                Vector3d surfaceVelocity = forward * craft.spawnSpeed + FlightGlobals.currentMainBody.getRFrmVel(vessel.CoM);

                vessel.Landed = false;
                vessel.Splashed = false;
                vessel.situation = Vessel.Situations.FLYING;

                vessel.SetWorldVelocity(surfaceVelocity);
                UnityEngine.Debug.Log("[BDA-AI] Applied initial velocity of " + craft.spawnSpeed + " m/s to vessel: " + vessel.vesselName);
            }
            else
            {

                UnityEngine.Debug.Log("[BDA-AI] Spawned vessel on the ground: ");
            }
            EnableAI(vessel);
            EnableGaurdMode(vessel);
        }

        private void DestroyAI(Part part)
        {
            if (part == null || (part.FindModuleImplementing<BDModulePilotAI>() != null) || (part.FindModuleImplementing<BDModuleSurfaceAI>() != null))
                return;

            if (part.vessel.vesselName.Contains("Enemy"))
            {
                UnityEngine.Debug.Log("[BDA-AI] AI Module destroyed on " + part.vessel.vesselName);
                part.vessel.Die();
                foreach (Part otherParts in part.vessel.Parts)
                {
                    if (otherParts != null)
                        otherParts.Die();
                }
                UnityEngine.Debug.Log("[BDA-AI] Vessel destroyed");
            }
        }

        private void EnableAI(Vessel vessel)
        {
            // PILOT AI
            foreach (var ai in vessel.FindPartModulesImplementing<BDArmory.Control.BDModulePilotAI>())
            {
                ai.ActivatePilot();
                UnityEngine.Debug.Log("[BDA-AI] Pilot AI enabled");
            }

            // SURFACE PILOT AI
            foreach (var ai in vessel.FindPartModulesImplementing<BDArmory.Control.BDModuleSurfaceAI>())
            {
                ai.ActivatePilot();
                UnityEngine.Debug.Log("[BDA-AI] Surface Pilot AI enabled");
            }
        }

        private void EnableGaurdMode(Vessel vessel)
        {
            foreach (var wm in vessel.FindPartModulesImplementing<BDArmory.Control.MissileFire>())
            {
                if (!wm.guardMode)
                {
                    wm.ToggleGuardMode();
                    
                    
                }
                if (friendly)
                {
                    UnityEngine.Debug.Log("[BDA-AI] Friendly " + vessel.name);
                    wm.SetTeam(BDTeam.Get("A"));
                }
                else
                {
                    UnityEngine.Debug.Log("[BDA-AI] Enemy " + vessel.name);
                    wm.SetTeam(BDTeam.Get("B"));
                }
                UnityEngine.Debug.Log("[BDA-AI] Guard mode enabled for weapon: " + wm.part.partName);
            }
        }

        class CraftSelection
        {
            public string name;
            public bool selected;
            public int quantity;
            public bool spawnFlying = false;
            public double spawnAltitude = 500.0;
            public double spawnSpeed = 0;

            public CraftSelection(string name)
            {
                this.name = name;
                selected = false;
                quantity = 1;

            }
        }

        class SpawnLocation
        {
            public string name;
            public bool selected;
            public double lat;
            public double lon;

            public SpawnLocation(string name, double lat, double lon)
            {
                this.name = name;
                this.lat = lat;
                this.lon = lon;
                selected = false;
            }
        }
    }
}