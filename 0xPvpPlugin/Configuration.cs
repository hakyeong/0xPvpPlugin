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
        ImGui.Begin("OPP����", ref drawConfig);

        ImGui.BeginTabBar("tabbar");
        if (ImGui.BeginTabItem("����"))
        {
            if (ImGui.Checkbox("����LB����ʱ����", ref Ninoption))
            {
                Save();
            }
            if (ImGui.Checkbox("��������(pvp)", ref TD))
            {
                Save();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("������ˮ(pvp)", ref MS))
            {
                Save();
            }
            if (ImGui.Checkbox("�Զ��ͷ��Ƕ�����Ƽ��ֶ���", ref AutoXDTZ))
            {
                Save();
            }
            ImGui.SetNextItemWidth(120);
            modified |= ImGui.SliderInt("��ն��ʱ(ms)", ref AutoXDTZDelay, 500, 3000);

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("��ʿ"))
        {
            if (ImGui.Checkbox("����ֹ��", ref SLBX))
            {
                Save();
            }
            if (ImGui.Checkbox("�Զ��ͷ�ն����", ref AutoZhan))
            {
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("��Χ��������ֵ", ref AutoZhanNum))
            {
                if (AutoZhanNum < 1)
                {
                    AutoZhanNum = 1;
                }
                Save();
            }

            //ImGui.SetWindowFontScale(3.7f);
            ImGui.NewLine();
            ImGui.Text("��Χ��������" + Plugin.totalPlayer);
            //ImGui.SetWindowFontScale(1.0f);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("DK"))
        {
            ImGui.Text("ѡ���˶� /0x TargetOnce");
            if (ImGui.IsItemHovered())
            {
                // ��ʾ���ߵ���ʾ
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("д�����");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            if (ImGui.Checkbox("�Զ���ն", ref AutoTiaoZhan))
            {
                Save();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("��ħ"))
        {
            if (ImGui.Checkbox("�Զ�������", ref AutoBianZhu))
            {
                Save();
            }
            if (ImGui.RadioButton("������ʿ", ref AutoBianZhuSAMint, 1))
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
                // ��ʾ���ߵ���ʾ
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("50000Ѫ���ϵ�DK");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("ͨ��"))
        {
            if (ImGui.Checkbox("�Զ�����ɨ�������Ƽ���", ref AutoSelectInterval))
            {
                Save();
            }
            ImGui.BeginDisabled(AutoSelectInterval);
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("ɨ����(ms)", ref SelectInterval))
            {
                Save();
            }
            ImGui.EndDisabled();

            ImGui.NewLine();

            ImGui.Text("ѡ���˶� /0x Select");
            if (ImGui.IsItemHovered())
            {
                // ��ʾ���ߵ���ʾ
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("ȫְҵͨ�ã���DK�Ĳ�һ��");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("ѡ�˷�Χ(m)", ref SelectDistance))
            {
                if (SelectDistance < 5)
                {
                    SelectDistance = 5;
                }
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("���ܷ�Χ(m)", ref SelectSkillRange))
            {
                if (SelectSkillRange < 5) {
                    SelectSkillRange = 5;
                }
                Save();
            }

            ImGui.EndTabItem();
        }        
        
        if (ImGui.BeginTabItem("λ����� (�����Ը�)"))
        {
            if (ImGui.Checkbox("����λ�ƣ�DK��Ͱ/սʿ������", ref antiKnockback))
            {
                Save();
            }
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("�ƶ��ٶ�(��)", ref Dspeed, 0.1f, 1f, "%.1f", ImGuiInputTextFlags.NoHorizontalScroll))
            {
                Plugin.SetSpeed(Dspeed * speedOffset);
                Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("����"))
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
            if (ImGui.Button("�½�(m)"))
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
            if (ImGui.Button("�ص�����"))
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
            if (ImGui.Button("����(m)"))
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
        if (ImGui.BeginTabItem("ս���״�"))
        {
            if (ImGui.Checkbox("����", ref Overlay2D_Enabled))
            {
                Save();
            }
            if (ImGui.Checkbox("��ʾ���ط�ΧȦ", ref Overlay2D_ShowAssist))
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
