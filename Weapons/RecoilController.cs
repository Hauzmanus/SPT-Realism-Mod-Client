﻿using BepInEx.Logging;
using EFT.Animations;
using EFT.Animations.NewRecoil;
using EFT.InventoryLogic;
using EFT;
using UnityEngine;
using static EFT.Player;

namespace RealismMod
{
    public class RecoilController
    {
        public static bool IsFiring = false;
        public static bool IsFiringDeafen = false;
        public static bool IsFiringMovement = false;
        public static bool IsFiringWiggle = false;
        public static int ShotCount = 0;
        public static float PrevShotCount = ShotCount;
        public static float ShotTimer = 0.0f;
        public static float DeafenShotTimer = 0.0f;
        public static float WiggleShotTimer = 0.0f;
        public static float MovementSpeedShotTimer = 0.0f;

        public static float BaseTotalHRecoil;
        public static float BaseTotalVRecoil;
        public static float BaseTotalDispersion;
        public static float BaseTotalCamRecoil;

        public static float BaseTotalRecoilDamping;
        public static float BaseTotalHandDamping;

        public static float BaseTotalConvergence;
        public static float BaseTotalRecoilAngle;

        public static float PlayerControl;

        public static float FactoredTotalHRecoil;
        public static float FactoredTotalVRecoil;
        public static float FactoredTotalDispersion;
        public static float FactoredTotalCamRecoil;

        public static void RecoilUpdate() 
        {
            if (RecoilController.ShotCount > RecoilController.PrevShotCount)
            {
                RecoilController.IsFiring = true;
                RecoilController.IsFiringDeafen = true;
                RecoilController.IsFiringWiggle = true;
                StanceController.IsFiringFromStance = true;
                RecoilController.IsFiringMovement = true;
                RecoilController.PrevShotCount = RecoilController.ShotCount;
            }

            if (RecoilController.ShotCount == RecoilController.PrevShotCount) //&& !IsFiringEFT
            {
                RecoilController.DeafenShotTimer += Time.deltaTime;
                RecoilController.WiggleShotTimer += Time.deltaTime;
                RecoilController.ShotTimer += Time.deltaTime;
                RecoilController.MovementSpeedShotTimer += Time.deltaTime;

                if (RecoilController.ShotTimer >= PluginConfig.ResetTime.Value)
                {
                    RecoilController.IsFiring = false;
                    RecoilController.ShotCount = 0;
                    RecoilController.PrevShotCount = 0;
                    RecoilController.ShotTimer = 0f;
                }

                if (RecoilController.DeafenShotTimer >= PluginConfig.DeafenResetDelay.Value)
                {
                    RecoilController.IsFiringDeafen = false;
                    RecoilController.DeafenShotTimer = 0f;
                }

                if (RecoilController.WiggleShotTimer >= 0.12f)
                {
                    RecoilController.IsFiringWiggle = false;
                    RecoilController.WiggleShotTimer = 0f;
                }

                if (RecoilController.MovementSpeedShotTimer >= 0.5f)
                {
                    RecoilController.IsFiringMovement = false;
                    RecoilController.MovementSpeedShotTimer = 0f;
                }

                StanceController.StanceShotTimer();
            }
        }

        private static float PistolShotFactor(Weapon weapon)
        {
            if (weapon.WeapClass != "pistol" || weapon.SelectedFireMode != Weapon.EFireMode.fullauto) return 1f;

            switch (ShotCount)
            {
                case 0:
                    return 1.1f;
                case 1:
                    return 2.5f;
                case 2:
                    return 2.25f;
                case 3:
                    return 2f;
                case 4:
                    return 1.8f;
                case 5:
                    return 1.7f;
                case 6:
                    return 1.6f;
                case 7:
                    return 1.5f;
                case >= 8:
                    return 1.4f;
                default:
                    return 1;
            }
        }

        public static void DoVisualRecoil(ref Vector3 targetRecoil, ref Vector3 currentRecoil, ref Quaternion weapRotation, ManualLogSource logger)
        {
            float cantedRecoilSpeed = Mathf.Clamp(BaseTotalConvergence * 0.95f, 9f, 16f);

            if (RecoilController.IsFiringWiggle)
            {
                float cantedRecoilAmount = FactoredTotalHRecoil / 32f;
                float totalCantedRecoil = Mathf.Lerp(-cantedRecoilAmount, cantedRecoilAmount, Mathf.PingPong(Time.time * cantedRecoilSpeed * 1.05f, 1.0f));
                float additionalRecoilAmount = FactoredTotalDispersion / 16f;
                float totalSideRecoil = Mathf.Lerp(-additionalRecoilAmount, additionalRecoilAmount, Mathf.PingPong(Time.time * cantedRecoilSpeed, 1.0f)) * 0.05f;
                float totalVertical = Mathf.Lerp(-additionalRecoilAmount, additionalRecoilAmount, Mathf.PingPong(Time.time * cantedRecoilSpeed * 1.5f, 1.0f)) * 0.1f;
                targetRecoil = new Vector3(totalVertical * 0.95f, totalCantedRecoil, totalSideRecoil * 0.89f) * PluginConfig.VisRecoilMulti.Value * WeaponStats.CurrentVisualRecoilMulti;
            }
            else
            {
                targetRecoil = Vector3.Lerp(targetRecoil, Vector3.zero, 0.1f);
            }

            currentRecoil = Vector3.Lerp(currentRecoil, targetRecoil, 1f);
            Quaternion recoilQ = Quaternion.Euler(currentRecoil);
            weapRotation *= recoilQ;
        }

        public static void SetRecoilParams(ProceduralWeaponAnimation pwa, Weapon weapon, Player player)
        {
            NewRecoilShotEffect newRecoil = pwa.Shootingg.CurrentRecoilEffect as NewRecoilShotEffect;
            bool hasOptic = WeaponStats.IsOptic && StanceController.IsAiming;
            float shoulderContactFactor = weapon.WeapClass != "pistol" && !WeaponStats.HasShoulderContact ? 1.25f : WeaponStats.IsStockedPistol ? 0.85f : 1f;
            float opticFactorRear = StanceController.IsAiming && hasOptic ? 0.9f : 1f;
            float opticFactorVert = StanceController.IsAiming && hasOptic ? 0.95f : 1f;
            float stanceFactor = player.IsInPronePose ? 0.6f : 1f;

            newRecoil.HandRotationRecoil.CategoryIntensityMultiplier = weapon.Template.RecoilCategoryMultiplierHandRotation * PluginConfig.RecoilIntensity.Value * shoulderContactFactor;
       
            newRecoil.HandRotationRecoil.ReturnTrajectoryDumping = weapon.Template.RecoilReturnPathDampingHandRotation * PluginConfig.HandsDampingMulti.Value * opticFactorRear;
            pwa.Shootingg.CurrentRecoilEffect.HandRotationRecoilEffect.Damping = weapon.Template.RecoilDampingHandRotation * PluginConfig.RecoilDampingMulti.Value * opticFactorVert;

            pwa.Shootingg.CurrentRecoilEffect.CameraRotationRecoilEffect.Damping = PluginConfig.CamWiggle.Value;
            pwa.Shootingg.CurrentRecoilEffect.CameraRotationRecoilEffect.ReturnSpeed = PluginConfig.CamReturn.Value; 
            pwa.Shootingg.CurrentRecoilEffect.CameraRotationRecoilEffect.Intensity = 1; 

            pwa.Shootingg.CurrentRecoilEffect.HandPositionRecoilEffect.Damping = 0.68f; // 0.77
            pwa.Shootingg.CurrentRecoilEffect.HandPositionRecoilEffect.ReturnSpeed = 0.14f; //0.15

            newRecoil.HandRotationRecoil.NextStablePointDistanceRange.x = 1; //1  (defaults are 0.1, 6)
            newRecoil.HandRotationRecoil.NextStablePointDistanceRange.y = 4; //4

            if (PluginConfig.EnableHybridRecoil.Value && (PluginConfig.HybridForAll.Value || (!PluginConfig.HybridForAll.Value && !WeaponStats.HasShoulderContact)))
            {
                pwa.Shootingg.CurrentRecoilEffect.HandRotationRecoilEffect.ReturnSpeed = Mathf.Clamp((RecoilController.BaseTotalConvergence - Mathf.Clamp(25f + RecoilController.ShotCount, 0, 100f)) + Mathf.Clamp(15f + RecoilController.PlayerControl, 0f, 100f), 2f, RecoilController.BaseTotalConvergence * stanceFactor * PistolShotFactor(weapon));
            }
            else
            {
                pwa.Shootingg.CurrentRecoilEffect.HandRotationRecoilEffect.ReturnSpeed = RecoilController.BaseTotalConvergence * stanceFactor * PistolShotFactor(weapon);
            }
        }
    }
}
