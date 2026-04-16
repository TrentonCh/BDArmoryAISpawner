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

        private double spawnRange = 50000.0;
        private bool useActiveVessel = true;

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
                double.TryParse(GUILayout.TextField(manualLat.ToString()), out manualLat);
                GUILayout.Label("Manual Longitude:");
                double.TryParse(GUILayout.TextField(manualLon.ToString()), out manualLon);
            }

            GUILayout.Label("Spawn Range (m): " + spawnRange.ToString("F0"));
            spawnRange = GUILayout.HorizontalSlider((float)spawnRange, 1000f, 50000f);

            GUILayout.Space(10);

            if (GUILayout.Button("Spawn Tank Randomly"))
            {
                SpawnTankRandomly();
            }

            if (GUILayout.Button("Close"))
            {
                showGUI = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void SpawnTankRandomly()
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

                    spawnPos.z = 0.2f + FlightGlobals.currentMainBody.TerrainAltitude(spawnPos.x, spawnPos.y);
                    VesselSpawner.SpawnVesselFromCraftFile("GameData/ContractPacks/KerbheedMartin/ships/Enemy-m1abram-tb.craft", spawnPos, 180f, 0f, 0f, out shipFacility);
                    UnityEngine.Debug.Log("Spawned vessel at: " + spawnPos + " : " + FlightGlobals.currentMainBody);

                }
            }
        }
    }
}