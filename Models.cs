using System;
using System.Collections.Generic;

namespace Soundboard
{
    internal class PadPosition
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int PaletteValue { get; set; }
    }

    internal class PadAction
    {
        public string? SoundFilePath { get; set; }
        public string? DisplayName { get; set; }
        public byte? LightColorValue { get; set; }
    }

    internal class MidiDevice
    {
        public UIntPtr Id { get; set; }
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    internal class AudioSettingsData
    {
        public string ActiveMode { get; set; } = "TV";
        public Dictionary<string, string?> Assignments { get; set; } = new();
    }
}