using System;
using System.Runtime.InteropServices;

namespace Soundboard
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIOUTCAPS
        {
            public ushort ManufacturerId;
            public ushort ProductId;
            public uint DriverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ProductName;

            public ushort Technology;
            public ushort Voices;
            public ushort Notes;
            public ushort ChannelMask;
            public uint Support;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIINCAPS
        {
            public ushort ManufacturerId;
            public ushort ProductId;
            public uint DriverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ProductName;

            public ushort Support;
        }

        [DllImport("winmm.dll", EntryPoint = "midiOutGetNumDevs")]
        public static extern uint midiOutGetNumDevs();

        [DllImport("winmm.dll", EntryPoint = "midiOutGetDevCapsW", CharSet = CharSet.Unicode)]
        public static extern uint midiOutGetDevCapsW(
            UIntPtr deviceId,
            out MIDIOUTCAPS capabilities,
            uint capabilitiesSize);

        [DllImport("winmm.dll", EntryPoint = "midiOutOpen")]
        public static extern uint midiOutOpen(
            out IntPtr midiDeviceHandle,
            UIntPtr deviceId,
            UIntPtr callback,
            UIntPtr instance,
            uint flags);

        [DllImport("winmm.dll", EntryPoint = "midiInGetNumDevs")]
        public static extern uint midiInGetNumDevs();

        [DllImport("winmm.dll", EntryPoint = "midiInGetDevCapsW", CharSet = CharSet.Unicode)]
        public static extern uint midiInGetDevCapsW(
            UIntPtr deviceId,
            out MIDIINCAPS capabilities,
            uint capabilitiesSize);

        [DllImport("winmm.dll", EntryPoint = "midiInOpen")]
        public static extern uint midiInOpen(
            out IntPtr midiDeviceHandle,
            UIntPtr deviceId,
            MidiInProc callback,
            IntPtr instance,
            uint flags);

        [DllImport("winmm.dll", EntryPoint = "midiInStart")]
        public static extern uint midiInStart(IntPtr midiDeviceHandle);

        [DllImport("winmm.dll", EntryPoint = "midiInStop")]
        public static extern uint midiInStop(IntPtr midiDeviceHandle);

        [DllImport("winmm.dll", EntryPoint = "midiInReset")]
        public static extern uint midiInReset(IntPtr midiDeviceHandle);

        [DllImport("winmm.dll", EntryPoint = "midiInClose")]
        public static extern uint midiInClose(IntPtr midiDeviceHandle);

        [DllImport("winmm.dll", EntryPoint = "midiOutShortMsg")]
        public static extern uint midiOutShortMsg(
            IntPtr midiDeviceHandle,
            uint message);

        [DllImport("winmm.dll", EntryPoint = "midiOutReset")]
        public static extern uint midiOutReset(IntPtr midiDeviceHandle);

        [DllImport("winmm.dll", EntryPoint = "midiOutClose")]
        public static extern uint midiOutClose(IntPtr midiDeviceHandle);

        public delegate void MidiInProc(
            IntPtr midiDeviceHandle,
            uint message,
            IntPtr instance,
            IntPtr parameter1,
            IntPtr parameter2);

        public const uint MIM_DATA = 0x3C3;
    }
}