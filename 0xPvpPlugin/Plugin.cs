using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Runtime.InteropServices;
using System;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using Dalamud.Data;
using Action = Lumina.Excel.GeneratedSheets.Action;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using System.Diagnostics;
using System.ComponentModel;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Statuses;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.STD;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Collections;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Interop;
using Lumina.Excel.GeneratedSheets2;
using Dalamud;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState.Conditions;
using OPP.Windows;
using System.Net;
using System.Reflection.Metadata;
using OPP.fr;


namespace OPP
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "0xPvpPlugin";
        private const string CommandName = "/0x";

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        [PluginService] public static ITargetManager TargetManaget { get; private set; } = null!;
        public Configuration Configuration { get; init; }
        [PluginService] public static IClientState clientState { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider gameInteropProvider { get; private set; }
        [PluginService] public static IChatGui chatGui { get; private set; } = null!;
        public WindowSystem WindowSystem = new("0xPvpPlugin");
        [PluginService] public static IObjectTable objectTable { get; private set; }
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ISigScanner Scanner { get; set; }
        //internal PluginAddressResolver Address{get; set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        internal static ActionManager ActionManager { get; private set; }
        [PluginService] internal static IPluginLog pluginLog { get; private set; }
        [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IGameGui gui { get; private set; }
        internal UIBuilder Ui;

        internal static Queue<(ulong id, DateTime time)> AttackedTargets { get; } = new(48);

        DateTime LastSelectTime;
        DateTime LastXDTZTime;

        uint xdtz = (uint)29515; //星遁天诛

        private bool drawConfigWindow;
        Action _action => DataManager.GetExcelSheet<Action>().GetRow(xdtz);
        uint ID => _action.RowId;
        //uint ID = 11;

        ushort bhbuff = (ushort)1301; //被保护
        ushort sybuff = (ushort)1317; //三印
        ushort dtbuff = (ushort)1240; //必杀剑·地天

        public static double totalTime = 0;
        public static double totalPlayer = 0;
        public static bool hasUseXDTZ = false;

        private IntPtr actionManager = IntPtr.Zero;
        private delegate uint GetIconDelegate(IntPtr actionManager, uint actionID);
        //private readonly Hook<GetIconDelegate> getIconHook;

        private Hook<GetIconDelegate> getIconHook { get; init; }

        private delegate byte ReturnDelegate(nint a1);

        private static Hook<ReturnDelegate>? returnHook;

        #region 更新地址
        private IntPtr GetAdjustedActionId;
        private IntPtr OnReturn;

        private IntPtr pattern_main;
        private IntPtr pattern_actor_move;
        private IntPtr actorKnockAdress;
        private delegate byte knockDelegate(IntPtr actor_ptr, uint angle, uint dis, uint knock_time, uint a5, uint a6);
        private static Hook<knockDelegate>? knockHook;


        private CanAttackDelegate CanAttack;
        private delegate int CanAttackDelegate(int arg, IntPtr objectAddress);
        #endregion

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            this.CommandManager.AddHandler("/0x", new CommandInfo(OnCommand)
            {
                HelpMessage = "/0x setting"
            });

            Ui = new UIBuilder(this, pluginInterface, Configuration);

            CanAttack = Marshal.GetDelegateForFunctionPointer<CanAttackDelegate>(Scanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3"));

            GetAdjustedActionId = Scanner.ScanText("E8 ?? ?? ?? ?? 8B F8 3B DF");
            OnReturn = Scanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B 3D ?? ?? ?? ?? 48 8B D9 48 8D 0D");

            getIconHook = gameInteropProvider.HookFromAddress<GetIconDelegate>(GetAdjustedActionId, GetIconDetour);
            getIconHook.Enable();

            returnHook = gameInteropProvider.HookFromAddress<ReturnDelegate>(OnReturn, ReturnDetour);
            returnHook?.Enable();


            //pattern_main = Scanner.ScanText("f3 0f ?? ?? ?? ?? ?? ?? eb ?? 48 8b ?? ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 85");
            //pattern_actor_move = Scanner.ScanText("40 53 48 83 EC ?? F3 0F 11 89 ?? ?? ?? ?? 48 8B D9 F3 0F 11 91 ?? ?? ?? ??");

            actorKnockAdress = Scanner.ScanText("48 8B C4 48 89 70 ?? 57 48 81 EC ?? ?? ?? ?? 0F 29 70 ?? 0F 28 C1");
            knockHook = gameInteropProvider.HookFromAddress<knockDelegate>(actorKnockAdress, knockDetour);
            //knockHook?.Enable();


            Framework.Update += this.OnFrameworkUpdate;
            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;
            this.PluginInterface.UiBuilder.OpenMainUi += OnOpenConfig;
        }

        public void Dispose()
        {
            //knockHook?.Dispose();

            getIconHook?.Dispose();
            returnHook?.Dispose();
            Framework.Update -= this.OnFrameworkUpdate;
            this.WindowSystem.RemoveAllWindows();
            this.CommandManager.RemoveHandler(CommandName);
        }

        private byte knockDetour(IntPtr actor_ptr, uint angle, uint dis, uint knock_time, uint a5, uint a6) {
            if (Configuration.antiKnockback)
            {
                return 0;
            }
            else 
            {
                return knockHook.Original(actor_ptr, angle, dis, knock_time, a5, a6);
            }
        }


        internal uint OriginalHook(uint actionID) => getIconHook.Original(actionManager, actionID);
        private unsafe uint GetIconDetour(IntPtr actionManager, uint actionID)
        {
            try
            {
                if (clientState.IsPvP && clientState.LocalPlayer != null)
                {
                    //忍者
                    if (clientState.LocalPlayer.ClassJob.Id == 30)
                    {

                        if (Configuration.TD && actionID == 29513)  //缩地不受三印影响
                        {
                            return actionID;
                        }

                        if (Configuration.MS && clientState.LocalPlayer != null && actionID == 29507 && HasEffect(sybuff, clientState.LocalPlayer))  //防止重复点三印点出命水
                        {
                            return 29657;
                        }

                        if (clientState.LocalPlayer.TargetObject != null)
                        {
                            PlayerCharacter actor = clientState.LocalPlayer.TargetObject as PlayerCharacter;
                            if (clientState.LocalPlayer.CurrentHp != 0 && actor.CurrentHp != 0 && actor.CurrentHp >= ((actor.MaxHp / 2) - 2) && actionID == 29515) //星遁天诛
                            {
                                return 29657;
                            }
                            else
                            {
                                return OriginalHook(actionID);
                            }
                        }
                        else if (clientState.LocalPlayer.CurrentHp != 0 && actionID == 29515)
                        {
                            return 29657;
                        }
                        else
                        {
                            return OriginalHook(actionID);
                        }
                    }
                    //武士
                    else if (clientState.LocalPlayer.ClassJob.Id == 34)
                    {
                        if (Configuration.SLBX && actionID == 29524)  //冰雪
                        {
                            return 29523;
                        }
                        else if (totalPlayer <= 0 && actionID == 29537)
                        { //斩铁剑
                            return 29657;
                        }
                        else
                        {
                            return OriginalHook(actionID);
                        }
                    }
                    //其他职业
                    else
                    {
                        return OriginalHook(actionID);
                    }
                }
                else
                {
                    return OriginalHook(actionID);
                }
            }
            catch (Exception e)
            {
                if (actionID == 29515)
                {
                    return 29657;
                }
                else
                {
                    return OriginalHook(actionID);
                }
            }
        }

        private static byte ReturnDetour(nint a1)
        {
            //if (clientState.IsPvP)
                return returnHook.Original(a1);
            //return 1;
        }

        public bool HasEffect(ushort effectID, GameObject? obj) => GetStatus(effectID, obj, null) is not null;

        internal Dalamud.Game.ClientState.Statuses.Status? GetStatus(uint statusID, GameObject? obj, uint? sourceID)
        {
            if (obj is null || obj is not BattleChara chara) return null;

            //Dictionary<(uint StatusID, uint? TargetID, uint? SourceID), Dalamud.Game.ClientState.Statuses.Status?> statusCache = new();
            uint InvalidObjectID = 0xE000_0000;
            //var key = (statusID, obj?.ObjectId, sourceID);

            BattleChara charaTemp = (BattleChara)obj;

            foreach (Dalamud.Game.ClientState.Statuses.Status? status in charaTemp.StatusList)
            {
                if (status.StatusId == statusID && (!sourceID.HasValue || status.SourceId == 0 || status.SourceId == InvalidObjectID || status.SourceId == sourceID))
                {
                    return status;
                }
            }
            return null;
        }

        internal Dalamud.Game.ClientState.Statuses.Status? GetStatusFromMe(uint statusID, GameObject? obj)
        {
            if (obj is null || obj is not BattleChara chara) return null;
            BattleChara charaTemp = (BattleChara)obj;
            //chatGui.Print(charaTemp.StatusList.Count().ToString());
            foreach (Dalamud.Game.ClientState.Statuses.Status status in charaTemp.StatusList)
            {
                if (status.StatusId == statusID && (status.SourceId == clientState.LocalPlayer.ObjectId || status.SourceObject?.OwnerId == clientState.LocalPlayer.ObjectId))
                {
                    return status;
                }
            }
            return null;
        }

        private void OnCommand(string command, string args)
        {
            if (command == "/0x" && (args == "settings" || args == ""))
            {
                drawConfigWindow = !drawConfigWindow;
            }
            if (command == "/0x" && (args == "TargetOnce")) //DK跳斩
            {
                SelectDenceEnemyOnce();
            }            
            if (command == "/0x" && (args == "Select")) //选人
            {
                SelectDenceEnemyOnceForAll();
            }
        }
        private void OnFrameworkUpdate(IFramework framework)
        {
            try
            {
                if (clientState.LocalPlayer != null && clientState.IsPvP && clientState.LocalPlayer.CurrentHp != 0)
                {
                    if (clientState.LocalPlayer.ClassJob.Id == 30 && Configuration.AutoSelect) //忍者
                    {
                        if (Configuration.Ninoption)
                        {
                            if (isNINLBReady())
                            {
                                this.ReFreshEnermyActors_And_AutoSelect();
                            }
                        }
                        else
                        {
                            this.ReFreshEnermyActors_And_AutoSelect();
                        }
                    }
                    else if (clientState.LocalPlayer.ClassJob.Id == 34 && Configuration.AutoSelectSAM && isSAMLBReady()) //武士
                    {
                        this.ReFreshDebuffedActors_And_AutoSelect();
                    }
                    else if (clientState.LocalPlayer.ClassJob.Id == 24 && Configuration.AutoBianZhu) //白魔
                    {
                        this.ReFreshNearestSAM_And_AutoSelect();
                    }
                    else //选人堆
                    {
                        this.ReFreshDenseActors_And_AutoSelect();
                    }
                }
            }
            catch (Exception ex) { }
        }





        private void ReFreshEnermyActors_And_AutoSelect()
        {
            if (clientState.LocalPlayer == null)
            {
                return;
            }
            DateTime beforDT = System.DateTime.Now;
            lock (Configuration.EnermyActors)
            {
                Configuration.EnermyActors.Clear();
                if (objectTable == null)
                {
                    return;
                }
                foreach (var obj in objectTable)
                {
                    try
                    {
                        //if (obj != null && (obj.ObjectId != clientState.LocalPlayer.ObjectId) & obj.Address.ToInt64() != 0 )
                        if (obj != null && (obj.ObjectId != clientState.LocalPlayer.ObjectId) & obj.Address.ToInt64() != 0 && CanAttack(142, obj.Address) == 1)
                        {
                            PlayerCharacter rcTemp = obj as PlayerCharacter;
                            //19 骑士   32 DK
                            if (rcTemp != null
                                //&& rcTemp.ClassJob.Id != 19 && rcTemp.ClassJob.Id != 32
                                && rcTemp.StatusList.Where(x => x.StatusId == 1301 || x.StatusId == 1302 || x.StatusId == 3039).Count() == 0
                                && CameraHelper.CanSee(clientState.LocalPlayer.Position, rcTemp.Position))
                            {
                                Configuration.EnermyActors.Add(rcTemp);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            if (Configuration.AutoSelect)
            {
                DateTime now = DateTime.Now;
                if (LastSelectTime == null || (now - LastSelectTime).TotalMilliseconds > Configuration.SelectInterval)
                {
                    SelectEnermyOnce();
                    DateTime afterDT = DateTime.Now;
                    TimeSpan ts = afterDT.Subtract(beforDT);
                    if (totalTime < ts.TotalMilliseconds)
                    {
                        totalTime = ts.TotalMilliseconds;
                    }
                    if (Configuration.AutoSelectInterval)
                    {
                        if (Configuration.SelectInterval < (int)Math.Ceiling(totalTime)) 
                        {
                            Configuration.SelectInterval = (int)Math.Ceiling(totalTime);
                            if (Configuration.SelectInterval > 100)
                            {
                                Configuration.SelectInterval = 100;
                            }
                            Configuration.Save();
                        }
                    }
                    LastSelectTime = now;
                }
            }
        }

        private void SelectEnermyOnce()
        {
            if (clientState.LocalPlayer == null || Configuration.EnermyActors == null)
            {
                return;
            }
            PlayerCharacter selectActor = null;
            if (clientState.LocalPlayer.ClassJob.Id == 30)
            {
                if (clientState.LocalPlayer.TargetObject != null && clientState.LocalPlayer.TargetObject is PlayerCharacter)
                {
                    PlayerCharacter temp = clientState.LocalPlayer.TargetObject as PlayerCharacter;
                    if (temp.CurrentHp != 0)
                    {
                        Configuration.EnermyActors.Insert(0, temp);
                    }
                }

                foreach (PlayerCharacter actor in Configuration.EnermyActors)
                {
                    try
                    {
                        //var distance2D = Math.Sqrt(Math.Pow(actor.YalmDistanceX, 2) + Math.Pow(actor.YalmDistanceZ, 2)) - 6;
                        var distance2D = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, actor.Position);
                        if (distance2D <= 20 && actor.CurrentHp != 0 && actor.CurrentHp < (actor.MaxHp / 2))
                        {
                            selectActor = actor;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            if (selectActor != null)
            {
                TargetManaget.Target = selectActor;
                PlayerCharacter tar = clientState.LocalPlayer.TargetObject as PlayerCharacter;
                if (TargetManaget.Target != null && Configuration.AutoXDTZ && tar != null && tar.StatusList.Where(x => x.StatusId == 1301 || x.StatusId == 1302 || x.StatusId == 3039).Count() == 0)
                {
                    XDTZ(selectActor.ObjectId);
                }
            }
        }





        private void ReFreshDebuffedActors_And_AutoSelect()
        {
            if (clientState.LocalPlayer == null)
            {
                return;
            }
            DateTime beforDT = System.DateTime.Now;
            lock (Configuration.BuffedActors)
            {
                Configuration.BuffedActors.Clear();
                if (objectTable == null)
                {
                    return;
                }
                foreach (var obj in objectTable)
                {
                    try
                    {
                        if (obj != null && (obj.ObjectId != clientState.LocalPlayer.ObjectId) & obj.Address.ToInt64() != 0 && CanAttack(142, obj.Address) == 1)
                        {
                            PlayerCharacter rcTemp = obj as PlayerCharacter;
                            if (rcTemp != null
                                && (rcTemp.ShieldPercentage == 0 || (rcTemp.ShieldPercentage > 0 && rcTemp.ShieldPercentage * 0.01 * rcTemp.MaxHp + rcTemp.CurrentHp < rcTemp.MaxHp - 30000))
                                && CameraHelper.CanSee(clientState.LocalPlayer.Position, rcTemp.Position)
                                && rcTemp.StatusList.Where(x => x.StatusId == 1301 || x.StatusId == 1302 || x.StatusId == 3039).Count() == 0  //保护/神圣领域/DKLB
                                && rcTemp.StatusList.Where(x => x.StatusId == 3202 && x.SourceId == clientState.LocalPlayer.ObjectId).Count() >= 1)
                            {
                                Configuration.BuffedActors.Add(rcTemp);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            if (true)
            {
                DateTime now = DateTime.Now;
                if (LastSelectTime == null || (now - LastSelectTime).TotalMilliseconds > Configuration.SelectInterval)
                {
                    SelectBuffedEnermyOnce();
                    DateTime afterDT = System.DateTime.Now;
                    TimeSpan ts = afterDT.Subtract(beforDT);
                    if (totalTime < ts.TotalMilliseconds)
                    {
                        totalTime = ts.TotalMilliseconds;
                    }
                    if (Configuration.AutoSelectInterval)
                    {
                        if (Configuration.SelectInterval < (int)Math.Ceiling(totalTime))
                        {
                            Configuration.SelectInterval = (int)Math.Ceiling(totalTime);
                            if (Configuration.SelectInterval > 100)
                            {
                                Configuration.SelectInterval = 100;
                            }
                            Configuration.Save();
                        }
                    }
                    LastSelectTime = now;
                }
            }
        }

        private void SelectBuffedEnermyOnce()
        {
            if (clientState.LocalPlayer == null || Configuration.BuffedActors == null)
            {
                totalPlayer = 0;
                return;
            }
            PlayerCharacter selectActor = null;

            var maxNum = -1;
            PlayerCharacter maxNumActor = null;

            foreach (PlayerCharacter actor in Configuration.BuffedActors)
            {
                var num = 0;
                var targetDistance = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, actor.Position);
                if (targetDistance > 21.7)
                {
                    continue;
                }
                foreach (PlayerCharacter actor2 in Configuration.BuffedActors)
                {
                    var distance = Vector3.Distance(actor.Position, actor2.Position);
                    if (distance < 5)
                    {
                        num++;
                    }
                }
                if (maxNum < num)
                {
                    maxNum = num;
                    maxNumActor = actor;
                }
            }

            if (maxNumActor != null)
            {
                //TargetManaget.Target = maxNumActor;
                totalPlayer = maxNum;
                if (maxNum >= Configuration.AutoZhanNum)
                {
                    Zhan(maxNumActor.ObjectId);
                }
            }
            else
            {
                totalPlayer = 0;
            }
        }





        private void ReFreshDenseActors_And_AutoSelect()
        {
            if (clientState.LocalPlayer == null)
            {
                return;
            }
            lock (Configuration.DenseActors)
            {
                Configuration.DenseActors.Clear();
                if (objectTable == null)
                {
                    return;
                }
                foreach (var obj in objectTable)
                {
                    try
                    {
                        if (obj != null && (obj.ObjectId != clientState.LocalPlayer.ObjectId) & obj.Address.ToInt64() != 0 && CanAttack(142, obj.Address) == 1)
                        {
                            PlayerCharacter rcTemp = obj as PlayerCharacter;
                            if (rcTemp != null)
                            {
                                var distance = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, rcTemp.Position);
                                if (rcTemp.CurrentHp > 0 && distance <= 30)
                                {
                                    Configuration.DenseActors.Add(rcTemp);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
        }
        public void SelectDenceEnemyOnce()
        {
            if (!clientState.IsPvP) return;
            var maxNum = -1;
            PlayerCharacter maxNumActor = null;
            foreach (var actor in Configuration.DenseActors)
            {
                var num = 0;
                var targetDistance = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, actor.Position);
                if (targetDistance > 21.7)
                {
                    continue;
                }
                foreach (PlayerCharacter actor2 in Configuration.DenseActors)
                {
                    var distance = Vector3.Distance(actor.Position, actor2.Position);
                    if (distance < 11)
                    {
                        num++;
                    }
                }
                if (maxNum < num)
                {
                    maxNum = num;
                    maxNumActor = actor;
                }
            }
            if (maxNumActor != null)
            {
                TargetManaget.Target = maxNumActor;
                if (Configuration.AutoTiaoZhan)
                {
                    TiaoZhan(maxNumActor.ObjectId);
                }
            }
        }
        
        public void SelectDenceEnemyOnceForAll()
        {
            if (!clientState.IsPvP) return;
            var maxNum = -1;
            PlayerCharacter maxNumActor = null;
            foreach (var actor in Configuration.DenseActors)
            {
                var num = 0;
                var targetDistance = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, actor.Position);
                if (targetDistance > Configuration.SelectDistance + 1)
                {
                    continue;
                }
                foreach (PlayerCharacter actor2 in Configuration.DenseActors)
                {
                    var distance = Vector3.Distance(actor.Position, actor2.Position);
                    if (distance < Configuration.SelectSkillRange + 1)
                    {
                        num++;
                    }
                }
                if (maxNum < num)
                {
                    maxNum = num;
                    maxNumActor = actor;
                }
            }
            if (maxNumActor != null)
            {
                TargetManaget.Target = maxNumActor;
            }
        }
        
        
        
        
        private void ReFreshNearestSAM_And_AutoSelect()
        {
            if (clientState.LocalPlayer == null)
            {
                return;
            }
            DateTime beforDT = System.DateTime.Now;
            Configuration.NearestOBJ = null;
            if (objectTable == null)
            {
                return;
            }
            foreach (var obj in objectTable)
            {
                try
                {
                    if (obj != null && (obj.ObjectId != clientState.LocalPlayer.ObjectId) & obj.Address.ToInt64() != 0 && CanAttack(142, obj.Address) == 1)
                    {
                        PlayerCharacter rcTemp = obj as PlayerCharacter;
                        if (rcTemp != null)
                        {
                            var distance = Vector3.Distance(clientState.LocalPlayer?.Position ?? Vector3.Zero, rcTemp.Position);
                                
                            if (Configuration.AutoBianZhuSAM)
                            {
                                    
                                if (rcTemp.CurrentHp > 0 && rcTemp.ClassJob.Id == 34 && distance <= 11.7 && //10m内活着的武士
                                rcTemp.StatusList.Where(x => x.StatusId == 1240).Count() >= 1 && //有地天
                                rcTemp.StatusList.Where(x => x.StatusId == 3054).Count() == 0) //无罩子
                                {
                                    Configuration.NearestOBJ = rcTemp;
                                }
                            }
                            else if (Configuration.AutoBianZhuDRK) 
                            {
                                if (rcTemp.CurrentHp > 50000 && rcTemp.ClassJob.Id == 32 
                                    && distance <= 10 && rcTemp.StatusList.Where(x => x.StatusId == 3039).Count() == 0) //10m内50000血以上没开无敌的DK
                                {
                                    Configuration.NearestOBJ = rcTemp;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
            if (Configuration.NearestOBJ != null)
            {
                DateTime now = DateTime.Now;
                if (LastSelectTime == null || (now - LastSelectTime).TotalMilliseconds > Configuration.SelectInterval)
                {
                    SelectNearestOBJOnce();
                    DateTime afterDT = System.DateTime.Now;
                    TimeSpan ts = afterDT.Subtract(beforDT);
                    if (totalTime < ts.TotalMilliseconds)
                    {
                        totalTime = ts.TotalMilliseconds;
                    }
                    if (Configuration.AutoSelectInterval)
                    {
                        if (Configuration.SelectInterval < (int)Math.Ceiling(totalTime))
                        {
                            Configuration.SelectInterval = (int)Math.Ceiling(totalTime);
                            if (Configuration.SelectInterval > 100)
                            {
                                Configuration.SelectInterval = 100;
                            }
                            Configuration.Save();
                        }
                    }
                    LastSelectTime = now;
                }
            }
        }
        public void SelectNearestOBJOnce()
        {
            if (Configuration.NearestOBJ != null)
            {
                //TargetManaget.Target = Configuration.NearestOBJ;
                if (Configuration.AutoBianZhuSAM)
                {
                    BianZhu(Configuration.NearestOBJ.ObjectId);
                }
            }
        }





        unsafe private void Zhan(uint ObjectId)
        {
            ActionManager.Instance()->UseAction(ActionType.Action, 29537, ObjectId); //斩铁剑
        }

        unsafe private void XDTZ(uint ObjectId)
        {
            if (hasUseXDTZ)
            {
                DateTime now = DateTime.Now;
                if (LastXDTZTime == null || (now - LastXDTZTime).TotalMilliseconds > Configuration.AutoXDTZDelay + 2500)
                {
                    bool thisTime = ActionManager.Instance()->UseAction(ActionType.Action, 29515, ObjectId); //星遁天诛
                    if (thisTime)
                    {
                        LastXDTZTime = now;
                    }
                }
            }
            else
            {
                hasUseXDTZ = ActionManager.Instance()->UseAction(ActionType.Action, 29515, ObjectId); //星遁天诛
            }
        }
        unsafe static public void TiaoZhan(uint ObjectId)
        {
            ActionManager.Instance()->UseAction(ActionType.Action, 29092, ObjectId); //跳斩
        }        
        unsafe static public void BianZhu(uint ObjectId)
        {
            ActionManager.Instance()->UseAction(ActionType.Action, 29228, ObjectId); //变猪
        }

        unsafe public static bool isSAMLBReady()
        {
            var lb = LimitBreakController.Instance();
            var lbNow = lb->CurrentValue;
            var lbMax = lb->BarValue & 0xFFFF;
            if (lbNow != lbMax)
            {
                Plugin.totalPlayer = 0;
            }
            return lbNow == lbMax;
            //chatGui.Print(lbnow + "/" + lbMax);
        }

        unsafe public static bool isNINLBReady()
        {
            var lb = LimitBreakController.Instance();
            var lbNow = lb->CurrentValue;
            var lbMax = lb->BarValue & 0xFFFF;

            bool hasNext = false;
            foreach (var status in clientState.LocalPlayer.StatusList)
            {
                if (status.StatusId == 3192)
                {
                    hasNext = true;
                    break;
                }
            }
            return (lbNow == lbMax) || hasNext;
        }

        public float GetMapZoneCoordSize(ushort TerritoryType)
        {
            //EVERYTHING EXCEPT HEAVENSWARD HAS 41 COORDS, BUT FOR SOME REASON HW HAS 43, WHYYYYYY
            if (TerritoryType is >= 397 and <= 402) return 43.1f;
            return 41f;
        }
        public static float ConvertToMapCoordinate(float pos, float zoneMaxCoordSize)
        {
            return (float)Math.Floor(((zoneMaxCoordSize + 1.96) / 2 + (pos / 50)) * 100) / 100;
        }

        private float ConvertMapMarkerToMapCoordinate(float pos, float scale)
        {
            float num = scale / 100f;
            var rawPosition = ((float)(pos - 1024.0) / num * 1000f);
            return ConvertRawPositionToMapCoordinate(rawPosition, scale);
        }

        private float ConvertRawPositionToMapCoordinate(float pos, float scale)
        {
            float num = scale / 100f;
            return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
        }

        public static void SetSpeed(float speedBase)
        {
            SigScanner.TryScanText("f3 ?? ?? ?? ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 0f ?? ?? e8 ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? ?? ?? ?? ?? f3 ?? ?? ?? f3", out var address);
            address = address + 4 + Marshal.ReadInt32(address + 4) + 4;
            SafeMemory.Write(address + 20, speedBase);
            SetMoveControlData(speedBase);
        }

        private unsafe static void SetMoveControlData(float speed)
        {
            SafeMemory.Write(((delegate* unmanaged[Stdcall]<byte, nint>)SigScanner.ScanText("E8 ?? ?? ?? ?? 48 ?? ?? 74 ?? 83 ?? ?? 75 ?? 0F ?? ?? ?? 66"))(1) + 8, speed);
        }

        private void DrawUI()
        {
            drawConfigWindow = drawConfigWindow && Configuration.DrawConfigUI();
        }

        public void OnOpenConfig()
        {
            drawConfigWindow = !drawConfigWindow;
        }
    }
}
