using System;
using System.Collections.Generic;
using System.Text;

namespace ToucanTally.Events;

/// <summary>
/// Keeps the last few hundred debug messages for the debug window, and can also copy them to Dalamud's log (/xllog).
/// It is off after every load, and messages are thrown away until you switch it on in the debug window.
/// The game hooks and the drawing code can write to it at the same time.
/// </summary>
internal sealed class EventLog
{
    private readonly Plugin plugin;
    private readonly LinkedList<string> lines = new();
    private readonly object gate = new();

    public EventLog(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public bool Enabled { get; set; }

    private Configuration configuration => plugin.Configuration;

    public void Add(string category, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}";
        lock (gate)
        {
            lines.AddLast(line);
            while (lines.Count > configuration.LogCapacity)
            {
                lines.RemoveFirst();
            }
        }

        if (configuration.LogToDalamud)
        {
            Plugin.Log.Information("[{Category}] {Message}", category, message);
        }
    }

    public string[] Snapshot()
    {
        lock (gate)
        {
            var copy = new string[lines.Count];
            lines.CopyTo(copy, 0);
            return copy;
        }
    }

    public string Dump()
    {
        var builder = new StringBuilder();
        foreach (var line in Snapshot())
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    public void Clear()
    {
        lock (gate)
        {
            lines.Clear();
        }
    }
}
