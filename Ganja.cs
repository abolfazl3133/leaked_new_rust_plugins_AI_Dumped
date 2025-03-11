/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © copyright the_kiiiing.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Facepunch;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using PluginComponents.Ganja;
using PluginComponents.Ganja.Core;
using PluginComponents.Ganja.Extensions.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * VERSION HISTORY
 * 
 * V 1.0.7
 * - fixed gather permission being ignored for collectable hemp
 * - added smoke effect
 * - color effects rework
 * 
 * V 1.0.8
 * - fixed gathering not working
 * - improved ui, image library not needed anymore
 * - added weed skin ids to config
 * - added custom item support for loottable
 * 
 * V 1.0.9
 * - fixed drop chance when collecting hemp
 * 
 * V 2.0.0
 * - full rewrite
 * 
 * V 2.0.1
 * - add support for deployable nature
 * - brought back gene support
 * - amount of produced items can now be configured
 * 
 * V 2.0.2
 * - fix hook error with deployable nature
 * - modify internal crafting system
 * - change joint break effect
 * - add option to disable gathering from growable hemp
 * - add page navigation to crafting ui
 * 
 * V 2.0.3
 * - added the ability to extinguish joints
 * - joint burn time and loss per hit can now be configured
 * 
 * V 2.0.4
 * - apply stacking logic only to joints and weed
 * - prevent joints from being repaired
 * - burn time can now be configured per joint
 * - add support for AutoFarm
 * 
 * V 2.0.5
 * - display numbers with up to 4 digits
 * - drop overflow items when inventory is full
 * - fix bug when crafting a recipe with 5 ingredients
 * - adjust ui spacing
 * - support for PlanterboxDefender
 * 
 * V 2.0.6
 * - display amounts higher than 1000 as 1k
 * - add config option for G genes
 * 
 * V 2.0.7
 * - add scrollbar to recipe list
 * - resize container for rust update
 * - add metabolism options to joint buff config
 * 
 * V 2.0.8
 * - fix nre with scroll container on oxide
 * 
 * V 2.0.9
 * - fix display issues with large numbers
 *
 * V 2.0.10
 * - change to BasePlugin
 * - replace deprecated Pool methods
 * - add ganja.give command
 * - add bulk crafting
 *
 * V 2.0.11
 * - fix custom items not properly registered
 * 
 **/

namespace Oxide.Plugins
{
    [Info(nameof(Ganja), "The_Kiiiing", "2.0.11")]
    public class Ganja : BasePlugin<Ganja, Ganja.Configuration>
    {
        #region Fields

        #region Constants

        private const float JOINT_USE_COOLDOWN = 2f;

        [Perm]
        private const string PERM_GATHER = "ganja.gather";
        [Perm]
        private const string PERM_CRAFT = "ganja.craft";
        [Perm]
        private const string PERM_GIVE = "ganja.give";
        
        private const string CMD_TOGGLE_UI = "ganja.toggleui";
        private const string CMD_CRAFT = "ganja.craft";

        private const string SHAKE_EFFECT = "assets/bundled/prefabs/fx/screen_land.prefab";
        private const string SHAKE2_EFFECT = "assets/bundled/prefabs/fx/takedamage_generic.prefab";
        private const string VOMIT_EFFECT = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
        private const string LICK_EFFECT = "assets/bundled/prefabs/fx/gestures/lick.prefab";
        private const string BREATHE_EFFECT = "assets/prefabs/npc/bear/sound/breathe.prefab";
        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/door/barricade_spawn.prefab";

        #endregion

        [PluginReference, UsedImplicitly]
        private Plugin CustomSkinsStacksFix, StackModifier, Loottable, DeployableNature, PlanterboxDefender;
        
        private readonly Dictionary<ulong, float> lastUsed = new Dictionary<ulong, float>();
        private readonly Dictionary<Item, Timer> jointTimers = new Dictionary<Item, Timer>();

        private readonly List<ulong> openUis = new();

        #endregion

        #region Configuration

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        public class Configuration
        {
            [JsonProperty("Weed configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<WeedConfig> weedConfig = new()
            {
                new WeedConfig
                {
                    shortName = "sticks",
                    skinId = 2661029427,
                    displayName = "Low Quality Weed",
                    dropChance = 0.4f,
                    dropAmount = new MinMaxInt(1, 3),
                    biomeMask = 6,
                    minHGenesChance = 1,
                    minHGenesGuaranteed = 3,
                    disableCollGathering = false,
                    identifier = "low_quality"
                },
                new WeedConfig
                {
                    shortName = "sticks",
                    skinId = 2661031542,
                    displayName = "Medium Quality Weed",
                    dropChance = 0.3f,
                    dropAmount = new MinMaxInt(1, 3),
                    biomeMask = 1,
                    minHGenesChance = 1,
                    minHGenesGuaranteed = 3,
                    disableCollGathering = false,
                    identifier = "med_quality"
                },
                new WeedConfig
                {
                    shortName = "sticks",
                    skinId = 2660588149,
                    displayName = "High Quality Weed",
                    dropChance = 0.1f,
                    dropAmount = new MinMaxInt(1, 2),
                    biomeMask = 8,
                    minHGenesChance = 1,
                    minHGenesGuaranteed = 3,
                    disableCollGathering = false,
                    identifier = "high_quality"
                }
            };

#if COCA
            [JsonProperty("Berry configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<WeedConfig> berryConfig = new()
            {
                new WeedConfig
                {
                    shortName = "sticks",
                    skinId = 2946165766,
                    displayName = "Coca Leaves",
                    dropChance = 0.8f,
                    dropAmount = new MinMaxInt(1, 3),
                    biomeMask = 6,
                    minHGenesChance = 1,
                    minHGenesGuaranteed = 3,
                    disableCollGathering = false,
                    disableGrowableGathering = false
                }
            };
#endif

            [JsonProperty("Crafting Recipes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CustomRecipe> recipes = new()
            {
                new CustomRecipe
                {
                    identifier = "low_quality",
                    ingredientSlots = new Dictionary<int, Ingredient>
                    {
                        [0] = new Ingredient
                        {
                            shortName = "note",
                            skinId = 0,
                            amount = 1
                        },
                        [1] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2661029427,
                            amount = 1
                        },
                        [2] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2661029427,
                            amount = 1
                        }
                    },

                    producedItem = new ProducedItem
                    {
                        shortName = "horse.shoes.basic",
                        skinId = 2894101592,
                        displayName = "Low Quality Joint",
                        amount = 1
                    },
                    
                    isJoint = true,
                    boosts = new BoostCollection
                    {
                        woodPercentage = 0.4f,
                        woodDuration = 20f,
                        orePercentage = 0f,
                        oreDuration = 0f,
                        scrapPercentage = 0f,
                        scrapDuration = 0f,
                        maxHealthPercentage = 0f,
                        maxHealthDuration = 0f,
                        healingPerUse = 1f
                    }
                },

                new CustomRecipe
                {
                    identifier = "med_quality",
                    ingredientSlots = new Dictionary<int, Ingredient>
                    {
                        [0] = new Ingredient
                        {
                            shortName = "note",
                            skinId = 0,
                            amount = 1
                        },
                        [1] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2661031542,
                            amount = 1
                        },
                        [2] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2661031542,
                            amount = 1
                        }
                    },

                    producedItem = new ProducedItem
                    {
                        shortName = "horse.shoes.basic",
                        skinId = 2894101290,
                        displayName = "Medium Quality Joint",
                        amount = 1
                    },

                    isJoint = true,
                    boosts = new BoostCollection
                    {
                        woodPercentage = 0f,
                        woodDuration = 0f,
                        orePercentage = 0.8f,
                        oreDuration = 20f,
                        scrapPercentage = 0f,
                        scrapDuration = 0f,
                        maxHealthPercentage = 0f,
                        maxHealthDuration = 0f,
                        healingPerUse = 4f
                    }
                },

                new CustomRecipe
                {
                    identifier = "high_quality",
                    ingredientSlots = new Dictionary<int, Ingredient>
                    {
                        [0] = new Ingredient
                        {
                            shortName = "note",
                            skinId = 0,
                            amount = 1
                        },
                        [1] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2660588149,
                            amount = 1
                        },
                        [2] = new Ingredient
                        {
                            shortName = "sticks",
                            skinId = 2660588149,
                            amount = 1
                        }
                    },

                    producedItem = new ProducedItem
                    {
                        shortName = "horse.shoes.basic",
                        skinId = 2893700325,
                        displayName = "High Quality Joint",
                        amount = 1
                    },

                    isJoint = true,
                    boosts = new BoostCollection
                    {
                        woodPercentage = 0f,
                        woodDuration = 0f,
                        orePercentage = 0f,
                        oreDuration = 0f,
                        scrapPercentage = 1f,
                        scrapDuration = 30f,
                        maxHealthPercentage = 0.3f,
                        maxHealthDuration = 30f,
                        healingPerUse = 8f
                    }
                },
            };

            [JsonProperty("Require permission for crafting")]
            public bool enableCraftPerm = true;

            [JsonProperty("Require permission for gathering")]
            public bool enableGatherPerm = true;

            [JsonProperty("Disable built-in stack fix (set to true if you have problems with item stacking/splitting)")]
            public bool disableStackFix = false;

            [JsonProperty("Automatically extinguish joint when unequiping it")]
            public bool extinguishOnUnequip = true;

            [JsonIgnore]
            private HashSet<ulong> jointSkins;

            [JsonIgnore]
            private HashSet<ulong> weedSkins;

            public CustomRecipe GetJointRecipe(Item joint)
            {
                foreach (var recipe in recipes)
                {
                    if (!recipe.isJoint)
                    {
                        continue;
                    }

                    if (recipe.producedItem.skinId == joint.skin &&
                        recipe.producedItem.shortName == joint.info.shortname)
                    {
                        return recipe;
                    }
                }

                return null;
            }

            public bool IsJointSkin(ulong skin)
            {
                jointSkins ??= new HashSet<ulong>(recipes.Where(x => x.isJoint).Select(x => x.producedItem.skinId));

                return jointSkins.Contains(skin);
            }

            public bool IsWeedSkin(ulong skin)
            {
                weedSkins ??= new HashSet<ulong>(weedConfig.Select(x => x.skinId));

                return weedSkins.Contains(skin);
            }

        }
        
        #endregion

        #region Config Classes

        public class CustomRecipe
        {
            [JsonProperty("Ingredient Slots")]
            public Dictionary<int, Ingredient> ingredientSlots;

            [JsonProperty("Produced Item")]
            public ProducedItem producedItem;

            [JsonProperty("Identifier (used with ganja.give command)")]
            public string identifier = String.Empty;

            [JsonProperty("Is joint")]
            public bool isJoint;
            [JsonProperty("Boosts (only works for joints)")]
            public BoostCollection boosts;

            /// <summary>
            /// Attempt to craft the current recipe
            /// </summary>
            /// <param name="mixingTable">The mixing table containing the ingredients</param>
            /// <param name="overflow">Contains excess items when crafting was successful</param>
            /// <returns>true when crafting was successful</returns>
            public bool TryCraft(MixingTable mixingTable, List<Item> overflow)
            {
                if (ingredientSlots.Count < 1)
                {
                    LogError("Invalid Recipe! Recipe needs to have at least 1 ingredient");
                    return false;
                }

                var collect = Pool.Get<List<Item>>();
                var collectAmount = Pool.Get<List<int>>();
                var collectTimes = Int32.MaxValue;
                
                // Check if mixing table contains correct ingredients
                for (int slot = 0; slot < mixingTable.inventory.capacity; slot++)
                {
                    Ingredient ingredient;
                    try
                    {
                        ingredient = ingredientSlots[slot];
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }

                    var item = mixingTable.inventory.GetSlot(slot);

                    if (item == null || 
                        item.info.shortname != ingredient.shortName || 
                        item.skin != ingredient.skinId ||  
                        item.amount < ingredient.amount)
                    {
                        Pool.FreeUnmanaged(ref collect);
                        Pool.FreeUnmanaged(ref collectAmount);
                        return false;
                    }

                    collectTimes = Mathf.Min(collectTimes, Mathf.FloorToInt(item.amount / (float)ingredient.amount));
                    collect.Add(item);
                    collectAmount.Add(ingredient.amount);
                }

                LogDebug($"Collect {collectTimes} times");
                
                // Collect ingredients
                for (int i = 0; i < collect.Count; i++)
                {
                    var item = collect[i];
                    var amount = collectAmount[i] * collectTimes;

                    if (item.amount == amount)
                    {
                        item.Remove();
                    }
                    else
                    {
                        item.amount -= amount;
                        item.RemoveFromContainer();
                        overflow.Add(item);
                    }
                }

                // Free resources
                Pool.FreeUnmanaged(ref collect);
                Pool.FreeUnmanaged(ref collectAmount);
                ItemManager.DoRemoves();

                // Create new item
                var result = producedItem.CreateItem(producedItem.amount * collectTimes);
                if (result == null)
                {
                    LogError($"Crafting failed: failed to create item {producedItem.shortName}[{producedItem.skinId}] x{producedItem.amount} - make sure the item you specified in the config exists");
                    return false;
                }

                if (!result.MoveToContainer(mixingTable.inventory))
                {
                    LogError("Crafting failed: failed to move item");
                }

                return true;
            }
        }

        [SuppressMessage("ReSharper", "UnassignedField.Global")]
        public class WeedConfig : CustomItem
        {
            [JsonProperty("Drop chance when harvesting (1 = 100%)")]
            public float dropChance;
            [JsonProperty("Drop amount when harvesting")]
            public MinMaxInt dropAmount;
            [JsonProperty("Biome mask (see description for details)")]
            public ushort biomeMask;

            [JsonProperty("Minimum amount of H genes for a chance to yield weed")]
            public int minHGenesChance;
            [JsonProperty("Minimum amount of H genes for guaranteed yield")]
            public int minHGenesGuaranteed;
            [JsonProperty("Minimum amount of G genes for a chance to yield weed")]
            public int minGGenesChance;
            [JsonProperty("Minimum amount of G genes for guaranteed yield")]
            public int minGGenesGuaranteed;

            [JsonProperty("Disable gathering from collectable hemp")]
            public bool disableCollGathering;
            [JsonProperty("Disable gathering from growable hemp")]
            public bool disableGrowableGathering;

            [JsonProperty("Item identifier (used with ganja.give command)")]
            public string identifier = String.Empty;

            public bool IsValidGatherLocation(ushort locationMask)
            {
                //BitArray maskBits = new BitArray(BitConverter.GetBytes(biomeMask));
                //BitArray locationBits = new BitArray(BitConverter.GetBytes(locationMask));

                //return maskBits.And(locationBits).Cast<bool>().Contains(true);

                return (biomeMask & locationMask) > 0;
            }

            public bool MaybeGather(ushort locationMask, bool isCollectable, int hGenes, int gGenes, out Item weed)
            {
                weed = null;

                if (!IsValidGatherLocation(locationMask) || (isCollectable && disableCollGathering) || (!isCollectable && disableGrowableGathering))
                {
                    return false;
                }

                float chance = dropChance;

                if (!isCollectable)
                {
                    if (hGenes < minHGenesChance || gGenes < minGGenesChance)
                    {
                        // Not enough H or G genes
                        return false;
                    }
                    else if (hGenes >= minHGenesGuaranteed && gGenes >= minGGenesGuaranteed)
                    {
                        // Enough H and G genes for 100% drop chance
                        chance = 1f;
                    }
                }

                if (Random.Range(0f, 1f) > chance)
                {
                    return false;
                }

                int amount = dropAmount.Random();
                if (amount > 0)
                {
                    weed = CreateItem(amount);
                    if (weed == null)
                    {
                        Instance.PrintError("Failed to create weed item, check if your config is correct");
                    }
                }
                
                return weed != null;
            }
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        [SuppressMessage("ReSharper", "UnassignedField.Global")]
        public class BoostCollection
        {
            [JsonProperty("Wood boost percentage (1 = 100%)")]
            public float woodPercentage;
            [JsonProperty("Wood boost duration (seconds)")]
            public float woodDuration;

            [JsonProperty("Ore boost percentage (1 = 100%)")]
            public float orePercentage;
            [JsonProperty("Ore boost duration (seconds)")]
            public float oreDuration;

            [JsonProperty("Scrap boost percentage (1 = 100%)")]
            public float scrapPercentage;
            [JsonProperty("Scrap boost duration (seconds)")]
            public float scrapDuration;

            [JsonProperty("Max Health percentage (1 = 100%)")]
            public float maxHealthPercentage;
            [JsonProperty("Max Health duration (seconds)")]
            public float maxHealthDuration;

            [JsonProperty("Healing per use")]
            public float healingPerUse;
            [JsonProperty("Health regeneration per use")]
            public float regenerationPerUse;

            [JsonProperty("Poisoning per use(a negative value will decrease poisoning)")]
            public float poisonPerUse;
            [JsonProperty("Radiation poisoning per use (a negative value will decrease radiation)")]
            public float radiationPoisonPerUse;
            [JsonProperty("Bleeding per use (a negative value will decrease bleeding)")]
            public float bleedingPerUse;
            [JsonProperty("Calories per use (a negative value will decrease calories)")]
            public float caloriesPerUse;
            [JsonProperty("Hydration per use (a negative value will decrease hydration)")]
            public float hydrationPerUse;

            [JsonProperty("Joint durability (seconds)")]
            public float jointDurability = 120f;
            [JsonProperty("Joint durability loss per hit (seconds)")]
            public float jointDurabilityLossPerHit = 10f;

            [JsonIgnore]
            public float JointDurabilityLossPerSecond => 100f / jointDurability;

            public void ApplyToPlayer(BasePlayer player)
            {
                var mods = Pool.Get<List<ModifierDefintion>>();

                if (scrapDuration > 0 && scrapPercentage > 0)
                {
                    mods.Add(new ModifierDefintion
                    {
                        source = Modifier.ModifierSource.Tea,
                        type = Modifier.ModifierType.Scrap_Yield,
                        duration = scrapDuration,
                        value = scrapPercentage
                    });
                }

                if (woodDuration > 0 && woodPercentage > 0)
                {
                    mods.Add(new ModifierDefintion
                    {
                        source = Modifier.ModifierSource.Tea,
                        type = Modifier.ModifierType.Wood_Yield,
                        duration = woodDuration,
                        value = woodPercentage
                    });
                }

                if (oreDuration > 0 && orePercentage > 0)
                {
                    mods.Add(new ModifierDefintion
                    {
                        source = Modifier.ModifierSource.Tea,
                        type = Modifier.ModifierType.Ore_Yield,
                        duration = oreDuration,
                        value = orePercentage
                    });
                }

                if (maxHealthDuration > 0 && maxHealthPercentage > 0)
                {
                    mods.Add(new ModifierDefintion
                    {
                        source = Modifier.ModifierSource.Tea,
                        type = Modifier.ModifierType.Max_Health,
                        duration = maxHealthDuration,
                        value = maxHealthPercentage
                    });
                }

                player.modifiers.Add(mods);
                Pool.FreeUnmanaged(ref mods);

                if (healingPerUse > 0)
                {
                    player.Heal(healingPerUse);
                }

                if (radiationPoisonPerUse != 0)
                {
                    player.metabolism.radiation_poison.Add(radiationPoisonPerUse);
                }
                if (poisonPerUse != 0)
                {
                    player.metabolism.poison.Add(poisonPerUse);
                }
                if (bleedingPerUse != 0)
                {
                    player.metabolism.bleeding.Add(bleedingPerUse);
                }
                if (caloriesPerUse != 0)
                {
                    player.metabolism.calories.Add(caloriesPerUse);
                }
                if (hydrationPerUse != 0)
                {
                    player.metabolism.hydration.Add(hydrationPerUse);
                }
                if (regenerationPerUse != 0)
                {
                    player.metabolism.pending_health.Add(regenerationPerUse);
                }

                player.metabolism.isDirty = true;
                player.metabolism.SendChangesToClient();
            }

            public override string ToString()
            {
                return $"wood {woodPercentage:N2} {woodDuration}s; scrap  {scrapPercentage:N2} {scrapDuration}s; ore {orePercentage:N2} {oreDuration}s; maxHealth {maxHealthPercentage:N2} {maxHealthDuration}s; heal {healingPerUse}";
            }
        }

        public class Ingredient : SkinnedItem
        {
            [JsonProperty("Amount")]
            public int amount;
        }

        public class ProducedItem : CustomItem
        {
            [JsonProperty("Amount")]
            public int amount = 1;
        }

        public class CustomItem : SkinnedItem
        {
            [JsonProperty("Custom item name (null = default name)")]
            public string displayName;

            [JsonIgnore]
            public string UiDisplayName => GetDisplayName(false);

            public string GetDisplayName(bool nullIfNotCustom)
            {
                if (String.IsNullOrEmpty(displayName))
                {
                    if (nullIfNotCustom)
                    {
                        return null;
                    }
                    else
                    {
                        return ItemDefinition.displayName.english;
                    }
                }

                return displayName;
            }

            public override Item CreateItem(int amount)
            {
                var itm = ItemManager.Create(ItemDefinition, amount, skinId);
                string name = GetDisplayName(true);
                if (itm != null && name != null)
                {
                    itm.name = name;
                }

                return itm;
            }
        }

        public class SkinnedItem
        {
            [JsonProperty("Item short name")]
            public string shortName;
            [JsonProperty("Item skin id")]
            public ulong skinId;

            [JsonIgnore]
            public ItemDefinition ItemDefinition => ItemManager.FindItemDefinition(shortName);

            public virtual Item CreateItem(int amount)
            {
                return ItemManager.Create(ItemDefinition, amount, skinId);
            }
        }

        public struct MinMaxInt
        {
            public int min;
            public int max;

            public MinMaxInt(int min, int max)
            {
                this.min = min;
                this.max = max;
            }

            public int Random()
            {
                return UnityEngine.Random.Range(min, max + 1);
            }
        }

        #endregion

        #region Data

        [UsedImplicitly]
        private class PlayerData
        {
            [JsonProperty]
            // ReSharper disable once FieldCanBeMadeReadOnly.Local
            private Dictionary<ulong, bool> craftingUiState = new();

            private static PlayerData _ins;

            public static void Load()
            {
                _ins = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(Instance.Name);
            }

            public static void Save()
            {
                if (_ins != null)
                {
                    Interface.Oxide.DataFileSystem.WriteObject(Instance.Name, _ins);
                }
            }

            public static bool IsCraftingUiOpen(BasePlayer player)
            {
                if (_ins.craftingUiState.TryAdd(player.userID, false))
                {
                    return false;
                }

                return _ins.craftingUiState[player.userID];
            }

            public static bool ToggleCraftingUi(BasePlayer player)
            {
                if (_ins.craftingUiState.TryAdd(player.userID, true))
                {
                    return true;
                }

                return _ins.craftingUiState[player.userID] = !_ins.craftingUiState[player.userID];
            }
        }

        #endregion

        #region Helpers

        private bool DisableStackFix()
        {
            if (Config.disableStackFix)
            {
                return true;
            }

            return StackModifier != null || Loottable != null || CustomSkinsStacksFix != null;
        }

        private bool AllowedToCraft(BasePlayer player)
        {
            bool permEnabled = Config.enableCraftPerm;
            bool hasPermission = permission.UserHasPermission(player.UserIDString, PERM_CRAFT);
            bool result = !permEnabled || hasPermission;

            return result;
        }

        private bool AllowedToGather(BasePlayer player) => AllowedToGather(player.UserIDString);
        private bool AllowedToGather(string playerId)
        {
            bool permEnabled = Config.enableGatherPerm;
            bool hasPermission = permission.UserHasPermission(playerId, PERM_GATHER);
            bool result = !permEnabled || hasPermission;

            return result;
        }

        private void SendNote(BasePlayer player, Item item, int amount = 0)
        {
            player.Command("note.inv", item.info.itemid, amount == 0 ? item.amount.ToString() : amount.ToString(), item.name);
        }

        private bool IsDeployableNature(BaseEntity entity)
        {
            if (DeployableNature == null)
            {
                return false;
            }

            var b = DeployableNature.Call("STCanGainXP", null, entity) as Boolean?;
            if (b == false)
            {
                return true;
            }
            
            return false;
        }

        #endregion

        #region Functions

        private static bool IsJointOrWeed(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return Config.IsWeedSkin(item.skin) || Config.IsJointSkin(item.skin);
        }

        private static bool IsJoint(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return Config.IsJointSkin(item.skin);
        }

        private void UseJoint(BasePlayer player, Item joint)
        {
            if (!joint.HasFlag(global::Item.Flag.OnFire) || joint.isBroken)
            {
                return;
            }

            var def = Config.GetJointRecipe(joint);
            if (def == null)
            {
                LogError($"Failed to find joint definition for joint with skin id '{joint.skin}'");
                return;
            }

            def.boosts.ApplyToPlayer(player);
            RunEffects(player, 2.5f);
            JointLoseCondition(joint, def.boosts.JointDurabilityLossPerSecond * def.boosts.jointDurabilityLossPerHit);
        }

        private void IgniteJoint(BasePlayer player, Item joint)
        {
            if (joint.HasFlag(global::Item.Flag.OnFire))
            {
                return;
            }

            var def = Config.GetJointRecipe(joint);
            if (def == null)
            {
                LogError($"Failed to find joint definition for joint with skin id '{joint.skin}'");
                return;
            }

            joint.SetFlag(global::Item.Flag.OnFire, true);

            RunEffect("assets/prefabs/weapons/torch/effects/ignite.prefab", player);

            // Start decay timer
            jointTimers[joint] = timer.Every(1, () => JointLoseCondition(joint, def.boosts.JointDurabilityLossPerSecond));
        }

        private void ExtinguishJoint(BasePlayer player, Item joint)
        {
            if (!joint.HasFlag(global::Item.Flag.OnFire))
            {
                return;
            }

            joint.SetFlag(global::Item.Flag.OnFire, false);
            joint.MarkDirty();

            if (player != null)
            {
                RunEffect("assets/prefabs/weapons/torch/effects/extinguish.prefab", player);
            }

            if (jointTimers.ContainsKey(joint))
            {
                jointTimers[joint].Destroy();
                jointTimers.Remove(joint);
            }
        }

        private void JointLoseCondition(Item joint, float loss)
        {
            if (joint.condition - loss < 1f)
            {
                jointTimers[joint].Destroy();
                jointTimers.Remove(joint);
                joint.Remove();

                var player = joint.GetOwnerPlayer();
                if (player != null)
                {
                    RunEffect("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", player);
                }
            }
            else
            {
                joint.condition -= loss;
            }
        }

        private void OnHempGather(BasePlayer player, Vector3 plantPosition, bool isCollectable, int hGenes = 0, int gGenes = 0)
        {
            ushort locationMask = (ushort)TerrainMeta.BiomeMap.GetBiomeMaxType(plantPosition);

            foreach(var cfg in Config.weedConfig)
            {
                if (cfg.MaybeGather(locationMask, isCollectable, hGenes, gGenes, out var weed))
                {
                    if (player.inventory.containerMain.IsFull() &&
                        player.inventory.containerBelt.IsFull())
                    {
                        weed.Drop(plantPosition, Vector3.up * 3f);
                        SendNote(player, weed);
                        SendNote(player, weed, -weed.amount);
                    }
                    else
                    {
                        int amt = weed.amount;
                        player.inventory.GiveItem(weed);
                        SendNote(player, weed, amt);
                    }
                }
            }
        }

        #if COCA
        private void OnBerryGather(BasePlayer player, Vector3 plantPosition, bool isCollectable, int hGenes = 0, int gGenes = 0)
        {
            ushort locationMask = (ushort)TerrainMeta.BiomeMap.GetBiomeMaxType(plantPosition);

            foreach (var cfg in Config.berryConfig)
            {
                if (cfg.MaybeGather(locationMask, isCollectable, hGenes, gGenes, out var coca))
                {
                    if (player.inventory.containerMain.IsFull() &&
                        player.inventory.containerBelt.IsFull())
                    {
                        coca.Drop(plantPosition, Vector3.up * 3f);
                        SendNote(player, coca);
                        SendNote(player, coca, -coca.amount);
                    }
                    else
                    {
                        int amt = coca.amount;
                        player.inventory.GiveItem(coca);
                        SendNote(player, coca, amt);
                    }
                }
            }
        }
        #endif

        private bool OnCooldown(BasePlayer player)
        {
            ulong userId = player.userID;
            if (!lastUsed.ContainsKey(userId) || Time.time - lastUsed[userId] > JOINT_USE_COOLDOWN)
            {
                lastUsed[player.userID] = Time.time;
                return false;
            }

            return true;
        }

        #endregion

        #region Default Hooks

        protected override void Init()
        {
            base.Init();
            
            AddCovalenceCommand(CMD_CRAFT, nameof(CmdCraft));
            AddCovalenceCommand(CMD_TOGGLE_UI, nameof(CmdToggleUi));

            PlayerData.Load();

            timer.In(1f, () =>
            {
                if (DisableStackFix())
                {
                    Unsubscribe(nameof(CanStackItem));
                    Unsubscribe(nameof(CanCombineDroppedItem));
                    Unsubscribe(nameof(OnItemSplit));
                }
                else
                {
                    Subscribe(nameof(CanStackItem));
                    Subscribe(nameof(CanCombineDroppedItem));
                    Subscribe(nameof(OnItemSplit));
                }

                if (!Config.extinguishOnUnequip)
                {
                    Unsubscribe(nameof(OnActiveItemChanged));
                }
                else
                {
                    Subscribe(nameof(OnActiveItemChanged));
                }
            });
        }

        protected override void Unload()
        {
            RemoveEffectsGlobal();

            foreach (var id in openUis.ToArray())
            {
                var player = BasePlayer.FindByID(id);
                DestroyUi(player);
            }

            foreach (var joint in jointTimers.Keys.ToList())
            {
                ExtinguishJoint(null, joint);
            }

            PlayerData.Save();

            base.Unload();
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();
            
            foreach (var item in Config.weedConfig)
            {
                Loottable?.Call("AddCustomItem", this, item.ItemDefinition.itemid, item.skinId, item.displayName);
            }
        }

        #endregion

        #region Hooks

        #region Stack Fix

        [Hook] object CanStackItem(Item item, Item target)
        {
            if (DisableStackFix() || !IsJointOrWeed(item))
            {
                return null;
            }
            
            if (item.info.itemid != target.info.itemid
                || item.skin != target.skin
                || item.name != target.name)
            {
                return false;
            }
            
            return null;
        }

        [Hook] Item OnItemSplit(Item item, int amount)
        {
            if (DisableStackFix() || !IsJointOrWeed(item))
            {
                return null;
            }

            item.amount -= amount;
            var split = ItemManager.Create(item.info, amount, item.skin);
            split.name = item.name;
            split.MarkDirty();
            item.MarkDirty();

            return split;
        }

        [Hook] object CanCombineDroppedItem(WorldItem witem1, WorldItem witem2) => CanStackItem(witem1.item, witem2.item);

        #endregion

        #region Joint controls

        [Hook] void OnPlayerInput(BasePlayer player, InputState input)
        {
            var joint = player.GetActiveItem();

            if (!IsJoint(joint))
            {
                return;
            }

            // Ignite or extinguish
            if (input.IsDown(BUTTON.FIRE_SECONDARY))
            {
                if (!OnCooldown(player))
                {
                    if (joint.HasFlag(global::Item.Flag.OnFire))
                    {
                        ExtinguishJoint(player, joint);
                    }
                    else
                    {
                        IgniteJoint(player, joint);
                    }
                }
            }

            // Use
            if (input.IsDown(BUTTON.FIRE_PRIMARY))
            {
                if (!OnCooldown(player))
                {
                    UseJoint(player, joint);
                }
            }
        }

        [Hook] void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (IsJoint(oldItem) && Config.extinguishOnUnequip)
            {
                ExtinguishJoint(player, oldItem);
            }
        }

        #endregion

        #region Gathering

        [Hook] void OnGrowableGather(GrowableEntity plant, BasePlayer player)
        {
            if (PlanterboxDefender != null && PlanterboxDefender.Call("CanLootGrowableEntity", plant, player) != null)
            {
                return;
            }

            if (plant.prefabID == 3587624038 && plant.State == PlantProperties.State.Ripe && AllowedToGather(player))
            {
                OnHempGather(player, plant.transform.position, false, plant.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Hardiness), plant.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.GrowthSpeed));
            }

#if COCA
            if (plant.prefabID == 4038822397 && plant.State == PlantProperties.State.Ripe && AllowedToGather(player))
            {
                OnBerryGather(player, plant.transform.position, false, plant.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Hardiness), plant.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.GrowthSpeed));
            }
#endif
        }

        [Hook] void OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (entity.prefabID == 3006540952 && AllowedToGather(player) && !IsDeployableNature(entity) && !entity.IsDestroyed)
            {
                OnHempGather(player, entity.transform.position, true);
            }

#if COCA
            if (entity.prefabID == 1989241797 && AllowedToGather(player) && !IsDeployableNature(entity) && !entity.IsDestroyed)
            {
                OnBerryGather(player, entity.transform.position, true);
            }
#endif
        }

        // Support AutoFarm
        [Hook] void OnAutoFarmGather(string userID, StorageContainer container, Vector3 plantPositon, bool isCollectable, int hGenes = 0, int gGenes = 0)
        {
            if (!AllowedToGather(userID))
            {
                return;
            }

            ushort locationMask = (ushort)TerrainMeta.BiomeMap.GetBiomeMaxType(plantPositon);
            foreach (var cfg in Config.weedConfig)
            {
                if (cfg.MaybeGather(locationMask, isCollectable, hGenes, gGenes, out var weed))
                {
                    if (container.inventory.IsFull() || !weed.MoveToContainer(container.inventory))
                    {
                        weed.Drop(plantPositon, Vector3.up * 3f);
                    }
                }
            }
        }

        #endregion

        #region Crafting Ui

        [Hook]  void OnLootEntity(BasePlayer player, MixingTable entity)
        {
            if (entity == null)
            {
                return;
            }

            if (AllowedToCraft(player))
            {
                entity.OnlyAcceptValidIngredients = false;
                CreateToggleUi(player);
                if (PlayerData.IsCraftingUiOpen(player))
                {
                    CreateRecipeUi(player);
                    CreateCraftButtonUi(player);
                }
            }
        }

        [Hook]  void OnLootEntityEnd(BasePlayer player, MixingTable entity)
        {
            if (entity == null)
            {
                return;
            }

            entity.OnlyAcceptValidIngredients = true;
            DestroyUi(player);
        }

        #endregion

        #region Misc

        [Hook] ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            // Prevent joints form being put in repair bench
            if (container.entityOwner?.prefabID == 3846783416u && IsJoint(item))
            {
                return ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        [Hook] void OnPlayerDeath(BasePlayer p, HitInfo hitInfo) => OnPlayerDisconnected(p, null);

        [Hook] void OnPlayerDisconnected(BasePlayer p, string reason)
        {
            RemoveEffects(p);
            DestroyUi(p);
        }

        #endregion

        #endregion

        #region UI

        private static class UI
        {
            public const string LAYER_RECIPE = "ganja.ui.craft";
            public const string LAYER_TOGGLE = "ganja.ui.toggle";
            public const string LAYER_CRAFT_BUTTON = "ganja.ui.craftbutton";

            public const string LAYER_BLUR = "ganja.ui.effects.blur";
            public const string LAYER_COLOR = "ganja.ui.effects.color";

            public const string greenButtonColor = "0.415 0.5 0.258 0.7";
            // public const string redButtonColor = "0.8 0.28 0.2 1";
            // public const string blueButtonColor = "0.13 0.52 0.82 0.8";
            // public const string greyButtonColor = "0.4 0.4 0.4 0.4";

            public const string buttonColor = "0.75 0.75 0.75 0.3";
            public const string buttonTextColor2 = "0.68 0.68 0.68 1";

            public const string textColor = "0.745 0.709 0.674 1";

            public const string itemTileColor = "0.2 0.2 0.19 1";
        }

        private void CreateToggleUi(BasePlayer player)
        {
            var result = new CuiElementContainer();

            string rootPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "430 608",
                    OffsetMax = "572 633"
                }
            }, "Hud.Menu", UI.LAYER_TOGGLE);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                },
                Button =
                {
                    Command = CMD_TOGGLE_UI,
                    Color = UI.buttonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = !PlayerData.IsCraftingUiOpen(player) ? "SHOW CUSTOM RECIPES" : "HIDE CUSTOM RECIPES",
                    Color = UI.buttonTextColor2,
                    FontSize = 12
                }
            }, rootPanel);

            openUis.Add(player.userID);
            CuiHelper.AddUi(player, result);
        }

        private void CreateRecipeUi(BasePlayer player)
        {
            const int item_panel_size = 14;
            const int recipe_height = 40;
            CuiElementContainer result = new CuiElementContainer();

            #region Root Panel

            string rootPanelName = UI.LAYER_RECIPE;

            result.Add(new CuiElement
            {
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.33 0.314 0.29 1",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "192 302",
                        OffsetMax = "572 582"
                    },
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"0 -{Mathf.Max(0, (Config.recipes.Count - 7) * recipe_height)}",
                            OffsetMax = "0 0"
                        },
                        Vertical = true,
                        Horizontal = false,
                        ScrollSensitivity = 10f,
                        Elasticity = 0.05f,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            HandleColor = "1 1 1 1",
                            Size = 8,
                        }
                    }
                },
                Parent = "Hud.Menu",
                Name = rootPanelName
            });

            #endregion

            #region Recipe List

            for (int i = 0; i < Config.recipes.Count; i++)
            {
                var recipe = Config.recipes[i];

                string recipePanel = result.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = "0.5 0.5 0.15 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.02 1",
                        AnchorMax = $"0.96 1",
                        OffsetMax = $"0 -{i * recipe_height}",
                        OffsetMin = $"0 -{(i + 1) * recipe_height - 2}"
                    }
                }, rootPanelName);

                // Recipe preview
                result.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiImageComponent
                        {
                            SkinId = recipe.producedItem.skinId,
                            ItemId = recipe.producedItem.ItemDefinition.itemid
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.05 0.5",
                            AnchorMax = "0.05 0.5",
                            OffsetMin = $"{-item_panel_size} {-item_panel_size}",
                            OffsetMax = $"{item_panel_size} {item_panel_size}"
                        }
                    },
                    Parent = recipePanel
                });

                // Recipe name
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.11 0",
                        AnchorMax = "0.4 1"
                    },
                    Text =
                    {
                        Text = recipe.producedItem.amount > 1 ? $"{recipe.producedItem.UiDisplayName} x{recipe.producedItem.amount}" : recipe.producedItem.UiDisplayName,
                        Align = TextAnchor.MiddleLeft,
                        Color = UI.textColor,
                        FontSize = 12
                    }
                }, recipePanel);

                // Display ingredients
                for (int slot = 0; slot < 5; slot++)
                {
                    Ingredient ingredient;
                    string anchor = $"{0.58f + slot * 0.095f} 0.5";

                    // Ingredient panel
                    result.Add(new CuiPanel
                    {
                        Image = new CuiImageComponent
                        {
                            Color = UI.itemTileColor
                        },
                        RectTransform =
                        {
                            AnchorMin = anchor,
                            AnchorMax = anchor,
                            OffsetMin = $"{-item_panel_size} {-item_panel_size}",
                            OffsetMax = $"{item_panel_size} {item_panel_size}"
                        }
                    }, recipePanel);

                    try
                    {
                        ingredient = recipe.ingredientSlots[slot];
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }

                    // Ingredient image
                    result.Add(new CuiElement
                    {
                        Components =
                        {
                            new CuiImageComponent
                            {
                                SkinId = ingredient.skinId,
                                ItemId = ingredient.ItemDefinition.itemid
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = anchor,
                                AnchorMax = anchor,
                                OffsetMin = $"{-item_panel_size} {-item_panel_size}",
                                OffsetMax = $"{item_panel_size} {item_panel_size}"
                            }
                        },
                        Parent = recipePanel
                    });

                    // Ingredient amount
                    var amount = ingredient.amount < 1000 ? ingredient.amount.ToString() : (ingredient.amount / 1000f).ToString(CultureInfo.InvariantCulture) + "k";
                    result.Add(new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = anchor,
                                AnchorMax = anchor,
                                OffsetMin = $"{-item_panel_size-8} {-item_panel_size-2}",
                                OffsetMax = $"{item_panel_size-2} 0"
                            },
                            new CuiTextComponent
                            {
                                Text = amount,
                                Align = TextAnchor.MiddleRight,
                                Color = UI.textColor,
                                FontSize = 12
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "0.8 0.8",
                            }
                        },
                        Parent = recipePanel
                    });

                }
            }

            #endregion

            CuiHelper.AddUi(player, result);
        }

        private void CreateCraftButtonUi(BasePlayer player)
        {
            CuiElementContainer craftButton = new CuiElementContainer();

            #region Craft Button

            craftButton.Add(new CuiElement
            {
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.33 0.314 0.29 1",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "193 40",
                        OffsetMax = "420 98"
                    }
                },
                Parent = "Hud.Menu",
                Name = UI.LAYER_CRAFT_BUTTON
            });

            craftButton.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.03 0.1",
                    AnchorMax = "0.48 0.9",
                },
                Button =
                {
                    Command = CMD_CRAFT,
                    Color = UI.greenButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "START CRAFTING",
                    Color = UI.buttonTextColor2,
                    FontSize = 12
                }
            }, UI.LAYER_CRAFT_BUTTON);

            craftButton.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.52 0",
                    AnchorMax = "0.95 1"
                },
                Text =
                {
                    Text = "Use this button to craft custom recipes",
                    Align = TextAnchor.MiddleLeft,
                    Color = UI.textColor,
                    FontSize = 12,
                    Font = "robotocondensed-regular.ttf"
                }
            }, UI.LAYER_CRAFT_BUTTON);

            #endregion

            CuiHelper.AddUi(player, craftButton);
        }

        private void DestroyUi(BasePlayer player, string layer = null)
        {
            if (layer != null)
            {
                CuiHelper.DestroyUi(player, layer);
                return;
            }

            if (openUis.Remove(player.userID))
            {
                CuiHelper.DestroyUi(player, UI.LAYER_RECIPE);
                CuiHelper.DestroyUi(player, UI.LAYER_TOGGLE);
                CuiHelper.DestroyUi(player, UI.LAYER_CRAFT_BUTTON);
            }
        }

        #endregion

        #region Commands
        
        private void CmdToggleUi(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.Object is not BasePlayer player)
            {
                return;
            }

            DestroyUi(player);

            if (PlayerData.ToggleCraftingUi(player))
            {
                CreateRecipeUi(player);
                CreateCraftButtonUi(player);
            }

            CreateToggleUi(player);
        }

        private void CmdCraft(IPlayer iPlayer, string command, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            var mixingTable = player?.inventory.loot?.entitySource as MixingTable;

            if (iPlayer.IsServer || player == null || mixingTable == null || !AllowedToCraft(player))
            {
                return;
            }

            var overflow = Pool.Get<List<Item>>();
            foreach(var recipe in Config.recipes)
            {
                if (recipe.TryCraft(mixingTable, overflow))
                {
                    foreach(var item in overflow)
                    {
                        if (!item.MoveToContainer(player.inventory.containerMain))
                        {
                            item.DropAndTossUpwards(mixingTable.transform.position + mixingTable.transform.up * 1.2f);
                        }
                    }
                }
            }

            Pool.FreeUnmanaged(ref overflow);
        }

        [UniversalCommand("ganja.give", Permission = PERM_GIVE)]
        private void CmdGive(IPlayer iPlayer, string command, string[] args)
        {
            var itemType = args.GetString(0);
            var itemIdentifier = args.GetString(1);
            var amount = args.GetInt(2, 1);
            var targetPlayer = args.GetString(3, null);
            
            var player = targetPlayer != null ? BasePlayer.Find(targetPlayer) : iPlayer.Object as BasePlayer;
            if (player == null)
            {
                iPlayer.Reply(targetPlayer != null ? $"No player found with name or id '{targetPlayer}'" : "Invalid player. Please specify a target player explicitly");
                return;
            }

            CustomItem giveItem;            
            if (itemType == "weed")
            {
                giveItem = Config.weedConfig.Find(x => x.identifier?.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase) ?? false);
                if (giveItem == null)
                {
                    iPlayer.Reply($"No weed config found with identifier '{itemIdentifier}'");
                    return;
                }
            }
            else if (itemType == "joint")
            {
                giveItem = Config.recipes.Find(x => x.identifier?.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase) ?? false)?.producedItem;
                if (giveItem == null)
                {
                    iPlayer.Reply($"No joint config found with identifier '{itemIdentifier}'");
                    return;
                }
            }
            else
            {
                iPlayer.Reply("Invalid item type! Must be 'weed' or 'joint'");
                return;
            }

            var item = giveItem.CreateItem(amount);
            player.GiveItem(item);
            iPlayer.Reply($"Gave {item.name ?? item.info.displayName.english} x{amount} to {player.displayName}");
        }

        #endregion

        #region Effects

        private void RunEffects(BasePlayer player, float time)
        {
            const float repeatInterval = 0.25f;

            timer.In(time, () => CuiHelper.DestroyUi(player, UI.LAYER_BLUR));
            timer.In(time, () => CuiHelper.DestroyUi(player, UI.LAYER_COLOR));

            timer.Repeat(time / 3f, 2, () =>
            {
                if (Random.Range(0, 2) == 1)
                {
                    RunEffect(SHAKE_EFFECT, player);
                }
                else
                {
                    RunEffect(SHAKE2_EFFECT, player);
                }
            });

            RunEffect(BREATHE_EFFECT, player);
            RunEffect(LICK_EFFECT, player);

            timer.Repeat(0.25f, 4, () =>
            {
                RunEffect(SMOKE_EFFECT, player, false);
            });

            timer.In(time / 2f, () =>
            {
                if (Random.Range(0, 11) == 1)
                {
                    RunEffect(VOMIT_EFFECT, player);
                }
            });

            CreateBlur(player);
            timer.Repeat(repeatInterval, Mathf.FloorToInt(time / repeatInterval), () => CreateColor(player));
        }

        private void RunEffect(string effect, BasePlayer player, bool defaultPos = true, bool broadcast = true)
        {
            if (player == null)
            {
                return;
            }

            if (defaultPos)
            {
                Effect.server.Run(effect, player, 0, Vector3.zero, Vector3.zero, null, broadcast);
            }
            else
            {
                Effect.server.Run(effect, player, 0, Vector3.up*1.7f, new Vector3(1, 0, 0), null, broadcast);
            }
        }

        private void CreateBlur(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" },
                FadeOut = 1f
            }, "Overlay", UI.LAYER_BLUR);
            CuiHelper.DestroyUi(player, UI.LAYER_BLUR);
            CuiHelper.AddUi(player, container);
        }

        private void CreateColor(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = RandomColor() },
                FadeOut = 0.5f
            }, UI.LAYER_BLUR, UI.LAYER_COLOR);
            CuiHelper.DestroyUi(player, UI.LAYER_COLOR);
            CuiHelper.AddUi(player, container);
        }

        private void RemoveEffectsGlobal()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveEffects(player);
            }
        }

        private void RemoveEffects(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI.LAYER_BLUR);
        }

        private string RandomColor(float opacity = 0.3f)
        {
            var random = new System.Random();
            return $"{random.NextDouble()} {random.NextDouble()} {random.NextDouble()} {opacity}";
        }

        #endregion
    }
}
namespace PluginComponents.Ganja{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.Ganja.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.Ganja;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,nameof(TPlugin));protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
#if CARBON
true;
#else
false;
#endif
public const bool DEBUG=
#if DEBUG
true;
#else
false;
#endif
public static BasePlayer DebugPlayer=>DEBUG?BasePlayer.activePlayerList.FirstOrDefault(x=>!x.IsNpc):null;public static string PluginName=>Instance?.Name??"NULL";public static BasePlugin Instance{get;private set;}protected virtual UnityEngine.Color ChatColor=>default;protected virtual string ChatPrefix=>ChatColor!=default?$"<color=#{ColorUtility.ToHtmlStringRGB(ChatColor)}>[{Title}]</color>":$"[{Title}]";[HookMethod("Init")]protected virtual void Init(){Instance=this;foreach(var field in GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){if(field.IsLiteral&&!field.IsInitOnly&&field.FieldType==typeof(string)&&field.HasAttribute(typeof(PermAttribute))){if(field.GetValue(null)is string perm){LogDebug($"Auto-registered permission '{perm}'");permission.RegisterPermission(perm,this);}}}foreach(var method in GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)){if(method.GetCustomAttributes(typeof(UniversalCommandAttribute),true).FirstOrDefault()is UniversalCommandAttribute attribute){var commandName=attribute.Name??method.Name.ToLower().Replace("cmd",string.Empty);if(attribute.Permission!=null){LogDebug($"Auto-registered command '{commandName}' with permission '{attribute.Permission??"<null>"}'");}else{LogDebug($"Auto-registered command '{commandName}'");}AddUniversalCommand(commandName,method.Name,attribute.Permission);}}}[HookMethod("Unload")]protected virtual void Unload(){Instance=null;}[HookMethod("OnServerInitialized")]protected virtual void OnServerInitialized(bool initial){if(!CARBONARA){OnServerInitialized();}timer.In(OSI_DELAY,OnServerInitializedDelayed);}
#if CARBON
[HookMethod("OnServerInitialized")]
#endif
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.Ganja.Extensions.Command{using Oxide.Core.Libraries.Covalence;using System;using PluginComponents.Ganja;using PluginComponents.Ganja.Extensions;public static class CommandExtensions{public static string GetString(this string[]args,int index,string def=""){if(args.Length>index){return args[index];}else{return def;}}public static ulong GetUlong(this string[]args,int index,ulong def=0){if(UInt64.TryParse(args.GetString(index),out var value)){return value;}return def;}public static int GetInt(this string[]args,int index,int def=0){if(Int32.TryParse(args.GetString(index),out var value)){return value;}return def;}public static BasePlayer ToBasePlayer(this IPlayer iPlayer){return iPlayer.Object as BasePlayer;}public static bool TryGetBasePlayer(this IPlayer iplayer,out BasePlayer player){player=iplayer.IsServer?null:iplayer.Object as BasePlayer;return player!=null;}}}