using CompanionServer.Handlers;
using Facepunch;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("AdminMap", "0xF // dsc.gg/0xf-plugins", "2.0.4")]
    partial class AdminMap : RustPlugin
    {
        #region References
        [PluginReference]
        private Plugin AdminMenu;
        #endregion

        #region Consts
        private const string PERMISSION_ALLOW = "adminmap.allow";
        private const string PERMISSION_TPMARKER = "adminmap.teleport2marker";
        private const string PERMISSION_INVIS = "adminmap.invis";
        private const string PERMISSION_RUSTPLUS = "adminmap.rust+";
        private const string PERMISSION_RUSTPLUSINVIS = "adminmap.rust+.invis";
        #endregion

        #region Vars
        private static AdminMap Instance;
        private static string CapsuleBackgroundVerticalCRC, LetterTCRC;
        private List<string> permissions = new() { PERMISSION_ALLOW, PERMISSION_TPMARKER, PERMISSION_INVIS, PERMISSION_RUSTPLUS, PERMISSION_RUSTPLUSINVIS };
        private Dictionary<(ulong userID, string permissionName), bool> permissionCache = new();
        #endregion


        #region Enums
        public enum MapLayer
        {
            None,
            Player = 1 << 0,
            Sleeper = 1 << 2,
            TC = 1 << 3,
            Stash = 1 << 4,
            SleepingBag = 1 << 5,
            Text = 1 << 6,
        }

        public enum MapLayerModification
        {
            None,
            NoLabels = 1 << 0
        }
        #endregion

        #region Hooks
        void Init()
        {
            Instance = this;

            foreach (var button in config.Buttons)
                if (!string.IsNullOrEmpty(button.Permission))
                    permissions.Add($"adminmap.{button.Permission}");

            foreach (var perm in permissions)
                permission.RegisterPermission(perm, this);

        }

        void Unload()
        {
            permissions.Clear();
            permissionCache.Clear();

            foreach (ConnectionData connectionData in ConnectionData.all.Values.ToArray())
                connectionData.Dispose();
        }

        void OnServerInitialized(bool initial)
        {
            CapsuleBackgroundVerticalCRC = FileStorage.server.Store(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAALQAAAQ9CAYAAADwPWcxAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAABYdSURBVHgB7d0/kJ1nfejxVyth3caiubeyKxpLdxjuLTKmCkUwBQNoJWYoUjhOlSLBXQqSJlX+NJkUmCZp8DgdI2QZJRSYNEljd2mQPUOJOppIlaVZOc+rnCNW/3dXZ3fP+e7nM7M+q38gW9999Huf5z3nnJrYt29/+9uvnDlz5vznn39+bny8Mn9sbW29PH7o3KlTp16Zf874vi+Oh5ef8j9xe/y8/7p37978eGt8+9bi85vzx87Ozs35+65fv/7JxL6cmniqixcvvjxCfX3E9uoI9Pzp06dfH4+vTkdo/P/dGJH/Zjx+Mn4fH4/fz40PPvjg9sQTCXqXeeUdwbxxXPHu1fh9zSv5r8anvxyr+Q0r+e+c6KDnFXiE8cZY+V4fIX9jevqIsNbmwMfHR+OL8MN5FT/JK/iJC3qOeDx8d4T89fH41anpo/FxdazeH4/V++Z0gpyIoOeIx8p1Yaxg35+6ET/NL8bHL69du3Z1OgHSQV++fPnCWKW+PlbjP542dJxYleVYMj7eKa/ayaDHxd3rJ3Q13quPxhf5u++///4vp5hU0Nvb25fHw/wh5D1Y7Jb8sDSOJIKeQx5/OG8vDzXYn1LYGx20kFerEPZGBm1GPlxz2GNX6Afj4vHjacNsVNDzSd4I+S/Hp29MHLoR9U83bVdkY4Iee8lvjb8O355O+PbbMbg1on53nD6+M22AtQ96sZf8tyPmCxPHZjGGvLnuq/XpaY2NVXmek/9hxPx/Jo7V+DM4t7W19dZrr702ffrpp2s7W6/lCr246+1HVuX1tM6r9dqt0POsPGL+B1tx62uxWl8+f/78Z2O1/s9pjazNCj3fQDTvKc9/rU1sjPFn9uPx8M663LK6FkEbMTbbOo0gxz5yLA5J/tmIsbnmEWR+osSXv/zlj27cuPHb6Rgda9DLeXl8enZio81Rj1X6D8dcfes45+pjC/rSpUvzltyfT9R87Ti39o4l6O3t7b8dD388kTRW66+OqF8eUf/HdMSOPOhFzN+dSBtR//8xfrw6ov5wOkJHtsuxeHLqe3YyTpb5dUXGw5tHta13JEGL+WQ7yqi3pqMh5hNs8Wf/3nQEDn2Gnmfm8S/0tYkTbb7B7Chm6kMN+jvf+c5fjn+RP5zgf1w47KgPLeh5n3nE/CcTPOzCYe5TH0rQ8wng5NCEp5j3qQ/rRHHluxyLezOO5AKAzbazs/Pmqp+Iu9KgF3fNvedGI/bo1oj60irv0ltZ0Iu95mtiZj9WvUe9sn1oL/jCQSz2qL8/rchKLgoXt4G+PcEBLO77WMlF4guPHIsXf7k2eb0MXsxK5ukXHjnmi8BJzLy4+Ym3P5pe0AuNHIub9L0sFysxH4+/6KHLgUeOxajxbxOs2JkzZ7avXLlyoHf2OvDIsRg1YOXu3r37d9MBHWjkMGpwmBZ35h1o12PfI4ddDY7IgXY99j1yjK+eeXUWM4ft3OK1wPdlX0GPUWN+x1VPcOWovDHf7LafX7CvoO/du3fgYR0OYr8n0HsOen6DHvdqcNRGc68v3q5vT/Yc9Hzz0QTHYD/t7SloqzPHaW5vr6v0noK2OnPc9trgc4O2OrMO9rpKPzdoqzPrYrT43C3jZwY9viLesDqzLuYdj+ftSz9vhf6jCdbI8/alnxr0fM/G5L20WTOLVfr803586xm/cGVPXIRVGqv0U+/0fFbQVmfW0mjzrcXLZjzmiUHbqmPNzW9Q9MSXZ35i0OMnu3mftfa0i8PHbvD3XEE2xdiX/r1HX3HpsRV6xLyv+0/huIyx+LGTwyeNHHu+VQ+O01ihv/Ho9z0U9De/+c1zk71nNsS8J/3obsdDQb/00kvGDTbK/B7ju7/9UNBjd+PrE2yQ0exDi/BDQTtMYdM8emr4IOhvfetbFxymsIHOLe47uu9B0GfOnDk/wQYaW80PRuUHQTsdZFPtPgZ/ELS3LmZT7b72ux/0vP9sfmZTze0u96PvB3327FnzMxtt7Hbc3767H/Sje3mwacYx+P0JY2vxDfMzG215YXg/6DGDvDrBBlteGC6DNkOz0ZabGqfmE8JxqPL+BBtuZ2fnD7a+8IUveDV+EubT7q3l1SFsutHyOUGTMbe85YSQivtBj/27cxMEjNPC+f3Ct1wUkrCcob84QcP9GdoKTcZ8UmiGJmFenOegrdBUnNv3e33DOhM0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMyB317goZbc9C3Jgj4/PPPbxs5yNja2rq/Qv9mgoB79+7d3pr/MUHAqVOnbm3Ny/QEAWNxvrU1BumbEwSMFfrm1vyPCQLuBz3ZhyZiZ2fn5tb4x40JAs6ePXtr6/r160YOEq5cufLJ8mBF1Gy0MT/fnzSWQX80wQZb7tZtLb7xyQQbbDT8uxXa1h2bbhyqfDw/3g/6zp07H0+wwcaJ9+9W6J///Ofz8bdVmk1184MPPrh/nrL79lEXhmyqB2cpD4J2YcgG+3D5yYOgx1D94QQb6MyZM4+v0IsTQ/d1sGluzieEy288+hSsX0ywWR7aodt61g/CuhtnKA8twg8FPfajfznBBlkeqCw9FPS8Hz12O6zSbIS51eX+89KTXsbAbgcbYYwbP330+x4L+u7du1cn2AAvvfTSY9PEY0EbO9gQH/7kJz957HaNJ75y0tbW1g8nWG9PHI2fGPRnn302b1Q7ZGFd3bx27doTR+MnBj2PHWPg/vEE6+mpI/FTX6xxBG1PmrU0LgafOhI/NeirV6/ecHHIupmbfNLF4NIzX07XxSHrZjT542f++LN+8P333//YKs0auTmafOYo/NwXPH/SaQwck+dODKemPdje3v638fDKBMdn3qr7g+f9pL2+JYVZmuO2pwb3tELPrNIcoz2tzrP9vGmQVZrjsuf29rxCzy5evPjeuEh8fYKjs+fVebavt3WzL81R29nZ+cF+fv6+gl7sSzsS56hcvX79+r7OQfb9xptnz57968mdeBy+28+6Z+NpTk/79Ktf/er2a6+99tmYpX9/gkNy7969f7p69eq+nw64r4vC3cYF4rUR9fkJVm9fF4K7Hfi9vk+fPr2vYR32aowab04HtO+RY+mTTz757YULF+ZPvzrBioxR452DjBpLBx45lowerNCBR42lA48cS2PX408nux68uNsvMmosHXjkWLLrwSqMQ7u/v3Llyr9PL+iFg559+umn/3n+/Pkvjk//3wT7NObmd8eo8c60Ai88cizduXPnh94FgAO4Of52X9ktFSsLen7pA/M0+3RznpsffcHFF/HCuxyPunTp0utjpX5vgucYK/Ob8/1B0wqtZIbebexP3xwXibddJPIs4yLwb0bM/zqt2MqDns0XiePQZV793TvNY+bDk3ER+I/TITiUoGdjpf547Hy8Oj69MMHCvKPxs5/97O+nQ3JoQc/GSv2hqNnl6rgA/KvpEK38ovBJHI8zNgpujJgvTYdsZdt2z3L37t037VGfXHPM4+GFj7X34lBHjqVf//rXn33pS1/613Fl+7WxUv/viRNjGfMq95qf5UhGjt22t7f/bjxcnjgJro7djCO9b/5IVujdFheK7vvoO/KYZ0ce9GxE/e/2qbvmfeYxYvzNdAyOJejZvE/tRLFnPgE8rEOTvTjyGfpRly9fvjC+on80ed28TTcvTn+66nsz9utItu2eZX7ri8UzFW5ObKR5J2P8GW4fd8yzYxs5dpuf9TK29a6ePn36f00uFjfKfJQ9VuYfjIXpt9MaOPaR41HjVPGt8R/o7fHpyxPr7Pb8Wocj5HenNbJ2Qc++973vvXLnzp35nmpz9RqaR4yzZ8/+2bPejeq4rGXQS5cuXXp7/Mf7/sTaGH97vjNm5bV9Fdq1DnpmtV4P86o8rnH+Yr6In9bY2ge9tFit35rM1kdtLWflp1mLXY69mA9ivvKVr/zLzs7Oucn91Udifi3w+YnPq3i9jKOyMSv0bosn4s43ORlDDsH8ZqvzqrwO+8r7tZFBL21vb8937c1bfMJejXnX4ofj6PrqtKE2OuglYb+wjQ95KRH0krD3Zx4t5re+LoS8lAp6aYT9xrwj4i3onmyTZ+TnSQa9tNjDnlfsOeyTvmrPd8O9O/aSfzF2LbLP70wHvds8joyV6RvjD/Xr0wmyXI3v3bt346ie13ecTkzQS4tVe972+251JJkjHg/z2zr89CREvNuJC3q3ixcvvjyint8j5o1ps8eS22MF/nCsxB+NmD88aRHvdqKDftTi2TPnF6PJ/MI46xr4zfH7nHcobswhX79+3ZMjFgT9DPMKPla9OfJ59f6/4+OVY3gFqAfxjo+b44vto5O8Aj+PoA9gXslHWC+Pj1cWH6+O2OZ7TOYbp86Nz+8/Tk+/kWoO8tb8yfi18+p6ezzeGr/uN3O087d3dnZuWHn3778BB1zL9xAmZqAAAAAASUVORK5CYII="), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
            LetterTCRC = FileStorage.server.Store(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAThSURBVHgB7d3Pi1V1GMDh96RoZYmWRUW1KggtComgH1ZYFC2kIFq0bdH/IEEUtHIV7aJli0CCFtG+RbSoFi0kLRgRSSSFMCkxU0/v4U4YUTD3Xhq/57zPAy9nFjMgOO9nzjl37pkIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAZfR9f3fOIzldANOWi35bzqs5H+Ss9Fe8H0yOqheXi709D0/l7F2dXf/xqRdztnRddyGYjI1BKbnwW/KwJ64s/EM5G9bwpcP3yh05x4LJEICJy4Uf/o8fzHl2dYbl3xyLWUsoGBEBmJhc+GFJh5/qw7I/kfNkztaAfyEAE5BLP1y3PxezU3oLz5oJwIjl4t+Shw9zng9YwDXBmB0Iy88SBGDcdgcsQQDGzV15liIAUJgAQGECAIUJABQmAFCYAEBhAgCFCQAUJgBQmABAYQIAhQkAFCYAUJgAQGECAIV5JFhD+r7flofXY/Zs/uvW8CV3xfp6J/+NZ2M+fc7JnM+6rvsmaIo/DNKQXK4v8vB4TNPlnD0ZgS+DZrgEaEQu/00x3eUfDN9r+4KmCEA7bozp87jyxggAFCYAUJgAQGECAIUJABQmAFCYAEBhAgCFCQAUJgBQmABAYQIAhQkAFCYAUJgAtONcTN/5oCkC0Iiu607n4WhM21dBUzwTsC0v5ryZszPW9kzAO3M2xfr5JWaP9prH8EzAEzkf5xwMmuKZgCPW9/2hmD1AdL3ck2cqK8FkuASAwgQAChMAKEwAoDABgMIEAAoTAChMAKAwAYDCBAAKEwAoTACgMAGAwgQAChMAKEwAoDABgMIEAAoTAChMAKAwAYDCBAAKEwAoTACgMAGAwgQAChMAKEwAoDABgMIEAAoTAChMAKAwAYDCBAAKEwAoTACgMAGAwgSAeWwIJkUAmMeWYFIEgHlsDyZFAJjHA8GkCADzeDqYFAFgHvv6vr83mAwBYB7DqwCfZARuDSZBAJjXrpwjGYE3cu4LRq0LRisX8FDMFvJq+jXnWM5POX3OmdXjP13KOZzzXtd1Z4ImbAxYzg0596/OWuzOeSlogkuAcetjfPYGzRCAcfs9xsdvEzZEAMbtfMASBGDcBIClCMC4/RywBAEYtx8DliAA43YiYAkCMG5HA5YgAOP2dcASBGDEuq4b7gGcDFiQAIzf5wELEoDxOxiwIO8GHLm+76+N2WXAthiHy3np4unCjXAGMHK5TMNvA74bsABnABOQZwFb87CSsyPa5wygIc4AJiAX6mweXotxvj2Yq0gAJiIj8GkeDgTMQQCmZX/OW9H2mcCFoBkCMCF5FtDnvJ0fvpxzPNr0XQD/r7wxeH3O/pzjfTvO5TwTNMOrABOXCzfccX8h55WcfbG+f9/vYszesPR9zrc5H+UZyuGgGQJQSMZguOTbmfNozmM5D+fcnnNzLG6433AqZi9DHsn5IWYLP3y8kgv/R9AsAeCvs4QhAjtWj5tj9vDOTX/7tOF7ZVj232J2I+90zBb/VC75pQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAKbpT6jRW4l52+/oAAAAAElFTkSuQmCC"), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();

            foreach (BasePlayer player in BasePlayer.allPlayerList)
                UpdatePermissionCache(player);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void Loaded()
        {
            foreach (Type type in this.GetType().GetNestedTypes(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object[] attribute = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                if (attribute.Length >= 1)
                {
                    PatchClassProcessor patchClassProcessor = this.HarmonyInstance.CreateClassProcessor(type);
                    patchClassProcessor.Patch();
                }
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            UpdatePermissionCache(player);
            CheckConnectionDataConditions(player);
            if (config.AutoShowPanel)
                ConnectionData.Get(player.Connection)?.UI.RenderSidebar();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData != null)
                connectionData.Dispose();
        }

        void OnUserPermissionGranted(string userID, string permName)
        {
            if (!permissions.Contains(permName))
                return;

            BasePlayer player = BasePlayer.FindAwakeOrSleepingByID(ulong.Parse(userID));
            if (player == null)
                return;

            UpdatePermissionCache(player);
            CheckConnectionDataConditions(player);
        }

        void OnUserPermissionRevoked(string userID, string permName)
        {
            if (!permissions.Contains(permName))
                return;

            BasePlayer player = BasePlayer.FindAwakeOrSleepingByID(ulong.Parse(userID));
            if (player == null)
                return;

            UpdatePermissionCache(player);
            CheckConnectionDataConditions(player);
        }

        void OnUserGroupAdded(string userID, string groupName)
        {
            BasePlayer player = BasePlayer.FindAwakeOrSleepingByID(ulong.Parse(userID));
            if (player == null)
                return;

            UpdatePermissionCache(player);
            CheckConnectionDataConditions(player);
        }

        void OnUserGroupRemoved(string userID, string groupName)
        {
            BasePlayer player = BasePlayer.FindAwakeOrSleepingByID(ulong.Parse(userID));
            if (player == null)
                return;

            UpdatePermissionCache(player);
            CheckConnectionDataConditions(player);
        }

        object OnMapMarkerAdd(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (HasPermission(player.userID, PERMISSION_TPMARKER))
            {
                if (player.serverInput.WasDown(BUTTON.DUCK))
                {
                    float y = TerrainMeta.HeightMap.GetHeight(note.worldPosition) + 2.5f;
                    float highestPoint = TerrainMeta.HighestPoint.y + 250f;
                    RaycastHit[] hits = Physics.RaycastAll(note.worldPosition.WithY(highestPoint), Vector3.down, ++highestPoint, Layers.Mask.World | Layers.Mask.Terrain | Layers.Mask.Default, QueryTriggerInteraction.Ignore);
                    if (hits.Length > 0)
                    {
                        GamePhysics.Sort(hits);
                        y = hits.Max(hit => hit.point.y);
                    }
                    if (player.IsFlying)
                        y = Mathf.Max(y, player.transform.position.y);
                    player.Teleport(note.worldPosition.WithY(y));
                    return false;
                }
            }

            if (HasPermission(player.userID, PERMISSION_ALLOW))
            {
                ConnectionData connectionData = ConnectionData.Get(player.Connection);
                if (connectionData == null)
                    return null;

                if (connectionData.IsLayerActive(MapLayer.Text))
                {
                    (BasePlayer player, float distance) minDistancePlayer = (null, float.MaxValue);
                    foreach (BasePlayer basePlayer in connectionData.textPlayers)
                    {
                        float distance = Vector2.Distance(basePlayer.transform.position.XZ2D(), note.worldPosition.XZ2D());
                        if (distance < minDistancePlayer.distance)
                            minDistancePlayer = (basePlayer, distance);
                    }

                    if (minDistancePlayer.distance < 75)
                    {
                        Instance.InvokeActionMenu(connectionData, player, minDistancePlayer.player);
                        return false;
                    }
                }
            }
            return null;
        }

        void OnPlayerPingsSend(BasePlayer player, MapNoteList mapNoteList)
        {
            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData == null)
                return;

            mapNoteList.notes.AddRange(connectionData.notes);
        }

        object OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            return BasePlayer_TeamUpdate_Patch.Prefix(player) ? null : false;
        }
        #endregion

        #region Methods
        private void UpdatePermissionCache(BasePlayer player)
        {
            foreach (string permissionName in permissions)
                permissionCache[(player.userID, permissionName)] = permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private bool HasPermission(ulong userID, string permission)
        {
            if (!Instance.permissionCache.TryGetValue((userID, permission), out bool result))
                return false;

            return result;
        }

        public void SendPingsToClient(BasePlayer player, List<MapNote> notes)
        {
            using (MapNoteList mapNoteList = Facepunch.Pool.Get<MapNoteList>())
            {
                mapNoteList.notes = Facepunch.Pool.Get<List<MapNote>>();
                mapNoteList.notes.AddRange(notes);
                player.ClientRPC<MapNoteList>(global::RpcTarget.Player("Client_ReceivePings", player), mapNoteList);
                Facepunch.Pool.FreeUnmanaged(ref mapNoteList.notes);
            }
        }

        private void CheckConnectionDataConditions(BasePlayer player)
        {
            if (!player.IsConnected)
                return;

            Connection connection = player.Connection;
            ConnectionData connectionData = ConnectionData.Get(connection);
            bool hasPermission = HasPermission(player.userID, PERMISSION_ALLOW);
            if (hasPermission && connectionData == null)
                ConnectionData.GetOrCreate(connection);
            else if (!hasPermission && connectionData != null)
                connectionData.Dispose();
        }

        private void InvokeActionMenu(ConnectionData connectionData, BasePlayer from, BasePlayer to)
        {
            if (config.AdminMenuInsteadOfActionMenu && Instance.AdminMenu != null && Instance.AdminMenu.IsLoaded)
            {
                from.SendConsoleCommand($"adminmenu userinfo.open {to.UserIDString}");
                return;
            }

            connectionData.selectedPlayer = to;
            connectionData.UI.RenderPlayerActionUI(to);
        }

        private static string GetHexColorForTeam(ulong teamID)
        {
            return Convert.ToString((int)Math.Floor((Math.Abs(Math.Sin(teamID) * 16777215))), 16);
        }

        private void ExceptPlayers(List<BasePlayer> list, BasePlayer @for = null, bool rustPlus = false)
        {
            if (@for != null)
                list.Remove(@for);

            for (int i = 0; i < list.Count; i++)
            {
                BasePlayer player = list[i];
                if (HasPermission(player.userID, PERMISSION_INVIS) || rustPlus && HasPermission(player.userID, PERMISSION_RUSTPLUSINVIS))
                    if (list.Remove(player))
                        i--;
            }
        }

        private static AppMarker GetPlayerMarker(global::BasePlayer player)
        {
            AppMarker appMarker = Facepunch.Pool.Get<AppMarker>();
            Vector2 vector = CompanionServer.Util.WorldToMap(player.transform.position);
            appMarker.id = player.net.ID;
            appMarker.type = AppMarkerType.Player;
            appMarker.x = vector.x;
            appMarker.y = vector.y;
            appMarker.steamId = player.userID;
            return appMarker;
        }

        private static bool TryAddAppMarkers(ulong userId, List<AppMarker> markers)
        {
            if (!Instance.HasPermission(userId, PERMISSION_RUSTPLUS))
                return false;

            List<BasePlayer> list = Facepunch.Pool.Get<List<BasePlayer>>();
            list.AddRange(BasePlayer.activePlayerList);
            Instance.ExceptPlayers(list, null, true);
            foreach (BasePlayer player in list)
                markers.Add(GetPlayerMarker(player));
            Facepunch.Pool.FreeUnmanaged(ref list);
            return true;
        }

        private static AppTeamInfo GetAppTeamInfo(ulong userId)
        {
            if (!Instance.HasPermission(userId, PERMISSION_RUSTPLUS))
                return null;

            AppTeamInfo appTeamInfo = Pool.Get<AppTeamInfo>();
            appTeamInfo.members = Pool.Get<List<AppTeamInfo.Member>>();
            List<BasePlayer> list = Facepunch.Pool.Get<List<BasePlayer>>();
            list.AddRange(BasePlayer.activePlayerList);
            Instance.ExceptPlayers(list, null, true);
            foreach (BasePlayer player in list)
            {
                AppTeamInfo.Member member = Pool.Get<AppTeamInfo.Member>();
                Vector2 vector = CompanionServer.Util.WorldToMap(player.transform.position);
                member.steamId = player.userID;
                member.name = (player.displayName ?? "");
                member.x = vector.x;
                member.y = vector.y;
                member.isOnline = player.IsConnected;
                AppTeamInfo.Member member2 = member;
                PlayerLifeStory lifeStory = player.lifeStory;
                member2.spawnTime = ((lifeStory != null) ? lifeStory.timeBorn : 0U);
                member.isAlive = player.IsAlive();
                AppTeamInfo.Member member3 = member;
                PlayerLifeStory previousLifeStory = player.previousLifeStory;
                member3.deathTime = ((previousLifeStory != null) ? previousLifeStory.timeDied : 0U);
                appTeamInfo.members.Add(member);
            }
            Facepunch.Pool.FreeUnmanaged(ref list);
            appTeamInfo.leaderSteamId = 0UL;
            appTeamInfo.mapNotes = Pool.Get<List<AppTeamInfo.Note>>();
            appTeamInfo.leaderMapNotes = Pool.Get<List<AppTeamInfo.Note>>();
            return appTeamInfo;
        }

        #region Notes
        private void AddTempNote(BasePlayer player, ulong noteId, MapNote note, float time)
        {
            if (!player.IsConnected)
                return;

            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData == null)
                return;

            AddTempNote(connectionData, noteId, note, time);
        }
        private void AddTempNote(ConnectionData data, ulong noteId, MapNote note, float time)
        {
            note.totalDuration = Time.realtimeSinceStartup + time;
            if (data.tempNotes.TryGetValue(noteId, out MapNote note2))
                note2.Dispose();

            data.tempNotes[noteId] = note;
        }

        private MapNote GetNote(int icon, int color, Vector3 worldPosition, NetworkableId associatedId = default(NetworkableId))
        {
            return new MapNote
            {
                noteType = 1,
                isPing = true,
                icon = icon,
                colourIndex = color,
                worldPosition = worldPosition,
                associatedId = associatedId
            };
        }

        private void GetPlayerNotes(IEnumerable<BasePlayer> players, List<MapNote> list, bool withoutLabels = false)
        {
            const int icon = 6;
            foreach (BasePlayer player in players)
            {
                int color = player.IsSleeping() ? 3 : 2;
                MapNote note = GetNote(icon, color, player.transform.position, player.net.ID);
                if (!withoutLabels)
                    note.label = player.displayName;
                list.Add(note);
            }
        }

        private void GetTCNotes(IEnumerable<BuildingPrivlidge> privlidges, List<MapNote> list)
        {
            const int icon = 2;
            foreach (BuildingPrivlidge privlidge in privlidges)
            {
                int color = Mathf.Clamp(privlidge.authorizedPlayers.Count - 1, 0, 5);
                MapNote note = GetNote(icon, color, privlidge.transform.position, privlidge.net.ID);
                list.Add(note);
            }
        }

        private void GetTCNotes(IEnumerable<SimplePrivilege> privlidges, List<MapNote> list)
        {
            const int icon = 5;
            foreach (SimplePrivilege privlidge in privlidges)
            {
                int color = Mathf.Clamp(privlidge.authorizedPlayers.Count - 1, 0, 5);
                MapNote note = GetNote(icon, color, privlidge.transform.position, privlidge.net.ID);
                list.Add(note);
            }
        }

        private void GetTCNotes(List<MapNote> list)
        {
            GetTCNotes(BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>(), list);
            GetTCNotes(BaseNetworkable.serverEntities.OfType<SimplePrivilege>(), list);
        }

        private void GetStashNotes(IEnumerable<StashContainer> stashes, List<MapNote> list)
        {
            const int icon = 11;
            foreach (StashContainer stash in stashes)
            {
                if (stash.inventory.itemList.Count == 0)
                    continue;

                int color = Mathf.Clamp(stash.inventory.itemList.Count - 1, 0, 5);
                MapNote note = GetNote(icon, color, stash.transform.position, stash.net.ID);
                list.Add(note);
            }
        }

        private void GetStashNotes(List<MapNote> list)
        {
            GetStashNotes(BaseNetworkable.serverEntities.OfType<StashContainer>(), list);
        }
        
        private void GetSleepingBagNotes(IEnumerable<SleepingBag> sleepingBags, List<MapNote> list)
        {
            foreach (SleepingBag sleepingBag in sleepingBags)
                list.Add(GetSleepingBagNote(sleepingBag));
        }

        private MapNote GetSleepingBagNote(SleepingBag sleepingBag)
        {
            const int icon = 7;
            const int color = 3;
            return GetNote(icon, color, sleepingBag.transform.position, sleepingBag.net.ID);
        }

        private void GetSleepingBagNotes(List<MapNote> list)
        {
            GetSleepingBagNotes(BaseNetworkable.serverEntities.OfType<SleepingBag>(), list);
        }
        #endregion
        #endregion

        #region Commands

        [ChatCommand("amap")]
        private void amap(BasePlayer player, string command, string[] args)
        {
            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData != null)
            {
                if (args.Length > 0 && args[0] == "off")
                    connectionData.Reset();
                else
                    connectionData.UI.RenderSidebar();
            }
        }

        [ConsoleCommand("adminmap.cmd")]
        private void cmd(ConsoleSystem.Arg arg)
        {
            if (arg.Args.Length < 1) return;

            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!HasPermission(player.userID, PERMISSION_ALLOW))
                return;

            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData == null)
                return;

            switch (arg.GetString(0))
            {
                case "player_action":
                    {
                        Configuration.Button button = config.Buttons.ElementAtOrDefault(arg.GetInt(1, -1));
                        if (!string.IsNullOrEmpty(button.Permission) && !HasPermission(player.userID, $"adminmap.{button.Permission}"))
                            break;

                        string[] commands = button.Command.Split(';');
                        for (int i = 0; i < commands.Length; i++)
                        {
                            string command = commands[i];
                            if (command.First() == '/')
                                command = command.Replace(command, $"chat.say \"{command}\"");
                            command = command.Replace("{steamid}", connectionData.selectedPlayer.UserIDString).Replace("{username}", connectionData.selectedPlayer.displayName).Replace("{admin.steamid}", player.UserIDString).Replace("{admin.username}", player.displayName);
                            player.SendConsoleCommand(command);
                        }
                        break;
                    }
                case "teleport":
                    {
                        NetworkableId networkableId = arg.GetEntityID(1);
                        if (!networkableId.IsValid)
                            break;
                        BaseEntity entity = BaseNetworkable.serverEntities.Find(networkableId) as BaseEntity;
                        if (entity)
                            player.Teleport(entity.transform.position);
                        break;
                    }
                case "toggle_layer":
                    {
                        MapLayer mapLayer = (MapLayer)arg.GetInt(1);
                        if (mapLayer == MapLayer.None)
                            break;
                        if (connectionData.IsLayerActive(mapLayer))
                        {
                            connectionData.Layers &= ~mapLayer;

                            if (mapLayer == MapLayer.Text)
                                connectionData.RestoreTeam();
                        }
                        else
                        {
                            connectionData.Layers |= mapLayer;
                        }
                        connectionData.UI.RenderSidebar();
                        break;
                    }
                case "toggle_mod":
                    {
                        MapLayerModification layerModification = (MapLayerModification)arg.GetInt(1);
                        if (layerModification == MapLayerModification.None)
                            break;
                        if (connectionData.HasModification(layerModification))
                            connectionData.Modifications &= ~layerModification;
                        else
                            connectionData.Modifications |= layerModification;
                        connectionData.UI.RenderSidebar();
                        break;
                    }
                case "show_player_teammates":
                    {
                        BasePlayer argPlayer = arg.GetPlayerOrSleeper(1);
                        if (argPlayer == null)
                            break;

                        if (argPlayer.currentTeam == 0)
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"The player is not a member of the team");
                            break;
                        }

                        RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(argPlayer.currentTeam);
                        if (team == null)
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"The team #{argPlayer.currentTeam} not found");
                            break;
                        }

                        List<MapNote> notes = Facepunch.Pool.Get<List<MapNote>>();
                        GetPlayerNotes(team.members.Select(userId => BasePlayer.FindAwakeOrSleepingByID(userId)), notes);
                        foreach (MapNote note in notes)
                        {
                            if (note.colourIndex != 3)
                                note.colourIndex = 5;

                            AddTempNote(connectionData, note.associatedId.Value, note, 15);
                        }
                        Facepunch.Pool.FreeUnmanaged(ref notes);

                        connectionData.updater.SendUpdate();
                        player.ShowToast(GameTip.Styles.Blue_Normal, $"Showing {team.members.Count} teammates for 15 sec.");
                        break;
                    }
                case "show_player_privlidges":
                    {
                        BasePlayer argPlayer = arg.GetPlayerOrSleeper(1);
                        if (argPlayer == null)
                            break;

                        var privlidges = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(p => p.IsAuthed(argPlayer));
                        var privlidges2 = BaseNetworkable.serverEntities.OfType<SimplePrivilege>().Where(p => p.IsAuthed(argPlayer));
                        int privlidgesCount = privlidges.Count() + privlidges2.Count();


                        if (privlidgesCount > 0)
                        {
                            List<MapNote> notes = Facepunch.Pool.Get<List<MapNote>>();
                            GetTCNotes(privlidges, notes);
                            GetTCNotes(privlidges2, notes);
                            foreach (MapNote note in notes)
                                AddTempNote(connectionData, note.associatedId.Value, note, 10);
                            Facepunch.Pool.FreeUnmanaged(ref notes);

                            connectionData.updater.SendUpdate();
                            player.ShowToast(GameTip.Styles.Blue_Normal, $"Showing {privlidgesCount} privlidges for 10 sec.");
                        }
                        else
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"No privlidges of player {argPlayer.displayName} found");
                        }
                        break;
                    }
                case "show_player_sleepingbags":
                    {
                        BasePlayer argPlayer = arg.GetPlayerOrSleeper(1);
                        if (argPlayer == null)
                            break;

                        var sleepingBags = BaseNetworkable.serverEntities.OfType<SleepingBag>().Where(p => p.deployerUserID == argPlayer.userID);
                        int sleepingBagsCount = sleepingBags.Count();


                        if (sleepingBagsCount > 0)
                        {
                            List<MapNote> notes = Facepunch.Pool.Get<List<MapNote>>();
                            GetSleepingBagNotes(sleepingBags, notes);
                            foreach (MapNote note in notes)
                                AddTempNote(connectionData, note.associatedId.Value, note, 10);
                            Facepunch.Pool.FreeUnmanaged(ref notes);

                            connectionData.updater.SendUpdate();
                            player.ShowToast(GameTip.Styles.Blue_Normal, $"Showing {sleepingBagsCount} sleeping bags for 10 sec.");
                        }
                        else
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"No sleeping bags of player {argPlayer.displayName} found");
                        }
                        break;
                    }
                case "show_player_stashes":
                    {
                        BasePlayer argPlayer = arg.GetPlayerOrSleeper(1);
                        if (argPlayer == null)
                            break;

                        var stashes = BaseNetworkable.serverEntities.OfType<StashContainer>().Where(p => p.OwnerID == argPlayer.userID);
                        int stashesCount = stashes.Count();


                        if (stashesCount > 0)
                        {
                            List<MapNote> notes = Facepunch.Pool.Get<List<MapNote>>();
                            GetStashNotes(stashes, notes);
                            foreach (MapNote note in notes)
                                AddTempNote(connectionData, note.associatedId.Value, note, 10);
                            Facepunch.Pool.FreeUnmanaged(ref notes);

                            connectionData.updater.SendUpdate();
                            player.ShowToast(GameTip.Styles.Blue_Normal, $"Showing {stashesCount} stashes for 10 sec.");
                        }
                        else
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"No stashes of player {argPlayer.displayName} found");
                        }
                        break;
                    }
                case "show_owned_item":
                    {
                        int icon = arg.GetInt(3, 10);
                        int color = arg.GetInt(4, 4);

                        BasePlayer argPlayer = arg.GetPlayerOrSleeper(1);
                        if (argPlayer == null)
                            break;

                        string filter = arg.GetString(2, "");
                        var entities = BaseEntity.Util.FindTargetsOwnedBy(argPlayer.userID, filter);
                        int entitiesCount = entities.Count();


                        if (entitiesCount > 0)
                        {
                            foreach (BaseEntity entity in entities)
                            {
                                MapNote note = GetNote(icon, color, entity.transform.position, entity.net.ID);
                                AddTempNote(connectionData, note.associatedId.Value, note, 15);
                            }

                            connectionData.updater.SendUpdate();
                            player.ShowToast(GameTip.Styles.Blue_Normal, $"Showing {entitiesCount} entities for 15 sec.");
                        }
                        else
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"No entities of player {argPlayer.displayName} found");
                        }
                        break;
                    }
            }
        }
        #endregion

        #region Classes

        public class AdminMapper : FacepunchBehaviour
        {
            private BasePlayer player;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating(SendUpdateInternal, 2f, 2f);
            }

            private bool HasPermission()
            {
                return Instance.HasPermission(player.userID, PERMISSION_ALLOW);
            }

            public void SendUpdate()
            {
                CancelInvoke(SendUpdateInternal);
                InvokeRepeating(SendUpdateInternal, 0, 2f);
            }

            private List<BasePlayer> GetPooledPlayerList()
            {
                List<BasePlayer> list = Facepunch.Pool.Get<List<BasePlayer>>();
                list.AddRange(BasePlayer.activePlayerList);
                Instance.ExceptPlayers(list, player);
                return list;
            }

            private void SendUpdateInternal()
            {
                if (player == null || !player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                ConnectionData connectionData = ConnectionData.Get(player.Connection);
                if (!HasPermission())
                {
                    Destroy(this);
                    connectionData.Reset();
                    return;
                }

                connectionData.notes.Clear();
                if (connectionData.Layers == MapLayer.None && connectionData.tempNotes.Count == 0)
                    return;

                for (int i = 0; i < connectionData.tempNotes.Count; i++)
                {
                    var pair = connectionData.tempNotes.ElementAt(i);
                    MapNote note = pair.Value;
                    if (note.totalDuration < Time.realtimeSinceStartup)
                    {
                        connectionData.tempNotes.Remove(pair.Key);
                        i--;
                        continue;
                    }
                }
                List<BasePlayer> players = null;

                bool withoutLabels = connectionData.HasModification(MapLayerModification.NoLabels);
                if (connectionData.IsLayerActive(MapLayer.Player))
                    Instance.GetPlayerNotes(players ??= GetPooledPlayerList(), connectionData.notes, withoutLabels);
                if (connectionData.IsLayerActive(MapLayer.Sleeper))
                    Instance.GetPlayerNotes(BasePlayer.sleepingPlayerList, connectionData.notes, withoutLabels);
                if (connectionData.IsLayerActive(MapLayer.TC))
                    Instance.GetTCNotes(connectionData.notes);
                if (connectionData.IsLayerActive(MapLayer.Stash))
                    Instance.GetStashNotes(connectionData.notes);
                if (connectionData.IsLayerActive(MapLayer.SleepingBag))
                    Instance.GetSleepingBagNotes(connectionData.notes);


                connectionData.notes.AddRange(connectionData.tempNotes.Values);


                if (connectionData.IsLayerActive(MapLayer.Text))
                {
                    connectionData.textPlayers.Clear();
                    players ??= GetPooledPlayerList();
                    using (PlayerTeam playerTeam = Facepunch.Pool.Get<PlayerTeam>())
                    {
                        playerTeam.members = Facepunch.Pool.Get<List<PlayerTeam.TeamMember>>();
                        for (int i = 0; i < players.Count; i++)
                        {
                            BasePlayer player = players[i];
                            PlayerTeam.TeamMember teamMember = Facepunch.Pool.Get<PlayerTeam.TeamMember>();
                            string color = config.TextMap.SoloColor;
                            if (player.IsSleeping())
                                color = config.TextMap.SleeperColor;
                            else if (player.currentTeam > 0)
                                color = (config.TextMap.UseGeneratedTeamsColors ? GetHexColorForTeam(player.currentTeam) : config.TextMap.TeamColor);
                            teamMember.displayName = $"\t\t\t<size={config.TextMap.FontSize * 10}><color=#{color}>{player.displayName}</color></size>\t\t\t";
                            teamMember.healthFraction = 1;
                            teamMember.online = true;
                            teamMember.wounded = true;
                            teamMember.position = player.transform.position;
                            teamMember.userID = (ulong)i;
                            playerTeam.members.Add(teamMember);
                            connectionData.textPlayers.Add(player);
                        }
                        player.ClientRPC<PlayerTeam>(global::RpcTarget.PlayerAndSpectators("CLIENT_ReceiveTeamInfo", player), playerTeam);
                    }
                }
                player.SendPingsToClient();

                if (players != null)
                    Facepunch.Pool.FreeUnmanaged(ref players);
            }
        }

        public class ConnectionData : IDisposable
        {
            public Connection connection;
            public List<MapNote> notes = new List<MapNote>();
            public Dictionary<ulong, MapNote> tempNotes = new Dictionary<ulong, MapNote>();
            public List<BasePlayer> textPlayers = new List<BasePlayer>();
            public BasePlayer selectedPlayer;
            public AdminMapper updater;
            private MapLayer layers = MapLayer.None;
            private MapLayerModification modifications = MapLayerModification.None;

            public ConnectionUI UI { get; }

            public ConnectionData(Connection connection)
            {
                this.connection = connection;
                this.UI = new ConnectionUI(this);

                BasePlayer player = this.Player;
                if (player == null || !player.IsConnected)
                    return;

                updater = player.gameObject.AddComponent<AdminMapper>();
            }

            public BasePlayer Player
            {
                get
                {
                    return connection.player as BasePlayer;
                }
            }

            public MapLayer Layers
            {
                get
                {
                    return layers;
                }
                set
                {
                    layers = value;

                    if (layers == MapLayer.None)
                        Reset();

                    updater.SendUpdate();
                }
            }

            public MapLayerModification Modifications
            {
                get
                {
                    return modifications;
                }
                set
                {
                    modifications = value;
                    updater?.SendUpdate();
                }
            }

            public void Reset()
            {
                notes.Clear();
                textPlayers.Clear();
                layers = MapLayer.None;
                modifications = MapLayerModification.None;
                selectedPlayer = null;
                RestoreTeam();
                Player.SendPingsToClient();
                UI.Dispose();
            }
            public void RestoreTeam()
            {
                BasePlayer player = this.Player;
                if (player.currentTeam != 0UL)
                    player.TeamUpdate();
                else
                    player.ClientRPC(RpcTarget.PlayerAndSpectators("CLIENT_ClearTeam", player));
            }

            public bool IsLayerActive(MapLayer mapType)
            {
                return (this.layers & mapType) == mapType;
            }
            public bool HasModification(MapLayerModification modification)
            {
                return (this.modifications & modification) == modification;
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(updater);
                Reset();
                all.Remove(connection);
            }

            public static Dictionary<Connection, ConnectionData> all = new Dictionary<Connection, ConnectionData>();

            public static ConnectionData Get(Connection connection)
            {
                if (connection == null)
                    return null;

                ConnectionData data;
                if (all.TryGetValue(connection, out data))
                    return data;
                return null;
            }

            public static ConnectionData GetOrCreate(Connection connection)
            {
                if (connection == null)
                    return null;

                ConnectionData data = Get(connection);
                if (data == null)
                    data = all[connection] = new ConnectionData(connection);
                return data;
            }

        }

        public class ConnectionUI : IDisposable
        {
            private Connection connection;
            private ConnectionData connectionData;
            private static (Func<ConnectionData, bool> condition, string command, string icon, (string min, string max) offset)[] sidebarButtonsProperties = new (Func<ConnectionData, bool>, string, string, (string, string))[7]
            {
                ((ConnectionData cd) => cd.IsLayerActive(MapLayer.Player), $"adminmap.cmd toggle_layer {(int)MapLayer.Player}", "assets/content/ui/map/icon-map_pin.png", ("0 1", "0 1")),
                ((ConnectionData cd) =>  cd.IsLayerActive(MapLayer.Text), $"adminmap.cmd toggle_layer {(int)MapLayer.Text}", LetterTCRC, ("-0.5 0", "-0.5 0")),
                ((ConnectionData cd) => cd.IsLayerActive(MapLayer.Sleeper), $"adminmap.cmd toggle_layer {(int)MapLayer.Sleeper}", "assets/content/ui/map/icon-map_sleep.png", ("0 0", "0 0")),
                ((ConnectionData cd) => cd.IsLayerActive(MapLayer.TC), $"adminmap.cmd toggle_layer {(int)MapLayer.TC}", "assets/content/ui/map/icon-map_home.png", ("-4 -5", "4 3")),
                ((ConnectionData cd) => cd.IsLayerActive(MapLayer.Stash), $"adminmap.cmd toggle_layer {(int)MapLayer.Stash}", "assets/prefabs/deployable/small stash/small_stash.png", ("6 6",  "-6 -6")),
                ((ConnectionData cd) => cd.IsLayerActive(MapLayer.SleepingBag), $"adminmap.cmd toggle_layer {(int)MapLayer.SleepingBag}", "assets/prefabs/deployable/sleeping bag/sleepingbag.png", ("6 6",  "-6 -6")),
                ((ConnectionData cd) => cd.HasModification(MapLayerModification.NoLabels), $"adminmap.cmd toggle_mod {(int)MapLayerModification.NoLabels}", "assets/content/ui/hypnotized.png", ("4 4", "-4 -4")),
            };

            public ConnectionUI(ConnectionData connectionData)
            {
                this.connectionData = connectionData;
                this.connection = connectionData.connection;
            }

            public void RenderSidebar()
            {
                const string lockedButtonBackground = "0.723 0.035 0 1";
                const string lockedButtonIcon = "0.902 0.282 0.2 1";
                const string disabledButtonBackground = "0.31 0.302 0.294 1";
                const string disabledButtonIcon = "0.565 0.545 0.529 1";
                const string enabledButtonBackground = "0.3568628 0.4431373 0.2235294 1";
                const string enabledButtonIcon = "0.5490196 0.7764707 0.1921569 1";

                CUI.Root root = new CUI.Root("OverlayNonScaled");

                float height = 7 * 32.8f;
                float halfHeight = height * 0.5f;

                var container = root.AddContainer(
                    anchorMin: "0 0.5",
                    anchorMax: "0 0.5",
                    offsetMin: $"3 -{halfHeight}",
                    offsetMax: $"39 {halfHeight}",
                    name: "ADMINMAP_SIDEBAR").AddDestroySelfAttribute();

                container.AddHImage(
                      CapsuleBackgroundVerticalCRC,
                      color: "0 0 0 1");

                for (int i = 0; i < sidebarButtonsProperties.Length; i++)
                {
                    var prop = sidebarButtonsProperties[i];
                    string command = prop.command;
                    string icon = prop.icon;
                    bool isIconCRC = icon.IsNumeric();
                    bool condition = prop.condition.Invoke(connectionData);
                    string backgroundColor = string.IsNullOrEmpty(command) ? lockedButtonBackground : (condition ? enabledButtonBackground : disabledButtonBackground);
                    string iconColor = string.IsNullOrEmpty(command) ? lockedButtonIcon : (condition ? enabledButtonIcon : disabledButtonIcon);
                    var circle = container.AddButton(
                        command: command,
                        color: backgroundColor,
                        sprite: "assets/icons/circle_closed.png",
                        material: CUI.Defaults.IconMaterial,
                        imageType: UnityEngine.UI.Image.Type.Simple,
                        anchorMin: "0.5 1",
                        anchorMax: "0.5 1",
                        offsetMin: $"-16 -{(i + 1) * 32 + 2.2f}",
                        offsetMax: $"16 -{i * 32 + 2.2f}");
                    if (isIconCRC)
                    {
                        circle.AddHImage(
                            icon,
                            color: iconColor,
                            anchorMin: "0 0",
                            anchorMax: "1 1",
                            offsetMin: prop.offset.min,
                            offsetMax: prop.offset.max);
                    }
                    else
                    {
                        circle.AddPanel(
                           color: iconColor,
                           sprite: icon,
                           material: CUI.Defaults.IconMaterial,
                           imageType: UnityEngine.UI.Image.Type.Simple,
                           anchorMin: "0 0",
                           anchorMax: "1 1",
                           offsetMin: prop.offset.min,
                           offsetMax: prop.offset.max);
                    }
                }

                root.Render(connection);
            }

            public void RenderPlayerActionUI(BasePlayer target)
            {
                const float fadeIn = 0.3f;
                const float fadeOut = 0.5f;
                const int destroyTime = 5;

                const int maxButtonCountPerRow = 10;
                const float buttonSize = 40f;

                List<Configuration.Button> buttons = Facepunch.Pool.Get<List<Configuration.Button>>();
                foreach (Configuration.Button button in config.Buttons)
                {
                    if (!string.IsNullOrEmpty(button.Permission) && !Instance.HasPermission(connection.userid, $"adminmap.{button.Permission}"))
                        continue;
                    buttons.Add(button);
                }

                int buttonCount = buttons.Count;
                int rowCount = Mathf.CeilToInt(buttonCount / (float)maxButtonCountPerRow);

                float width = Mathf.Clamp(buttonSize * buttonCount, 0, buttonSize * maxButtonCountPerRow);
                float height = buttonSize * rowCount;
                float halfWidth = width * 0.5f;

                CUI.Root root = new CUI.Root("OverlayNonScaled");
                var container = root.AddContainer(name: "PlayerActionUI").AddDestroySelfAttribute();
                var panel = container.AddPanel(
                    color: "0 0 0 0.3",
                    sprite: "assets/content/ui/ui.background.rounded.png",
                    imageType: Image.Type.Tiled,
                    anchorMin: "0.5 0.3",
                    anchorMax: "0.5 0.3",
                    offsetMin: $"-{halfWidth} -{height}",
                    offsetMax: $"{halfWidth} 0");


                panel.AddPanel(
                    color: "0 0 0 0.3",
                    sprite: "assets/content/ui/ui.background.rounded.png",
                    imageType: Image.Type.Tiled,
                    anchorMin: "0 1",
                    anchorMax: "1 1",
                    offsetMin: "0 3",
                    offsetMax: "0 23")
                    .AddText(
                          text: $"{target.displayName} ({target.UserIDString})",
                          font: CUI.Font.RobotoCondensedBold,
                          fontSize: 10,
                          align: TextAnchor.MiddleCenter,
                          overflow: VerticalWrapMode.Truncate,
                          offsetMin: "5 0",
                          offsetMax: "-5 0");

                for (int i = 0; i < buttonCount; i++)
                {
                    Configuration.Button buttonSettings = buttons.ElementAtOrDefault(i);
                    if (buttonSettings == null)
                        break;

                    int row = i / maxButtonCountPerRow;
                    int column = i % maxButtonCountPerRow;

                    panel.AddButton(
                        command: $"adminmap.cmd player_action {i}",
                        close: "PlayerActionUI",
                        sprite: "assets/icons/circle_open.png",
                        material: "assets/icons/iconmaterial.mat",
                        color: buttonSettings.ColorString,
                        anchorMin: "0 1",
                        anchorMax: "0 1",
                        offsetMin: $"{40 * column} -{40 * (row + 1)}",
                        offsetMax: $"{40 * (column + 1)} -{40 * row}")
                        .AddText(
                            text: buttonSettings.Label,
                            color: buttonSettings.ColorString,
                            font: CUI.Font.RobotoCondensedBold,
                            fontSize: 6,
                            align: TextAnchor.MiddleCenter);
                }

                for (int i = 1; i < root.Container.Count; i++)
                {
                    CUI.Element element = root.Container[i];
                    element.WithFade(fadeIn, fadeOut);
                    element.Components.AddCountdown(endTime: destroyTime);
                }

                root.Render(connection);
                Facepunch.Pool.FreeUnmanaged(ref buttons);
            }

            public void RenderPrivlidgeUI(NetworkableId networkableId, IEnumerable<PlayerNameID> authorizedUsers, List<Item> items, string upkeepTime)
            {
                CUI.Root root = new CUI.Root("OverlayNonScaled");
                {
                    CUI.Element DJHiAC = root.AddContainer(
                        anchorMin: "0.5 0.325",
                        anchorMax: "0.5 0.325",
                        offsetMin: "-155 -60",
                        offsetMax: "155 60",
                        name: "PrivlidgeUI").AddDestroySelfAttribute();
                    DJHiAC.Components.AddCountdown(endTime: 10);
                    {
                        CUI.Element VdEpeV = DJHiAC.AddPanel(
                            sprite: "assets/content/ui/ui.background.rounded.png",
                            color: "0 0 0 0.2980392",
                            imageType: UnityEngine.UI.Image.Type.Tiled,
                            anchorMin: "0 1",
                            anchorMax: "0 1",
                            offsetMin: "0 -120",
                            offsetMax: "100 0"
                            /* name: "Panel" */);
                        {
                            CUI.Element TjfedI = VdEpeV.AddPanel(
                                sprite: "assets/icons/circle_closed_white_toedge.png",
                                material: "assets/icons/iconmaterial.mat",
                                color: "0 0 0 0.5019608",
                                imageType: UnityEngine.UI.Image.Type.Simple,
                                anchorMin: "0.5 0.5",
                                anchorMax: "0.5 0.5",
                                offsetMin: "-26.496 -4.396",
                                offsetMax: "26.496 48.596"
                                /* name: "Panel" */);

                            string home_color;
                            switch (authorizedUsers.Count())
                            {
                                case 0:
                                    home_color = "0.9 0.9 0.9 1";
                                    break;
                                case 1:
                                    home_color = "0.812 0.824 0.333 1";
                                    break;
                                case 2:
                                    home_color = "0.173 0.412 0.702 1";
                                    break;
                                case 3:
                                    home_color = "0.439 0.62 0.212 1";
                                    break;
                                case 4:
                                    home_color = "0.682 0.212 0.208 1";
                                    break;
                                case 5:
                                    home_color = "0.643 0.329 0.69 1";
                                    break;
                                case 6:
                                default:
                                    home_color = "0.059 0.827 0.678 1";
                                    break;
                            }

                            TjfedI.AddPanel(
                                sprite: "assets/content/ui/map/icon-map_home.png",
                                material: "assets/icons/iconmaterial.mat",
                                color: home_color,
                                imageType: UnityEngine.UI.Image.Type.Simple,
                                anchorMin: "0.5 0.5",
                                anchorMax: "0.5 0.5",
                                offsetMin: "-26.496 -26.496",
                                offsetMax: "26.496 26.496"
                                /* name: "Panel (1)" */);
                        }
                        {
                            CUI.Element bJkGDe = VdEpeV.AddButton(
                                command: $"adminmap.cmd teleport {networkableId}",
                                close: "PrivlidgeUI",
                                color: "0.7264151 0.2448715 0.2227216 0.4",
                                sprite: "assets/content/ui/ui.background.rounded.png",
                                imageType: UnityEngine.UI.Image.Type.Tiled,
                                anchorMin: "0.5 0.5",
                                anchorMax: "0.5 0.5",
                                offsetMin: "-29.566 -44.572",
                                offsetMax: "29.566 -25.028"
                                /* name: "Button" */);
                            bJkGDe.AddText(
                                text: "TELEPORT",
                                color: "0.9019608 0.9019608 0.9019608 1",
                                font: CUI.Font.RobotoCondensedBold,
                                fontSize: 8,
                                align: TextAnchor.MiddleCenter,
                                overflow: VerticalWrapMode.Overflow,
                                anchorMin: "0 0",
                                anchorMax: "1 1",
                                offsetMin: "0 0",
                                offsetMax: "0 0"
                                /* name: "Text" */);
                        }
                    }
                    {
                        CUI.Element YXJXMH = DJHiAC.AddPanel(
                            sprite: "assets/content/ui/ui.background.rounded.png",
                            color: "0 0 0 0.2980392",
                            imageType: UnityEngine.UI.Image.Type.Tiled,
                            anchorMin: "0 1",
                            anchorMax: "0 1",
                            offsetMin: "105 -120",
                            offsetMax: "205 0"
                            /* name: "Panel (1)" */);
                        YXJXMH.AddText(
                            text: "Users:",
                            color: "0.9 0.9 0.9 1",
                            font: CUI.Font.RobotoCondensedBold,
                            fontSize: 8,
                            align: TextAnchor.MiddleCenter,
                            overflow: VerticalWrapMode.Overflow,
                            anchorMin: "0 1",
                            anchorMax: "1 1",
                            offsetMin: "0 -13",
                            offsetMax: "0 -3"
                            /* name: "Text" */);

                        var authUsers = authorizedUsers;
                        int authUsersCount = authUsers.Count();

                        var scrollArea = YXJXMH.AddPanel(
                            sprite: "assets/content/ui/ui.rounded.tga",
                            color: "0 0 0 0.2",
                            imageType: UnityEngine.UI.Image.Type.Tiled,
                            anchorMin: "0.5 0.5",
                            anchorMax: "0.5 0.5",
                            offsetMin: "-45 -57",
                            offsetMax: "45 43"
                            /* name: "Panel" */);
                        scrollArea.Components.AddScrollView(
                            vertical: true,
                            scrollSensitivity: 10f,
                            verticalScrollbar: new CuiScrollbar
                            {
                                Size = 8,
                                HandleColor = "0.05 0.05 0.05 1",
                                HighlightColor = "0.1 0.1 0.1 1",
                                PressedColor = "0.2 0.2 0.2 1",
                                TrackColor = "0 0 0 0.5",
                                AutoHide = true,
                            },
                            anchorMin: "0 1",
                            anchorMax: "1 1",
                            offsetMin: $"0 -{10 + 15 * authUsersCount}");
                        for (int i = 0; i < authUsersCount; i++)
                        {
                            scrollArea.AddText(
                                text: authUsers.ElementAt(i).username,
                                fontSize: 8,
                                align: TextAnchor.MiddleCenter,
                                anchorMin: "0 1",
                                anchorMax: "1 1",
                                offsetMin: $"0 {-5 + -15 * (i + 1)}",
                                offsetMax: $"0 {-15 * i}");
                        }
                    }
                    {
                        int hqm = 0, hqm_ore = 0, metal = 0, metal_ore = 0, sulfur = 0, sulfur_ore = 0, stones = 0, wood = 0;

                        if (items != null)
                        {
                            foreach (Item item in items)
                            {
                                switch (item.info.itemid)
                                {
                                    case 317398316:
                                        hqm += item.amount;
                                        break;
                                    case -1982036270:
                                        hqm_ore += item.amount;
                                        break;
                                    case 69511070:
                                        metal += item.amount;
                                        break;
                                    case -4031221:
                                        metal_ore += item.amount;
                                        break;
                                    case -1581843485:
                                        sulfur += item.amount;
                                        break;
                                    case -1157596551:
                                        sulfur_ore += item.amount;
                                        break;
                                    case -2099697608:
                                        stones += item.amount;
                                        break;
                                    case -151838493:
                                        wood += item.amount;
                                        break;
                                }
                            }
                        }
                        {
                            CUI.Element QJXwox = DJHiAC.AddPanel(
                                sprite: "assets/content/ui/ui.background.rounded.png",
                                color: "0 0 0 0.2980392",
                                imageType: UnityEngine.UI.Image.Type.Tiled,
                                anchorMin: "0 1",
                                anchorMax: "0 1",
                                offsetMin: "210 -120",
                                offsetMax: "310 0"
                                /* name: "Panel (2)" */);
                            var reVgWk_formatedUpkeepTime = "Upkeep: " + (upkeepTime ?? "Unknown");
                            QJXwox.AddText(
                                text: reVgWk_formatedUpkeepTime,
                                color: "0.9 0.9 0.9 1",
                                font: CUI.Font.RobotoCondensedBold,
                                fontSize: 9,
                                align: TextAnchor.MiddleCenter,
                                overflow: VerticalWrapMode.Overflow,
                                anchorMin: "0 1",
                                anchorMax: "1 1",
                                offsetMin: "0 -14.782",
                                offsetMax: "0 -2.782"
                                /* name: "Text" */);
                            QJXwox.AddText(
                                text: "Materials:",
                                color: "0.9 0.9 0.9 1",
                                font: CUI.Font.RobotoCondensedBold,
                                fontSize: 9,
                                align: TextAnchor.MiddleCenter,
                                overflow: VerticalWrapMode.Overflow,
                                anchorMin: "0 1",
                                anchorMax: "1 1",
                                offsetMin: "0 -28.2",
                                offsetMax: "0 -16.2"
                                /* name: "Text (1)" */);
                            {
                                CUI.Element ivCusv = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 19.7",
                                    offsetMax: "50 29.7"
                                    /* name: "Container (5)" */);
                                var EuJSFV_amount = hqm.ToString();
                                ivCusv.AddText(
                                    text: EuJSFV_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                ivCusv.AddPanel(
                                    sprite: "assets/prefabs/resource/hq metal/metal_refined.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element vYJWgZ = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 9.7",
                                    offsetMax: "50 19.7"
                                    /* name: "Container (1)" */);
                                var CCUxKF_amount = metal.ToString();
                                vYJWgZ.AddText(
                                    text: CCUxKF_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                vYJWgZ.AddPanel(
                                    sprite: "assets/prefabs/resource/metal fragments/metal_fragments.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element cUwhBy = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -0.3",
                                    offsetMax: "50 9.7"
                                    /* name: "Container (2)" */);
                                var TTUoFy_amount = stones.ToString();
                                cUwhBy.AddText(
                                    text: TTUoFy_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                cUwhBy.AddPanel(
                                    sprite: "assets/prefabs/resource/stone/stones.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element lVzjNc = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -10",
                                    offsetMax: "50 0"
                                    /* name: "Container (3)" */);
                                var QPHozQ_amount = wood.ToString();
                                lVzjNc.AddText(
                                    text: QPHozQ_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                lVzjNc.AddPanel(
                                    sprite: "assets/prefabs/resource/wood/wood.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element PNtNXT = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -20",
                                    offsetMax: "50 -10"
                                    /* name: "Container (4)" */);
                                var aXcgMO_amount = sulfur.ToString();
                                PNtNXT.AddText(
                                    text: aXcgMO_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                PNtNXT.AddPanel(
                                    sprite: "assets/prefabs/resource/sulfur/sulfur.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element xaDeEX = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -30",
                                    offsetMax: "50 -20"
                                    /* name: "Container" */);
                                var UHJkqW_amount = hqm_ore.ToString();
                                xaDeEX.AddText(
                                    text: UHJkqW_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                xaDeEX.AddPanel(
                                    sprite: "assets/prefabs/resource/hq metal ore/hq_metal_ore.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element ToOjRw = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -40",
                                    offsetMax: "50 -30"
                                    /* name: "Container (6)" */);
                                var dSANBf_amount = metal_ore.ToString();
                                ToOjRw.AddText(
                                    text: dSANBf_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                ToOjRw.AddPanel(
                                    sprite: "assets/prefabs/resource/metal ore/metal_ore.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                            {
                                CUI.Element TumtFk = QJXwox.AddContainer(
                                    anchorMin: "0.5 0.5",
                                    anchorMax: "0.5 0.5",
                                    offsetMin: "-50 -50.3",
                                    offsetMax: "50 -40.3"
                                    /* name: "Container (7)" */);
                                var MZJDUl_amount = sulfur_ore.ToString();
                                TumtFk.AddText(
                                    text: MZJDUl_amount,
                                    color: "0.9 0.9 0.9 1",
                                    font: CUI.Font.RobotoCondensedRegular,
                                    fontSize: 10,
                                    align: TextAnchor.UpperLeft,
                                    overflow: VerticalWrapMode.Overflow,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-15.803 0",
                                    offsetMax: "30.279 0"
                                    /* name: "Text" */);
                                TumtFk.AddPanel(
                                    sprite: "assets/prefabs/resource/sulfur ore/sulfur_ore.png",
                                    imageType: UnityEngine.UI.Image.Type.Simple,
                                    anchorMin: "0.5 0",
                                    anchorMax: "0.5 1",
                                    offsetMin: "-28.1 0",
                                    offsetMax: "-18.1 0"
                                    /* name: "Panel" */);
                            }
                        }
                    }
                }
                root.Render(connection);
            }

            public void Dispose()
            {
                BasePlayer player = connection.player as BasePlayer;
                if (!player.IsConnected)
                    return;
                CuiHelper.DestroyUi(player, "ADMINMAP_SIDEBAR");
                CuiHelper.DestroyUi(player, "PlayerActionUI");
                CuiHelper.DestroyUi(player, "PrivlidgeUI");
            }
        }
        #endregion

        #region Patches
        [HarmonyPatch(typeof(MapMarkers), "Execute")]
        private static class MapMarkers_Execute_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
            {
                FieldInfo AppMapMarkers_markers_Field = AccessTools.Field(typeof(AppMapMarkers), nameof(AppMapMarkers.markers));
                CodeInstruction leaveS = instructions.FirstOrDefault(i => i.opcode == OpCodes.Leave_S);
                var jmpAddress = leaveS.operand;
                foreach (CodeInstruction instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.Is(OpCodes.Stfld, AppMapMarkers_markers_Field))
                    {
                        yield return CodeInstruction.LoadArgument(0);
                        yield return CodeInstruction.Call(typeof(BasePlayerHandler<ProtoBuf.AppEmpty>), "get_UserId");
                        yield return CodeInstruction.LoadLocal(0);
                        yield return new CodeInstruction(OpCodes.Ldfld, AppMapMarkers_markers_Field);
                        yield return CodeInstruction.Call(typeof(AdminMap), nameof(AdminMap.TryAddAppMarkers));
                        yield return new CodeInstruction(OpCodes.Brtrue, jmpAddress);
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(TeamInfo), "Execute")]
        private static class TeamInfo_Execute_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
            {
                CodeInstruction getResponse = instructions.FirstOrDefault(i => i.opcode == OpCodes.Call && i.operand.ToString().Contains("AppResponse"));
                var jmpAddress = getResponse.labels[0];
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.Call(typeof(BasePlayerHandler<ProtoBuf.AppEmpty>), "get_UserId");
                yield return CodeInstruction.Call(typeof(AdminMap), nameof(AdminMap.GetAppTeamInfo));
                yield return new CodeInstruction(OpCodes.Stloc_0);
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                yield return new CodeInstruction(OpCodes.Brtrue, jmpAddress);
                foreach (CodeInstruction instruction in instructions)
                    yield return instruction;
            }
        }

        [HarmonyPatch(typeof(BasePlayer), "SendPingsToClient")]
        private static class BasePlayer_SendPingsToClient_Patch
        {
            private static bool Prefix(BasePlayer __instance)
            {
                if (__instance.State.pings == null)
                    __instance.State.pings = new List<MapNote>();

                return true;
            }
        }
        [HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.TeamUpdate), new Type[] { typeof(bool) })]
        private static class BasePlayer_TeamUpdate_Patch
        {
            internal static bool Prefix(BasePlayer __instance)
            {
                return ConnectionData.Get(__instance.Connection)?.IsLayerActive(MapLayer.Text) != true;
            }
        }
        [HarmonyPatch(typeof(BaseGameMode), nameof(BaseGameMode.Save))]
        private static class BaseGameMode_Save_Patch
        {
            private static void Postfix(BaseGameMode __instance, BaseNetworkable.SaveInfo info)
            {
                Connection connection = info.forConnection;
                if (connection != null && !__instance.ingameMap && Instance.HasPermission(connection.userid, PERMISSION_ALLOW))
                    info.msg.baseNetworkable.prefabID = 2957505463;
            }
        }

        [HarmonyPatch(typeof(BasePlayer), "Server_RemovePing")]
        private static class BasePlayer_Server_RemovePing_Patch
        {
            private static bool Prefix(BasePlayer __instance, BaseEntity.RPCMessage msg)
            {
                if (!Instance.HasPermission(__instance.userID, PERMISSION_ALLOW))
                    return true;

                int index = msg.read.Int32();
                msg.read.Position = 13;
                if (__instance.State != null)
                    index -= __instance.State.pings.Count;
                if (index < 0)
                    return true;

                ConnectionData connectionData = ConnectionData.GetOrCreate(__instance.Connection);
                if (connectionData.Layers == MapLayer.None && connectionData.notes == null && connectionData.tempNotes == null)
                    return true;

                MapNote mapNote;

                if (index < connectionData.notes.Count)
                {
                    mapNote = connectionData.notes[index];
                }
                else
                {
                    index -= connectionData.notes.Count;
                    if (index > -1 && index < connectionData.tempNotes.Count)
                        mapNote = connectionData.tempNotes.Values.ElementAt(index);
                    else
                        return true;
                }

                NetworkableId associatedEntityId = mapNote.associatedId;
                if (associatedEntityId.IsValid)
                {
                    BaseEntity entity = BaseNetworkable.serverEntities.Find(mapNote.associatedId) as BaseEntity;
                    if (entity)
                    {
                        InputState input = __instance.serverInput;
                        if (input.WasDown(BUTTON.SPRINT))
                        {
                            TeleportToEntity();
                        }
                        else
                        {
                            if (entity is BasePlayer player)
                            {
                                Instance.InvokeActionMenu(connectionData, __instance, player);
                            }
                            else if (entity is BuildingPrivlidge privlidge)
                            {
                                connectionData.UI.RenderPrivlidgeUI(privlidge.net.ID, privlidge.authorizedPlayers, privlidge.inventory.itemList, BuildingPrivlidge.FormatUpkeepMinutes(privlidge.GetProtectedMinutes()));
                            }
                            else if (entity is SimplePrivilege simplePrivilege)
                            {
                                connectionData.UI.RenderPrivlidgeUI(simplePrivilege.net.ID, simplePrivilege.authorizedPlayers, null, null);
                            }
                            else if (entity is SleepingBag sleepingBag)
                            {
                                NetworkableId netId = sleepingBag.net.ID;
                                if (netId.IsValid && !connectionData.tempNotes.ContainsKey(netId.Value))
                                {
                                    MapNote note = Instance.GetSleepingBagNote(sleepingBag);
                                    note.label = ServerMgr.Instance.persistance.GetPlayerName(sleepingBag.deployerUserID) ?? $"Unknown ({sleepingBag.deployerUserID})";
                                    Instance.AddTempNote(connectionData, note.associatedId.Value, note, 3f);
                                    connectionData.updater.SendUpdate();
                                }
                                else
                                {
                                    TeleportToEntity();
                                }
                            }
                            else
                            {
                                TeleportToEntity();
                            }
                        }


                        void TeleportToEntity()
                        {
                            if (entity != __instance)
                                __instance.Teleport(entity.transform.position);
                        }
                    }
                }
                return false;
            }
        }
        #endregion

        #region Config
        static Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Auto show sidebar panel")]
            public bool AutoShowPanel { get; set; } = true;
            [JsonProperty(PropertyName = "Open the admin menu instead of the action menu")]
            public bool AdminMenuInsteadOfActionMenu { get; set; } = false;
            [JsonProperty(PropertyName = "Text Map Settings")]
            public TextMapSettings TextMap { get; set; } = new TextMapSettings();
            [JsonProperty(PropertyName = "Command Buttons")]
            public List<Button> Buttons { get; set; } = new List<Button>();


            public class TextMapSettings
            {
                [JsonProperty(PropertyName = "Font size")]
                public float FontSize { get; set; } = 8;
                [JsonProperty(PropertyName = "Use color generation for teams?")]
                public bool UseGeneratedTeamsColors { get; set; } = true;
                [JsonProperty(PropertyName = "Color for team")]
                public string TeamColor { get; set; } = "ffaf4d";
                [JsonProperty(PropertyName = "Color for solo player")]
                public string SoloColor { get; set; } = "9bd92f";
                [JsonProperty(PropertyName = "Color for sleeper")]
                public string SleeperColor { get; set; } = "404040";
            }

            public class Button
            {
                public Button() { }
                public Button(string permission, string label, string command)
                {
                    this.Permission = permission;
                    this.Label = label;
                    this.Command = command;
                }
                public Button(string permission, string label, string command, string color) : this(permission, label, command)
                {
                    this.ColorString = color;
                }

                [JsonProperty(PropertyName = "Permission (adminmap.<perm>)")]
                public string Permission { get; set; } = string.Empty;

                [JsonProperty(PropertyName = "Label")]
                public string Label { get; set; } = string.Empty;

                [JsonProperty(PropertyName = "Command")]
                public string Command { get; set; } = string.Empty;

                [JsonProperty(PropertyName = "Color")]
                public string ColorString { get; set; } = "1 1 1 1";
            }

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Buttons = new List<Button>
                    {
                        new Button(string.Empty, "TP", "teleport {steamid}"),
                        new Button(string.Empty, "TP2ME", "teleport {steamid} {admin.steamid}"),
                        new Button(string.Empty, "INV", "/viewinv {username}"),
                        new Button(string.Empty, "SPECTATE", "spectate {steamid}"),
                        new Button(string.Empty, "KILL", "kill {steamid}", "0.9 0.1 0.25 1"),
                        new Button(string.Empty, "KICK", "kick {steamid}", "0.9 0.1 0.25 1"),
                        new Button(string.Empty, "SHOW\nTEAMMATES", "adminmap.cmd show_player_teammates {steamid}"),
                        new Button(string.Empty, "SHOW\nPRIVLIDGES", "adminmap.cmd show_player_privlidges {steamid}"),
                        new Button(string.Empty, "SHOW\nSLEEPING\nBAGS", "adminmap.cmd show_player_sleepingbags {steamid}"),
                        new Button(string.Empty, "SHOW\nSTASHES", "adminmap.cmd show_player_stashes {steamid}")
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                PrintWarning("Creating new configuration file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }
}

namespace Oxide.Plugins
{
    #region 0xF UI Library 2.3
    partial class AdminMap
    {
        public class CUI
        {
            public enum Font
            {
                RobotoCondensedBold,
                RobotoCondensedRegular,
                RobotoMonoRegular,
                DroidSansMono,
                PermanentMarker,
                PressStart2PRegular,
                NotoSansArabicBold
            }

            private static readonly Dictionary<Font, string> FontToString = new Dictionary<Font, string>
            {
                { Font.RobotoCondensedBold, "RobotoCondensed-Bold.ttf" },
                { Font.RobotoCondensedRegular, "RobotoCondensed-Regular.ttf" },
                { Font.RobotoMonoRegular, "RobotoMono-Regular.ttf" },
                { Font.DroidSansMono, "DroidSansMono.ttf" },
                { Font.PermanentMarker, "PermanentMarker.ttf" },
                { Font.PressStart2PRegular, "PressStart2P-Regular.ttf" },
                { Font.NotoSansArabicBold, "nonenglish/notosansarabic-bold.ttf" }
            };

            public enum InputType
            {
                None,
                Default,
                HudMenuInput
            }

            private static readonly Dictionary<TextAnchor, string> TextAnchorToString = new Dictionary<TextAnchor, string>
            {
                { TextAnchor.UpperLeft, TextAnchor.UpperLeft.ToString() },
                { TextAnchor.UpperCenter, TextAnchor.UpperCenter.ToString() },
                { TextAnchor.UpperRight, TextAnchor.UpperRight.ToString() },
                { TextAnchor.MiddleLeft, TextAnchor.MiddleLeft.ToString() },
                { TextAnchor.MiddleCenter, TextAnchor.MiddleCenter.ToString() },
                { TextAnchor.MiddleRight, TextAnchor.MiddleRight.ToString() },
                { TextAnchor.LowerLeft, TextAnchor.LowerLeft.ToString() },
                { TextAnchor.LowerCenter, TextAnchor.LowerCenter.ToString() },
                { TextAnchor.LowerRight, TextAnchor.LowerRight.ToString() }
            };

            private static readonly Dictionary<VerticalWrapMode, string> VWMToString = new Dictionary<VerticalWrapMode, string>
            {
                { VerticalWrapMode.Truncate, VerticalWrapMode.Truncate.ToString() },
                { VerticalWrapMode.Overflow, VerticalWrapMode.Overflow.ToString() },
            };

            private static readonly Dictionary<Image.Type, string> ImageTypeToString = new Dictionary<Image.Type, string>
            {
                { Image.Type.Simple, Image.Type.Simple.ToString() },
                { Image.Type.Sliced, Image.Type.Sliced.ToString() },
                { Image.Type.Tiled, Image.Type.Tiled.ToString() },
                { Image.Type.Filled, Image.Type.Filled.ToString() },
            };

            private static readonly Dictionary<InputField.LineType, string> LineTypeToString = new Dictionary<InputField.LineType, string>
            {
                { InputField.LineType.MultiLineNewline, InputField.LineType.MultiLineNewline.ToString() },
                { InputField.LineType.MultiLineSubmit, InputField.LineType.MultiLineSubmit.ToString() },
                { InputField.LineType.SingleLine, InputField.LineType.SingleLine.ToString() },
            };

            private static readonly Dictionary<ScrollRect.MovementType, string> MovementTypeToString = new Dictionary<ScrollRect.MovementType, string>
            {
                { ScrollRect.MovementType.Unrestricted, ScrollRect.MovementType.Unrestricted.ToString() },
                { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Elastic.ToString() },
                { ScrollRect.MovementType.Clamped, ScrollRect.MovementType.Clamped.ToString() },
            };

            public static class Defaults
            {
                public const string VectorZero = "0 0";
                public const string VectorOne = "1 1";
                public const string Color = "1 1 1 1";
                public const string OutlineColor = "0 0 0 1";
                public const string Sprite = "assets/content/ui/ui.background.tile.psd";
                public const string Material = "assets/content/ui/namefontmaterial.mat";
                public const string IconMaterial = "assets/icons/iconmaterial.mat";
                public const Image.Type ImageType = Image.Type.Simple;
                public const CUI.Font Font = CUI.Font.RobotoCondensedRegular;
                public const int FontSize = 14;
                public const TextAnchor Align = TextAnchor.UpperLeft;
                public const VerticalWrapMode VerticalOverflow = VerticalWrapMode.Overflow;
                public const InputField.LineType LineType = InputField.LineType.SingleLine;
            }

            public static Color GetColor(string colorStr)
            {
                return ColorEx.Parse(colorStr);
            }

            public static string GetColorString(Color color)
            {
                return string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);
            }

            public static void AddUI(Connection connection, string json)
            {
                CommunityEntity.ServerInstance.ClientRPCEx<string>(new SendInfo
                {
                    connection = connection
                }, null, "AddUI", json);
            }

            private static void SerializeType(ICuiComponent component, JsonWriter jsonWriter)
            {
                jsonWriter.WritePropertyName("type");
                jsonWriter.WriteValue(component.Type);
            }

            private static void SerializeField(string key, object value, object defaultValue, JsonWriter jsonWriter)
            {
                if (value != null && !value.Equals(defaultValue))
                {
                    if (value is string && defaultValue != null && string.IsNullOrEmpty(value as string))
                        return;

                    jsonWriter.WritePropertyName(key);

                    if (value is ICuiComponent)
                        SerializeComponent(value as ICuiComponent, jsonWriter);
                    else
                        jsonWriter.WriteValue(value ?? defaultValue);
                }
            }


            private static void SerializeField(string key, CuiScrollbar scrollbar, JsonWriter jsonWriter)
            {
                const string defaultHandleSprite = "assets/content/ui/ui.rounded.tga";
                const string defaultHandleColor = "0.15 0.15 0.15 1";
                const string defaultHighlightColor = "0.17 0.17 0.17 1";
                const string defaultPressedColor = "0.2 0.2 0.2 1";
                const string defaultTrackSprite = "assets/content/ui/ui.background.tile.psd";
                const string defaultTrackColor = "0.09 0.09 0.09 1";

                if (scrollbar == null)
                    return;

                jsonWriter.WritePropertyName(key);
                jsonWriter.WriteStartObject();
                SerializeField("invert", scrollbar.Invert, false, jsonWriter);
                SerializeField("autoHide", scrollbar.AutoHide, false, jsonWriter);
                SerializeField("handleSprite", scrollbar.HandleSprite, defaultHandleSprite, jsonWriter);
                SerializeField("size", scrollbar.Size, 20f, jsonWriter);
                SerializeField("handleColor", scrollbar.HandleColor, defaultHandleColor, jsonWriter);
                SerializeField("highlightColor", scrollbar.HighlightColor, defaultHighlightColor, jsonWriter);
                SerializeField("pressedColor", scrollbar.PressedColor, defaultPressedColor, jsonWriter);
                SerializeField("trackSprite", scrollbar.TrackSprite, defaultTrackSprite, jsonWriter);
                SerializeField("trackColor", scrollbar.TrackColor, defaultTrackColor, jsonWriter);
                jsonWriter.WriteEndObject();
            }

            private static void SerializeComponent(ICuiComponent IComponent, JsonWriter jsonWriter)
            {
                const string vector2zero = "0 0";
                const string vector2one = "1 1";
                const string colorWhite = "1 1 1 1";
                const string backgroundTile = "assets/content/ui/ui.background.tile.psd";
                const string iconMaterial = "assets/icons/iconmaterial.mat";
                const string fontBold = "RobotoCondensed-Bold.ttf";
                const string defaultOutlineDistance = "1.0 -1.0";

                void SerializeType() => CUI.SerializeType(IComponent, jsonWriter);
                void SerializeField(string key, object value, object defaultValue) => CUI.SerializeField(key, value, defaultValue, jsonWriter);
                void SerializeScrollbar(string key, CuiScrollbar value) => CUI.SerializeField(key, value, jsonWriter);

                switch (IComponent.Type)
                {
                    case "RectTransform":
                        {
                            CuiRectTransformComponent component = IComponent as CuiRectTransformComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("anchormin", component.AnchorMin, vector2zero);
                            SerializeField("anchormax", component.AnchorMax, vector2one);
                            SerializeField("offsetmin", component.OffsetMin, vector2zero);
                            SerializeField("offsetmax", component.OffsetMax, vector2zero);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Image":
                        {
                            CuiImageComponent component = IComponent as CuiImageComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                            SerializeField("png", component.Png, null);
                            SerializeField("itemid", component.ItemId, 0);
                            SerializeField("skinid", component.SkinId, 0UL);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.RawImage":
                        {
                            CuiRawImageComponent component = IComponent as CuiRawImageComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("url", component.Url, null);
                            SerializeField("png", component.Png, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Text":
                        {
                            CuiTextComponent component = IComponent as CuiTextComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("text", component.Text, null);
                            SerializeField("font", component.Font, fontBold);
                            SerializeField("fontSize", component.FontSize, 14);
                            SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("verticalOverflow", VWMToString[component.VerticalOverflow], VWMToString[VerticalWrapMode.Truncate]);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Button":
                        {
                            CuiButtonComponent component = IComponent as CuiButtonComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("sprite", component.Sprite, backgroundTile);
                            SerializeField("material", component.Material, iconMaterial);
                            SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                            SerializeField("command", component.Command, null);
                            SerializeField("close", component.Close, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.InputField":
                        {
                            CuiInputFieldComponent component = IComponent as CuiInputFieldComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("text", component.Text, null);
                            SerializeField("font", component.Font, fontBold);
                            SerializeField("fontSize", component.FontSize, 14);
                            SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("command", component.Command, null);
                            SerializeField("characterLimit", component.CharsLimit, 0);
                            SerializeField("lineType", LineTypeToString[component.LineType], LineTypeToString[InputField.LineType.SingleLine]);
                            SerializeField("readOnly", component.ReadOnly, false);
                            SerializeField("password", component.IsPassword, false);
                            SerializeField("needsKeyboard", component.NeedsKeyboard, false);
                            SerializeField("hudMenuInput", component.HudMenuInput, false);
                            SerializeField("autofocus", component.Autofocus, false);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.ScrollView":
                        {
                            CuiScrollViewComponent component = IComponent as CuiScrollViewComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("contentTransform", component.ContentTransform, null);
                            SerializeField("horizontal", component.Horizontal, false);
                            SerializeField("vertical", component.Vertical, false);
                            SerializeField("movementType", MovementTypeToString[component.MovementType], MovementTypeToString[ScrollRect.MovementType.Clamped]);
                            SerializeField("elasticity", component.Elasticity, 0.1f);
                            SerializeField("inertia", component.Inertia, false);
                            SerializeField("decelerationRate", component.DecelerationRate, 0.135f);
                            SerializeField("scrollSensitivity", component.ScrollSensitivity, 1f);
                            SerializeScrollbar("horizontalScrollbar", component.HorizontalScrollbar);
                            SerializeScrollbar("verticalScrollbar", component.VerticalScrollbar);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "UnityEngine.UI.Outline":
                        {
                            CuiOutlineComponent component = IComponent as CuiOutlineComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("color", component.Color, colorWhite);
                            SerializeField("distance", component.Distance, defaultOutlineDistance);
                            SerializeField("useGraphicAlpha", component.UseGraphicAlpha, false);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "Countdown":
                        {
                            CuiCountdownComponent component = IComponent as CuiCountdownComponent;
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            SerializeField("endTime", component.EndTime, 0);
                            SerializeField("startTime", component.StartTime, 0);
                            SerializeField("step", component.Step, 1);
                            SerializeField("command", component.Command, null);
                            SerializeField("fadeIn", component.FadeIn, 0f);
                            jsonWriter.WriteEndObject();
                            break;
                        }
                    case "NeedsKeyboard":
                    case "NeedsCursor":
                        {
                            jsonWriter.WriteStartObject();
                            SerializeType();
                            jsonWriter.WriteEndObject();
                            break;
                        }
                }
            }


            [JsonObject(MemberSerialization.OptIn)]
            public class Element : CuiElement
            {
                public new string Name { get; set; } = null;

                public Element ParentElement { get; set; }
                public virtual List<Element> Container => ParentElement?.Container;
                public ComponentList Components { get; set; } = new ComponentList();

                [JsonProperty("name")]
                public string JsonName
                {
                    get
                    {
                        if (Name == null)
                        {
                            string result = this.GetHashCode().ToString();
                            if (ParentElement != null)
                                result.Insert(0, ParentElement.JsonName);
                            return result.GetHashCode().ToString();
                        }
                        return Name;
                    }
                }


                public Element() { }
                public Element(Element parent)
                {
                    AssignParent(parent);
                }

                public CUI.Element AssignParent(Element parent)
                {
                    if (parent == null)
                        return this;

                    ParentElement = parent;
                    Parent = ParentElement.JsonName;
                    return this;
                }

                public Element AddDestroy(string elementName)
                {
                    this.DestroyUi = elementName;
                    return this;
                }

                public Element AddDestroySelfAttribute()
                {
                    return AddDestroy(this.Name);
                }

                public virtual void WriteJson(JsonWriter jsonWriter)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("name");
                    jsonWriter.WriteValue(this.JsonName);
                    if (!string.IsNullOrEmpty(Parent))
                    {
                        jsonWriter.WritePropertyName("parent");
                        jsonWriter.WriteValue(this.Parent);
                    }
                    if (!string.IsNullOrEmpty(this.DestroyUi))
                    {
                        jsonWriter.WritePropertyName("destroyUi");
                        jsonWriter.WriteValue(this.DestroyUi);
                    }
                    if (this.Update)
                    {
                        jsonWriter.WritePropertyName("update");
                        jsonWriter.WriteValue(this.Update);
                    }
                    if (this.FadeOut > 0f)
                    {
                        jsonWriter.WritePropertyName("fadeOut");
                        jsonWriter.WriteValue(this.FadeOut);
                    }
                    jsonWriter.WritePropertyName("components");
                    jsonWriter.WriteStartArray();
                    for (int i = 0; i < this.Components.Count; i++)
                    {
                        SerializeComponent(this.Components[i], jsonWriter);
                    }
                    jsonWriter.WriteEndArray();
                    jsonWriter.WriteEndObject();
                }

                public Element Add(Element element)
                {
                    if (element.ParentElement == null)
                        element.AssignParent(this);
                    Container.Add(element);
                    return element;
                }

                public Element AddEmpty(string name = null)
                {
                    return Add(new Element(this) { Name = name });
                }

                public Element AddUpdateElement(string name = null)
                {
                    Element element = AddEmpty(name);
                    element.Parent = null;
                    element.Update = true;
                    return element;
                }

                public Element AddText(
                    string text,
                    string color = Defaults.Color,
                    CUI.Font font = Defaults.Font,
                    int fontSize = Defaults.FontSize,
                    TextAnchor align = Defaults.Align,
                    VerticalWrapMode overflow = Defaults.VerticalOverflow,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddOutlinedText(
                   string text,
                   string color = Defaults.Color,
                   CUI.Font font = Defaults.Font,
                   int fontSize = Defaults.FontSize,
                   TextAnchor align = Defaults.Align,
                   VerticalWrapMode overflow = Defaults.VerticalOverflow,
                   string outlineColor = Defaults.OutlineColor,
                   int outlineWidth = 1,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    return Add(ElementContructor.CreateOutlinedText(text, color, font, fontSize, align, overflow, outlineColor, outlineWidth, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddInputfield(
                    string command = null,
                    string text = "",
                    string color = Defaults.Color,
                    CUI.Font font = Defaults.Font,
                    int fontSize = Defaults.FontSize,
                    TextAnchor align = Defaults.Align,
                    InputField.LineType lineType = Defaults.LineType,
                    CUI.InputType inputType = CUI.InputType.Default,
                    bool @readonly = false,
                    bool autoFocus = false,
                    bool isPassword = false,
                    int charsLimit = 0,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddPanel(
                    string color = Defaults.Color,
                    string sprite = Defaults.Sprite,
                    string material = Defaults.Material,
                    Image.Type imageType = Defaults.ImageType,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    bool cursorEnabled = false,
                    bool keyboardEnabled = false,
                    string name = null)
                {
                    return Add(ElementContructor.CreatePanel(color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, cursorEnabled, keyboardEnabled, name));
                }

                public Element AddButton(
                   string command = null,
                   string close = null,
                   string color = Defaults.Color,
                   string sprite = Defaults.Sprite,
                   string material = Defaults.Material,
                   Image.Type imageType = Defaults.ImageType,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    return Add(ElementContructor.CreateButton(command, close, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddImage(
                    string content,
                    string color = Defaults.Color,
                    string material = null,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateImage(content, color, material, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddHImage(
                    string content,
                    string color = Defaults.Color,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return AddImage(content, color, Defaults.IconMaterial, anchorMin, anchorMax, offsetMin, offsetMax, name);
                }

                public Element AddIcon(
                    int itemId,
                    ulong skin = 0,
                    string color = Defaults.Color,
                    string sprite = Defaults.Sprite,
                    string material = Defaults.IconMaterial,
                    Image.Type imageType = Defaults.ImageType,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateIcon(itemId, skin, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddContainer(
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    return Add(ElementContructor.CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public CUI.Element WithRect(
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero)
                {
                    if (this.Components.Count > 0)
                        this.Components.RemoveAll(c => c is CuiRectTransformComponent);
                    this.Components.Add(new CuiRectTransformComponent()
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    });
                    return this;
                }

                public CUI.Element WithFade(
                    float @in = 0f,
                    float @out = 0f)
                {
                    this.FadeOut = @out;
                    foreach (ICuiComponent component in this.Components)
                    {
                        CuiImageComponent imageComponent = component as CuiImageComponent;
                        if (imageComponent != null)
                        {
                            imageComponent.FadeIn = @in;
                            continue;
                        }
                        CuiButtonComponent buttonComponent = component as CuiButtonComponent;
                        if (buttonComponent != null)
                        {
                            buttonComponent.FadeIn = @in;
                            continue;
                        }
                        CuiTextComponent textComponent = component as CuiTextComponent;
                        if (textComponent != null)
                        {
                            textComponent.FadeIn = @in;
                            continue;
                        }
                    }
                    return this;
                }

                public void AddComponents(params ICuiComponent[] components)
                {
                    this.Components.AddRange(components);
                }

                public CUI.Element WithComponents(params ICuiComponent[] components)
                {
                    AddComponents(components);
                    return this;
                }

                public CUI.Element CreateChild(string name = null, params ICuiComponent[] components)
                {
                    return CUI.Element.Create(name, components).AssignParent(this);
                }

                public static CUI.Element Create(string name = null, params ICuiComponent[] components)
                {
                    return new CUI.Element()
                    {
                        Name = name
                    }.WithComponents(components);
                }

                public class ComponentList : List<ICuiComponent>
                {
                    public ComponentList AddImage(
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        int itemId = 0,
                        ulong skinId = 0UL)
                    {
                        Add(new CuiImageComponent
                        {
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                            ImageType = imageType,
                            ItemId = itemId,
                            SkinId = skinId,
                        });
                        return this;
                    }

                    public ComponentList AddRawImage(
                        string content,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.IconMaterial)
                    {
                        CuiRawImageComponent rawImageComponent = new CuiRawImageComponent
                        {
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                        };
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("://"))
                                rawImageComponent.Url = content;
                            else if (content.IsNumeric())
                                rawImageComponent.Png = content;
                        }
                        Add(rawImageComponent);
                        return this;
                    }

                    public ComponentList AddButton(
                        string command = null,
                        string close = null,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType)
                    {
                        Add(new CuiButtonComponent
                        {
                            Command = command,
                            Close = close,
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                            ImageType = imageType,
                        });
                        return this;
                    }

                    public ComponentList AddText(
                        string text,
                        string color = Defaults.Color,
                        CUI.Font font = Defaults.Font,
                        int fontSize = Defaults.FontSize,
                        TextAnchor align = Defaults.Align,
                        VerticalWrapMode overflow = Defaults.VerticalOverflow)
                    {
                        Add(new CuiTextComponent
                        {
                            Text = text,
                            Color = color,
                            Font = FontToString[font],
                            FontSize = fontSize,
                            Align = align,
                            VerticalOverflow = overflow
                        });
                        return this;
                    }

                    public ComponentList AddInputfield(
                        string command = null,
                        string text = "",
                        string color = Defaults.Color,
                        CUI.Font font = Defaults.Font,
                        int fontSize = Defaults.FontSize,
                        TextAnchor align = Defaults.Align,
                        InputField.LineType lineType = Defaults.LineType,
                        CUI.InputType inputType = CUI.InputType.Default,
                        bool @readonly = false,
                        bool autoFocus = false,
                        bool isPassword = false,
                        int charsLimit = 0)
                    {
                        Add(new CuiInputFieldComponent
                        {
                            Command = command,
                            Text = text,
                            Color = color,
                            Font = FontToString[font],
                            FontSize = fontSize,
                            Align = align,
                            NeedsKeyboard = inputType == InputType.Default,
                            HudMenuInput = inputType == InputType.HudMenuInput,
                            Autofocus = autoFocus,
                            ReadOnly = @readonly,
                            CharsLimit = charsLimit,
                            IsPassword = isPassword,
                            LineType = lineType
                        });
                        return this;
                    }

                    public ComponentList AddScrollView(
                        bool horizontal = false,
                        CuiScrollbar horizonalScrollbar = null,
                        bool vertical = false,
                        CuiScrollbar verticalScrollbar = null,
                        bool inertia = false,
                        ScrollRect.MovementType movementType = ScrollRect.MovementType.Clamped,
                        float decelerationRate = 0.135f,
                        float elasticity = 0.1f,
                        float scrollSensitivity = 1f,
                        string anchorMin = "0 0",
                        string anchorMax = "1 1",
                        string offsetMin = "0 0",
                        string offsetMax = "0 0")
                    {
                        Add(new CuiScrollViewComponent()
                        {
                            ContentTransform =
                                         new CuiRectTransformComponent()
                                         {
                                             AnchorMin = anchorMin,
                                             AnchorMax = anchorMax,
                                             OffsetMin = offsetMin,
                                             OffsetMax = offsetMax
                                         },
                            Horizontal = horizontal,
                            HorizontalScrollbar = horizonalScrollbar,
                            Vertical = vertical,
                            VerticalScrollbar = verticalScrollbar,
                            Inertia = inertia,
                            DecelerationRate = decelerationRate,
                            Elasticity = elasticity,
                            ScrollSensitivity = scrollSensitivity,
                            MovementType = movementType,
                        });
                        return this;
                    }

                    public ComponentList AddOutline(
                        string color = Defaults.OutlineColor,
                        int width = 1)
                    {
                        Add(new CuiOutlineComponent
                        {
                            Color = color,
                            Distance = string.Format("{0} -{0}", width)
                        });
                        return this;
                    }
                    public ComponentList AddNeedsKeyboard()
                    {
                        Add(new CuiNeedsKeyboardComponent());
                        return this;
                    }

                    public ComponentList AddNeedsCursor()
                    {
                        Add(new CuiNeedsCursorComponent());
                        return this;
                    }

                    public ComponentList AddCountdown(string command = null, int endTime = 0, int startTime = 0, int step = 1)
                    {
                        Add(new CuiCountdownComponent
                        {
                            Command = command,
                            EndTime = endTime,
                            StartTime = startTime,
                            Step = step
                        });
                        return this;
                    }
                }
            }

            public static class ElementContructor
            {
                public static CUI.Element CreateText(
                 string text,
                 string color = Defaults.Color,
                 CUI.Font font = Defaults.Font,
                 int fontSize = Defaults.FontSize,
                 TextAnchor align = Defaults.Align,
                 VerticalWrapMode overflow = Defaults.VerticalOverflow,
                 string anchorMin = Defaults.VectorZero,
                 string anchorMax = Defaults.VectorOne,
                 string offsetMin = Defaults.VectorZero,
                 string offsetMax = Defaults.VectorZero,
                 string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddText(text, color, font, fontSize, align, overflow);
                    return element;
                }

                public static CUI.Element CreateOutlinedText(
                   string text,
                   string color = Defaults.Color,
                   CUI.Font font = Defaults.Font,
                   int fontSize = Defaults.FontSize,
                   TextAnchor align = Defaults.Align,
                   VerticalWrapMode overflow = Defaults.VerticalOverflow,
                   string outlineColor = Defaults.OutlineColor,
                   int outlineWidth = 1,
                   string anchorMin = Defaults.VectorZero,
                   string anchorMax = Defaults.VectorOne,
                   string offsetMin = Defaults.VectorZero,
                   string offsetMax = Defaults.VectorZero,
                   string name = null)
                {
                    CUI.Element element = CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddOutline(outlineColor, outlineWidth);
                    return element;
                }

                public static CUI.Element CreateInputfield(
                      string command = null,
                      string text = "",
                      string color = Defaults.Color,
                      CUI.Font font = Defaults.Font,
                      int fontSize = Defaults.FontSize,
                      TextAnchor align = Defaults.Align,
                      InputField.LineType lineType = Defaults.LineType,
                      CUI.InputType inputType = CUI.InputType.Default,
                      bool @readonly = false,
                      bool autoFocus = false,
                      bool isPassword = false,
                      int charsLimit = 0,
                      string anchorMin = Defaults.VectorZero,
                      string anchorMax = Defaults.VectorOne,
                      string offsetMin = Defaults.VectorZero,
                      string offsetMax = Defaults.VectorZero,
                      string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit);
                    return element;
                }

                public static CUI.Element CreateButton(
                        string command = null,
                        string close = null,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddButton(command, close, color, sprite, material, imageType);
                    return element;
                }

                public static CUI.Element CreatePanel(
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.Material,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        bool cursorEnabled = false,
                        bool keyboardEnabled = false,
                        string name = null)
                {

                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType);
                    if (cursorEnabled)
                        element.Components.AddNeedsCursor();
                    if (keyboardEnabled)
                        element.Components.AddNeedsKeyboard();
                    return element;
                }

                public static CUI.Element CreateImage(
                    string content,
                    string color = Defaults.Color,
                    string material = null,
                    string anchorMin = Defaults.VectorZero,
                    string anchorMax = Defaults.VectorOne,
                    string offsetMin = Defaults.VectorZero,
                    string offsetMax = Defaults.VectorZero,
                    string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddRawImage(content, color, material: material);
                    return element;
                }


                public static CUI.Element CreateIcon(
                        int itemId,
                        ulong skin = 0,
                        string color = Defaults.Color,
                        string sprite = Defaults.Sprite,
                        string material = Defaults.IconMaterial,
                        Image.Type imageType = Defaults.ImageType,
                        string anchorMin = Defaults.VectorZero,
                        string anchorMax = Defaults.VectorOne,
                        string offsetMin = Defaults.VectorZero,
                        string offsetMax = Defaults.VectorZero,
                        string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType, itemId, skin);
                    return element;
                }

                public static Element CreateContainer(
                       string anchorMin = Defaults.VectorZero,
                       string anchorMax = Defaults.VectorOne,
                       string offsetMin = Defaults.VectorZero,
                       string offsetMax = Defaults.VectorZero,
                       string name = null)
                {
                    return Element.Create(name).WithRect(anchorMin, anchorMax, offsetMin, offsetMax);
                }
            }


            public class Root : Element
            {
                public bool wasRendered = false;
                private static StringBuilder stringBuilder = new StringBuilder();

                public Root()
                {
                    Name = string.Empty;
                }

                public Root(string rootObjectName = "Overlay")
                {
                    Name = rootObjectName;
                }

                public override List<Element> Container { get; } = new List<Element>();

                public string ToJson(List<Element> elements)
                {
                    stringBuilder.Clear();
                    try
                    {
                        using (StringWriter stringWriter = new StringWriter(stringBuilder))
                        {
                            using (JsonWriter jsonWriter = new JsonTextWriter(stringWriter))
                            {
                                jsonWriter.WriteStartArray();
                                foreach (Element element in elements)
                                    element.WriteJson(jsonWriter);
                                jsonWriter.WriteEndArray();
                            }
                        }
                        return stringBuilder.ToString().Replace("\\n", "\n");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError(ex.Message + "\n" + ex.StackTrace);
                        return string.Empty;
                    }
                }

                public string ToJson()
                {
                    return ToJson(Container);
                }

                public void Render(Connection connection)
                {
                    if (connection == null || !connection.connected)
                        return;

                    wasRendered = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Render(BasePlayer player)
                {
                    Render(player.Connection);
                }

                public void Update(Connection connection)
                {
                    foreach (Element element in Container)
                        element.Update = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Update(BasePlayer player)
                {
                    Update(player.Connection);
                }

            }
        }
    }
    #endregion
}