using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.STD;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using OPP.Windows;

namespace OPP.fr;

public unsafe class UIBuilder : IDisposable
{
    public Configuration Configuration;

    private DalamudPluginInterface pi;

    private readonly Dictionary<uint, ushort> sizeFactorDict;
    //private Dictionary<ushort, Map[]> TerritoryMapsDictionary { get; }

    private ImDrawListPtr BDL;

    private Vector2? mapOrigin = Vector2.Zero;
    private Vector2[] MapPosSize = new Vector2[2];

    private List<(Vector3 worldpos, uint fgcolor, uint bgcolor, string name, string comp)> ObjectList2D = new List<(Vector3, uint, uint, string, string)>();

    private CanAttackDelegate CanAttack;
    private delegate int CanAttackDelegate(int arg, IntPtr objectAddress);

    PlayerState* playerState = PlayerState.Instance();

    public int GrandCompanyId = -1;

    public const uint Red = 4278190335u;

    public const uint Magenta = 4294902015u;

    public const uint Yellow = 4278255615u;

    public const uint Green = 4278255360u;

    public const uint GrassGreen = 4278247424u;

    public const uint Cyan = 4294967040u;

    public const uint DarkCyan = 4287664128u;

    public const uint LightCyan = 4294967200u;

    public const uint Blue = 4294901760u;

    public const uint Black = 4278190080u;

    public const uint TransBlack = 2147483648u;

    public const uint Grey = 4286611584u;

    public const uint White = uint.MaxValue;

    private float GlobalUIScale = 1f;
    private float WorldToMapScale => AreaMap.MapScale * sizeFactorDict[Plugin.clientState.TerritoryType] / 100f * GlobalUIScale;

    public UIBuilder(Plugin plugin, DalamudPluginInterface pluginInterface, Configuration config)
    {
        pi = pluginInterface;
        Configuration = config;
        sizeFactorDict = Plugin.DataManager.GetExcelSheet<TerritoryType>().ToDictionary((TerritoryType k) => k.RowId, (TerritoryType v) => v.Map.Value.SizeFactor);
        CanAttack = Marshal.GetDelegateForFunctionPointer<CanAttackDelegate>(Plugin.Scanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3"));
        Plugin.clientState.TerritoryChanged += TerritoryChanged;
        pi.UiBuilder.Draw += UiBuilder_OnBuildUi;
    }

    private void TerritoryChanged(ushort territoryId)
    {
        //切图清空记录
        //Plugin.log.Error($"territory changed to: {territoryId}", Array.Empty<object>());
    }

    public void Dispose()
    {
        pi.UiBuilder.Draw -= UiBuilder_OnBuildUi;
        Plugin.clientState.TerritoryChanged -= TerritoryChanged;
    }

    private unsafe void UiBuilder_OnBuildUi()
    {
        bool flag = false;
        try
        {
            if (Plugin.clientState.LocalPlayer != null && !(Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51]) && Plugin.clientState.IsPvP)
            {
                flag = true;
            }
            else
            {
                flag = false;
            }
        }
        catch (Exception)
        {
            flag = false;
        }
        if (flag)
        {
            BDL = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
            RefreshObjects();
            if (Configuration.Overlay2D_Enabled)
            {
                DrawMapOverlay();
            }
        }
        ObjectList2D.Clear();
    }

    private void RefreshObjects()
    {
        foreach (var o in Plugin.objectTable)
        {
            if (o.Name is not null && !"".Equals(o.Name) && o.ObjectId != Plugin.clientState.LocalPlayer.ObjectId)
            {
                try {
                    var enemyPlayer = o as PlayerCharacter;
                    if (o != null && CanAttack(142, enemyPlayer.Address) == 1)
                    {
                        ObjectList2D.Add((enemyPlayer.Position, White, 4278190080u, enemyPlayer.Name.ToString(), enemyPlayer.CompanyTag.ToString()));
                    }
                }
                catch (Exception) { 
                    
                }
            }
        }
    }
    private void DrawMapOverlay()
    {
        RefreshMapOrigin(); //刷新小地图位置
        Vector2? vector = mapOrigin;
        if (!vector.HasValue)
        {
            return;
        }
        Vector2 valueOrDefault = vector.GetValueOrDefault();
        if (!(valueOrDefault != Vector2.Zero) || Plugin.clientState.TerritoryType == 0)
        {
            return;
        }
        BDL.PushClipRect(MapPosSize[0], MapPosSize[1]); //仅在小地图区域内绘制

        foreach (var item in ObjectList2D)
        {
            Vector2 pos = WorldToMap(valueOrDefault, item.worldpos);
            BDL.DrawMapTextDot(pos, item.comp, item.fgcolor, item.bgcolor);
        }
        if (Configuration.Overlay2D_ShowAssist) {
            BDL.AddCircle(valueOrDefault, WorldToMapScale * 125f, 4286611584u, 0, 1f); //服务器加载范围
        }
        BDL.PopClipRect();
    }
    private unsafe void RefreshMapOrigin()
    {
        mapOrigin = null;
        if (!AreaMap.MapVisible)
        {
            return;
        }
        AtkUnitBase* areaMapAddon = AreaMap.AreaMapAddon;
        GlobalUIScale = (*areaMapAddon).Scale;
        if (((*areaMapAddon).UldManager).NodeListCount <= 4)
        {
            return;
        }
        AtkComponentNode* ptr = (AtkComponentNode*)((*areaMapAddon).UldManager).NodeList[3];
        AtkResNode atkResNode = (*ptr).AtkResNode;
        if ((*(&(*(*ptr).Component).UldManager)).NodeListCount < 233)
        {
            return;
        }
        for (int i = 6; i < (*(&(*(*ptr).Component).UldManager)).NodeListCount - 1; i++)
        {
            if (!(*(*(&(*(*ptr).Component).UldManager)).NodeList[i]).IsVisible)
            {
                continue;
            }
            AtkComponentNode* ptr2 = (AtkComponentNode*)(*(&(*(*ptr).Component).UldManager)).NodeList[i];
            AtkImageNode* ptr3 = (AtkImageNode*)(*(&(*(*ptr2).Component).UldManager)).NodeList[4];
            string text = null;
            if ((*ptr3).PartsList != null && (*ptr3).PartId <= (*(*ptr3).PartsList).PartCount)
            {
                AtkUldAsset* uldAsset = (*(AtkUldPart*)((byte*)(*(*ptr3).PartsList).Parts + ((*ptr3).PartId * (nint)Unsafe.SizeOf<AtkUldPart>()))).UldAsset;
                if ((int)(*(&(*uldAsset).AtkTexture)).TextureType == 1)
                {
                    StdString fileName = ((*(*((*uldAsset).AtkTexture).Resource).TexFileResourceHandle).ResourceHandle).FileName;
                    text = Path.GetFileName(fileName.ToString());
                }
            }
            if (text == "060443.tex" || text == "060443_hr1.tex")
            {
                AtkComponentNode* ptr4 = (AtkComponentNode*)(*(&(*(*ptr).Component).UldManager)).NodeList[i];
                AtkResNode atkResNode2 = (*ptr4).AtkResNode;
                Vector2 vector = new Vector2((*areaMapAddon).X, (*areaMapAddon).Y);
                ImGuiViewportPtr mainViewport = ImGui.GetMainViewport();
                mapOrigin = mainViewport.Pos + vector + (new Vector2(atkResNode.X, atkResNode.Y) + new Vector2(atkResNode2.X, atkResNode2.Y) + new Vector2(atkResNode2.OriginX, atkResNode2.OriginY)) * GlobalUIScale;
                Vector2[] mapPosSize = MapPosSize;
                mainViewport = ImGui.GetMainViewport();
                mapPosSize[0] = mainViewport.Pos + vector + new Vector2(atkResNode.X, atkResNode.Y) * GlobalUIScale;
                Vector2[] mapPosSize2 = MapPosSize;
                mainViewport = ImGui.GetMainViewport();
                mapPosSize2[1] = mainViewport.Pos + vector + new Vector2(atkResNode.X, atkResNode.Y) + new Vector2((int)atkResNode.Width, (int)atkResNode.Height) * GlobalUIScale;
                break;
            }
        }
    }

    private Vector2 WorldToMap(Vector2 origin, Vector3 worldVector3)
    {
        Vector2 vector = (ToVector2(worldVector3) - ToVector2(Plugin.clientState.LocalPlayer.Position)) * WorldToMapScale;
        return origin + vector;
    }
    public static Vector2 ToVector2(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
    public static Vector3 ToVector3(Vector2 v)
    {
        return new Vector3(v.X, 0, v.Y);
    }
    public static float MapToWorld(float value, uint scale, int offset = 0)
    {
        return offset * (scale / 100.0f) + 50.0f * (value - 1) * (scale / 100.0f);
    }
    public static Vector2 MapToWorld(Vector2 coordinates, Lumina.Excel.GeneratedSheets.Map map)
    {
        var scalar = map.SizeFactor / 100.0f;

        var xWorldCoord = MapToWorld(coordinates.X, map.SizeFactor, map.OffsetX);
        var yWorldCoord = MapToWorld(coordinates.Y, map.SizeFactor, map.OffsetY);

        var objectPosition = new Vector2(xWorldCoord, yWorldCoord);
        var center = new Vector2(1024.0f, 1024.0f);

        return objectPosition / scalar - center / scalar;
    }
}
