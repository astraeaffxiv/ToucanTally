using System;
using System.Numerics;

namespace ToucanTally.Rendering;

/// <summary>
/// Works out where a piece of combat text (for example "12,345 Heavy Swing") is drawn while it moves through its
/// box on screen. 
/// </summary>
internal static class Animations
{
    private const float SprinklerSpread = MathF.PI / 3f;
    private const int SprinklerSteps = 5;

    /// <summary>
    /// Returns where the text should be drawn right now, measured from the top-left corner of its box.
    /// <paramref name="progress"/> is 0 when the text appears and 1 when it disappears.
    /// <paramref name="side"/> is 1 or -1 and decides which way the curving styles bend, so texts can alternate.
    /// <paramref name="index"/> counts up by one for every new text and gives Sprinkler its fan-out angle.
    /// X is null for Straight up or down movement; then the box's left, centre or right alignment decides X.
    /// </summary>
    public static (float? X, float Y) Position(TextStyle style, float progress, int side, int index, float width, float height, float lineHeight)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        var forward = Forward(style.Direction);
        var sideways = new Vector2(-forward.Y, forward.X);
        var travel = MathF.Abs(forward.Y) > 0 ? height - lineHeight : width;
        var start = Start(style.Direction, width, height, lineHeight);

        switch (style.Animation)
        {
            case AnimationStyle.Straight:
            {
                // Moves in a straight line from one edge of the box to the other.
                var p = start + forward * travel * t;
                return (IsVertical(style.Direction) ? null : p.X, p.Y);
            }

            case AnimationStyle.Parabola:
            {
                // Moves forward while curving out to one side and back, like a thrown ball.
                var bulge = MathF.Sin(t * MathF.PI) * Perpendicular(style.Direction, width, height) / 2f;
                var p = start + forward * travel * t + sideways * bulge * side;
                return (p.X, p.Y);
            }

            case AnimationStyle.Angled:
            {
                // Moves forward while drifting steadily to one side.
                var drift = t * Perpendicular(style.Direction, width, height) / 2f;
                var p = start + forward * travel * t + sideways * drift * side;
                return (p.X, p.Y);
            }

            case AnimationStyle.Sprinkler:
            {
                // Every new text leaves at a slightly different angle, so they fan out like a garden sprinkler.
                var step = index % SprinklerSteps;
                var angle = -SprinklerSpread / 2f + SprinklerSpread * step / (SprinklerSteps - 1);
                var dir = Rotate(forward, angle);
                var p = start + dir * travel * t;
                return (p.X, p.Y);
            }

            default:
                // Static: stays in the middle of the box.
                return (width / 2f, (height - lineHeight) / 2f);
        }
    }

    /// <summary>The direction of travel as a one-step arrow. Up is (0, -1) because screen Y grows downward.</summary>
    private static Vector2 Forward(Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Vector2(0, -1),
            Direction.Down => new Vector2(0, 1),
            Direction.Left => new Vector2(-1, 0),
            _ => new Vector2(1, 0),
        };
    }

    /// <summary>Where a text first appears: the bottom edge when it moves up, the top edge when it moves down, and so on.</summary>
    private static Vector2 Start(Direction direction, float width, float height, float lineHeight)
    {
        return direction switch
        {
            Direction.Up => new Vector2(width / 2f, height - lineHeight),
            Direction.Down => new Vector2(width / 2f, 0),
            Direction.Left => new Vector2(width, (height - lineHeight) / 2f),
            _ => new Vector2(0, (height - lineHeight) / 2f),
        };
    }

    private static bool IsVertical(Direction direction)
    {
        return direction is Direction.Up or Direction.Down;
    }

    /// <summary>How much room the box has sideways to the direction of travel.</summary>
    private static float Perpendicular(Direction direction, float width, float height)
    {
        return IsVertical(direction) ? width : height;
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
