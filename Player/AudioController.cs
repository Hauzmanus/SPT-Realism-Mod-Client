﻿using Comfort.Common;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using EFT;

namespace RealismMod
{
    public static class AudioController
    {
        private const float _baseBreathVolume = 0.2f;
        private static float _currentBreathClipLength = 0f;
        private static float _breathTimer = 0f;
        private static float _breathCountdown = 2.5f;
        private static float _coughTimer = 0f;
        private static bool _breathedOut = false;

        public static void HazardZonesAudioController()
        {
            if (Utils.IsInHideout || !Utils.PlayerIsReady) return;

            _breathTimer += Time.deltaTime;
            _coughTimer += Time.deltaTime;  

            if (GearController.HasGasMask && _breathCountdown > 0f)
            {
                _breathCountdown -= Time.deltaTime;
            }
            else 
            {
                _breathCountdown = 2.5f;
            }

            if (_breathTimer > _currentBreathClipLength && _breathCountdown <= 0f && GearController.HasGasMask) 
            {
                Player player = Utils.GetYourPlayer();
                bool isNotBusy = !player.Speaker.Busy || !player.Speaker.Speaking;
                if (isNotBusy) 
                {
                    PlayGasMaskBreathing(_breathedOut, player);
                    _breathTimer = 0f;
                    _breathedOut = !_breathedOut;
                }
            }

            if (_coughTimer > 5f) 
            {
                CoughController(Utils.GetYourPlayer());
                _coughTimer = 0f;
            }

            DeviceController.DoGasAnalyserAudio();
            DeviceController.DoGeigerAudio();
        }

        private static void CoughController(Player player) 
        {
            if (Plugin.RealHealthController.DoCoughingAudio) player.Speaker.Play(EPhraseTrigger.OnBreath, ETagStatus.Dying | ETagStatus.Aware, true, null);
        }

        private static float GetBreathVolume() 
        {
            return _baseBreathVolume * (2f - PlayerState.StaminaPerc) * PluginConfig.GasMaskBreathVolume.Value;
        }

        private static string GetAudioFromOtherStates()
        {
            if (HazardTracker.TotalToxicity >= 70f || HazardTracker.TotalRadiation >= 85f)
            {
                return "Dying";
            }
            if (HazardTracker.TotalToxicity >= 50f || PlayerState.StaminaPerc <= 0.55 || HazardTracker.TotalRadiation >= 70f || Plugin.RealHealthController.HasAdrenalineEffect)
            {
                return "BadlyInjured";
            }
            if (HazardTracker.TotalToxicity >= 30f || PlayerState.StaminaPerc <= 0.8f || HazardTracker.TotalRadiation >= 50f)
            {
                return "Injured";
            }
            return "Healthy";
        }

        private static string ChooseAudioClip(string healthStatus, string desiredClip)
        {
            if (desiredClip == "Dying" || healthStatus == "Dying")
            {
                return "Dying";
            }
            if (desiredClip == "BadlyInjured" || healthStatus == "BadlyInjured")
            {
                return "BadlyInjured";
            }
            if (desiredClip == "Injured" || healthStatus == "Injured")
            {
                return "Injured";
            }
            return "Healthy";
        }

        public static void PlayGasMaskBreathing(bool breathOut, Player player)
        {
            int rndNumber = UnityEngine.Random.Range(1, 4);
            string healthStatus = player.HealthStatus.ToString();
            string desiredClip = GetAudioFromOtherStates();
            string clipToUse = ChooseAudioClip(healthStatus, desiredClip);
            string inOut = breathOut ? "out" : "in";
            string clipName = inOut + "_" + clipToUse + rndNumber + ".wav";
            AudioClip audioClip = Plugin.GasMaskAudioClips[clipName];
            _currentBreathClipLength = audioClip.length;
            float playBackVolume = GetBreathVolume();
            player.SpeechSource.SetLowPassFilterParameters(0.99f, ESoundOcclusionType.Obstruction, 1600, 5000, true);
            Singleton<BetterAudio>.Instance.PlayAtPoint(new Vector3(0, 0, 0), audioClip, 0, BetterAudio.AudioSourceGroupType.Nonspatial, 100, playBackVolume, EOcclusionTest.None, null, false);
        }

        public static void HeadsetVolumeAdjust() 
        {
            if (Input.GetKeyDown(PluginConfig.IncGain.Value.MainKey) && DeafeningController.HasHeadSet)
            {
                if (PluginConfig.RealTimeGain.Value < 30)
                {
                    PluginConfig.RealTimeGain.Value += 1f;
                    Singleton<BetterAudio>.Instance.PlayAtPoint(new Vector3(0, 0, 0), Plugin.HitAudioClips["beep.wav"], 0, BetterAudio.AudioSourceGroupType.Nonspatial, 100, 1.0f, EOcclusionTest.None, null, false);
                }
            }
            if (Input.GetKeyDown(PluginConfig.DecGain.Value.MainKey) && DeafeningController.HasHeadSet)
            {

                if (PluginConfig.RealTimeGain.Value > 0)
                {
                    PluginConfig.RealTimeGain.Value -= 1f;
                    Singleton<BetterAudio>.Instance.PlayAtPoint(new Vector3(0, 0, 0), Plugin.HitAudioClips["beep.wav"], 0, BetterAudio.AudioSourceGroupType.Nonspatial, 100, 1.0f, EOcclusionTest.None, null, false);
                }
            }

        }
    }
}
