using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState;
using ImGuiNET;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Dalamud;
using System.Runtime.InteropServices;
using System.Linq;

namespace OPP.Windows;
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool AutoSelect = true;
    public bool AutoSelectSAM = true;
    public bool AutoZhan = true;
    public bool needLog = false;
    public bool Ninoption = true;
    public bool AutoTiaoZhan = false;
    public bool AutoSelectInterval = true;
    public bool antiKnockback = false;
    public int AutoZhanNum = 2;
    public int SelectInterval = 5;
    public int AutoXDTZDelay = 500;

    public int SelectDistance = 20;
    public int SelectSkillRange = 5;

    public float Dspeed = 1.0f;
    internal static float speedOffset = 6f;

    public int upDistance = 0;
    public int downDistance = 0;

    public int downDistanceAll = 0;

    public bool SLBX = false;
    public bool MS = true;
    public bool TD = true;
    public bool AutoXDTZ = false;
    public bool AutoBianZhu = true;
    public bool AutoBianZhuSAM = true;
    public int AutoBianZhuSAMint = 1;
    public bool AutoBianZhuDRK = false;
    public int AutoBianZhuDRKint = 0;

    public bool Overlay2D_Enabled = true;
    public bool Overlay2D_ShowCenter = false;
    public bool Overlay2D_ShowAssist = true;

    [NonSerialized] public DalamudPluginInterface? pluginInterface;
    [NonSerialized] private Plugin plugin;
    [PluginService] public static IChatGui chatGui { get; private set; } = null!;
    public IObjectTable nearObjects;
    public List<PlayerCharacter> EnermyActors = new List<PlayerCharacter>();
    public List<PlayerCharacter> BuffedActors = new List<PlayerCharacter>();
    public List<PlayerCharacter> DenseActors = new List<PlayerCharacter>();
    public PlayerCharacter NearestOBJ = null;
    public PlayerCharacter LocalPlayer;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        if (Dspeed != 1.0f)
        {
            Plugin.SetSpeed(Dspeed * speedOffset);
        }
    }

    public unsafe bool DrawConfigUI()
    {
        var drawConfig = true;

        var scale = ImGui.GetIO().FontGlobalScale;

        var modified = false;

        //ImGui.SetNextWindowSize(new Vector2(300, 200));
        ImGui.Begin("OPP设置", ref drawConfig);

        ImGui.BeginTabBar("tabbar");
        if (ImGui.BeginTabItem("忍者"))
        {
            if (ImGui.Checkbox("仅在LB可用时启用", ref Ninoption))
            {
                Save();
            }
            if (ImGui.Checkbox("弃用土遁(pvp)", ref TD))
            {
                Save();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("弃用命水(pvp)", ref MS))
            {
                Save();
            }
            if (ImGui.Checkbox("自动释放星遁天诛（推荐手动）", ref AutoXDTZ))
            {
                Save();
            }
            ImGui.SetNextItemWidth(120);
            modified |= ImGui.SliderInt("连斩延时(ms)", ref AutoXDTZDelay, 500, 3000);

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("武士"))
        {
            if (ImGui.Checkbox("无限止步", ref SLBX))
            {
                Save();
            }
            if (ImGui.Checkbox("自动释放斩铁剑", ref AutoZhan))
            {
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("范围内人数阈值", ref AutoZhanNum))
            {
                if (AutoZhanNum < 1)
                {
                    AutoZhanNum = 1;
                }
                Save();
            }

            //ImGui.SetWindowFontScale(3.7f);
            ImGui.NewLine();
            ImGui.Text("范围内人数：" + Plugin.totalPlayer);
            //ImGui.SetWindowFontScale(1.0f);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("DK"))
        {
            ImGui.Text("选中人堆 /0x TargetOnce");
            if (ImGui.IsItemHovered())
            {
                // 显示工具的提示
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("写个宏吧");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            if (ImGui.Checkbox("自动跳斩", ref AutoTiaoZhan))
            {
                Save();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("白魔"))
        {
            if (ImGui.Checkbox("自动变猪开关", ref AutoBianZhu))
            {
                Save();
            }
            if (ImGui.RadioButton("地天武士", ref AutoBianZhuSAMint, 1))
            {
                AutoBianZhuDRKint = 0;

                AutoBianZhuSAM = true;
                AutoBianZhuDRK = false;
                Save();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("DK", ref AutoBianZhuDRKint, 2))
            {
                AutoBianZhuSAMint = 0;

                AutoBianZhuDRK = true;
                AutoBianZhuSAM = false;
                Save();
            }
            if (ImGui.IsItemHovered())
            {
                // 显示工具的提示
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("50000血以上的DK");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("通用"))
        {
            if (ImGui.Checkbox("自动设置扫描间隔（推荐）", ref AutoSelectInterval))
            {
                Save();
            }
            ImGui.BeginDisabled(AutoSelectInterval);
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("扫描间隔(ms)", ref SelectInterval))
            {
                Save();
            }
            ImGui.EndDisabled();

            ImGui.NewLine();

            ImGui.Text("选中人堆 /0x Select");
            if (ImGui.IsItemHovered())
            {
                // 显示工具的提示
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("全职业通用，跟DK的不一样");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("选人范围(m)", ref SelectDistance))
            {
                if (SelectDistance < 5)
                {
                    SelectDistance = 5;
                }
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("技能范围(m)", ref SelectSkillRange))
            {
                if (SelectSkillRange < 5) {
                    SelectSkillRange = 5;
                }
                Save();
            }

            ImGui.EndTabItem();
        }        
        
        if (ImGui.BeginTabItem("位移相关 (风险自负)"))
        {
            if (ImGui.Checkbox("无视位移（DK马桶/战士死斗）", ref antiKnockback))
            {
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("移动速度(倍)", ref Dspeed, 0.1f, 1f, "%.1f", ImGuiInputTextFlags.NoHorizontalScroll))
            {
                Plugin.SetSpeed(Dspeed * speedOffset);
                Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("重置"))
            {
                Dspeed = 1f;
                Plugin.SetSpeed(speedOffset);
                Save();
            }

            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##down", ref downDistance))
            {
                Save();
            }
            ImGui.SameLine();
            ImGui.BeginDisabled(IsBusy());
            if (ImGui.Button("下降(m)"))
            {
                if (Plugin.clientState.LocalPlayer != null)
                {
                    var pos = Plugin.clientState.LocalPlayer.Position;
                    var address = Plugin.clientState.LocalPlayer.Address;
                    SafeMemory.Write(address + 180, pos.Y - downDistance);

                    downDistanceAll += downDistance;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("回到地面"))
            {
                var pos = Plugin.clientState.LocalPlayer.Position;
                var address = Plugin.clientState.LocalPlayer.Address;
                SafeMemory.Write(address + 180, pos.Y + downDistanceAll);
                downDistanceAll = 0;
            }
            ImGui.EndDisabled();

            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##up", ref upDistance))
            {
                Save();
            }
            ImGui.SameLine();
            ImGui.BeginDisabled(IsBusy());
            if (ImGui.Button("上升(m)"))
            {
                if (Plugin.clientState.LocalPlayer != null)
                {
                    var pos = Plugin.clientState.LocalPlayer.Position;
                    var address = Plugin.clientState.LocalPlayer.Address;
                    SafeMemory.Write(address + 180, pos.Y + upDistance);

                    downDistanceAll -= upDistance;
                }
            }
            ImGui.EndDisabled();

            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("战场雷达"))
        {
            if (ImGui.Checkbox("开关", ref Overlay2D_Enabled))
            {
                Save();
            }
            if (ImGui.Checkbox("显示加载范围圈", ref Overlay2D_ShowAssist))
            {
                Save();
            }

            ImGui.EndTabItem();
        }
        ImGui.End();


        if (modified)
        {
            Save();
        }

        return drawConfig;
    }

    private static bool IsBusy()
    {
        return Plugin.Condition[ConditionFlag.Jumping] ||
               Plugin.Condition[ConditionFlag.Jumping61] ||
               Plugin.Condition[ConditionFlag.OccupiedInQuestEvent] ||
               Plugin.Condition[ConditionFlag.InFlight];
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(this);
    }
}
