using System;
using System.Collections.Generic;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace ToucanTally.Rendering;

/// <summary>
/// Loads the game's own fonts (Jupiter, Axis and the others) at the sizes the boxes ask for. Each font and size is
/// loaded once, the first time it is needed, and kept until the plugin unloads.
/// </summary>
internal sealed class FontService : IDisposable
{
    private const int MinSize = 10;
    private const int MaxSize = 160;

    private readonly Dictionary<(GameFontFamily Family, int Size), IFontHandle> handles = new();

    /// <summary>
    /// Returns the font at that size. Returns null for the first frame or two while Dalamud is still loading it; the
    /// text is then drawn in the default font.
    /// </summary>
    public IFontHandle? Get(GameFontFamily family, float sizePx)
    {
        var size = Math.Clamp((int)MathF.Round(sizePx), MinSize, MaxSize);
        if (!handles.TryGetValue((family, size), out var handle))
        {
            handle = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(family, size));
            handles[(family, size)] = handle;
        }

        return handle.Available ? handle : null;
    }

    public void Dispose()
    {
        foreach (var handle in handles.Values)
        {
            handle.Dispose();
        }

        handles.Clear();
    }
}
