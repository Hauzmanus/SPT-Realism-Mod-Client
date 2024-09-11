﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using EFT;
using System.Linq;
using UnityEngine.Assertions;
using EFT.Quests;

namespace RealismMod
{
    public static class HazardZoneSpawner
    {
        public const float MinBotSpawnDistanceFromPlayer = 150f;

        //for player, get closest spawn. For bot, sort by min distance, or furthest from player failing that.
        public static Vector3 GetSafeSpawnPoint(Player entitiy, bool isBot, bool blocksNav, bool isInRads)
        {
            IEnumerable<Vector3> spawns = HazardZoneData.GetSafeSpawn();
            if (spawns == null || (isBot && !blocksNav) || (!isBot && GameWorldController.CurrentMap == "laboratory" && !isInRads)) return entitiy.Transform.position; //can't account for bot vs player, because of maps like Labs where player should spawn in gas
            IEnumerable<Vector3> validSpawns = spawns;
            Player player = Utils.GetYourPlayer();

            if (isBot)
            {
                validSpawns = spawns.Where(s => Vector3.Distance(s, player.Transform.position) >= MinBotSpawnDistanceFromPlayer);
            }

            if (validSpawns.Any() || !isBot)
            {
                return validSpawns.OrderBy(s => Vector3.Distance(s, entitiy.Transform.position)).First();
            }
            else 
            {
                return spawns.OrderByDescending(s => Vector3.Distance(s, player.Transform.position)).First();
            }
        }

        public static void CreateZones(ZoneCollection collection)
        {
            var zones = HazardZoneData.GetZones(collection.ZoneType, GameWorldController.CurrentMap);
            if (zones == null) return;
            foreach (var zone in zones)
            {
                if (collection.ZoneType == EZoneType.Gas || collection.ZoneType == EZoneType.GasAssets) CreateZone<GasZone>(zone, EZoneType.Gas);
                else CreateZone<RadiationZone>(zone, EZoneType.Radiation);
            }
        }

        private static bool ShouldSpawnZone(float zoneProbability, EZoneType zoneType) 
        {
            if(PluginConfig.ZoneDebug.Value) return true;

            if (!Plugin.FikaPresent) 
            {
                bool doTimmyFactor = ProfileData.PMCLevel <= 10f && zoneType != EZoneType.Radiation;
                float timmyFactor = doTimmyFactor && GameWorldController.CurrentMap == "sandbox" ? 0f : doTimmyFactor ? 0.25f : 1f;
                zoneProbability = Mathf.Max(zoneProbability * timmyFactor, 0.01f);
                zoneProbability = Mathf.Clamp01(zoneProbability);
                float randomValue = UnityEngine.Random.value;
                return randomValue <= zoneProbability;
            }

            DateTime utcNow = DateTime.UtcNow;
            int seed = utcNow.Year * 1000000 + utcNow.Month * 10000 + utcNow.Day * 100 + utcNow.Hour * 10;
            int finalSeed = seed % 101;
            return finalSeed <= zoneProbability * 100f;    
        }

        public static void CreateZone<T>(HazardLocation zone, EZoneType zoneType) where T : MonoBehaviour, IHazardZone
        {
            if (!ShouldSpawnZone(zone.SpawnChance, zoneType)) return;

            HandleZoneAssets(zone);
            HandleZoneLoot(zone);

            foreach (var subZone in zone.Zones) 
            {
                string zoneName = subZone.Name;
                Vector3 position = new Vector3(subZone.Position.X, subZone.Position.Y, subZone.Position.Z);
                Vector3 rotation = new Vector3(subZone.Rotation.X, subZone.Rotation.Y, subZone.Rotation.Z);
                Vector3 size = new Vector3(subZone.Size.X, subZone.Size.Y, subZone.Size.Z);
                Vector3 scale = size;

                GameObject hazardZone = new GameObject(zoneName);
                T hazard = hazardZone.AddComponent<T>();

                float strengthModifier = 1f;
                if ((hazard.ZoneType == EZoneType.Gas || hazard.ZoneType == EZoneType.GasAssets) && (!Plugin.FikaPresent && !PluginConfig.ZoneDebug.Value))
                {
                    strengthModifier = UnityEngine.Random.Range(0.95f, 1.3f);
                }
                hazard.ZoneStrengthModifier = subZone.Strength * strengthModifier;

                hazardZone.transform.position = position;
                hazardZone.transform.rotation = Quaternion.Euler(rotation);

                EFT.Interactive.TriggerWithId trigger = hazardZone.AddComponent<EFT.Interactive.TriggerWithId>();
                trigger.SetId(zoneName);

                string questZoneName = zone.Assets == null ? zoneName : "dynamic" + GameWorldController.CurrentMap;

                EFT.Interactive.ExperienceTrigger questTrigger = hazardZone.AddComponent<EFT.Interactive.ExperienceTrigger>();
                questTrigger.SetId(questZoneName);

                EFT.Interactive.PlaceItemTrigger placeIemTrigger = hazardZone.AddComponent<EFT.Interactive.PlaceItemTrigger>();
                placeIemTrigger.SetId(questZoneName);

                hazardZone.layer = LayerMask.NameToLayer("Triggers");
                hazardZone.name = zoneName;

                BoxCollider boxCollider = hazardZone.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.size = size;

                hazard.BlocksNav = subZone.BlockNav;
                if (subZone.BlockNav)
                {
                    var navMeshObstacle = hazardZone.AddComponent<NavMeshObstacle>();
                    navMeshObstacle.carving = true;
                    navMeshObstacle.center = boxCollider.center;
                    navMeshObstacle.size = boxCollider.size;
                }

                // visual representation for debugging
                if (PluginConfig.ZoneDebug.Value)
                {
                    GameObject visualRepresentation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visualRepresentation.name = zoneName + "Visual";
                    visualRepresentation.transform.parent = hazardZone.transform;
                    visualRepresentation.transform.localScale = size;
                    visualRepresentation.transform.localPosition = boxCollider.center;
                    visualRepresentation.transform.rotation = boxCollider.transform.rotation;
                    visualRepresentation.GetComponent<Renderer>().material.color = hazard.ZoneType == EZoneType.Radiation || hazard.ZoneType == EZoneType.RadAssets ? new Color(0f, 1f, 0f, 0.15f) : new Color(1f, 0f, 0f, 0.15f);
                    UnityEngine.Object.Destroy(visualRepresentation.GetComponent<Collider>()); // Remove the collider from the visual representation
                    MoveDaCube.AddComponentToExistingGO(visualRepresentation, zoneName);
                }
            }
        }

        public static void HandleZoneAssets(HazardLocation zone) 
        {
            if (zone.Assets == null) return;
            foreach (var asset in zone.Assets) 
            {
                if (Utils.SystemRandom.Next(101) > asset.Odds && !Plugin.FikaPresent) continue;

                if (asset.RandomizeRotation) 
                {
                    asset.Rotation.Y = Utils.SystemRandom.Range(0, 360);
                }

                Vector3 position = new Vector3(asset.Position.X, asset.Position.Y, asset.Position.Z);
                Vector3 rotaiton = new Vector3(asset.Rotation.X, asset.Rotation.Y, asset.Rotation.Z);

                GameObject containerPrefab = GetAndLoadAsset(asset.AssetName);
                if (containerPrefab == null) 
                {
                    Utils.Logger.LogError("Realism Mod: Error Loading Asset From Bundle For Asset: " + asset.AssetName);
                }
                GameObject spawnedContainer = UnityEngine.Object.Instantiate(containerPrefab, position, Quaternion.Euler(rotaiton));
            }
        }

        public static void HandleZoneLoot(HazardLocation zone)
        {
            if (zone.Loot == null || Plugin.FikaPresent) return;
            foreach (var loot in zone.Loot)
            {
                if (Utils.SystemRandom.Next(101) > loot.Odds) continue;

                if (loot.RandomizeRotation)
                {
                    loot.Rotation.Y = Utils.SystemRandom.Range(0, 360);
                }

                Vector3 position = new Vector3(loot.Position.X, loot.Position.Y, loot.Position.Z);
                Vector3 rotaiton = new Vector3(loot.Rotation.X, loot.Rotation.Y, loot.Rotation.Z);

                LoadLooseLoot(position, rotaiton, GetLootTempalteId(loot.Type));
            }
        }

        public static string GetLootTempalteId(string lootTier) 
        {
            Dictionary<string, int> lootDict;
            switch (lootTier) 
            {
       
                case "highTier":
                    lootDict = ZoneLoot.HighTier;
                    break;
                case "midTier":
                    lootDict = ZoneLoot.MidTier;
                    break;
                case "lowTier":
                default:
                    lootDict = ZoneLoot.LowTier;
                    break;

            }
            return Utils.GetRandomWeightedKey(lootDict);
        }

        //previously I stored the loaded assets as static fields and used reflection to dynamically load them, however this strangely caused issues with certain bundles,
        //so instead I have to use this method to manually load in assets
        public static GameObject GetAndLoadAsset(string assetName)
        {
            if (assetName == "GooBarrel") return Assets.GooBarrelBundle.LoadAsset<GameObject>("Assets/Labs/yellow_barrel.prefab");
            if (assetName == "BlueBox") return Assets.BlueBoxBundle.LoadAsset<GameObject>("Assets/Prefabs/polytheneBox (6).prefab");
            if (assetName == "RedForkLift") return Assets.RedForkLiftBundle.LoadAsset<GameObject>("Assets/Prefabs/autoloader.prefab");
            if (assetName == "ElectroForkLift") return Assets.ElectroForkLiftBundle.LoadAsset<GameObject>("Assets/Prefabs/electroCar (2).prefab");
            if (assetName == "LabsCrate") return Assets.LabsCrateBundle.LoadAsset<GameObject>("Assets/Prefabs/woodBox_medium.prefab");
            if (assetName == "Ural") return Assets.UralBundle.LoadAsset<GameObject>("Assets/Prefabs/ural280_closed_update.prefab");
            if (assetName == "BluePallet") return Assets.BluePalletBundle.LoadAsset<GameObject>("Assets/Prefabs/pallete_plastic_blue (10).prefab");
            if (assetName == "BlueFuelPalletCloth") return Assets.BlueFuelPalletClothBundle.LoadAsset<GameObject>("Assets/Prefabs/pallet_barrel_heap_update.prefab");
            if (assetName == "BarrelPile") return Assets.BarrelPileBundle.LoadAsset<GameObject>("Assets/Prefabs/barrel_pile (1).prefab");
            if (assetName == "LabsCrateSmall") return Assets.LabsCrateSmallBundle.LoadAsset<GameObject>("Assets/Prefabs/woodBox_small (2).prefab");
            if (assetName == "YellowPlasticPallet") return Assets.YellowPlasticPalletBundle.LoadAsset<GameObject>("Assets/Prefabs/pallet_barrel_plastic_clear_P (4).prefab");
            if (assetName == "WhitePlasticPallet") return Assets.WhitePlasticPalletBundle.LoadAsset<GameObject>("Assets/Prefabs/pallet_barrel_plastic_clear_P (5).prefab");
            if (assetName == "MetalFence") return Assets.MetalFenceBundle.LoadAsset<GameObject>("Assets/Prefabs/fence_metall_part3_update.prefab");
            if (assetName == "RedContainer") return Assets.RedContainerBundle.LoadAsset<GameObject>("Assets/Prefabs/container_6m_red_close.prefab"); if (assetName == "RedContainer") return Assets.RedContainerBundle.LoadAsset<GameObject>("Assets/Prefabs/container_6m_red_close.prefab");
            if (assetName == "BlueContainer") return Assets.BlueContainerBundle.LoadAsset<GameObject>("container_6m_blue_close (1)");
            return null;
        }

        private static void LoadLooseLoot(Vector3 postion, Vector3 rotation, string tempalteId)
        {
            Quaternion quat = Quaternion.Euler(rotation);
#pragma warning disable CS4014 
            Utils.LoadLoot(postion, quat, tempalteId); //yes, I know this isn't running asnyc
#pragma warning restore CS4014 
        }

        public static void DebugZones()
        {
            string targetZone = PluginConfig.TargetZone.Value;
            GameObject gasZone = GameObject.Find(targetZone);
            if (gasZone == null)
            {
                gasZone = new GameObject(targetZone);
                gasZone.transform.position = new Vector3(PluginConfig.test4.Value, PluginConfig.test5.Value, PluginConfig.test6.Value);
                gasZone.transform.rotation = Quaternion.Euler(new Vector3(PluginConfig.test7.Value, PluginConfig.test8.Value, PluginConfig.test9.Value));

                EFT.Interactive.TriggerWithId trigger = gasZone.AddComponent<EFT.Interactive.TriggerWithId>();
                trigger.SetId(targetZone);

                gasZone.layer = LayerMask.NameToLayer("Triggers");
                gasZone.name = targetZone;

                BoxCollider boxCollider = gasZone.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.size = new Vector3(PluginConfig.test1.Value, PluginConfig.test2.Value, PluginConfig.test3.Value);

                // visual representation for debugging
                GameObject visualRepresentation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visualRepresentation.name = targetZone + "Visual";
                visualRepresentation.transform.parent = gasZone.transform;
                visualRepresentation.transform.localScale = boxCollider.size;
                visualRepresentation.transform.localPosition = boxCollider.center;
                visualRepresentation.transform.rotation = boxCollider.transform.rotation;
                visualRepresentation.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 0.25f);
                UnityEngine.Object.Destroy(visualRepresentation.GetComponent<Collider>()); // Remove the collider from the visual representation

                Utils.Logger.LogWarning("player pos " + Utils.GetYourPlayer().Transform.position);
                Utils.Logger.LogWarning("gasZone pos " + gasZone.transform.position);
                Utils.Logger.LogWarning("gasZone rot " + gasZone.transform.rotation);
                Utils.Logger.LogWarning("gasZone size " + gasZone.GetComponent<BoxCollider>().size);
            }
            else 
            {
                gasZone.transform.position = new Vector3(PluginConfig.test4.Value, PluginConfig.test5.Value, PluginConfig.test6.Value);
                gasZone.transform.rotation = Quaternion.Euler(new Vector3(PluginConfig.test7.Value, PluginConfig.test8.Value, PluginConfig.test9.Value));
                BoxCollider boxCollider = gasZone.GetComponent<BoxCollider>();
                boxCollider.size = new Vector3(PluginConfig.test1.Value, PluginConfig.test2.Value, PluginConfig.test3.Value);

                GameObject visualRepresentation = GameObject.Find(targetZone + "Visual");
                visualRepresentation.transform.parent = gasZone.transform;
                visualRepresentation.transform.localScale = boxCollider.size;
                visualRepresentation.transform.localPosition = boxCollider.center;
                visualRepresentation.transform.rotation = boxCollider.transform.rotation;
                Utils.Logger.LogWarning("player pos " + Utils.GetYourPlayer().Transform.position);
                Utils.Logger.LogWarning("gasZone pos " + gasZone.transform.position);
                Utils.Logger.LogWarning("gasZone rot " + gasZone.transform.rotation);
                Utils.Logger.LogWarning("gasZone size " + gasZone.GetComponent<BoxCollider>().size);

                /* UnityEngine.Object.Destroy(GameObject.Find("DebugZone"));*/
            }
        }
    }
}
