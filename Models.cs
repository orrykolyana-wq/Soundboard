using System;

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

    internal class ProfileData
    {
        public string? Name { get; set; }
        public byte? BasePaletteValue { get; set; }
        public List<ProfilePadAssignment> Pads { get; set; } = new();
    }

    internal class ProfilePadAssignment
    {
        public int Index { get; set; }
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
}