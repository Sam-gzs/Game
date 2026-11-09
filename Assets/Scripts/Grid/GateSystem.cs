using UnityEngine;

/// <summary>
/// Links switches, pressure plates, gates and mirrors by channel number.
/// Buttons use odd/even parity; plates are "pressed while occupied" (logical OR).
/// </summary>
public static class GateSystem
{
    /// <summary>Press one button: flip it, then refresh the channel it belongs to.</summary>
    public static void Toggle(int channel, GridTile source)
    {
        if (source != null) source.SetSwitchState(!source.SwitchOn);

        SetChannel(channel, IsButtonChannelOpen(channel));
    }

    /// <summary>
    /// Open while the number of pressed switches on the channel is odd. A switch with
    /// <c>invert</c> set counts the other way round: unlit counts as pressed.
    /// </summary>
    static bool IsButtonChannelOpen(int channel)
    {
        int pressed = 0;
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null) continue;
            if (t.type != TileType.Switch && t.type != TileType.BallSwitch) continue;
            if (t.channel != channel) continue;
            if (t.SwitchOn ^ t.invert) pressed++;
        }

        return (pressed & 1) == 1;
    }

    /// <summary>
    /// Re-evaluate a switch channel without toggling a source. Used by a ball-hold
    /// switch, whose pressed state is driven by ball occupancy.
    /// </summary>
    public static void RefreshButtons(int channel)
    {
        SetChannel(channel, IsButtonChannelOpen(channel));
    }

    /// <summary>
    /// Level start: an inverted switch already counts as pressed on the first frame, so its
    /// channel has to be evaluated before play. Only channels that actually contain such a
    /// switch are touched, so every other level keeps the gate state its scene was saved with.
    /// </summary>
    public static void ApplyStartStates()
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        var channels = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || !t.invert) continue;
            if (t.type != TileType.Switch && t.type != TileType.BallSwitch) continue;
            channels.Add(t.channel);
        }

        foreach (int ch in channels) RefreshButtons(ch);
    }

    /// <summary>Plate channels: open while any plate on the channel is pressed.</summary>
    public static void RefreshPlates(int channel)
    {
        bool open = false;
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null) continue;
            if (t.type != TileType.Plate) continue;
            if (t.channel != channel) continue;
            if (t.PlatePressed) { open = true; break; }
        }

        SetChannel(channel, open);
    }

    /// <summary>Send a channel signal to every receiver on that channel.</summary>
    public static void SetChannel(int channel, bool open)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null) continue;
            if (t.channel != channel) continue;
            t.OnSignal(open);
        }
    }
}
