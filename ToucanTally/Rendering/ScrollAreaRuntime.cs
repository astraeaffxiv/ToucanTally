using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Textures;
using ToucanTally.Formatting;

namespace ToucanTally.Rendering;

/// <summary>
/// This is where the rendering happens, and where the plugin really comes together 
/// from hooking events, to creating objects that are queued, and finally rendered.
/// 
/// Everything currently on screen in one content box, for example the Outgoing box. A box has two separate streams of
/// text. Normal text scrolls through the box. Sticky text (crits, killing blows) zooms in and stays in the middle
/// for a moment. Each stream uses its own style from the box's settings.
/// </summary>
internal sealed class ScrollAreaRuntime
{
    /// <summary>One piece of combat text on screen, for example "12,345 Heavy Swing" with its icon.</summary>
    private sealed class Line
    {
        /// <summary>The text pieces, colours and icon to draw.</summary>
        public required ComposedLine Content { get; init; }

        /// <summary>1 or -1: which way the curving animation styles bend this text.</summary>
        public required int Side { get; init; }

        /// <summary>Counts up by one for every new text in the stream. Sprinkler uses it to pick an angle.</summary>
        public required int Index { get; init; }

        /// <summary>0 when the text appears, 1 when it is removed.</summary>
        public float Progress;

        /// <summary>Sticky text only: 0 is the newest text in the middle, older ones are pushed out one slot at a time.</summary>
        public float Slot;

        /// <summary>Sticky text only: where the text is drawn right now. It glides toward <see cref="Slot"/> instead of jumping.</summary>
        public float ShownSlot;
    }

    private sealed class Stream
    {
        public readonly List<Line> Lines = new();
        public readonly Queue<ComposedLine> Pending = new();
        public int Counter;
    }

    /// <summary>At most this many texts wait for room. In a big burst the oldest waiting text is dropped.</summary>
    private const int MaxPending = 12;

    /// <summary>At most this many sticky texts on screen at once. A new one pushes the oldest off.</summary>
    private const int MaxStatic = 3;

    /// <summary>Text starts fading out after 70% of its time on screen.</summary>
    private const float FadeStart = 0.7f;

    /// <summary>The sticky zoom-in takes the first 18% of the text's time on screen.</summary>
    private const float IntroEnd = 0.18f;

    /// <summary>Standard "ease out back" constant. It makes the zoom-in overshoot slightly and settle.</summary>
    private const float BackOvershoot = 2.70158f;

    /// <summary>Sticky text grows by 30% while it fades out.</summary>
    private const float DissolveGrow = 0.3f;

    /// <summary>How quickly sticky text glides to its new slot. Higher is faster.</summary>
    private const float SlotEase = 12f;

    private const float IconGap = 4f;
    private const float LineSpacing = 1.2f;

    /// <summary>The name shown under the main text is 80% of the main text's size.</summary>
    private const float SubLineScale = 0.8f;

    private readonly Stream normal = new();
    private readonly Stream sticky = new();

    /// <summary>Queues a new text for this box. It appears as soon as there is room.</summary>
    public void Add(ComposedLine line)
    {
        var stream = line.Sticky ? sticky : normal;
        if (stream.Pending.Count >= MaxPending)
        {
            stream.Pending.Dequeue();
        }

        stream.Pending.Enqueue(line);
    }

    /// <summary>Moves and draws everything in this box for one frame.</summary>
    public void Draw(ScrollAreaConfig area, Configuration configuration, FontService fonts, ImDrawListPtr drawList, Vector2 viewportPos, Vector2 viewportSize, float deltaSeconds)
    {
        var origin = viewportPos + new Vector2(area.X * viewportSize.X, area.Y * viewportSize.Y);
        DrawStream(normal, area, area.Normal, configuration, fonts, drawList, origin, deltaSeconds);
        DrawStream(sticky, area, area.Sticky, configuration, fonts, drawList, origin, deltaSeconds);
    }

    private static void DrawStream(Stream stream, ScrollAreaConfig area, TextStyle style, Configuration configuration, FontService fonts, ImDrawListPtr drawList, Vector2 origin, float deltaSeconds)
    {
        // "Use master font" on: take the font from the General page instead of this box's own setting.
        var font = style.InheritFont ? configuration.Master.Font : style.Font;
        var baseSize = style.InheritFont ? configuration.Master.FontSize : style.FontSize;
        var outline = style.InheritFont ? configuration.Master.Outline : style.Outline;
        var lineHeight = baseSize * LineSpacing;
        var isStatic = style.Animation == AnimationStyle.Static;

        Advance(stream, style, lineHeight, area.Height, deltaSeconds);

        foreach (var line in stream.Lines)
        {
            var restSize = (float)(line.Content.FontSize ?? baseSize);
            var drawSize = restSize;
            var atlasSize = restSize;
            var alpha = line.Progress < FadeStart ? 1f : 1f - (line.Progress - FadeStart) / (1f - FadeStart);
            var (x, y) = Animations.Position(style, line.Progress, line.Side, line.Index, area.Width, area.Height, lineHeight);

            if (isStatic)
            {
                var (scale, introAlpha) = Impact(line.Progress, style.IntroScale);
                drawSize = restSize * scale;

                // Load the font once at the biggest size the zoom reaches and shrink it while drawing. Asking for a
                // new font size every frame makes Dalamud rebuild its fonts, and the text flickers.
                atlasSize = restSize * MathF.Max(1f, style.IntroScale);
                alpha *= introAlpha;

                // Older sticky texts sit one text-height further from the middle per slot, upward unless the box moves down.
                var away = style.Direction == Direction.Down ? 1f : -1f;
                y += away * line.ShownSlot * lineHeight - (drawSize - restSize) / 2f;
            }

            DrawLine(line.Content, area, font, atlasSize, drawSize, outline, fonts, drawList, origin, x, y, alpha);
        }
    }

    /// <summary>
    /// Size and see-through amount of a sticky text over its life. It starts big and zooms down to normal size with
    /// a small bounce, stays still, then grows a little while it fades out.
    /// </summary>
    private static (float Scale, float Alpha) Impact(float progress, float introScale)
    {
        if (progress < IntroEnd)
        {
            var u = progress / IntroEnd;
            var eased = 1f + BackOvershoot * MathF.Pow(u - 1f, 3f) + (BackOvershoot - 1f) * MathF.Pow(u - 1f, 2f);
            var scale = introScale + (1f - introScale) * eased;
            return (MathF.Max(scale, 0.5f), MathF.Min(1f, u * 4f));
        }

        if (progress > FadeStart)
        {
            var u = (progress - FadeStart) / (1f - FadeStart);
            return (1f + DissolveGrow * u, 1f);
        }

        return (1f, 1f);
    }

    /// <summary>
    /// Moves every text forward by the time since the last frame and removes the ones that are done. Then lets
    /// waiting texts in. Sticky texts come in straight away in the middle slot. Normal texts come in once the one
    /// before has moved far enough that they will not overlap.
    /// </summary>
    private static void Advance(Stream stream, TextStyle style, float lineHeight, float height, float deltaSeconds)
    {
        for (var i = stream.Lines.Count - 1; i >= 0; i--)
        {
            var line = stream.Lines[i];
            line.Progress += deltaSeconds / style.TravelSeconds;
            line.ShownSlot += (line.Slot - line.ShownSlot) * MathF.Min(1f, deltaSeconds * SlotEase);
            if (line.Progress >= 1f)
            {
                stream.Lines.RemoveAt(i);
            }
        }

        if (style.Animation == AnimationStyle.Static)
        {
            while (stream.Pending.Count > 0)
            {
                foreach (var line in stream.Lines)
                {
                    line.Slot += 1;
                }

                stream.Lines.RemoveAll(l => l.Slot >= MaxStatic);
                stream.Lines.Add(new Line { Content = stream.Pending.Dequeue(), Side = 1, Index = stream.Counter++ });
            }

            return;
        }

        while (stream.Pending.Count > 0 && (stream.Lines.Count == 0 || stream.Lines[^1].Progress >= Spacing(stream.Lines[^1], lineHeight, height)))
        {
            var index = stream.Counter++;
            var side = style.Alternate ? (index % 2 == 0 ? 1 : -1) : 1;
            stream.Lines.Add(new Line { Content = stream.Pending.Dequeue(), Side = side, Index = index });
        }
    }

    /// <summary>
    /// How far the last text must have moved before the next one may start, so they do not overlap. A text with a
    /// name shown under it needs more room.
    /// </summary>
    private static float Spacing(Line last, float lineHeight, float height)
    {
        var rows = last.Content.SubLine == null ? 1f : 1f + SubLineScale;
        return lineHeight * rows / height;
    }

    /// <summary>
    /// Draws one text: the icon, then each coloured piece of text, then the name underneath if there is one.
    /// The horizontal position comes from the animation, or from the box's alignment setting when the text moves
    /// straight up or down.
    /// </summary>
    private static void DrawLine(ComposedLine content, ScrollAreaConfig area, GameFontFamily family, float atlasSize, float drawSize, Outline outline, FontService fonts, ImDrawListPtr drawList, Vector2 origin, float? centerX, float y, float alpha)
    {
        var handle = fonts.Get(family, atlasSize);
        using var pushed = handle?.Push();

        var font = ImGui.GetFont();
        var scale = drawSize / ImGui.GetFontSize();

        var textWidth = 0f;
        foreach (var segment in content.Segments)
        {
            textWidth += ImGui.CalcTextSize(segment.Text).X * scale;
        }

        var iconSize = content.IconId != 0 && area.ShowIcons ? drawSize : 0f;
        var iconSpace = iconSize > 0 ? iconSize + IconGap : 0f;
        var totalWidth = textWidth + iconSpace;

        float x;
        if (centerX is { } center)
        {
            x = origin.X + center - totalWidth / 2f;
        }
        else
        {
            x = area.Align switch
            {
                TextAlign.Center => origin.X + (area.Width - totalWidth) / 2f,
                TextAlign.Right => origin.X + area.Width - totalWidth,
                _ => origin.X,
            };
        }

        var textX = area.IconSide == IconSide.Left ? x + iconSpace : x;
        var iconX = area.IconSide == IconSide.Left ? x : x + textWidth + IconGap;
        var top = origin.Y + y;

        if (iconSize > 0)
        {
            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(content.IconId)).GetWrapOrDefault();
            if (wrap != null)
            {
                var iconMin = new Vector2(iconX, top);
                var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha * area.IconAlpha));
                drawList.AddImage(wrap.Handle, iconMin, iconMin + new Vector2(iconSize, iconSize), Vector2.Zero, Vector2.One, tint);
            }
        }

        var cursor = textX;
        var outlineOffset = outline switch
        {
            Outline.Thin => MathF.Max(1f, drawSize / 20f),
            Outline.Thick => MathF.Max(2f, drawSize / 10f),
            _ => 0f,
        };
        var outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, alpha));

        foreach (var segment in content.Segments)
        {
            DrawSegment(drawList, font, drawSize, new Vector2(cursor, top), segment, alpha, outlineOffset, outlineColor);
            cursor += ImGui.CalcTextSize(segment.Text).X * scale;
        }

        if (content.SubLine is { } subLine)
        {
            var subSize = drawSize * SubLineScale;
            DrawSegment(drawList, font, subSize, new Vector2(textX, top + drawSize), subLine, alpha, outlineOffset * SubLineScale, outlineColor);
        }
    }

    /// <summary>
    /// Draws one coloured piece of text. The black outline is the same text drawn eight times underneath, each
    /// copy shifted a little in a different direction.
    /// </summary>
    private static void DrawSegment(ImDrawListPtr drawList, ImFontPtr font, float size, Vector2 position, Segment segment, float alpha, float outlineOffset, uint outlineColor)
    {
        if (outlineOffset > 0)
        {
            foreach (var dir in OutlineOffsets)
            {
                drawList.AddText(font, size, position + dir * outlineOffset, outlineColor, segment.Text);
            }
        }

        var color = segment.Color with { W = segment.Color.W * alpha };
        drawList.AddText(font, size, position, ImGui.ColorConvertFloat4ToU32(color), segment.Text);
    }

    private static readonly Vector2[] OutlineOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];
}
