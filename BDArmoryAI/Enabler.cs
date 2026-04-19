using BDArmory;
using BDArmory.Competition.OrchestrationStrategies;
using BDArmory.Control;
using BDArmory.Modules;
using BDArmory.UI;
using KSP;
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
    public class Enabler : MonoBehaviour
    {
        void Start()
        {
            UnityEngine.Debug.Log("BDArmoryAIEnabler Plugin loaded!");
            GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);

        }

        void OnDestroy()
        {
            UnityEngine.Debug.Log("[BDA-AI] Plugin unloading, removing event hook");
            GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
        }

        private void OnVesselGoOffRails(Vessel vessel)
        {
            UnityEngine.Debug.Log("[BDA-AI] Vessel off rails: " + vessel.vesselName);
            //StartCoroutine(SettleToGround(vessel));
            StartCoroutine(HandleVessel(vessel));
        }

        private IEnumerator HandleVessel(Vessel vessel)
        {
            // let BDArmory + parts initialize
            yield return new WaitForSeconds(1.0f);

            if (vessel == null || !vessel.loaded)
                yield break;

            if (!IsTargetVessel(vessel))
                yield break;

            UnityEngine.Debug.Log("[BDA-AI] Enabling AI for: " + vessel.vesselName);
            EnableAI(vessel);
            EnableGaurdMode(vessel);
        }

        private bool IsTargetVessel(Vessel vessel)
        {
            if (string.IsNullOrEmpty(vessel.vesselName))
                return false;

            // SIMPLE CONTRACT ENEMY DETECTION
            if (vessel.vesselName.Contains("Enemy") ||
                vessel.vesselName.Contains("CC_") ||
                vessel.vesselName.Contains("Target"))
            {
                return true;
            }

            return false;
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
                UnityEngine.Debug.Log("[BDA-AI] Pilot AI enabled");

            }
        }

        private void EnableGaurdMode(Vessel vessel)
        {
            foreach (var wm in vessel.FindPartModulesImplementing<BDArmory.Control.MissileFire>())
            {
                if (!wm.guardMode)
                {
                    wm.ToggleGuardMode();
                    UnityEngine.Debug.Log("[BDA-AI] Guard mode enabled for weapon: " + vessel.name);
                    UnityEngine.Debug.Log("Spawned at " + vessel.GetWorldPos3D());
                }


                UnityEngine.Debug.Log("[BDA-AI] Guard mode enabled for weapon: " + wm.part.partName);
            }
        }
    }
}