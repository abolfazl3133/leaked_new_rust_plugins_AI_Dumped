using System.Collections;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("LockOnRockets", "k1lly0u", "0.3.18")]
    class LockOnRockets : RustPlugin
    {
        #region Fields
        private static LockOnRockets Instance { get; set; }

        private const int TARGET_LAYERS = ~(1 << 3 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private const int LAUNCHER_ITEMID = 442886268;
        //private const int HOMING_ITEMID = -218009552;
        private const int SMOKE_ITEMID = -17123659;

        private const float CAST_RADIUS = 0.25f;
        private const float MAX_LOS_ANGLE = 5.5f;

        private const string EXPLOSION_PREFAB = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";
        private const string BEEP_PREFAB = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

        private const string UI_PANEL = "UI_LockOn";
        private const string UI_Image1 = "UI_LockOn_Image1";
        private const string UI_Image2 = "UI_LockOn_Image2";

        private static string UI_TARGET;
        private static string UI_INFO;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("lockonrockets.craft", this);
            lang.RegisterMessages(Messages, this);
            Instance = this;

            UI.UI_COLOR = UI.Color(configData.UIColor, 1f);
        }

        private void OnServerInitialized()
        {
            ServerMgr.Instance.StartCoroutine(DownloadImages());
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void Unload()
        {
            LockOnPlayer[] lockOnPlayers = UnityEngine.Object.FindObjectsOfType<LockOnPlayer>();
            for (int i = 0; i < lockOnPlayers?.Length; i++)            
                UnityEngine.Object.Destroy(lockOnPlayers[i]);

            HomingRocket[] homingRockets = UnityEngine.Object.FindObjectsOfType<HomingRocket>();
            for (int i = 0; i < homingRockets?.Length; i++)
                UnityEngine.Object.Destroy(homingRockets[i]);

            configData = null;
            Instance = null;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item?.info.itemid == -17123659 && container.playerOwner != null)
            {
                SendReply(container.playerOwner, msg("inventory", container.playerOwner.UserIDString));
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (GetPlayer(player) == null)
                player.gameObject.AddComponent<LockOnPlayer>();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            LockOnPlayer lockOnPlayer = GetPlayer(player);
            if (lockOnPlayer != null)
                UnityEngine.Object.Destroy(lockOnPlayer);
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null)
                return;

            if (newItem.info.itemid == LAUNCHER_ITEMID /*|| (configData.UseHomingLauncher && newItem.info.itemid == HOMING_ITEMID)*/)
            {
                BaseProjectile baseProjectile = newItem.GetHeldEntity() as BaseProjectile;
                if (baseProjectile == null)
                    return;

                OnWeaponReload(baseProjectile, player);
            }
        }

        private void OnWeaponReload(BaseProjectile baseProjectile, BasePlayer player)
        {
            if (!baseProjectile || baseProjectile.primaryMagazine.contents <= 0)
                return;

            int itemId = baseProjectile.GetItem().info.itemid;
            if (/*itemId == HOMING_ITEMID || */(itemId == LAUNCHER_ITEMID && baseProjectile.primaryMagazine.ammoType.itemid == SMOKE_ITEMID))
                SendReply(player, msg("loaded", player.UserIDString));
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            LockOnPlayer lockOnPlayer = GetPlayer(player);
            if (lockOnPlayer == null)
                return;

            if (lockOnPlayer.TargetLocked)
            {
                TimedExplosive timedExplosive = entity.GetComponent<TimedExplosive>();

                entity.GetComponent<ServerProjectile>().gravityModifier = 0f;

                timedExplosive.damageTypes.Clear();
                timedExplosive.SetFuse(configData.DetonationTime);
                timedExplosive.explosionEffect.guid = string.Empty;

                HomingRocket homingRocket = entity.gameObject.AddComponent<HomingRocket>();
                homingRocket.Initialize(lockOnPlayer.Target, player, lockOnPlayer.LockType);
                lockOnPlayer.Reset(false);
            }
        }
        #endregion

        #region Functions
        private LockOnPlayer GetPlayer(BasePlayer player) => player?.GetComponent<LockOnPlayer>();

        private static void RadiusDamage(BasePlayer attackingPlayer, BaseEntity weaponPrefab, Vector3 pos, ConfigData.LockType lockType)
        {
            List<HitInfo> hitInfo = Facepunch.Pool.GetList<HitInfo>();
            List<BaseEntity> foundEntities = Facepunch.Pool.GetList<BaseEntity>();
            List<BaseEntity> hitEntities = Facepunch.Pool.GetList<BaseEntity>();

            Vis.Entities(pos, 3.8f, foundEntities);
            for (int i = 0; i < foundEntities.Count; i++)
            {
                BaseEntity item = foundEntities[i];
                if (item != null && !item.IsDestroyed && !hitEntities.Contains(item))
                {
                    Vector3 closestPoint = item.ClosestPoint(pos);
                    float distance = Vector3.Distance(closestPoint, pos);
                    float damageScale = Mathf.Clamp01((distance - 1.5f) / (3.8f - 1.5f));
                    if (damageScale <= 1f)
                    {
                        float scale = 1f - damageScale;
                        if (item.IsVisible(pos, float.PositiveInfinity))
                        {
                            HitInfo info = new HitInfo()
                            {
                                Initiator = attackingPlayer,
                                WeaponPrefab = weaponPrefab,
                                HitPositionWorld = closestPoint,
                                HitNormalWorld = (pos - closestPoint).normalized,
                                PointStart = pos,
                                PointEnd = closestPoint
                            };

                            info.damageTypes.Add(new List<Rust.DamageTypeEntry>
                            {
                                new Rust.DamageTypeEntry
                                {
                                    amount = configData.RocketDamage * lockType.DamageModifier,
                                    type = Rust.DamageType.Explosion
                                },
                                new Rust.DamageTypeEntry
                                {
                                    amount = 75,
                                    type = Rust.DamageType.Blunt
                                }
                            });

                            info.damageTypes.ScaleAll(scale);
                            
                            hitInfo.Add(info);
                            hitEntities.Add(item);
                        }
                    }
                }
            }
            for (int j = 0; j < hitEntities.Count; j++)
            {
                BaseEntity baseEntity = hitEntities[j];
                HitInfo info = hitInfo[j];

                PatrolHelicopter baseHelicopter = baseEntity as PatrolHelicopter;

                if (baseHelicopter != null && baseHelicopter.enabled && baseHelicopter.health - info.damageTypes.Total() <= 0)
                    baseHelicopter.Die(info);
                else
                {
                    baseEntity.OnAttacked(info);

                    if (baseHelicopter != null && attackingPlayer != null)
                    {
                        baseHelicopter.myAI._targetList.Add(new PatrolHelicopterAI.targetinfo(attackingPlayer, attackingPlayer));

                        if (!baseHelicopter.myAI.IsTargeting())                        
                            baseHelicopter.myAI.SetTargetDestination(attackingPlayer.transform.position, 50);                        
                    }
                }
            }

            Facepunch.Pool.FreeList(ref hitEntities);
            Facepunch.Pool.FreeList(ref foundEntities);
            Facepunch.Pool.FreeList(ref hitInfo);
        }
        #endregion
        
        #region Image Storage
        private const string UI_TARGET_URL = "https://chaoscode.io/oxide/Images/lockon-target.png";
        private const string UI_INFO_URL = "https://chaoscode.io/oxide/Images/lockon-info.png";
        private IEnumerator DownloadImages()
        {
            UnityWebRequest www = UnityWebRequest.Get(UI_TARGET_URL);

            yield return www.SendWebRequest();
            
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(string.Format("[LockOnRockets] Image failed to download! Error: {0} - URL: {1}", www.error, UI_TARGET_URL));
                www.Dispose();
                yield break;
            }

            if (www?.downloadHandler?.data != null)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();

                    UnityEngine.Object.DestroyImmediate(texture);

                    UI_TARGET = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                }
            }
            www.Dispose();
            
            www = UnityWebRequest.Get(UI_INFO_URL);

            yield return www.SendWebRequest();
            
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(string.Format("[LockOnRockets] Image failed to download! Error: {0} - URL: {1}", www.error, UI_INFO_URL));
                www.Dispose();
                yield break;
            }

            if (www?.downloadHandler?.data != null)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();

                    UnityEngine.Object.DestroyImmediate(texture);

                    UI_INFO = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                }
            }
        }
        #endregion

        #region UI        
        public static class UI
        {
            public static string UI_COLOR;
            
            public static CuiElementContainer Container(string panel, Anchor anchor, Offset offset, string parent = "Hud.Menu")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    new CuiElement
                    {
                        Name = panel,
                        Parent = parent,
                        Components =
                        {
                            new CuiRectTransformComponent { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() }
                        }
                    }
                };
                return container;
            }

            public static void Label(CuiElementContainer container, string panel, string color, string text, int size, Anchor anchor, Offset offset, TextAnchor align = TextAnchor.MiddleLeft)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent { Color = color, FontSize = size, Align = align, Text = text },
                        new CuiRectTransformComponent {AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString()},
                    }
                });
            }
            
            public static CuiElement Image(CuiElementContainer container, string panel, string name, string color, string png, Anchor anchor, Offset offset)
            {
                CuiElement cuiElement = new CuiElement
                {
                    Name = name,
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent { Png = png, Color = color },
                        new CuiRectTransformComponent { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() }
                    }
                };

                container.Add(cuiElement);
                return cuiElement;
            }
            
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
            
            public struct Anchor
            {
                public Bounds Min;
                public Bounds Max;

                public Anchor(float xMin, float yMin, float xMax, float yMax)
                {
                    this.Min = new Bounds(xMin, yMin);
                    this.Max = new Bounds(xMax, yMax);
                }
                
                public static Anchor TopLeft = new Anchor(0f, 1f, 0f, 1f);
                public static Anchor TopCenter = new Anchor(0.5f, 1f, 0.5f, 1f);
                public static Anchor TopRight = new Anchor(1f, 1f, 1f, 1f);
                public static Anchor CenterLeft = new Anchor(0f, 0.5f, 0f, 0.5f);
                public static Anchor Center = new Anchor(0.5f, 0.5f, 0.5f, 0.5f);
                public static Anchor CenterRight = new Anchor(1f, 0.5f, 1f, 0.5f);
                public static Anchor BottomLeft = new Anchor(0f, 0f, 0f, 0f);
                public static Anchor BottomCenter = new Anchor(0.5f, 0f, 0.5f, 0f);
                public static Anchor BottomRight = new Anchor(1f, 0f, 1f, 0f);

                public static Anchor FullStretch = new Anchor(0f, 0f, 1f, 1f);
                public static Anchor TopStretch = new Anchor(0f, 1f, 1f, 1f);
                public static Anchor HoriztonalCenterStretch = new Anchor(0f, 0.5f, 1f, 0.5f);
                public static Anchor BottomStretch = new Anchor(0f, 0f, 1f, 0f);
                public static Anchor LeftStretch = new Anchor(0f, 0f, 0f, 1f);
                public static Anchor VerticalCenterStretch = new Anchor(0.5f, 0f, 0.5f, 1f);
                public static Anchor RightStretch = new Anchor(1f, 0f, 1f, 1f);

                public override string ToString() => $"{Min.ToString()} {Max.ToString()}";
            }

            public struct Offset
            {
                public Bounds Min;
                public Bounds Max;

                public static Offset zero = new Offset(0, 0, 0, 0);

                public Offset(float xMin, float yMin, float xMax, float yMax)
                {
                    this.Min = new Bounds(xMin, yMin);
                    this.Max = new Bounds(xMax, yMax);
                }
                
                public override string ToString() => $"{Min.ToString()} {Max.ToString()}";
            }

            public struct Bounds
            {
                public readonly float X;
                public readonly float Y;

                public static readonly Bounds zero = new Bounds(0, 0);

                public static readonly Bounds one = new Bounds(1, 1);

                public Bounds(float x, float y)
                {
                    this.X = x;
                    this.Y = y;
                }

                public override string ToString() => $"{X} {Y}";
            }
        }

       private static void CreateUIOverlay(BasePlayer player, string targetShortname, string distance, bool isLocked)
        {
            CuiElementContainer root = UI.Container(UI_PANEL, UI.Anchor.FullStretch, UI.Offset.zero);
            UI.Image(root, UI_PANEL, UI_Image1, UI.UI_COLOR, UI_TARGET, UI.Anchor.Center, new UI.Offset(-247, -150, 247, 150));

            CuiElement info = UI.Image(root, UI_PANEL, UI_Image2, UI.UI_COLOR, UI_INFO, UI.Anchor.Center, new UI.Offset(252.5f, -50, 602.5f, 50));
            UI.Label(root, info.Name, UI.UI_COLOR, $"Target : {targetShortname}", 18, UI.Anchor.TopStretch, new UI.Offset(20, -50, -20, 0));
            UI.Label(root, info.Name, UI.UI_COLOR, $"Distance : {distance}", 18, UI.Anchor.HoriztonalCenterStretch, new UI.Offset(20, -25, -20, 25));
            UI.Label(root, info.Name, UI.UI_COLOR, $"Lock Status : {(isLocked ? "<color=#75FF00>LOCKED</color>" : "<color=#FF420B>PENDING</color>")}", 18, UI.Anchor.BottomStretch, new UI.Offset(20, 0, -20, 50));

            CuiHelper.DestroyUi(player, UI_Image1);
            CuiHelper.DestroyUi(player, UI_Image2);
            CuiHelper.DestroyUi(player, UI_PANEL);
            CuiHelper.AddUi(player, root);
        }
        #endregion

        #region Components
        private class LockOnPlayer : MonoBehaviour
        {
            private BasePlayer player;

            private RaycastHit raycastHit;

            private BaseEntity lastTarget;

            private Vector3 targetOffset;

            private float timeLocked;

            private bool isBeeping;

            private bool isLocked;

           // private bool activeDDraw;

           public bool TargetLocked => isLocked;

           public BaseEntity Target => lastTarget;

            public ConfigData.LockType LockType { get; private set; }

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            private void Update()
            {
                if (!player || player.IsDead())
                    return;

                if (player.serverInput.IsDown(BUTTON.FIRE_SECONDARY) && HasValidWeapon())
                {
                    TryLockTarget();
                    //DDrawOverlay();
                }
                else Reset(false);
            }

            private bool HasValidWeapon()
            {                
                Item item = player.GetActiveItem();
                if (item == null)
                    return false;

                if (item.info.itemid == LAUNCHER_ITEMID/* || (configData.UseHomingLauncher && item.info.itemid == HOMING_ITEMID)*/)
                {
                    BaseProjectile baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (!baseProjectile)
                        return false;

                    if (baseProjectile.primaryMagazine.contents > 0)
                    {
                        if (item.info.itemid == LAUNCHER_ITEMID)
                            return baseProjectile.primaryMagazine.ammoType.itemid == SMOKE_ITEMID;

                        return true;
                    }
                }

                return false;
            }

            private void TryLockTarget()
            {
                if (lastTarget && !lastTarget.IsDestroyed)
                {
                    float angle = Vector3.Angle(player.eyes.HeadForward(), lastTarget.transform.TransformPoint(targetOffset) - player.eyes.position);
                    if (angle <= MAX_LOS_ANGLE)
                    {
                        if (GamePhysics.LineOfSight(player.eyes.position + player.eyes.HeadForward(), lastTarget.transform.TransformPoint(targetOffset), TARGET_LAYERS, CAST_RADIUS))
                        {
                            OnTargetTick();
                            return;
                        }
                    }
                }

                if (Physics.SphereCast(player.eyes.HeadRay(), CAST_RADIUS, out raycastHit, 2000, TARGET_LAYERS, QueryTriggerInteraction.Collide))
                {
                    BaseEntity hitEntity = raycastHit.GetEntity();
                    if (hitEntity)
                    {
                        if (hitEntity != lastTarget)
                        {
                            ConfigData.LockType lockType;
                            if (CanTargetEntity(hitEntity, Vector3.Distance(player.transform.position, hitEntity.transform.position), out lockType))
                            {
                                lastTarget = hitEntity;
                                targetOffset = lastTarget.transform.InverseTransformPoint(raycastHit.point);
                                LockType = lockType;
                            }
                            Reset(true);
                            return;
                        }

                        OnTargetTick();
                        return;
                    }
                }

                Reset(false);
            }

            private float m_nextUIUpdate;
            
            private void OnTargetTick()
            {
                if (!isBeeping)
                    Beep();

                timeLocked += Time.deltaTime;

                isLocked = timeLocked >= configData.TimeToLockOn;
                
                if (Time.time > m_nextUIUpdate)
                {
                    m_nextUIUpdate = Time.time + 0.25f;

                    string shortname = Target ? (Target is BasePlayer ? (Target as BasePlayer).displayName : Target.ShortPrefabName) : "Nothing";
                    string distance = Target ? $"{Vector3.Distance(Target.transform.position, player.transform.position).ToString("F2")}m" : "~";

                    CreateUIOverlay(player, shortname, distance, isLocked);
                }
            }

            private bool CanTargetEntity(BaseEntity entity, float distance, out ConfigData.LockType lockType)
            {
                if (entity is BasePlayer)
                {
                    if ((entity as BasePlayer).IsNpc)
                    {
                        lockType = configData.LockOnTypes[LockTypes.NPC];
                        return lockType.Enabled && distance <= lockType.Distance;
                    }

                    lockType = configData.LockOnTypes[LockTypes.Player];
                    return lockType.Enabled && distance <= lockType.Distance;
                }

                if (entity is BaseNpc || entity is BaseRidableAnimal)
                {
                    lockType = configData.LockOnTypes[LockTypes.Animal];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is AutoTurret || entity is FlameTurret || entity is GunTrap)
                {
                    lockType = configData.LockOnTypes[LockTypes.GunTrap];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is BradleyAPC)
                {
                    lockType = configData.LockOnTypes[LockTypes.APC];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is ScrapTransportHelicopter)
                {
                    lockType = configData.LockOnTypes[LockTypes.TransportHelicopter];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is Minicopter)
                {
                    lockType = configData.LockOnTypes[LockTypes.Minicopter];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is AttackHelicopter)
                {
                    lockType = configData.LockOnTypes[LockTypes.AttackHelicopter];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is CH47Helicopter)
                {
                    lockType = configData.LockOnTypes[LockTypes.CH47];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is PatrolHelicopter)
                {
                    lockType = configData.LockOnTypes[LockTypes.PatrolHelicopter];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is HotAirBalloon)
                {
                    lockType = configData.LockOnTypes[LockTypes.HotAirBalloon];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is Drone && (entity as Drone).IsBeingControlled)
                {
                    lockType = configData.LockOnTypes[LockTypes.Drone];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is BasicCar)
                {
                    lockType = configData.LockOnTypes[LockTypes.Car];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is ModularCar || entity is BaseVehicleModule)
                {
                    lockType = configData.LockOnTypes[LockTypes.ModularCar];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is RHIB)
                {
                    lockType = configData.LockOnTypes[LockTypes.RHIB];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is BaseBoat)
                {
                    lockType = configData.LockOnTypes[LockTypes.Boat];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is TrainEngine)
                {
                    lockType = configData.LockOnTypes[LockTypes.TrainEngine];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is TrainCar)
                {
                    lockType = configData.LockOnTypes[LockTypes.TrainCar];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is BuildingPrivlidge)
                {
                    lockType = null;
                    return false;
                }
                if ((entity is LootContainer || entity is StorageContainer) && !(entity is StashContainer))
                {
                    lockType = configData.LockOnTypes[LockTypes.Loot];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is ResourceEntity)
                {
                    lockType = configData.LockOnTypes[LockTypes.Resource];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity is BuildingBlock || entity is SimpleBuildingBlock || entity is Door)
                {
                    lockType = configData.LockOnTypes[LockTypes.Structure];
                    return lockType.Enabled && distance <= lockType.Distance;
                }
                if (entity.ShortPrefabName.Equals("cargo_plane", System.StringComparison.OrdinalIgnoreCase))
                {
                    lockType = configData.LockOnTypes[LockTypes.Plane];
                    return lockType.Enabled && distance <= lockType.Distance;
                }

                lockType = null;
                return false;
            }

            private void Beep()
            {
                isBeeping = true;
                Effect.server.Run(BEEP_PREFAB, player.transform.position + player.transform.forward);
                InvokeHandler.Invoke(this, Beep, TargetLocked ? 0.25f : 1f);
            }

            /*private void DDrawOverlay()
            {
                if (activeDDraw || lastTarget == null)
                    return;

                bool tempAdmin = false;
                if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                {
                    tempAdmin = true;
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                }

                player.SendConsoleCommand("ddraw.box", 0.2f, (TargetLocked ? Color.green : Color.red), lastTarget.transform.TransformPoint(targetOffset), 0.5f);

                if (tempAdmin)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);

                activeDDraw = true;

                InvokeHandler.Invoke(this, () => activeDDraw = false, 0.2f);
            }*/

            internal void Reset(bool keepTarget)
            {
                if (!keepTarget)
                {
                    lastTarget = null;
                    LockType = null;
                }

                if (timeLocked > 0)
                {
                    timeLocked = 0;
                    isLocked = false;

                    if (isBeeping)
                    {
                        isBeeping = false;
                        InvokeHandler.CancelInvoke(this, Beep);
                    }

                    CuiHelper.DestroyUi(player, UI_Image1);
                    CuiHelper.DestroyUi(player, UI_Image2);
                    CuiHelper.DestroyUi(player, UI_PANEL);

                    //activeDDraw = false;
                }
            }
        }

        private class HomingRocket : MonoBehaviour
        {
            private ServerProjectile projectile;

            private BaseEntity target;

            private BasePlayer owner;

            private ConfigData.LockType lockType;

            private Vector3 targetPosition;

            private float totalDistance;

            private float fraction;

            private void Awake()
            {
                projectile = GetComponent<ServerProjectile>();
                enabled = false;
            }

            public void Initialize(BaseEntity target, BasePlayer owner, ConfigData.LockType lockType)
            {
                this.target = target;
                this.owner = owner;
                this.lockType = lockType;

                projectile.speed = configData.RocketSpeed * lockType.SpeedModifier;
                totalDistance = Vector3.Distance(projectile.transform.position, target.transform.position);
                fraction = 0;

                if (configData.RocketBeep)
                    Beep();

                InvokeHandler.Invoke(this, () => enabled = true, 0.5f);
            }

            private void Update()
            {
                if (target == null || target.IsDestroyed)
                    return;

                Vector3 position = target.transform.position + new Vector3(0, target.bounds.center.y / 2, 0);

                if (position != targetPosition)
                {
                    targetPosition = position;

                    float distance = Vector3.Distance(projectile.transform.position, targetPosition);

                    if (distance <= 0.25f)
                    {
                        Destroy(this);
                        return;
                    }

                    Vector3 direction = (targetPosition - projectile.transform.position).normalized;
                    projectile.InitializeVelocity(direction * projectile.speed);

                    float remaining = totalDistance - distance;
                    if (remaining > 0 && totalDistance > 0)
                        fraction = remaining / totalDistance;
                }
            }

            private void OnDestroy()
            {
                RadiusDamage(owner, projectile.baseEntity, projectile.transform.position, lockType);
                Effect.server.Run(EXPLOSION_PREFAB, projectile.transform.position);
            }

            private void Beep()
            {
                Effect.server.Run(BEEP_PREFAB, projectile.transform.position + Vector3.up);
                InvokeHandler.Invoke(this, Beep, 1f - fraction);
            }
        }
        #endregion

        #region Crafting
        private void ValidateCraftingConfig()
        {
            foreach (ConfigData.CraftingItems craftingItem in configData.CraftCost)
            {
                ItemDefinition itemDefinition;
                if (!ItemManager.itemDictionaryByName.TryGetValue(craftingItem.Shortname, out itemDefinition))
                {
                    PrintError($"An invalid item shortname has been set in the crafting section of the config, crafting has been disabled: {craftingItem.Shortname}");
                    configData.EnableCrafting = false;
                }
            }
        }

        [ChatCommand("craft.lockon")]
        private void cmdCraftLockon(BasePlayer player, string command, string[] args)
        {
            if (!configData.EnableCrafting)
                return;

            if (!permission.UserHasPermission(player.UserIDString, "lockonrockets.craft"))
            {
                SendReply(player, msg("noPerms", player.UserIDString));
                return;
            }

            int amount = 1;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out amount))
                    amount = 1;
            }

            if (!HasResourcesForCrafting(player, amount))
            {
                SendReply(player, string.Format(msg("notEnoughResources", player.UserIDString), GetRequiredResources()));
                return;
            }

            TakeAllResources(player, amount);

            player.GiveItem(ItemManager.CreateByItemID(-17123659, amount, 0), BaseEntity.GiveItemReason.PickedUp);

        }

        private bool HasResourcesForCrafting(BasePlayer player, int amount)
        {
            foreach (ConfigData.CraftingItems craftingItem in configData.CraftCost)
            {
                ItemDefinition itemDefinition = ItemManager.itemDictionaryByName[craftingItem.Shortname];

                if (!HasEnoughResources(player, itemDefinition.itemid, craftingItem.Amount * amount))
                    return false;
            }
            return true;
        }

        private bool HasEnoughResources(BasePlayer player, int itemid, int amount) => player.inventory.GetAmount(itemid) >= amount;

        private string GetRequiredResources()
        {
            string message = string.Empty;

            for (int i = 0; i < configData.CraftCost.Count; i++)
            {
                ItemDefinition itemDefinition = ItemManager.itemDictionaryByName[configData.CraftCost[i].Shortname];
                message += $"{configData.CraftCost[i].Amount} x {itemDefinition.displayName.english}";
                if (i < configData.CraftCost.Count - 1)
                    message += ", ";
            }
            return message;
        }

        private void TakeAllResources(BasePlayer player, int amount)
        {
            foreach (ConfigData.CraftingItems craftingItem in configData.CraftCost)
            {
                ItemDefinition itemDefinition = ItemManager.itemDictionaryByName[craftingItem.Shortname];

                TakeResources(player, itemDefinition.itemid, craftingItem.Amount * amount);
            }
        }

        private void TakeResources(BasePlayer player, int itemid, int amount) => player.inventory.Take(null, itemid, amount);
        #endregion

        #region Config        
        internal enum LockTypes { AttackHelicopter, CH47, Minicopter, PatrolHelicopter, TransportHelicopter, RHIB, Boat, Car, APC, Plane, NPC, Player, Animal, Structure, Resource, Loot, GunTrap, ModularCar, HotAirBalloon, Drone, TrainEngine, TrainCar }

        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Amount of time to acquire target lock")]
            public float TimeToLockOn { get; set; }

            [JsonProperty(PropertyName = "Amount of time before the rocket self detonates")]
            public float DetonationTime { get; set; }

            [JsonProperty(PropertyName = "Enable beeping sfx on the rocket as it approaches the target")]
            public bool RocketBeep { get; set; }

            [JsonProperty(PropertyName = "Base speed of the rocket")]
            public float RocketSpeed { get; set; }

            [JsonProperty(PropertyName = "Base damage of the rocket")]
            public float RocketDamage { get; set; }

            [JsonProperty(PropertyName = "Allow smoke rocket crafting")]
            public bool EnableCrafting { get; set; }
            
            [JsonProperty(PropertyName = "Allow homing missle launchers to act as a lock on rocket launcher")]
            public bool UseHomingLauncher { get; set; }

            [JsonProperty(PropertyName = "Crafting Costs")]
            public List<CraftingItems> CraftCost { get; set; }

            [JsonProperty(PropertyName = "Targeting Types")]
            public Dictionary<LockTypes, LockType> LockOnTypes { get; set; }
            
            [JsonProperty(PropertyName = "UI Overlay Color (hex)")]
            public string UIColor { get; set; }

            public class LockType
            {
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Maximum distance")]
                public float Distance { get; set; }

                [JsonProperty(PropertyName = "Rocket damage modifier")]
                public float DamageModifier { get; set; } = 1f;

                [JsonProperty(PropertyName = "Rocket speed modifier")]
                public float SpeedModifier { get; set; } = 1f;
            }
                        
            public class CraftingItems
            {
                public string Shortname { get; set; }

                public int Amount { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                TimeToLockOn = 3f,
                DetonationTime = 30f,
                RocketBeep = true,
                RocketDamage = 300,
                RocketSpeed = 40,               
                EnableCrafting = true,
                UIColor = "76FF00",
                CraftCost = new List<ConfigData.CraftingItems>
                {
                    new ConfigData.CraftingItems
                    {
                        Shortname = "ammo.rocket.basic",
                        Amount = 1
                    },
                    new ConfigData.CraftingItems
                    {
                        Shortname = "techparts",
                        Amount = 3
                    }
                },
                LockOnTypes = new Dictionary<LockTypes, ConfigData.LockType>
                {
                    [LockTypes.PatrolHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.AttackHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.CH47] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.Minicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.TransportHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.HotAirBalloon] = new ConfigData.LockType() { Enabled = true, Distance = 350 },
                    [LockTypes.RHIB] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Boat] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Car] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.ModularCar] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.APC] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Plane] = new ConfigData.LockType() { Enabled = true, Distance = 1000 },
                    [LockTypes.NPC] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Player] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Animal] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Structure] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Resource] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Loot] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.GunTrap] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.Drone] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.TrainEngine] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                    [LockTypes.TrainCar] = new ConfigData.LockType() { Enabled = true, Distance = 250 },
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(0, 3, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(0, 3, 4))
                configData.LockOnTypes = baseConfig.LockOnTypes;

            if (configData.Version < new Core.VersionNumber(0, 3, 8))
            {
                configData.LockOnTypes.Add(LockTypes.ModularCar, new ConfigData.LockType() { Enabled = true, Distance = 250 });
                configData.LockOnTypes.Add(LockTypes.HotAirBalloon, new ConfigData.LockType() { Enabled = true, Distance = 350 });
            }

            if (configData.Version < new Core.VersionNumber(0, 3, 11))
            {
                configData.LockOnTypes.Add(LockTypes.Drone, new ConfigData.LockType() { Enabled = true, Distance = 250 });
                configData.LockOnTypes[LockTypes.PatrolHelicopter].DamageModifier = 5f;
                configData.LockOnTypes[LockTypes.PatrolHelicopter].SpeedModifier = 2.5f;
            }

            if (configData.Version < new Core.VersionNumber(0, 3, 14))
            {
                configData.LockOnTypes.Add(LockTypes.TrainEngine, new ConfigData.LockType() { Enabled = true, Distance = 250 });
                configData.LockOnTypes.Add(LockTypes.TrainCar, new ConfigData.LockType() { Enabled = true, Distance = 250 });
            }

            if (configData.Version < new VersionNumber(0, 3, 15))
                configData.UIColor = "76FF00";

            if (configData.Version < new VersionNumber(0, 3, 17))
            {
                if (configData.LockOnTypes.TryGetValue(LockTypes.AttackHelicopter, out ConfigData.LockType lockType))
                {
                    configData.LockOnTypes.Add(LockTypes.PatrolHelicopter, lockType);
                    configData.LockOnTypes[LockTypes.AttackHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 };
                }
                else
                {
                    configData.LockOnTypes[LockTypes.PatrolHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 };
                    configData.LockOnTypes[LockTypes.AttackHelicopter] = new ConfigData.LockType() { Enabled = true, Distance = 350 };
                }
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["locked"] = ">><color=#00E500> Target Locked </color><<",
            ["aquiring"] = ">><color=#E50000> Aquiring Target </color><<",
            ["loaded"] = "<color=#939393>A </color><color=#C4FF00>lock-on rocket</color><color=#939393> is loaded in your rocket launcher!</color>",
            ["inventory"] = "<color=#939393>You have a </color><color=#C4FF00>lock-on rocket</color><color=#939393> in your inventory! To use it load the smoke rocket into your rocket launcher</color>",
            ["noPerms"] = "You do not have permission to use this command",
            ["notEnoughResources"] = "You do not have the required resources to craft a lock-on rocket\nResources required per rocket: {0}",
            ["craftSuccess"] = "You have crafted a lock-on rocket!"
        };
        #endregion
    }
}
