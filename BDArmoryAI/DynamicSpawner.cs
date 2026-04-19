using BDArmory;
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
using System.Linq;
using System.Text;
using UnityEngine;


namespace BDArmoryAI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DynamicSpawner : MonoBehaviour
    {
        private ApplicationLauncherButton button;
        private static DynamicSpawner instance;
        private bool showGUI = false;
        private Rect windowRect = new Rect(300, 200, 300, 200);

        private string craftsFolder = "GameData/BDArmoryAI/Ships/";
        private List<string> craftFiles = new List<string>();
        private string[] craftFilesArray;

        private int selectedCraftIndex = 0;
        private bool craftsLoaded = false;
        private int quantityToSpawn = 1;

        private double spawnRange = 50000.0;
        private bool useActiveVessel = true;

        private bool spawnFlying = false;
        private double spawnAltitude = 500.0;
        private double spawnSpeed = 0;

        private double manualLat = 0.0;
        private double manualLon = 0.0;

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
                Texture2D icon = GameDatabase.Instance.GetTexture("BDArmoryAI/Textures/icon/icon_ai.png", false);
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
            if (!craftsLoaded) LoadCraftNames();
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Dynamic Spawner");
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Dynamic Spawner Settings");

            useActiveVessel = GUILayout.Toggle(useActiveVessel, "Use Active Vessel Position");

            if (!useActiveVessel)
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

            spawnFlying = GUILayout.Toggle(spawnFlying, "Spawn Flying");

            if (spawnFlying)
            {
                GUILayout.Label("Altitude (m): " + spawnAltitude.ToString("F0"));
                spawnAltitude = GUILayout.HorizontalSlider((float)spawnAltitude, 10f, 50000f);
                GUILayout.Label("Speed (m/s): " + spawnSpeed.ToString("F0"));
                spawnSpeed = GUILayout.HorizontalSlider((float)spawnSpeed, 0f, 1000f);
            }

            GUILayout.Label("Spawn Range (m): " + spawnRange.ToString("F0"));
            spawnRange = GUILayout.HorizontalSlider((float)spawnRange, 1000f, 50000f);

            GUILayout.Label("Spawn Quantity: " + quantityToSpawn);
            string qtyStr = GUILayout.TextField(quantityToSpawn.ToString());
            if (int.TryParse(qtyStr, out int parsedQty))
            {
                quantityToSpawn = parsedQty;
            }

            GUILayout.Label("Select Craft:");
            selectedCraftIndex = GUILayout.SelectionGrid(selectedCraftIndex, craftFilesArray, 1);

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
                    if (spawnFlying)
                    {
                        spawnPos.z = spawnAltitude + FlightGlobals.currentMainBody.TerrainAltitude(spawnPos.x, spawnPos.y);
                    }
                    else
                    {
                        spawnPos.z = 0.2f + FlightGlobals.currentMainBody.TerrainAltitude(spawnPos.x, spawnPos.y);
                    }

                    for (int i = 0; i < quantityToSpawn; i++)
                    {
                        
                        Vessel spawnedVessel = VesselSpawner.SpawnVesselFromCraftFile(craftsFolder + craftFiles[selectedCraftIndex] + ".craft", spawnPos, 0f, 0f, 0f, out shipFacility);
                        StartCoroutine(SetupSpawnVessel(spawnedVessel));
                        spawnPos.x += 0.006f;
                    }
                    UnityEngine.Debug.Log("Spawned vessel at: " + spawnPos + " : " + FlightGlobals.currentMainBody);

                }
            }
        }

        void LoadCraftNames()
        {
            craftFiles.Clear();    
            string fullPath = System.IO.Path.Combine(KSPUtil.ApplicationRootPath, craftsFolder);

            if (System.IO.Directory.Exists(fullPath))
            {
                var files = System.IO.Directory.GetFiles(fullPath, "*.craft");
                foreach (var file in files)
                {
                    craftFiles.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[BDA-AI] Dynamic Spawner: Crafts folder not found at " + fullPath);
            }
            
            if (craftFiles.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[BDA-AI] Dynamic Spawner: No craft files found in " + fullPath);
            }
            craftFilesArray = craftFiles.ToArray();
            craftsLoaded = true;
        }

        IEnumerator SetupSpawnVessel(Vessel vessel)
        {
            if (vessel == null)
                yield break;

            yield return new WaitForSeconds(2.5f);



            if (spawnFlying)
            {

                vessel.SetPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, spawnAltitude));

                Vector3d forward = vessel.transform.up;
                Vector3d surfaceVelocity = forward * spawnSpeed + FlightGlobals.currentMainBody.getRFrmVel(vessel.CoM);

                vessel.Landed = false;
                vessel.Splashed = false;
                vessel.situation = Vessel.Situations.FLYING;

                vessel.SetWorldVelocity(surfaceVelocity);
                UnityEngine.Debug.Log("[BDA-AI] Applied initial velocity of " + spawnSpeed + " m/s to vessel: " + vessel.vesselName);
            }
            else
            {

                UnityEngine.Debug.Log("[BDA-AI] Spawned vessel on the ground: ");
            }
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
    }
}