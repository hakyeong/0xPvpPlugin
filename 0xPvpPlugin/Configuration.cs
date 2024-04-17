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

namespace OPP.Windows;
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ConfigWindowVisible = false;
    public Vector2 ConfigWindowPos = new(20, 20);
    public Vector2 ConfigWindowSize = new(300, 300);
    public float ConfigWindowBgAlpha = 1;

    public bool AutoSelect = false;
    public int SelectDistance = 20;
    public int SelectInterval = 50;

    public float Dspeed = 1.0f;
    internal static float speedOffset = 6f;

    public int upDistance = 0;
    public int downDistance = 0;

    public int downDistanceAll = 0;

    public bool SLBX = false;
    public bool MS = true;
    public bool TD = true;
    public bool XDTZ = true;

    [NonSerialized]
    public DalamudPluginInterface? pluginInterface;
    [NonSerialized]
    private Plugin plugin;
    [PluginService]
    public static IChatGui chatGui { get; private set; } = null!;
    public IObjectTable nearObjects;
    public List<PlayerCharacter> EnermyActors = new List<PlayerCharacter>();
    public PlayerCharacter LocalPlayer;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        Plugin.SetSpeed(Dspeed * speedOffset);
    }

    public bool DrawConfigUI()
    {
        var drawConfig = true;

        var scale = ImGui.GetIO().FontGlobalScale;

        var modified = false;

        ImGui.SetNextWindowSize(new Vector2(560 * scale, 200), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(560 * scale, 200), new Vector2(560 * scale, 200));
        ImGui.Begin("设置", ref drawConfig, ImGuiWindowFlags.NoResize);


        if (ImGui.Checkbox("忍者索敌", ref AutoSelect))
        {
            Save();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("弃用土遁(pvp)", ref TD))
        {
            Save();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("弃用命水(pvp)", ref MS))
        {
            Save();
        }

        ImGui.SetNextItemWidth(100);
        modified |= ImGui.SliderInt("选择范围(m)", ref SelectDistance, 5, 30);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        modified |= ImGui.SliderInt("选择间隔(ms)", ref SelectInterval, 50, 100);

        ImGui.NewLine();

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

        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##up", ref upDistance))
        {
            Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("上升(m)"))
        {
            if (Plugin.clientState.LocalPlayer != null)
            {
                var pos = Plugin.clientState.LocalPlayer.Position;
                var address = Plugin.clientState.LocalPlayer.Address;
                SafeMemory.Write(address + 180, pos.Y + upDistance);
            }
        }

        ImGui.End();


        if (modified)
        {
            Save();
        }

        return drawConfig;
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(this);
    }
}
