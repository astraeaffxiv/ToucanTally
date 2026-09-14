using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ToucanTally.Events;

namespace ToucanTally.Windows;

internal sealed class ConfigWindow : Window
{
    private static readonly GameFontFamily[] Fonts =
    [
        GameFontFamily.Axis,
        GameFontFamily.Jupiter,
        GameFontFamily.Meidinger,
        GameFontFamily.MiedingerMid,
        GameFontFamily.TrumpGothic,
    ];

    private const string GitHubUrl = "https://github.com/astraeaffxiv/ToucanTally";
    private const string PatreonUrl = "https://www.patreon.com/cw/FFXIV_Aether";
    private const string DeleteAreaPopup = "Delete area?";
    private const string ResetPopup = "Reset Toucan Tally?";

    private readonly Plugin plugin;
    private Guid selectedArea;
    private Guid? areaToDelete;
    private bool resetRequested;
    private string newFilterName = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("Toucan Tally")
    {
        this.plugin = plugin;
        Size = new Vector2(760, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnClose()
    {
        plugin.Overlay.MoveMode = false;
        plugin.Overlay.PreviewMode = false;
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("tabs"))
        {
            return;
        }

        Tab("General", DrawGeneral);
        Tab("Areas", DrawAreas);
        Tab("Events", DrawEvents);
        Tab("Modifiers", DrawModifiers);
        Tab("Filters", DrawFilters);
        Tab("About", DrawAbout);
        ImGui.EndTabBar();

        DrawDeleteAreaModal();
        DrawResetModal();
    }

    private void DrawDeleteAreaModal()
    {
        if (areaToDelete is { } id && !ImGui.IsPopupOpen(DeleteAreaPopup))
        {
            ImGui.OpenPopup(DeleteAreaPopup);
        }

        if (!ImGui.BeginPopupModal(DeleteAreaPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        var area = plugin.Configuration.Areas.FirstOrDefault(a => a.Id == areaToDelete);
        if (area == null)
        {
            areaToDelete = null;
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        ImGui.TextUnformatted($"Delete the area \"{area.Name}\"?");
        ImGui.TextUnformatted("Events assigned to it move to the first area.");
        ImGui.Separator();

        if (ImGui.Button("Delete", new Vector2(120, 0)))
        {
            DeleteArea(area);
            areaToDelete = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
        {
            areaToDelete = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawResetModal()
    {
        if (resetRequested && !ImGui.IsPopupOpen(ResetPopup))
        {
            ImGui.OpenPopup(ResetPopup);
        }

        if (!ImGui.BeginPopupModal(ResetPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.TextUnformatted("Reset every Toucan Tally setting to the factory defaults?");
        ImGui.TextUnformatted("Areas, positions, events, modifiers and filters are all replaced.");
        ImGui.TextUnformatted("This cannot be undone.");
        ImGui.Separator();

        if (ImGui.Button("Reset everything", new Vector2(160, 0)))
        {
            plugin.ResetConfiguration();
            selectedArea = Guid.Empty;
            resetRequested = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0)))
        {
            resetRequested = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DeleteArea(ScrollAreaConfig area)
    {
        var configuration = plugin.Configuration;
        configuration.Areas.Remove(area);
        var fallback = configuration.Areas[0].Id;
        foreach (var setting in configuration.Events.Values.Where(s => s.AreaId == area.Id))
        {
            setting.AreaId = fallback;
        }

        selectedArea = fallback;
        plugin.SaveConfiguration();
    }

    private static void Tab(string name, Action body)
    {
        if (ImGui.BeginTabItem(name))
        {
            body();
            ImGui.EndTabItem();
        }
    }

    private void DrawGeneral()
    {
        var configuration = plugin.Configuration;
        var changed = false;

        var hide = configuration.HideNative;
        if (ImGui.Checkbox("Hide the game's flying text and pop-up text", ref hide))
        {
            configuration.HideNative = hide;
            changed = true;
        }

        ImGui.Checkbox("Move and resize areas", ref plugin.Overlay.MoveMode);
        ImGui.Checkbox("Preview sample text", ref plugin.Overlay.PreviewMode);

        ImGui.Separator();
        ImGui.TextUnformatted("Master font");
        changed |= FontCombo("Font##master", configuration.Master, static (m, f) => m.Font = f, configuration.Master.Font);
        var size = configuration.Master.FontSize;
        if (ImGui.SliderInt("Font size##master", ref size, 12, 72))
        {
            configuration.Master.FontSize = size;
            changed = true;
        }

        changed |= OutlineCombo("Outline##master", configuration.Master.Outline, o => configuration.Master.Outline = o);

        ImGui.Separator();
        ImGui.TextUnformatted("Numbers and names");
        if (ImGui.BeginCombo("Number format", configuration.Numbers.ToString()))
        {
            foreach (var format in Enum.GetValues<NumberFormat>())
            {
                if (ImGui.Selectable(format.ToString(), format == configuration.Numbers))
                {
                    configuration.Numbers = format;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        var shortThrottle = configuration.ShortThrottleText;
        if (ImGui.Checkbox("Short merged text (5++ instead of 5 hits)", ref shortThrottle))
        {
            configuration.ShortThrottleText = shortThrottle;
            changed = true;
        }

        var byRole = configuration.ColorNamesByRole;
        if (ImGui.Checkbox("Colour player names by role", ref byRole))
        {
            configuration.ColorNamesByRole = byRole;
            changed = true;
        }

        changed |= Color("Tank", configuration.TankColor, c => configuration.TankColor = c);
        ImGui.SameLine();
        changed |= Color("Healer", configuration.HealerColor, c => configuration.HealerColor = c);
        ImGui.SameLine();
        changed |= Color("DPS", configuration.DpsColor, c => configuration.DpsColor = c);

        ImGui.Separator();
        ImGui.TextUnformatted("Healing");
        var healNames = configuration.ShowNamesOnHeals;
        if (ImGui.Checkbox("Show the party member's name on heals", ref healNames))
        {
            configuration.ShowNamesOnHeals = healNames;
            changed = true;
        }

        var hideOverheal = configuration.HideFullOverheals;
        if (ImGui.Checkbox("Hide full overheals", ref hideOverheal))
        {
            configuration.HideFullOverheals = hideOverheal;
            changed = true;
        }

        var showOverheal = configuration.ShowOverhealAmount;
        if (ImGui.Checkbox("Show overheal amount", ref showOverheal))
        {
            configuration.ShowOverhealAmount = showOverheal;
            changed = true;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Merging");
        var mergeAoe = configuration.MergeAoe;
        if (ImGui.Checkbox("Merge hits of one action that land together", ref mergeAoe))
        {
            configuration.MergeAoe = mergeAoe;
            changed = true;
        }

        var aoeWindow = configuration.AoeMergeSeconds;
        if (ImGui.SliderFloat("Merge window (s)", ref aoeWindow, 0.1f, 1.0f, "%.1f"))
        {
            configuration.AoeMergeSeconds = aoeWindow;
            changed = true;
        }

        ImGui.Separator();
        if (ImGui.Button("Reset to factory defaults"))
        {
            resetRequested = true;
        }

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private void DrawAreas()
    {
        var configuration = plugin.Configuration;

        ImGui.BeginChild("areaList", new Vector2(180, 0), true);
        foreach (var area in configuration.Areas)
        {
            if (ImGui.Selectable($"{area.Name}##{area.Id}", area.Id == selectedArea))
            {
                selectedArea = area.Id;
            }
        }

        ImGui.Separator();
        if (ImGui.Button("Add"))
        {
            var area = new ScrollAreaConfig { Name = $"Area {configuration.Areas.Count + 1}" };
            configuration.Areas.Add(area);
            selectedArea = area.Id;
            plugin.SaveConfiguration();
        }

        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("areaEdit", new Vector2(0, 0), false);
        var selected = configuration.Areas.FirstOrDefault(a => a.Id == selectedArea);
        if (selected != null)
        {
            DrawArea(selected);
        }
        else
        {
            ImGui.TextDisabled("Select an area.");
        }

        ImGui.EndChild();
    }

    private void DrawArea(ScrollAreaConfig area)
    {
        var changed = false;

        var name = area.Name;
        if (ImGui.InputText("Name", ref name, 32))
        {
            area.Name = name;
            changed = true;
        }

        var width = area.Width;
        if (ImGui.SliderFloat("Width", ref width, 100, 1200, "%.0f"))
        {
            area.Width = width;
            changed = true;
        }

        var height = area.Height;
        if (ImGui.SliderFloat("Height", ref height, 60, 1200, "%.0f"))
        {
            area.Height = height;
            changed = true;
        }

        changed |= EnumCombo("Text alignment", area.Align, a => area.Align = a);

        var showIcons = area.ShowIcons;
        if (ImGui.Checkbox("Show icons", ref showIcons))
        {
            area.ShowIcons = showIcons;
            changed = true;
        }

        changed |= EnumCombo("Icon side", area.IconSide, s => area.IconSide = s);

        var iconAlpha = area.IconAlpha;
        if (ImGui.SliderFloat("Icon opacity", ref iconAlpha, 0.1f, 1f, "%.1f"))
        {
            area.IconAlpha = iconAlpha;
            changed = true;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Normal text");
        changed |= DrawStyle("normal", area.Normal);

        ImGui.Separator();
        ImGui.TextUnformatted("Sticky text (crits and important lines)");
        changed |= DrawStyle("sticky", area.Sticky);

        ImGui.Separator();
        if (plugin.Configuration.Areas.Count > 1 && ImGui.Button("Delete area"))
        {
            areaToDelete = area.Id;
        }

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private bool DrawStyle(string id, TextStyle style)
    {
        var changed = false;
        ImGui.PushID(id);

        var inherit = style.InheritFont;
        if (ImGui.Checkbox("Use master font", ref inherit))
        {
            style.InheritFont = inherit;
            changed = true;
        }

        if (!inherit)
        {
            changed |= FontCombo("Font", style, static (s, f) => s.Font = f, style.Font);
            var size = style.FontSize;
            if (ImGui.SliderInt("Font size", ref size, 12, 72))
            {
                style.FontSize = size;
                changed = true;
            }

            changed |= OutlineCombo("Outline", style.Outline, o => style.Outline = o);
        }

        changed |= EnumCombo("Animation", style.Animation, a => style.Animation = a);
        changed |= EnumCombo("Direction", style.Direction, d => style.Direction = d);

        var alternate = style.Alternate;
        if (ImGui.Checkbox("Alternate sides", ref alternate))
        {
            style.Alternate = alternate;
            changed = true;
        }

        var travel = style.TravelSeconds;
        if (ImGui.SliderFloat("Travel time (s)", ref travel, 0.5f, 10, "%.1f"))
        {
            style.TravelSeconds = travel;
            changed = true;
        }

        if (style.Animation == AnimationStyle.Static)
        {
            var intro = style.IntroScale;
            if (ImGui.SliderFloat("Zoom-in size", ref intro, 1f, 5f, "%.1fx"))
            {
                style.IntroScale = intro;
                changed = true;
            }
        }

        ImGui.PopID();
        return changed;
    }

    private void DrawEvents()
    {
        var configuration = plugin.Configuration;
        var changed = false;

        if (!ImGui.BeginTable("events", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            return;
        }

        ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Area", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Colour", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Sticky", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("Merge (s)", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Min amount", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableHeadersRow();

        foreach (var category in Enum.GetValues<EventCategory>())
        {
            if (!configuration.Events.TryGetValue(category, out var setting))
            {
                continue;
            }

            ImGui.PushID(category.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Label(category));

            ImGui.TableNextColumn();
            var enabled = setting.Enabled;
            if (ImGui.Checkbox("##on", ref enabled))
            {
                setting.Enabled = enabled;
                changed = true;
            }

            ImGui.TableNextColumn();
            var current = configuration.Areas.FirstOrDefault(a => a.Id == setting.AreaId)?.Name ?? "?";
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##area", current))
            {
                foreach (var area in configuration.Areas)
                {
                    if (ImGui.Selectable($"{area.Name}##{area.Id}", area.Id == setting.AreaId))
                    {
                        setting.AreaId = area.Id;
                        changed = true;
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            changed |= Color("##color", setting.Color, c => setting.Color = c);

            ImGui.TableNextColumn();
            var sticky = setting.Sticky;
            if (ImGui.Checkbox("##sticky", ref sticky))
            {
                setting.Sticky = sticky;
                changed = true;
            }

            ImGui.TableNextColumn();
            var throttle = setting.ThrottleSeconds;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##throttle", ref throttle, 0, 5, "%.1f"))
            {
                setting.ThrottleSeconds = throttle;
                changed = true;
            }

            ImGui.TableNextColumn();
            var min = setting.MinAmount;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("##min", ref min, 0, 0))
            {
                setting.MinAmount = Math.Max(0, min);
                changed = true;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private void DrawModifiers()
    {
        var configuration = plugin.Configuration;
        var changed = false;

        ImGui.TextDisabled("A modifier colours the number, adds its text after the name, and can make the line sticky.");
        if (!ImGui.BeginTable("modifiers", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            return;
        }

        ImGui.TableSetupColumn("Modifier", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Colour and sticky", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableHeadersRow();

        foreach (var modifier in Enum.GetValues<Modifier>())
        {
            if (!configuration.Modifiers.TryGetValue(modifier, out var setting))
            {
                continue;
            }

            ImGui.PushID(modifier.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(modifier.ToString());

            ImGui.TableNextColumn();
            var enabled = setting.Enabled;
            if (ImGui.Checkbox("##on", ref enabled))
            {
                setting.Enabled = enabled;
                changed = true;
            }

            ImGui.TableNextColumn();
            var text = setting.Text;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##text", ref text, 24))
            {
                setting.Text = text;
                changed = true;
            }

            ImGui.TableNextColumn();
            changed |= Color("##color", setting.Color, c => setting.Color = c);
            ImGui.SameLine();
            var sticky = setting.Sticky;
            if (ImGui.Checkbox("Sticky", ref sticky))
            {
                setting.Sticky = sticky;
                changed = true;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private static void DrawAbout()
    {
        ImGui.TextUnformatted("Toucan Tally");
        ImGui.TextDisabled($"Version {typeof(Plugin).Assembly.GetName().Version}");
        ImGui.Separator();

        AboutRow("Created by", "Astraea");
        AboutRow("Requested by", "Nihal");
        AboutRow("Inspired by", "Parrot for World of Warcraft");
        ImGui.Separator();

        if (ImGui.Button("GitHub"))
        {
            Util.OpenLink(GitHubUrl);
        }

        ImGui.SameLine();
        if (ImGui.Button("Patreon"))
        {
            Util.OpenLink(PatreonUrl);
        }
    }

    private static void AboutRow(string label, string value)
    {
        ImGui.TextDisabled(label);
        ImGui.SameLine(140);
        ImGui.TextUnformatted(value);
    }

    private void DrawFilters()
    {
        var configuration = plugin.Configuration;
        var changed = false;

        ImGui.TextWrapped("Hide a skill or status you do not want to see, or hide only its small numbers.");
        ImGui.TextWrapped("Type the name the way the game writes it, for example Heavy Swing or Regen. Capital letters do not matter.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(240);
        ImGui.InputText("##newFilter", ref newFilterName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add") && newFilterName.Trim().Length > 0)
        {
            configuration.SpellFilters.Add(new SpellFilter { Name = newFilterName.Trim() });
            newFilterName = string.Empty;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Hide always: this skill or status never shows.");
        ImGui.TextWrapped("Hide below: numbers smaller than this do not show. 0 shows every number.");
        ImGui.Spacing();

        if (ImGui.BeginTable("filters", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Skill or status", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Hide always", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Hide below", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableHeadersRow();

            for (var i = configuration.SpellFilters.Count - 1; i >= 0; i--)
            {
                var filter = configuration.SpellFilters[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(filter.Name);

                ImGui.TableNextColumn();
                var hide = filter.Hide;
                if (ImGui.Checkbox("##hide", ref hide))
                {
                    filter.Hide = hide;
                    changed = true;
                }

                ImGui.TableNextColumn();
                var min = filter.MinAmount;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##min", ref min, 0, 0))
                {
                    filter.MinAmount = Math.Max(0, min);
                    changed = true;
                }

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Remove"))
                {
                    configuration.SpellFilters.RemoveAt(i);
                    changed = true;
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private static bool FontCombo<T>(string label, T owner, Action<T, GameFontFamily> set, GameFontFamily current)
    {
        var changed = false;
        if (ImGui.BeginCombo(label, current.ToString()))
        {
            foreach (var font in Fonts)
            {
                if (ImGui.Selectable(font.ToString(), font == current))
                {
                    set(owner, font);
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool OutlineCombo(string label, Outline current, Action<Outline> set)
    {
        return EnumCombo(label, current, set);
    }

    private static bool EnumCombo<T>(string label, T current, Action<T> set)
        where T : struct, Enum
    {
        var changed = false;
        if (ImGui.BeginCombo(label, current.ToString()))
        {
            foreach (var value in Enum.GetValues<T>())
            {
                if (ImGui.Selectable(value.ToString(), value.Equals(current)))
                {
                    set(value);
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool Color(string label, Vector4 current, Action<Vector4> set)
    {
        var color = current;
        if (ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.NoInputs))
        {
            set(color);
            return true;
        }

        return false;
    }

    private static string Label(EventCategory category)
    {
        return category switch
        {
            EventCategory.OutgoingDamage => "Your damage",
            EventCategory.OutgoingHeal => "Your healing on others",
            EventCategory.OutgoingMiss => "Your misses",
            EventCategory.IncomingDamage => "Damage on you",
            EventCategory.IncomingHeal => "Healing on you",
            EventCategory.IncomingMiss => "Attacks you avoided",
            EventCategory.MpGain => "MP gained",
            EventCategory.MpLoss => "MP lost",
            EventCategory.BuffGain => "Buff gained",
            EventCategory.BuffFade => "Buff faded",
            EventCategory.DebuffGain => "Debuff gained",
            EventCategory.DebuffFade => "Debuff faded",
            EventCategory.OwnCast => "Your ability casts",
            EventCategory.KillingBlow => "Killing blows",
            _ => category.ToString(),
        };
    }
}
