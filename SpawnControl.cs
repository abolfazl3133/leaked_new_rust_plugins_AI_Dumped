using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.UIFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rust;
using UnityEngine;

using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;
using GridLayoutGroup = Oxide.Ext.Chaos.UIFramework.GridLayoutGroup;
using HorizontalLayoutGroup = Oxide.Ext.Chaos.UIFramework.HorizontalLayoutGroup;
using Layer = Oxide.Ext.Chaos.UIFramework.Layer;
using VerticalLayoutGroup = Oxide.Ext.Chaos.UIFramework.VerticalLayoutGroup;

namespace Oxide.Plugins
{
    [Info("SpawnControl", "k1lly0u", "2.0.10")]
    class SpawnControl : ChaosPlugin
    {
        #region Fields
        private static Hash<string, Datafile<MonumentGroups>> m_MonumentSpawnGroups;

        private static Datafile<Hash<string, SC_ConvarControlledSpawnPopulation>> m_ConVarSpawnPopulations;

        private static Datafile<Hash<string, SC_SpawnPopulation>> m_SpawnPopulations;

        private static Func<string, BasePlayer, string> m_GetString;

        private bool m_DoneInitialSetup = false;
        
        [Chaos.Permission] private const string USE_PERMISSION = "spawncontrol.admin";

        #endregion

        #region Oxide Hooks
        private void Init()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            m_GetString = GetString;
            m_CreateTimer = timer.Once;
            
            m_MonumentSpawnGroups = new Hash<string, Datafile<MonumentGroups>>();

            m_ConVarSpawnPopulations = new Datafile<Hash<string, SC_ConvarControlledSpawnPopulation>>($"{Name}/ConvarControlledSpawnPopulations");
            m_SpawnPopulations = new Datafile<Hash<string, SC_SpawnPopulation>>($"{Name}/SpawnPopulations");
        }

        private void OnServerInitialize()
        {
            if (!m_DoneInitialSetup)
                InitialSetup(false);
        }
        
        private void OnServerInitialized()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);
            
            if (!m_DoneInitialSetup)
                InitialSetup(Configuration.UpdateDistributionsReload);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ChaosUI.Destroy(player, SCUI);
            ChaosUI.Destroy(player, SCUI_OVERLAY);
            ChaosUI.Destroy(player, SCUI_POPUP);
        }

        private void Unload()
        {
            if (m_CurrentRoutine != null)
                Global.Runner.StopCoroutine(m_CurrentRoutine);

            SC_SpawnPopulation.OnUnload();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);
            
            RestoreDefaults();

            m_MonumentSpawnGroups = null;
            m_ConVarSpawnPopulations = null;
            m_SpawnPopulations = null;
        }

        #endregion

        #region Functions
        private void InitialSetup(bool updateDistributions)
        {
            SpawnHandler.Instance.TickInterval = Configuration.Handler.TickInterval;
            SpawnHandler.Instance.MinSpawnsPerTick = Configuration.Handler.MinSpawnsPerTick;
            SpawnHandler.Instance.MaxSpawnsPerTick = Configuration.Handler.MaxSpawnsPerTick;

            LoadConVarSpawnPopulations(updateDistributions);
            LoadSpawnPopulations(updateDistributions);
            LoadMonumentSpawnGroups();

            m_DoneInitialSetup = true;
        }

        private void RestoreDefaults()
        {
            SpawnHandler.Instance.TickInterval = 60;
            SpawnHandler.Instance.MinSpawnsPerTick = 100;
            SpawnHandler.Instance.MaxSpawnsPerTick = 100;

            RestoreConVarSpawnPopulations();
            RestoreSpawnPopulations();
            RestoreMonumentSpawnGroups();
        }

        private static string IsValidSpawnablePrefab(string s)
        {
            GameObject gameObject = GameManager.server.FindPrefab(s);
            if (!gameObject)
                return "Invalid prefab path entered";

            if (!gameObject.GetComponentInChildren<Spawnable>())
                return "Selected prefab must have a Spawnable component";

            return null;
        }
        
        private static string TranslatedString(string key, BasePlayer player) => m_GetString(key, player);
        #endregion

        #region Load Stored Values
        private void LoadConVarSpawnPopulations(bool updateDistributions)
        {
            bool isDirty = false;

            foreach (DensitySpawnPopulation population in SpawnHandler.Instance.ConvarSpawnPopulations)
            {
                SC_ConvarControlledSpawnPopulation spawnPopulation;
                if (!m_ConVarSpawnPopulations.Data.TryGetValue(population.name, out spawnPopulation))
                {
                    m_ConVarSpawnPopulations.Data.Add(population.name, 
                        spawnPopulation = new SC_ConvarControlledSpawnPopulation(population as ConvarControlledSpawnPopulation));
                    
                    Debug.Log($"[SpawnControl] Added ConVar SpawnPopulation '{population.name}' to data");
                    isDirty = true;
                }
                
                spawnPopulation.SetReference(population);
                spawnPopulation.SetDefaultValues(population);
                
                spawnPopulation.PopulationConvar = (population as ConvarControlledSpawnPopulation).PopulationConvar;
                spawnPopulation.ApplyTo(population as ConvarControlledSpawnPopulation, updateDistributions);
            }

            if (isDirty)
                m_ConVarSpawnPopulations.Save();
        }

        private void LoadSpawnPopulations(bool updateDistributions)
        {
            bool isDirty = false;

            foreach (SpawnPopulationBase population in SpawnHandler.Instance.SpawnPopulations)
            {
                if (!(population is DensitySpawnPopulation))
                    continue;
                
                SC_SpawnPopulation spawnPopulation;
                if (!m_SpawnPopulations.Data.TryGetValue(population.name, out spawnPopulation))
                {
                    m_SpawnPopulations.Data.Add(population.name, spawnPopulation = new SC_SpawnPopulation(population as DensitySpawnPopulation));
                    Debug.Log($"[SpawnControl] Added SpawnPopulation '{population.name}' to data");
                    isDirty = true;
                }
                
                spawnPopulation.SetReference(population as DensitySpawnPopulation);
                spawnPopulation.SetDefaultValues(population as DensitySpawnPopulation);
                
                spawnPopulation.ApplyTo(population as DensitySpawnPopulation, updateDistributions);
            }

            if (isDirty)
                m_SpawnPopulations.Save();
        }

        private void LoadMonumentSpawnGroups()
        {
            if (!string.IsNullOrEmpty(ConVar.Server.levelurl))
                FindSpawnGroupsInChildren("Decor", "Monument");
            
            FindSpawnGroupsInChildren("Monument");
            FindSpawnGroupsInChildren("DungeonBase");
            FindSpawnGroupsInChildren("Dungeon");

            FlushExpiredGroups();
        }

        private void FindSpawnGroupsInChildren(string hierarchyName, string replaceName = "")
        {
            Transform root = HierarchyUtil.GetRoot(hierarchyName).transform;

            if (!string.IsNullOrEmpty(replaceName))
                hierarchyName = replaceName;
            
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                SpawnGroup[] spawnGroups = child.GetComponentsInChildren<SpawnGroup>();
                IndividualSpawner[] individualSpawners = child.GetComponentsInChildren<IndividualSpawner>();

                if (spawnGroups.Length == 0 && individualSpawners.Length == 0)
                    continue;

                string shortname = System.IO.Path.GetFileNameWithoutExtension(child.name);

                MakeNamesUnique(spawnGroups.Select(x => x.gameObject).Distinct(), "SpawnGroup", shortname);
                MakeNamesUnique(individualSpawners.Select(x => x.gameObject).Distinct(), "IndividualSpawner", shortname);

                bool isDirty = false;

                Datafile<MonumentGroups> datafile;
                if (!m_MonumentSpawnGroups.TryGetValue(shortname, out datafile))
                {
                    datafile = m_MonumentSpawnGroups[shortname] = new Datafile<MonumentGroups>($"{Name}/{hierarchyName}/{shortname}");

                    if (datafile.Data.IsEmpty())
                        Debug.Log($"[SpawnControl] Added spawn group data file for '{shortname}' /data/{hierarchyName}/");
                }

                datafile.Data.ExistsOnMap = true;
                
                foreach (SpawnGroup spawnGroup in spawnGroups)
                {
                    string tier = ToTierString(spawnGroup.Tier);
                    
                    string lookup = $"{spawnGroup.name}{(!string.IsNullOrEmpty(tier) ? $":{tier}" : string.Empty)}";
                    
                    SC_SpawnGroup group;
                    if (!datafile.Data.Groups.TryGetValue(lookup, out group))
                    {
                        Debug.Log($"[SpawnControl] Added new spawngroup {lookup} to monument {shortname} data");
                        
                        datafile.Data.Groups.Add(lookup, group = new SC_SpawnGroup(spawnGroup));
                        isDirty = true;
                    }
                    
                    group.SetReference(spawnGroup);
                    group.SetDefaultValues(spawnGroup);
                    
                    group.ApplyTo(spawnGroup);
                }

                foreach (IndividualSpawner individualSpawner in individualSpawners)
                {
                    string lookup = individualSpawner.name;

                    SC_IndividualSpawner spawner;
                    if (!datafile.Data.Individuals.TryGetValue(lookup, out spawner))
                    {
                        Debug.Log($"[SpawnControl] Added new individual spawner {lookup} to monument {shortname} data");
                        
                        datafile.Data.Individuals.Add(lookup, spawner = new SC_IndividualSpawner(individualSpawner));
                        isDirty = true;
                    }
                    
                    spawner.SetReference(individualSpawner);
                    spawner.SetDefaultValues(individualSpawner);
                    
                    spawner.ApplyTo(individualSpawner);
                }

                if (isDirty)
                    datafile.Save();
            }
        }

        private void FlushExpiredGroups()
        {
            List<string> remove = Facepunch.Pool.Get<List<string>>();

            foreach (KeyValuePair<string, Datafile<MonumentGroups>> kvp in m_MonumentSpawnGroups)
            {
                if (!kvp.Value.Data.ExistsOnMap)
                    continue;

                bool isDirty = false;
                
                foreach (KeyValuePair<string, SC_SpawnGroup> spawnGroup in kvp.Value.Data.Groups)
                {
                    if (spawnGroup.Value.References == null || spawnGroup.Value.References.Count == 0)
                        remove.Add(spawnGroup.Key);
                }

                if (remove.Count > 0)
                {
                    Debug.Log($"[SpawnControl] Removing {remove.Count} unused SpawnGroups from {kvp.Key}");

                    foreach (string s in remove)
                        kvp.Value.Data.Groups.Remove(s);

                    isDirty = true;
                }
                remove.Clear();

                foreach (KeyValuePair<string, SC_IndividualSpawner> individualSpawners in kvp.Value.Data.Individuals)
                {
                    if (individualSpawners.Value.References == null || individualSpawners.Value.References.Count == 0)
                        remove.Add(individualSpawners.Key);
                }

                if (remove.Count > 0)
                {
                    Debug.Log($"[SpawnControl] Removing {remove.Count} unused IndividualSpawners from {kvp.Key}");
                    
                    foreach (string s in remove)
                        kvp.Value.Data.Individuals.Remove(s);

                    isDirty = true;
                }
                
                remove.Clear();
                
                if (isDirty)
                    kvp.Value.Save();
            }

            Facepunch.Pool.FreeUnmanaged(ref remove);
        }

        private string ToTierString(MonumentTier tier)
        {
            if ((int) tier <= 0)
                return string.Empty;

            string s = string.Empty;
            
            if ((tier & MonumentTier.Tier0) != (MonumentTier) 0)
                s += $"{(!string.IsNullOrEmpty(s) ? "|" : "")}{MonumentTier.Tier0}";
            
            if ((tier & MonumentTier.Tier1) != (MonumentTier) 0)
                s += $"{(!string.IsNullOrEmpty(s) ? "|" : "")}{MonumentTier.Tier1}";
            
            if ((tier & MonumentTier.Tier2) != (MonumentTier) 0)
                s += $"{(!string.IsNullOrEmpty(s) ? "|" : "")}{MonumentTier.Tier2}";
            
            return s;
        }

        private Dictionary<string, int> m_UniqueNames = new Dictionary<string, int>();

        private void MakeNamesUnique(IEnumerable<GameObject> array, string type, string monument)
        {
            foreach (GameObject t in array)
            {
                string name = t.name;
                
                if (m_UniqueNames.ContainsKey(name))
                {
                    m_UniqueNames[name]++;
                    t.name = $"{name} {m_UniqueNames[name]}";
                    
                    Debug.Log($"[SpawnControl] [{monument}] Renaming {type} match from '{name}' to '{t.name}'");
                }
                else
                {
                    m_UniqueNames[t.name] = 1;
                }
            }
            
            m_UniqueNames.Clear();
        }
        #endregion

        #region Restore Defaults
        private void RestoreConVarSpawnPopulations()
        {
            foreach (SC_ConvarControlledSpawnPopulation spawnPopulation in m_ConVarSpawnPopulations.Data.Values)
                spawnPopulation.RestoreDefaults();
        }

        private void RestoreSpawnPopulations()
        {
            foreach (SC_SpawnPopulation spawnPopulation in m_SpawnPopulations.Data.Values)
                spawnPopulation.RestoreDefaults();
        }

        private void RestoreMonumentSpawnGroups()
        {
            foreach (Datafile<MonumentGroups> datafile in m_MonumentSpawnGroups.Values)
            {
                foreach (SC_SpawnGroup spawnGroup in datafile.Data.Groups.Values)
                    spawnGroup.RestoreDefaults();

                foreach (SC_IndividualSpawner individualSpawner in datafile.Data.Individuals.Values)
                    individualSpawner.RestoreDefaults();
            }
        }
        #endregion
        
        #region Chat Commands
        [ChatCommand("sc")]
        private void CommandSpawnControl(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(USE_PERMISSION))
            {
                player.ChatMessage("You do not have permission to use this command");
                return;
            }
            
            CreateSpawnControlUI(player);
        }
        #endregion
        
        #region Console Commands
        [ConsoleCommand("sc.killgroups")]
        private void ccmdGroupKill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;

            if (m_RoutineRunning)
            {
                SendReply(arg, "[SpawnControl] Another action is currently being performed. Please wait until it has completed");
                return;
            }
            
            m_CurrentRoutine = Global.Runner.StartCoroutine(KillGroups());
        }

        [ConsoleCommand("sc.fillgroups")]
        private void ccmdGroupFill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;
            
            if (m_RoutineRunning)
            {
                SendReply(arg, "[SpawnControl] Another action is currently being performed. Please wait until it has completed");
                return;
            }
            
            m_CurrentRoutine = Global.Runner.StartCoroutine(FillGroups());
        }
        
        [ConsoleCommand("sc.enforcelimits")]
        private void ccmdEnforceLimits(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;
            
            if (m_RoutineRunning)
            {
                SendReply(arg, "[SpawnControl] Another action is currently being performed. Please wait until it has completed");
                return;
            }
            
            m_CurrentRoutine = Global.Runner.StartCoroutine(EnforceLimits());
        }
        
        [ConsoleCommand("sc.fillpopulations")]
        private void ccmdSpawnFill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;
            
            if (m_RoutineRunning)
            {
                SendReply(arg, "[SpawnControl] Another action is currently being performed. Please wait until it has completed");
                return;
            }
            
            m_CurrentRoutine = Global.Runner.StartCoroutine(FillPopulations());
        }

        [ConsoleCommand("sc.populationreport")]
        private void ccmdGetPopulationReport(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            PauseSpawnTicks(true);

            TextTable textTable = new TextTable();
            textTable.AddColumn("SpawnPopulation");
            textTable.AddColumn("Current");
            textTable.AddColumn("Limits");

            int totalCurrent = 0;
            int totalLimits = 0;
            for (int i = 0; i < SpawnHandler.Instance.SpawnPopulations.Length; i++)
            {
                DensitySpawnPopulation population = SpawnHandler.Instance.SpawnPopulations[i] as DensitySpawnPopulation;
                SpawnDistribution spawnDistribution = SpawnHandler.Instance.SpawnDistributions[i];
                if (population && spawnDistribution != null)
                {
                    int currentCount = spawnDistribution.Count;
                    int targetCount = population.GetTargetCount(spawnDistribution);

                    textTable.AddRow(population.name, currentCount.ToString(), targetCount.ToString());
                    totalCurrent += currentCount;
                    totalLimits += targetCount;
                }
            }

            textTable.AddRow("TOTAL:", totalCurrent.ToString(), totalLimits.ToString());

            SendReply(arg, "\n\n>> Report:\n" + textTable);
            PauseSpawnTicks(false);
        }

        [ConsoleCommand("sc.populationsettings")]
        private void ccmdGetPopulations(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;
            
            PauseSpawnTicks(true);
                
            TextTable textTable = new TextTable();
            textTable.AddColumns("Name", "Target Density", "Cluster Size Min", "Cluster Size Max", 
                "Cluster Dithering", "Attempts Initial", "Attempts Repeating", "Enforce Limits", "Scale (Large Maps)", 
                "Scale (Population)", "Scale (Spawn Filter)");
            
            for (int i = 0; i < SpawnHandler.Instance.SpawnPopulations.Length; i++)
            {
                DensitySpawnPopulation population = SpawnHandler.Instance.SpawnPopulations[i] as DensitySpawnPopulation;
                if (population == null)
                    continue;
                
                textTable.AddRow(population.name, population.TargetDensity.ToString("N2"), population.ClusterSizeMin.ToString(),
                    population.ClusterSizeMax.ToString("N2"), population.ClusterDithering.ToString(), population.SpawnAttemptsInitial.ToString(),
                    population.SpawnAttemptsRepeating.ToString(), population.EnforcePopulationLimits.ToString(), population.ScaleWithLargeMaps.ToString(),
                    population.ScaleWithServerPopulation.ToString(), population.ScaleWithSpawnFilter.ToString());
            }
            
            SendReply(arg, textTable.ToString());
            PauseSpawnTicks(false);
        }
        
        #region Routines
        private bool m_RoutineRunning = false;
        private Coroutine m_CurrentRoutine;
        
        private void PauseSpawnTicks(bool status)
        {
            ConVar.Spawn.respawn_populations = status;
            ConVar.Spawn.respawn_groups = status;
            ConVar.Spawn.respawn_individuals = status;
        }

        private IEnumerator KillGroups()
        {
            m_RoutineRunning = true;
            PauseSpawnTicks(false);

            int deleted = 0;
            
            Debug.Log($"[SpawnControl] Killing all spawn group entities...");
            for (int i = 0; i < SpawnHandler.Instance.SpawnGroups.Count; i++)
            {
                ISpawnGroup spawnGroup = SpawnHandler.Instance.SpawnGroups[i];
                
                deleted += spawnGroup.currentPopulation;
                spawnGroup.Clear();

                yield return null;
            }
            
            Debug.Log($"[SpawnControl] Killed {deleted} entities in {SpawnHandler.Instance.SpawnGroups.Count} spawn groups");
            
            PauseSpawnTicks(true);
            m_CurrentRoutine = null;
            m_RoutineRunning = false;
        }
        
        private IEnumerator FillGroups()
        {
            m_RoutineRunning = true;
            PauseSpawnTicks(false);

            int spawned = 0;
            
            Debug.Log($"[SpawnControl] Filling all spawn group entities...");
            for (int i = 0; i < SpawnHandler.Instance.SpawnGroups.Count; i++)
            {
                ISpawnGroup spawnGroup = SpawnHandler.Instance.SpawnGroups[i];
                int currentPopulation = spawnGroup.currentPopulation;
                spawnGroup.Fill();
                spawned += spawnGroup.currentPopulation - currentPopulation;
                yield return null;
            }
            
            Debug.Log($"[SpawnControl] Filled {spawned} entities in {SpawnHandler.Instance.SpawnGroups.Count} spawn groups");
            
            PauseSpawnTicks(true);
            m_CurrentRoutine = null;
            m_RoutineRunning = false;
        }
        
        private IEnumerator EnforceLimits()
        {
            m_RoutineRunning = true;
            PauseSpawnTicks(false);

            int deleted = 0;
            
            Debug.Log($"[SpawnControl] Enforcing spawn limits on all spawn populations...");
            for (int i = 0; i < SpawnHandler.Instance.SpawnPopulations.Length; i++)
            {
                DensitySpawnPopulation spawnPopulation = SpawnHandler.Instance.SpawnPopulations[i] as DensitySpawnPopulation;
                if (spawnPopulation && spawnPopulation.EnforcePopulationLimits)
                {
                    SpawnDistribution spawnDistribution = SpawnHandler.Instance.SpawnDistributions[i];

                    int targetCount = spawnPopulation.GetTargetCount(spawnDistribution);
                    Spawnable[] array = SpawnHandler.Instance.FindAll(spawnPopulation);
                    if (array.Length <= targetCount) 
                        continue;
                    
                    int amountToRemove = array.Length - targetCount;
                    
                    foreach (Spawnable current in array.Take(amountToRemove))
                    {
                        BaseEntity baseEntity = current.gameObject.ToBaseEntity();
                        if (baseEntity.IsValid())
                        {
                            deleted++;
                            baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                            yield return null;
                        }
                        else
                        {
                            GameManager.Destroy(current.gameObject, 0f);
                            deleted++;
                            yield return null;
                        }
                    }
                    
                    yield return null;
                }
            }
            
            Debug.Log($"[SpawnControl] Killed {deleted} entities from {SpawnHandler.Instance.SpawnPopulations.Length} spawn populations");
            
            PauseSpawnTicks(true);
            m_CurrentRoutine = null;
            m_RoutineRunning = false;
        }
        
        private IEnumerator FillPopulations()
        {
            m_RoutineRunning = true;
            PauseSpawnTicks(false);

            Debug.Log($"[SpawnControl] Filling all spawn populations...");
            for (int i = 0; i < SpawnHandler.Instance.SpawnPopulations.Length; i++)
            {
                DensitySpawnPopulation spawnPopulation = SpawnHandler.Instance.SpawnPopulations[i] as DensitySpawnPopulation;
                if (!spawnPopulation)
                    continue;
                
                SpawnDistribution spawnDistribution = SpawnHandler.Instance.SpawnDistributions[i];
                
                SpawnHandler.Instance.SpawnRepeating(spawnPopulation, spawnDistribution);
                yield return null;
            }
            
            Debug.Log($"[SpawnControl] Filled entities in {SpawnHandler.Instance.SpawnPopulations.Length} spawn populations");
            
            PauseSpawnTicks(true);
            m_CurrentRoutine = null;
            m_RoutineRunning = false;
        }
        #endregion
        #endregion

        #region UI

        private const string SCUI = "spawncontrol.ui";
        private const string SCUI_OVERLAY = "spawncontrol.ui.overlay";
        private const string SCUI_POPUP = "spawncontrol.ui.popup";
        
        private static CommandCallbackHandler m_CallbackHandler;

        private Category[] m_Categories = new Category[] { Category.MonumentSpawnGroups, Category.SpawnPopulations, Category.ConVarSpawnPopulations };

        private readonly GridLayoutGroup m_GridLayout = new GridLayoutGroup(Axis.Vertical)
        {
            Area = new Area(-245f, -215f, 245f, 215f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(237.5f, 20),
            FixedCount = new Vector2Int(2, 17),
        };

        private readonly HorizontalLayoutGroup m_CategoryLayout = new HorizontalLayoutGroup(3)
        {
            Area = new Area(-245f, -15f, 245f, 15f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
        };

        private readonly VerticalLayoutGroup m_ReportLayout = new VerticalLayoutGroup(16)
        {
            Area = new Area(-245f, -202.5f, 245f, 202.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedCount = new Vector2Int(1, 16),
        };
        
        private enum Category
        {
            SpawnPopulations,
            ConVarSpawnPopulations,
            MonumentSpawnGroups
        }
        
        private void CreateSpawnControlUI(BasePlayer player, Category category = Category.MonumentSpawnGroups, int page = 0)
        {
            BaseContainer root = ChaosPrefab.Background(SCUI, Layer.Overall, Anchor.Center, new Offset(-250f, -272.5f, 250f, 272.5f))
                .WithChildren(parent =>
                {
                    CreateTitleBar(player, parent);

                    CreateCategoryBar(player, parent, category);

                    switch (category)
                    {
                        case Category.SpawnPopulations:
                            CreateHeaderBar(player, parent, category, page, m_SpawnPopulations.Data.Count, m_GridLayout, CreateSpawnControlUI);
                            CreateElementLayout(player, parent, page, m_SpawnPopulations.Data, 
                                (population => population.Value.CreateUI(player)));
                            break;
                        case Category.ConVarSpawnPopulations:
                            CreateHeaderBar(player, parent, category, page, m_ConVarSpawnPopulations.Data.Count, m_GridLayout, CreateSpawnControlUI);
                            CreateElementLayout(player, parent, page, m_ConVarSpawnPopulations.Data, 
                                (population => population.Value.CreateUI(player)));
                            break;
                        case Category.MonumentSpawnGroups:
                            CreateHeaderBar(player, parent, category, page, m_MonumentSpawnGroups.Count, m_GridLayout, CreateSpawnControlUI);
                            CreateElementLayout(player, parent, page, m_MonumentSpawnGroups, 
                                (monumentGroup => monumentGroup.Value.Data.CreateUI(new MonumentGroups.UIUser(player, monumentGroup))));
                            break;
                    }
                })
                .DestroyExisting()
                .NeedsCursor()
                .NeedsKeyboard();
            
            ChaosUI.Show(player, root);
        }

        private void CreateSpawnReport(BasePlayer player, Category category, int page)
        {
            BaseContainer root = ChaosPrefab.Background(SCUI, Layer.Overall, Anchor.Center, new Offset(-250f, -272.5f, 250f, 272.5f))
                .WithChildren(parent =>
                {
                    CreateTitleBar(player, parent);

                    CreateCategoryBar(player, parent, category);

                    List<SC_SpawnPopulation> list = Facepunch.Pool.Get<List<SC_SpawnPopulation>>();
                    
                    if (category == Category.SpawnPopulations)
                        list.AddRange(m_SpawnPopulations.Data.Values);
                    
                    if (category == Category.ConVarSpawnPopulations)
                        list.AddRange(m_ConVarSpawnPopulations.Data.Values);
                    
                    list.Sort((a, b) => a.Name.CompareTo(b.Name));

                    CreateHeaderBar(player, parent, category, page, list.Count, m_ReportLayout, CreateSpawnReport);
                                        
                    ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -130f, -5f, -110f))
                        .WithChildren(columns =>
                        {
                            TextContainer.Create(columns, Anchor.CenterLeft, new Offset(5f, -10f, 105f, 10f))
                                .WithText(GetString("Label.Name", player))
                                .WithAlignment(TextAnchor.MiddleLeft);

                            TextContainer.Create(columns, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f))
                                .WithText(GetString("Label.Limits", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            TextContainer.Create(columns, Anchor.Center, new Offset(45f, -10f, 145f, 10f))
                                .WithText(GetString("Label.Current", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                        });

                    ChaosPrefab.Panel(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -135f))
                        .WithLayoutGroup(m_ReportLayout, list, page, (int i, SC_SpawnPopulation t, BaseContainer layout, Anchor anchor, Offset offset) =>
                        {
                            ChaosPrefab.Panel(layout, anchor, offset)
                                .WithChildren(template =>
                                {
                                    TextContainer.Create(template, Anchor.CenterLeft, new Offset(5f, -10f, 285f, 10f))
                                        .WithText(t.Name)
                                        .WithAlignment(TextAnchor.MiddleLeft);

                                    TextContainer.Create(template, Anchor.CenterRight, new Offset(-95f, -10f, 0f, 10f))
                                        .WithText(t.TargetCount.ToString())
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    TextContainer.Create(template, Anchor.CenterRight, new Offset(-195f, -10f, -95f, 10f))
                                        .WithText(t.CurrentCount.ToString())
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(template, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => t.CreateUI(player), $"{player.UserIDString}.report.{t.Name}");
                                });
                        });
                    
                    Facepunch.Pool.FreeUnmanaged(ref list);
                })
                .DestroyExisting()
                .NeedsCursor()
                .NeedsKeyboard();
            
            ChaosUI.Show(player, root);
        }

        private void CreateTitleBar(BasePlayer player, BaseContainer parent)
        {
            ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                .WithChildren(titleBar =>
                {
                    ChaosPrefab.Title(titleBar, Anchor.CenterLeft, new Offset(5f, -15f, 205f, 15f), Title)
                        .WithOutline(ChaosStyle.BlackOutline);

                    ChaosPrefab.CloseButton(titleBar, Anchor.CenterRight, new Offset(-25f, -10f, -5f, 10f), ChaosStyle.RedOutline)
                        .WithCallback(m_CallbackHandler, arg => ChaosUI.Destroy(player, SCUI), $"{player.UserIDString}.exit");
                });
        }

        private void CreateCategoryBar(BasePlayer player, BaseContainer parent, Category category)
        {
            ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -70f, -5f, -40f))
                .WithLayoutGroup(m_CategoryLayout, m_Categories, 0, (int i, Category t, BaseContainer categories, Anchor anchor, Offset offset) =>
                {
                    ChaosPrefab.TextButton(categories, anchor, offset, GetString($"Button.{t}", player), null, category == t ? ChaosStyle.GreenOutline : null)
                        .WithCallback(m_CallbackHandler, arg => CreateSpawnControlUI(player, t), $"{player.UserIDString}.{t}");
                });
        }

        private void CreateHeaderBar(BasePlayer player, BaseContainer parent, Category category, int page, int count, BaseLayoutGroup layout, Action<BasePlayer, Category, int> onClick)
        {
            // Header
            ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -105f, -5f, -75f))
                .WithChildren(header =>
                {
                    // Pagination
                    ChaosPrefab.PreviousPage(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f), page > 0)?
                        .WithCallback(m_CallbackHandler, arg => onClick(player, category, page - 1), $"{player.UserIDString}.{category}.previous");

                    ChaosPrefab.NextPage(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f), layout.HasNextPage(page, count))?
                        .WithCallback(m_CallbackHandler, arg => onClick(player, category, page + 1), $"{player.UserIDString}.{category}.next");

                    if (category is Category.SpawnPopulations or Category.ConVarSpawnPopulations)
                    {
                        ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-160f, -10f, -40f, 10f), 
                                GetString("Button.Report", player), null, layout == m_ReportLayout ? ChaosStyle.GreenOutline : null)
                            .WithCallback(m_CallbackHandler, arg => CreateSpawnReport(player, category, 0), $"{player.UserIDString}.spawnreport");
                    }
                });
        }

        private void CreateElementLayout<T>(BasePlayer player, BaseContainer parent, int page, Hash<string, T> collection, Action<KeyValuePair<string, T>> callback)
        {
            List<KeyValuePair<string, T>> list = Facepunch.Pool.Get<List<KeyValuePair<string, T>>>();
            list.AddRange(collection);
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            
            ChaosPrefab.Panel(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -110f))
                .WithLayoutGroup(m_GridLayout, list, page, (int i, KeyValuePair<string, T> t, BaseContainer layout, Anchor anchor, Offset offset) =>
                {
                    ChaosPrefab.TextButton(layout, anchor, offset, t.Key, null)
                        .WithCallback(m_CallbackHandler, arg => callback(t), $"{player.UserIDString}.layout.{i}");
                });
            
            Facepunch.Pool.FreeUnmanaged(ref list);
        }
        
        #region Popup Message

        private static Hash<ulong, Timer> m_PopupTimers = new Hash<ulong, Timer>();

        private static Func<float, Action, Timer> m_CreateTimer;

        private static void CreatePopupMessage(BasePlayer player, string message)
        {
            BaseContainer baseContainer = ChaosPrefab.Background(SCUI_POPUP, Layer.Overall, Anchor.Center, new Offset(-250f, -20f, 250f, 20f))
                .WithChildren(popup =>
                {
                    ChaosPrefab.Panel(popup, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                        .WithChildren(titleBar =>
                        {
                            TextContainer.Create(titleBar, Anchor.FullStretch, Offset.zero)
                                .WithText(message)
                                .WithAlignment(TextAnchor.MiddleCenter);

                        });
                })
                .DestroyExisting();
			
            ChaosUI.Show(player, baseContainer);

            Timer t;
            if (m_PopupTimers.TryGetValue(player.userID, out t))
                t?.Destroy();

            m_PopupTimers[player.userID] = m_CreateTimer(3f, () => ChaosUI.Destroy(player, SCUI_POPUP));
        }
        #endregion
        #endregion
   
        #region Config
        private ConfigData Configuration => ConfigurationData as ConfigData;
        
        protected class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Spawn Handler")]
            public SpawnHandler Handler { get; set; }
            
            [JsonProperty(PropertyName = "Update spawn distributions on plugin reload (slow)")]
            public bool UpdateDistributionsReload { get; set; }

            public class SpawnHandler
            {
                [JsonProperty(PropertyName = "Tick Interval (seconds)")]
                public float TickInterval { get; set; }

                [JsonProperty(PropertyName = "Minimum spawns per tick")]
                public int MinSpawnsPerTick { get; set; }

                [JsonProperty(PropertyName = "Maximum spawns per tick")]
                public int MaxSpawnsPerTick { get; set; }
            }
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override void OnConfigurationUpdated(VersionNumber version)
        {
            if (ConfigurationData.Version < new VersionNumber(2, 0, 0))
                ConfigurationData = GenerateDefaultConfiguration<ConfigData>();
        }

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Handler = new ConfigData.SpawnHandler
                {
                    TickInterval = 60f,
                    MinSpawnsPerTick = 100,
                    MaxSpawnsPerTick = 100
                },
                Version = Version
            } as T;
        }
        #endregion

        #region Data Structures

        [Serializable]
        public class MonumentGroups
        {
            public Hash<string, SC_SpawnGroup> Groups = new Hash<string, SC_SpawnGroup>();
            public Hash<string, SC_IndividualSpawner> Individuals = new Hash<string, SC_IndividualSpawner>();
            
            [JsonIgnore]
            public bool ExistsOnMap { get; set; }

            public bool IsEmpty() => Groups.Count == 0 && Individuals.Count == 0;

            public void UpdateReferencedComponents()
            {
                foreach (SC_SpawnGroup spawnGroup in Groups.Values)
                    spawnGroup.UpdateReferencedComponents();

                foreach (SC_IndividualSpawner individualSpawner in Individuals.Values)
                    individualSpawner.UpdateReferencedComponents();
            }

            #region UI
            [JsonIgnore] private bool m_IsDirty;

            public class UIUser
            {
                public BasePlayer Player;
                public string MonumentName;
                public Datafile<MonumentGroups> Datafile;
                public int IndividualPage;
                public int SpawnGroupPage;

                public string SelectedIndividual = string.Empty;
                public string SelectedSpawnGroup = string.Empty;

                public string AddPrefabString = string.Empty;
                public int AddPrefabWeight = 1;

                public UIUser(BasePlayer player, KeyValuePair<string, Datafile<MonumentGroups>> monument)
                {
                    Player = player;
                    Datafile = monument.Value;
                    MonumentName = monument.Key;
                }
            }

            private static readonly VerticalLayoutGroup m_SpawnGroupListLayout = new VerticalLayoutGroup
            {
                Area = new Area(-70f, -172.5f, 70f, 172.5f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(0f, 0f, 0f, 0f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(140, 20),
            };

            private static readonly VerticalLayoutGroup m_IndividualSpawnerListLayout = new VerticalLayoutGroup()
            {
                Area = new Area(-70f, -107.5f, 70f, 107.5f),
                Spacing = new Spacing(0f, 4f),
                Padding = new Padding(0f, 0f, 0f, 0f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(140, 20),
            };

            private static readonly VerticalLayoutGroup m_SpawnGroupPointsLayout = new VerticalLayoutGroup(20)
            {
                Area = new Area(-175f, -235f, 175f, 235f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedCount = new Vector2Int(0, 19)
            };

            private static readonly VerticalLayoutGroup m_PrefabsLayout = new VerticalLayoutGroup()
            {
                Area = new Area(-380f, -302.5f, 380f, 302.5f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(750, 20),
            };

            private static readonly Style m_ButtonSmallText = new Style(ChaosStyle.Button)
            {
                FontSize = 12
            };

            public void CreateUI(UIUser uiUser)
            {
                BaseContainer root = ChaosPrefab.Background(SCUI_OVERLAY, Layer.Overall, Anchor.FullStretch, Offset.zero)
                    .WithChildren(parent =>
                    {
                        ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(string.Format(TranslatedString("Label.Monument", uiUser.Player), uiUser.MonumentName))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ChaosPrefab.CloseButton(header, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f), ChaosStyle.RedOutline)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (m_IsDirty)
                                        {
                                            uiUser.Datafile.Save();
                                            UpdateReferencedComponents();
                                            m_IsDirty = false;
                                        }

                                        ChaosUI.Destroy(uiUser.Player, SCUI_POPUP);
                                        ChaosUI.Destroy(uiUser.Player, SCUI_OVERLAY);
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.exit");
                            });

                        CreateSpawnGroupList(uiUser, parent);
                        CreateIndividualSpawnerList(uiUser, parent);

                        if (!string.IsNullOrEmpty(uiUser.SelectedSpawnGroup))
                            CreateSpawnGroup(uiUser, parent, Groups[uiUser.SelectedSpawnGroup]);
                        else if (!string.IsNullOrEmpty(uiUser.SelectedIndividual))
                            CreateIndividualSpawner(uiUser, parent, Individuals[uiUser.SelectedIndividual]);
                    })
                    .DestroyExisting()
                    .NeedsCursor()
                    .NeedsKeyboard();

                ChaosUI.Show(uiUser.Player, root);
            }

            private void CreateSpawnGroupList(UIUser uiUser, BaseContainer parent)
            {
                ChaosPrefab.Panel(parent, Anchor.CenterLeft, new Offset(5f, -75f, 155f, 330f))
                    .WithChildren(spawngroups =>
                    {
                        ChaosPrefab.Panel(spawngroups, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                            .WithChildren(label =>
                            {
                                TextContainer.Create(label, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.SpawnGroups", uiUser.Player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                            });

                        // Pagination
                        ChaosPrefab.PreviousPage(spawngroups, Anchor.TopLeft, new Offset(5f, -50f, 72.5f, -30f), uiUser.SpawnGroupPage > 0)?
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                uiUser.SpawnGroupPage -= 1;
                                CreateUI(uiUser);
                            }, $"{uiUser.Player.UserIDString}.spawngroup.prevpage");

                        ChaosPrefab.NextPage(spawngroups, Anchor.TopLeft, new Offset(77.5f, -50f, 145f, -30f), m_SpawnGroupListLayout.HasNextPage(uiUser.SpawnGroupPage, Groups.Count))?
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                uiUser.SpawnGroupPage += 1;
                                CreateUI(uiUser);
                            }, $"{uiUser.Player.UserIDString}.spawngroup.nextpage");

                        // Layout
                        BaseContainer.Create(spawngroups, Anchor.FullStretch, new Offset(5f, 5f, -5f, -55f))
                            .WithLayoutGroup(m_SpawnGroupListLayout, Groups, uiUser.SpawnGroupPage, (int i, KeyValuePair<string, SC_SpawnGroup> t, BaseContainer layout, Anchor anchor, Offset offset) =>
                            {
                                //string[] s = t.Key.Split(':');

                                ChaosPrefab.TextButton(layout, anchor, offset, t.Key,  m_ButtonSmallText, uiUser.SelectedSpawnGroup == t.Key ? ChaosStyle.GreenOutline : null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.SelectedSpawnGroup = t.Key;
                                        uiUser.SelectedIndividual = string.Empty;
                                        CreateUI(uiUser);
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.{t.Key}");
                            });
                    });
            }

            private void CreateIndividualSpawnerList(UIUser uiUser, BaseContainer parent)
            {
                ChaosPrefab.Panel(parent, Anchor.CenterLeft, new Offset(5f, -350f, 155f, -80f))
                    .WithChildren(individualSpawners =>
                    {
                        ChaosPrefab.Panel(individualSpawners, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.IndividualSpawners", uiUser.Player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                            });

                        // Pagination
                        ChaosPrefab.PreviousPage(individualSpawners, Anchor.TopLeft, new Offset(5f, -50f, 72.5f, -30f), uiUser.IndividualPage > 0)?
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                uiUser.IndividualPage -= 1;
                                CreateUI(uiUser);
                            }, $"{uiUser.Player.UserIDString}.individual.prevpage");

                        ChaosPrefab.NextPage(individualSpawners, Anchor.TopLeft, new Offset(77.5f, -50f, 145f, -30f), m_IndividualSpawnerListLayout.HasNextPage(uiUser.IndividualPage, Individuals.Count))?
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                uiUser.IndividualPage += 1;
                                CreateUI(uiUser);
                            }, $"{uiUser.Player.UserIDString}.individual.nextpage");
                        
                        BaseContainer.Create(individualSpawners, Anchor.FullStretch, new Offset(5f, 0f, -5f, -55f))
                            .WithLayoutGroup(m_IndividualSpawnerListLayout, Individuals, uiUser.IndividualPage, (int i, KeyValuePair<string, SC_IndividualSpawner> t, BaseContainer layout, Anchor anchor, Offset offset) =>
                            {
                                ChaosPrefab.TextButton(layout, anchor, offset, t.Key, m_ButtonSmallText, uiUser.SelectedIndividual == t.Key ? ChaosStyle.GreenOutline : null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.SelectedSpawnGroup = string.Empty;
                                        uiUser.SelectedIndividual = t.Key;
                                        CreateUI(uiUser);
                                    }, $"{uiUser.Player.UserIDString}.individual.{t.Key}");
                            });
                    });
            }

            private void CreateSpawnGroup(UIUser uiUser, BaseContainer parent, SC_SpawnGroup spawnGroup)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-480f, -350f, 635f, 330f))
                    .WithChildren(spawngroupOptions =>
                    {
                        ChaosPrefab.Panel(spawngroupOptions, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(string.Format(TranslatedString("Label.Group.References", uiUser.Player), uiUser.SelectedSpawnGroup/*.Split(':')[0]*/, 
                                        spawnGroup.References?.Count, spawnGroup.GetCurrentSpawnInstances, spawnGroup.GetMaxSpawnInstances))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(0f, -10f, 100f, 10f),
                                        TranslatedString("Button.SpawnGroup.Fill", uiUser.Player), ChaosStyle.Header)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (spawnGroup.References?.Count > 0)
                                        {
                                            foreach (SpawnGroup group in spawnGroup.References)
                                                group.Fill();
                                            
                                            CreateUI(uiUser);
                                        }
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.fill");
                                
                                ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(105f, -10f, 205f, 10f),
                                        TranslatedString("Button.SpawnGroup.Clear", uiUser.Player), ChaosStyle.Button, ChaosStyle.RedOutline)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (spawnGroup.References?.Count > 0)
                                        {
                                            foreach (SpawnGroup group in spawnGroup.References)
                                                group.Clear();
                                            CreateUI(uiUser);
                                        }
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.clear");

                            });

                        CreateSpawnGroupSettings(uiUser, spawngroupOptions, spawnGroup);
                        CreateLocalSpawnList(uiUser, spawngroupOptions, spawnGroup);
                        CreatePrefabList(uiUser, spawngroupOptions, spawnGroup);
                    });
            }

            private void CreateSpawnGroupSettings(UIUser uiUser, BaseContainer parent, SC_SpawnGroup spawnGroup)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-557.5f, 160f, -207.5f, 315f))
                    .WithChildren(fields =>
                    {
                        ChaosPrefab.Panel(fields, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.Settings", uiUser.Player))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), TranslatedString("Button.Reset", uiUser.Player), null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        spawnGroup.SetDefaultSettings();
                                        m_IsDirty = true;
                                        CreateUI(uiUser);
                                    }, $"{uiUser.Player.UserIDString}.reset.settings");
                            });

                        ChaosPrefab.Panel(fields, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 130f))
                            .WithChildren(options =>
                            {
                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-170f, 40f, 170f, 60f))
                                    .WithChildren(maxPopulation =>
                                    {
                                        TextContainer.Create(maxPopulation, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                            .WithText(TranslatedString("Label.MaxPopulation", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(maxPopulation, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), spawnGroup.MaxPopulation.ToString())
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                spawnGroup.MaxPopulation = arg.GetInt(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.spawngroup.maxpop");
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-170f, 15f, 170f, 35f))
                                    .WithChildren(numToSpawnPerTickMin =>
                                    {
                                        TextContainer.Create(numToSpawnPerTickMin, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                            .WithText(TranslatedString("Label.NumToSpawnperTickMin", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(numToSpawnPerTickMin, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), spawnGroup.NumToSpawnPerTickMin.ToString())
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                spawnGroup.NumToSpawnPerTickMin = arg.GetInt(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.spawngroup.pertickmin");
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-170f, -10f, 170f, 10f))
                                    .WithChildren(numToSpawnPerTickMax =>
                                    {
                                        TextContainer.Create(numToSpawnPerTickMax, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                            .WithText(TranslatedString("Label.NumToSpawnperTickMax", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(numToSpawnPerTickMax, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), spawnGroup.NumToSpawnPerTickMax.ToString())
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                spawnGroup.NumToSpawnPerTickMax = arg.GetInt(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.spawngroup.pertickmax");
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-170f, -35f, 170f, -15f))
                                    .WithChildren(respawnDelayMin =>
                                    {
                                        TextContainer.Create(respawnDelayMin, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                            .WithText(TranslatedString("Label.RespawnDelayMin", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(respawnDelayMin, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), spawnGroup.RespawnDelayMin.ToString("N2"))
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                spawnGroup.RespawnDelayMin = arg.GetFloat(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.spawngroup.respawndelaymin");
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-170f, -60f, 170f, -40f))
                                    .WithChildren(respawnDelayMax =>
                                    {
                                        TextContainer.Create(respawnDelayMax, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                            .WithText(TranslatedString("Label.RespawnDelayMax", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(respawnDelayMax, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), spawnGroup.RespawnDelayMax.ToString("N2"))
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                spawnGroup.RespawnDelayMax = arg.GetFloat(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.spawngroup.respawndelaymax");
                                    });
                            });
                    });
            }

            private void CreateLocalSpawnList(UIUser uiUser, BaseContainer parent, SC_SpawnGroup spawnGroup)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-557.5f, -340f, -207.5f, 155f))
                    .WithChildren(spawnpoints =>
                    {
                        ChaosPrefab.Panel(spawnpoints, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.LocalSpawnPoints", uiUser.Player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                            });

                        if (spawnGroup.References?.Count > 0)
                        {
                            ChaosPrefab.Panel(spawnpoints, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 470f))
                                .WithLayoutGroup(m_SpawnGroupPointsLayout, spawnGroup.SpawnPoints, 0, (int i, BaseSpawnPoint t, BaseContainer options, Anchor anchor, Offset offset) =>
                                {
                                    ChaosPrefab.Panel(options, anchor, offset)
                                        .WithChildren(template =>
                                            {
                                                TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                                    .WithText(t.transform.localPosition + $"{((t is RadialSpawnPoint) ? $" (in {(t as RadialSpawnPoint).radius}m radius)" : string.Empty)}")
                                                    .WithAlignment(TextAnchor.MiddleLeft);

                                                ChaosPrefab.TextButton(template, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), 
                                                    TranslatedString("Button.Teleport", uiUser.Player), null)
                                                    .WithCallback(m_CallbackHandler, arg => uiUser.Player.Teleport(t.transform.position), $"{uiUser.Player.UserIDString}.spawngrouptp.{i}");
                                        });
                                });
                        }
                        else
                        {
                            ChaosPrefab.Panel(spawnpoints, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 495f))
                                .WithChildren(noReferences =>
                                {
                                    TextContainer.Create(noReferences, Anchor.FullStretch, Offset.zero)
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithText(TranslatedString("Label.NoReferencedGroups", uiUser.Player));
                                });
                        }
                    });
            }

            private void CreatePrefabList(UIUser uiUser, BaseContainer parent, SC_SpawnGroup spawnGroup)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-202.5f, -340f, 557.5f, 315f))
                    .WithChildren(prefabs =>
                    {
                        ChaosPrefab.Panel(prefabs, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.Prefabs", uiUser.Player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                                
                                ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), TranslatedString("Button.Reset", uiUser.Player), null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        spawnGroup.SetDefaultPrefabs();
                                        m_IsDirty = true;
                                        CreateUI(uiUser);
                                    }, $"{uiUser.Player.UserIDString}.reset.prefabs");
                            });

                        ChaosPrefab.Panel(prefabs, Anchor.BottomStretch, new Offset(0f, 25f, 0f, 630f))
                            .WithLayoutGroup(m_PrefabsLayout, spawnGroup.Prefabs, 0, (int i, SC_SpawnGroup.SC_SpawnEntry t, BaseContainer options, Anchor anchor, Offset offset) =>
                            {
                                ChaosPrefab.Panel(options, anchor, offset)
                                    .WithChildren(template =>
                                    {
                                        TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                            .WithText(TranslatedString("Label.Prefab", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(template, Anchor.FullStretch, new Offset(60f, 0f, -120f, 0f), t.Prefab)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                string s = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                                string result = IsValidSpawnablePrefab(s);

                                                if (!string.IsNullOrEmpty(result))
                                                {
                                                    CreateUI(uiUser);
                                                    CreatePopupMessage(uiUser.Player, result);
                                                }
                                                else
                                                {
                                                    t.Prefab = s;
                                                    m_IsDirty = true;
                                                    CreateUI(uiUser);
                                                }
                                            }, $"{uiUser.Player.UserIDString}.prefabstring.{i}");

                                        TextContainer.Create(template, Anchor.FullStretch, new Offset(640.8591f, 0f, 6.103516E-05f, 0f))
                                            .WithText(TranslatedString("Label.Weight", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleLeft);

                                        ChaosPrefab.Input(template, Anchor.CenterRight, new Offset(-60f, -10f, 0f, 10f), t.Weight.ToString())
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                t.Weight = arg.GetInt(1);
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.prefabweight.{i}");
                                    });
                            });
                        
                        ChaosPrefab.Panel(prefabs, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 20f))
                            .WithChildren(addPrefab =>
                            {
                                ChaosPrefab.TextButton(addPrefab, Anchor.TopLeft, new Offset(0f, -20f, 75f, 0f), 
                                        TranslatedString("Label.AddPrefab", uiUser.Player), ChaosStyle.Header)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        string result = IsValidSpawnablePrefab(uiUser.AddPrefabString);

                                        if (!string.IsNullOrEmpty(result))
                                        {
                                            CreateUI(uiUser);
                                            CreatePopupMessage(uiUser.Player, result);
                                        }
                                        else
                                        {
                                            spawnGroup.Prefabs.Add(new SC_SpawnGroup.SC_SpawnEntry
                                            {
                                                Prefab = uiUser.AddPrefabString,
                                                Weight = uiUser.AddPrefabWeight
                                            });

                                            uiUser.AddPrefabWeight = 1;
                                            uiUser.AddPrefabString = string.Empty;

                                            m_IsDirty = true;
                                            CreateUI(uiUser);
                                        }
                                    });
                                    
                                ChaosPrefab.Input(addPrefab, Anchor.FullStretch, new Offset(80f, 0f, -120f, 0f), uiUser.AddPrefabString)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        string s = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                        string result = IsValidSpawnablePrefab(s);

                                        if (!string.IsNullOrEmpty(result))
                                        {
                                            CreateUI(uiUser);
                                            CreatePopupMessage(uiUser.Player, result);
                                        }
                                        else
                                        {
                                            uiUser.AddPrefabString = s;
                                            CreateUI(uiUser);
                                        }
                                    }, $"{uiUser.Player.UserIDString}.addprefabstring");

                                TextContainer.Create(addPrefab, Anchor.FullStretch, new Offset(645f, 0f, 6.103516E-05f, 0f))
                                    .WithText("Weight")
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                ChaosPrefab.Input(addPrefab, Anchor.CenterRight, new Offset(-65f, -10f, -5f, 10f), uiUser.AddPrefabWeight.ToString())
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.AddPrefabWeight = arg.GetInt(1);
                                        CreateUI(uiUser);
                                    }, $"{uiUser.Player.UserIDString}.addprefabweight");
                            });
                    });
            }

            private void CreateIndividualSpawner(UIUser uiUser, BaseContainer parent, SC_IndividualSpawner individualSpawner)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-480f, -350f, 635f, 330f))
                    .WithChildren(individualOptions =>
                    {
                        ChaosPrefab.Panel(individualOptions, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(string.Format(TranslatedString("Label.Group.References", uiUser.Player), 
                                        uiUser.SelectedIndividual, individualSpawner.References?.Count, individualSpawner.GetCurrentSpawnInstances, individualSpawner.GetMaxSpawnInstances))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(0f, -10f, 100f, 10f),
                                        TranslatedString("Button.IndividualSpawner.Fill", uiUser.Player), ChaosStyle.Header)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (individualSpawner.References?.Count > 0)
                                        {
                                            foreach (IndividualSpawner spawner in individualSpawner.References)
                                                spawner.Fill();
                                            CreateUI(uiUser);
                                        }
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.fill");
                                
                                ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(105f, -10f, 205f, 10f),
                                        TranslatedString("Button.IndividualSpawner.Clear", uiUser.Player), ChaosStyle.Button, ChaosStyle.RedOutline)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (individualSpawner.References?.Count > 0)
                                        {
                                            foreach (IndividualSpawner spawner in individualSpawner.References)
                                                spawner.Clear();
                                            CreateUI(uiUser);
                                        }
                                    }, $"{uiUser.Player.UserIDString}.spawngroup.clear");

                            });
                        
                        BaseContainer.Create(individualOptions, Anchor.Center, new Offset(-557.5f, 160f, -207.5f, 315f))
                            .WithChildren(fields =>
                            {
                                ChaosPrefab.Panel(fields, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                                    .WithChildren(header =>
                                    {
                                        TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                            .WithText(TranslatedString("Label.Settings", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleCenter);

                                        ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), TranslatedString("Button.Reset", uiUser.Player), null)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                individualSpawner.SetDefaultSettings();
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.reset.settings");
                                    });

                                ChaosPrefab.Panel(fields, Anchor.BottomStretch, new Offset(0f, 50f, 0f, 130f))
                                    .WithChildren(options =>
                                    {
                                        ChaosPrefab.Panel(options, Anchor.TopCenter, new Offset(-170f, -25f, 170f, -5f))
                                            .WithChildren(respawnDelayMin =>
                                            {
                                                TextContainer.Create(respawnDelayMin, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                                    .WithText(TranslatedString("Label.RespawnDelayMin", uiUser.Player))
                                                    .WithAlignment(TextAnchor.MiddleLeft);

                                                ChaosPrefab.Input(respawnDelayMin, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), individualSpawner.RespawnDelayMin.ToString("N1"))
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        individualSpawner.RespawnDelayMin = arg.GetFloat(1);
                                                        m_IsDirty = true;
                                                        CreateUI(uiUser);
                                                    }, $"{uiUser.Player.UserIDString}.spawngroup.respawndelaymin");
                                            });

                                        ChaosPrefab.Panel(options, Anchor.TopCenter, new Offset(-170f, -50f, 170f, -30f))
                                            .WithChildren(respawnDelayMax =>
                                            {
                                                TextContainer.Create(respawnDelayMax, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                                                    .WithText(TranslatedString("Label.RespawnDelayMax", uiUser.Player))
                                                    .WithAlignment(TextAnchor.MiddleLeft);

                                                ChaosPrefab.Input(respawnDelayMax, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), individualSpawner.RespawnDelayMax.ToString("N1"))
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        individualSpawner.RespawnDelayMax = arg.GetFloat(1);
                                                        m_IsDirty = true;
                                                        CreateUI(uiUser);
                                                    }, $"{uiUser.Player.UserIDString}.spawngroup.respawndelaymax");
                                            });

                                        ChaosPrefab.Panel(options, Anchor.TopCenter, new Offset(-170f, -75f, 170f, -55f))
                                            .WithChildren(useBoundsCheckMask =>
                                            {
                                                TextContainer.Create(useBoundsCheckMask, Anchor.HoriztonalCenterStretch, new Offset(5f, -10f, -20f, 10f))
                                                    .WithText(TranslatedString("Label.UseBoundsCheckMask", uiUser.Player))
                                                    .WithAlignment(TextAnchor.MiddleLeft);

                                                ChaosPrefab.Toggle(useBoundsCheckMask, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f), individualSpawner.UseBoundsCheckMask)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        individualSpawner.UseBoundsCheckMask = !individualSpawner.UseBoundsCheckMask;
                                                        m_IsDirty = true;
                                                        CreateUI(uiUser);
                                                    }, $"{uiUser.Player.UserIDString}.individualspawner.boundscheckmask");
                                            });
                                    });
                            });

                        BaseContainer.Create(individualOptions, Anchor.Center, new Offset(-202.5f, -365f, 557.5f, 315f))
                            .WithChildren(prefabs =>
                            {
                                ChaosPrefab.Panel(prefabs, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                                    .WithChildren(header =>
                                    {
                                        TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                            .WithText(TranslatedString("Label.PrefabToSpawn", uiUser.Player))
                                            .WithAlignment(TextAnchor.MiddleCenter);
                                        
                                        ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), 
                                                TranslatedString("Button.Reset", uiUser.Player), null)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                individualSpawner.SetDefaultPrefab();
                                                m_IsDirty = true;
                                                CreateUI(uiUser);
                                            }, $"{uiUser.Player.UserIDString}.reset.prefabs");
                                    });

                                ChaosPrefab.Panel(prefabs, Anchor.BottomStretch, new Offset(0f, 625f, 0f, 655f))
                                    .WithChildren(options =>
                                    {
                                        ChaosPrefab.Panel(options, Anchor.TopCenter, new Offset(-375f, -25f, 375f, -5f))
                                            .WithChildren(template =>
                                            {
                                                TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                                    .WithText(TranslatedString("Label.Prefab", uiUser.Player))
                                                    .WithAlignment(TextAnchor.MiddleLeft);

                                                ChaosPrefab.Input(template, Anchor.FullStretch, new Offset(60f, 0f, 0f, 0f), individualSpawner.Prefab)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        string s = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                                        string result = IsValidSpawnablePrefab(s);

                                                        if (!string.IsNullOrEmpty(result))
                                                        {
                                                            CreateUI(uiUser);
                                                            CreatePopupMessage(uiUser.Player, result);
                                                        }
                                                        else
                                                        {
                                                            individualSpawner.Prefab = s;
                                                            m_IsDirty = true;
                                                            CreateUI(uiUser);
                                                        }
                                                    }, $"{uiUser.Player.UserIDString}.prefabstring");
                                            });
                                    });
                            });
                    });
            }
            #endregion
        }

        [Serializable]
        public class SC_SpawnGroup
        {
            public int MaxPopulation { get; set; }

            public int NumToSpawnPerTickMin { get; set; }

            public int NumToSpawnPerTickMax { get; set; }

            public float RespawnDelayMin { get; set; }

            public float RespawnDelayMax { get; set; }

            public List<SC_SpawnEntry> Prefabs { get; set; }

            [JsonIgnore]
            public List<SpawnGroup> References;

            [JsonIgnore]
            private SC_SpawnGroup Defaults;

            [JsonIgnore] 
            public BaseSpawnPoint[] SpawnPoints;

            [JsonIgnore]
            public int GetMaxSpawnInstances
            {
                get
                {
                    if (References?.Count == 0)
                        return 0;

                    return References[0].maxPopulation * References.Count;
                }
            }

            [JsonIgnore]
            public int GetCurrentSpawnInstances
            {
                get
                {
                    if (References?.Count == 0)
                        return 0;

                    return References?.Sum(x => x.currentPopulation) ?? 0;
                }
            }

            public SC_SpawnGroup()
            {
            }

            public SC_SpawnGroup(SpawnGroup spawnGroup)
            {
                MaxPopulation = spawnGroup.maxPopulation;
                NumToSpawnPerTickMin = spawnGroup.numToSpawnPerTickMin;
                NumToSpawnPerTickMax = spawnGroup.numToSpawnPerTickMax;
                RespawnDelayMin = spawnGroup.respawnDelayMin;
                RespawnDelayMax = spawnGroup.respawnDelayMax;

                Prefabs = new List<SC_SpawnEntry>();
                spawnGroup.prefabs.ForEach((SpawnGroup.SpawnEntry spawnEntry) => Prefabs.Add(new SC_SpawnEntry(spawnEntry)));
            }
            
            public void SetReference(SpawnGroup spawnGroup)
            {
                if (References == null)
                    References = new List<SpawnGroup>();
                
                References.Add(spawnGroup);
                
                if (SpawnPoints == null || SpawnPoints.Length == 0)
                    SpawnPoints = spawnGroup.GetComponentsInChildren<BaseSpawnPoint>(true);
            }

            public void SetDefaultValues(SpawnGroup spawnGroup)
            {
                if (Defaults == null)
                    Defaults = new SC_SpawnGroup(spawnGroup);
            }

            public void UpdateReferencedComponents()
            {
                foreach (SpawnGroup reference in References)
                    ApplyTo(reference);
            }

            public void SetDefaultSettings()
            {
                if (Defaults != null)
                {
                    MaxPopulation = Defaults.MaxPopulation;
                    NumToSpawnPerTickMin= Defaults.NumToSpawnPerTickMin;
                    NumToSpawnPerTickMax = Defaults.NumToSpawnPerTickMax;
                    RespawnDelayMin = Defaults.RespawnDelayMin;
                    RespawnDelayMax = Defaults.RespawnDelayMax;
                }
            }

            public void SetDefaultPrefabs()
            {
                if (Defaults != null)
                {
                    Prefabs = Defaults.Prefabs;
                }
            }

            public void ApplyTo(SpawnGroup spawnGroup)
            {
                /*if (spawnGroup.currentPopulation == 0 && spawnGroup.spawnPoints.Any(x => x is SpaceCheckingSpawnPoint))
                {
                    foreach (var spawnPoint in spawnGroup.spawnPoints)
                    {
                        spawnPoint.
                    }
                }*/
                
                spawnGroup.maxPopulation = MaxPopulation;
                spawnGroup.numToSpawnPerTickMin = NumToSpawnPerTickMin;
                spawnGroup.numToSpawnPerTickMax = NumToSpawnPerTickMax;
                spawnGroup.respawnDelayMin = RespawnDelayMin;
                spawnGroup.respawnDelayMax = RespawnDelayMax;
                
                spawnGroup.prefabs.Clear();
                Prefabs.ForEach((SC_SpawnEntry spawnEntry) =>
                {
                    SpawnGroup.SpawnEntry spawn = spawnEntry.Create();
                    if (spawn != null)
                        spawnGroup.prefabs.Add(spawn);
                });
                
                spawnGroup.spawnClock.events.Clear();
                
                if (spawnGroup.WantsTimedSpawn())
                    spawnGroup.spawnClock.Add(spawnGroup.GetSpawnDelta(), spawnGroup.GetSpawnVariance(), spawnGroup.Spawn);
            }

            public void RestoreDefaults()
            {
                if (References?.Count > 0 && Defaults != null)
                {
                    foreach (SpawnGroup reference in References)
                    {
                        reference.maxPopulation = Defaults.MaxPopulation;
                        reference.numToSpawnPerTickMin = Defaults.NumToSpawnPerTickMin;
                        reference.numToSpawnPerTickMax = Defaults.NumToSpawnPerTickMax;
                        reference.respawnDelayMin = Defaults.RespawnDelayMin;
                        reference.respawnDelayMax = Defaults.RespawnDelayMax;

                        reference.prefabs.Clear();
                        Defaults.Prefabs.ForEach((SC_SpawnEntry spawnEntry) =>
                        {
                            SpawnGroup.SpawnEntry spawn = spawnEntry.Create();
                            if (spawn != null)
                                reference.prefabs.Add(spawn);
                        });
                        
                        reference.spawnClock.events.Clear();
                
                        if (reference.WantsTimedSpawn())
                            reference.spawnClock.Add(reference.GetSpawnDelta(), reference.GetSpawnVariance(), reference.Spawn);
                    }
                }
            }

            [Serializable]
            public class SC_SpawnEntry
            {
                public string Prefab { get; set; }

                public int Weight { get; set; }

                public SC_SpawnEntry()
                {
                }

                public SC_SpawnEntry(SpawnGroup.SpawnEntry spawnEntry)
                {
                    Prefab = spawnEntry.prefab.resourcePath;
                    Weight = spawnEntry.weight;
                }

                public SpawnGroup.SpawnEntry Create()
                {
                    string guid;
                    if (GameManifest.pathToGuid.TryGetValue(Prefab, out guid))
                    {
                        return new SpawnGroup.SpawnEntry
                        {
                            prefab = new GameObjectRef {guid = guid},
                            weight = Weight
                        };
                    }

                    return null;
                }
            }
        }

        [Serializable]
        public class SC_IndividualSpawner
        {            
            public float RespawnDelayMin { get; set; }

            public float RespawnDelayMax { get; set; }

            public bool UseBoundsCheckMask { get; set; }

            public string Prefab { get; set; }

            [JsonIgnore]
            public List<IndividualSpawner> References;

            [JsonIgnore]
            private SC_IndividualSpawner Defaults;
            
            [JsonIgnore]
            public int GetMaxSpawnInstances
            {
                get
                {
                    return References?.Count ?? 0;
                }
            }

            [JsonIgnore]
            public int GetCurrentSpawnInstances
            {
                get
                {
                    if (References?.Count == 0)
                        return 0;

                    return References?.Sum(x => x.IsSpawned || (!x.HasSpaceToSpawn()) ? 1 : 0) ?? 0;
                }
            }

            public SC_IndividualSpawner() { }

            public SC_IndividualSpawner(IndividualSpawner individualSpawner)
            {              
                RespawnDelayMin = individualSpawner.respawnDelayMin;
                RespawnDelayMax = individualSpawner.respawnDelayMax;

                UseBoundsCheckMask = individualSpawner.useCustomBoundsCheckMask;

                Prefab = individualSpawner.entityPrefab.resourcePath;
            }

            public void SetReference(IndividualSpawner individualSpawner)
            {
                if (References == null)
                    References = new List<IndividualSpawner>();
                References.Add(individualSpawner);
            }

            public void SetDefaultValues(IndividualSpawner individualSpawner)
            {
                if (Defaults == null)
                    Defaults = new SC_IndividualSpawner(individualSpawner);
            }

            public void UpdateReferencedComponents()
            {
                foreach (IndividualSpawner reference in References)
                    ApplyTo(reference);
            }

            public void SetDefaultSettings()
            {
                if (Defaults != null)
                {
                    RespawnDelayMin = Defaults.RespawnDelayMin;
                    RespawnDelayMax = Defaults.RespawnDelayMax;
                    UseBoundsCheckMask = Defaults.UseBoundsCheckMask;
                }
            }

            public void SetDefaultPrefab()
            {
                if (Defaults != null)
                {
                    Prefab = Defaults.Prefab;
                }
            }
            
            public void ApplyTo(IndividualSpawner individualSpawner)
            {
                individualSpawner.respawnDelayMin = RespawnDelayMin;
                individualSpawner.respawnDelayMax = RespawnDelayMax;

                individualSpawner.useCustomBoundsCheckMask = UseBoundsCheckMask;

                if (individualSpawner.entityPrefab.resourcePath != Prefab)
                {
                    string guid;
                    if (GameManifest.pathToGuid.TryGetValue(Prefab, out guid))                    
                        individualSpawner.entityPrefab = new GameObjectRef() { guid = guid };                    
                    else Debug.Log($"[SpawnControl] - Unable to find GUID for prefab path {Prefab}. Check your IndividualSpawner setup and correct this prefab path");
                }
            }  
            
            public void RestoreDefaults()
            {
                if (References?.Count > 0 && Defaults != null)
                {
                    foreach (IndividualSpawner reference in References)
                    {
                        reference.respawnDelayMin = Defaults.RespawnDelayMin;
                        reference.respawnDelayMax = Defaults.RespawnDelayMax;

                        reference.useCustomBoundsCheckMask = Defaults.UseBoundsCheckMask;

                        if (reference.entityPrefab.resourcePath != Defaults.Prefab)
                        {
                            string guid;
                            if (GameManifest.pathToGuid.TryGetValue(Defaults.Prefab, out guid))
                                reference.entityPrefab = new GameObjectRef() {guid = guid};
                        }
                    }
                }
            }
        }

        [Serializable]
        public class SC_ConvarControlledSpawnPopulation : SC_SpawnPopulation
        {
            [JsonIgnore]
            public string PopulationConvar;
            
            [JsonIgnore]
            private ConsoleSystem.Command m_Command;

            [JsonIgnore]
            public ConsoleSystem.Command Command
            {
                get
                {
                    if (m_Command == null)
                        m_Command = ConsoleSystem.Index.Server.Find(PopulationConvar);
                    
                    return m_Command;
                }
            }

            public SC_ConvarControlledSpawnPopulation() { }

            public SC_ConvarControlledSpawnPopulation(ConvarControlledSpawnPopulation spawnPopulation) : base(spawnPopulation)
            {
                PopulationConvar = spawnPopulation.PopulationConvar;
            }

            public override void ApplyTo(DensitySpawnPopulation spawnPopulation, bool updateDistribution)
            {
                base.ApplyTo(spawnPopulation, updateDistribution);

                Command.Set(TargetDensity);
            }

            public override void RestoreDefaults()
            {
                base.RestoreDefaults();
                
                if (Reference != null && Defaults != null)
                    Command.Set(TargetDensity);
            }

            protected override void SetDefaultSettings()
            {
                base.SetDefaultSettings();
                
                if (Defaults != null)
                    Command.Set(TargetDensity);
            }
        }

        [Serializable]
        public class SC_SpawnPopulation
        {
            public float TargetDensity { get; set; }

            public float SpawnRate { get; set; }

            public int ClusterSizeMin { get; set; }

            public int ClusterSizeMax { get; set; }

            public int ClusterDithering { get; set; }

            public int SpawnAttemptsInitial { get; set; }

            public int SpawnAttemptsRepeating { get; set; }

            public bool EnforcePopulationLimits { get; set; }

            public bool ScaleWithLargeMaps { get; set; }

            public bool ScaleWithSpawnFilter { get; set; }

            public bool ScaleWithServerPopulation { get; set; }

            public List<string> ResourceList { get; set; }

            public string ResourceFolder { get; set; }

            public SC_SpawnFilter Filter { get; set; }

            [JsonIgnore]
            protected DensitySpawnPopulation Reference;

            [JsonIgnore] 
            protected SpawnDistribution m_Distribution;

            [JsonIgnore]
            protected SpawnDistribution Distribution
            {
                get
                {
                    if (m_Distribution == null)
                        m_Distribution = SpawnHandler.Instance.population2distribution[Reference];

                    return m_Distribution;
                }
            }

            [JsonIgnore]
            protected SC_SpawnPopulation Defaults;

            [JsonIgnore]
            public string Name => Reference.name;
            
            [JsonIgnore]
            public int TargetCount => Reference.GetTargetCount(Distribution);
            
            [JsonIgnore]
            public int CurrentCount => Distribution.Count;

            public SC_SpawnPopulation()
            {
            }

            public SC_SpawnPopulation(DensitySpawnPopulation spawnPopulation)
            {
                TargetDensity = spawnPopulation.TargetDensity;
                SpawnRate = spawnPopulation.SpawnRate;
                ClusterSizeMin = spawnPopulation.ClusterSizeMin;
                ClusterSizeMax = spawnPopulation.ClusterSizeMax;
                ClusterDithering = spawnPopulation.ClusterDithering;
                SpawnAttemptsInitial = spawnPopulation.SpawnAttemptsInitial;
                SpawnAttemptsRepeating = spawnPopulation.SpawnAttemptsRepeating;
                EnforcePopulationLimits = spawnPopulation.EnforcePopulationLimits;
                ScaleWithLargeMaps = spawnPopulation.ScaleWithLargeMaps;
                ScaleWithSpawnFilter = spawnPopulation.ScaleWithSpawnFilter;
                ScaleWithServerPopulation = spawnPopulation.ScaleWithServerPopulation;

                if (spawnPopulation.ResourceList != null && spawnPopulation.ResourceList.Length > 0)
                {
                    ResourceList = spawnPopulation.ResourceList.Select(x => x.resourcePath).ToList();
                    ResourceFolder = string.Empty;
                }
                else if (!string.IsNullOrEmpty(spawnPopulation.ResourceFolder))
                {
                    ResourceFolder = spawnPopulation.ResourceFolder;
                    Prefab<Spawnable>[] prefabs = Prefab.Load<Spawnable>("assets/bundled/prefabs/autospawn/" + ResourceFolder, GameManager.server, PrefabAttribute.server, false);

                    ResourceList = new List<string>();
                    ResourceList.AddRange(prefabs.Select(x => x.Name));
                }

                Filter = new SC_SpawnFilter(spawnPopulation.Filter);
            }

            public void SetReference(DensitySpawnPopulation spawnPopulation) => Reference = spawnPopulation;
            
            public void SetDefaultValues(DensitySpawnPopulation t) => Defaults = new SC_SpawnPopulation(t);

            protected virtual void SetDefaultSettings()
            {
                if (Defaults != null)
                {
                    TargetDensity = Defaults.TargetDensity;
                    SpawnRate = Defaults.SpawnRate;
                    ClusterSizeMin = Defaults.ClusterSizeMin;
                    ClusterSizeMax = Defaults.ClusterSizeMax;
                    ClusterDithering = Defaults.ClusterDithering;
                    SpawnAttemptsInitial = Defaults.SpawnAttemptsInitial;
                    SpawnAttemptsRepeating = Defaults.SpawnAttemptsRepeating;
                    EnforcePopulationLimits = Defaults.EnforcePopulationLimits;
                    ScaleWithLargeMaps = Defaults.ScaleWithLargeMaps;
                    ScaleWithSpawnFilter = Defaults.ScaleWithSpawnFilter;
                    ScaleWithServerPopulation = Defaults.ScaleWithServerPopulation;
                    m_IsDirty = true;
                    
                    UpdateReferencedComponents();
                }
            }

            private void SetDefaultFilter()
            {
                if (Defaults != null)
                {
                    Filter.SplatType = Defaults.Filter.SplatType;
                    Filter.BiomeType = Defaults.Filter.BiomeType;
                    Filter.TopologyAll = Defaults.Filter.TopologyAll;
                    Filter.TopologyAny = Defaults.Filter.TopologyAny;
                    Filter.TopologyNot = Defaults.Filter.TopologyNot;
                    m_IsDirty = true;
                    
                    UpdateReferencedComponents();
                }
            }

            public void UpdateReferencedComponents()
            {
                if (Reference != null)
                    ApplyTo(Reference, false);
            }

            public virtual void ApplyTo(DensitySpawnPopulation spawnPopulation, bool updateDistributions)
            {
                spawnPopulation._targetDensity = TargetDensity;
                spawnPopulation.SpawnRate = SpawnRate;
                spawnPopulation.ClusterSizeMin = ClusterSizeMin;
                spawnPopulation.ClusterSizeMax = ClusterSizeMax;
                spawnPopulation.ClusterDithering = ClusterDithering;
                spawnPopulation.SpawnAttemptsInitial = SpawnAttemptsInitial;
                spawnPopulation.SpawnAttemptsRepeating = SpawnAttemptsRepeating;
                spawnPopulation.EnforcePopulationLimits = EnforcePopulationLimits;
                spawnPopulation.ScaleWithLargeMaps = ScaleWithLargeMaps;
                spawnPopulation.ScaleWithSpawnFilter = ScaleWithSpawnFilter;
                spawnPopulation.ScaleWithServerPopulation = ScaleWithServerPopulation;
                
                if (ResourceList.Count > 0)
                {
                    List<GameObjectRef> gameObjectRefs = Facepunch.Pool.Get<List<GameObjectRef>>();
                    
                    spawnPopulation.ResourceFolder = string.Empty;
                    for (int i = 0; i < ResourceList.Count; i++)
                    {
                        string guid;
                        if (!GameManifest.pathToGuid.TryGetValue(ResourceList[i], out guid))
                        {
                            Debug.LogError($"[SpawnControl] Unable to find GUID for prefab path {ResourceList[i]}. Check your SpawnPopulation setup and correct this prefab path");
                            continue;
                        }
                        
                        gameObjectRefs.Add(new GameObjectRef(){guid = guid});
                    }

                    spawnPopulation.ResourceList = gameObjectRefs.ToArray();
                    Facepunch.Pool.FreeUnmanaged(ref gameObjectRefs);
                }
                else if (!string.IsNullOrEmpty(ResourceFolder))
                    spawnPopulation.ResourceFolder = ResourceFolder;

                if (!Filter.IsMatch(spawnPopulation.Filter))
                {
                    Filter.ApplyTo(spawnPopulation.Filter);

                    if (updateDistributions)
                        EnqueueDistributionUpdate();
                    else m_DistributionDirty = true;
                }
            }

            #region Distributions
            [JsonIgnore] 
            private ByteQuadtree m_ByteQuadTree;

            private bool m_DistributionDirty = false;
            
            [JsonIgnore] 
            private static System.Reflection.FieldInfo m_QuadTreeField = typeof(SpawnDistribution).GetField("quadtree", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
           
            [JsonIgnore] 
            private static byte[] m_BaseValues;

            private static Queue<SC_SpawnPopulation> m_DistributionUpdateQueue = new Queue<SC_SpawnPopulation>();

            private static bool m_QueueRunning = false;

            private static Coroutine m_DistributionUpdate;

            public static void OnUnload()
            {
                if (m_DistributionUpdate != null)
                {
                    Global.Runner.StopCoroutine(m_DistributionUpdate);
                    m_DistributionUpdate = null;
                }
                
                m_DistributionUpdateQueue.Clear();
            }
            
            private static IEnumerator ProcessDistributionQueue()
            {
                m_QueueRunning = true;
                
                while (m_DistributionUpdateQueue.Count > 0)
                {
                    SC_SpawnPopulation spawnPopulation = m_DistributionUpdateQueue.Dequeue();
                    
                    yield return Global.Runner.StartCoroutine(spawnPopulation.UpdateDistribution());
                    yield return null;
                }

                m_QueueRunning = false;
                m_DistributionUpdate = null;
            }

            private static bool IsQueuedForUpdate(SC_SpawnPopulation spawnPopulation) => m_DistributionUpdateQueue.Contains(spawnPopulation);
            
            public void EnqueueDistributionUpdate()
            {
                if (IsQueuedForUpdate(this))
                    return;
                
                m_DistributionUpdateQueue.Enqueue(this);

                m_DistributionDirty = false;
                
                if (!m_QueueRunning)
                    m_DistributionUpdate = Global.Runner.StartCoroutine(ProcessDistributionQueue());
            }

            private IEnumerator UpdateDistribution()
            {
                Debug.Log($"[SpawnControl] Starting update for distribution {Name}");
                if (m_ByteQuadTree == null)
                    m_ByteQuadTree = m_QuadTreeField.GetValue(Distribution) as ByteQuadtree;
                
                int size = Mathf.NextPowerOfTwo((int)((float)World.Size * 0.25f));
                if (m_BaseValues == null)
                    m_BaseValues = new byte[size * size];
                
                float total = 0f;
                Parallel.For(0, size, z =>
                {
                    for (int x = 0; x < size; x++)
                    {
                        float normX = ((float) x + 0.5f) / (float) size;
                        float normZ = ((float) z + 0.5f) / (float) size;
                        float factor = Reference.Filter.GetFactor(normX, normZ, true);
                        float v = factor >= Reference.FilterCutoff ? 255f * factor : 0f;

                        total += v;

                        m_BaseValues[z * size + x] = (byte) v;
                    }
                });
                
                m_ByteQuadTree.UpdateValues(m_BaseValues);
                Distribution.Density = total / (float)(255 * m_BaseValues.Length);

                Debug.Log($"[SpawnControl] Finished updating distribution {Name}");
                yield return null;
            }
            #endregion
            
            public void EnforceLimits()
            {
                int targetCount = TargetCount;
                Spawnable[] spawnables = SpawnHandler.Instance.FindAll(Reference);
                
                if (spawnables.Length <= targetCount)
                    return;

                foreach (Spawnable spawnable in spawnables.Take(spawnables.Length - targetCount))
                {
                    BaseEntity baseEntity = spawnable.gameObject.ToBaseEntity();
                    if (!baseEntity.IsValid())
                        GameManager.Destroy(spawnable.gameObject, 0f);
                    else baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                }
            }
            
            public void SpawnRepeating()
            {
                if (Reference == null)
                    return;
                
                int targetCount = TargetCount;
                int currentCount = CurrentCount;
                currentCount = Mathf.RoundToInt((float)currentCount * Reference.GetCurrentSpawnRate());
                currentCount = UnityEngine.Random.Range(Mathf.Min(currentCount, SpawnHandler.Instance.MinSpawnsPerTick), Mathf.Min(currentCount, SpawnHandler.Instance.MaxSpawnsPerTick));
                Fill(targetCount, currentCount, currentCount * Reference.SpawnAttemptsRepeating);
            }

            private void Fill(int targetCount, int numToFill, int numToTry)
            {
                if (targetCount == 0)
                    return;

                if (!Reference.Initialize())
                {
                    Debug.LogError(string.Concat("[SpawnControl] No prefabs to spawn in ", Reference.ResourceFolder), Reference);
                    return;
                }

                float clusterSize = Mathf.Max((float) Reference.ClusterSizeMax, Distribution.GetGridCellArea() * Reference.GetMaximumSpawnDensity());
                
                Reference.UpdateWeights(Distribution, targetCount);
                
                while (numToFill >= Reference.ClusterSizeMin && numToTry > 0)
                {
                    ByteQuadtree.Element element = Distribution.SampleNode();
                   
                    for (int i = 0; i < Mathx.Min(numToTry, numToFill, UnityEngine.Random.Range(Reference.ClusterSizeMin, Reference.ClusterSizeMax + 1)); i++)
                    {
                        Vector3 position;
                        Quaternion rotation;
                        
                        bool shouldSpawn = Distribution.Sample(out position, out rotation, element, Reference.AlignToNormal, (float) Reference.ClusterDithering) && 
                                           Reference.Filter.GetFactor(position, true) > 0f;
                        
                        if (shouldSpawn && Reference.FilterRadius > 0f)
                        {
                            shouldSpawn = !(Reference.Filter.GetFactor(position + (Vector3.forward * Reference.FilterRadius), true) <= 0f) && 
                                   !(Reference.Filter.GetFactor(position - (Vector3.forward * Reference.FilterRadius), true) <= 0f) && 
                                   !(Reference.Filter.GetFactor(position + (Vector3.right * Reference.FilterRadius), true) <= 0f) && 
                                     Reference.Filter.GetFactor(position - (Vector3.right * Reference.FilterRadius), true) > 0f;
                        }

                        Prefab<Spawnable> prefab;
                        if (shouldSpawn && Reference.TryTakeRandomPrefab(out prefab))
                        {
                            if (!Reference.GetSpawnPosOverride(prefab, ref position, ref rotation) || (float) Distribution.GetCount(position) >= clusterSize)
                            {
                                Reference.ReturnPrefab(prefab);
                            }
                            else
                            {
                                SpawnHandler.Instance.Spawn(Reference, prefab, position, rotation);
                                numToFill--;
                            }
                        }

                        numToTry--;
                    }
                }

                if (Reference is ConvarControlledSpawnPopulation)
                {
                    List<Prefab<Spawnable>> list = Facepunch.Pool.Get<List<Prefab<Spawnable>>>();
                    foreach (Prefab<Spawnable> prefab in Reference.Prefabs)
                    {
                        TrainCar component = prefab.Object.GetComponent<TrainCar>();
                        if (component != null && component.CarType == TrainCar.TrainCarType.Engine)
                        {
                            list.Add(prefab);
                        }
                    }
                    foreach (TrainTrackSpline trainTrackSpline in TrainTrackSpline.SidingSplines)
                    {
                        if (!trainTrackSpline.HasAnyUsersOfType(TrainCar.TrainCarType.Engine))
                        {
                            int num = UnityEngine.Random.Range(0, list.Count);
                            Prefab<Spawnable> prefab2 = Reference.Prefabs[num];
                            TrainCar component2 = prefab2.Object.GetComponent<TrainCar>();
                            if (!(component2 == null))
                            {
                                int j = 0;
                                while (j < 20)
                                {
                                    j++;
                                    Vector3 pos;
                                    Quaternion rot;
                                    if (TryGetRandomPointOnSpline(trainTrackSpline, component2, out pos, out rot))
                                    {
                                        SpawnHandler.Instance.Spawn(Reference, prefab2, pos, rot);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    Facepunch.Pool.FreeUnmanaged(ref list);
                }
            }
            
            private bool TryGetRandomPointOnSpline(TrainTrackSpline spline, TrainCar trainCar, out Vector3 pos, out Quaternion rot)
            {
                float length = spline.GetLength();
                if (length < 65f)
                {
                    pos = Vector3.zero;
                    rot = Quaternion.identity;
                    return false;
                }
                float distance = UnityEngine.Random.Range(60f, length - 60f);
                Vector3 forward;
                pos = spline.GetPointAndTangentCubicHermiteWorld(distance, out forward) + Vector3.up * 0.5f;
                rot = Quaternion.LookRotation(forward);
                float radius = trainCar.bounds.extents.Max();
                List<Collider> list = Facepunch.Pool.Get<List<Collider>>();
                GamePhysics.OverlapSphere(pos, radius, list, 32768, QueryTriggerInteraction.Ignore);
                bool result = true;
                foreach (Collider collider in list)
                {
                    if (!trainCar.ColliderIsPartOfTrain(collider))
                    {
                        result = false;
                        break;
                    }
                }
                Facepunch.Pool.FreeUnmanaged(ref list);
                return result;
            }

            public virtual void RestoreDefaults()
            {
                if (Reference != null && Defaults != null)
                {
                    Reference._targetDensity = Defaults.TargetDensity;
                    Reference.SpawnRate = Defaults.SpawnRate;
                    Reference.ClusterSizeMin = Defaults.ClusterSizeMin;
                    Reference.ClusterSizeMax = Defaults.ClusterSizeMax;
                    Reference.ClusterDithering = Defaults.ClusterDithering;
                    Reference.SpawnAttemptsInitial = Defaults.SpawnAttemptsInitial;
                    Reference.SpawnAttemptsRepeating = Defaults.SpawnAttemptsRepeating;
                    Reference.EnforcePopulationLimits = Defaults.EnforcePopulationLimits;
                    Reference.ScaleWithLargeMaps = Defaults.ScaleWithLargeMaps;
                    Reference.ScaleWithSpawnFilter = Defaults.ScaleWithSpawnFilter;
                    Reference.ScaleWithServerPopulation = Defaults.ScaleWithServerPopulation;

                    Reference.ResourceFolder = Defaults.ResourceFolder;

                    Defaults.Filter.ApplyTo(Reference.Filter);
                }
            }

            #region UI
            [JsonIgnore] 
            private bool m_IsDirty = false;
            
            public void CreateUI(BasePlayer player)
            {
                BaseContainer root = ChaosPrefab.Background(SCUI_OVERLAY, Layer.Overall, Anchor.FullStretch, Offset.zero)
                    .WithChildren(parent =>
                    {
                        CreateHeaderUI(player, parent);
                        
                        CreateSettingsUI(player, parent);

                        CreateSpawnFilterUI(player, parent);

                        CreateResourcesUI(player, parent);
                    })
                    .DestroyExisting()
                    .NeedsCursor()
                    .NeedsKeyboard();
                
                ChaosUI.Show(player, root);
            }

            private void CreateHeaderUI(BasePlayer player, BaseContainer parent)
            {
                ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                    .WithChildren(header =>
                    {
                        SpawnDistribution spawnDistribution = SpawnHandler.Instance.population2distribution[Reference];
                        
                        TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                            .WithText(string.Format(TranslatedString("Label.SpawnPopulation", player), 
                                Reference.name, CurrentCount, TargetCount))
                            .WithAlignment(TextAnchor.MiddleCenter);
                        
                        ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(0f, -10f, 100f, 10f), 
                                TranslatedString("Button.FillPopulation", player), ChaosStyle.Header)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                SpawnRepeating();
                                CreateUI(player);
                            }, $"{player.UserIDString}.fillpopulation");
                        
                        ChaosPrefab.TextButton(header, Anchor.CenterLeft, new Offset(105f, -9f, 205f, 9f), 
                                TranslatedString("Button.EnforceLimits", player), ChaosStyle.Button, ChaosStyle.RedOutline)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                EnforceLimits();
                                CreateUI(player);
                            }, $"{player.UserIDString}.enforcelimit");

                        bool requiresDistributionUpdate = IsQueuedForUpdate(this) || !Filter.IsMatch(Reference.Filter) || m_DistributionDirty;
                        
                        ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-150f, -9f, -25f, 9f), 
                                TranslatedString("Button.UpdateDistribution", player), 
                                requiresDistributionUpdate ? ChaosStyle.Button : ChaosStyle.DisabledButton, 
                                requiresDistributionUpdate ? ChaosStyle.BlueOutline : null)
                            .WithCallback(m_CallbackHandler, arg => CreateUpdateDistributionPopup(player), $"{player.UserIDString}.updatedistribution");
                        
                        ChaosPrefab.CloseButton(header, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f), ChaosStyle.RedOutline)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                if (m_IsDirty)
                                {
                                    if (Reference is ConvarControlledSpawnPopulation)
                                        m_ConVarSpawnPopulations.Save();
                                    else m_SpawnPopulations.Save();
                                    
                                    m_IsDirty = false;
                                }

                                ChaosUI.Destroy(player, SCUI_POPUP);
                                ChaosUI.Destroy(player, SCUI_OVERLAY);
                            }, $"{player.UserIDString}.spawnpop.exit");
                    });
            }

            private void CreateUpdateDistributionPopup(BasePlayer player)
            {
                BaseContainer root = ChaosPrefab.Background(SCUI_POPUP, Layer.Overall, Anchor.FullStretch, Offset.zero)
                    .WithChildren(parent =>
                    {
                        ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-300f, 100f, 300f, 130f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.UpdateDistribution.Confirm", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                            });

                        ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-300f, -95f, 300f, 95f))
                            .WithChildren(label =>
                            {
                                TextContainer.Create(label, Anchor.TopStretch, new Offset(5f, -45f, -5f, -5f))
                                    .WithText(TranslatedString("Notification.UpdateDistribution.Confirm1", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(label, Anchor.TopStretch, new Offset(5f, -73.9f, -5f, -53.9f))
                                    .WithText(TranslatedString("Notification.UpdateDistribution.Confirm2", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(label, Anchor.TopStretch, new Offset(5f, -113.9f, -5f, -73.9f))
                                    .WithText(TranslatedString("Notification.UpdateDistribution.Confirm3", player));

                                TextContainer.Create(label, Anchor.TopStretch, new Offset(5f, -155f, -5f, -115f))
                                    .WithText(TranslatedString("Notification.UpdateDistribution.Confirm4", player));

                                ChaosPrefab.TextButton(label, Anchor.BottomLeft, new Offset(5f, 5f, 155f, 25f), TranslatedString("Button.Confirm", player), null, ChaosStyle.GreenOutline)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        ChaosUI.Destroy(player, SCUI_POPUP);
                                        EnqueueDistributionUpdate();
                                        CreateUI(player);
                                        CreatePopupMessage(player, TranslatedString("Notification.UpdateDistribution", player));
                                    }, $"{player.UserIDString}.updatedistribution.confirm");

                                ChaosPrefab.TextButton(label, Anchor.BottomRight, new Offset(-155f, 5f, -5f, 25f), TranslatedString("Button.Cancel", player), null, ChaosStyle.RedOutline)
                                    .WithCallback(m_CallbackHandler, arg => ChaosUI.Destroy(player, SCUI_POPUP), $"{player.UserIDString}.updatedistribution.cancel");
                            });
                    });

                ChaosUI.Show(player, root);
            }

            #region Settings
            private void CreateSettingsUI(BasePlayer player, BaseContainer parent)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-302.5f, 25f, -2.5f, 330f))
                    .WithChildren(fields =>
                    {
                        ChaosPrefab.Panel(fields, Anchor.Center, new Offset(-150f, 132.5f, 150f, 152.5f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.Settings", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                                
                                ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), 
                                        TranslatedString("Button.Reset", player), null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        SetDefaultSettings();
                                        CreateUI(player);
                                    }, $"{player.UserIDString}.reset.settings");
                            });

                        ChaosPrefab.Panel(fields, Anchor.Center, new Offset(-150f, -152.5f, 150f, 127.5f))
                            .WithChildren(options =>
                            {
                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, 115f, 145f, 135f))
                                    .WithChildren(template =>
                                    {
                                        CreateInputField(player, template, "Label.TargetDensity", TargetDensity.ToString("N2"), arg =>
                                        {
                                            TargetDensity = Reference._targetDensity = arg.GetFloat(1);
                                            if (this is SC_ConvarControlledSpawnPopulation)
                                                (this as SC_ConvarControlledSpawnPopulation).Command.Set(TargetDensity);
                                        });
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, 90f, 145f, 110f))
                                    .WithChildren(spawnRate =>
                                    {
                                        CreateInputField(player, spawnRate, "Label.SpawnRate", SpawnRate.ToString("N2"), arg =>
                                            SpawnRate = Reference.SpawnRate = arg.GetFloat(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, 65f, 145f, 85f))
                                    .WithChildren(clusterSizeMin =>
                                    {
                                        CreateInputField(player, clusterSizeMin, "Label.ClusterSizeMin", ClusterSizeMin.ToString(), arg =>
                                            ClusterSizeMin = Reference.ClusterSizeMin = arg.GetInt(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, 40f, 145f, 60f))
                                    .WithChildren(clusterSizeMax =>
                                    {
                                        CreateInputField(player, clusterSizeMax, "Label.ClusterSizeMax", ClusterSizeMax.ToString(), arg =>
                                            ClusterSizeMax = Reference.ClusterSizeMax = arg.GetInt(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, 15f, 145f, 35f))
                                    .WithChildren(clusterDithering =>
                                    {
                                        CreateInputField(player, clusterDithering, "Label.ClusterDithering", ClusterDithering.ToString(), arg =>
                                            ClusterDithering = Reference.ClusterDithering = arg.GetInt(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -10f, 145f, 10f))
                                    .WithChildren(spawnAttemptsInitial =>
                                    {
                                        CreateInputField(player, spawnAttemptsInitial, "Label.SpawnAttemptsInitial", SpawnAttemptsInitial.ToString(), arg =>
                                            SpawnAttemptsInitial = Reference.SpawnAttemptsInitial = arg.GetInt(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -35f, 145f, -15f))
                                    .WithChildren(spawnAttemptsRepeating =>
                                    {
                                        CreateInputField(player, spawnAttemptsRepeating, "Label.SpawnAttemptsRepeating", SpawnAttemptsRepeating.ToString(), arg =>
                                            SpawnAttemptsRepeating = Reference.SpawnAttemptsRepeating = arg.GetInt(1));
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -60f, 145f, -40f))
                                    .WithChildren(enforcePopulationLimits =>
                                    {
                                        CreateToggleField(player, enforcePopulationLimits, "Label.EnforcePopulationLimits", EnforcePopulationLimits, () =>
                                            EnforcePopulationLimits = Reference.EnforcePopulationLimits = !EnforcePopulationLimits);
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -85f, 145f, -65f))
                                    .WithChildren(scaleWithSpawnFilter =>
                                    {
                                        CreateToggleField(player, scaleWithSpawnFilter, "Label.ScaleWithSpawnFilter", ScaleWithSpawnFilter, () =>
                                            ScaleWithSpawnFilter = Reference.ScaleWithSpawnFilter = !ScaleWithSpawnFilter);
                                    });

                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -110f, 145f, -90f))
                                    .WithChildren(scaleWithServerPopulation =>
                                    {
                                        CreateToggleField(player, scaleWithServerPopulation, "Label.ScaleWithServerPopulation", ScaleWithServerPopulation, () =>
                                            ScaleWithServerPopulation = Reference.ScaleWithServerPopulation = !ScaleWithServerPopulation);
                                    });
                                
                                ChaosPrefab.Panel(options, Anchor.Center, new Offset(-145f, -135f, 145f, -115f))
                                    .WithChildren(scaleWithLargeMaps =>
                                    {
                                        CreateToggleField(player, scaleWithLargeMaps, "Label.ScaleWithLargeMaps", ScaleWithLargeMaps, () =>
                                            ScaleWithLargeMaps = Reference.ScaleWithLargeMaps = !ScaleWithLargeMaps);
                                    });
                            });
                    });
            }
            
            private void CreateInputField(BasePlayer player, BaseContainer parent, string key, string value, Action<ConsoleSystem.Arg> callback)
            {
                TextContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 0f, -100f, 0f))
                    .WithText(TranslatedString(key, player))
                    .WithAlignment(TextAnchor.MiddleLeft);

                ChaosPrefab.Input(parent, Anchor.CenterRight, new Offset(-100f, -10f, 0f, 10f), value)
                    .WithCallback(m_CallbackHandler, arg =>
                    {
                        callback(arg);
                        
                        m_IsDirty = true;
                        CreateUI(player);
                    }, $"{player.UserIDString}.{key}");
            }

            private void CreateToggleField(BasePlayer player, BaseContainer parent, string key, bool value, Action callback)
            {
                TextContainer.Create(parent, Anchor.HoriztonalCenterStretch, new Offset(5f, -10f, -20f, 10f))
                    .WithText(TranslatedString(key, player))
                    .WithAlignment(TextAnchor.MiddleLeft);

                ChaosPrefab.Toggle(parent, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f), value)
                    .WithCallback(m_CallbackHandler, arg =>
                    {
                        callback();
                        
                        m_IsDirty = true;
                        CreateUI(player);
                    }, $"{player.UserIDString}.{key}");
            }
            #endregion
            
            #region Spawn Filter

            private static readonly VerticalLayoutGroup m_SplatLayout = new VerticalLayoutGroup
            {
                Area = new Area(-55f, -102.5f, 55f, 102.5f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(100, 20),
            };

            private static readonly VerticalLayoutGroup m_BiomeLayout = new VerticalLayoutGroup
            {
                Area = new Area(-55f, -52.5f, 55f, 52.5f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(100, 20),
            };
            
            private static readonly GridLayoutGroup m_TopologyLayout = new GridLayoutGroup(Axis.Horizontal)
            {
                Area = new Area(-150f, -140f, 150f, 140f),
                Spacing = new Spacing(5f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(93.33f, 20),
                FixedCount = new Vector2Int(3, 16),
            };

            private static readonly TerrainSplat.Enum[] m_SplatFlags = (TerrainSplat.Enum[])Enum.GetValues(typeof(TerrainSplat.Enum));
            private static readonly TerrainBiome.Enum[] m_BiomeFlags = (TerrainBiome.Enum[])Enum.GetValues(typeof(TerrainBiome.Enum));
            private static readonly TerrainTopology.Enum[] m_TopologyFlags = (TerrainTopology.Enum[])Enum.GetValues(typeof(TerrainTopology.Enum));
            
            private void CreateSpawnFilterUI(BasePlayer player, BaseContainer parent)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(-575f, -355f, 575f, 20f))
                    .WithChildren(spawnFilter =>
                    {
                        ChaosPrefab.Panel(spawnFilter, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.SpawnFilter", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ChaosPrefab.TextButton(header, Anchor.CenterRight, new Offset(-50f, -10f, 0f, 10f), 
                                        TranslatedString("Button.Reset", player), null)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        SetDefaultFilter();
                                        CreateUI(player);
                                    }, $"{player.UserIDString}.reset.filter");
                            });

                        ChaosPrefab.Panel(spawnFilter, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 350f))
                            .WithChildren(filters =>
                            {
                                BaseContainer.Create(filters, Anchor.CenterLeft, new Offset(5f, -60f, 115f, 170f))
                                    .WithChildren(splatFilter =>
                                    {
                                        ImageContainer.Create(splatFilter, Anchor.TopCenter, new Offset(-55f, -20f, 55f, 0f))
                                            .WithStyle(ChaosStyle.Header)
                                            .WithChildren(header =>
                                            {
                                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                                    .WithText(TranslatedString("Label.SplatFilter", player))
                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                            });

                                        ChaosPrefab.Panel(splatFilter, Anchor.BottomCenter, new Offset(-55f, 0f, 55f, 205f))
                                            .WithLayoutGroup(m_SplatLayout, m_SplatFlags, 0, (int i, TerrainSplat.Enum t, BaseContainer options, Anchor anchor, Offset offset) =>
                                            {
                                                bool hasFlag = (Filter.SplatType & (int)t) == (int)t;

                                                ChaosPrefab.TextButton(options, anchor, offset, t.ToString(), null, hasFlag ? ChaosStyle.GreenOutline : null)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (hasFlag)
                                                            Filter.SplatType &= ~(int)t;
                                                        else Filter.SplatType |= (int)t;

                                                        UpdateReferencedComponents();
                                                        m_IsDirty = true;
                                                        CreateUI(player);
                                                    }, $"{player.UserIDString}.splat.{i}");
                                            });
                                    });

                                BaseContainer.Create(filters, Anchor.CenterLeft, new Offset(120f, 40f, 230f, 170f))
                                    .WithChildren(biomeFilter =>
                                    {
                                        ImageContainer.Create(biomeFilter, Anchor.TopCenter, new Offset(-55f, -20f, 55f, 0f))
                                            .WithStyle(ChaosStyle.Header)
                                            .WithChildren(header =>
                                            {
                                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                                    .WithText(TranslatedString("Label.BiomeFilter", player))
                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                            });

                                        ChaosPrefab.Panel(biomeFilter, Anchor.BottomCenter, new Offset(-55f, 0f, 55f, 105f))
                                            .WithLayoutGroup(m_BiomeLayout, m_BiomeFlags, 0, (int i, TerrainBiome.Enum t, BaseContainer options, Anchor anchor, Offset offset) =>
                                            {
                                                bool hasFlag = (Filter.BiomeType & (int)t) == (int)t;

                                                ChaosPrefab.TextButton(options, anchor, offset, t.ToString(), null, hasFlag ? ChaosStyle.GreenOutline : null)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (hasFlag)
                                                            Filter.BiomeType &= ~(int)t;
                                                        else Filter.BiomeType |= (int)t;

                                                        UpdateReferencedComponents();
                                                        m_IsDirty = true;
                                                        CreateUI(player);
                                                    }, $"{player.UserIDString}.biome.{i}");
                                            });

                                    });

                                BaseContainer.Create(filters, Anchor.CenterRight, new Offset(-915f, -135f, -615f, 170f))
                                    .WithChildren(topologyAnyFilter =>
                                    {
                                        ImageContainer.Create(topologyAnyFilter, Anchor.TopCenter, new Offset(-150f, -20f, 150f, 0f))
                                            .WithStyle(ChaosStyle.Header)
                                            .WithChildren(header =>
                                            {
                                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                                    .WithText(TranslatedString("Label.TopologyAnyFilter", player))
                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                            });

                                        ChaosPrefab.Panel(topologyAnyFilter, Anchor.BottomCenter, new Offset(-150f, 0f, 150f, 280f))
                                            .WithLayoutGroup(m_TopologyLayout, m_TopologyFlags, 0, (int i, TerrainTopology.Enum t, BaseContainer options, Anchor anchor, Offset offset) =>
                                            {
                                                bool hasFlag = (Filter.TopologyAny & (int)t) == (int)t;
                                                
                                                ChaosPrefab.TextButton(options, anchor, offset, t.ToString(), null, hasFlag ? ChaosStyle.GreenOutline : null)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (hasFlag)
                                                            Filter.TopologyAny &= ~(int)t;
                                                        else Filter.TopologyAny |= (int)t;

                                                        UpdateReferencedComponents();
                                                        m_IsDirty = true;
                                                        CreateUI(player);
                                                    }, $"{player.UserIDString}.topologyany.{i}");
                                            });
                                    });

                                BaseContainer.Create(filters, Anchor.CenterRight, new Offset(-610f, -135f, -310f, 170f))
                                    .WithChildren(topologyAllFilter =>
                                    {
                                        ImageContainer.Create(topologyAllFilter, Anchor.TopCenter, new Offset(-150f, -20f, 150f, 0f))
                                            .WithStyle(ChaosStyle.Header)
                                            .WithChildren(header =>
                                            {
                                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                                    .WithText(TranslatedString("Label.TopologyAllFilter", player))
                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                            });

                                        ChaosPrefab.Panel(topologyAllFilter, Anchor.BottomCenter, new Offset(-150f, 0f, 150f, 280f))
                                            .WithLayoutGroup(m_TopologyLayout, m_TopologyFlags, 0, (int i, TerrainTopology.Enum t, BaseContainer options, Anchor anchor, Offset offset) =>
                                            {
                                                bool hasFlag = (Filter.TopologyAll & (int)t) == (int)t;
                                                
                                                ChaosPrefab.TextButton(options, anchor, offset, t.ToString(), null, hasFlag ? ChaosStyle.GreenOutline : null)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (hasFlag)
                                                            Filter.TopologyAll &= ~(int)t;
                                                        else Filter.TopologyAll |= (int)t;

                                                        UpdateReferencedComponents();
                                                        m_IsDirty = true;
                                                        CreateUI(player);
                                                    }, $"{player.UserIDString}.topologyall.{i}");
                                            });

                                    });

                                BaseContainer.Create(filters, Anchor.CenterRight, new Offset(-305f, -135f, -5f, 170f))
                                    .WithChildren(topologyNotFilter =>
                                    {
                                        ImageContainer.Create(topologyNotFilter, Anchor.TopCenter, new Offset(-150f, -20f, 150f, 0f))
                                            .WithStyle(ChaosStyle.Header)
                                            .WithChildren(header =>
                                            {
                                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                                    .WithText(TranslatedString("Label.TopologyNotFilter", player))
                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                            });

                                        ChaosPrefab.Panel(topologyNotFilter, Anchor.BottomCenter, new Offset(-150f, 0f, 150f, 280f))
                                            .WithLayoutGroup(m_TopologyLayout, m_TopologyFlags, 0, (int i, TerrainTopology.Enum t, BaseContainer options, Anchor anchor, Offset offset) =>
                                            {
                                                bool hasFlag = (Filter.TopologyNot & (int)t) == (int)t;
                                                
                                                ChaosPrefab.TextButton(options, anchor, offset, t.ToString(), null, hasFlag ? ChaosStyle.GreenOutline : null)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (hasFlag)
                                                            Filter.TopologyNot &= ~(int)t;
                                                        else Filter.TopologyNot |= (int)t;

                                                        UpdateReferencedComponents();
                                                        m_IsDirty = true;
                                                        CreateUI(player);
                                                    }, $"{player.UserIDString}.topologynot.{i}");
                                            });
                                    });

                                TextContainer.Create(filters, Anchor.Center, new Offset(-570f, -155f, -220f, -140f))
                                    .WithSize(12)
                                    .WithText(TranslatedString("Label.SplatHelp", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(filters, Anchor.Center, new Offset(-570f, -170f, -220f, -155f))
                                    .WithSize(12)
                                    .WithText(TranslatedString("Label.BiomeHelp", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(filters, Anchor.Center, new Offset(-175f, -155f, 175f, -140f))
                                    .WithSize(12)
                                    .WithText(TranslatedString("Label.TopologyAnyHelp", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(filters, Anchor.Center, new Offset(-175f, -170f, 175f, -155f))
                                    .WithSize(12)
                                    .WithText(TranslatedString("Label.TopologyAllHelp", player))
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                TextContainer.Create(filters, Anchor.Center, new Offset(220f, -155f, 570f, -140f))
                                    .WithSize(12)
                                    .WithText(TranslatedString("Label.TopologyNotHelp", player))
                                    .WithAlignment(TextAnchor.MiddleRight);

                            });
                    });
            }
            #endregion
            
            #region Resources

            private static readonly VerticalLayoutGroup m_ResourcesLayout = new VerticalLayoutGroup()
            {
                Area = new Area(-286.25f, -140f, 286.25f, 140f),
                Spacing = new Spacing(0f, 5f),
                Padding = new Padding(5f, 5f, 5f, 5f),
                Corner = Corner.TopLeft,
                FixedSize = new Vector2(562.5f, 20),
            };

            private void CreateResourcesUI(BasePlayer player, BaseContainer parent)
            {
                BaseContainer.Create(parent, Anchor.Center, new Offset(2.5f, 25f, 575f, 330f))
                    .WithChildren(resources =>
                    {
                        ChaosPrefab.Panel(resources, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                    .WithText(TranslatedString("Label.Resources", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);
                            });

                        ChaosPrefab.Panel(resources, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 280f))
                            .WithLayoutGroup(m_ResourcesLayout, ResourceList, 0, (int i, string t, BaseContainer options, Anchor anchor, Offset offset) =>
                            {
                                ChaosPrefab.Panel(options, anchor, offset)
                                    .WithChildren(template =>
                                    {
                                        TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                            .WithText(t)
                                            .WithAlignment(TextAnchor.MiddleLeft)
                                            .WithSize(13);
                                    });
                            });
                    });
            }
            #endregion
            #endregion
        }
        
        [Serializable]
        public class SC_SpawnFilter
        {
            public int SplatType;

            public int BiomeType;

            public int TopologyAny;

            public int TopologyAll;

            public int TopologyNot;

            public SC_SpawnFilter() { }

            public SC_SpawnFilter(SpawnFilter spawnFilter)
            {
                SplatType = (int)spawnFilter.SplatType;
                BiomeType = (int)spawnFilter.BiomeType;
                TopologyAny = (int)spawnFilter.TopologyAny;
                TopologyAll = (int)spawnFilter.TopologyAll;
                TopologyNot = (int)spawnFilter.TopologyNot;
            }

            public void ApplyTo(SpawnFilter spawnFilter)
            {
                spawnFilter.SplatType = (TerrainSplat.Enum)SplatType;
                spawnFilter.BiomeType = (TerrainBiome.Enum)BiomeType;
                spawnFilter.TopologyAny = (TerrainTopology.Enum)TopologyAny;
                spawnFilter.TopologyAll = (TerrainTopology.Enum)TopologyAll;
                spawnFilter.TopologyNot = (TerrainTopology.Enum)TopologyNot;
            }

            public bool IsMatch(SpawnFilter spawnFilter)
            {
                return ((int)spawnFilter.SplatType == SplatType) &&
                       ((int)spawnFilter.BiomeType == BiomeType) &&
                       ((int)spawnFilter.TopologyAll == TopologyAll) &&
                       ((int)spawnFilter.TopologyAny == TopologyAny) &&
                       ((int)spawnFilter.TopologyNot == TopologyNot);
            }
        }

        #endregion

        #region Localization

        protected override void PopulatePhrases()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Label.Settings"] = "Settings",
                ["Label.TargetDensity"] = "Target Density",
                ["Label.SpawnRate"] = "Spawn Rate",
                ["Label.ClusterSizeMin"] = "Cluster Size Min",
                ["Label.ClusterSizeMax"] = "Cluster Size Max",
                ["Label.ClusterDithering"] = "Cluster Dithering",
                ["Label.SpawnAttemptsInitial"] = "Spawn Attempts Initial",
                ["Label.SpawnAttemptsRepeating"] = "Spawn Attempts Repeating",
                ["Label.EnforcePopulationLimits"] = "Enforce Population Limits",
                ["Label.ScaleWithSpawnFilter"] = "Scale With Spawn Filter",
                ["Label.ScaleWithServerPopulation"] = "Scale With Server Population",
                ["Label.ScaleWithLargeMaps"] = "Scale With Large Maps",
                ["Label.SplatHelp"] = "* Splat - Will only spawn on any the selected splats",
                ["Label.BiomeHelp"] = "* Biome - Will only spawn on any the selected biomes",
                ["Label.TopologyAnyHelp"] = "* Topology Any - Will only spawn on any of the selected topologies",
                ["Label.TopologyAllHelp"] = "* Topology All - Will only spawn on the selected topologies",
                ["Label.TopologyNotHelp"] = "* Topology Not - Will not spawn on any of the selected topologies",
                ["Label.SpawnFilter"] = "Spawn Filter",
                ["Label.SplatFilter"] = "Splat Filter",
                ["Label.BiomeFilter"] = "Biome Filter",
                ["Label.TopologyAnyFilter"] = "Topology Any Filter",
                ["Label.TopologyAllFilter"] = "Topology All Filter",
                ["Label.TopologyNotFilter"] = "Topology Not Filter",
                ["Label.Resources"] = "Resources",
                ["Label.SpawnPopulation"] = "Spawn Population : {0} ({1} / {2} spawned)",
                ["Label.SpawnGroups"] = "Spawn Groups",
                ["Label.Monument"] = "Monument : {0}",
                ["Label.IndividualSpawners"] = "Individual Spawners",
                ["Label.MaxPopulation"] = "Max Population",
                ["Label.NumToSpawnperTickMin"] = "Number To Spawn Per Tick Min",
                ["Label.NumToSpawnperTickMax"] = "Number To Spawn Per Tick Max",
                ["Label.RespawnDelayMin"] = "Respawn Delay Min",
                ["Label.RespawnDelayMax"] = "Respawn Delay Max",
                ["Label.LocalSpawnPoints"] = "Local Spawn Points",
                ["Label.TeleportTo"] = "Teleport To",
                ["Label.Prefabs"] = "Prefabs",
                ["Label.Prefab"] = "Prefab",
                ["Label.AddPrefab"] = "Add Prefab",
                ["Label.Weight"] = "Weight",
                ["Label.PrefabToSpawn"] = "Prefab To Spawn",
                ["Label.UseBoundsCheckMask"] = "Use Bounds Check Mask",
                ["Label.NoReferencedGroups"] = "Unable to find positions. This prefab is not on the map",
                ["Label.Group.References"] = "'{0}' - {1} instance(s) on map ({2} / {3} spawned)",
                ["Label.Name"] = "Name",
                ["Label.Limits"] = "Limits",
                ["Label.Current"] = "Current",
                ["Button.MonumentSpawnGroups"] = "Monuments",
                ["Button.SpawnPopulations"] = "Spawn Populations",
                ["Button.ConVarSpawnPopulations"] = "Convar Populations",
                ["Button.Reset"] = "Reset",
                ["Button.Teleport"] = "Teleport To",
                ["Button.ApplyChanges"] = "Apply Changes",
                ["Button.ApplyChanges.Exit"] = "Apply & Exit",
                ["Button.NoChanges.Exit"] = "Exit",
                ["Button.FillPopulation"] = "Fill Population",
                ["Button.EnforceLimits"] = "Enforce Limits",
                ["Button.SpawnGroup.Fill"] = "Fill Groups",
                ["Button.SpawnGroup.Clear"] = "Clear Groups",
                ["Button.IndividualSpawner.Fill"] = "Fill Spawners",
                ["Button.IndividualSpawner.Clear"] = "Clear Spawners",
                ["Button.Report"] = "Spawn Report",
                ["Button.UpdateDistribution"] = "Update Distribution",
                ["Button.Confirm"] = "Confirm",
                ["Button.Cancel"] = "Cancel",
                ["Notification.UpdateDistribution"] = "The spawn distribution has been queued for an update",
                ["Label.UpdateDistribution.Confirm"] = "Are you sure you want to update this spawn distribution?",
                ["Notification.UpdateDistribution.Confirm1"] = "Spawn distributions are responsible for generating placement positions on the map based on the values in the spawn filter.",
                ["Notification.UpdateDistribution.Confirm2"] = "Normally these values are applied during the server startup process where it is un-noticeable.",
                ["Notification.UpdateDistribution.Confirm3"] = "Recalcuating these values during server operation is a costly process (100-150ms on a large map) and will cause a brief lag spike.",
                ["Notification.UpdateDistribution.Confirm4"] = "You can either force the update now, or your custom spawn filter values will be applied during the servers next start-up cycle",
            }, this);
        }

        #endregion
    }
}
