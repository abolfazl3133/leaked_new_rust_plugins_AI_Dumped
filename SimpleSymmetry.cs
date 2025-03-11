using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using VLB;


//SimpleSymmetry created with PluginMerge v(1.0.4.0) by MJSU @ https://github.com/dassjosh/Plugin.Merge
namespace Oxide.Plugins
{
    [Info("SimpleSymmetry", "Shady14u", "1.0.9")]
    [Description("Effortlessly create symmetrical base designs")]
    public partial class SimpleSymmetry : RustPlugin
    {
        #region 0.SimpleSymmetry.cs
        private static SimpleSymmetry _instance;
        private readonly int _constructionMask = LayerMask.GetMask("Construction");
        private readonly int _terrainMask = LayerMask.GetMask("Terrain");
        private SphereEntity _sphere;
        
        #region
        
        private void AdjustSymmetry(PlayerBuildOptions buildOptions, BasePlayer player, SymmetryType symmetryType)
        {
            buildOptions.SymmetryType = symmetryType;
            player.ChatMessage($"Symmetry set to {symmetryType}.");
            ShowCurrentSymmetryPoint(player);
        }
        
        private void AttachToBuilding(BaseEntity entity, BuildingBlock newBuildBlock, BasePlayer player)
        {
            var block = entity.GetOrAddComponent<BuildingBlock>();
            if (block != null)
            {
                block.blockDefinition = PrefabAttribute.server.Find<Construction>(block.prefabID);
                block.SetGrade(newBuildBlock.grade);
                block.OwnerID = player.userID;
            }
            
            var baseCombatEntity = block as BaseCombatEntity;
            if (baseCombatEntity)
            {
                var num2 = (block != null) ? block.currentGrade.maxHealth : baseCombatEntity.startHealth;
                baseCombatEntity.ResetLifeStateOnSpawn = false;
                baseCombatEntity.InitializeHealth(num2, num2);
            }
            if (!CanPlace(block, player))
            {
                if (!block.IsDestroyed)
                {
                    block.Kill();
                }
                
                return;
            }
            
            block.Spawn();
            
            var decayEntity = block as DecayEntity;
            var nearbyBuildingBlock = GetNearbyBuildingBlock(decayEntity);
            if (nearbyBuildingBlock != null)
            {
                if (nearbyBuildingBlock.buildingID == 0)
                nearbyBuildingBlock.buildingID = BuildingManager.server.NewBuildingID();
                decayEntity.AttachToBuilding(nearbyBuildingBlock.buildingID);
            }
            
            var stability = (StabilityEntity)block;
            stability.UpdateSurroundingEntities();
            block.SendNetworkUpdate();
            
        }
        
        private bool CanPlace(BaseEntity foundation, BasePlayer player)
        {
            var buildOptions = GetBuildOptions(player.userID);
            var dist = Vector3.Distance(buildOptions.StartPoint, foundation.transform.position);
            if (buildOptions.SymmetryEnabled &&
            dist > _config.MaxRadiusForBuilding)
            {
                player.ChatMessage("You are to far from your center point to use symmetry");
                return false;
            }
            
            var volumes = PrefabAttribute.server.FindAll<DeployVolume>(foundation.prefabID);
            var trans = foundation.transform;
            
            if (DeployVolume.Check(trans.position, trans.rotation, volumes))
            {
                player.ChatMessage("Not enough space");
                return false;
            }
            
            if (player.IsBuildingBlocked(trans.position, trans.rotation, foundation.bounds))
            {
                player.ChatMessage("You don't have permission to build here");
                return false;
            }
            
            if (foundation.ShortPrefabName.Contains("foundation") && !CheckFoundationCollision(foundation))
            {
                return false;
            }
            
            if (IsToCloseToRoad(foundation)) return false;
            
            var block = foundation.GetComponent<BuildingBlock>();
            if (block == null) return false;
            
            var collect = new List<Item>();
            foreach (ItemAmount itemAmount in block.BuildCost())
            {
                player.inventory.Take(collect, itemAmount.itemid, (int) itemAmount.amount);
                player.Command("note.inv " + itemAmount.itemid + " " + (float) (itemAmount.amount * -1.0));
            }
            
            foreach (Item obj in collect)
            obj.Remove();
            return true;
        }
        
        private bool CheckFoundationCollision(BaseEntity entity)
        {
            RaycastHit hitInfo;
            var ray = new Ray(entity.transform.position, Vector3.down);
            Physics.Raycast(ray, out hitInfo,10, LayerMask.GetMask("Terrain"));
            return hitInfo.distance<=2.8 && !SocketMod_TerrainCheck.IsInTerrain(entity.transform.position);
        }
        
        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax,
        string offsetMin, string offsetMax, float padding, string buttonColor, string textColor, string buttonText,
        int fontSize, string buttonCommand, string parent = "Overlay",
        TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                },
                Image = {Color = "0 0 0 0"}
            }, parent);
            
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = $"{padding} {padding}",
                    AnchorMax = $"{1 - padding} {1 - padding}"
                },
                Button = {Color = buttonColor, Command = $"{buttonCommand}"},
                Text = {Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText}
            }, panel);
            return panel;
        }
        
        private static string CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax,
        string offsetMin, string offsetMax, float padding, string imageData, string parent = "Overlay",
        string panelName = null)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                },
                Image = {Color = "0 0 0 0"}
            }, parent, panelName);
            
            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{padding} {padding + .004f}",
                        AnchorMax = $"{1 - padding - .004f} {1 - padding - .02f}"
                    },
                    new CuiRawImageComponent {Png = imageData}
                }
            });
            
            return panel;
        }
        
        private string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax,
        string offsetMin, string offsetMax, float padding, string backgroundColor, string textColor,
        string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay",
        string labelName = null)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax,
                    OffsetMin = offsetMin, OffsetMax = offsetMax
                },
                Image = {Color = backgroundColor}
            }, parent, labelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize
                },
                RectTransform =
                {
                    AnchorMin = $"{padding} {padding}", AnchorMax = $"{1 - padding} {1 - padding}"
                }
            }, panel);
            return panel;
        }
        
        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax,
        string offsetMin, string offsetMax, string panelColor, string parent = "Overlay",
        string panelName = null)
        {
            return container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                },
                Image = {Color = panelColor}
            }, parent, panelName);
        }
        
        private void CreateSymmetryPanel(ref CuiElementContainer container, string anchorMin, string anchorMax,
        string offsetMin, string offsetMax, string backgroundColor, PlayerBuildOptions buildOptions,
        string parent = "Overlay", string buttonName = "")
        {
            var btnHeight = 36;
            var top = 0;
            var symmetryPanel = CreatePanel(ref container, anchorMin, anchorMax, offsetMin, offsetMax, backgroundColor,
            parent);
            var labelOffset = 0f;
            
            if (buildOptions.SymmetryShape == SymmetryShape.Triangle)
            {
                labelOffset = -25;
            }
            
            var symImagePanel = CreateImagePanel(ref container, Anchors.TopLeft, Anchors.TopLeft, $"5 {top - 132}",
            $"140 {top}", .02f,
            GetSymmetryImage(buildOptions),
            symmetryPanel);
            
            CreateLabel(ref container, Anchors.Center, Anchors.Center, $"-100 {labelOffset - 10}",
            $"100 {labelOffset + 10}", .02f,
            "0 0 0 0", "1 1 1 1",
            ToSentenceCase(GetMsg(buildOptions.SymmetryEnabled ? buildOptions.SymmetryType.ToString() : "SymmetryNotSet")),
            10, TextAnchor.MiddleCenter, symImagePanel);
            
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-95 {top - 136}", "-70 1.5", .04f,
            GetButtonColor(buttonName == "cycle"), "1 1 1 1",
            ">", 8, "symmetry cycle", "SymmetryPanel");
            
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-70 -25", $"-35 0", .2f,
            GetButtonColor(buttonName == "minimize"),
            "1 1 1 1", "_", 8, "symmetry minimize", "SymmetryPanel");
            
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-35 -25", $"0 0", .2f,
            GetButtonColor(buttonName == "close"),
            "1 1 1 1", "X", 8, "symmetry ui", "SymmetryPanel");
            
            top = -25;
            
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-70 {top - btnHeight}", $"0 -25", .1f,
            GetButtonColor(buttonName == "set"),
            "1 1 1 1", GetMsg(PluginMessages.SetSymmetry), 8, "symmetry set", "SymmetryPanel");
            top -= btnHeight;
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-70 {top - btnHeight}", $"0 {top}", .1f,
            GetButtonColor(buttonName == "toggle"),
            "1 1 1 1", GetMsg(PluginMessages.ToggleSymmetry), 8, "symmetry toggle", "SymmetryPanel");
            top -= btnHeight;
            CreateButton(ref container, Anchors.TopRight, Anchors.TopRight, $"-70 {top - btnHeight}", $"0 {top}", .1f,
            GetButtonColor(buttonName == "delete"),
            "1 1 1 1", GetMsg(PluginMessages.DeleteSymmetry), 8, "symmetry delete", "SymmetryPanel");
        }
        
        private void DemolishByType(SymmetryShape symmetryShape, SymmetryType symmetryType, Vector3 startPoint,
        BaseNetworkable entity, Quaternion buildOptionsStartRotation)
        {
            switch (symmetryShape)
            {
                case SymmetryShape.Triangle:
                DemolishGeneric(startPoint, entity, 3);
                break;
                case SymmetryShape.Hexagon:
                switch (symmetryType)
                {
                    case SymmetryType.Normal6Sided:
                    DemolishGeneric(startPoint, entity, 6);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    DemolishGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal3Sided:
                    DemolishGeneric(startPoint, entity, 3);
                    return;
                    default:
                    DemolishGeneric(startPoint, entity, 2);
                    return;
                }
                case SymmetryShape.Square:
                case SymmetryShape.Rectangle:
                switch (symmetryType)
                {
                    case SymmetryType.Normal2Sided:
                    DemolishGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal4Sided:
                    DemolishGeneric(startPoint, entity, 4);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    DemolishMirroredGeneric(startPoint, entity, 2, buildOptionsStartRotation);
                    return;
                    case SymmetryType.Mirrored4Sided:
                    DemolishMirroredGeneric(startPoint, entity, 4, buildOptionsStartRotation);
                    return;
                }
                
                break;
                case SymmetryShape.Octagon:
                switch (symmetryType)
                {
                    case SymmetryType.Normal2Sided:
                    DemolishGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal4Sided:
                    DemolishGeneric(startPoint, entity, 4);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    DemolishMirroredGeneric(startPoint, entity, 2, buildOptionsStartRotation);
                    return;
                    case SymmetryType.Mirrored4Sided:
                    DemolishMirroredGeneric(startPoint, entity, 4, buildOptionsStartRotation);
                    return;
                }
                
                break;
            }
        }
        
        private void DemolishGeneric(Vector3 startingPoint, BaseNetworkable newBlock, int total)
        {
            var pos = newBlock.transform;
            var playerId = (newBlock as BaseEntity)?.OwnerID ?? 0;
            if (playerId == 0 || total < 1) return;
            var player = BasePlayer.Find(playerId.ToString());
            if (player == null) return;
            var newBuildBlock = newBlock.GetComponent<BuildingBlock>();
            if (newBuildBlock == null) return;
            
            for (var i = 1; i < total; i++)
            {
                var angles = 360 / total;
                _sphere = GameManager.server
                .CreateEntity("assets/prefabs/visualization/sphere.prefab", pos.position)
                .GetComponent<SphereEntity>();
                
                _sphere.currentRadius = .1f;
                _sphere.lerpRadius = .1f;
                _sphere.lerpSpeed = 1f;
                
                _sphere.transform.RotateAround(startingPoint, Vector3.down, angles * i);
                var entityToDemolish = Physics.OverlapSphere(_sphere.transform.position, 0.15f, _constructionMask)
                .Select(x => x.ToBaseEntity())
                .FirstOrDefault(x => x.ShortPrefabName == newBlock.ShortPrefabName);
                _sphere.Kill();
                if (entityToDemolish == null) return;
                var bb = entityToDemolish.GetComponent<BuildingBlock>();
                if (bb == null) return;
                if (bb.IsDestroyed)
                {
                    bb.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }
        
        private void DemolishMirroredGeneric(Vector3 startingPoint, BaseNetworkable newBlock, int sides,
        Quaternion startRotation)
        {
            var pos = newBlock.transform;
            var playerId = (newBlock as BaseEntity)?.OwnerID ?? 0;
            if (playerId == 0) return;
            var player = BasePlayer.Find(playerId.ToString());
            if (player == null) return;
            var newBuildBlock = newBlock.GetComponent<BuildingBlock>();
            if (newBuildBlock == null) return;
            
            var newPos = pos.position;
            var localOffset = Quaternion.Inverse(startRotation) * (newPos - startingPoint);
            
            localOffset = Math.Abs(localOffset.x) > Math.Abs(localOffset.z)
            ? localOffset.WithX(-localOffset.x)
            : localOffset.WithZ(-localOffset.z);
            
            var newPoint = startingPoint + startRotation * localOffset;
            var eulerAngles = pos.eulerAngles;
            var deltaY = startRotation.eulerAngles.y - eulerAngles.y;
            var newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
            
            _sphere = GameManager.server
            .CreateEntity("assets/prefabs/visualization/sphere.prefab", newPoint, newRot)
            .GetComponent<SphereEntity>();
            
            _sphere.currentRadius = .1f;
            _sphere.lerpRadius = .1f;
            _sphere.lerpSpeed = 1f;
            
            var entityToDemolish = Physics.OverlapSphere(_sphere.transform.position, 0.2f, _constructionMask)
            .Select(x => x.ToBaseEntity())
            .FirstOrDefault(x => x.ShortPrefabName == newBlock.ShortPrefabName);
            _sphere.Kill();
            if (entityToDemolish == null) return;
            var bb = entityToDemolish.GetComponent<BuildingBlock>();
            if (bb == null) return;
            if (bb.IsDestroyed)
            {
                bb.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            
            if (sides == 4)
            {
                localOffset = Quaternion.Inverse(startRotation) * (newPos - startingPoint);
                
                localOffset = Math.Abs(localOffset.x) < Math.Abs(localOffset.z)
                ? localOffset.WithX(-localOffset.x)
                : localOffset.WithZ(-localOffset.z);
                
                newPoint = startingPoint + startRotation * localOffset;
                eulerAngles = pos.eulerAngles;
                deltaY = startRotation.eulerAngles.y - eulerAngles.y;
                
                newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
                
                _sphere = GameManager.server
                .CreateEntity("assets/prefabs/visualization/sphere.prefab", newPoint, newRot)
                .GetComponent<SphereEntity>();
                
                _sphere.currentRadius = .1f;
                _sphere.lerpRadius = .1f;
                _sphere.lerpSpeed = 1f;
                
                entityToDemolish = Physics.OverlapSphere(_sphere.transform.position, 0.2f, _constructionMask)
                .Select(x => x.ToBaseEntity())
                .FirstOrDefault(x => x.ShortPrefabName == newBlock.ShortPrefabName);
                if (entityToDemolish == null) return;
                bb = entityToDemolish.GetComponent<BuildingBlock>();
                if (bb == null) return;
                if (bb.IsDestroyed)
                {
                    bb.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                
                localOffset = Quaternion.Inverse(startRotation) * (_sphere.transform.position - startingPoint);
                
                localOffset = Math.Abs(localOffset.x) > Math.Abs(localOffset.z)
                ? localOffset.WithX(-localOffset.x)
                : localOffset.WithZ(-localOffset.z);
                
                newPoint = startingPoint + startRotation * localOffset;
                eulerAngles = _sphere.transform.eulerAngles;
                deltaY = startRotation.eulerAngles.y - eulerAngles.y;
                
                newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
                _sphere.Kill();
                
                _sphere = GameManager.server
                .CreateEntity("assets/prefabs/visualization/sphere.prefab", newPoint, newRot)
                .GetComponent<SphereEntity>();
                
                _sphere.currentRadius = .1f;
                _sphere.lerpRadius = .1f;
                _sphere.lerpSpeed = 1f;
                
                entityToDemolish = Physics.OverlapSphere(_sphere.transform.position, 0.2f, _constructionMask)
                .Select(x => x.ToBaseEntity())
                .FirstOrDefault(x => x.ShortPrefabName == newBlock.ShortPrefabName);
                if (entityToDemolish == null) return;
                bb = entityToDemolish.GetComponent<BuildingBlock>();
                if (bb == null) return;
                if (bb.IsDestroyed)
                {
                    bb.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }
        
        private Vector3 FindTheCenter(BaseNetworkable entity, BasePlayer player)
        {
            if (!entity) return new Vector3();
            // Create a new vector to store the center position
            var centerPosition = Vector3.zero;
            if (entity.ShortPrefabName == "foundation")
            {
                centerPosition = entity.transform.position;
                player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, centerPosition, 0.2f);
                return centerPosition;
            }
            
            // Get the angle of the triangle's baseline relative to the x-axis
            var transform = entity.transform;
            var baselineAngle = transform.eulerAngles.y;
            
            // Set the position of the triangle's baseline
            var baselinePosition = transform.position;
            
            // Calculate the position of the center of the triangle
            centerPosition.x = baselinePosition.x + (0.8667854f * Mathf.Sin(baselineAngle * Mathf.Deg2Rad));
            centerPosition.z = baselinePosition.z + (0.8667854f * Mathf.Cos(baselineAngle * Mathf.Deg2Rad));
            centerPosition.y = baselinePosition.y;
            player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, centerPosition, 0.2f);
            return centerPosition;
        }
        
        
        private string GetButtonColor(bool isSelected)
        {
            return isSelected ? _config.SelectedButtonColor : _config.DefaultButtonColor;
        }
        
        private string GetSymmetryImage(PlayerBuildOptions buildOptions)
        {
            var name = buildOptions.SymmetryEnabled
            ? $"{buildOptions.SymmetryShape}_{buildOptions.SymmetryType}.png"
            : $"{buildOptions.SymmetryShape}.png";
            
            return _storedData.CommonImages.ContainsKey(name)
            ? _storedData.CommonImages[name]
            : _storedData.CommonImages["Square.png"];
        }
        
        private bool IsToCloseToRoad(BaseEntity baseEntity)
        {
            var heightMap = TerrainMeta.HeightMap;
            var topologyMap = TerrainMeta.TopologyMap;
            if (heightMap == null)
            {
                return false;
            }
            
            if (topologyMap == null)
            {
                return false;
            }
            
            var transform = baseEntity.transform;
            OBB obb = new OBB(transform.position, Vector3.one, transform.rotation, baseEntity.bounds);
            float num = Mathf.Abs(heightMap.GetHeight(obb.position) - obb.position.y);
            if (num > 9f)
            {
                return false;
            }
            
            float radius = Mathf.Lerp(3f, 0f, num / 9f);
            Vector3 position = obb.position;
            Vector3 point = obb.GetPoint(-1f, 0f, -1f);
            Vector3 point2 = obb.GetPoint(-1f, 0f, 1f);
            Vector3 point3 = obb.GetPoint(1f, 0f, -1f);
            Vector3 point4 = obb.GetPoint(1f, 0f, 1f);
            int topology = topologyMap.GetTopology(position, radius);
            int topology2 = topologyMap.GetTopology(point, radius);
            int topology3 = topologyMap.GetTopology(point2, radius);
            int topology4 = topologyMap.GetTopology(point3, radius);
            int topology5 = topologyMap.GetTopology(point4, radius);
            return ((topology | topology2 | topology3 | topology4 | topology5) & 526336) != 0;
        }
        
        private void MinimizeUi(BasePlayer player)
        {
            var buildOptions = GetBuildOptions(player.userID);
            buildOptions.UiMinimized = true;
            var container = new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = "Overlay",
                    Name = "SymmetryPanel",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UiAnchorMax,
                            AnchorMax = _config.UiAnchorMax,
                            OffsetMin = "-50 -50",
                            OffsetMax = "0 0"
                        },
                        new CuiImageComponent
                        {
                            Color = _config.MainBackgroundColor
                        }
                    }
                }
            };
            
            CreateImagePanel(ref container, "0 0", "1 1", "0 0", "0 0", 0.1f, GetSymmetryImage(buildOptions),
            "SymmetryPanel");
            CreateButton(ref container, "0 0", "1 1", "0 0", "0 0", 0.1f, ".2 .2 .2 .9", "1 1 1 1", "+", 12,
            "symmetry ui true", "SymmetryPanel");
            
            
            CuiHelper.DestroyUi(player, "SymmetryPanel");
            CuiHelper.AddUi(player, container);
        }
        
        private void ReplicateByType(SymmetryShape symmetryShape, SymmetryType symmetryType, Vector3 startPoint,
        BaseNetworkable entity, Quaternion buildOptionsStartRotation)
        {
            switch (symmetryShape)
            {
                case SymmetryShape.Triangle:
                ReplicateGeneric(startPoint, entity, 3);
                break;
                case SymmetryShape.Hexagon:
                switch (symmetryType)
                {
                    case SymmetryType.Normal6Sided:
                    ReplicateGeneric(startPoint, entity, 6);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    ReplicateGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal3Sided:
                    ReplicateGeneric(startPoint, entity, 3);
                    return;
                    default:
                    ReplicateGeneric(startPoint, entity, 2);
                    return;
                }
                case SymmetryShape.Square:
                case SymmetryShape.Rectangle:
                switch (symmetryType)
                {
                    case SymmetryType.Normal2Sided:
                    ReplicateGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal4Sided:
                    ReplicateGeneric(startPoint, entity, 4);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    ReplicateMirroredGeneric(startPoint, entity, 2, buildOptionsStartRotation);
                    return;
                    case SymmetryType.Mirrored4Sided:
                    ReplicateMirroredGeneric(startPoint, entity, 4, buildOptionsStartRotation);
                    return;
                }
                
                break;
                case SymmetryShape.Octagon:
                switch (symmetryType)
                {
                    case SymmetryType.Normal2Sided:
                    ReplicateGeneric(startPoint, entity, 2);
                    return;
                    case SymmetryType.Normal4Sided:
                    ReplicateGeneric(startPoint, entity, 4);
                    return;
                    case SymmetryType.Mirrored2Sided:
                    ReplicateMirroredGeneric(startPoint, entity, 2, buildOptionsStartRotation);
                    return;
                    case SymmetryType.Mirrored4Sided:
                    ReplicateMirroredGeneric(startPoint, entity, 4, buildOptionsStartRotation);
                    return;
                }
                
                break;
            }
        }
        
        private void ReplicateGeneric(Vector3 startingPoint, BaseNetworkable newBlock, int total)
        {
            var pos = newBlock.transform;
            var playerId = (newBlock as BaseEntity)?.OwnerID ?? 0;
            if (playerId == 0 || total < 1) return;
            var player = BasePlayer.Find(playerId.ToString());
            if (player == null) return;
            var newBuildBlock = newBlock.GetComponent<BuildingBlock>();
            if (newBuildBlock == null) return;
            
            for (var i = 1; i < total; i++)
            {
                var angles = 360 / total;
                var foundation = GameManager.server.CreateEntity(newBlock.PrefabName, pos.position, pos.rotation);
                if (foundation == null) continue;
                foundation.transform.RotateAround(startingPoint, Vector3.down, angles * i);
                
                var block = foundation as BuildingBlock;
                if (block!=null)
                {
                    block.blockDefinition = PrefabAttribute.server.Find<Construction>(foundation.prefabID);
                    block.SetGrade(newBuildBlock.grade);
                    block.OwnerID = playerId;
                }
                
                var baseCombatEntity = foundation as BaseCombatEntity;
                if (baseCombatEntity)
                {
                    var num2 = (block != null) ? block.currentGrade.maxHealth : baseCombatEntity.startHealth;
                    baseCombatEntity.ResetLifeStateOnSpawn = false;
                    baseCombatEntity.InitializeHealth(num2,num2);
                }
                
                if (!CanPlace(block, player) || block==null)
                {
                    if (block != null && !block.IsDestroyed)
                    {
                        block.Kill();
                    }
                    
                    continue;
                }
                
                block.Spawn();
                
                var decayEntity = foundation as DecayEntity;
                var nearbyBuildingBlock = GetNearbyBuildingBlock(decayEntity);
                if (nearbyBuildingBlock != null)
                {
                    if (nearbyBuildingBlock.buildingID == 0)
                    nearbyBuildingBlock.buildingID = BuildingManager.server.NewBuildingID();
                    decayEntity.AttachToBuilding(nearbyBuildingBlock.buildingID);
                }
                
                var stability = (StabilityEntity) block;
                stability.UpdateSurroundingEntities();
                
                block.SendNetworkUpdate();
            }
        }
        
        public BuildingBlock GetNearbyBuildingBlock(DecayEntity block)
        {
            var num1 = float.MaxValue;
            var nearbyBuildingBlock = (BuildingBlock)null;
            var position = block.PivotPoint();
            var list = Facepunch.Pool.GetList<BuildingBlock>();
            Vis.Entities(position, 3f, list, LayerMask.GetMask("Construction"));
            foreach (var buildingBlock in list)
            {
                if (buildingBlock.net.ID == block.net.ID) continue;
                var num2 = buildingBlock.SqrDistance(position);
                if (!buildingBlock.grounded)
                ++num2;
                if (!((double) num2 < (double) num1)) continue;
                num1 = num2;
                nearbyBuildingBlock = buildingBlock;
            }
            Facepunch.Pool.FreeList<BuildingBlock>(ref list);
            return nearbyBuildingBlock;
        }
        private void ReplicateMirroredGeneric(Vector3 startingPoint, BaseNetworkable newBlock, int sides,
        Quaternion startRotation)
        {
            var pos = newBlock.transform;
            var playerId = (newBlock as BaseEntity)?.OwnerID ?? 0;
            if (playerId == 0) return;
            var player = BasePlayer.Find(playerId.ToString());
            if (player == null) return;
            var newBuildBlock = newBlock.GetComponent<BuildingBlock>();
            if (newBuildBlock == null) return;
            
            var newPos = pos.position;
            var localOffset = Quaternion.Inverse(startRotation) * (newPos - startingPoint);
            
            localOffset = Math.Abs(localOffset.x) > Math.Abs(localOffset.z)
            ? localOffset.WithX(-localOffset.x)
            : localOffset.WithZ(-localOffset.z);
            
            var newPoint = startingPoint + startRotation * localOffset;
            var eulerAngles = pos.eulerAngles;
            var deltaY = startRotation.eulerAngles.y - eulerAngles.y;
            var newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
            
            var entity = GameManager.server.CreateEntity(newBlock.PrefabName, newPoint, newRot);
            if (entity == null) return;
            
            AttachToBuilding(entity, newBuildBlock, player);
            
            if (sides == 4)
            {
                localOffset = Quaternion.Inverse(startRotation) * (newPos - startingPoint);
                
                localOffset = Math.Abs(localOffset.x) < Math.Abs(localOffset.z)
                ? localOffset.WithX(-localOffset.x)
                : localOffset.WithZ(-localOffset.z);
                
                newPoint = startingPoint + startRotation * localOffset;
                eulerAngles = pos.eulerAngles;
                deltaY = startRotation.eulerAngles.y - eulerAngles.y;
                
                newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
                
                entity = GameManager.server.CreateEntity(newBlock.PrefabName, newPoint, newRot);
                if (entity == null) return;
                
                AttachToBuilding(entity, newBuildBlock, player);
                
                localOffset = Quaternion.Inverse(startRotation) * (entity.transform.position - startingPoint);
                
                localOffset = Math.Abs(localOffset.x) > Math.Abs(localOffset.z)
                ? localOffset.WithX(-localOffset.x)
                : localOffset.WithZ(-localOffset.z);
                
                newPoint = startingPoint + startRotation * localOffset;
                eulerAngles = entity.transform.eulerAngles;
                deltaY = startRotation.eulerAngles.y - eulerAngles.y;
                
                newRot = Quaternion.Euler(eulerAngles.WithY(startRotation.eulerAngles.y + deltaY + 180));
                
                entity = GameManager.server.CreateEntity(newBlock.PrefabName, newPoint, newRot);
                if (entity == null) return;
                
                AttachToBuilding(entity, newBuildBlock, player);
            }
        }
        
        private void SetupSymmetry(BasePlayer player)
        {
            var buildOptions = GetBuildOptions(player.userID);
            buildOptions.SymmetryEnabled = false;
            
            var pos = player.eyes.center;
            var allEntities = Physics.OverlapSphere(pos, 1.6f, _constructionMask)
            .Select(x => x.ToBaseEntity())
            .Where(x => x.ShortPrefabName.Contains("foundation")).ToArray();
            var color = Color.blue;
            
            if (!allEntities.Any())
            {
                player.ChatMessage(GetMsg(PluginMessages.NoProperFoundations, player.userID));
                return;
            }
            
            if (allEntities.Any(x => x.ShortPrefabName == "foundation.triangle"))
            {
                switch (allEntities.Length)
                {
                    case 6:
                    buildOptions.SymmetryShape = SymmetryShape.Hexagon;
                    var x1 = FindTheCenter(allEntities[0], player);
                    x1 += FindTheCenter(allEntities[1], player);
                    x1 += FindTheCenter(allEntities[2], player);
                    x1 += FindTheCenter(allEntities[3], player);
                    x1 += FindTheCenter(allEntities[4], player);
                    x1 += FindTheCenter(allEntities[5], player);
                    
                    x1 /= 6;
                    buildOptions.StartPoint = x1;
                    buildOptions.SymmetryType = SymmetryType.Normal6Sided;
                    break;
                    default:
                    var centerBlock = allEntities.First(x => x.ShortPrefabName == "foundation.triangle");
                    buildOptions.SymmetryShape = SymmetryShape.Triangle;
                    var initialPosition = centerBlock.transform.position;
                    
                    // Set the angle of the triangle's baseline relative to the x-axis
                    var baselineAngle = centerBlock.transform.eulerAngles.y;
                    
                    // Set the position of the triangle's baseline
                    var baselinePosition = initialPosition;
                    
                    // Create a new vector to store the center position
                    var centerPosition = Vector3.zero;
                    
                    // Calculate the position of the center of the triangle
                    centerPosition.x = baselinePosition.x +
                    (0.8667854f * Mathf.Sin(baselineAngle * Mathf.Deg2Rad));
                    centerPosition.z = baselinePosition.z +
                    (0.8667854f * Mathf.Cos(baselineAngle * Mathf.Deg2Rad));
                    centerPosition.y = baselinePosition.y;
                    
                    buildOptions.StartPoint = centerPosition;
                    buildOptions.SymmetryType = SymmetryType.Normal3Sided;
                    break;
                }
            }
            else
            {
                switch (allEntities.Length)
                {
                    case 1:
                    buildOptions.SymmetryShape = SymmetryShape.Square;
                    buildOptions.StartPoint = allEntities[0].transform.position;
                    break;
                    case 2:
                    buildOptions.SymmetryShape = SymmetryShape.Rectangle;
                    var x0 = FindTheCenter(allEntities[0], player);
                    x0 += FindTheCenter(allEntities[1], player);
                    x0 /= 2;
                    buildOptions.StartPoint = x0;
                    color = Color.magenta;
                    break;
                    case 4:
                    buildOptions.SymmetryShape = SymmetryShape.Octagon;
                    var x1 = FindTheCenter(allEntities[0], player);
                    x1 += FindTheCenter(allEntities[1], player);
                    x1 += FindTheCenter(allEntities[2], player);
                    x1 += FindTheCenter(allEntities[3], player);
                    x1 /= 4;
                    buildOptions.StartPoint = x1;
                    break;
                    default:
                    player.ChatMessage(GetMsg(PluginMessages.NoProperFoundations, player.userID));
                    return;
                }
                
                buildOptions.SymmetryEnabled = true;
                buildOptions.StartRotation = allEntities[0].transform.rotation;
                buildOptions.SymmetryType = SymmetryType.Normal2Sided;
                player.ChatMessage(GetMsg(PluginMessages.SymmetrySet, player.userID));
            }
            
            
            player.SendConsoleCommand("ddraw.sphere", 10f, color, buildOptions.StartPoint, 0.5f);
        }
        
        private void ShowCurrentSymmetryPoint(BasePlayer player)
        {
            var buildOptions = GetBuildOptions(player.userID);
            if (buildOptions.StartPoint == Vector3.zero)
            {
                player.ChatMessage(GetMsg(PluginMessages.SymmetryUnSet, player.userID));
                return;
            }
            
            var color = Color.blue;
            switch (buildOptions.SymmetryShape)
            {
                case SymmetryShape.Rectangle:
                {
                    color = Color.magenta;
                    if (buildOptions.SymmetryType == SymmetryType.Mirrored2Sided)
                    {
                        color = Color.red;
                    }
                    
                    break;
                }
                case SymmetryShape.Octagon:
                {
                    switch (buildOptions.SymmetryType)
                    {
                        case SymmetryType.Mirrored2Sided:
                        color = Color.red;
                        break;
                        case SymmetryType.Mirrored4Sided:
                        color = Color.yellow;
                        break;
                        case SymmetryType.Normal2Sided:
                        color = Color.cyan;
                        break;
                    }
                    
                    break;
                }
            }
            
            player.SendConsoleCommand("ddraw.sphere", 10f, color, buildOptions.StartPoint, 0.5f);
        }
        
        private void ShowUi(BasePlayer player, string buttonName = "")
        {
            var buildOptions = GetBuildOptions(player.userID);
            buildOptions.UiEnabled = true;
            buildOptions.UiMinimized = false;
            var container = new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = "Overlay",
                    Name = "SymmetryPanel",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UiAnchorMin,
                            AnchorMax = _config.UiAnchorMax,
                            OffsetMin = _config.OffsetMin,
                            OffsetMax = _config.OffsetMax
                        },
                        new CuiImageComponent
                        {
                            Color = _config.MainBackgroundColor
                        }
                    }
                }
            };
            
            CreateSymmetryPanel(ref container, "0 0", "1 1", "0 0", "0 0", "0 0 0 0", buildOptions, "SymmetryPanel",
            buttonName);
            CuiHelper.DestroyUi(player, "SymmetryPanel");
            CuiHelper.AddUi(player, container);
        }
        
        private void StopSymmetry(BasePlayer player)
        {
            var buildOptions = GetBuildOptions(player.userID);
            buildOptions.SymmetryEnabled = false;
        }
        
        private static string ToSentenceCase(string input)
        {
            return Regex.Replace(input, "((^[a-z]+)|([0-9]+)|([A-Z]{1}[a-z]+)|([A-Z]+(?=([A-Z][a-z])|($)|([0-9]))))", "$1 ");
        }
        
        #endregion
        
        internal class Anchors
        {
            public static string TopLeft = "0 1";
            public static string CenterLeft = "0 .5";
            public static string BottomLeft = "0 0";
            
            public static string TopCenter = ".5 1";
            public static string Center = ".5 .5";
            public static string BottomCenter = ".5 0";
            
            public static string TopRight = "1 1";
            public static string CenterRight = "1 .5";
            public static string BottomRight = "1 0";
        }
        #endregion

        #region 1.SimpleSymmetry.Config.cs
        private static Configuration _config;
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Max Radius For Building")]
            public double MaxRadiusForBuilding { get; set; }
            
            public bool ToggleUiWithPlanner { get; set; } = true;
            public string OffsetMax = "-5 -5";
            public string OffsetMin = "-245 -140";
            public string DefaultButtonColor = "0 0 0 .7";
            public string SelectedButtonColor = "0 .45 1 .7";
            public string MainBackgroundColor = "0 0 0 .8";
            public string UiAnchorMin = "1 1";
            public string UiAnchorMax = "1 1";
            public bool ShowUIByDefault = false;
            
            
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    MaxRadiusForBuilding = 40
                };
            }
        }
        
        
        #region BoilerPlate
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
                LoadData();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion
        #endregion

        #region 2.SimpleSymmetry.Localization.cs
        #region
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [PluginMessages.NoPermission] = "You do not have permission to do that",
                [PluginMessages.MustUse2X2] =
                "Your Symmetry point could not be set.\n You must start with a square or 2x2",
                [PluginMessages.SetSymmetry] = "Set\nSymmetry",
                [PluginMessages.ToggleSymmetry] = "Toggle\nSymmetry",
                [PluginMessages.DeleteSymmetry] = "Delete\nSymmetry",
                [$"{SymmetryType.Mirrored2Sided}"] = SymmetryType.Mirrored2Sided.ToString(),
                [$"{SymmetryType.Mirrored4Sided}"] = SymmetryType.Mirrored4Sided.ToString(),
                [$"{SymmetryType.Normal2Sided}"] = SymmetryType.Normal2Sided.ToString(),
                [$"{SymmetryType.Normal3Sided}"] = SymmetryType.Normal3Sided.ToString(),
                [$"{SymmetryType.Normal4Sided}"] = SymmetryType.Normal4Sided.ToString(),
                [$"{SymmetryType.Normal6Sided}"] = SymmetryType.Normal6Sided.ToString(),
                [$"SymmetryNotSet"] = "Symmetry Not Set",
                [PluginMessages.SymmetryChanged] = "Symmetry type changed to {0}",
                [PluginMessages.SymmetrySet] = "Your Symmetry point has been set.",
                [PluginMessages.SymmetryUnSet] = "Symmetry Point not set.",
                [PluginMessages.NoProperFoundations] =
                "Your Symmetry point could not be set. You must start with:\n1. A single triangle\n2. A single square\n3. Squares in a  2x2\n4. Triangles in a hexagon",
                [PluginMessages.CanNotAfford] = "Can not afford to fully upgrade.",
                [PluginMessages.DisablingSymmetry] = "Disabling Symmetry",
                [PluginMessages.SymmetryPointDeleted] = "Symmetry point deleted",
                [PluginMessages.SymmetryReEnabled] = "Symmetry Re-Enabled",
                [PluginMessages.HelpMenu] = "<color=#28B9DD>/sym ui</color> - Toggles UI on/off \n" +
                "<color=#28B9DD>/sym toggle</color> - Toggles Symmetry on/off \n" +
                "<color=#28B9DD>/sym show</color> - Show the current Symmetry Center\n" +
                "<color=#28B9DD>/sym set</color> - Sets the Symmetry Center\n" +
                "<color=#28B9DD>/sym delete</color> - Deletes the Symmetry Center\n" +
                "<color=#28B9DD>/sym {type}</color> - Sets the Symmetry Type (see below) \n" +
                "<color=#28B9DD>  N2S</color> (Normal 2 sided)\n" +
                "<color=#28B9DD>  N3S</color> (Normal 3 sided)\n" +
                "<color=#28B9DD>  N4S</color> (Normal 4 sided)\n" +
                "<color=#28B9DD>  N6S</color> (Normal 6 sided)\n" +
                "<color=#28B9DD>  M2S</color> (Mirrored 2 sided)\n" +
                "<color=#28B9DD>  M4S</color> (Mirrored 4 sided)\n"
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [PluginMessages.NoPermission] = "No tienes permiso para hacer eso",
                [PluginMessages.MustUse2X2] =
                "No se pudo establecer tu punto de simetría.\nDebes comenzar con un cuadrado o un 2x2",
                [PluginMessages.SymmetrySet] = "Se ha establecido tu punto de simetría.",
                [PluginMessages.SetSymmetry] = "Establecer\nsimetría",
                [PluginMessages.ToggleSymmetry] = "Alternar\nsimetría",
                [PluginMessages.DeleteSymmetry] = "Eliminar\nsimetría",
                [PluginMessages.SymmetryUnSet] = "Punto de simetría no establecido.",
                [$"{SymmetryType.Mirrored2Sided}"] = "Simetría de 2 lados",
                [$"{SymmetryType.Mirrored4Sided}"] = "Simetría de 4 lados",
                [$"{SymmetryType.Normal2Sided}"] = "Normal de 2 lados",
                [$"{SymmetryType.Normal3Sided}"] = "Normal de 3 lados",
                [$"{SymmetryType.Normal4Sided}"] = "Normal de 4 lados",
                [$"{SymmetryType.Normal6Sided}"] = "Normal de 6 lados",
                [$"SymmetryNotSet"] = "Simetría no establecida",
                [PluginMessages.SymmetryChanged] = "El tipo de simetría cambió a {0}",
                [PluginMessages.NoProperFoundations] =
                "No se pudo establecer tu punto de simetría. Debes comenzar con:\n1. Un triángulo único\n2. Un cuadrado único\n3. Cuadrados en un 2x2\n4. Triángulos en un hexágono",
                [PluginMessages.CanNotAfford] = "No puedes permitirte mejorar completamente.",
                [PluginMessages.DisablingSymmetry] = "Desactivando simetría",
                [PluginMessages.SymmetryPointDeleted] = "Punto de simetría eliminado",
                [PluginMessages.SymmetryReEnabled] = "Simetría reactivada",
                [PluginMessages.HelpMenu] =
                "<color=#28B9DD>/sym ui</color> - Alterna la interfaz de usuario (UI) activada/desactivada \n" +
                "<color=#28B9DD>/sym toggle</color> - Activa/desactiva la simetría \n" +
                "<color=#28B9DD>/sym show</color> - Muestra el punto de simetría actual\n" +
                "<color=#28B9DD>/sym set</color> - Establece el punto de simetría\n" +
                "<color=#28B9DD>/sym delete</color> - Elimina el punto de simetría\n" +
                "<color=#28B9DD>/sym {tipo}</color> - Establece el tipo de simetría (ver más abajo) \n" +
                "<color=#28B9DD>  N2S</color> (Normal de 2 lados)\n" +
                "<color=#28B9DD>  N3S</color> (Normal de 3 lados)\n" +
                "<color=#28B9DD>  N4S</color> (Normal de 4 lados)\n" +
                "<color=#28B9DD>  N6S</color> (Normal de 6 lados)\n" +
                "<color=#28B9DD>  M2S</color> (Simetría de 2 lados)\n" +
                "<color=#28B9DD>  M4S</color> (Simetría de 4 lados)\n"
            }, this, "es");
        }
        
        #endregion
        
        #region
        
        private string GetMsg(string key, object userId = null)
        {
            return lang.GetMessage(key, this, userId?.ToString());
        }
        
        #endregion
        
        private static class PluginMessages
        {
            public const string MustUse2X2 = "MustUse2X2";
            public const string NoPermission = "NoPermission";
            public const string SymmetrySet = "SymmetrySet";
            public const string SymmetryUnSet = "SymmetryUnset";
            public const string NoProperFoundations = "NoProperFoundations";
            public const string CanNotAfford = "CanNotAfford";
            public const string DisablingSymmetry = "DisablingSymmetry";
            public const string SymmetryReEnabled = "SymmetryReEnabled";
            public const string HelpMenu = "HelpMenu";
            public const string SymmetryPointDeleted = "SymmetryPointDeleted";
            public const string SetSymmetry = "SetSymmetry";
            public const string ToggleSymmetry = "ToggleSymmetry";
            public const string DeleteSymmetry = "DeleteSymmetry";
            public const string SymmetryChanged = "SymmetryChanged";
        }
        #endregion

        #region 3.SimpleSymmetry.Permissions.cs
        public class PluginPermissions
        {
            public const string SimpleSymmetryUse = "simplesymmetry.use";
        }
        
        private void LoadPermissions()
        {
            permission.RegisterPermission(PluginPermissions.SimpleSymmetryUse, this);
        }
        #endregion

        #region 4.SimpleSymmetry.Data.cs
        private readonly Dictionary<ulong, PlayerBuildOptions> _activeBuildOptions =
        new Dictionary<ulong, PlayerBuildOptions>();
        
        private readonly List<AssetInfo> _assets = new List<AssetInfo>
        {
            new AssetInfo {FileName = "Triangle.png"},
            new AssetInfo {FileName = "Triangle_Normal3Sided.png"},
            new AssetInfo {FileName = "Rectangle.png"},
            new AssetInfo {FileName = "Rectangle_Normal2Sided.png"},
            new AssetInfo {FileName = "Rectangle_Mirrored2Sided.png"},
            new AssetInfo {FileName = "Square.png"},
            new AssetInfo {FileName = "Square_Normal2Sided.png"},
            new AssetInfo {FileName = "Square_Normal4Sided.png"},
            new AssetInfo {FileName = "Square_Mirrored2Sided.png"},
            new AssetInfo {FileName = "Square_Mirrored4Sided.png"},
            new AssetInfo {FileName = "Octagon.png"},
            new AssetInfo {FileName = "Octagon_Normal2Sided.png"},
            new AssetInfo {FileName = "Octagon_Normal4Sided.png"},
            new AssetInfo {FileName = "Octagon_Mirrored2Sided.png"},
            new AssetInfo {FileName = "Octagon_Mirrored4Sided.png"},
            new AssetInfo {FileName = "Hexagon.png"},
            new AssetInfo {FileName = "Hexagon_Normal2Sided.png"},
            new AssetInfo {FileName = "Hexagon_Normal3Sided.png"},
            new AssetInfo {FileName = "Hexagon_Normal6Sided.png"}
        };
        
        private StoredData _storedData;
        
        #region
        
        public static string BaseFolder()
        {
            return $"SimpleSymmetry{Path.DirectorySeparatorChar}";
        }
        
        public static string BuildOptionsFolder()
        {
            return $"SimpleSymmetry{Path.DirectorySeparatorChar}BuildOptions{Path.DirectorySeparatorChar}";
        }
        
        #endregion
        
        #region
        
        private void DownloadAssetImages()
        {
            if (_storedData.CommonImages == null) _storedData.CommonImages = new Dictionary<string, string>();
            var assetInfo = _assets.FirstOrDefault(x => !_storedData.CommonImages.ContainsKey(x.FileName));
            if (assetInfo == null) return;
            ServerMgr.Instance.StartCoroutine(GetImageFromFilePath(assetInfo));
        }
        
        private static PlayerBuildOptions GetBuildOptions(ulong playerId)
        {
            if (!_instance._activeBuildOptions.ContainsKey(playerId))
            {
                _instance._activeBuildOptions.Add(playerId, _instance.LoadOrCreateBuildOptions(playerId));
            }
            
            return _instance._activeBuildOptions[playerId];
        }
        
        IEnumerator GetImageFromFilePath(AssetInfo assetInfo)
        {
            var url =
            $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}SimpleSymmetry{Path.DirectorySeparatorChar}assets{Path.DirectorySeparatorChar}{assetInfo.FileName}";
            
            using (var webRequest = UnityWebRequestTexture.GetTexture(url))
            {
                yield return webRequest.SendWebRequest();
                
                if (webRequest.isHttpError || webRequest.isNetworkError)
                {
                    Debug.LogError($"Image could not be loaded {url}: {webRequest.error}");
                }
                else
                {
                    var texture = DownloadHandlerTexture.GetContent(webRequest);
                    _storedData.CommonImages.Add(assetInfo.FileName,
                    FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png,
                    CommunityEntity.ServerInstance.net.ID).ToString());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            
            _assets.Remove(assetInfo);
            DownloadAssetImages();
        }
        
        private void LoadData()
        {
            try
            {
                _storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("SimpleSymmetry");
            }
            catch (Exception e)
            {
                Puts(e.Message);
                Puts(e.StackTrace);
                _storedData = new StoredData
                {
                    CommonImages = new Dictionary<string, string>()
                };
                SaveData();
            }
        }
        
        private PlayerBuildOptions LoadOrCreateBuildOptions(ulong playerId)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"{BuildOptionsFolder()}{playerId}")
            ? Interface.Oxide.DataFileSystem.GetFile($"{BuildOptionsFolder()}{playerId}")
            .ReadObject<PlayerBuildOptions>()
            : new PlayerBuildOptions();
        }
        
        void OnNewSave(string filename)
        {
            _storedData.CommonImages = new Dictionary<string, string>();
            SaveData();
        }
        
        private static void SaveAllBuildOptions()
        {
            foreach (var key in _instance._activeBuildOptions.Keys)
            {
                SavePlayersBuildOptions(key);
            }
        }
        
        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SimpleSymmetry", _storedData);
        }
        
        private static void SavePlayersBuildOptions(ulong playerId)
        {
            try
            {
                PlayerBuildOptions buildOptions;
                if (!_instance._activeBuildOptions.TryGetValue(playerId, out buildOptions)) return;
                
                Interface.Oxide.DataFileSystem.WriteObject($"{BuildOptionsFolder()}{playerId}", buildOptions);
            }
            catch
            {
                //Need to simplify the start rotation
            }
        }
        
        private static void UnloadPlayersBuildOptions(ulong playerId)
        {
            if (!_instance._activeBuildOptions.ContainsKey(playerId)) return;
            SavePlayersBuildOptions(playerId);
            _instance._activeBuildOptions.Remove(playerId);
        }
        
        #endregion
        
        public class StoredData
        {
            public Dictionary<string, string> CommonImages = new Dictionary<string, string>();
        }
        #endregion

        #region 5.SimpleSymmetry.Hooks.cs
        #region
        
        private void Init()
        {
            _instance = this;
            LoadPermissions();
        }
        
        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || player.IsNpc || !_config.ToggleUiWithPlanner ||
            !permission.UserHasPermission(player.UserIDString,"simplesymmetry.use")) return;
            
            var itemInHand = newItem?.GetHeldEntity()?.ShortPrefabName;
            if (itemInHand == "planner" || itemInHand == "hammer.entity" || itemInHand== "toolgun.entity")
            {
                var buildOptions = GetBuildOptions(player.userID);
                if (buildOptions.UiMinimized)
                {
                    MinimizeUi(player);
                }
                else
                {
                    ShowUi(player);
                }
                
            }
            else
            {
                CuiHelper.DestroyUi(player, "SymmetryPanel");
            }
            
        }
        
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go.ToBaseEntity();
            var player = plan.GetOwnerPlayer();
            
            if (entity == null || player == null) return;
            var buildOptions = GetBuildOptions(player.userID);
            
            if (buildOptions.SymmetryEnabled)
            {
                ReplicateByType(buildOptions.SymmetryShape, buildOptions.SymmetryType, buildOptions.StartPoint, entity,
                buildOptions.StartRotation);
            }
        }
        
        void OnPlayerConnected(BasePlayer player)
        {
            if (_config.ShowUIByDefault)
            {
                ShowUi(player);
            }
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            UnloadPlayersBuildOptions(player.userID);
        }
        
        void OnServerInitialized(bool initial)
        {
            PreLoadBuildOptions();
            DownloadAssetImages();
        }
        
        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer player, bool immediate)
        {
            if (entity == null || player == null) return;
            var buildOptions = GetBuildOptions(player.userID);
            
            if (buildOptions.SymmetryEnabled)
            {
                DemolishByType(buildOptions.SymmetryShape, buildOptions.SymmetryType, buildOptions.StartPoint, entity,
                buildOptions.StartRotation);
            }
        }
        
        private void PreLoadBuildOptions()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GetBuildOptions(player.userID);
            }
        }
        
        private void Unload()
        {
            SaveAllBuildOptions();
        }
        
        #endregion
        #endregion

        #region 6.SimpleSymmetry.Commands.cs
        #region
        
        [ConsoleCommand("symmetry")]
        void ConsoleCmdSymmetry(ConsoleSystem.Arg arg)
        {
            if(arg==null || !arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            
            ChatCmdSymmetry(player,"sym",arg.Args);
        }
        
        [ChatCommand("sym")]
        void ChatCmdSymmetry(BasePlayer player, string command, string[] args)
        {
            
            var action = "help";
            if (args != null && args.Length > 0)
            {
                action = args[0];
            }
            
            if (!permission.UserHasPermission(player.UserIDString, PluginPermissions.SimpleSymmetryUse))
            {
                player.ChatMessage(GetMsg(PluginMessages.NoPermission,player.userID));
                CuiHelper.DestroyUi(player, "SymmetryPanel");
                return;
            }
            
            var buildOptions = GetBuildOptions(player.userID);
            
            switch (action)
            {
                case "toggle":
                if (buildOptions.SymmetryEnabled)
                {
                    player.ChatMessage(GetMsg(PluginMessages.DisablingSymmetry,player.userID));
                    StopSymmetry(player);
                    break;
                }
                
                if (buildOptions.StartPoint != Vector3.zero)
                {
                    player.ChatMessage(GetMsg(PluginMessages.SymmetryReEnabled,player.userID));
                    buildOptions.SymmetryEnabled = true;
                }
                break;
                case "ui":
                if (args != null && args.Length > 1)
                {
                    buildOptions.UiEnabled = Convert.ToBoolean(args[1]);
                }
                else
                {
                    buildOptions.UiEnabled = !buildOptions.UiEnabled;
                }
                break;
                case "set":
                SetupSymmetry(player);
                break;
                case "show":
                ShowCurrentSymmetryPoint(player);
                break;
                case "minimize":
                MinimizeUi(player);
                return;
                case "M2S":
                AdjustSymmetry(buildOptions, player, SymmetryType.Mirrored2Sided);
                break;
                case "M4S":
                AdjustSymmetry(buildOptions, player, SymmetryType.Mirrored4Sided);
                break;
                case "N2S":
                AdjustSymmetry(buildOptions, player, SymmetryType.Normal2Sided);
                break;
                case "N3S":
                AdjustSymmetry(buildOptions,player,SymmetryType.Normal3Sided);
                break;
                case "N4S":
                AdjustSymmetry(buildOptions, player, SymmetryType.Normal4Sided);
                break;
                case "N6S":
                AdjustSymmetry(buildOptions, player, SymmetryType.Normal6Sided);
                break;
                case "delete":
                buildOptions.StartPoint = Vector3.zero;
                buildOptions.StartRotation = new Quaternion(0,0,0,0);
                buildOptions.SymmetryEnabled = false;
                player.ChatMessage(GetMsg(PluginMessages.SymmetryPointDeleted, player.userID));
                break;
                case "cycle":
                CycleSymmetryType(buildOptions);
                ShowCurrentSymmetryPoint(player);
                player.ChatMessage(string.Format(GetMsg(PluginMessages.SymmetryChanged,player.userID), ToSentenceCase(GetMsg(buildOptions.SymmetryType.ToString()))));
                break;
                default:
                player.ChatMessage(GetMsg(PluginMessages.HelpMenu,player.userID));
                return;
            }
            
            if (buildOptions.UiEnabled)
            {
                
                ShowUi(player, action);
                timer.Once(.3f, () => { ShowUi(player); });
            }
            else
            {
                CuiHelper.DestroyUi(player, "SymmetryPanel");
            }
        }
        private static void CycleSymmetryType(PlayerBuildOptions buildOptions)
        {
            switch (buildOptions.SymmetryShape)
            {
                case SymmetryShape.Triangle:
                buildOptions.SymmetryType = SymmetryType.Normal3Sided;
                return;
                case SymmetryShape.Hexagon:
                {
                    switch (buildOptions.SymmetryType)
                    {
                        case SymmetryType.Normal2Sided:
                        buildOptions.SymmetryType = SymmetryType.Normal3Sided;
                        return;
                        case SymmetryType.Normal3Sided:
                        buildOptions.SymmetryType = SymmetryType.Normal6Sided;
                        return;
                        default:
                        buildOptions.SymmetryType = SymmetryType.Normal2Sided;
                        return;
                    }
                }
                case SymmetryShape.Rectangle:
                {
                    if (buildOptions.SymmetryType == SymmetryType.Mirrored2Sided)
                    {
                        buildOptions.SymmetryType = SymmetryType.Normal2Sided;
                        return;
                    }
                    
                    buildOptions.SymmetryType = SymmetryType.Mirrored2Sided;
                    return;
                }
                case SymmetryShape.Square:
                case SymmetryShape.Octagon:
                {
                    switch (buildOptions.SymmetryType)
                    {
                        case SymmetryType.Mirrored2Sided:
                        buildOptions.SymmetryType = SymmetryType.Mirrored4Sided;
                        return;
                        case SymmetryType.Mirrored4Sided:
                        buildOptions.SymmetryType = SymmetryType.Normal2Sided;
                        return;
                        case SymmetryType.Normal2Sided:
                        buildOptions.SymmetryType = SymmetryType.Normal4Sided;
                        return;
                    }
                    
                    buildOptions.SymmetryType = SymmetryType.Mirrored2Sided;
                    break;
                }
            }
        }
        #endregion
        #endregion

        #region 7.SimpleSymmetry.Classes.cs
        #region SymmetryShape enum
        
        public enum SymmetryShape
        {
            Rectangle = 2,
            Triangle = 3,
            Square = 4,
            Hexagon = 6,
            Octagon = 8
        }
        
        #endregion
        
        #region SymmetryType enum
        
        public enum SymmetryType
        {
            Normal2Sided = 2,
            Normal3Sided = 3,
            Normal4Sided = 4,
            Normal6Sided = 6,
            Mirrored2Sided = 8,
            Mirrored4Sided = 16
        }
        
        #endregion
        
        public class PlayerBuildOptions
        {
            #region
            
            public Vector3 StartPoint { get; set; } = new Vector3();
            public Quaternion StartRotation { get; set; }
            public bool SymmetryEnabled { get; set; } = false;
            public SymmetryShape SymmetryShape { get; set; } = SymmetryShape.Square;
            public SymmetryType SymmetryType { get; set; } = SymmetryType.Mirrored4Sided;
            public bool UiEnabled { get; set; } = false;
            public bool UiMinimized { get; set; } = false;
            
            #endregion
        }
        
        public class AssetInfo
        {
            public string FileData { get; set; }
            public string FileName { get; set; }
        }
        #endregion

    }

}
