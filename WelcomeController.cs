using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core.Libraries;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("WelcomeController", "Amino", "2.0.8")]
    [Description("An advanced info panel system")]
    public class WelcomeController : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary, ServerRewards, Economics;

        #region [ CONFIG ]
        public class Configuration
        {
            [JsonProperty(PropertyName = "Display UI on server join")]
            public bool DisplayOnJoin { get; set; } = true;
            [JsonProperty(PropertyName = "Use social link display ( Requires Welcome UI Additions )")]
            public bool UseLinks = false;
            public string AddonInfo { get; set; } = "Accepted plugins (KitController, LoadoutController, ServersController, SkinController, ShopController, StatsController, WUIAttachments)";
            public List<InfoPanelSettings> UIPanels { get; set; } = new List<InfoPanelSettings>();
            public KeyValuePair<string, ThemeSettings> CurrentTheme { get; set; } = new KeyValuePair<string, ThemeSettings>();
            public Dictionary<string, List<DefaultColors>> ThemeColors { get; set; } = new Dictionary<string, List<DefaultColors>>();
            public List<string> CustomColors { get; set; } = new List<string>();
            public Dictionary<string, ThemeSettings> CustomThemes { get; set; } = new Dictionary<string, ThemeSettings>();
            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    UIPanels = new List<InfoPanelSettings>()
                    {
                        new InfoPanelSettings()
                        {
                            Commands = { "info", "welcome" },
                            ButtonName = "INFO",
                            Enabled = true,
                            PagePosition = 0,
                            PanelName = "INFO",
                            PanelSettings = new PanelSettings()
                            {
                                PanelPages = new List<List<string>>
                                {
                                    new List<string>
                                    {
                                        "[color=#5cffa8][size=35]RUST ADMIN ACADEMY[/size][/color]",
                                        "[size=30]WIPE SCHEDULE: [color=#5cffa8]Monday's and Friday's[/color] @ [color=#5cffa8]4PM[/color] (ET)[/size]",
                                        "",
                                        "[color=#5cffa8][size=25]SERVER INFO:[/size][/color]",
                                        "[size=20]- 10x Gather Rates",
                                        "- NoBPs",
                                        "- Mazes",
                                        "- Custom UI",
                                        "- Instant Barrels[/size]",
                                        "",
                                        "[color=#5cffa8][size=25]LINKS:[/size][/color]",
                                        "[size=20]DISCORD: [color=#5cffa8]discord.srtbull.com[/color]",
                                        "STORE: [color=#5cffa8]store.yourstore.com[/color]",
                                        "LINKING: [color=#5cffa8]link.yoursite.com[/color][/size]"
                                    }
                                }
                            }
                        },
                        new InfoPanelSettings()
                        {
                            Commands = { "rules" },
                            ButtonName = "RULES",
                            Enabled = true,
                            PagePosition = 1,
                            PanelName = "RULES",
                            PanelSettings = new PanelSettings()
                            {
                                PanelPages = new List<List<string>>
                                {
                                    new List<string>
                                    {
                                        "[color=#5cffa8][size=35>]RULES[/size][/color]",
                                        "- Rule 1",
                                        "- Rule 2"
                                    }
                                }
                            }
                        },
                        new InfoPanelSettings()
                        {
                            Commands = { "kits" },
                            ButtonName = "KITS",
                            Enabled = true,
                            PagePosition = 2,
                            PanelName = "KITS",
                            AddonSettings = new AddonSettings()
                            {
                                AddonName = "KitController"
                            } 
                        },
                        new InfoPanelSettings()
                        {
                            Commands = { "shop" },
                            ButtonName = "SHOP",
                            Enabled = true,
                            PagePosition = 2,
                            PanelName = "SHOP",
                            AddonSettings = new AddonSettings()
                            {
                                AddonName = "ShopController"
                            }
                        }
                    }
                };
            }
        }

        public class PanelSettings
        {
            [JsonProperty(Order = 0, PropertyName = "Panel Image")]
            public string PanelImage { get; set; } = String.Empty;
            [JsonProperty(Order = 0, PropertyName = "Text Panel Image (Covers entire text panel)")]
            public string TextPanelImage { get; set; } = String.Empty;
            [JsonProperty(Order = 0, PropertyName = "Panel Pages")]
            public List<List<string>> PanelPages = new List<List<string>>();

            public PanelSettings Clone()
            {
                return new PanelSettings
                {
                    TextPanelImage = this.TextPanelImage,
                    PanelImage = this.PanelImage,
                    PanelPages = this.PanelPages.Select(page => new List<string>(page)).ToList()
                };
            }
        }

        public class InfoPanelSettings
        {
            public bool Enabled { get; set; } = true;
            [JsonProperty(Order = 0, PropertyName = "Panel Name")]
            public string PanelName { get; set; }
            [JsonProperty(Order = 0, PropertyName = "Button Name")]
            public string ButtonName { get; set; }
            [JsonProperty(Order = 0, PropertyName = "Panel Commands")]
            public List<string> Commands = new List<string>();
            [JsonProperty(Order = 0, PropertyName = "Panel Permission")]
            public string PanelPermission { get; set; } = String.Empty;
            [JsonProperty(Order = 0, PropertyName = "Show No Permission Panel")]
            public bool ShowPanelPermission { get; set; } = true;
            [JsonProperty(Order = 0, PropertyName = "Panel No Permission Text")]
            public string PanelNoPermissionText { get; set; } = "You do not have permission to use this page!";
            [JsonProperty(Order = 0, PropertyName = "Button Image (Leave blank for no image)")]
            public string ButtonImage { get; set; } = String.Empty;
            [JsonProperty(Order = 0, PropertyName = "Display button name (Button stays there, but the text gets removed)")]
            public bool DisplayButtonName { get; set; } = true;
            [JsonProperty(Order = 0, PropertyName = "Page Position")]
            public int PagePosition { get; set; }
            [JsonProperty(Order = 0, PropertyName = "Info Panel Options (Won't be used if you're using an addon)")]
            public PanelSettings PanelSettings { get; set; } = new PanelSettings();
            public AddonSettings AddonSettings { get; set; } = new AddonSettings();

            public InfoPanelSettings Clone()
            {
                return new InfoPanelSettings
                {
                    Enabled = this.Enabled,
                    PanelName = this.PanelName,
                    ButtonName = this.ButtonName,
                    Commands = new List<string>(this.Commands),
                    PanelPermission = this.PanelPermission,
                    ShowPanelPermission = this.ShowPanelPermission,
                    PanelNoPermissionText = this.PanelNoPermissionText,
                    ButtonImage = this.ButtonImage,
                    DisplayButtonName = this.DisplayButtonName,
                    PagePosition = this.PagePosition,
                    PanelSettings = this.PanelSettings?.Clone(),
                    AddonSettings = this.AddonSettings?.Clone()
                };
            }
        }

        public class AddonSettings
        {
            [JsonProperty(Order = 0, PropertyName = "Addon Name (Empty = Not an addon)")]
            public string AddonName { get; set; } = string.Empty;

            public AddonSettings Clone()
            {
                return new AddonSettings
                {
                    AddonName = this.AddonName
                };
            }
        }

        public class DefaultColors
        {
            public string Name { get; set; } = string.Empty;
            public string Color { get; set; } = string.Empty;
        }

        private static Configuration _config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();

                foreach (var panel in _config.UIPanels) if (string.IsNullOrEmpty(panel.ButtonName)) panel.ButtonName = panel.PanelName;

                if (_config.CurrentTheme.Key == null || _config.CurrentTheme.Value == null || _config.CurrentTheme.Value.UIParts == null || _config.CurrentTheme.Value.UIParts.Count == 0)
                {
                    GetUIFromAPI("original", () =>
                    {
                        SaveConfig();
                    });
                }
                else
                {
                    foreach (var themeColor in compareThemeColors)
                    {
                        if (!_config.CurrentTheme.Value.UIColors.Any(x => x.Name == themeColor.Name))
                        {
                            _config.CurrentTheme.Value.UIColors.Add(themeColor);
                        }
                    }
                    SaveConfig();
                }
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

        #region [ UI DATA STRUCTURE ]
        List<string> _fonts = new List<string>()
        {
            "droidsansmono.ttf",
            "permanentmarker.ttf",
            "robotocondensed-bold.ttf",
            "robotocondensed-regular.ttf"
        };
        public Dictionary<ulong, DateTime> _cooldowns = new Dictionary<ulong, DateTime>();

        public class StoredUIList
        {
            public string UIName { get; set; } = string.Empty;
            public string UIPath { get; set; } = string.Empty;
            public string UICommandPath { get; set; } = string.Empty;
            public int SpaceNumber { get; set; } = 0;
            public bool HasChildren { get; set; } = false;
            public bool IsOpen { get; set; } = false;
        }

        public class UIParts
        {
            public string UIName { get; set; } = null;
            public string UIParent { get; set; } = string.Empty;
            public bool FullyEdit { get; set; } = false;
            public UIAnchorPositions UIAnchors = new UIAnchorPositions();
            public UIOffsetPositions UIOffsets = new UIOffsetPositions();
            public UIProperties UIProperties = new UIProperties();
            public UIText UIText = new UIText();
            public LoopSettings UILoopSettings = new LoopSettings();
            public List<UIParts> UIChildren = new List<UIParts>();

            public bool RemoveChildByName(string childName)
            {
                int foundChildIndex = UIChildren.FindIndex(x => x.UIName == childName);
                if (foundChildIndex != -1)
                {
                    UIChildren.RemoveAt(foundChildIndex);
                    return true;
                }

                foreach (var child in UIChildren)
                {
                    if (child.RemoveChildByName(childName))
                    {
                        return true;
                    }
                }
                return false;
            }

            public void AddChild(UIParts child)
            {
                if (!IsDescendant(child)) UIChildren.Add(child);
                else throw new InvalidOperationException("Cannot add child; would create circular reference.");
            }

            private bool IsDescendant(UIParts part)
            {
                if (this == part) return true;

                foreach (var child in UIChildren) if (child.IsDescendant(part)) return true;

                return false;
            }

            public UIParts FindParentByName(string childUIName, List<UIParts> allParts)
            {
                foreach (var part in allParts)
                {
                    foreach (var child in part.UIChildren)
                    {
                        if (child.UIName == childUIName)
                        {
                            return part;
                        }
                    }

                    UIParts potentialParent = part.FindParentByNameRecursive(childUIName);
                    if (potentialParent != null)
                    {
                        return potentialParent;
                    }
                }

                return null;
            }

            private UIParts FindParentByNameRecursive(string childUIName)
            {
                foreach (var grandChild in this.UIChildren)
                {
                    if (grandChild.UIName == childUIName)
                    {
                        return this;
                    }
                    else
                    {
                        UIParts potentialParent = grandChild.FindParentByNameRecursive(childUIName);
                        if (potentialParent != null)
                        {
                            return potentialParent;
                        }
                    }
                }

                return null;
            }


            public UIParts FindParent(List<UIParts> allParts)
            {
                foreach (var part in allParts)
                {
                    if (part.UIChildren.Contains(this))
                    {
                        return part;
                    }
                    else
                    {
                        UIParts potentialParent = part.FindParentRecursive(this);
                        if (potentialParent != null)
                        {
                            return potentialParent;
                        }
                    }
                }

                return null;
            }

            private UIParts FindParentRecursive(UIParts child)
            {
                foreach (var grandChild in this.UIChildren)
                {
                    if (grandChild == child)
                    {
                        return this;
                    }
                    else
                    {
                        UIParts potentialParent = grandChild.FindParentRecursive(child);
                        if (potentialParent != null)
                        {
                            return potentialParent;
                        }
                    }
                }

                return null;
            }

            public UIParts FindPanelByName(string uiName, List<UIParts> allParts)
            {
                foreach (var part in allParts)
                {
                    if (part.UIName == uiName)
                    {
                        return part;
                    }

                    UIParts foundPanel = part.FindPanelByNameRecursive(uiName);
                    if (foundPanel != null)
                    {
                        return foundPanel;
                    }
                }

                return null;
            }

            private UIParts FindPanelByNameRecursive(string uiName)
            {
                foreach (var child in this.UIChildren)
                {
                    if (child.UIName == uiName)
                    {
                        return child;
                    }
                    else
                    {
                        UIParts foundPanel = child.FindPanelByNameRecursive(uiName);
                        if (foundPanel != null)
                        {
                            return foundPanel;
                        }
                    }
                }

                return null;
            }


            public UIParts Clone()
            {
                UIParts clone = new UIParts
                {
                    UIName = this.UIName,
                    UIParent = this.UIParent,
                    FullyEdit = this.FullyEdit,
                    UIAnchors = this.UIAnchors.Clone(),
                    UIOffsets = this.UIOffsets.Clone(),
                    UIProperties = this.UIProperties.Clone(),
                    UIText = this.UIText.Clone(),
                    UILoopSettings = this.UILoopSettings.Clone()
                };

                foreach (var child in this.UIChildren)
                {
                    clone.UIChildren.Add(child.Clone());
                }

                return clone;
            }
        }

        public class UIAnchorPositions
        {
            public double XMin { get; set; }
            public double XMax { get; set; }
            public double YMin { get; set; }
            public double YMax { get; set; }

            public UIAnchorPositions Clone()
            {
                return new UIAnchorPositions
                {
                    XMin = this.XMin,
                    XMax = this.XMax,
                    YMin = this.YMin,
                    YMax = this.YMax
                };
            }
        }

        public class UIOffsetPositions
        {
            public bool Enabled { get; set; } = false;
            public double XMin { get; set; }
            public double XMax { get; set; }
            public double YMin { get; set; }
            public double YMax { get; set; }

            public UIOffsetPositions Clone()
            {
                return new UIOffsetPositions
                {
                    Enabled = this.Enabled,
                    XMin = this.XMin,
                    XMax = this.XMax,
                    YMin = this.YMin,
                    YMax = this.YMax
                };
            }
        }

        public class UIProperties
        {
            public string Color { get; set; } = "0 0 0 0";
            public bool Blur { get; set; } = false;
            public string Material { get; set; } = string.Empty;
            public float FadeIn { get; set; } = 0f;
            public string Image { get; set; } = string.Empty;
            public bool EnableCursor { get; set; } = false;
            public string Command { get; set; } = string.Empty;

            public UIProperties Clone()
            {
                return new UIProperties
                {
                    Color = this.Color,
                    Blur = this.Blur,
                    FadeIn = this.FadeIn,
                    Image = this.Image,
                    EnableCursor = this.EnableCursor,
                    Command = this.Command
                };
            }
        }

        public class UIText
        {
            public string Text { get; set; } = string.Empty;
            public int Size { get; set; } = 15;
            public string Font { get; set; } = "robotocondensed-bold.ttf";
            public string Color { get; set; } = "1 1 1 1";
            public TextAnchor Alignment { get; set; } = TextAnchor.MiddleCenter;

            public UIText Clone()
            {
                return new UIText
                {
                    Text = this.Text,
                    Size = this.Size,
                    Font = this.Font,
                    Color = this.Color,
                    Alignment = this.Alignment
                };
            }
        }

        public class LoopSettings
        {
            public bool Loop { get; set; } = false;
            public double Spacing { get; set; } = .15;
            public bool Vertical { get; set; } = true;

            public LoopSettings Clone()
            {
                return new LoopSettings
                {
                    Loop = this.Loop,
                    Spacing = this.Spacing
                };
            }
        }

        public List<UIParts> CloneUIList(List<UIParts> originalList)
        {
            List<UIParts> clonedList = new List<UIParts>();

            foreach (var uiPart in originalList)
            {
                clonedList.Add(uiPart.Clone());
            }

            return clonedList;
        }

        public class ThemeSettings
        {
            public List<UIParts> UIParts = new List<UIParts>();
            public List<DefaultColors> UIColors = new List<DefaultColors>();
        }

        public class RootObject
        {
            public List<UIParts> UIParts { get; set; }
        }


        private void GetUIFromAPI(string themeName = "original", Action onComplete = null)
        {
            string apiUrl = $"https://solarrust.com/api/{themeName}.json";

            GetUIPartsFromApi(apiUrl, uiParts =>
            {
                if (uiParts != null)
                {
                    List<DefaultColors> newColors = new List<DefaultColors>();
                    foreach (var color in _defaultThemes.First().Value.UIColors) newColors.Add(new DefaultColors { Color = color.Color, Name = color.Name });

                    _config.CurrentTheme = new KeyValuePair<string, ThemeSettings>("Classic", new ThemeSettings()
                    {
                        UIColors = newColors,
                        UIParts = uiParts
                    });
                }
                else
                {
                    Console.WriteLine("Failed to fetch UI Parts.");
                }

                onComplete?.Invoke();
            });
        }


        public void GetUIPartsFromApi(string apiUrl, Action<List<UIParts>> callback)
        {
            webrequest.Enqueue(apiUrl, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    Console.WriteLine($"Error fetching UI Parts from API: Status Code {code}");
                    callback(null);
                    return;
                }

                try
                {
                    RootObject rootObject = JsonConvert.DeserializeObject<RootObject>(response);
                    List<UIParts> uiParts = rootObject?.UIParts ?? new List<UIParts>();

                    callback(uiParts);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during deserialization: {ex.Message}");
                    callback(null);
                }
            }, this, RequestMethod.GET);
        }

        Dictionary<string, ThemeSettings> _defaultThemes = new Dictionary<string, ThemeSettings>()
        {
            { "Rust", 
                new ThemeSettings()
                {
                    UIParts = new List<UIParts>()
                    {
                        new UIParts
                {
                UIName = "BackgrounPanel",
                UIParent = "Overlay",
                FullyEdit = false,
                UIAnchors = new UIAnchorPositions()
                {
                    XMin = 0,
                    XMax = 1,
                    YMin = 0,
                    YMax = 1
                },
                UIProperties = new UIProperties()
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    EnableCursor = true,
                    FadeIn = 1f
                },
                UIChildren = new List<UIParts>()
                {
                    new UIParts()
                    {
                        UIName = "BackgrounColorOverlay",
                        FullyEdit = true,
                        UIAnchors = new UIAnchorPositions()
                        {
                            XMin = 0,
                            XMax = 1,
                            YMin = 0,
                            YMax = 1
                        },
                        UIProperties = new UIProperties()
                        {
                            Color = "0.2 0.2 0.2 .5",
                        },
                        UIChildren = new List<UIParts>()
                        {
                            new UIParts()
                            {
                            UIName = "InfoSeperationOverlay",
                            FullyEdit = true,
                            UIAnchors = new UIAnchorPositions()
                            {
                                XMin = .24,
                                XMax = 1,
                                YMin = 0,
                                YMax = 1
                            },
                            UIProperties = new UIProperties()
                            {
                                Color = "0 0 0 .5",
                            },
                        },
                            new UIParts()
                            {
                                UIName = "MainImage",
                                UIAnchors = new UIAnchorPositions()
                                {
                                    XMin = .03,
                                    YMin = .77,
                                    XMax = .23,
                                    YMax = .91
                                },
                                UIProperties = new UIProperties()
                                {
                                    Image = "https://i.ibb.co/JQkq1wd/WELCOMe.png"
                                } 
                            },
                            new UIParts
                        {
                            UIName = "ButtonPanel",
                            FullyEdit = false,
                            UIAnchors = new UIAnchorPositions()
                            {
                                XMin = .03,
                                YMin = .08,
                                XMax = .22,
                                YMax = .71
                            },
                            UIProperties = new UIProperties()
                            {
                                Color = "0 0 0 0",
                            },
                            UIChildren = new List<UIParts>()
                            {
                                new UIParts
                                {
                                    UIName = "ButtonSettings",
                                    FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = .01,
                                        YMin = .92,
                                        XMax = .99,
                                        YMax = .99
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = "0 0 0 0"
                                    },
                                    UIText = new UIText()
                                    {
                                        Size = 25,
                                        Color = "1 1 1 .9",
                                        Alignment = TextAnchor.MiddleCenter
                                    },
                                    UILoopSettings = new LoopSettings()
                                    {
                                        Spacing = .01,
                                        Loop = true
                                    }
                                },
                                new UIParts
                                {
                                    UIName = "SelectedButtonOverlay",
                                    FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = -.01,
                                        YMin = -.02,
                                        XMax = 1.01,
                                        YMax = 1.02
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = ".36 .60 1 0.6"
                                    }
                                }
                            }
                        },
                            new UIParts
                        {
                            UIName = "TitlePanel",
                            FullyEdit = true,
                            UIAnchors = new UIAnchorPositions()
                            {
                                XMin = .25,
                                YMin = .92,
                                XMax = .99,
                                YMax = .98
                            },
                            UIProperties = new UIProperties()
                            {
                                Color = "0 0 0 .4",
                            },
                            UIText = new UIText()
                            {
                                Text = "YOUR SERVER NAME",
                                Size = 25
                            },
                        },
                            new UIParts
                        {
                            UIName = "PlayersPanel",
                            FullyEdit = true,
                            UIAnchors = new UIAnchorPositions()
                            {
                                XMin = .03,
                                YMin = .92,
                                XMax = .23,
                                YMax = .98
                            },
                            UIProperties = new UIProperties()
                            {
                                Color = "0 0 0 .4",
                            },
                            UIText = new UIText()
                            {
                                Size = 25,
                                Text = "1 / 1"
                            }
                        },
                            new UIParts
                            {
                                UIName = "WCSourcePanel",
                                FullyEdit = false,
                                UIAnchors = new UIAnchorPositions()
                                {
                                    XMin = .25,
                                    YMin = .07,
                                    XMax = .99,
                                    YMax = .91
                                },
                                UIProperties = new UIProperties()
                                {
                                    Color = "0 0 0 0",
                                },
                                UIChildren = new List<UIParts>()
                            {
                                new UIParts
                                {
                                    UIName = "TextPanel_V1",
                                    FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = 0,
                                        YMin = 0,
                                        XMax = 1,
                                        YMax = 1
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = "0 0 0 0",
                                    },
                                    UIChildren = new List<UIParts>()
                                    {
                                        new UIParts()
                                        {
                                            UIName = "PanelTitle",
                                            FullyEdit = false,
                                            UIAnchors =
                                            {
                                                XMin = 0,
                                                YMin = .91,
                                                XMax = 1,
                                                YMax = 1
                                            },
                                            UIProperties= new UIProperties()
                                            {
                                                Color = "0 0 0 .4"
                                            },
                                            UIText = new UIText()
                                            {
                                                Size = 20,
                                                Text = "INFO"
                                            }
                                        },
                                        new UIParts
                                        {
                                            UIName = "TextPanel",
                                            FullyEdit = false,
                                            UIAnchors = new UIAnchorPositions()
                                            {
                                                XMin = 0,
                                                YMin = 0,
                                                XMax = 1,
                                                YMax = .9
                                            },
                                            UIProperties = new UIProperties()
                                            {
                                                Color = "0 0 0 .4",
                                            },
                                            UIChildren = new List<UIParts>()
                                            {
                                                new UIParts
                                                {
                                                    UIName = "TextPanelInlay",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = .01,
                                                        YMin = .02,
                                                        XMax = .99,
                                                        YMax = .98
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new UIParts
                                {
                                    UIName = "TextPanel_V2",
                                            FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = 0,
                                        YMin = 0,
                                        XMax = 1,
                                        YMax = 1
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = "0 0 0 0",
                                    },
                                    UIChildren = new List<UIParts>()
                                    {
                                        new UIParts()
                                        {
                                            UIName = "PanelTitle",
                                            FullyEdit = false,
                                            UIAnchors =
                                            {
                                                XMin = 0,
                                                YMin = .91,
                                                XMax = 1,
                                                YMax = 1
                                            },
                                            UIProperties= new UIProperties()
                                            {
                                                Color = "0 0 0 .4"
                                            },
                                            UIText = new UIText()
                                            {
                                                Size = 20,
                                                Text = "INFO"
                                            }
                                        },
                                        new UIParts
                                        {
                                            UIName = "TextPanel",
                                            FullyEdit = false,
                                            UIAnchors = new UIAnchorPositions()
                                            {
                                                XMin = 0,
                                                YMin = .13,
                                                XMax = 1,
                                                YMax = .9
                                            },
                                            UIProperties = new UIProperties()
                                            {
                                                Color = "0 0 0 .4",
                                            },
                                            UIChildren = new List<UIParts>()
                                            {
                                                new UIParts
                                                {
                                                    UIName = "TextPanelInlay",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = .01,
                                                        YMin = .02,
                                                        XMax = .99,
                                                        YMax = .98
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                            }
                                        },
                                        new UIParts
                                                {
                                                    UIName = "BottomImage",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = 0,
                                                        YMin = .012,
                                                        XMax = .99,
                                                        YMax = 1
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                    }
                                },
                                new UIParts
                                {
                                    UIName = "TextPanel_V3",
                                            FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = 0,
                                        YMin = 0,
                                        XMax = 1,
                                        YMax = 1
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = "0 0 0 0",
                                    },
                                                                        UIChildren = new List<UIParts>()
                                    {
                                        new UIParts()
                                        {
                                            UIName = "PanelTitle",
                                            FullyEdit = false,
                                            UIAnchors =
                                            {
                                                XMin = 0,
                                                YMin = .91,
                                                XMax = 1,
                                                YMax = 1
                                            },
                                            UIProperties= new UIProperties()
                                            {
                                                Color = "0 0 0 .4"
                                            },
                                            UIText = new UIText()
                                            {
                                                Size = 20,
                                                Text = "INFO"
                                            }
                                        },
                                        new UIParts
                                        {
                                            UIName = "TextPanel",
                                            FullyEdit = false,
                                            UIAnchors = new UIAnchorPositions()
                                            {
                                                XMin = 0,
                                                YMin = .09,
                                                XMax = 1,
                                                YMax = .9
                                            },
                                            UIProperties = new UIProperties()
                                            {
                                                Color = "0 0 0 .4",
                                            },
                                            UIChildren = new List<UIParts>()
                                            {
                                                new UIParts
                                                {
                                                    UIName = "TextPanelInlay",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = .01,
                                                        YMin = .02,
                                                        XMax = .99,
                                                        YMax = .98
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                            }
                                        },
                                        new UIParts
                                                {
                                                    UIName = "LeftButton",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = 0,
                                                        YMin = 0,
                                                        XMax = .495,
                                                        YMax = .08
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.MiddleCenter,
                                                        Text = "<",
                                                        Color = "1 1 1 1",
                                                        Size = 20
                                                    },
                                                    UIProperties = new UIProperties()
                                                    {
                                                        Command = "wc_main page last",
                                                        Color = "0 0 0 .4"
                                                    } 
                                        },
                                                                                new UIParts
                                                {
                                                    UIName = "RightButton",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = .505,
                                                        YMin = 0,
                                                        XMax = 1,
                                                        YMax = .08
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.MiddleCenter,
                                                        Text = ">",
                                                        Color = "1 1 1 1",
                                                        Size = 20
                                                    },
                                                    UIProperties = new UIProperties()
                                                    {
                                                        Command = "wc_main page next",
                                                        Color = "0 0 0 .4"
                                                    }
                                        }
                                    }
                                },
                                new UIParts
                                {
                                    UIName = "TextPanel_V4",
                                            FullyEdit = false,
                                    UIAnchors = new UIAnchorPositions()
                                    {
                                        XMin = 0,
                                        YMin = 0,
                                        XMax = 1,
                                        YMax = 1
                                    },
                                    UIProperties = new UIProperties()
                                    {
                                        Color = "0 0 0 0",
                                    },
                                                                                                            UIChildren = new List<UIParts>()
                                    {
                                        new UIParts()
                                        {
                                            UIName = "PanelTitle",
                                            FullyEdit = false,
                                            UIAnchors =
                                            {
                                                XMin = 0,
                                                YMin = .91,
                                                XMax = 1,
                                                YMax = 1
                                            },
                                            UIProperties= new UIProperties()
                                            {
                                                Color = "0 0 0 .4"
                                            },
                                            UIText = new UIText()
                                            {
                                                Size = 20,
                                                Text = "INFO"
                                            }
                                        },
                                        new UIParts
                                        {
                                            UIName = "TextPanel",
                                            FullyEdit = false,
                                            UIAnchors = new UIAnchorPositions()
                                            {
                                                XMin = 0,
                                                YMin = .22,
                                                XMax = 1,
                                                YMax = .9
                                            },
                                            UIProperties = new UIProperties()
                                            {
                                                Color = "0 0 0 .4",
                                            },
                                            UIChildren = new List<UIParts>()
                                            {
                                                new UIParts
                                                {
                                                    UIName = "TextPanelInlay",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = .01,
                                                        YMin = .02,
                                                        XMax = .99,
                                                        YMax = .98
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                            }
                                        },
                                        new UIParts
                                                {
                                                    UIName = "LeftButton",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = 0,
                                                        YMin = 0,
                                                        XMax = .495,
                                                        YMax = .08
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.MiddleCenter,
                                                        Text = "<",
                                                        Color = "1 1 1 1",
                                                        Size = 20
                                                    },
                                                    UIProperties = new UIProperties()
                                                    {
                                                        Command = "wc_main page last",
                                                        Color = "0 0 0 .4"
                                                    }
                                        },
                                                                                new UIParts
                                                {
                                                    UIName = "RightButton",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = 0,
                                                        YMin = .505,
                                                        XMax = 1,
                                                        YMax = .08
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.MiddleCenter,
                                                        Text = ">",
                                                        Color = "1 1 1 1",
                                                        Size = 20
                                                    },
                                                    UIProperties = new UIProperties()
                                                    {
                                                        Command = "wc_main page next",
                                                        Color = "0 0 0 .4"
                                                    }
                                        },
                                         new UIParts
                                                {
                                                    UIName = "BottomImage",
                                                    FullyEdit = false,
                                                    UIAnchors = new UIAnchorPositions()
                                                    {
                                                        XMin = 0,
                                                        YMin = .09,
                                                        XMax = 1,
                                                        YMax = .21
                                                    },
                                                    UIText = new UIText()
                                                    {
                                                        Alignment = TextAnchor.UpperLeft
                                                    }
                                                }
                                    }

                                },
                            }
                            },
                            new UIParts
                            {
                                UIName = "CloseButton",
                                            FullyEdit = false,
                                UIAnchors = new UIAnchorPositions()
                                {
                                    XMin = .03,
                                    YMin = .02,
                                    XMax = .22,
                                    YMax = .07
                                },
                                UIProperties = new UIProperties()
                                {
                                    Command = "wc_main close",
                                    EnableCursor = true
                                },
                                UIText = new UIText()
                                {
                                    Text = "CLOSE",
                                    Size = 30
                                }
                            },
                        }
                    },
                }
            },
                    },
                    UIColors = new List<DefaultColors>()
                    {
                        new DefaultColors()
                        {
                            Name = "BackgroundColor",
                            Color = "0 0 0 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "SecondaryColor",
                            Color = "0 0 0 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "PrimaryButtonColor",
                            Color = "0.22 0.83 0.91 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "SecondaryButtonColor",
                            Color = "0.91 0.22 0.22 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "ThirdButtonColor",
                            Color = "0.26 0.91 0.22 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "PopupMainColor",
                            Color = ".17 .17 .17 1"
                        },
                        new DefaultColors()
                        {
                            Name = "PopupSecondaryColor",
                            Color = "1 1 1 .1"
                        }
                    }
                } 
            }
        };

        List<DefaultColors> compareThemeColors = new List<DefaultColors>()
        {
                        new DefaultColors()
                        {
                            Name = "BackgroundColor",
                            Color = "0 0 0 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "SecondaryColor",
                            Color = "0 0 0 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "PrimaryButtonColor",
                            Color = "0.22 0.83 0.91 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "SecondaryButtonColor",
                            Color = "0.91 0.22 0.22 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "ThirdButtonColor",
                            Color = "0.26 0.91 0.22 .5"
                        },
                        new DefaultColors()
                        {
                            Name = "PopupMainColor",
                            Color = ".17 .17 .17 1"
                        },
                        new DefaultColors()
                        {
                            Name = "PopupSecondaryColor",
                            Color = "1 1 1 .1"
                        }
        };
        #endregion

        #region [ UI EDITOR DATA STRUCTURE ]
        System.Random rnd = new System.Random();
        public Dictionary<BasePlayer, UIEditor> _uiEditors = new Dictionary<BasePlayer, UIEditor>();

        public class UIEditor
        {
            public List<string> OpenParents { get; set; } = new List<string>();
            public UIParts OpenPanel = new UIParts();
            public InfoPanelSettings PanelCopy = new InfoPanelSettings();
            public string PanelCopyName { get; set; } = string.Empty;
            public UIParts CreatingPanel = null;
            public List<UIParts> UICopy { get; set; } = new List<UIParts>();
            public string LastSelectedPanel { get; set; } = String.Empty;
            public int CommandNumber { get; set; } = 0;
            public string CommandHolder { get; set; } = string.Empty;
            public int LinesPage { get; set; } = 0;
            public string PanelColors { get; set; } = string.Empty;
            public Dictionary<string, string> PluginColors { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, XYLocations> UIEditorLocations = new Dictionary<string, XYLocations>()
            {
                { "UIList", new XYLocations() { XMin = .005, YMin = .65, XMax = .2, YMax = .99 } },
                { "UICFG", new XYLocations() { XMin = .005, YMin = .3, XMax = .2, YMax = .64 } },
                { "UIColor", new XYLocations() { XMin = .005, YMin = .01, XMax = .2, YMax = .29 } },
            };
        }

        public class XYLocations
        {
            public double XMin { get; set; } = 0;
            public double XMax { get; set; } = 0;
            public double YMin { get; set; } = 0;
            public double YMax { get; set; } = 0;
        }
        #endregion

        #region [ EXTERNAL DATA GATHERING ]
        void GetPluginColors(string pluginName) => Interface.CallHook("OnWCRequestColors", pluginName);

        void SendThemeColors(List<string> pluginNames, Dictionary<string, string> themeColors) => Interface.CallHook("OnWCSentThemeColors", pluginNames, themeColors);

        [HookMethod("IsUsingPlugin")]
        bool IsUsingPlugin(string pluginName)
        {
            return _config.UIPanels.Any(x => x.Enabled && !string.IsNullOrEmpty(x.AddonSettings?.AddonName) && x.AddonSettings.AddonName.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
        }

        void WCSendColors(Dictionary<string, string> pluginColors, string pluginTitle)
        {
            var uiEditor = _uiEditors.FirstOrDefault(x => x.Value.PanelColors == pluginTitle);
            if (uiEditor.Value != null)
            {
                uiEditor.Value.PluginColors = pluginColors;
                timer.Once(.5f, () => CreatePluginColorsList(uiEditor.Key));
                _uiEditors[uiEditor.Key].PanelColors = String.Empty;
            }

        }
        #endregion

        #region [ COMMANDS ]
        void MoveUIPanelSelector(BasePlayer player, UIEditor editor)
        {
            double moveBy = .3;
            var UIList = editor.UIEditorLocations["UIList"];
            UIList.YMin += moveBy;

            var UICFG = editor.UIEditorLocations["UICFG"];
            UICFG.YMin += moveBy;
            UICFG.YMax += moveBy;

            var UIColor = editor.UIEditorLocations["UIColor"];
            UIColor.YMin += moveBy;
            UIColor.YMax += moveBy;

            CuiHelper.DestroyUi(player, "WC.Edit.List.ListPanel");
            UIUpdatePanel(player, "WC.Edit.List.Main", "0 0 0 0", new CuiRectTransformComponent() { AnchorMin = $"{UIList.XMin} {UIList.YMin}", AnchorMax = $"{UIList.XMax} {UIList.YMax}" });
            UIUpdateLabel(player, "WC.Edit.List.ListTitle", "UI PANELS", new CuiRectTransformComponent() { AnchorMin = "0 0", AnchorMax = ".995 1" });
            UIUpdateButton(player, "WC.Edit.List.ListButton", true, "wc_editor openobj list");
            UIUpdateLabel(player, "WC.Edit.List.ListButtonText", "+");

            MoveCFGSelector(player, editor);
            MoveColorSelector(player, editor);
        }

        void MoveCFGSelector(BasePlayer player, UIEditor editor, bool direct = false, bool up = true)
        {
            var UICFG = editor.UIEditorLocations["UICFG"];
            double moveBy = .3;

            if (direct) UICFG.YMin += moveBy; 

            UIUpdatePanel(player, "WC.Edit.CFG.Main", "0 0 0 0", new CuiRectTransformComponent() { AnchorMin = $"{UICFG.XMin} {UICFG.YMin}", AnchorMax = $"{UICFG.XMax} {UICFG.YMax}" });

            if (direct)
            {
                CuiHelper.DestroyUi(player, "WC.Edit.CFG.CFGList");
                UIUpdateLabel(player, "WC.Edit.CFG.CFGTitle", "CFG", new CuiRectTransformComponent() { AnchorMin = "0 0", AnchorMax = ".995 1" });
                UIUpdateButton(player, "WC.Edit.CFG.CFGButton", true, "wc_editor openobj cfg");
                UIUpdateLabel(player, "WC.Edit.CFG.CFGButtonText", "+");

                var UIColor = editor.UIEditorLocations["UIColor"];
                UIColor.YMin += moveBy;
                UIColor.YMax += moveBy;
                MoveColorSelector(player, editor);
            }
        }

        void MoveColorSelector(BasePlayer player, UIEditor editor, bool direct = false, bool up = true)
        {
            var UIColor = editor.UIEditorLocations["UIColor"];

            if (direct)
            {
                double moveBy = .24;
                UIColor.YMin += moveBy;
            }

            UIUpdatePanel(player, "WC.Edit.Color.Main", "0 0 0 0", new CuiRectTransformComponent() { AnchorMin = $"{UIColor.XMin} {UIColor.YMin}", AnchorMax = $"{UIColor.XMax} {UIColor.YMax}" });

            if (direct)
            {
                CuiHelper.DestroyUi(player, "WC.Edit.Color.ColorList");
                UIUpdateLabel(player, "WC.Edit.Color.ColorTitle", "Color", new CuiRectTransformComponent() { AnchorMin = "0 0", AnchorMax = ".995 1" });
                UIUpdateButton(player, "WC.Edit.Color.ColorButton", true, "wc_editor openobj color");
                UIUpdateLabel(player, "WC.Edit.Color.ColorButtonText", "+");
            }
        }

        bool FindAndRemoveUIPart(List<UIParts> uiParts, string panelName, int i = 0)
        {
            int foundChildIndex = uiParts.FindIndex(x => x.UIName == panelName);
            if (foundChildIndex != -1)
            {
                uiParts.RemoveAt(foundChildIndex);
                return true;
            }

            string dashes = "-";
            for (int intt = 0; intt < i; intt++) dashes += "-";

            foreach (var uiPart in uiParts)
            {
                if (FindAndRemoveUIPart(uiPart.UIChildren, panelName, i))
                {
                    return true;
                }
            }

            return false;
        }

        void UIOpenEditPanel(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "welcomecontroller.admin"))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            var clonedUI = CloneUIList(_config.CurrentTheme.Value.UIParts);
            clonedUI.First().UIParent = "Hud";

            _uiEditors[player] = new UIEditor()
            {
                UICopy = clonedUI,
                OpenPanel = clonedUI.First()
            };

            CreateEditorUI(player);
        }

        [ConsoleCommand("wc_editor")]
        private void CMDWelcomeControllerEdit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, "welcomecontroller.admin")) return;

            switch (arg.Args[0])
            {
                case "selecttheme":
                    CuiHelper.DestroyUi(player, "WC.Theme.Main");

                    GetUIFromAPI(arg.Args[1].ToLower(), () =>
                    {
                        SaveConfig();
                    });

                    ReverseImageChanges(_config.CurrentTheme.Value.UIParts);
                    break;
                case "createlist":
                    var clonedUI = CloneUIList(_config.CurrentTheme.Value.UIParts);
                    clonedUI.First().UIParent = "Hud";

                    _uiEditors[player] = new UIEditor()
                    {
                        UICopy = clonedUI,
                        OpenPanel = clonedUI.First()
                    };

                    CreateEditorUI(player);
                    break;
                case "close":
                    UIEditor editor = _uiEditors[player];

                    CuiHelper.DestroyUi(player, "WC.Edit.List.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CFG.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Color.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");

                    CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Panel.Main");
                    foreach (var mainUi in editor.UICopy) CuiHelper.DestroyUi(player, mainUi.UIName);
                    ReverseImageChanges(_config.CurrentTheme.Value.UIParts);

                    if(_uiEditors.ContainsKey(player)) _uiEditors.Remove(player);
                    UILoadPanels(player, _config.CurrentTheme.Value.UIParts);
                    break;
                case "panelname":
                    editor = _uiEditors[player];
                    var newName = String.Join(" ", arg.Args.Skip(1)).Replace(" ", "_");
                    editor.OpenPanel.UIName = newName;
                    UICreateUIList(player, editor.UICopy);
                    break;
                case "customcolorpage":
                    GenerateCustomColorsOnPanel(player, page: int.Parse(arg.Args[1]));
                    break;
                case "savechanges":
                    editor = _uiEditors[player];
                    editor.UICopy.First().UIParent = "Overlay";
                    _config.CurrentTheme.Value.UIParts = CloneUIList(editor.UICopy);

                    CuiHelper.DestroyUi(player, "WC.Edit.List.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CFG.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Color.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");

                    CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Panel.Main");
                    foreach (var mainUi in _config.CurrentTheme.Value.UIParts) CuiHelper.DestroyUi(player, mainUi.UIName);
                    SaveConfig();
                    _uiEditors.Remove(player);
                    UILoadPanels(player, _config.CurrentTheme.Value.UIParts);
                    break;
                case "collapse":
                    editor = _uiEditors[player];
                    switch (arg.Args[1])
                    {
                        case "list":
                            MoveUIPanelSelector(player, editor);
                            break;
                        case "cfg":
                            MoveCFGSelector(player, editor, true);
                            break;
                        case "color":
                            MoveColorSelector(player, editor, true);
                            break;
                    }
                    break;
                case "openobj":
                    editor = _uiEditors[player];
                    switch (arg.Args[1])
                    {
                        case "list":
                            double moveBy = .3;
                            var UIList = editor.UIEditorLocations["UIList"];
                            UIList.YMin -= moveBy;

                            var UICFG = editor.UIEditorLocations["UICFG"];
                            UICFG.YMax -= moveBy;
                            UICFG.YMin -= moveBy;

                            var UIColor = editor.UIEditorLocations["UIColor"];
                            UIColor.YMax -= moveBy;
                            UIColor.YMin -= moveBy;

                            UICreateUIList(player, editor.UICopy);
                            MoveCFGSelector(player, editor, false, false);
                            MoveColorSelector(player, editor, false, false);
                            break;
                        case "cfg":
                            moveBy = .3;
                            UICFG = editor.UIEditorLocations["UICFG"];
                            UICFG.YMin -= moveBy;

                            UIColor = editor.UIEditorLocations["UIColor"];
                            UIColor.YMax -= moveBy;
                            UIColor.YMin -= moveBy;

                            UICreateCFGSelector(player);
                            MoveColorSelector(player, editor, false, false);
                            break;
                        case "color":
                            moveBy = .24;
                            UIColor = editor.UIEditorLocations["UIColor"];
                            UIColor.YMin -= moveBy;

                            UICreateColorCreator(player);
                            break;
                    }
                    break;
                case "selectcfgaddon":
                    editor = _uiEditors[player];
                    editor.PanelCopy = _config.UIPanels[int.Parse(arg.Args[1])].Clone();
                    editor.PanelColors = editor.PanelCopy.AddonSettings.AddonName;
                    editor.PanelCopyName = editor.PanelCopy.PanelName;
                    GetPluginColors(editor.PanelCopy.AddonSettings.AddonName.Contains("WUIAttachments") ? "WUIAttachments" : editor.PanelCopy.AddonSettings.AddonName);
                    UICreatePluginColorEditor(player);
                    break;
                case "selectcfgpanel":
                    editor = _uiEditors[player];

                    editor.PanelCopy = _config.UIPanels[int.Parse(arg.Args[1])].Clone();
                    editor.PanelCopyName = editor.PanelCopy.PanelName;
                    UICreateTextPanelEditor(player);
                    break;
                case "expandpanel":
                    editor = _uiEditors[player];
                    string panelName = arg.Args[1];
                    if (editor.OpenParents.Contains(panelName)) editor.OpenParents.Remove(panelName);
                    else editor.OpenParents.Add(panelName);

                    UICreateUIList(player, editor.UICopy);
                    break;
                case "selectpanel":
                    editor = _uiEditors[player];
                    string[] panelNames = arg.Args[1].Split('.').Skip(4).ToArray();
                    UIParts firstParts = null;
                    UIParts foundParts = null;

                    bool isSelectingParent = _selectingParents.Contains(player.userID);

                    for (int i = 0; i < editor.UICopy.Count; i++)
                    {
                        if (editor.UICopy[i].UIName == panelNames[0])
                        {
                            firstParts = editor.UICopy[i];
                            break;
                        }
                    }

                    if (firstParts != null)
                    {
                        if (panelNames.Length > 1)
                        {
                            UIParts uiParts = firstParts;
                            for (int i = 1; i < panelNames.Length; i++)
                            {
                                uiParts = GetUISection(ref uiParts, panelNames[i]);
                            }

                            foundParts = uiParts;

                        }
                        else foundParts = firstParts; 

                        if (!isSelectingParent) {
                            editor.OpenPanel = foundParts;

                            var container = new CuiElementContainer();
                            UIAlterSelectedItem(ref container, player, editor.LastSelectedPanel, false);

                            editor.LastSelectedPanel = arg.Args[1];
                            UIAlterSelectedItem(ref container, player, arg.Args[1], true);
                            PutUpDownButtons(player, container, arg.Args[1]);

                            UIFlashPanelColor(player, editor.OpenPanel.UIName, "1 0.78 0 0.7");

                            CuiHelper.AddUi(player, container);
                            UICreatePanelEditor(player);

                            timer.Once(1, () => UIFlashPanelColor(player, editor.OpenPanel.UIName, editor.OpenPanel.UIProperties.Color));
                        } else
                        {
                            if (editor.CreatingPanel != null)
                            {
                                UIParts clonedPanel = editor.CreatingPanel.Clone();

                                CuiHelper.DestroyUi(player, "WCCreatePanelPopup");

                                editor.OpenPanel = clonedPanel;
                                foundParts.AddChild(editor.OpenPanel);

                                if (!editor.OpenParents.Contains(foundParts.UIName)) editor.OpenParents.Add(foundParts.UIName);

                                UICreateUIList(player, editor.UICopy);

                                foreach (var panel in editor.UICopy) CuiHelper.DestroyUi(player, panel.UIName);
                                UILoadPanels(player, editor.UICopy);

                                UICreatePanelEditor(player);

                                UIFlashPanelColor(player, editor.OpenPanel.UIName, "1 0.78 0 0.7");
                                timer.Once(1, () => UIFlashPanelColor(player, editor.OpenPanel.UIName, editor.OpenPanel.UIProperties.Color));

                                if (_selectingParents.Contains(player.userID)) _selectingParents.Remove(player.userID);
                                editor.CreatingPanel = null;
                            }
                            else
                            {
                                UIParts clonedPanel = editor.OpenPanel.Clone();

                                bool isRemoved = FindAndRemoveUIPart(editor.UICopy, clonedPanel.UIName);
                                foreach (var panel in editor.UICopy) CuiHelper.DestroyUi(player, panel.UIName);

                                editor.OpenPanel = clonedPanel;
                                foundParts.AddChild(editor.OpenPanel);

                                UICreateUIList(player, editor.UICopy);
                                TryChangeSelectParent(player);
                                UILoadPanels(player, editor.UICopy);
                            }
                        }
                    }
                    break;
                case "anchor":
                    editor = _uiEditors[player];

                    switch (arg.Args[1])
                    {
                        case "ymax":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIAnchors.YMax += .01;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIAnchors.YMax -= .01;
                            else editor.OpenPanel.UIAnchors.YMax = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMax", $"{editor.OpenPanel.UIAnchors.YMax}");
                            break;
                        case "ymin":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIAnchors.YMin += .01;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIAnchors.YMin -= .01;
                            else editor.OpenPanel.UIAnchors.YMin = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMin", $"{editor.OpenPanel.UIAnchors.YMin}");
                            break;
                        case "xmin":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIAnchors.XMin += .01;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIAnchors.XMin -= .01;
                            else editor.OpenPanel.UIAnchors.XMin = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMin", $"{editor.OpenPanel.UIAnchors.XMin}");
                            break;
                        case "xmax":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIAnchors.XMax += .01;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIAnchors.XMax -= .01;
                            else editor.OpenPanel.UIAnchors.XMax = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMax", $"{editor.OpenPanel.UIAnchors.XMax}");                              
                            break;
                        case "xright":
                            editor.OpenPanel.UIAnchors.XMax += .01;
                            editor.OpenPanel.UIAnchors.XMin += .01;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMax", $"{editor.OpenPanel.UIAnchors.XMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMin", $"{editor.OpenPanel.UIAnchors.XMin}");
                            break;
                        case "xleft":
                            editor.OpenPanel.UIAnchors.XMax -= .01;
                            editor.OpenPanel.UIAnchors.XMin -= .01;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMax", $"{editor.OpenPanel.UIAnchors.XMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.XMin", $"{editor.OpenPanel.UIAnchors.XMin}");
                            break;
                        case "yup":
                            editor.OpenPanel.UIAnchors.YMax += .01;
                            editor.OpenPanel.UIAnchors.YMin += .01;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMax", $"{editor.OpenPanel.UIAnchors.YMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMin", $"{editor.OpenPanel.UIAnchors.YMin}");
                            break;
                        case "ydown":
                            editor.OpenPanel.UIAnchors.YMax -= .01;
                            editor.OpenPanel.UIAnchors.YMin -= .01;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMax", $"{editor.OpenPanel.UIAnchors.YMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Anchors.YMin", $"{editor.OpenPanel.UIAnchors.YMin}");
                            break;
                    }

                    //CuiHelper.DestroyUi(player, editor.OpenPanel.UIName);
                    UIParts parentPart = editor.OpenPanel.FindParent(editor.UICopy);

                    var cTainer = new CuiElementContainer();
                    cTainer.Add(new CuiElement()
                    {
                        Name = editor.OpenPanel.UIName,
                        Update = true,
                        Components =
                        {
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = $"{editor.OpenPanel.UIAnchors.XMin} {editor.OpenPanel.UIAnchors.YMin}",
                                AnchorMax = $"{editor.OpenPanel.UIAnchors.XMax} {editor.OpenPanel.UIAnchors.YMax}",
                            }
                        }
                    });

                    CuiHelper.AddUi(player, cTainer);

                    //if(parentPart != null) UILoadPanels(player, new List<UIParts> { editor.OpenPanel }, null, true, parentPart.UIName);
                    //else UILoadPanels(player, editor.UICopy);
                    break;
                case "offset":
                    editor = _uiEditors[player];

                    switch (arg.Args[1])
                    {
                        case "ymax":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIOffsets.YMax += 1;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIOffsets.YMax -= 1;
                            else editor.OpenPanel.UIOffsets.YMax = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMax", $"{editor.OpenPanel.UIOffsets.YMax}");
                            break;
                        case "ymin":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIOffsets.YMin += 1;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIOffsets.YMin -= 1;
                            else editor.OpenPanel.UIOffsets.YMin = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMin", $"{editor.OpenPanel.UIOffsets.YMin}");
                            break;
                        case "xmin":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIOffsets.XMin += 1;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIOffsets.XMin -= 1;
                            else editor.OpenPanel.UIOffsets.XMin = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMin", $"{editor.OpenPanel.UIOffsets.XMin}");
                            break;
                        case "xmax":
                            if (arg.Args[2] == "+") editor.OpenPanel.UIOffsets.XMax += 1;
                            else if (arg.Args[2] == "-") editor.OpenPanel.UIOffsets.XMax -= 1;
                            else editor.OpenPanel.UIOffsets.XMax = double.Parse(arg.Args[2]);
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMax", $"{editor.OpenPanel.UIOffsets.XMax}");
                            break;
                        case "xright":
                            editor.OpenPanel.UIOffsets.XMax += 1;
                            editor.OpenPanel.UIOffsets.XMin += 1;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMax", $"{editor.OpenPanel.UIOffsets.XMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMin", $"{editor.OpenPanel.UIOffsets.XMin}");
                            break;
                        case "xleft":
                            editor.OpenPanel.UIOffsets.XMax -= 1;
                            editor.OpenPanel.UIOffsets.XMin -= 1;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMax", $"{editor.OpenPanel.UIOffsets.XMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.XMin", $"{editor.OpenPanel.UIOffsets.XMin}");
                            break;
                        case "yup":
                            editor.OpenPanel.UIOffsets.YMax += 1;
                            editor.OpenPanel.UIOffsets.YMin += 1;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMax", $"{editor.OpenPanel.UIOffsets.YMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMin", $"{editor.OpenPanel.UIOffsets.YMin}");
                            break;
                        case "ydown":
                            editor.OpenPanel.UIOffsets.YMax -= 1;
                            editor.OpenPanel.UIOffsets.YMin -= 1;
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMax", $"{editor.OpenPanel.UIOffsets.YMax}");
                            UIUpdateInputValue(player, "WC.Edit.Panel.Offsets.YMin", $"{editor.OpenPanel.UIOffsets.YMin}");
                            break;
                        case "enabled":
                            editor.OpenPanel.UIOffsets.Enabled = !editor.OpenPanel.UIOffsets.Enabled;

                            UIUpdateButton(player, "WC.Edit.Panel.Offsets.Enabled", editor.OpenPanel.UIOffsets.Enabled);

                            SendPanelUpdate(player, editor);
                            break;
                    }

                    SendPanelUpdate(player, editor);
                    break;
                case "panelcolor":
                    editor = _uiEditors[player];

                    string color = ConvertHexColorToRgba(string.Join(" ", arg.Args.Skip(1)));

                    editor.OpenPanel.UIProperties.Color = color;
                    UIUpdateInputValue(player, "WC.Edit.Panel.Properties.Color", editor.OpenPanel.UIProperties.Color);
                    UIUpdatePanel(player, "WC.Edit.Panel.Properties.ColorBlock", editor.OpenPanel.UIProperties.Color);

                    SendPanelUpdate(player, editor);
                    break;
                case "panelblur":
                    editor = _uiEditors[player];

                    editor.OpenPanel.UIProperties.Blur = !editor.OpenPanel.UIProperties.Blur;

                    UIUpdateButton(player, "WC.Edit.Panel.Properties.Blur", editor.OpenPanel.UIProperties.Blur);

                    SendPanelUpdate(player, editor);
                    break;
                case "panelimage":
                    editor = _uiEditors[player];

                    editor.OpenPanel.UIProperties.Image = arg.Args.Length < 2 ? String.Empty : arg.Args[1];
                    if (editor.OpenPanel.UIProperties.Image != string.Empty)
                    {
                        RegisterNewImage(editor.OpenPanel.UIName, editor.OpenPanel.UIProperties.Image);
                        timer.Once(1f, () => SendPanelUpdate(player, editor));
                    } else SendPanelUpdate(player, editor);
                    break;
                case "panelfadein":
                    editor = _uiEditors[player];

                    if (arg.Args.Length < 2 || !float.TryParse(arg.Args[1], out float fadeIn))
                    {
                        editor.OpenPanel.UIProperties.FadeIn = 0f;
                    }
                    else editor.OpenPanel.UIProperties.FadeIn = fadeIn;

                    SendPanelUpdate(player, editor);
                    break;
                case "spacing":
                    editor = _uiEditors[player];

                    switch (arg.Args[1])
                    {
                        case "direction":
                            if (editor.OpenPanel.UILoopSettings.Vertical) editor.OpenPanel.UILoopSettings.Vertical = false;
                            else editor.OpenPanel.UILoopSettings.Vertical = true;

                            UIUpdateLabel(player, "WC.Edit.Panel.Spacing.DirectionText", $"CURRENTLY {(editor.OpenPanel.UILoopSettings.Vertical ? "VERTICAL" : "HORIZONTAL")}");
                            break;
                        case "change":
                            if (arg.Args[2] == "-") editor.OpenPanel.UILoopSettings.Spacing -= .01;
                            else editor.OpenPanel.UILoopSettings.Spacing += .01;

                            UIUpdateInputValue(player, "WC.Edit.Panel.Spacing.Space", $"{editor.OpenPanel.UILoopSettings.Spacing}");
                            break;
                        case "space":
                            editor.OpenPanel.UILoopSettings.Spacing = double.Parse(arg.Args[2]);
                            break;
                    }
                    break;
                case "text":
                    editor = _uiEditors[player];

                    switch (arg.Args[1])
                    {
                        case "text":
                            editor.OpenPanel.UIText.Text = arg.Args.Length < 3 ? String.Empty : String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "alignment":
                            editor.OpenPanel.UIText.Alignment = (TextAnchor)int.Parse(arg.Args[2]);
                            break;
                        case "font":
                            var fontNumber = _fonts.FindIndex(x => x == editor.OpenPanel.UIText.Font);

                            if (arg.Args[2] == "next")
                            {
                                if (fontNumber == _fonts.Count - 1) editor.OpenPanel.UIText.Font = _fonts[0];
                                else editor.OpenPanel.UIText.Font = _fonts[fontNumber + 1];
                            } else
                            {
                                if (fontNumber == 0) editor.OpenPanel.UIText.Font = _fonts.Last();
                                else editor.OpenPanel.UIText.Font = _fonts[fontNumber - 1];
                            }

                            UIUpdateLabel(player, "WC.Edit.Panel.Text.Font", editor.OpenPanel.UIText.Font);
                            break;
                        case "color":
                            if (arg.Args.Length < 3) editor.OpenPanel.UIText.Color = "1 1 1 1";
                            else
                            {
                                color = ConvertHexColorToRgba(string.Join(" ", arg.Args.Skip(2)));

                                editor.OpenPanel.UIText.Color = color;
                            }

                            UIUpdateInputValue(player, "WC.Edit.Panel.Text.Color", editor.OpenPanel.UIText.Color);
                            break;
                        case "size":
                            if (arg.Args.Length < 3 || !int.TryParse(arg.Args[2], out int size))
                            {
                                editor.OpenPanel.UIText.Size = 15;
                            }
                            else editor.OpenPanel.UIText.Size = size;

                            UIUpdateInputValue(player, "WC.Edit.Panel.Text.Size", $"{editor.OpenPanel.UIText.Size}");
                            break;
                        case "checktags":
                            UICreateTextTagsPopup(player);
                            break;
                    }

                    if (arg.Args[1] != "checktags") SendPanelUpdate(player, editor);
                    break;
                case "plugincolor":
                    CMDOtherPluginColors(player, arg.Args);
                    break;
                case "panelsettings":
                    editor = _uiEditors[player];
                    var panelCopy = editor.PanelCopy;

                    switch (arg.Args[1])
                    {
                        case "addonname":
                            panelCopy.AddonSettings.AddonName = String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "showaddons":
                            UIViewAddonOptionsPanel(player);
                            break;
                        case "panelenabled":
                            panelCopy.Enabled = !panelCopy.Enabled;
                            UIUpdateButton(player, "WC.Edit.Text.PanelEnabled", panelCopy.Enabled);
                            break;
                        case "displaybtnname":
                            panelCopy.DisplayButtonName = !panelCopy.DisplayButtonName;
                            UIUpdateButton(player, "WC.Edit.Text.DisplayBTNName", panelCopy.DisplayButtonName);
                            break;
                        case "showpermissionpanel":
                            panelCopy.ShowPanelPermission = !panelCopy.ShowPanelPermission;
                            UIUpdateButton(player, "WC.Edit.Text.ShowPermissionPanel", panelCopy.ShowPanelPermission);
                            break;
                        case "panelname":
                            panelCopy.PanelName = String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "buttonname":
                            panelCopy.ButtonName = String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "panelposition":
                            panelCopy.PagePosition = int.Parse(arg.Args[2]);
                            break;
                        case "buttonimage":
                            panelCopy.ButtonImage = String.Join(" ", arg.Args.Skip(2));

                            if (panelCopy.ButtonImage != string.Empty)
                            {
                                RegisterNewImage($"-button-{panelCopy.PanelName}-part", panelCopy.ButtonImage);
                            }
                            break;
                        case "textpanelimage":
                            panelCopy.PanelSettings.TextPanelImage = String.Join(" ", arg.Args.Skip(2));

                            if (panelCopy.PanelSettings.TextPanelImage != string.Empty)
                            {
                                RegisterNewImage($"-text-{panelCopy.PanelName}-part", panelCopy.PanelSettings.TextPanelImage);
                            }
                            break;
                        case "panelimage":
                            panelCopy.PanelSettings.PanelImage = String.Join(" ", arg.Args.Skip(2));

                            if (panelCopy.PanelSettings.PanelImage != string.Empty)
                            {
                                RegisterNewImage($"-bottom-{panelCopy.PanelName}-part", panelCopy.PanelSettings.PanelImage);
                            }
                            break;
                        case "nopermtext":
                            panelCopy.PanelNoPermissionText = String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "panelpermission":
                            panelCopy.PanelPermission = String.Join(" ", arg.Args.Skip(2));
                            if (!string.IsNullOrEmpty(panelCopy.PanelPermission)) permission.RegisterPermission(panelCopy.PanelPermission, this);
                            break;
                        case "commandnumber":
                            if(editor.PanelCopy.Commands.Count == 0) return;

                            if (arg.Args[2] == "+") editor.CommandNumber++;
                            else editor.CommandNumber--;

                            var maxPage = editor.PanelCopy.Commands.Count - 1;
                            if (editor.CommandNumber < 0) editor.CommandNumber = maxPage;
                            else if (editor.CommandNumber > maxPage) editor.CommandNumber = 0;

                            UIUpdateLabel(player, "WC.Edit.Text.CurrentCommand", editor.PanelCopy.Commands[editor.CommandNumber]);
                            break;
                        case "deletecommand":
                            if (editor.PanelCopy.Commands.Count == 0) return;

                            editor.PanelCopy.Commands.RemoveAt(editor.CommandNumber);
                            editor.CommandNumber = 0;

                            if(editor.PanelCopy.Commands.Count == 0) UIUpdateLabel(player, "WC.Edit.Text.CurrentCommand", "No commands");
                            else UIUpdateLabel(player, "WC.Edit.Text.CurrentCommand", editor.PanelCopy.Commands[editor.CommandNumber]);
                            break;
                        case "newcommand":
                            editor.CommandHolder = String.Join(" ", arg.Args.Skip(2));
                            break;
                        case "addcommand":
                            editor.PanelCopy.Commands.Add(editor.CommandHolder);
                            editor.CommandHolder = String.Empty;
                            editor.CommandNumber = editor.PanelCopy.Commands.Count - 1;

                            UIUpdateLabel(player, "WC.Edit.Text.CurrentCommand", editor.PanelCopy.Commands[editor.CommandNumber]);
                            UIUpdateInputValue(player, "WC.Edit.Text.NewCommand", "");
                            break;
                        case "addline":
                            editor.PanelCopy.PanelSettings.PanelPages[editor.LinesPage].Add(string.Empty);
                            UIDrawTextLines(player, editor.PanelCopy, editor);
                            break;
                        case "addpage":
                            editor.PanelCopy.PanelSettings.PanelPages.Add(new List<string> { "Hello" });

                            var totalPages = editor.PanelCopy.PanelSettings.PanelPages.Count;
                            editor.LinesPage = totalPages - 1;

                            UIUpdateLabel(player, "WC.Edit.Text.Page", $"{editor.LinesPage + 1} / {totalPages}");
                            UIDrawTextLines(player, editor.PanelCopy, editor);
                            break;
                        case "page":
                            totalPages = editor.PanelCopy.PanelSettings.PanelPages.Count;
                            if (totalPages == 1) return;

                            if (arg.Args[2] == "-") editor.LinesPage--;
                            else editor.LinesPage++;

                            if (editor.LinesPage < 0) editor.LinesPage = totalPages - 1;
                            else if (editor.LinesPage + 1 > totalPages) editor.LinesPage = 0;

                            UIUpdateLabel(player, "WC.Edit.Text.Page", $"{editor.LinesPage + 1} / {totalPages}");
                            UIDrawTextLines(player, editor.PanelCopy, editor);
                            break;
                        case "line":
                            int line = int.Parse(arg.Args[2]);

                            editor.PanelCopy.PanelSettings.PanelPages[editor.LinesPage][line] = String.Join(" ", arg.Args.Skip(3));
                            break;
                        case "removeline":
                            line = int.Parse(arg.Args[2]);

                            editor.PanelCopy.PanelSettings.PanelPages[editor.LinesPage].RemoveAt(line);
                            if(editor.PanelCopy.PanelSettings.PanelPages[editor.LinesPage].Count == 0 && editor.PanelCopy.PanelSettings.PanelPages.Count > 1)
                            {
                                editor.PanelCopy.PanelSettings.PanelPages.RemoveAt(editor.LinesPage);
                                editor.LinesPage = 0;
                            } 

                            UIDrawTextLines(player, editor.PanelCopy, editor);
                            break;
                        case "delete":
                            var idx = _config.UIPanels.FindIndex(x => x.PanelName == editor.PanelCopyName);
                            _config.UIPanels.RemoveAt(idx);

                            CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
                            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");

                            editor.PanelCopyName = string.Empty;
                            editor.PanelCopy = null;
                            UILoadInfoPanel(player);
                            SaveConfig();
                            UICreateCFGSelector(player);
                            break;
                        case "save":
                            idx = _config.UIPanels.FindIndex(x => x.PanelName == editor.PanelCopyName);

                            foreach (var command in editor.PanelCopy.Commands)
                            {
                                if (!_config.UIPanels[idx].Commands.Contains(command)) cmd.AddChatCommand(command, this, UIOpenWelcomeMenuPanels);
                            }

                            _config.UIPanels[idx] = editor.PanelCopy.Clone();
                            CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
                            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");

                            InfoPanelSettings info = _config.UIPanels[idx];
                            if (info != null) UILoadInfoPanel(player, info: info);

                            editor.PanelCopyName = string.Empty;
                            editor.PanelCopy = null;
                            SaveConfig();
                            UICreateCFGSelector(player);
                            break;
                    }
                    break;
                case "customcolors":
                    CMDCustomColors(player, arg.Args);
                    break;
                case "startselectparent":
                    if (!_selectingParents.Contains(player.userID)) 
                    {
                        _selectingParents.Add(player.userID);
                        UIUpdateButtonColor(player, "WC.Edit.Panel.Parent.Button", "0.17 0.79 0.8 .5");
                        timer.Once(10f, () => TryChangeSelectParent(player));
                    }
                    else
                    {
                        _selectingParents.Remove(player.userID);
                        UIUpdateButtonColor(player, "WC.Edit.Panel.Parent.Button", "0 0 0 .5");
                    }
                    break;
                case "createpanel":
                    editor = _uiEditors[player];

                    String b = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    String random = "";

                    for (int i = 0; i < 6; i++)
                    {
                        int a = rnd.Next(26);
                        random += b.ElementAt(a);
                    }

                    editor.CreatingPanel = new UIParts()
                    {
                        UIName = random,
                        FullyEdit = true,
                        UIAnchors = new UIAnchorPositions()
                        {
                            XMin = 0,
                            YMin = .5,
                            XMax = .5,
                            YMax = 1
                        },
                        UIProperties = new UIProperties()
                        {
                            Color = "0 0 0 .5"
                        }
                    };

                    if (!_selectingParents.Contains(player.userID)) _selectingParents.Add(player.userID); 

                    UICreatePanelPopup(player);
                    timer.Once(10f, () => DeleteAndRemoveCreatingPanel(player, editor));
                    break;
                case "clonepanel":
                    editor = _uiEditors[player];

                    b = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    random = "";

                    for (int i = 0; i < 3; i++)
                    {
                        int a = rnd.Next(26);
                        random += b.ElementAt(a);
                    }

                    editor.CreatingPanel = editor.OpenPanel.Clone();
                    editor.CreatingPanel.UIName += $"_{random}";

                    if (!_selectingParents.Contains(player.userID)) _selectingParents.Add(player.userID);

                    UICreatePanelPopup(player);
                    timer.Once(10f, () => DeleteAndRemoveCreatingPanel(player, editor));
                    break;
                case "deletepanel":
                    ConfirmPanelDelete(player);
                    break;
                case "canceldelete":
                    CuiHelper.DestroyUi(player, "WCConfirmDeletePanel");
                    break;
                case "confirmdelete":
                    editor = _uiEditors[player];
                    CuiHelper.DestroyUi(player, editor.OpenPanel.UIName);

                    FindAndRemoveUIPart(editor.UICopy, editor.OpenPanel.UIName);
                    editor.OpenPanel = editor.UICopy.First();
                    CuiHelper.DestroyUi(player, "WCConfirmDeletePanel");
                    UICreateUIList(player, editor.UICopy);
                    break;
                case "movepanel":
                    editor = _uiEditors[player];
                    UIParts foundParent = new UIParts();
                    panelNames = arg.Args[2].Split('.').Skip(4).ToArray();

                    string neededName = panelNames.Last();
                    if (neededName == "text") neededName = panelNames[panelNames.Count() - 2];

                    foundParent = foundParent.FindParentByName(neededName, editor.UICopy);
                    if (string.IsNullOrEmpty(foundParent?.UIName)) return;

                    var childIndex = foundParent.UIChildren.FindIndex(x => x.UIName == neededName);
                    if (childIndex == -1) return;

                    int totalChildren = foundParent.UIChildren.Count - 1;
                    int swapIndex = -1;

                    if (totalChildren < 1) return;

                    if (arg.Args[1] == "up")
                    {
                        swapIndex = childIndex - 1;
                        if (childIndex == 0) swapIndex = totalChildren;
                    } else
                    {
                        swapIndex = childIndex + 1;
                        if (childIndex == totalChildren) swapIndex = 0;
                    }

                    UIParts tempParts = foundParent.UIChildren[swapIndex].Clone();
                    foundParent.UIChildren[swapIndex] = foundParent.UIChildren[childIndex];
                    foundParent.UIChildren[childIndex] = tempParts;
                    UICreateUIList(player, editor.UICopy);

                    break;
                case "createnewinfo":
                    b = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    random = "";

                    for (int i = 0; i < 6; i++)
                    {
                        int a = rnd.Next(26);
                        random += b.ElementAt(a);
                    }

                    InfoPanelSettings newInfoPanel = new InfoPanelSettings()
                    {
                        Enabled = false,
                        ButtonName = random,
                        PanelName = random,
                        Commands = new List<string> { random },
                        PanelSettings = new PanelSettings()
                        {
                            PanelPages = new List<List<string>>()
                            {
                                new List<string> {
                                    "Line 1",
                                    "Line 2"
                                }
                            }
                        }
                    };

                    _config.UIPanels.Add(newInfoPanel);

                    editor = _uiEditors[player];
                    editor.PanelCopy = _config.UIPanels.Last().Clone();
                    editor.PanelCopyName = editor.PanelCopy.PanelName;
                    UICreateTextPanelEditor(player);
                    break;
                case "createnewaddon":
                    b = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    random = "";

                    for (int i = 0; i < 6; i++)
                    {
                        int a = rnd.Next(26);
                        random += b.ElementAt(a);
                    }

                    InfoPanelSettings newAddonPanel = new InfoPanelSettings()
                    {
                        Enabled = false,
                        ButtonName = random,
                        PanelName = random,
                        Commands = new List<string> { random },
                        AddonSettings = new AddonSettings()
                        {
                            AddonName = "KitController"
                        }
                    };

                    _config.UIPanels.Add(newAddonPanel);

                    editor = _uiEditors[player];
                    editor.PanelCopy = _config.UIPanels.Last().Clone();
                    editor.PanelCopyName = editor.PanelCopy.PanelName;
                    UICreatePluginColorEditor(player);
                    GetPluginColors("KitController");
                    break;
            }
        }

        void ReverseImageChanges(List<UIParts> uiParts)
        {
            foreach (var panel in uiParts)
            {
                if (!string.IsNullOrEmpty(panel.UIProperties?.Image)) RegisterNewImage(panel.UIName, panel.UIProperties.Image);

                if (panel.UIChildren.Count > 0)
                {
                    ReverseImageChanges(panel.UIChildren);
                }
            }
        }

        void DeleteAndRemoveCreatingPanel(BasePlayer player, UIEditor editor)
        {
            CuiHelper.DestroyUi(player, "WCCreatePanelPopup");
            editor.CreatingPanel = null;
        }

        void ConfirmPanelDelete(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UIEditor editor = _uiEditors[player];

            var panel = CreatePanel(ref container, ".55 .75", ".69 .9", ".17 .17 .17 1", "Overlay", "WCConfirmDeletePanel");
            CreateLabel(ref container, ".02 .4", ".98 .96", ".2 .2 .2 1", "1 1 1 1", $"Are you sure you want to delete {editor.OpenPanel.UIName}?", 13, TextAnchor.UpperCenter, panel);
            CreateButton(ref container, ".02 .04", ".49 .36", "1 1 1 .3", "1 1 1 1", "CLOSE", 15, "wc_editor canceldelete", panel);
            CreateButton(ref container, ".51 .04", ".98 .36", "1 0.12 0.12 .4", "1 1 1 1", "DELETE", 15, "wc_editor confirmdelete", panel);

            CuiHelper.DestroyUi(player, "WCConfirmDeletePanel");
            CuiHelper.AddUi(player, container);
        }

        void TryChangeSelectParent(BasePlayer player)
        {
            if (!_selectingParents.Contains(player.userID)) return;
            _selectingParents.Remove(player.userID);

            UIUpdateButtonColor(player, "WC.Edit.Panel.Parent.Button", "0 0 0 .5");
        }

        List<ulong> _selectingParents = new List<ulong>();

        void SendPanelUpdate(BasePlayer player, UIEditor editor)
        {
            CuiHelper.DestroyUi(player, editor.OpenPanel.UIName);
            UIParts  parentPart = editor.OpenPanel.FindParent(editor.UICopy);

            if (parentPart != null) UILoadPanels(player, new List<UIParts> { editor.OpenPanel }, null, null, true, parentPart.UIName);
            else UILoadPanels(player, editor.UICopy, null);
        }

        private void UpdateMatchingUIPart(List<UIParts> uiPartsList, UIParts targetUIPart)
        {
            foreach (var uiPart in uiPartsList)
            {
                if (uiPart.UIName == targetUIPart.UIName)
                {
                    uiPart.UIAnchors = targetUIPart.UIAnchors;
                    uiPart.UIOffsets = targetUIPart.UIOffsets;
                    uiPart.UIProperties = targetUIPart.UIProperties;
                    uiPart.UIText = targetUIPart.UIText;
                    uiPart.UILoopSettings = targetUIPart.UILoopSettings;
                    uiPart.UIChildren = targetUIPart.UIChildren;
                    return;
                }

                if (uiPart.UIChildren.Count > 0)
                {
                    UpdateMatchingUIPart(uiPart.UIChildren, targetUIPart);
                }
            }
        }

        UIParts GetUISection(ref UIParts uiParts, string panelName)
        {
            return uiParts.UIChildren.FirstOrDefault(x => x.UIName == panelName);
        }

        [ConsoleCommand("wc_main")]
        private void CMDWelcomeController(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!CheckCooldown(player.userID)) return;

            switch (arg.Args[0])
            {
                case "close":
                    foreach(var mainUi in _config.CurrentTheme.Value.UIParts) CuiHelper.DestroyUi(player, mainUi.UIName);
                    break;
                case "test":
                    //UILoadPanels(player, _config.CurrentTheme.Value.UIParts);
                    break;
                case "page":
                    var panelName = String.Join(" ", arg.Args.Skip(3));
                    var panel = _config.UIPanels.FirstOrDefault(x => x.PanelName == panelName);
                    int page = int.Parse(arg.Args[2]);
                    int totalPages = panel.PanelSettings.PanelPages.Count - 1;

                    if (arg.Args[1] == "next") page += 1;
                    else page -= 1;

                    if (page > totalPages) page = 0;
                    if (page < 0) page = totalPages;

                    UILoadInfoPanel(player, panel, page: page);
                    break;
                case "panel":
                    panelName = String.Join(" ", arg.Args.Skip(1));
                    panel = _config.UIPanels.FirstOrDefault(x => x.PanelName == panelName);

                    bool hasPerms = true;
                    if (!string.IsNullOrEmpty(panel.PanelPermission) && !permission.UserHasPermission(player.UserIDString, panel.PanelPermission))
                    {
                        if (!panel.ShowPanelPermission)
                        {
                            SendReply(player, panel.PanelNoPermissionText);
                            return;
                        }

                        hasPerms = false;
                    }

                    UILoadInfoPanel(player, panel, hasPerms: hasPerms);
                    AddButtonOverlay(player, $"ButtonSettings.{panelName}", _config.CurrentTheme.Value.UIParts);
                    break;
            }
        }
        #endregion

        #region [ HOOKS ]
        void OnServerInitialized(bool initial)
        {
            RegisterCommandsAndPermissions();
            timer.Once(5f, () => ImportImages());
        }

        void OnPlayerConnected(BasePlayer player)
        {
            var firstOpenPanel = _config.UIPanels.FirstOrDefault(x => x.Enabled);
            if (firstOpenPanel == null) return;
            UIOpenWelcomeMenuPanels(player, firstOpenPanel.Commands[0], null);
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "WC.Edit.List.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CFG.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Color.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");

                    CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
                    CuiHelper.DestroyUi(player, "WC.Edit.Panel.Main");
                    foreach (var mainUi in _config.CurrentTheme.Value.UIParts) CuiHelper.DestroyUi(player, mainUi.UIName);
                }

            _config = null;
        }
        #endregion

        #region [ METHODS ]
        bool CheckCooldown(ulong steamID)
        {
            if (_cooldowns.ContainsKey(steamID))
            {
                if (_cooldowns[steamID].Subtract(DateTime.Now).TotalSeconds >= 0) return false;
                else
                {
                    _cooldowns[steamID] = DateTime.Now.AddSeconds(0.5f);
                    return true;
                }
            }
            else _cooldowns[steamID] = DateTime.Now.AddSeconds(0.5f);
            return true;
        }

        static readonly Dictionary<string, Func<string>> TagReplacements = new Dictionary<string, Func<string>>
        {
            { "{worldSize}", () => $"{ConVar.Server.worldsize}" },
            { "{worldSeed}", () => $"{ConVar.Server.seed}" },
            { "{hostName}", () => $"{ConVar.Server.hostname}" },
            { "{maxPlayers}", () => $"{ConVar.Server.maxplayers}" },
            { "{online}", () => $"{BasePlayer.activePlayerList.Count()}" },
            { "{sleeping}", () => $"{BasePlayer.sleepingPlayerList.Count()}" },
            { "{joining}", () => $"{ServerMgr.Instance.connectionQueue.Joining}" },
            { "{queued}", () => $"{ServerMgr.Instance.connectionQueue.Queued}" },
        };

        private static string AlterTextTags(string textLine)
        {
            foreach (var tag in TagReplacements)
            {
                if (textLine.Contains(tag.Key))
                {
                    textLine = textLine.Replace(tag.Key, tag.Value());
                }
            }

            textLine = Regex.Replace(textLine, @"\[size=(\d+)\]", "<size=$1>");
            textLine = Regex.Replace(textLine, @"\[/size\]", "</size>");
            textLine = Regex.Replace(textLine, @"\[color=(#[0-9A-Fa-f]{6})\]", "<color=$1>");
            textLine = Regex.Replace(textLine, @"\[/color\]", "</color>");

            return textLine;
        }

        void RegisterCommandsAndPermissions()
        {
            if (!_config.DisplayOnJoin) Unsubscribe(nameof(OnPlayerConnected));

            for (int i = 0; i < _config.UIPanels.Count; i++)
            {
                var thePanel = _config.UIPanels[i];
                if (!thePanel.Enabled) continue;

                foreach (var command in thePanel.Commands) cmd.AddChatCommand(command, this, UIOpenWelcomeMenuPanels);
                if (!string.IsNullOrEmpty(thePanel.PanelPermission)) permission.RegisterPermission(thePanel.PanelPermission, this);
            }

            cmd.AddChatCommand("welcomeedit", this, UIOpenEditPanel);
            permission.RegisterPermission("welcomecontroller.admin", this);

            cmd.AddChatCommand("welcomethemes", this, OpenThemeSelector);

            //cmd.AddChatCommand("welcomeedit", this, UIOpenEditMenu);
        }

        void UIFlashPanelColor(BasePlayer player, string panelName, string color)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement()
            {
                Name = panelName,
                Update = true,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = color
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        public string ConvertHexColorToRgba(string hexColor, float? alphaOverride = null)
        {
            if (string.IsNullOrWhiteSpace(hexColor)) return hexColor;

            if (hexColor.StartsWith("#")) hexColor = hexColor.TrimStart('#'); 
            else return hexColor;

            if (hexColor.Length != 6 && hexColor.Length != 8) return hexColor;

            if (!int.TryParse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier, null, out int red) || !int.TryParse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier, null, out int green) || !int.TryParse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier, null, out int blue))
                return hexColor;

            double normalizedRed = Math.Round((double)red / 255, 2);
            double normalizedGreen = Math.Round((double)green / 255, 2);
            double normalizedBlue = Math.Round((double)blue / 255, 2);

            double alpha = 1.0;
            if (hexColor.Length == 8)
            {
                if (!int.TryParse(hexColor.Substring(6, 2), NumberStyles.AllowHexSpecifier, null, out int alphaHex)) return hexColor;

                alpha = Math.Round((double)alphaHex / 255, 2);
            }

            alpha = alphaOverride.HasValue ? Clamp(alphaOverride.Value, 0, 1) : alpha;

            return $"{normalizedRed} {normalizedGreen} {normalizedBlue} {alpha}";
        }

        private static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        void ImportImages()
        {
            Dictionary<string, string> images = new Dictionary<string, string> {
                { "UIAnchorsChart", "https://i.ibb.co/FqHct13/Anchors-Chart2.png" },
                { "UIOffsetsChart", "https://i.ibb.co/LvWzkrH/Offsets-Chart.png" },
                { "UITextPanelImg", "https://i.ibb.co/Khn3StM/Text-Panel-Settings.png"},
                { "UIPropertiesChart", "https://i.ibb.co/GFvD3bP/Properties-Settings.png" },
                { "UICustomColors", "https://i.ibb.co/jwywxH7/Custom-Colors.png" },
                { "UIPluginColors", "https://i.ibb.co/r7RTVg2/Addons-Editor.png" }
            };

            foreach (var panel in _config.UIPanels)
            {
                if (!string.IsNullOrEmpty(panel.ButtonImage)) images[$"UI-button-{panel.PanelName}-part"] = panel.ButtonImage;
                if (!string.IsNullOrEmpty(panel.PanelSettings.TextPanelImage)) images[$"UI-text-{panel.PanelName}-part"] = panel.PanelSettings.TextPanelImage;
                if (!string.IsNullOrEmpty(panel.PanelSettings.PanelImage)) images[$"UI-bottom-{panel.PanelName}-part"] = panel.PanelSettings.PanelImage;
            }

            GetPanelImage(ref images, _config.CurrentTheme.Value.UIParts);

            ImageLibrary?.Call("ImportImageList", "WelcomeController", images, 0UL, true, null);
        }

        void GetPanelImage(ref Dictionary<string, string> images, List<UIParts> uiParts)
        {
            foreach (var panel in uiParts)
            {
                if (!string.IsNullOrEmpty(panel.UIProperties.Image)) images[$"UI{panel.UIName}"] = panel.UIProperties.Image;

                if(panel.UIChildren.Count > 0) GetPanelImage(ref images, panel.UIChildren);
            }
        }

        private string GetImage(string imageName)
        {
            if (ImageLibrary == null)
            {
                PrintError("Could not load images due to no Image Library");
                return null;
            }

            return ImageLibrary?.Call<string>("GetImage", "UI" + imageName, 0UL, false);
        }

        private void RegisterNewImage(string imageName, string imageUrl)
        {
            ImageLibrary?.Call("AddImage", imageUrl, "UI" + imageName, 0UL, null);
        }
        #endregion

        #region [ EDITOR UI ]
        void CreateEditorUI(BasePlayer player)
        {
            foreach(var uiName in _config.CurrentTheme.Value.UIParts) CuiHelper.DestroyUi(player, uiName.UIName);

            UILoadPanels(player, _uiEditors[player].UICopy);
            UICreateCFGSelector(player);
            UICreateUIList(player, _uiEditors[player].UICopy);
            UICreateColorCreator(player);
            UICreatePanelEditor(player);

            if (_config.UseLinks) Interface.CallHook("OnWCRequestedUIPanel", player, "WCSourcePanel", "WUIAttachments_SocialLinks");
        }

        List<string> colors = new List<string>
        {
            "1 0.58 0.58 0.6",
            "1 0.46 0.46 0.6",
            "1 0.29 0.29 0.6",
            "1 0.21 0.21 0.6",
            "1 0.1 0.1 0.6",
            "1 0 0 0.6",
        
            "1 0.67 0.58 0.6",
            "1 0.58 0.46 0.6",
            "1 0.44 0.29 0.6",
            "1 0.38 0.21 0.6",
            "1 0.29 0.1 0.6",
            "1 0.22 0 0.6",
        
            "1 0.81 0.58 0.6",
            "1 0.76 0.46 0.6",
            "1 0.68 0.29 0.6",
            "1 0.64 0.21 0.6",
            "1 0.6 0.1 0.6",
            "1 0.55 0 0.6",
        
            "0.99 1 0.59 0.6",
            "0.99 1 0.46 0.6",
            "0.99 1 0.29 0.6",
            "0.99 1 0.21 0.6",
            "0.98 1 0.1 0.6",
            "0.98 1 0 0.6",
        
            "0.61 1 0.58 0.6",
            "0.49 1 0.46 0.6",
            "0.34 1 0.29 0.6",
            "0.26 1 0.21 0.6",
            "0.16 1 0.1 0.6",
            "0.07 1 0 0.6",
        
            "0.59 0.99 1 0.6",
            "0.46 0.99 1 0.6",
            "0.29 0.99 1 0.6",
            "0.21 0.99 1 0.6",
            "0.1 0.98 1 0.6",
            "0 0.98 1 0.6",
        
            "0.58 0.81 1 0.6",
            "0.46 0.76 1 0.6",
            "0.29 0.68 1 0.6",
            "0.21 0.64 1 0.6",
            "0.1 0.6 1 0.6",
            "0 0.55 1 0.6",
        
            "0.58 0.67 1 0.6",
            "0.46 0.58 1 0.6",
            "0.29 0.44 1 0.6",
            "0.21 0.38 1 0.6",
            "0.1 0.29 1 0.6",
            "0 0.22 1 0.6",
        
            "0.58 0.58 1 0.6",
            "0.46 0.46 1 0.6",
            "0.29 0.29 1 0.6",
            "0.21 0.21 1 0.6",
            "0.1 0.1 1 0.6",
            "0 0 1 0.6",
        
            "0.83 0.58 1 0.6",
            "0.78 0.46 1 0.6",
            "0.72 0.29 1 0.6",
            "0.68 0.21 1 0.6",
            "0.64 0.1 1 0.6",
            "0.6 0 1 0.6",
        
            "1 0.58 0.96 0.6",
            "1 0.46 0.95 0.6",
            "1 0.29 0.93 0.6",
            "1 0.21 0.92 0.6",
            "1 0.1 0.91 0.6",
            "1 0 0.9 0.6",
        
            "0.73 0.75 0.74 0.6",
            "0.64 0.65 0.64 0.6",
            "0.54 0.54 0.53 0.6",
            "0.42 0.42 0.42 0.6",
            "0.33 0.32 0.32 0.6",
            "0.22 0.22 0.22 0.6"
        };

        void UICreatePanelPopup(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, ".23 .75", ".4 .9", ".17 .17 .17 1", "Overlay", "WCCreatePanelPopup");
            var sidePanel = CreatePanel(ref container, "-.1 .4", ".1 .6", ".17 .17 .17 1", panel);
            CreateLabel(ref container, "0 0", ".5 1", "0 0 0 0", "1 1 1 1", "<-", 14, TextAnchor.MiddleCenter, sidePanel);
            CreateLabel(ref container, ".02 .04", ".98 .96", ".2 .2 .2 1", "1 1 1 1", "You're currently creating a new panel! Please select a parent for the panel to be attached to!", 13, TextAnchor.MiddleCenter, panel);

            CuiHelper.DestroyUi(player, "WCCreatePanelPopup");
            CuiHelper.AddUi(player, container);
        }

        void UIUpdateInputValue(BasePlayer player, string UIName, string DoubleValue)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = DoubleValue,
                        NeedsKeyboard = true,
                    }
                },
                Name = UIName,
                Update = true
            });

            CuiHelper.AddUi(player, container);
        }

        void UIUpdatePanel(BasePlayer player, string UIName, string ColorValue, CuiRectTransformComponent rect = null)
        {
            CuiElementContainer container = new CuiElementContainer();

            CuiElement newElement = new CuiElement
            {
                Name = UIName,
                Update = true
            };

            if (rect != null) newElement.Components.Add(rect);
            else newElement.Components.Add(new CuiImageComponent()
            {
                Color = ColorValue
            });

            container.Add(newElement);

            CuiHelper.AddUi(player, container);
        }

        void UIUpdateLabel(BasePlayer player, string UIName, string Label, CuiRectTransformComponent rect = null)
        {
            CuiElementContainer container = new CuiElementContainer();

            CuiElement newElement = new CuiElement
            {
                Name = UIName,
                Update = true
            };

            if (rect != null) newElement.Components.Add(rect);
            else newElement.Components.Add(new CuiTextComponent()
            {
                Text = Label,
            });

            container.Add(newElement);

            CuiHelper.AddUi(player, container);
        }

        void UIUpdateButton(BasePlayer player, string UIName, bool blur, string newString = null)
        {
            CuiElementContainer container = new CuiElementContainer();

            CuiElement newElement = new CuiElement
            {
                Name = UIName,
                Update = true
            };

            if (newString != null) newElement.Components.Add(new CuiButtonComponent() { Command = newString });
            else newElement.Components.Add(new CuiButtonComponent()
            {
                Color = blur ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
            });

            container.Add(newElement);

            CuiHelper.AddUi(player, container);
        }

        void UIUpdateButtonColor(BasePlayer player, string UIName, string color)
        {
            CuiElementContainer container = new CuiElementContainer();

            CuiElement newElement = new CuiElement
            {
                Name = UIName,
                Update = true
            };

            newElement.Components.Add(new CuiButtonComponent()
            {
                Color = color,
            });

            container.Add(newElement);

            CuiHelper.AddUi(player, container);
        }

        void UICreateTextTagsPopup(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, ".6 .15", ".69 .55", ".17 .17 .17 1", "Overlay", "WCCreateTextPopup");
            CreateLabel(ref container, ".03 .013", ".968 .986", ".2 .2 .2 1", "1 1 1 1", "<size=17>TAGS</size>\n\n{worldSize}\n{worldSeed}\n{hostName}\n{maxPlayers}\n{online}\n{sleeping}\n{joining}\n{queued}", 13, TextAnchor.UpperCenter, panel);
            CreateCloseButton(ref container, ".03 .013", ".968 .1", "0 0 0 .4", "1 1 1 1", "CLOSE", 15, "WCCreateTextPopup", panel);

            CuiHelper.DestroyUi(player, "WCCreateTextPopup");
            CuiHelper.AddUi(player, container);
        }

        void UICreatePanelEditor(BasePlayer player)
        {
            UIParts uIParts = _uiEditors[player].OpenPanel;
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, ".705 .01", ".995 .99", ".17 .17 .17 .8", "Overlay", "WC.Edit.Panel.Main");
            CreateLabel(ref container, "0 .96", ".997 .999", "0 0 0 .5", "1 1 1 1", "PANEL EDITOR", 20, TextAnchor.MiddleCenter, panel);

            if(uIParts.FullyEdit) CreateButton(ref container, ".01 .925", ".495 .955", "1 0.25 0.14 .4", "1 1 1 1", "DELETE PANEL", 15, "wc_editor deletepanel", panel);
            CreateButton(ref container, ".505 .925", ".99 .955", "0.27 1 0.98 .4", "1 1 1 1", "CLONE PANEL", 15, "wc_editor clonepanel", panel);

            // Anchors Part
            CreateImagePanel(ref container, ".01 .76", ".99 .92", GetImage("AnchorsChart"), panel);

            CreateInput(ref container, ".035 .818", ".195 .847", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIAnchors.YMax}", 13, "wc_editor anchor ymax", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Anchors.YMax");
            CreateButton(ref container, ".035 .775", ".11 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor ymax +", panel);
            CreateButton(ref container, ".12 .775", ".195 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor ymax -", panel);

            CreateInput(ref container, ".22 .818", ".383 .847", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIAnchors.YMin}", 13, "wc_editor anchor ymin", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Anchors.YMin");
            CreateButton(ref container, ".22 .775", ".295 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor ymin +", panel);
            CreateButton(ref container, ".305 .775", ".383 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor ymin -", panel);

            CreateInput(ref container, ".408 .818", ".568 .847", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIAnchors.XMin}", 13, "wc_editor anchor xmin", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Anchors.XMin");
            CreateButton(ref container, ".408 .775", ".483 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xmin -", panel);
            CreateButton(ref container, ".493 .775", ".568 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xmin +", panel);

            CreateInput(ref container, ".593 .818", ".753 .847", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIAnchors.XMax}", 13, "wc_editor anchor xmax", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Anchors.XMax");
            CreateButton(ref container, ".593 .775", ".668 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xmax -", panel);
            CreateButton(ref container, ".678 .775", ".753 .811", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xmax +", panel);

            CreateButton(ref container, ".84 .85", ".9 .88", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor yup", panel);
            CreateButton(ref container, ".84 .783", ".9 .818", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor ydown", panel);

            CreateButton(ref container, ".78 .818", ".84 .847", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xleft", panel);
            CreateButton(ref container, ".9 .818", ".964 .847", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor anchor xright", panel);

            // Offsets Part
            CreateImagePanel(ref container, ".01 .595", ".99 .755", GetImage("OffsetsChart"), panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".78 .735",
                       AnchorMax = ".99 .755",
                   },
                   new CuiButtonComponent()
                   {
                       Color = uIParts.UIOffsets.Enabled ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor offset enabled"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Panel.Offsets.Enabled"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 13,
                       Text = "ENABLED"
                    },
                },
                Parent = "WC.Edit.Panel.Offsets.Enabled"
            });

            CreateInput(ref container, ".035 .653", ".195 .682", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIOffsets.YMax}", 13, "wc_editor offset ymax", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Offsets.YMax");
            CreateButton(ref container, ".035 .61", ".11 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset ymax +", panel);
            CreateButton(ref container, ".12 .61", ".195 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset ymax -", panel);

            CreateInput(ref container, ".22 .653", ".383 .682", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIOffsets.YMin}", 13, "wc_editor offset ymin", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Offsets.YMin");
            CreateButton(ref container, ".22 .61", ".295 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset ymin +", panel);
            CreateButton(ref container, ".305 .61", ".383 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset ymin -", panel);

            CreateInput(ref container, ".408 .653", ".568 .682", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIOffsets.XMin}", 13, "wc_editor offset xmin", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Offsets.XMin");
            CreateButton(ref container, ".408 .61", ".483 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xmin -", panel);
            CreateButton(ref container, ".493 .61", ".568 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xmin +", panel);

            CreateInput(ref container, ".593 .653", ".753 .682", "0.98 0.35 0.25 0", "1 1 1 1", $"{uIParts.UIOffsets.XMin}", 13, "wc_editor offset xmax", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Offsets.XMax");
            CreateButton(ref container, ".593 .61", ".668 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xmax -", panel);
            CreateButton(ref container, ".678 .61", ".753 .646", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xmax +", panel);

            CreateButton(ref container, ".84 .682", ".9 .718", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset yup", panel);
            CreateButton(ref container, ".84 .614", ".9 .652", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset ydown", panel);

            CreateButton(ref container, ".78 .652", ".84 .682", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xleft", panel);
            CreateButton(ref container, ".9 .652", ".964 .682", "0.98 0.35 0.25 0", "1 1 1 1", " ", 15, "wc_editor offset xright", panel);

            // Properties Chart
            CreateImagePanel(ref container, ".01 .245", ".99 .59", GetImage("PropertiesChart"), panel);
            CreateInput(ref container, ".016 .5", ".46 .54", "0.98 0.35 0.25 0", "1 1 1 1", uIParts.UIProperties.Color, 13, "wc_editor panelcolor", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Properties.Color");

            container.Add(new CuiElement
            {
                Components = {
                   new CuiImageComponent()
                   {
                       Color = uIParts.UIProperties.Color
                   },
                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".465 .501",
                       AnchorMax = ".535 .539"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Panel.Properties.ColorBlock"
            });

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".545 .501",
                       AnchorMax = ".98 .539",
                   },
                   new CuiButtonComponent()
                   {
                       Color = uIParts.UIProperties.Blur ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelblur"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Panel.Properties.Blur"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 13,
                       Text = "BLUR"
                    },
                },
                Parent = "WC.Edit.Panel.Properties.Blur"
            });

            CreateInput(ref container, ".016 .44", ".4975 .475", "0 0 0 0", "1 1 1 1", uIParts.UIProperties.Image, 13, "wc_editor panelimage", TextAnchor.MiddleCenter, panel);

            CreateInput(ref container, ".5025 .44", ".984 .475", "0 0 0 0", "1 1 1 1", $"{uIParts.UIProperties.FadeIn}", 13, "wc_editor panelfadein", TextAnchor.MiddleCenter, panel);

            CreateInput(ref container, ".014 .365", ".835 .402", "0 0 0 0", "1 1 1 1", $"{uIParts.UIText.Text}", 13, "wc_editor text text", TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".014 .365", ".05 .402", "0 0 0 0", "1 1 1 1", "?", 15, "wc_editor text checktags", panel);

            CreateButton(ref container, ".014 .325", ".08 .36", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text font next", panel);

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 12,
                       Text = uIParts.UIText.Font,
                       VerticalOverflow = VerticalWrapMode.Truncate
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = ".095 .325",
                        AnchorMax = ".36 .36"
                    },
                },
                Parent = panel,
                Name = "WC.Edit.Panel.Text.Font"
            });

            CreateButton(ref container, ".375 .325", ".441 .36", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text font last", panel);

            CreateInput(ref container, ".451 .325", ".64 .36", "0 0 0 0", "1 1 1 1", $"{uIParts.UIText.Color}", 13, "wc_editor text color", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Text.Color");
            CreateInput(ref container, ".65 .325", ".835 .36", "0 0 0 0", "1 1 1 1", $"{uIParts.UIText.Size}", 13, "wc_editor text size", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Text.Size+");

            CreateButton(ref container, ".845 .38", ".885 .402", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 0", panel);
            CreateButton(ref container, ".9 .38", ".935 .402", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 1", panel);
            CreateButton(ref container, ".95 .38", ".985 .402", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 2", panel);

            CreateButton(ref container, ".845 .355", ".885 .377", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 3", panel);
            CreateButton(ref container, ".9 .355", ".935 .377", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 4", panel);
            CreateButton(ref container, ".95 .355", ".985 .377", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 5", panel);

            CreateButton(ref container, ".845 .33", ".885 .352", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 6", panel);
            CreateButton(ref container, ".9 .33", ".935 .352", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 7", panel);
            CreateButton(ref container, ".95 .33", ".985 .352", "1 0.06 0.06 0", "1 1 1 1", " ", 15, "wc_editor text alignment 8", panel);

            // Spacing settings
            CreateButton(ref container, ".01 .245", ".09 .288", "1 1 1 0", "1 1 1 1", " ", 15, "wc_editor spacing change -", panel);
            CreateButton(ref container, ".49 .245", ".57 .288", "1 1 1 0", "1 1 1 1", " ", 15, "wc_editor spacing change +", panel);
            CreateInput(ref container, ".1 .245", ".48 .288", "0 0 0 0", "1 1 1 1", $"{uIParts.UILoopSettings.Spacing}", 13, "wc_editor spacing space", TextAnchor.MiddleCenter, panel, "WC.Edit.Panel.Spacing.Space");

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".58 .247",
                       AnchorMax = ".985 .288",
                   },
                   new CuiButtonComponent()
                   {
                       Color = "0.58 0.17 0.8 .4",
                       Command = "wc_editor spacing direction"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Panel.Spacing.Direction"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 13,
                       Text = $"CURRENTLY {(uIParts.UILoopSettings.Vertical ? "VERTICAL" : "HORIZONTAL")}"
                    },
                },
                Parent = "WC.Edit.Panel.Spacing.Direction",
                Name = "WC.Edit.Panel.Spacing.DirectionText"
            });

            if (uIParts.FullyEdit)
            {
                container.Add(new CuiElement
                {
                    Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".01 .18",
                       AnchorMax = ".495 .24",
                   },
                   new CuiButtonComponent()
                   {
                       Color = "0 0 0 .5",
                       Command = "wc_editor startselectparent"
                   }
                },
                    Parent = panel,
                    Name = "WC.Edit.Panel.Parent.Button"
                });

                container.Add(new CuiElement()
                {
                    Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 13,
                       Text = $"SELECT PARENT"
                    },
                },
                    Parent = "WC.Edit.Panel.Parent.Button",
                    Name = "WC.Edit.Panel.Parent.ButtonText"
                });

                CreateLabel(ref container, ".505 .22", ".99 .24", "0 0 0 .5", "1 1 1 1", "PANEL NAME", 11, TextAnchor.MiddleCenter, panel);
                var namePanel = CreatePanel(ref container, ".505 .18", ".99 .215", "0 0 0 .5", panel);
                CreateInput(ref container, "0 0", "1 1", "0 0 0 .5", "1 1 1 1", uIParts.UIName, 12, "wc_editor panelname", TextAnchor.MiddleCenter, namePanel);
            }

            GenerateCustomColorsOnPanel(player, container);

            int i = 0;
            int row = 0;
            foreach (var color in colors)
            {
                CreateButton(ref container, $"{.01 + (row * .0825)} {.005 + (i * .023)}", $"{.0825 + (row * .0825)} {.023 + (i * .023)}", color, "1 1 1 1", " ", 15, $"wc_editor panelcolor {color}", panel);
                if (i != 0 && i % 5 == 0)
                {
                    row++;
                    i = 0;
                } else i++;
            }

            CuiHelper.DestroyUi(player, "WC.Edit.Panel.Main");
            CuiHelper.AddUi(player, container);
        }

        void GenerateCustomColorsOnPanel(BasePlayer player, CuiElementContainer container = null, int page = 0)
        {
            bool containerWasNull = container == null;
            if (containerWasNull)
            {
                container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, "WC.Edit.Panel.CustomColors");
            }

            var maxPage = (_config.CustomColors.Count - 1) / 10;
            if (maxPage < page) page = 0;
            if (page < 0) page = maxPage;

            var panel = CreatePanel(ref container, "0 .145", "1 .175", "0 0 0 0", "WC.Edit.Panel.Main", "WC.Edit.Panel.CustomColors");
            CreateButton(ref container, ".01 0", ".0825 1", "0 0 0 .55", "1 1 1 1", "<", 15, $"wc_editor customcolorpage {page - 1}", panel);
            CreateButton(ref container, ".9175 0", ".99 1", "0 0 0 .55", "1 1 1 1", ">", 15, $"wc_editor customcolorpage {page + 1}", panel);

            int i = 0;
            foreach (var color in _config.CustomColors.Skip(page * 10).Take(10))
            {
                var xMin = .0925 + (i * .0825);
                var xMax = xMin + .0725;

                int indexNumber = i + (page * 5);
                CreateButton(ref container, $"{xMin} 0", $"{xMax} .97", color, "1 1 1 1", " ", 15, $"wc_editor panelcolor {color}", panel);

                i++;
            }

            if (containerWasNull) CuiHelper.AddUi(player, container);
        }

        void UICreateTextPanelEditor(BasePlayer player)
        {
            var editor = _uiEditors[player];
            var info = editor.PanelCopy;

            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, ".205 .01", ".7 .99", ".17 .17 .17 .8", "Overlay", "WC.Edit.Text.Main", true);
            //CreatePanel(ref container, "0 0", "1 1", "0 0 0 .8", panel);
            CreateImagePanel(ref container, "0 0", "1 1", GetImage("TextPanelImg"), panel);
            CreateLabel(ref container, "0 .96", ".997 .999", "0 0 0 .5", "1 1 1 1", "INFO PANEL EDITOR", 20, TextAnchor.MiddleCenter, panel);
            CreateCloseButton(ref container, ".95 .96", "1 1", "0 0 0 0", "1 1 1 1", "X", 15, "WC.Edit.Text.Main", panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".01 .8865",
                       AnchorMax = ".1945 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.Enabled ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings panelenabled"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.PanelEnabled"
            });

            CreateInput(ref container, ".405 .8865", ".595 .927", "0 0 0 .5", "1 1 1 1", info.ButtonImage, 12, "wc_editor panelsettings buttonimage", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".605 .8865", ".795 .927", "0 0 0 .5", "1 1 1 1", $"{info.PagePosition}", 12, "wc_editor panelsettings panelposition", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".405 .808", ".595 .85", "0 0 0 .5", "1 1 1 1", info.PanelSettings.PanelImage, 12, "wc_editor panelsettings panelimage", TextAnchor.MiddleCenter, panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".205 .8865",
                       AnchorMax = ".395 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.DisplayButtonName ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings displaybtnname"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.DisplayBTNName"
            });

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".805 .8865",
                       AnchorMax = ".99 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.ShowPanelPermission ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings showpermissionpanel"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.ShowPermissionPanel"
            });

            CreateInput(ref container, ".01 .808", ".195 .85", "0 0 0 .5", "1 1 1 1", info.PanelName, 12, "wc_editor panelsettings panelname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".205 .808", ".395 .85", "0 0 0 .5", "1 1 1 1", info.ButtonName, 12, "wc_editor panelsettings buttonname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".605 .808", ".795 .85", "0 0 0 .5", "1 1 1 1", info.PanelSettings.TextPanelImage, 12, "wc_editor panelsettings textpanelimage", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".805 .808", ".99 .85", "0 0 0 .5", "1 1 1 1", info.PanelPermission, 12, "wc_editor panelsettings panelpermission", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".805 .7295", ".99 .7715", "0 0 0 .5", "1 1 1 1", info.PanelNoPermissionText, 12, "wc_editor panelsettings nopermtext", TextAnchor.MiddleCenter, panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".06 .7295",
                       AnchorMax = ".3 .7715",
                   },
                   new CuiTextComponent()
                   {
                       Color = "1 1 1 1",
                       Text = info.Commands.Count > 0 ? info.Commands[editor.CommandNumber] : "No Commands",
                       Align = TextAnchor.MiddleCenter,
                       FontSize = 13,
                       Font = "robotocondensed-bold.ttf"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.CurrentCommand"
            });

            CreateButton(ref container, ".01 .7295", ".055 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings commandnumber -", panel);
            CreateButton(ref container, ".35 .7295", ".395 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings commandnumber +", panel);
            CreateButton(ref container, ".29 .7295", ".335 .7715", "0 0.8 0.25 0", "1 1 1 1", "X", 15, "wc_editor panelsettings deletecommand", panel);

            CreateInput(ref container, ".405 .7295", ".74 .7715", "0 0.8 0.25 0", "1 1 1 1", editor.CommandHolder, 12, "wc_editor panelsettings newcommand", TextAnchor.MiddleCenter, panel, "WC.Edit.Text.NewCommand");
            CreateButton(ref container, ".75 .7295", ".795 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings addcommand", panel);

            CreateButton(ref container, ".25 .005", ".4 .055", "0 0 0 .5", "1 1 1 1", "ADD LINE", 15, "wc_editor panelsettings addline", panel);
            CreateButton(ref container, ".405 .005", ".55 .055", "0 0 0 .5", "1 1 1 1", "ADD PAGE", 15, "wc_editor panelsettings addpage", panel);

            CreateButton(ref container, ".555 .005", ".795 .055", "0 0 0 .5", "1 1 1 1", "SAVE", 15, "wc_editor panelsettings save", panel);
            CreateButton(ref container, ".8 .005", ".989 .055", "1 0.31 0.2 .5", "1 1 1 1", "DELETE", 15, "wc_editor panelsettings delete", panel);

            CreateButton(ref container, ".01 .005", ".05 .055", "0 0 0 .5", "1 1 1 1", "<", 15, "wc_editor panelsettings page -", panel);
            CreateButton(ref container, ".205 .005", ".245 .055", "0 0 0 .5", "1 1 1 1", ">", 15, "wc_editor panelsettings page +", panel);

            container.Add(new CuiElement
            {
                Components = {
                    new CuiImageComponent()
                    {
                        Color = "0 0 0 .5"
                    },
                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".055 .005",
                       AnchorMax = ".2 .055"
                   }
                },
                Parent = panel,
            });

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".055 .005",
                       AnchorMax = ".2 .055"
                   },
                   new CuiTextComponent()
                   {
                       Color = "1 1 1 1",
                       Text = $"{editor.LinesPage + 1} / {info.PanelSettings.PanelPages.Count}",
                       Align = TextAnchor.MiddleCenter,
                       FontSize = 12,
                       Font = "robotocondensed-bold.ttf"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.Page"
            });

            UIDrawTextLines(player, info, editor, container);

            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
            CuiHelper.AddUi(player, container);
        }

        void UIDrawTextLines(BasePlayer player, InfoPanelSettings info, UIEditor editor, CuiElementContainer container = null)
        {
            bool containerWasNull = container == null;
            if (containerWasNull)
            {
                container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, "WC.Edit.Text.LinesPanel");
            }

            var totalLines = info.PanelSettings.PanelPages[editor.LinesPage].Count;
            var totalDepth = totalLines > 13 ? totalLines * .0769 : 0;

            container.Add(new CuiElement()
            {
                Name = "WC.Edit.Text.LinesPanel",
                Parent = "WC.Edit.Text.Main",
                Components =
                {
                    new CuiRectTransformComponent { AnchorMin = "0 .06", AnchorMax = ".99 .72" },
                    new CuiScrollViewComponent()
                    {
                        Horizontal = false,
                        Vertical = true,
                        MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                        Elasticity = .25f,
                        Inertia = true,
                        DecelerationRate = .3f,
                        ScrollSensitivity = 30f,
                        VerticalScrollbar = new CuiScrollbar {
                            Invert = false,
                            AutoHide = false,
                            HandleSprite = "assets/content/ui/ui.rounded.tga",
                            HandleColor = "1 1 1 .2",
                            HighlightColor = "1 1 1 .3",
                            TrackSprite = "assets/content/ui/ui.background.tile.psd",
                            TrackColor = ".09 .09 .09 0",
                            Size = 3,
                            PressedColor = "1 1 1 .4"
                        },
                        ContentTransform = new CuiRectTransform()
                        {
                            AnchorMin = $"0 {1 - (totalLines > 13 ? totalLines * .0769 : 1)}",
                            AnchorMax = $"1 1",
                        }
                    }
                }
            });

            int i = 0;
            var rowDepth = totalLines > 13 ? .0769 / totalDepth : .0769;
            var space = totalLines > 13 ? .01 / totalDepth : .01;

            foreach (var line in info.PanelSettings.PanelPages[editor.LinesPage])
            {
                double yMin = .70 - (i * .05);
                double yMax = .745 - (i * .05);

                var startHeight = 1 - (rowDepth * i);
                var endHeight = startHeight - rowDepth + space;

                var lne = CreatePanel(ref container, $".01 {endHeight}", $".93 {startHeight}", "0 0 0 .4", "WC.Edit.Text.LinesPanel");
                CreateInput(ref container, "0 0", "1 1", "0 0 0 .4", "1 1 1 1", line, 15, $"wc_editor panelsettings line {i}", TextAnchor.MiddleLeft, lne);
                CreateButton(ref container, $".937 {endHeight}", $".985 {startHeight}", "0 0 0 .6", "1 1 1 1", "-", 13, $"wc_editor panelsettings removeline {i}", "WC.Edit.Text.LinesPanel");
                i++;
            }

            if (containerWasNull) CuiHelper.AddUi(player, container);
        }

        void CMDOtherPluginColors(BasePlayer player, string[] args)
        {
            UIEditor editor = _uiEditors[player];
            switch (args[1])
            {
                case "changecolor":
                    string colorName = args[2];
                    string colorValue = ConvertHexColorToRgba(String.Join(" ", args.Skip(3)));
                    editor.PluginColors[colorName] = colorValue;
                    UIUpdatePanel(player, $"plugincolor_{colorName}", colorValue);
                    break;
                case "save":
                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    SaveConfig();
                    break;
            }
        }

        void UIViewAddonOptionsPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, ".405 .5", ".595 .8", ".17 .17 .17 1", "WC.Edit.PluginColors.Main", "WC.Edit.PluginColors.AddonsList");
            CreateLabel(ref container, ".03 .013", ".968 .986", ".2 .2 .2 1", "1 1 1 1", "<size=17>ADDONS</size>\n\nKitController\nShopController\nLoadoutController\nSkinController\nCalendarController\nClanCores\nWUIAttachments\nServersUI\nDiscordLinkBot\nStatsController", 13, TextAnchor.UpperCenter, panel);

            CreateCloseButton(ref container, ".03 .01", ".97 .1", "0 0 0 .5", "1 1 1 1", "CLOSE", 15, "WC.Edit.PluginColors.AddonsList", panel);

            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.AddonsList");
            CuiHelper.AddUi(player, container);
        }

        void UICreatePluginColorEditor(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UIEditor editor = _uiEditors[player];
            var info = editor.PanelCopy;

            var panel = CreatePanel(ref container, ".205 .01", ".7 .99", ".17 .17 .17 .8", "Overlay", "WC.Edit.PluginColors.Main", blur: true);
            CreateImagePanel(ref container, "0 0", "1 1", GetImage("PluginColors"), panel);
            CreateLabel(ref container, "0 .96", ".997 .999", "0 0 0 .5", "1 1 1 1", "ADDON EDITOR", 20, TextAnchor.MiddleCenter, panel);

            CreateCloseButton(ref container, ".95 .96", "1 1", "0 0 0 0", "1 1 1 1", "X", 15, "WC.Edit.PluginColors.Main", panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".01 .8865",
                       AnchorMax = ".1945 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.Enabled ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings panelenabled"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.PanelEnabled"
            });

            CreateInput(ref container, ".405 .8865", ".595 .927", "0 0 0 .5", "1 1 1 1", info.ButtonImage, 12, "wc_editor panelsettings panelname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".605 .8865", ".795 .927", "0 0 0 .5", "1 1 1 1", $"{info.PagePosition}", 12, "wc_editor panelsettings panelposition", TextAnchor.MiddleCenter, panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".205 .8865",
                       AnchorMax = ".395 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.DisplayButtonName ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings displaybtnname"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.DisplayBTNName"
            });

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".805 .8865",
                       AnchorMax = ".99 .927",
                   },
                   new CuiButtonComponent()
                   {
                       Color = info.ShowPanelPermission ? "0.13 0.83 0 .5" : "1 0.06 0.06 .5",
                       Command = $"wc_editor panelsettings showpermissionpanel"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.ShowPermissionPanel"
            });

            CreateButton(ref container, ".405 .808", ".595 .85", "0 0 0 0", "1 0.79 0.76 1", "SHOW ADDONS", 14, "wc_editor panelsettings showaddons", panel);
            CreateInput(ref container, ".01 .808", ".195 .85", "0 0 0 .5", "1 1 1 1", info.PanelName, 12, "wc_editor panelsettings panelname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".205 .808", ".395 .85", "0 0 0 .5", "1 1 1 1", info.ButtonName, 12, "wc_editor panelsettings buttonname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".605 .808", ".795 .85", "0 0 0 .5", "1 1 1 1", info.AddonSettings.AddonName, 12, "wc_editor panelsettings addonname", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".805 .808", ".99 .85", "0 0 0 .5", "1 1 1 1", info.PanelPermission, 12, "wc_editor panelsettings panelpermission", TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".805 .7295", ".99 .7715", "0 0 0 .5", "1 1 1 1", info.PanelNoPermissionText, 12, "wc_editor panelsettings nopermtext", TextAnchor.MiddleCenter, panel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".06 .7295",
                       AnchorMax = ".3 .7715",
                   },
                   new CuiTextComponent()
                   {
                       Color = "1 1 1 1",
                       Text = info.Commands.Count > 0 ? info.Commands[editor.CommandNumber] : "No Commands",
                       Align = TextAnchor.MiddleCenter,
                       FontSize = 13,
                       Font = "robotocondensed-bold.ttf"
                   }
                },
                Parent = panel,
                Name = "WC.Edit.Text.CurrentCommand"
            });

            CreateButton(ref container, ".01 .7295", ".055 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings commandnumber -", panel);
            CreateButton(ref container, ".35 .7295", ".395 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings commandnumber +", panel);
            CreateButton(ref container, ".29 .7295", ".335 .7715", "0 0.8 0.25 0", "1 1 1 1", "X", 15, "wc_editor panelsettings deletecommand", panel);

            CreateInput(ref container, ".405 .7295", ".74 .7715", "0 0.8 0.25 0", "1 1 1 1", editor.CommandHolder, 12, "wc_editor panelsettings newcommand", TextAnchor.MiddleCenter, panel, "WC.Edit.Text.NewCommand");
            CreateButton(ref container, ".75 .7295", ".795 .7715", "0 0.8 0.25 0", "1 1 1 1", " ", 15, "wc_editor panelsettings addcommand", panel);

            CreateButton(ref container, ".01 .005", ".75 .055", "0 0 0 .5", "1 1 1 1", "SAVE", 15, "wc_editor panelsettings save", panel);
            CreateButton(ref container, ".755 .005", ".989 .055", "1 0.31 0.2 .5", "1 1 1 1", "DELETE", 15, "wc_editor panelsettings delete", panel);

            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
            CuiHelper.AddUi(player, container);
        }

        void CreatePluginColorsList(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UIEditor editor = _uiEditors[player];

            var totalLines = editor.PluginColors.Count;
            var totalDepth = totalLines > 13 ? totalLines * .0769 : 0;

            container.Add(new CuiElement()
            {
                Name = "WC.Edit.PluginColors.LinesPanel",
                Parent = "WC.Edit.PluginColors.Main",
                Components =
                {
                    new CuiRectTransformComponent { AnchorMin = "0 .06", AnchorMax = ".99 .72" },
                    new CuiScrollViewComponent()
                    {
                        Horizontal = false,
                        Vertical = true,
                        MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                        Elasticity = .25f,
                        Inertia = true,
                        DecelerationRate = .3f,
                        ScrollSensitivity = 30f,
                        VerticalScrollbar = new CuiScrollbar {
                            Invert = false,
                            AutoHide = false,
                            HandleSprite = "assets/content/ui/ui.rounded.tga",
                            HandleColor = "1 1 1 .2",
                            HighlightColor = "1 1 1 .3",
                            TrackSprite = "assets/content/ui/ui.background.tile.psd",
                            TrackColor = ".09 .09 .09 0",
                            Size = 3,
                            PressedColor = "1 1 1 .4"
                        },
                        ContentTransform = new CuiRectTransform()
                        {
                            AnchorMin = $"0 {1 - (totalLines > 13 ? totalLines * .0769 : 1)}",
                            AnchorMax = $"1 1",
                        }
                    }
                }
            });

            int i = 0;
            var rowDepth = totalLines > 13 ? .0769 / totalDepth : .0769;
            var space = totalLines > 13 ? .01 / totalDepth : .01;

            foreach (var color in editor.PluginColors)
            {
                double yMin = .70 - (i * .05);
                double yMax = .745 - (i * .05);

                var startHeight = 1 - (rowDepth * i);
                var endHeight = startHeight - rowDepth + space;

                var lbl = CreateLabel(ref container, $".01 {endHeight}", $".93 {startHeight}", "0 0 0 .4", "1 1 1 .8", $" {color.Key}", 15, TextAnchor.MiddleLeft, "WC.Edit.PluginColors.LinesPanel");
                CreateInput(ref container, $".5 {endHeight}", $"1 {startHeight}", "0 0 0 0", "1 1 1 1", color.Value, 15, $"wc_editor plugincolor changecolor {color.Key}", TextAnchor.MiddleCenter, "WC.Edit.PluginColors.LinesPanel");
                CreatePanel(ref container, $".94 {endHeight}", $".99 {startHeight}", color.Value, "WC.Edit.PluginColors.LinesPanel", $"plugincolor_{color.Key}");
                i++;
            }

            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.LinesPanel");
            CuiHelper.AddUi(player, container);
        }

        void CMDCustomColors(BasePlayer player, string[] args)
        {
            switch (args[1])
            {
                case "open":
                    UICreateCustomColors(player);
                    break;
                case "revertthemecolor":
                    _config.CurrentTheme.Value.UIColors = _defaultThemes.First().Value.UIColors;
                    UICreateThemeColorsSection(player, 0);
                    break;
                case "custompage":
                    UICreateCustomColorsSection(player, int.Parse(args[2]));
                    break;
                case "addcolor":
                    _config.CustomColors.Add("1 1 1 1");
                    UICreateCustomColorsSection(player, (_config.CustomColors.Count - 1) / 5);
                    break;
                case "customcolor":
                    int indexNumber = int.Parse(args[2]);
                    string colorValue = ConvertHexColorToRgba(String.Join(" ", args.Skip(3)));
                    _config.CustomColors[indexNumber] = colorValue;
                    UIUpdateInputValue(player, $"customcolor_{indexNumber}_input", colorValue);
                    UIUpdatePanel(player, $"customcolor_{indexNumber}", colorValue);
                    break;
                case "themepage":
                    UICreateThemeColorsSection(player, int.Parse(args[2]));
                    break;
                case "themecolor":
                    indexNumber = int.Parse(args[2]);
                    colorValue = ConvertHexColorToRgba(String.Join(" ", args.Skip(3)));
                    _config.CurrentTheme.Value.UIColors[indexNumber].Color = colorValue;
                    UIUpdateInputValue(player, $"themecolor_{indexNumber}_input", colorValue);
                    UIUpdatePanel(player, $"themecolor_{indexNumber}", colorValue);
                    break;
                case "applytheme":
                    UIEditor editor = _uiEditors[player];

                    List<string> pluginNames = new List<string>();
                    Dictionary<string, string> themeColors = new Dictionary<string, string>();
                    foreach (var plugin in _config.UIPanels.Where(x => !string.IsNullOrEmpty(x.AddonSettings.AddonName))) pluginNames.Add(plugin.AddonSettings.AddonName);
                    foreach (var color in _config.CurrentTheme.Value.UIColors) themeColors.Add(color.Name, color.Color);

                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    SaveConfig();
                    SendThemeColors(pluginNames, themeColors);
                    break;
                case "save":
                    CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
                    SaveConfig();
                    break;

            }
        }

        void UICreateThemeColorsSection(BasePlayer player, int page = 0, CuiElementContainer container = null)
        {
            bool containerWasNull = container == null;
            if (containerWasNull)
            {
                container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.ThemeColors");
            }

            var maxPage = (_config.CurrentTheme.Value.UIColors.Count - 1) / 5;
            if (maxPage < page) page = 0;
            if (page < 0) page = maxPage;

            var panel = CreatePanel(ref container, "0 .78", "1 .905", "0 0 0 0", "WC.Edit.CustomColors.Main", "WC.Edit.CustomColors.ThemeColors");
            CreateButton(ref container, ".015 0", ".05 1", "0 0 0 .55", "1 1 1 1", "<", 15, $"wc_editor customcolors themepage {page - 1}", panel);
            CreateButton(ref container, ".95 0", ".985 1", "0 0 0 .55", "1 1 1 1", ">", 15, $"wc_editor customcolors themepage {page + 1}", panel);

            int i = 0;
            foreach (var color in _config.CurrentTheme.Value.UIColors.Skip(page * 5).Take(5))
            {
                var xMin = .055 + (i * .18);
                var xMax = xMin + .17;

                int indexNumber = i + (page * 5);

                CreateLabel(ref container, $"{xMin} .7", $"{xMax} .995", "0 0 0 .5", "1 1 1 1", color.Name, 13, TextAnchor.MiddleCenter, panel);
                CreateInput(ref container, $"{xMin} .4", $"{xMax} .7", "0 0 0 .4", "1 1 1 1", color.Color, 13, $"wc_editor customcolors themecolor {indexNumber}", TextAnchor.MiddleCenter, panel, $"themecolor_{indexNumber}_input");
                CreatePanel(ref container, $"{xMin} 0", $"{xMax} .4", color.Color, panel, $"themecolor_{indexNumber}");

                i++;
            }

            if (containerWasNull) CuiHelper.AddUi(player, container);
        }

        void UICreateCustomColorsSection(BasePlayer player, int page = 0, CuiElementContainer container = null)
        {
            bool containerWasNull = container == null;
            if (containerWasNull)
            {
                container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.CustomColors");
            }

            var maxPage = (_config.CustomColors.Count - 1) / 5;
            if (maxPage < page) page = 0;
            if (page < 0) page = maxPage;

            var panel = CreatePanel(ref container, "0 .605", "1 .73", "0 0 0 0", "WC.Edit.CustomColors.Main", "WC.Edit.CustomColors.CustomColors");
            CreateButton(ref container, ".015 0", ".05 1", "0 0 0 .55", "1 1 1 1", "<", 15, $"wc_editor customcolors custompage {page - 1}", panel);
            CreateButton(ref container, ".95 0", ".985 1", "0 0 0 .55", "1 1 1 1", ">", 15, $"wc_editor customcolors custompage {page + 1}", panel);

            int i = 0;
            foreach (var color in _config.CustomColors.Skip(page * 5).Take(5))
            {
                var xMin = .055 + (i * .18);
                var xMax = xMin + .17;

                int indexNumber = i + (page * 5);

                CreateLabel(ref container, $"{xMin} .7", $"{xMax} .995", "0 0 0 .5", "1 1 1 1", $"COLOR {indexNumber}", 13, TextAnchor.MiddleCenter, panel);
                CreateInput(ref container, $"{xMin} .4", $"{xMax} .7", "0 0 0 .4", "1 1 1 1", color, 13, $"wc_editor customcolors customcolor {indexNumber}", TextAnchor.MiddleCenter, panel, $"customcolor_{indexNumber}_input");
                CreatePanel(ref container, $"{xMin} 0", $"{xMax} .4", color, panel, $"customcolor_{indexNumber}");

                i++;
            }

            if (containerWasNull) CuiHelper.AddUi(player, container);
        }

        void UICreateCustomColors(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, ".205 .01", ".7 .99", ".17 .17 .17 .8", "Overlay", "WC.Edit.CustomColors.Main", true);
            //CreatePanel(ref container, "0 0", "1 1", "0 0 0 .8", panel);

            var lbl = CreateLabel(ref container, "0 .96", ".997 .999", "0 0 0 .5", "1 1 1 1", "CUSTOM COLORS", 20, TextAnchor.MiddleCenter, panel);
            CreateCloseButton(ref container, ".96 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 15, "WC.Edit.CustomColors.Main", lbl);

            CreateLabel(ref container, ".015 .91", ".985 .945", "0 0 0 .5", "1 1 1 1", "THEME COLORS", 15, TextAnchor.MiddleCenter, panel);

            UICreateThemeColorsSection(player, 0, container);

            CreateLabel(ref container, ".015 .735", ".985 .77", "0 0 0 .5", "1 1 1 1", "CUSTOM COLORS", 15, TextAnchor.MiddleCenter, panel);

            UICreateCustomColorsSection(player, 0, container);

            CreateButton(ref container, ".015 .57", ".985 .6", "0.33 0.63 1 .2", "1 1 1 1", "ADD COLOR", 15, "wc_editor customcolors addcolor", panel);

            CreateLabel(ref container, ".1 .48", ".9 .52", "0 0 0 .6", "1 1 1 1", "APPLY THEME", 15, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".1 .38", ".9 .475", "0 0 0 .4", "1 1 1 1",
                "Applying the theme will automatically apply your selected colors to all imported plugins and some of the basic panels of the selected theme!",
                13, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".1 .335", ".9 .375", "0.84 0.45 0 .3", "1 1 1 1", "APPLY THEME", 15, "wc_editor customcolors applytheme", panel);

            CreateButton(ref container, ".1 .255", ".9 .305", "0 0 0 .5", "1 1 1 1", "REVERT THEME COLORS", 15, "wc_editor customcolors revertthemecolor", panel);
            CreateButton(ref container, ".1 .2", ".9 .25", "0.18 0.84 0 .3", "1 1 1 1", "SAVE CHANGES", 15, "wc_editor customcolors save", panel);

            CuiHelper.DestroyUi(player, "WC.Edit.Text.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.PluginColors.Main");
            CuiHelper.DestroyUi(player, "WC.Edit.CustomColors.Main");
            CuiHelper.AddUi(player, container);
        }

        void UICreateColorCreator(BasePlayer player)
        {
            UIEditor editor = _uiEditors[player];
            var container = new CuiElementContainer();

            var UIColor = editor.UIEditorLocations["UIColor"];

            var mainPanel = CreatePanel(ref container, $"{UIColor.XMin} {UIColor.YMin}", $"{UIColor.XMax} {UIColor.YMax}", "0 0 0 .3", "Overlay", "WC.Edit.Color.Main", isMainPanel: true, blur: true);
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 .7", mainPanel);
            CreateLabel(ref container, "0 .87", ".997 .999", "0 0 0 .5", "1 1 1 1", "COLOR CREATOR", 20, TextAnchor.MiddleCenter, mainPanel, "WC.Edit.Color.ColorTitle");

            var listPanel = CreatePanel(ref container, "0 0", "1 .86", "0 0 0 0", mainPanel, "WC.Edit.Color.ColorList");
            CreateButton(ref container, ".02 .8", ".98 .98", "0 0 0 .5", "1 1 1 1", "CUSTOM COLORS", 15, "wc_editor customcolors open", listPanel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".92 0",
                       AnchorMax = "1 1",
                   },
                   new CuiButtonComponent()
                   {
                       Command = $"wc_editor collapse color",
                       Color = "0 0 0 0"
                   }
                },
                Parent = "WC.Edit.Color.ColorTitle",
                Name = "WC.Edit.Color.ColorButton"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 15,
                       Text = "-"
                    },
                },
                Parent = "WC.Edit.Color.ColorButton",
                Name = "WC.Edit.Color.ColorButtonText"
            });

            CreateButton(ref container, ".02 .02", ".98 .15", "1 0.16 0.16 .4", "1 1 1 1", "CLOSE", 15, "wc_editor close", listPanel);
            CreateButton(ref container, ".02 .17", ".98 .3", "0.22 0.8 0.17 .3", "1 1 1 1", "SAVE CHANGES", 15, "wc_editor savechanges", listPanel);

            CuiHelper.DestroyUi(player, "WC.Edit.Color.Main");
            CuiHelper.AddUi(player, container);
        }

        void UICreateCFGSelector(BasePlayer player)
        {
            UIEditor editor = _uiEditors[player];
            var container = new CuiElementContainer();

            var UICFG = editor.UIEditorLocations["UICFG"];

            var mainPanel = CreatePanel(ref container, $"{UICFG.XMin} {UICFG.YMin}", $"{UICFG.XMax} {UICFG.YMax}", "0 0 0 .3", "Overlay", "WC.Edit.CFG.Main", isMainPanel: true, blur: true);
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 .7", mainPanel);
            CreateLabel(ref container, "0 .89", ".997 .999", "0 0 0 .5", "1 1 1 1", "INFO SELECTOR", 20, TextAnchor.MiddleCenter, mainPanel, "WC.Edit.CFG.CFGTitle");

            var listPanel = CreatePanel(ref container, "0 0", "1 .89", "0 0 0 0", mainPanel, "WC.Edit.CFG.CFGList");

            CreateButton(ref container, $".011 .01", $".495 .12", "0 0 0 .5", "1 1 1 1", "CREATE NEW INFO PANEL", 11, $"wc_editor createnewinfo", listPanel, TextAnchor.MiddleCenter);
            CreateButton(ref container, $".505 .01", $".989 .12", "0 0 0 .5", "1 1 1 1", "CREATE NEW ADDON", 11, $"wc_editor createnewaddon", listPanel, TextAnchor.MiddleCenter);

            UIAddCFGItems(player, ref container, listPanel);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".92 0",
                       AnchorMax = "1 1",
                   },
                   new CuiButtonComponent()
                   {
                       Command = $"wc_editor collapse cfg",
                       Color = "0 0 0 0"
                   }
                },
                Parent = "WC.Edit.CFG.CFGTitle",
                Name = "WC.Edit.CFG.CFGButton"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 15,
                       Text = "-"
                    },
                },
                Parent = "WC.Edit.CFG.CFGButton",
                Name = "WC.Edit.CFG.CFGButtonText"
            });

            CuiHelper.DestroyUi(player, "WC.Edit.CFG.Main");
            CuiHelper.AddUi(player, container);
        }

        void UIAddCFGItems(BasePlayer player, ref CuiElementContainer container, string parent)
        {
            int i = 0;
            int p = 0;
            int a = 0;
            foreach (var info in _config.UIPanels)
            {
                if (string.IsNullOrEmpty(info.AddonSettings.AddonName))
                {
                    CreateButton(ref container, $".011 {.89 - (p * .1)}", $".495 {.98 - (p * .1)}", "0 0 0 .5", "1 1 1 1", info.PanelName, 13, $"wc_editor selectcfgpanel {i}", parent, TextAnchor.MiddleCenter);
                    p++;
                } else
                {
                    CreateButton(ref container, $".505 {.89 - (a * .1)}", $".989 {.98 - (a * .1)}", "0 0 0 .5", "1 1 1 1", info.PanelName, 13, $"wc_editor selectcfgaddon {i}", parent, TextAnchor.MiddleCenter);
                    a++;
                }

                i++;
            }
        }

        void UICreateUIList(BasePlayer player, List<UIParts> uiParts)
        {
            var container = new CuiElementContainer();


            var mainPanel = CreatePanel(ref container, ".005 .65", ".2 .99", "0 0 0 .3", "Overlay", "WC.Edit.List.Main", isMainPanel: true, blur: true);
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 .7", mainPanel);
            CreateLabel(ref container, "0 .89", ".997 .999", "0 0 0 .5", "1 1 1 1", "UI PANELS", 20, TextAnchor.MiddleCenter, mainPanel, "WC.Edit.List.ListTitle");
            CreateButton(ref container, ".011 .01", ".99 .1", "0.17 0.72 0.8 .5", "1 1 1 1", "ADD PANEL", 13, "wc_editor createpanel", mainPanel);

            //var itemsPanel = CreatePanel(ref container, "0 0", "1 .89", "0 0 0 0", mainPanel, "WC.Edit.List.ListPanel");

            UIEditor editor = _uiEditors[player];

            List<StoredUIList> storedUI = new List<StoredUIList>();
            GenerateNeededUI(player, editor, uiParts, ref storedUI);

            decimal dec = decimal.Divide(1, 15);
            var anchorMinY = 1 - (storedUI.Count > 15 ? storedUI.Count * dec : 1);
            try
            {
                container.Add(new CuiElement()
                {
                    Name = "WC.Edit.List.ListPanel",
                    Parent = "WC.Edit.List.Main",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = 0f, Color = "0 0 0 0" },
                        new CuiScrollViewComponent()
                        {
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = .25f,
                            Inertia = true,
                            ContentTransform = new CuiRectTransform()
                            {
                                AnchorMin = $"0 {anchorMinY}",
                                AnchorMax = "1 1",
                            },
                            DecelerationRate = .3f,
                            ScrollSensitivity = 30f,
                            VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = "1 1 1 .2",
                                HighlightColor = "1 1 1 .3",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = ".09 .09 .09 0",
                                Size = 3,
                                PressedColor = "1 1 1 .4"
                            }                        },
                        new CuiRectTransformComponent { AnchorMin = "0 .11", AnchorMax = "1 .88" },
                    }
                });
            }
            catch (Exception ex)
            {
                Puts($"Exception encountered: {ex.Message}");
            }

            UICreateListItems(player, ref storedUI, editor, ref container);

            container.Add(new CuiElement
            {
                Components = {

                   new CuiRectTransformComponent()
                   {
                       AnchorMin = ".92 0",
                       AnchorMax = "1 1",
                   },
                   new CuiButtonComponent()
                   {
                       Command = $"wc_editor collapse list",
                       Color = "0 0 0 0"
                   }
                },
                Parent = "WC.Edit.List.ListTitle",
                Name = "WC.Edit.List.ListButton"
            });

            container.Add(new CuiElement()
            {
                Components = {
                    new CuiTextComponent()
                    {
                       Align = TextAnchor.MiddleCenter,
                       Color = "1 1 1 1",
                       FontSize = 15,
                       Text = "-"
                    },
                },
                Parent = "WC.Edit.List.ListButton",
                Name = "WC.Edit.List.ListButtonText"
            });

            CuiHelper.DestroyUi(player, "WC.Edit.List.Main");
            CuiHelper.AddUi(player, container);
        }

        void UIAlterSelectedItem(ref CuiElementContainer container, BasePlayer player, string panelName, bool isUsing)
        {
            container.Add(new CuiElement()
            {
                Name = panelName,
                Update = true,
                Components = {
                    new CuiButtonComponent()
                    {
                        Color = isUsing ? "0 0 0 .5" : "0 0 0 0"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        void GenerateNeededUI(BasePlayer player, UIEditor editor, List<UIParts> uiParts, ref List<StoredUIList> storedUI, int sideNumber = 0, string panelPath = null)
        {
            foreach (var panel in uiParts)
            {
                // Note to self, this finds if it has a parent
                // Removes the default parts of the UI path to check to see if there is still some left over
                var splitPath = panelPath?.Split('.') ?? new string[0];
                var path = splitPath.Length > 4 ? splitPath.Skip(4).Concat(new string[] { $"{panel.UIName}" }) : new string[] { $"{panel.UIName}" };
                bool hasChildren = panel.UIChildren.Count > 0;
                bool isOpen = hasChildren ? editor.OpenParents.Contains(panel.UIName) : false;
                var newPath = "WC.Edit.List.ListPanel." + string.Join(".", path);

                storedUI.Add(new StoredUIList()
                {
                    UIName = panel.UIName,
                    UIPath = newPath,
                    HasChildren = hasChildren,
                    SpaceNumber = sideNumber,
                    IsOpen = isOpen
                });

                if (hasChildren && isOpen)
                {
                    GenerateNeededUI(player, editor, panel.UIChildren, ref storedUI, sideNumber + 1, newPath);
                }
            }
        }

        void UICreateListItems(BasePlayer player, ref List<StoredUIList> storedUI, UIEditor editor, ref CuiElementContainer container)
        {
            int panelCount = storedUI.Count;
            decimal buttonDepth = decimal.Divide(1, 15);
            var panelDepth = buttonDepth * panelCount;
            var rowDepth = panelCount > 15 ? buttonDepth / panelDepth : buttonDepth;

            int i = 0;
            foreach (var panel in storedUI)
            {
                var startHeight = 1 - (rowDepth * i);
                var endHeight = startHeight - rowDepth;

                double xMin = .08 + (panel.SpaceNumber * .05);
                bool isOpen = editor.OpenPanel.UIName == panel.UIName;
                if (isOpen) editor.LastSelectedPanel = panel.UIPath;

                container.Add(new CuiElement()
                {
                    Name = panel.UIPath,
                    Parent = "WC.Edit.List.ListPanel",
                    Components = {
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{xMin} {endHeight}",
                            AnchorMax = $".88 {startHeight}"
                        },
                        new CuiButtonComponent()
                        {
                            Color = isOpen ? "0 0 0 .5" : "0 0 0 0",
                            Command = $"wc_editor selectpanel {panel.UIPath}"
                        }
                }
                });

                container.Add(new CuiElement()
                {
                    Parent = panel.UIPath,
                    Name = panel.UIPath + ".text",
                    Components = {
                        new CuiTextComponent()
                        {
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 .7",
                            FontSize = 11,
                            Text = $" {panel.UIName}"
                        }
                    }
                });

                if (isOpen)
                {
                    PutUpDownButtons(player, container, panel.UIPath + ".text");
                }

                if (panel.HasChildren)
                {
                    CreateButton(ref container, $"{xMin - .05} {endHeight}", $"{xMin} {startHeight}", "0 0 0 0", "1 1 1 .6", panel.IsOpen ? "-" : "+", 11, $"wc_editor expandpanel {panel.UIName}", "WC.Edit.List.ListPanel", TextAnchor.MiddleCenter, $"{panel.UIPath}.Expand");
                }

                i++;
            }
        }

        void PutUpDownButtons(BasePlayer player, CuiElementContainer container, string parentName)
        {
            var pnl = CreatePanel(ref container, "1 0", "1 1", "0 0 0 0", parentName, "WCMoveButtonsPanel", offsetMin: "-40 0", offsetMax: "0 0");
            CreateButton(ref container, "0 0", ".475 .9", "0 0 0 0", "1 1 1 1", "DN", 9, $"wc_editor movepanel down {parentName}", pnl, name: "WCMoveDownButton");
            CreateButton(ref container, ".525 0", "1 .9", "0 0 0 0", "1 1 1 1", "UP", 9, $"wc_editor movepanel up {parentName}", pnl, name: "WCMoveUpButton");

            CuiHelper.DestroyUi(player, "WCMoveButtonsPanel");
        }
        #endregion

        #region [ UI ]
        private void OpenThemeSelector(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "welcomecontroller.admin"))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            UIThemeSelector(player);
        }

        void UIThemeSelector(BasePlayer player)
        {
            var container = new CuiElementContainer();

            CreatePanel(ref container, "0 0", "1 1", "0 0 0 .5", "Overlay", "WC.Theme.Main", true, true);
            var panel = CreatePanel(ref container, ".15 .15", ".85 .85", "0 0 0 .5", "WC.Theme.Main", "WC.Theme.Panel");

            var title = CreateLabel(ref container, "0 .9", "1 1", "0 0 0 .5", "1 1 1 1", "THEME SELECTOR", 25, TextAnchor.MiddleCenter, panel);
            CreateCloseButton(ref container, ".94 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 30, "WC.Theme.Main", title);

            CuiHelper.DestroyUi(player, "WC.Theme.Main");
            CuiHelper.AddUi(player, container);

            GetThemes(player);
        }

        void UIPutThemes(BasePlayer player, List<ThemeAPIPart> themes)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 .9", "0 0 0 0", "WC.Theme.Panel", "WC.Theme.List");

            int i = 0;
            foreach (var theme in themes)
            {
                var themePanel = CreatePanel(ref container, $"{.01 + (i * .33)} {(i > 2 ? ".02" : ".51")}", $"{.33 + (i * .33)} {(i > 2 ? ".49" : ".98")}", "0 0 0 .5", panel);

                CreateLabel(ref container, ".02 .87", ".98 .98", "0 0 0 .4", "1 1 1 1", theme.ThemeName, 15, TextAnchor.MiddleCenter, themePanel);
                container.Add(new CuiElement
                {
                    Parent = themePanel,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = ".02 .15",
                            AnchorMax = ".98 .85"
                        },
                        new CuiRawImageComponent {Url = theme.ThemeImage},
                    }
                });
                CreateButton(ref container, ".02 .02", ".98 .13", "0.37 0.64 1 .6", "1 1 1 1", "SELECT THEME", 13, $"wc_editor selecttheme {theme.ThemeName}", themePanel);

                i++;
            }

            CuiHelper.DestroyUi(player, "WC.Theme.List");
            CuiHelper.AddUi(player, container);
        }

        public class ThemesRoot
        {
            public List<ThemeAPIPart> THEMES { get; set; }
        }

        public class ThemeAPIPart
        {
            public string ThemeName { get; set; }
            public string ThemeImage { get; set; }
        }

        private void GetThemes(BasePlayer player, Action onComplete = null)
        {
            string apiUrl = "https://solarrust.com/api/themes.json";

            RunThemesApi(apiUrl, themes =>
            {
                if (themes != null)
                {
                    UIPutThemes(player, themes);
                }
                else
                {
                    Console.WriteLine("Failed to fetch UI Parts.");
                }

                onComplete?.Invoke();
            });
        }


        public void RunThemesApi(string apiUrl, Action<List<ThemeAPIPart>> callback)
        {
            webrequest.Enqueue(apiUrl, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    Console.WriteLine($"Error fetching UI Parts from API: Status Code {code}");
                    callback(null);
                    return;
                }

                try
                {
                    ThemesRoot rootObject = JsonConvert.DeserializeObject<ThemesRoot>(response);
                    List<ThemeAPIPart> themes = rootObject?.THEMES ?? new List<ThemeAPIPart>();

                    callback(themes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during deserialization: {ex.Message}");
                    callback(null);
                }
            }, this, RequestMethod.GET);
        }

        private void UIOpenWelcomeMenuPanels(BasePlayer player, string command, string[] args)
        {
            var panel = _config.UIPanels.FirstOrDefault(x => x.Enabled && x.Commands.Any(y => y.Equals(command, StringComparison.OrdinalIgnoreCase)));

            if (args != null && args.Length > 0 && panel?.AddonSettings?.AddonName == "KitController")
            {
                Interface.CallHook("ClaimKit", player, args);
                return;
            }

            bool hasPerms = true;
            if (!string.IsNullOrEmpty(panel.PanelPermission) && !permission.UserHasPermission(player.UserIDString, panel.PanelPermission))
            {
                if (!panel.ShowPanelPermission)
                {
                    SendReply(player, panel.PanelNoPermissionText);
                    return;
                }

                hasPerms = false;
            }

            foreach (var pnl in _config.CurrentTheme.Value.UIParts) CuiHelper.DestroyUi(player, pnl.UIName);
            UILoadPanels(player, _config.CurrentTheme.Value.UIParts, panel, hasPerms: hasPerms);
            if(hasPerms && !string.IsNullOrEmpty(panel.AddonSettings.AddonName)) Interface.CallHook("OnWCRequestedUIPanel", player, "WCSourcePanel", panel.AddonSettings.AddonName);
            AddButtonOverlay(player, $"ButtonSettings.{panel.PanelName}", _config.CurrentTheme.Value.UIParts);
            if (_config.UseLinks) Interface.CallHook("OnWCRequestedUIPanel", player, "WCSourcePanel", "WUIAttachments_SocialLinks");
        }

        void UILoadInfoPanel(BasePlayer player, InfoPanelSettings info = null, CuiElementContainer container = null, int page = 0, bool hasPerms = true)
        {
            bool containerWasNull = container == null;
            if (containerWasNull) container = new CuiElementContainer();

            if (info == null) info = _config.UIPanels.First();
            List<UIParts> uiParts = _uiEditors.ContainsKey(player) ? _uiEditors[player].UICopy : _config.CurrentTheme.Value.UIParts;

            bool isAddon = !string.IsNullOrEmpty(info.AddonSettings?.AddonName);

            if (isAddon)
            {
                UIParts pnl = new UIParts();
                pnl = pnl.FindPanelByName("WCSourcePanel", uiParts);

                CuiHelper.DestroyUi(player, "WCSourcePanel");

                CreateSuperPanel(ref container, pnl, pnl.FindParent(uiParts).UIName);

                if (!hasPerms)
                {
                    UIParts infoPanel = new UIParts();
                    infoPanel = infoPanel.FindPanelByName("NoPerms", uiParts);
                    UILoadPanels(player, new List<UIParts> { infoPanel }, info, container, true, pnl.UIName);
                }
            }
            else if (info != null)
            {
                bool hasImage = !string.IsNullOrEmpty(info.PanelSettings?.PanelImage);
                UIParts infoPanel = new UIParts();
                if(!hasPerms) infoPanel = infoPanel.FindPanelByName("NoPerms", uiParts);
                else
                {
                    if (info.PanelSettings?.PanelPages.Count > 1)
                    {
                        if (hasImage) infoPanel = infoPanel.FindPanelByName("TextPanel_V4", uiParts);
                        else infoPanel = infoPanel.FindPanelByName("TextPanel_V3", uiParts);
                    }
                    else
                    {
                        if (hasImage) infoPanel = infoPanel.FindPanelByName("TextPanel_V2", uiParts);
                        else infoPanel = infoPanel.FindPanelByName("TextPanel_V1", uiParts);
                    }
                }

                var infoPanelParent = infoPanel.FindParent(uiParts);

                CuiHelper.DestroyUi(player, "WCSourcePanel");

                CreateSuperPanel(ref container, infoPanelParent, infoPanelParent.FindParent(uiParts).UIName);

                UILoadPanels(player, new List<UIParts> { infoPanel }, info, container, true, infoPanelParent.UIName, page: page, hasPerms: hasPerms);
            }

            if (containerWasNull)
            {
                CuiHelper.AddUi(player, container);
                if (isAddon && hasPerms) Interface.CallHook("OnWCRequestedUIPanel", player, "WCSourcePanel", info.AddonSettings.AddonName);
            }
        }

        void AddButtonOverlay(BasePlayer player, string parentName, List<UIParts> uiParts)
        {
            var container = new CuiElementContainer();
            UIParts foundButtonOverlay = new UIParts();

            foundButtonOverlay = foundButtonOverlay.FindPanelByName("SelectedButtonOverlay", uiParts).Clone();
            CreateSuperPanel(ref container, foundButtonOverlay, parentName);

            CuiHelper.DestroyUi(player, "SelectedButtonOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UILoadPanels(BasePlayer player, List<UIParts> uiParts, InfoPanelSettings info = null, CuiElementContainer container = null, bool isDecendent = false, string parentName = null, int page = 0, bool hasPerms = true)
        {
            bool containerWasNull = container == null;
            if(containerWasNull) container = new CuiElementContainer();

            if (info == null) info = _config.UIPanels.First();

            foreach (var panel in uiParts)
            {
                if (panel.UIName == "SelectedButtonOverlay") continue;
                switch (panel.UIName)
                {
                    case "LeftButton":
                    case "RightButton":
                        UIParts uiClone = panel.Clone();
                        uiClone.UIProperties.Command += $" {page} {info.PanelName}";

                        CreateSuperPanel(ref container, uiClone, isDecendent ? parentName : panel.UIParent);
                        break;
                    case "TextPanel":
                        uiClone = panel.Clone();

                        if (!string.IsNullOrEmpty(info.PanelSettings.TextPanelImage)) uiClone.UIProperties.Image = $"-text-{info.PanelName}-part";
                        CreateSuperPanel(ref container, uiClone, isDecendent ? parentName : panel.UIParent);
                        break;
                    case "PanelTitle":
                        uiClone = panel.Clone();

                        uiClone.UIText.Text = info.PanelName;
                        CreateSuperPanel(ref container, uiClone, isDecendent ? parentName : panel.UIParent);
                        break;
                    case "BottomImage":
                        uiClone = panel.Clone();
                        uiClone.UIProperties.Image = $"-bottom-{info.PanelName}-part";
                        CreateSuperPanel(ref container, uiClone, isDecendent ? parentName : panel.UIParent);
                        break;
                    case "TextPanelInlay":
                        uiClone = panel.Clone();

                        List<string> lines = new List<string> { " " };
                        if (info.PanelSettings.PanelPages.Count > 0)
                        {
                            lines = info.PanelSettings.PanelPages[page];
                        }

                        uiClone.UIText.Text = AlterTextTags(string.Join("\n", lines));
                        CreateSuperPanel(ref container, uiClone, isDecendent ? parentName : panel.UIParent);
                        break;
                    case "ButtonSettings":
                        List<InfoPanelSettings> btnList = new List<InfoPanelSettings>();
                        int skippedButtons = 0;

                        foreach (var pnl in _config.UIPanels.Where(x => x.Enabled))
                        {
                            InfoPanelSettings panelSettings = pnl.Clone();
                            panelSettings.PagePosition = btnList.Any(x => x.PagePosition == pnl.PagePosition) ? btnList.Count : pnl.PagePosition;
                            btnList.Add(panelSettings);
                        }

                        foreach (InfoPanelSettings pnlInfo in btnList)
                        {
                            bool displayName = true;
                            var currentSpot = pnlInfo.PagePosition - skippedButtons;

                            bool vertical = panel.UILoopSettings.Vertical;
                            var pos = panel.UIAnchors;
                            UIParts customParts = panel.Clone();

                            var buttonSize = vertical ? pos.YMax - pos.YMin : pos.XMax - pos.XMin;
                            var buttonSpace = buttonSize + panel.UILoopSettings.Spacing;

                            customParts.UIAnchors.XMin = !vertical ? pos.XMin + (currentSpot * buttonSpace) : pos.XMin;
                            customParts.UIAnchors.XMax = !vertical ? pos.XMax + (currentSpot * buttonSpace) : pos.XMax;
                            customParts.UIAnchors.YMin = vertical ? pos.YMin - (currentSpot * buttonSpace) : pos.YMin;
                            customParts.UIAnchors.YMax = vertical ? pos.YMax - (currentSpot * buttonSpace) : pos.YMax;

                            customParts.UIName += $".{pnlInfo.PanelName}";

                            bool isAddon = !string.IsNullOrEmpty(pnlInfo.AddonSettings?.AddonName);

                            if (!pnlInfo.ShowPanelPermission && !string.IsNullOrEmpty(pnlInfo.PanelPermission) && !permission.UserHasPermission(player.UserIDString, pnlInfo.PanelPermission))
                            {
                                skippedButtons++;
                                continue;
                            }

                            if(!string.IsNullOrEmpty(customParts.UIProperties.Image)) customParts.UIProperties.Image = $"-button-{pnlInfo.PanelName}-part";
                            if (!pnlInfo.DisplayButtonName) displayName = false;

                            customParts.UIProperties.Command = $"wc_main panel {pnlInfo.PanelName}";
                            customParts.UIText.Text = displayName ? pnlInfo.ButtonName : " ";

                            CreateSuperPanel(ref container, customParts, "ButtonPanel");
                        }
                        break;
                    case "NoPerms":
                        UIParts clonedPart = panel.Clone();
                        clonedPart.UIText.Text = info.PanelNoPermissionText;
                        CreateSuperPanel(ref container, clonedPart, isDecendent ? parentName : panel.UIParent);
                        break;
                    default:
                        CreateSuperPanel(ref container, panel, isDecendent ? parentName : panel.UIParent);
                        break;
                }

                if (panel.UIChildren.Count > 0)
                {
                    if (panel.UIName == "WCSourcePanel")
                    {
                        UILoadInfoPanel(player, info, container, page, hasPerms);
                    } else UILoadPanels(player, panel.UIChildren, info, container, true, panel.UIName, page: page, hasPerms: hasPerms);
                }
            }

            if(containerWasNull) CuiHelper.AddUi(player, container);
        }
        #endregion

        #region [ BASIC UI METHODS]
        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, string offsetMin = null, string offsetMax = null, float fadeInTime = 0f)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if (offsetMax != null) panel.RectTransform.OffsetMax = offsetMax;
            if (offsetMax != null) panel.RectTransform.OffsetMin = offsetMin;
            if (fadeInTime != 0) panel.Image.FadeIn = fadeInTime;

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax

                    },
                    new CuiRawImageComponent {Png = panelImage},
                }
            });
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string panelImage, string parent = "Overlay", string panelName = null)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax

                    },
                    new CuiRawImageComponent {Png = panelImage},
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Button = { Color = "0 0 0 0", Command = $"{command}" }
            }, parent);
        }

        private static void CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter, string name = null)
        {
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
            }, parent, name);
            return;
        }

        private static void CreateCloseButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter, string name = null)
        {
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Button = { Color = buttonColor, Close = buttonCommand },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
            }, parent, name);
            return;
        }

        private static void CreateSimpleLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay")
        {
            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax
                },
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                },
            }, parent);
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax
                },
                Image = { Color = backgroundColor }
            }, parent, labelName);

            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                },
            }, panel);
            return panel;
        }

        private static void CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string backgroundColor, string textColor, string labelText, int fontSize, string command, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
                },
                Parent = parent,
                Name = labelName
            });
        }
        #endregion

        #region [ MULTI UI METHODS ]
        private string CreateSuperPanel(ref CuiElementContainer container, UIParts uiParts, string parentName = null)
        {
            CuiElement tempPanel = new CuiElement()
            {
                Name = uiParts.UIName,
                Parent = parentName == null ? uiParts.UIParent : parentName
            };

            CuiRectTransformComponent rectTransform = new CuiRectTransformComponent()
            {
                AnchorMin = $"{uiParts.UIAnchors.XMin} {uiParts.UIAnchors.YMin}",
                AnchorMax = $"{uiParts.UIAnchors.XMax} {uiParts.UIAnchors.YMax}",
            };

            CuiImageComponent imageComponent = new CuiImageComponent()
            {
                Color = uiParts.UIProperties.Color,
                FadeIn = uiParts.UIProperties.FadeIn,

            };

            if (uiParts.UIOffsets.Enabled)
            {
                rectTransform.OffsetMin = $"{uiParts.UIOffsets.XMin} {uiParts.UIOffsets.YMin}";
                rectTransform.OffsetMax = $"{uiParts.UIOffsets.XMax} {uiParts.UIOffsets.YMax}";
            }

            if (uiParts.UIProperties.Blur) imageComponent.Material = "assets/content/ui/uibackgroundblur.mat";
            if (!string.IsNullOrEmpty(uiParts.UIProperties.Material)) imageComponent.Material = uiParts.UIProperties.Material;
            if (uiParts.UIProperties.EnableCursor) tempPanel.Components.Add(new CuiNeedsCursorComponent());
            tempPanel.Components.Add(imageComponent);
            tempPanel.Components.Add(rectTransform);

            container.Add(tempPanel);

            if (!string.IsNullOrEmpty(uiParts.UIProperties.Image))
            {
                container.Add(new CuiElement
                {
                    Parent = uiParts.UIName,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent {Png = GetImage(uiParts.UIProperties.Image.Contains("-part") ? uiParts.UIProperties.Image : uiParts.UIName)},
                    }
                });
            }

            if (!string.IsNullOrEmpty(uiParts.UIProperties.Command))
            {
                container.Add(new CuiButton
                {
                    Text =
                        {
                        Color = uiParts.UIText.Color,
                        Text = uiParts.UIText.Text,
                        Align = uiParts.UIText.Alignment,
                        FontSize = uiParts.UIText.Size,
                        Font = uiParts.UIText.Font
                    },
                    Button = { Command = $"{uiParts.UIProperties.Command}", Color = "0 0 0 0" }
                }, uiParts.UIName);
            }
            else if (!string.IsNullOrEmpty(uiParts.UIText.Text))
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = uiParts.UIText.Color,
                        Text = AlterTextTags(uiParts.UIText.Text),
                        Align = uiParts.UIText.Alignment,
                        FontSize = uiParts.UIText.Size,
                        Font = uiParts.UIText.Font
                    }
                }, uiParts.UIName);
            }

            return uiParts.UIName;
        }
        #endregion
    }
}
