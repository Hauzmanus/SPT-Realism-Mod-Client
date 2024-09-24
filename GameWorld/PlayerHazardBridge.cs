﻿using EFT;
using EFT.InventoryLogic;
using Comfort.Common;
using System.Collections.Generic;
using UnityEngine;
using ExistanceClass = GClass2470;
using System.Linq;

namespace RealismMod
{
    public class PlayerHazardBridge : MonoBehaviour
    {
        private const float BOT_INTERVAL = 10f;
        private const float SPAWNTIME = 30f;
        public Player _Player { get; set; }
        public bool IsBot { get; private set; } = false;
        public bool SpawnedInZone { get; private set; } = false;
        public bool ZoneBlocksNav { get; set; } = false;
        public int GasZoneCount { get; set; } = 0;
        public int RadZoneCount { get; set; } = 0;
        public int SafeZoneCount { get; set; } = 0;
        public Dictionary<string, bool> SafeZones = new Dictionary<string, bool>();
        public Dictionary<string, float> GasRates = new Dictionary<string, float>(); //to accomodate being in multiple zones
        public Dictionary<string, float> RadRates = new Dictionary<string, float>(); //to accomodate being in multiple zones
        private bool _isProtectedFromSafeZone;

        public bool IsProtectedFromSafeZone
        {
            get
            {
                return _isProtectedFromSafeZone;
            }
        }

        public float TotalGasRate
        {
            get
            {
                if (IsProtectedFromSafeZone) return 0f;
                float totalGas = 0f;
                foreach (var gas in GasRates)
                {
                    totalGas += gas.Value;
                }
                return totalGas;
            }
        }

        public float TotalRadRate
        {
            get
            {
                if (IsProtectedFromSafeZone) return 0f;
                float totalRads = 0f;
                foreach (var rad in RadRates)
                {
                    totalRads += rad.Value;
                }
                return totalRads;
            }
        }

        private float _safeZoneCheckTimer = 0f;
        private float _botTimer = 0f;
        private float _timeActive = 0f;
        private bool _checkedSpawn = false;

        private bool BotHasGasmask()
        {
            if (_Player?.Inventory == null || _Player?.Equipment == null) return true;
            Item containedItem = _Player.Inventory?.Equipment?.GetSlot(EquipmentSlot.FaceCover)?.ContainedItem;
            if (containedItem == null) return false;
            return GearStats.IsGasMask(containedItem);
        }

        private void HandleGas(bool hasGasmask)
        {
            if (GasZoneCount > 0 && !IsProtectedFromSafeZone)
            {
                if (!hasGasmask && TotalGasRate > 0.05f)
                {
                    _Player.ActiveHealthController.ApplyDamage(EBodyPart.Chest, TotalGasRate * BOT_INTERVAL * 0.75f, ExistanceClass.PoisonDamage);

                    if (_Player.ActiveHealthController.GetBodyPartHealth(EBodyPart.Chest).Current <= 115f)
                    {
                        _Player.Speaker.Play(EPhraseTrigger.OnBreath, ETagStatus.Dying | ETagStatus.Aware, true, null);
                    }
                }
            }
        }

        private void HandleRads(bool hasGasmask)
        {
            if (RadZoneCount > 0 && !IsProtectedFromSafeZone)
            {
                float realRadRate = hasGasmask ? TotalRadRate * 0.5f : TotalRadRate;
                if (realRadRate > 0.5f)
                {
                    _Player.ActiveHealthController.ApplyDamage(EBodyPart.Chest, realRadRate * BOT_INTERVAL, ExistanceClass.RadiationDamage);
                }
            }
        }

        private void CheckSpawnPoint()
        {
            if (!_checkedSpawn)
            {
                _timeActive += Time.deltaTime;
                if ((GasZoneCount > 0 || RadZoneCount > 0) && !IsProtectedFromSafeZone)
                {
                    SpawnedInZone = true;
                    MoveEntityToSafeLocation();
                }

                if (_timeActive >= SPAWNTIME || SpawnedInZone || (!IsBot && PlayerState.IsMoving)) _checkedSpawn = true;
            }
        }

        private void MoveEntityToSafeLocation()
        {
            _Player.Transform.position = HazardZoneSpawner.GetSafeSpawnPoint(_Player, IsBot, ZoneBlocksNav, RadZoneCount > 0);
            Utils.Logger.LogWarning("Realism Mod: Spawned in Hazard, moved to " + _Player.Transform.position + ", Was Bot? " + IsBot);
        }

        private void CheckSafeZones()
        {
            _safeZoneCheckTimer += Time.deltaTime;
            if (_safeZoneCheckTimer >= 1f)
            {
                _isProtectedFromSafeZone = false;
                foreach (var zone in SafeZones)
                {
                    if (zone.Value == true) _isProtectedFromSafeZone = true;
                }
                _safeZoneCheckTimer = 0f;
            }
        }

        //temporary solution to dealing with bots
        private void BotCheck() 
        {
            _botTimer += Time.deltaTime;
            IsBot = _Player?.AIData?.BotOwner != null || _Player.IsAI;
            if (_botTimer >= BOT_INTERVAL)
            {
                bool isAliveBot = IsBot && _Player != null && _Player?.ActiveHealthController != null && !_Player.AIData.BotOwner.IsDead && _Player.HealthController.IsAlive;
                if (!isAliveBot) return;
                bool hasGasmask = BotHasGasmask();
                HandleGas(hasGasmask);
                HandleRads(hasGasmask);
                _botTimer = 0f;
            }
        } 

        //for bots
        void Update()
        {
            CheckSpawnPoint();
            CheckSafeZones();
            BotCheck();
        }
    }

}
