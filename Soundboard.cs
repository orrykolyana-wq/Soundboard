using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LaunchpadLightTest
{
    public class MainForm : Form
    {
        // ============================================================
        // PROGRAM STATE
        // ============================================================

        private UIntPtr selectedMidiDeviceId = UIntPtr.Zero;
        private IntPtr midiHandle = IntPtr.Zero;
        private IntPtr midiInHandle = IntPtr.Zero;
        private GCHandle midiInCallbackHandle;
        private bool midiConnected = false;
        private bool midiInputOpen = false;
        private const uint MIM_DATA = 0x3C3;


        // ============================================================
        // USER INTERFACE
        // ============================================================

        private ComboBox midiDevices = null!;
        private Button connectButton = null!;

        private ComboBox paletteSelector = null!;
        private ListBox outputMenuListBox = null!;
        private Button addOutputButton = null!;
        private Button removeOutputButton = null!;
        private ComboBox audioModeSelector = null!;
        private Button audioSettingsButton = null!;
        private Button muteButton = null!;
        private Button activateLayoutButton = null!;
        private bool layoutActivated = false;
        private bool soundsMuted = false;
        private const byte DimWhiteLightValue = 1;
        private readonly Dictionary<int, byte> manualPadLights = new();
        private Button stopAllSoundsButton = null!;
        private string activeAudioMode = "TV";
        private readonly Dictionary<string, string?> audioModeAssignments = new()
        {
            ["Off"] = null,
            ["TV"] = null,
            ["Headset"] = null,
            ["Broadcast"] = null
        };
        private string AudioSettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ViciousMockery", "audio-settings.json");
	private Button allOffButton = null!;
	private Button allOnButton = null!;
	private Button rainbow1Button = null!;
	private Button rainbow2Button = null!;
        private Button assignSoundButton = null!;
        private Button clearAssignmentsButton = null!;

	private System.Windows.Forms.Timer paletteCycleTimer = null!;
	private int paletteCycleValue = 1;
	private bool rainbowPageTwo = false;

        private readonly Dictionary<int, PadAction> padActions = new();
        private string? pendingSoundFilePath = null;
        private string? pendingDisplayName = null;
        private byte? pendingLightColorValue = null;
        private string? selectedOutputDeviceName = null;
        private readonly List<string> audioOutputDevices = new();
        private readonly List<string> favoriteOutputDeviceNames = new();
        private readonly List<IWavePlayer> activeAudioPlayers = new();
        private readonly HashSet<string> activeSoundPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly object activeAudioPlayersLock = new();
        private int playbackGeneration = 0;
        private readonly Dictionary<string, byte> profileBasePaletteValues = new();

        // Launchpad MK2 palette RGB approximations for values 0–127.
        // Values are discrete hardware palette entries, not a continuous ramp.
        // RGB reference data: community-collected Launchpad palette table;
        // physical LED appearance can vary with brightness and camera/display.
        private static readonly (byte Value, Color Color)[] PadColorPalette =
        {
            (0, Color.FromArgb(0, 0, 0)),
            (1, Color.FromArgb(37, 37, 37)),
            (2, Color.FromArgb(143, 143, 143)),
            (3, Color.FromArgb(253, 253, 253)),
            (4, Color.FromArgb(255, 101, 92)),
            (5, Color.FromArgb(255, 40, 18)),
            (6, Color.FromArgb(110, 10, 3)),
            (7, Color.FromArgb(34, 1, 0)),
            (8, Color.FromArgb(255, 199, 124)),
            (9, Color.FromArgb(255, 108, 29)),
            (10, Color.FromArgb(110, 40, 6)),
            (11, Color.FromArgb(48, 31, 2)),
            (12, Color.FromArgb(255, 248, 77)),
            (13, Color.FromArgb(255, 248, 63)),
            (14, Color.FromArgb(108, 105, 21)),
            (15, Color.FromArgb(32, 31, 2)),
            (16, Color.FromArgb(148, 247, 81)),
            (17, Color.FromArgb(83, 246, 60)),
            (18, Color.FromArgb(30, 104, 20)),
            (19, Color.FromArgb(25, 51, 6)),
            (20, Color.FromArgb(69, 247, 81)),
            (21, Color.FromArgb(9, 246, 59)),
            (22, Color.FromArgb(2, 104, 19)),
            (23, Color.FromArgb(0, 30, 2)),
            (24, Color.FromArgb(67, 247, 104)),
            (25, Color.FromArgb(9, 246, 59)),
            (26, Color.FromArgb(2, 104, 19)),
            (27, Color.FromArgb(0, 30, 2)),
            (28, Color.FromArgb(64, 248, 151)),
            (29, Color.FromArgb(4, 247, 93)),
            (30, Color.FromArgb(1, 104, 34)),
            (31, Color.FromArgb(0, 36, 19)),
            (32, Color.FromArgb(57, 248, 193)),
            (33, Color.FromArgb(0, 247, 167)),
            (34, Color.FromArgb(0, 105, 67)),
            (35, Color.FromArgb(0, 31, 19)),
            (36, Color.FromArgb(63, 204, 252)),
            (37, Color.FromArgb(0, 184, 252)),
            (38, Color.FromArgb(0, 82, 99)),
            (39, Color.FromArgb(0, 20, 31)),
            (40, Color.FromArgb(73, 158, 251)),
            (41, Color.FromArgb(0, 112, 250)),
            (42, Color.FromArgb(0, 41, 107)),
            (43, Color.FromArgb(0, 7, 32)),
            (44, Color.FromArgb(79, 105, 250)),
            (45, Color.FromArgb(0, 60, 249)),
            (46, Color.FromArgb(0, 20, 108)),
            (47, Color.FromArgb(0, 2, 32)),
            (48, Color.FromArgb(146, 106, 250)),
            (49, Color.FromArgb(91, 61, 249)),
            (50, Color.FromArgb(23, 24, 119)),
            (51, Color.FromArgb(8, 8, 64)),
            (52, Color.FromArgb(255, 112, 250)),
            (53, Color.FromArgb(255, 71, 250)),
            (54, Color.FromArgb(109, 25, 107)),
            (55, Color.FromArgb(33, 3, 32)),
            (56, Color.FromArgb(255, 105, 149)),
            (57, Color.FromArgb(255, 44, 101)),
            (58, Color.FromArgb(110, 13, 36)),
            (59, Color.FromArgb(44, 2, 19)),
            (60, Color.FromArgb(255, 51, 19)),
            (61, Color.FromArgb(173, 71, 16)),
            (62, Color.FromArgb(142, 99, 21)),
            (63, Color.FromArgb(80, 116, 23)),
            (64, Color.FromArgb(1, 70, 10)),
            (65, Color.FromArgb(0, 101, 67)),
            (66, Color.FromArgb(0, 103, 143)),
            (67, Color.FromArgb(0, 60, 249)),
            (68, Color.FromArgb(0, 85, 95)),
            (69, Color.FromArgb(10, 50, 214)),
            (70, Color.FromArgb(143, 143, 143)),
            (71, Color.FromArgb(43, 43, 43)),
            (72, Color.FromArgb(255, 40, 18)),
            (73, Color.FromArgb(200, 247, 62)),
            (74, Color.FromArgb(186, 235, 58)),
            (75, Color.FromArgb(107, 247, 60)),
            (76, Color.FromArgb(3, 149, 32)),
            (77, Color.FromArgb(0, 247, 148)),
            (78, Color.FromArgb(0, 184, 252)),
            (79, Color.FromArgb(0, 70, 249)),
            (80, Color.FromArgb(56, 61, 249)),
            (81, Color.FromArgb(134, 63, 249)),
            (82, Color.FromArgb(194, 52, 142)),
            (83, Color.FromArgb(83, 43, 5)),
            (84, Color.FromArgb(255, 97, 26)),
            (85, Color.FromArgb(151, 225, 55)),
            (86, Color.FromArgb(122, 247, 60)),
            (87, Color.FromArgb(9, 246, 59)),
            (88, Color.FromArgb(9, 246, 59)),
            (89, Color.FromArgb(87, 247, 127)),
            (90, Color.FromArgb(0, 249, 213)),
            (91, Color.FromArgb(92, 158, 251)),
            (92, Color.FromArgb(40, 104, 207)),
            (93, Color.FromArgb(145, 147, 237)),
            (94, Color.FromArgb(218, 70, 250)),
            (95, Color.FromArgb(255, 45, 108)),
            (96, Color.FromArgb(255, 144, 37)),
            (97, Color.FromArgb(199, 187, 45)),
            (98, Color.FromArgb(158, 247, 61)),
            (99, Color.FromArgb(150, 111, 24)),
            (100, Color.FromArgb(74, 52, 6)),
            (101, Color.FromArgb(16, 92, 19)),
            (102, Color.FromArgb(0, 97, 72)),
            (103, Color.FromArgb(23, 26, 53)),
            (104, Color.FromArgb(13, 47, 107)),
            (105, Color.FromArgb(126, 77, 35)),
            (106, Color.FromArgb(188, 25, 10)),
            (107, Color.FromArgb(233, 103, 73)),
            (108, Color.FromArgb(229, 125, 31)),
            (109, Color.FromArgb(255, 227, 58)),
            (110, Color.FromArgb(171, 225, 55)),
            (111, Color.FromArgb(116, 189, 44)),
            (112, Color.FromArgb(35, 38, 63)),
            (113, Color.FromArgb(229, 249, 117)),
            (114, Color.FromArgb(136, 249, 199)),
            (115, Color.FromArgb(164, 173, 252)),
            (116, Color.FromArgb(154, 128, 250)),
            (117, Color.FromArgb(81, 81, 81)),
            (118, Color.FromArgb(135, 135, 135)),
            (119, Color.FromArgb(228, 252, 253)),
            (120, Color.FromArgb(181, 24, 9)),
            (121, Color.FromArgb(69, 3, 1)),
            (122, Color.FromArgb(6, 211, 50)),
            (123, Color.FromArgb(1, 79, 12)),
            (124, Color.FromArgb(199, 187, 45)),
            (125, Color.FromArgb(79, 62, 9)),
            (126, Color.FromArgb(195, 112, 26)),
            (127, Color.FromArgb(93, 28, 3))
        };

        private Label statusLabel = null!;
        private Label lastSoundLabel = null!;

        private TableLayoutPanel padGrid = null!;
        private readonly ToolTip padButtonToolTip = new();
        private int selectedPadIndex = 0;
        private Label selectedPadNumberLabel = null!;
        private TextBox selectedPadNameTextBox = null!;
        private Button selectedPadEnabledButton = null!;
        private Button selectedPadSoundButton = null!;
        private Label selectedPadSoundNameLabel = null!;
        private Button selectedPadColourPreview = null!;
        private TableLayoutPanel selectedPadPalettePanel = null!;
        private bool updatingEditor = false;
        private readonly HashSet<int> disabledPads = new();


        // ============================================================
        // WINDOWS MIDI API
        // ============================================================

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        private struct MIDIOUTCAPS
        {
            public ushort manufacturerId;
            public ushort productId;
            public uint driverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string productName;

            public ushort technology;
            public ushort voices;
            public ushort notes;
            public ushort channelMask;

            public uint support;
        }


        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        private struct MIDIINCAPS
        {
            public ushort manufacturerId;
            public ushort productId;
            public uint driverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string productName;

            public uint support;
        }


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutGetNumDevs")]
        private static extern uint midiOutGetNumDevs();


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutGetDevCapsW",
            CharSet = CharSet.Unicode)]
        private static extern uint midiOutGetDevCapsW(
            UIntPtr deviceId,
            out MIDIOUTCAPS capabilities,
            uint capabilitiesSize);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutOpen")]
        private static extern uint midiOutOpen(
            out IntPtr midiDeviceHandle,
            UIntPtr deviceId,
            UIntPtr callback,
            UIntPtr instance,
            uint flags);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInGetNumDevs")]
        private static extern uint midiInGetNumDevs();


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInGetDevCapsW",
            CharSet = CharSet.Unicode)]
        private static extern uint midiInGetDevCapsW(
            UIntPtr deviceId,
            out MIDIINCAPS capabilities,
            uint capabilitiesSize);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInOpen")]
        private static extern uint midiInOpen(
            out IntPtr midiDeviceHandle,
            UIntPtr deviceId,
            MidiInProc callback,
            IntPtr instance,
            uint flags);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInStart")]
        private static extern uint midiInStart(
            IntPtr midiDeviceHandle);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInStop")]
        private static extern uint midiInStop(
            IntPtr midiDeviceHandle);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInReset")]
        private static extern uint midiInReset(
            IntPtr midiDeviceHandle);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiInClose")]
        private static extern uint midiInClose(
            IntPtr midiDeviceHandle);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutShortMsg")]
        private static extern uint midiOutShortMsg(
            IntPtr midiDeviceHandle,
            uint message);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutReset")]
        private static extern uint midiOutReset(
            IntPtr midiDeviceHandle);


        [DllImport(
            "winmm.dll",
            EntryPoint = "midiOutClose")]
        private static extern uint midiOutClose(
            IntPtr midiDeviceHandle);


        private delegate void MidiInProc(
            IntPtr midiDeviceHandle,
            uint message,
            IntPtr instance,
            IntPtr parameter1,
            IntPtr parameter2);


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public MainForm()
        {
            LoadAudioSettings();
            BuildWindow();
            BuildControls();
            InitializePaletteCycle();
            FindMidiDevices();

            // Automatically connect to the Launchpad when it is available.
            // FindMidiDevices selects the Launchpad output when one is present.
            if (midiDevices.SelectedItem is MidiDevice device
                && device.Name.Contains(
                    "Launchpad",
                    StringComparison.OrdinalIgnoreCase))
            {
                ConnectButton_Click(this, EventArgs.Empty);
            }
        }


        // ============================================================
        // MAIN WINDOW
        // ============================================================

        private void BuildWindow()
        {
            Text = "Vicious Mockery";

            Width = 1580;
            Height = 900;

            MinimumSize = new Size(1280, 720);

            StartPosition =
                FormStartPosition.CenterScreen;

            FormClosing += MainForm_FormClosing;
        }


        // ============================================================
        // USER INTERFACE
        // ============================================================

        private void BuildControls()
        {
            // Three-part workspace: device controls, pad board, selected-pad editor.
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(25, 25, 25)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(mainLayout);

            // LEFT SIDEBAR
            TableLayoutPanel leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 12,
                Padding = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // heading
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // midi device
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // connection + settings
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // audio heading
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // audio mode
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // outputs heading
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // output list
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // add/remove
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // stop all
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // now playing heading
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // now playing info
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));  // status
            mainLayout.Controls.Add(leftLayout, 0, 0);

            Label controlsHeading = new Label
            {
                Text = "VICIOUS MOCKERY",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            leftLayout.Controls.Add(controlsHeading, 0, 0);

            midiDevices = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 0, 3)
            };
            leftLayout.Controls.Add(midiDevices, 0, 1);

            FlowLayoutPanel connectionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 3, 0, 3)
            };
            connectButton = new Button
            {
                Text = "CONNECT",
                Width = 174,
                Height = 34,
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            connectButton.FlatAppearance.BorderSize = 0;
            connectButton.Click += ConnectButton_Click;
            audioSettingsButton = new Button
            {
                Text = "AUDIO",
                Width = 66,
                Height = 34,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                BackColor = Color.FromArgb(48, 48, 52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0),
                AccessibleName = "Audio Settings",
                UseVisualStyleBackColor = false
            };
            audioSettingsButton.FlatAppearance.BorderColor = Color.Silver;
            audioSettingsButton.FlatAppearance.BorderSize = 1;
            audioSettingsButton.Click += AudioSettingsButton_Click;
            connectionRow.Controls.Add(connectButton);
            connectionRow.Controls.Add(audioSettingsButton);
            leftLayout.Controls.Add(connectionRow, 0, 2);

            Label audioHeading = new Label
            {
                Text = "AUDIO MODE",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gainsboro
            };
            leftLayout.Controls.Add(audioHeading, 0, 3);

            audioModeSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 0, 3)
            };
            audioModeSelector.Items.AddRange(new object[] { "Off", "TV", "Headset", "Broadcast" });
            audioModeSelector.SelectedItem = activeAudioMode;
            audioModeSelector.SelectedIndexChanged += (_, _) =>
            {
                if (audioModeSelector.SelectedItem is string mode)
                {
                    activeAudioMode = mode;
                    selectedOutputDeviceName = mode == "Off" ? null : audioModeAssignments[mode];
                    if (mode != "Off" && string.IsNullOrWhiteSpace(selectedOutputDeviceName))
                        statusLabel.Text = $"{mode} has no audio output assigned yet. Open Audio Settings.";
                    else
                        statusLabel.Text = mode == "Off" ? "Audio output off." : $"Audio mode: {mode}";
                    SaveAudioSettings();
                }
            };
            leftLayout.Controls.Add(audioModeSelector, 0, 4);

            Label outputsHeading = new Label
            {
                Text = "FAVOURITE OUTPUTS",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gainsboro
            };
            leftLayout.Controls.Add(outputsHeading, 0, 5);

            outputMenuListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
                Margin = new Padding(0, 4, 0, 4)
            };
            outputMenuListBox.SelectedIndexChanged += (_, _) =>
            {
                if (outputMenuListBox.SelectedItem is string selectedDeviceName
                    && !selectedDeviceName.StartsWith("No outputs", StringComparison.OrdinalIgnoreCase))
                {
                    selectedOutputDeviceName = selectedDeviceName;
                    if (activeAudioMode != "Off")
                    {
                        audioModeAssignments[activeAudioMode] = selectedDeviceName;
                        SaveAudioSettings();
                    }
                }
            };
            leftLayout.Controls.Add(outputMenuListBox, 0, 6);

            FlowLayoutPanel outputButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 2, 0, 0)
            };
            addOutputButton = new Button
            {
                Text = "Add Output",
                Width = 130,
                Height = 29,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            addOutputButton.FlatAppearance.BorderColor = Color.Silver;
            addOutputButton.Click += AddOutputButton_Click;
            removeOutputButton = new Button
            {
                Text = "Remove",
                Width = 100,
                Height = 29,
                Margin = new Padding(0),
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            removeOutputButton.FlatAppearance.BorderColor = Color.Silver;
            removeOutputButton.Click += RemoveOutputButton_Click;
            outputButtons.Controls.Add(addOutputButton);
            outputButtons.Controls.Add(removeOutputButton);
            leftLayout.Controls.Add(outputButtons, 0, 7);

            stopAllSoundsButton = new Button
            {
                Text = "STOP ALL SOUNDS",
                Dock = DockStyle.Fill,
                BackColor = Color.Firebrick,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 5, 0, 5)
            };
            stopAllSoundsButton.FlatAppearance.BorderSize = 0;
            stopAllSoundsButton.Click += (_, _) => StopAllSounds();
            leftLayout.Controls.Add(stopAllSoundsButton, 0, 8);

            Label nowPlayingHeading = new Label
            {
                Text = "ACTIVITY",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gainsboro
            };
            leftLayout.Controls.Add(nowPlayingHeading, 0, 9);

            lastSoundLabel = new Label
            {
                Text = "Last sound: —",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            leftLayout.Controls.Add(lastSoundLabel, 0, 10);

            statusLabel = new Label
            {
                Text = "Not connected.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                Padding = new Padding(8, 7, 8, 7),
                Margin = new Padding(0, 6, 0, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                BackColor = Color.FromArgb(243, 243, 243),
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            leftLayout.Controls.Add(statusLabel, 0, 11);

            // RIGHT SIDE: board title/action row and the 8x8 pad grid.
            TableLayoutPanel boardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            boardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            boardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            boardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.Controls.Add(boardLayout, 1, 0);

            FlowLayoutPanel boardToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(0)
            };
            Label boardHeading = new Label
            {
                Text = "PAD BOARD",
                AutoSize = false,
                Width = 180,
                Height = 38,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 12, 0)
            };
            assignSoundButton = new Button
            {
                Text = "Assign Sound",
                Width = 140,
                Height = 34,
                Margin = new Padding(0, 2, 8, 0),
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            assignSoundButton.FlatAppearance.BorderColor = Color.Silver;
            assignSoundButton.Click += AssignSoundButton_Click;
            clearAssignmentsButton = new Button
            {
                Text = "Clear Assignments",
                Width = 145,
                Height = 34,
                Margin = new Padding(0, 2, 0, 0),
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            clearAssignmentsButton.FlatAppearance.BorderColor = Color.Silver;
            clearAssignmentsButton.Click += ClearAssignmentsButton_Click;
            muteButton = new Button
            {
                Text = "SOUND ON",
                Width = 100,
                Height = 34,
                Margin = new Padding(0, 2, 8, 0),
                BackColor = Color.ForestGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            muteButton.FlatAppearance.BorderColor = Color.Silver;
            muteButton.FlatAppearance.BorderSize = 1;
            muteButton.Click += (_, _) =>
            {
                soundsMuted = !soundsMuted;
                muteButton.Text = soundsMuted ? "MUTED" : "SOUND ON";
                muteButton.BackColor = soundsMuted ? Color.Firebrick : Color.ForestGreen;
                statusLabel.Text = soundsMuted ? "Sound playback muted; assignments are preserved." : "Sound playback enabled.";
            };
            boardToolbar.Controls.Add(boardHeading);
            activateLayoutButton = new Button
            {
                Text = "ACTIVATE",
                Width = 112,
                Height = 34,
                Margin = new Padding(0, 2, 8, 0),
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                Enabled = false,
                AccessibleName = "Apply saved pad colours to the Launchpad"
            };
            activateLayoutButton.FlatAppearance.BorderColor = Color.Silver;
            activateLayoutButton.Click += ActivateLayoutButton_Click;
            boardToolbar.Controls.Add(activateLayoutButton);
            boardToolbar.Controls.Add(muteButton);
            boardToolbar.Controls.Add(assignSoundButton);
            boardToolbar.Controls.Add(clearAssignmentsButton);
            boardLayout.Controls.Add(boardToolbar, 0, 0);

            padGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 8,
                Margin = new Padding(0),
                Padding = new Padding(2)
            };
            for (int column = 0; column < 8; column++)
                padGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
            for (int row = 0; row < 8; row++)
                padGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5f));
            boardLayout.Controls.Add(padGrid, 0, 1);

            // Keep the hidden palette selector as an internal default-colour source.
            paletteSelector = new ComboBox { Visible = false, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int value = 0; value <= 127; value++) paletteSelector.Items.Add(value.ToString());
            paletteSelector.SelectedIndex = Random.Shared.Next(34, 38);
            Controls.Add(paletteSelector);

            allOffButton = new Button { Text = "ALL OFF", Visible = false };
            allOnButton = new Button { Text = "ALL ON", Visible = false };
            rainbow1Button = new Button { Text = "🌈 1", Visible = false };
            rainbow2Button = new Button { Text = "🌈 2", Visible = false };
            rainbow1Button.Click += Rainbow1Button_Click;
            rainbow2Button.Click += Rainbow2Button_Click;
            allOffButton.Click += AllOffButton_Click;
            allOnButton.Click += AllOnButton_Click;

            PopulateAudioOutputDevices();
            ApplyActiveAudioMode();

            // Create the 64 virtual pads. MIDI note mapping remains unchanged.
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Button padButton = new Button
                    {
                        Text = ((row * 8) + column + 1).ToString(),
                        Dock = DockStyle.Fill,
                        Margin = new Padding(3),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10, FontStyle.Bold),
                        BackColor = Color.FromArgb(31, 31, 31),
                        ForeColor = Color.FromArgb(78, 78, 78),
                        FlatStyle = FlatStyle.Flat,
                        UseVisualStyleBackColor = false,
                        Tag = new PadPosition { Row = row, Column = column, PaletteValue = (row * 8) + column }
                    };
                    padButton.FlatAppearance.BorderColor = Color.Gainsboro;
                    padButton.FlatAppearance.BorderSize = 2;
                    padButton.FlatAppearance.MouseOverBackColor = padButton.BackColor;
                    padButton.FlatAppearance.MouseDownBackColor = padButton.BackColor;
                    padButton.Click += PadButton_Click;
                    padButton.MouseDown += PadButton_MouseDown;
                    padGrid.Controls.Add(padButton, column, row);
                }
            }

            BuildSelectedPadEditor(mainLayout);
            RefreshSelectedPadEditor();
        }


        private void BuildSelectedPadEditor(TableLayoutPanel mainLayout)
        {
            Panel frame = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(34, 34, 34),
                Padding = new Padding(10),
                Margin = new Padding(8, 0, 0, 0)
            };
            mainLayout.Controls.Add(frame, 2, 0);

            TableLayoutPanel editor = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 11,
                BackColor = Color.FromArgb(34, 34, 34),
                Padding = new Padding(0)
            };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // heading
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // selected pad number
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // name heading
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // name field
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // enable toggle
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // sound heading
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // sound control
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // sound name
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // clear selected pad
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // colour heading
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // colour and palette
            frame.Controls.Add(editor);

            Label heading = new Label
            {
                Text = "SELECTED PAD",
                Dock = DockStyle.Fill,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            editor.Controls.Add(heading, 0, 0);

            selectedPadNumberLabel = new Label
            {
                Text = "1",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 15),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 34, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 2, 0, 5)
            };
            editor.Controls.Add(selectedPadNumberLabel, 0, 1);

            editor.Controls.Add(MakeEditorHeading("PAD NAME"), 0, 2);
            selectedPadNameTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 0, 5)
            };
            selectedPadNameTextBox.TextChanged += SelectedPadNameTextBox_TextChanged;
            editor.Controls.Add(selectedPadNameTextBox, 0, 3);

            selectedPadEnabledButton = new Button
            {
                Text = "PAD ENABLED   ● ON",
                Dock = DockStyle.Fill,
                BackColor = Color.ForestGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 3, 0, 5),
                UseVisualStyleBackColor = false
            };
            selectedPadEnabledButton.FlatAppearance.BorderColor = Color.Silver;
            selectedPadEnabledButton.Click += SelectedPadEnabledButton_Click;
            editor.Controls.Add(selectedPadEnabledButton, 0, 4);

            editor.Controls.Add(MakeEditorHeading("SOUND"), 0, 5);
            selectedPadSoundButton = new Button
            {
                Text = "◖))    ASSIGN SOUND",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4),
                UseVisualStyleBackColor = false
            };
            selectedPadSoundButton.FlatAppearance.BorderColor = Color.Silver;
            selectedPadSoundButton.Click += SelectedPadSoundButton_Click;
            editor.Controls.Add(selectedPadSoundButton, 0, 6);

            selectedPadSoundNameLabel = new Label
            {
                Text = "(No sound assigned)",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.Silver,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(7, 0, 4, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            editor.Controls.Add(selectedPadSoundNameLabel, 0, 7);

            Button clearSelectedPadButton = new Button
            {
                Text = "⊘   CLEAR PAD / TURN LIGHT OFF",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(82, 28, 28),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4),
                UseVisualStyleBackColor = false
            };
            clearSelectedPadButton.FlatAppearance.BorderColor = Color.FromArgb(160, 90, 90);
            clearSelectedPadButton.Click += (_, _) => ClearSelectedPad();
            editor.Controls.Add(clearSelectedPadButton, 0, 8);

            editor.Controls.Add(MakeEditorHeading("PAD COLOUR"), 0, 9);

            TableLayoutPanel colourLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(34, 34, 34),
                Padding = new Padding(0)
            };
            colourLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            colourLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            colourLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            colourLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editor.Controls.Add(colourLayout, 0, 10);

            selectedPadColourPreview = new Button
            {
                Text = "CURRENT COLOUR  ·  CLICK TO CHOOSE",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 37, 37),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4),
                UseVisualStyleBackColor = false
            };
            selectedPadColourPreview.FlatAppearance.BorderColor = Color.Silver;
            selectedPadColourPreview.Click += (_, _) =>
            {
                if (selectedPadPalettePanel != null)
                    selectedPadPalettePanel.Focus();
                statusLabel.Text = "Choose a hardware colour from the palette below.";
            };
            colourLayout.Controls.Add(selectedPadColourPreview, 0, 0);

            Label paletteLabel = new Label
            {
                Text = "COLOUR PALETTE  /  128 HARDWARE VALUES",
                Dock = DockStyle.Fill,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            colourLayout.Controls.Add(paletteLabel, 0, 1);

            Panel paletteScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(18, 18, 18),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(3)
            };
            colourLayout.Controls.Add(paletteScroll, 0, 2);

            selectedPadPalettePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 8,
                RowCount = 16,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(18, 18, 18),
                Margin = new Padding(0),
                Padding = new Padding(0),
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            for (int c = 0; c < 8; c++)
                selectedPadPalettePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 33));
            for (int r = 0; r < 16; r++)
                selectedPadPalettePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            paletteScroll.Controls.Add(selectedPadPalettePanel);

            foreach (var item in PadColorPalette)
            {
                byte paletteValue = item.Value;
                Color swatchColour = item.Color;
                Button swatch = new Button
                {
                    Text = paletteValue.ToString(),
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2),
                    BackColor = swatchColour,
                    ForeColor = GetReadableTextColor(swatchColour),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    UseVisualStyleBackColor = false,
                    Tag = paletteValue,
                    AccessibleName = $"Hardware colour {paletteValue}"
                };
                swatch.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                swatch.Click += PaletteSwatch_Click;
                selectedPadPalettePanel.Controls.Add(swatch, paletteValue % 8, paletteValue / 8);
                padButtonToolTip.SetToolTip(swatch, $"Palette value {paletteValue}");
            }
        }

        private Label MakeEditorHeading(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void RefreshSelectedPadEditor()
        {
            if (selectedPadNumberLabel == null) return;
            updatingEditor = true;
            selectedPadNumberLabel.Text = (selectedPadIndex + 1).ToString();
            padActions.TryGetValue(selectedPadIndex, out PadAction? action);
            selectedPadNameTextBox.Text = action?.DisplayName ?? string.Empty;
            bool enabled = !disabledPads.Contains(selectedPadIndex);
            selectedPadEnabledButton.Text = enabled ? "PAD ENABLED     ● ON" : "PAD DISABLED     ○ OFF";
            selectedPadEnabledButton.BackColor = enabled ? Color.ForestGreen : Color.Firebrick;
            selectedPadSoundNameLabel.Text = action != null && !string.IsNullOrWhiteSpace(action.SoundFilePath)
                ? (action.DisplayName ?? Path.GetFileNameWithoutExtension(action.SoundFilePath))
                : "(No sound assigned)";
            byte colourValue = action?.LightColorValue ?? (manualPadLights.ContainsKey(selectedPadIndex) ? manualPadLights[selectedPadIndex] : DimWhiteLightValue);
            Color colour = action?.LightColorValue.HasValue == true ? GetColorForPaletteValue(colourValue)
                : (manualPadLights.ContainsKey(selectedPadIndex) ? Color.FromArgb(125, 125, 125) : Color.FromArgb(37, 37, 37));
            selectedPadColourPreview.BackColor = colour;
            selectedPadColourPreview.ForeColor = GetReadableTextColor(colour);
            selectedPadColourPreview.Text = $"CURRENT COLOUR  ·  VALUE {colourValue}";
            updatingEditor = false;
        }

        private void SelectedPadNameTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (updatingEditor) return;
            if (!padActions.TryGetValue(selectedPadIndex, out PadAction? action))
            {
                action = new PadAction { LightColorValue = null };
                padActions[selectedPadIndex] = action;
            }
            action.DisplayName = string.IsNullOrWhiteSpace(selectedPadNameTextBox.Text)
                ? null : selectedPadNameTextBox.Text.Trim();
            UpdateSelectedPadButtonVisual();
            selectedPadSoundNameLabel.Text = !string.IsNullOrWhiteSpace(action.SoundFilePath)
                ? (action.DisplayName ?? Path.GetFileNameWithoutExtension(action.SoundFilePath))
                : "(No sound assigned)";
        }

        private void SelectedPadEnabledButton_Click(object? sender, EventArgs e)
        {
            int row = selectedPadIndex / 8;
            int column = selectedPadIndex % 8;
            if (disabledPads.Contains(selectedPadIndex))
            {
                disabledPads.Remove(selectedPadIndex);
                byte value = padActions.TryGetValue(selectedPadIndex, out PadAction? action) && action.LightColorValue.HasValue
                    ? action.LightColorValue.Value : DimWhiteLightValue;
                manualPadLights[selectedPadIndex] = value;
                if (midiConnected) SendPad(row, column, value);
            }
            else
            {
                disabledPads.Add(selectedPadIndex);
                manualPadLights.Remove(selectedPadIndex);
                if (midiConnected) SendPad(row, column, 0);
            }
            UpdateSelectedPadButtonVisual();
            RefreshSelectedPadEditor();
        }

        private void SelectedPadSoundButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Audio files (*.wav;*.mp3;*.aiff;*.wma)|*.wav;*.mp3;*.aiff;*.wma|All files (*.*)|*.*",
                Title = $"Choose a sound for pad {selectedPadIndex + 1}"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            string defaultName = Path.GetFileNameWithoutExtension(dialog.FileName);
            padActions.TryGetValue(selectedPadIndex, out PadAction? existing);
            var prompt = PromptForDisplayName(defaultName, existing?.LightColorValue ?? GetSelectedPaletteValue());
            if (prompt == null) return;

            padActions[selectedPadIndex] = new PadAction
            {
                SoundFilePath = dialog.FileName,
                DisplayName = string.IsNullOrWhiteSpace(prompt.Value.Name) ? defaultName : prompt.Value.Name.Trim(),
                LightColorValue = prompt.Value.SelectedColorValue
            };
            UpdateSelectedPadButtonVisual();
            RefreshSelectedPadEditor();
            statusLabel.Text = $"Sound assigned to pad {selectedPadIndex + 1}.";
        }

        private void PaletteSwatch_Click(object? sender, EventArgs e)
        {
            if (sender is not Button swatch || swatch.Tag is not byte colourValue) return;
            if (!padActions.TryGetValue(selectedPadIndex, out PadAction? action))
            {
                action = new PadAction();
                padActions[selectedPadIndex] = action;
            }
            action.LightColorValue = colourValue;
            manualPadLights.Remove(selectedPadIndex);
            UpdateSelectedPadButtonVisual();
            RefreshSelectedPadEditor();
            layoutActivated = false;
            UpdateActivateLayoutButtonAppearance();
            statusLabel.Text = $"Pad {selectedPadIndex + 1} colour set to palette value {colourValue}. Press ACTIVATE to apply it.";
        }

        private void ClearSelectedPad()
        {
            padActions.Remove(selectedPadIndex);
            manualPadLights.Remove(selectedPadIndex);
            disabledPads.Remove(selectedPadIndex);
            if (midiConnected) SendPad(selectedPadIndex / 8, selectedPadIndex % 8, 0);
            UpdateSelectedPadButtonVisual();
            RefreshSelectedPadEditor();
            layoutActivated = false;
            UpdateActivateLayoutButtonAppearance();
            statusLabel.Text = $"Pad {selectedPadIndex + 1} cleared and light turned off.";
        }

        private void UpdateSelectedPadButtonVisual()
        {
            foreach (Control control in padGrid.Controls)
            {
                if (control is Button button && button.Tag is PadPosition position
                    && position.Row * 8 + position.Column == selectedPadIndex)
                {
                    UpdatePadButtonVisualState(button, selectedPadIndex);
                    break;
                }
            }
        }


// ============================================================
// FIND MIDI DEVICES
// ============================================================

private void FindMidiDevices()
{
    midiDevices.Items.Clear();

    uint deviceCount =
        midiOutGetNumDevs();

    if (deviceCount == 0)
    {
        statusLabel.Text =
            "Windows reports no MIDI outputs.";

        return;
    }

    for (
        uint deviceId = 0;
        deviceId < deviceCount;
        deviceId++)
    {
        MIDIOUTCAPS capabilities;

        uint result =
            midiOutGetDevCapsW(
                new UIntPtr(deviceId),
                out capabilities,
                (uint)Marshal.SizeOf<MIDIOUTCAPS>());

        if (result == 0)
        {
            string name =
                capabilities.productName?
                .Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                name =
                    $"Unnamed MIDI Output {deviceId}";
            }

            midiDevices.Items.Add(
                new MidiDevice
                {
                    Id =
                        new UIntPtr(deviceId),

                    Name =
                        name
                });
        }
        else
        {
            midiDevices.Items.Add(
                new MidiDevice
                {
                    Id =
                        new UIntPtr(deviceId),

                    Name =
                        $"MIDI Output {deviceId} " +
                        $"(GetCaps error 0x{result:X8})"
                });
        }
    }

    if (midiDevices.Items.Count > 0)
    {
        midiDevices.SelectedIndex = 0;

        for (int i = 0; i < midiDevices.Items.Count; i++)
        {
            if (midiDevices.Items[i] is not MidiDevice device)
            {
                continue;
            }

            if (device.Name.Contains(
                    "Launchpad",
                    StringComparison.OrdinalIgnoreCase))
            {
                midiDevices.SelectedIndex = i;
                break;
            }
        }
    }
}

        // ============================================================
        // CONNECT
        // ============================================================

        private void ConnectButton_Click(
            object? sender,
            EventArgs e)
        {
            if (midiConnected)
            {
                CloseMidiDevice();
                return;
            }

            if (midiDevices.SelectedItem == null)
            {
                MessageBox.Show(
                    "Please select a MIDI output first.",
                    "No MIDI Device",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }


            CloseMidiDevice();


            MidiDevice device =
                (MidiDevice)midiDevices.SelectedItem;


            IntPtr newHandle;


            uint result =
                midiOutOpen(
                    out newHandle,
                    device.Id,
                    UIntPtr.Zero,
                    UIntPtr.Zero,
                    0);


            if (result != 0)
            {
                statusLabel.Text =
                    $"Could not open {device.Name}.";


                MessageBox.Show(
                    $"Windows could not open this MIDI device.\n\n" +
                    $"Device: {device.Name}\n" +
                    $"Device ID: {device.Id}\n" +
                    $"Error code: 0x{result:X8}",
                    "MIDI Open Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }


            midiHandle =
                newHandle;

            selectedMidiDeviceId =
                device.Id;

            // Input is best-effort: if the Launchpad input channel can't be opened,
            // stay connected on the out channel (lights still work) and warn that
            // physical pads won't respond.
            midiInputOpen =
                OpenMidiInput(device.Id, device.Name);

            midiConnected =
                true;

            UpdateConnectionToggleAppearance();


            // Connection is separate from activation. Start with subdued white LEDs;
            // ACTIVATE applies the saved pad colours when the user is ready.
            layoutActivated = false;
            UpdateActivateLayoutButtonAppearance();
            ShowAllPadsDimWhite();
            ApplyProfileVisualState();

            if (midiInputOpen)
            {
                statusLabel.Text =
                    $"Connected: {device.Name}. Press ACTIVATE to apply saved pad colours.";
            }
            else
            {
                statusLabel.Text =
                    $"Connected: {device.Name} (out only — no Launchpad input detected; " +
                    "physical pads won't respond). Press ACTIVATE to apply saved pad colours.";
            }
        }


        private void ActivateLayoutButton_Click(object? sender, EventArgs e)
        {
            if (!midiConnected)
            {
                statusLabel.Text = "Connect to the Launchpad before activating the layout.";
                return;
            }

            // Apply each saved pad colour without triggering sound playback.
            // Unassigned pads stay at the quiet dim-white startup level.
            for (int padIndex = 0; padIndex < 64; padIndex++)
            {
                if (disabledPads.Contains(padIndex))
                {
                    SendPad(padIndex / 8, padIndex % 8, 0);
                    continue;
                }

                byte colour = DimWhiteLightValue;
                if (padActions.TryGetValue(padIndex, out PadAction? action)
                    && action.LightColorValue.HasValue)
                {
                    colour = action.LightColorValue.Value;
                }

                SendPad(padIndex / 8, padIndex % 8, colour);
            }

            layoutActivated = true;
            UpdateActivateLayoutButtonAppearance();
            statusLabel.Text = "Layout activated. Saved pad colours sent to the Launchpad.";
        }

        private void UpdateActivateLayoutButtonAppearance()
        {
            if (activateLayoutButton == null) return;

            activateLayoutButton.Enabled = midiConnected;
            activateLayoutButton.Text = layoutActivated ? "ACTIVATED" : "ACTIVATE";
            activateLayoutButton.BackColor = layoutActivated
                ? Color.ForestGreen
                : Color.FromArgb(58, 58, 62);
            activateLayoutButton.ForeColor = Color.White;
            activateLayoutButton.FlatAppearance.BorderColor = layoutActivated
                ? Color.LightGreen
                : Color.Silver;
        }


        // ============================================================
        // CLOSE MIDI DEVICE
        // ============================================================

        private void CloseMidiDevice()
        {
            if (!midiConnected)
            {
                return;
            }


            TurnEverythingOff();


            // The input channel only exists if it opened successfully; an out-only
            // connection must not stop/reset/close a handle that was never opened.
            if (midiInHandle != IntPtr.Zero)
            {
                midiInStop(
                    midiInHandle);

                midiInReset(
                    midiInHandle);

                midiInClose(
                    midiInHandle);

                midiInHandle =
                    IntPtr.Zero;

                if (midiInCallbackHandle.IsAllocated)
                {
                    midiInCallbackHandle.Free();
                }
            }

            midiInputOpen =
                false;


            midiOutReset(
                midiHandle);


            midiOutClose(
                midiHandle);


            midiHandle =
                IntPtr.Zero;

            midiConnected =
                false;
            layoutActivated = false;
            UpdateActivateLayoutButtonAppearance();

            UpdateConnectionToggleAppearance();

            statusLabel.Text =
                "Disconnected.";
        }


        private void MidiSelectorButton_Click(
            object? sender,
            EventArgs e)
        {
            using ContextMenuStrip menu =
                new ContextMenuStrip();

            foreach (MidiDevice device in midiDevices.Items)
            {
                ToolStripMenuItem item =
                    new ToolStripMenuItem(device.Name);

                item.Tag = device;

                item.Click += (_, _) =>
                {
                    midiDevices.SelectedItem = device;

                    if (midiConnected)
                    {
                        CloseMidiDevice();
                        ConnectButton_Click(
                            this,
                            EventArgs.Empty);
                    }
                };

                menu.Items.Add(item);
            }

            menu.Show(
                midiDevices,
                new Point(0, midiDevices.Height));
        }


        private void UpdateConnectionToggleAppearance()
        {
            // A readable rectangular connection toggle, rather than a tiny unlabeled circle.
            connectButton.Text = midiConnected ? "CONNECTED" : "CONNECT";
            connectButton.BackColor = midiConnected ? Color.ForestGreen : Color.DimGray;
            connectButton.ForeColor = Color.White;
            connectButton.FlatStyle = FlatStyle.Flat;
            connectButton.FlatAppearance.BorderSize = 0;
            connectButton.FlatAppearance.BorderColor = midiConnected ? Color.ForestGreen : Color.DimGray;
            connectButton.FlatAppearance.MouseOverBackColor = midiConnected ? Color.ForestGreen : Color.Gray;
            connectButton.FlatAppearance.MouseDownBackColor = midiConnected ? Color.ForestGreen : Color.Gray;
            connectButton.Region = null;
        }


        // ============================================================
        // PC PAD CLICK
        // ============================================================

        private void PadButton_MouseDown(
            object? sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            if (sender is not Button clickedButton)
            {
                return;
            }

            if (clickedButton.Tag is not PadPosition position)
            {
                return;
            }

            int padIndex =
                (position.Row * 8) + position.Column;

            if (padActions.TryGetValue(
                    padIndex,
                    out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(
                    assignedAction.SoundFilePath))
            {
                string defaultName =
                    !string.IsNullOrWhiteSpace(
                        assignedAction.DisplayName)
                    ? assignedAction.DisplayName
                    : Path.GetFileNameWithoutExtension(
                        assignedAction.SoundFilePath);

                (string? Name, byte? SelectedColorValue)? promptResult =
                    PromptForDisplayName(
                        defaultName,
                        assignedAction.LightColorValue ?? GetSelectedPaletteValue());

                if (promptResult == null)
                {
                    statusLabel.Text =
                        "Pad rename cancelled.";

                    return;
                }

                assignedAction.DisplayName =
                    string.IsNullOrWhiteSpace(promptResult.Value.Name)
                    ? defaultName
                    : promptResult.Value.Name.Trim();

                assignedAction.LightColorValue =
                    promptResult.Value.SelectedColorValue;

                UpdatePadButtonVisualState(
                    clickedButton,
                    padIndex);

                if (midiConnected && promptResult.Value.SelectedColorValue.HasValue)
                {
                    SendPad(
                        position.Row,
                        position.Column,
                        promptResult.Value.SelectedColorValue.Value);
                }

                statusLabel.Text =
                    $"Pad {padIndex + 1} renamed.";

                return;
            }

            statusLabel.Text =
                $"Pad {padIndex + 1} has no sound assigned.";
        }


        private void PadButton_Click(
            object? sender,
            EventArgs e)
        {
            if (sender is not Button clickedButton)
            {
                return;
            }


            if (clickedButton.Tag is not PadPosition position)
            {
                return;
            }

            int padIndex =
                (position.Row * 8) + position.Column;

            selectedPadIndex = padIndex;
            RefreshSelectedPadEditor();

            if (disabledPads.Contains(padIndex))
            {
                if (midiConnected) SendPad(position.Row, position.Column, 0);
                statusLabel.Text = $"Pad {padIndex + 1} is disabled.";
                UpdatePadButtonVisualState(clickedButton, padIndex);
                return;
            }

            if (rainbowPageTwo && position.PaletteValue == 63)
            {
                if (paletteCycleTimer.Enabled)
                {
                    StopPaletteCycle();
                }
                else
                {
                    StartPaletteCycle();
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingSoundFilePath))
            {
                padActions[padIndex] =
                    new PadAction
                    {
                        SoundFilePath = pendingSoundFilePath,
                        DisplayName = pendingDisplayName,
                        LightColorValue = pendingLightColorValue
                    };

                pendingSoundFilePath = null;
                pendingDisplayName = null;
                pendingLightColorValue = null;

                UpdatePadButtonVisualState(
                    clickedButton,
                    padIndex);

                statusLabel.Text =
                    $"Sound assigned to pad {padIndex + 1}.";

                return;
            }

            if (padActions.TryGetValue(
                    padIndex,
                    out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                byte velocity = assignedAction.LightColorValue ?? DimWhiteLightValue;
                SendPad(position.Row, position.Column, velocity);
                UpdatePadButtonVisualState(clickedButton, padIndex);
                RefreshSelectedPadEditor();
                PlayPadSound(assignedAction.SoundFilePath!);
                if (lastSoundLabel != null)
                    lastSoundLabel.Text = $"Last sound: {assignedAction.DisplayName ?? Path.GetFileNameWithoutExtension(assignedAction.SoundFilePath)}";
                statusLabel.Text = soundsMuted
                    ? $"Pad {padIndex + 1} light shown; sound is muted."
                    : $"Playing sound for pad {padIndex + 1}.";
                return;
            }

            // Unassigned pads are light-test controls: toggle between dim white and off.
            bool currentlyLit = manualPadLights.ContainsKey(padIndex);
            if (currentlyLit)
            {
                manualPadLights.Remove(padIndex);
                SendPad(position.Row, position.Column, 0);
                UpdatePadButtonVisualState(clickedButton, padIndex);
                RefreshSelectedPadEditor();
                statusLabel.Text = $"Pad {padIndex + 1} light turned off.";
            }
            else
            {
                byte testColour = padActions.TryGetValue(padIndex, out PadAction? colourAction)
                    && colourAction.LightColorValue.HasValue
                    ? colourAction.LightColorValue.Value : DimWhiteLightValue;
                manualPadLights[padIndex] = testColour;
                SendPad(position.Row, position.Column, testColour);
                UpdatePadButtonVisualState(clickedButton, padIndex);
                RefreshSelectedPadEditor();
                statusLabel.Text = $"Pad {padIndex + 1} dim white light turned on.";
            }
        }


        // ============================================================
        // GET SELECTED PALETTE VALUE
        // ============================================================

        private byte GetSelectedPaletteValue()
        {
            if (paletteSelector.SelectedItem == null)
            {
                return 0;
            }


            if (byte.TryParse(
                paletteSelector.SelectedItem.ToString(),
                out byte value))
            {
                return value;
            }


            return 0;
        }


        private void AssignSoundButton_Click(
            object? sender,
            EventArgs e)
        {
            using OpenFileDialog dialog =
                new OpenFileDialog();

            dialog.Filter =
                "WAV files (*.wav)|*.wav|All files (*.*)|*.*";

            dialog.Title =
                "Choose a sound for the next pad you click";

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text =
                    "Sound assignment cancelled.";

                return;
            }

            string defaultName =
                Path.GetFileNameWithoutExtension(
                    dialog.FileName);

            (string? Name, byte? SelectedColorValue)? promptResult =
                PromptForDisplayName(
                    defaultName,
                    pendingLightColorValue ?? GetSelectedPaletteValue());

            if (promptResult == null)
            {
                pendingSoundFilePath = null;
                pendingDisplayName = null;
                pendingLightColorValue = null;

                statusLabel.Text =
                    "Sound assignment cancelled.";

                return;
            }

            pendingSoundFilePath =
                dialog.FileName;

            pendingDisplayName =
                string.IsNullOrWhiteSpace(promptResult.Value.Name)
                ? defaultName
                : promptResult.Value.Name.Trim();

            pendingLightColorValue =
                promptResult.Value.SelectedColorValue;

            statusLabel.Text =
                "Choose a Launchpad pad to assign that sound to.";
        }


        private (string? Name, byte? SelectedColorValue)? PromptForDisplayName(
            string defaultName,
            byte? initialLightColorValue)
        {
            byte? selectedColorValue =
                initialLightColorValue ?? GetSelectedPaletteValue();

            using Form promptForm =
                new Form();

            promptForm.Text =
                "Name this sound";

            promptForm.StartPosition =
                FormStartPosition.CenterParent;

            promptForm.FormBorderStyle =
                FormBorderStyle.FixedDialog;

            promptForm.MinimizeBox =
                false;

            promptForm.MaximizeBox =
                false;

            // Build the dialog with docked layout containers instead of fixed
            // coordinates. WinForms DPI autoscaling can otherwise scale child
            // positions without leaving enough room for the bottom buttons.
            promptForm.AutoScaleMode = AutoScaleMode.Dpi;
            promptForm.ClientSize = new Size(400, 230);
            promptForm.MinimumSize = new Size(420, 280);
            promptForm.Padding = new Padding(12);

            TableLayoutPanel promptLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            promptLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            Label promptLabel = new Label
            {
                Text = "What would you like to name this?",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };

            TableLayoutPanel nameRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            nameRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            TextBox textBox = new TextBox
            {
                Text = defaultName,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 6, 8, 0)
            };

            Button colorSwatchButton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Width = 30,
                Height = 30,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0),
                CausesValidation = false
            };
            colorSwatchButton.FlatAppearance.BorderColor = Color.Black;
            colorSwatchButton.FlatAppearance.BorderSize = 1;

            GraphicsPath swatchPath =
                new GraphicsPath();

            swatchPath.AddEllipse(
                0,
                0,
                colorSwatchButton.Width,
                colorSwatchButton.Height);

            colorSwatchButton.Region =
                new Region(swatchPath);

            colorSwatchButton.BackColor =
                GetColorForPaletteValue(
                    selectedColorValue.Value);

            colorSwatchButton.CausesValidation =
                false;

            colorSwatchButton.Click +=
                (_, _) =>
                {
                    byte? newColorValue =
                        ShowColorPalettePicker(
                            selectedColorValue ?? GetSelectedPaletteValue());

                    if (!newColorValue.HasValue)
                    {
                        return;
                    }

                    selectedColorValue =
                        newColorValue.Value;

                    colorSwatchButton.BackColor =
                        GetColorForPaletteValue(
                            selectedColorValue.Value);
                };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Margin = new Padding(6, 4, 6, 4)
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Margin = new Padding(6, 4, 6, 4)
            };

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            buttonRow.Controls.Add(cancelButton);
            buttonRow.Controls.Add(okButton);

            nameRow.Controls.Add(textBox, 0, 0);
            nameRow.Controls.Add(colorSwatchButton, 1, 0);
            promptLayout.Controls.Add(promptLabel, 0, 0);
            promptLayout.Controls.Add(nameRow, 0, 1);
            promptLayout.Controls.Add(buttonRow, 0, 2);
            promptForm.Controls.Add(promptLayout);

            promptForm.AcceptButton = okButton;
            promptForm.CancelButton = cancelButton;

            DialogResult result =
                promptForm.ShowDialog(this);

            if (result != DialogResult.OK)
            {
                return null;
            }

            return (
                textBox.Text,
                selectedColorValue);
        }


        private byte? ShowColorPalettePicker(byte currentValue)
        {
            byte? selectedValue = null;

            using Form paletteForm =
                new Form();

            paletteForm.Text =
                "FULL MK2 PALETTE — 128 VALUES (0–127)";

            paletteForm.StartPosition =
                FormStartPosition.CenterParent;

            paletteForm.FormBorderStyle =
                FormBorderStyle.FixedDialog;

            paletteForm.MinimizeBox =
                false;

            paletteForm.MaximizeBox =
                false;

            paletteForm.ClientSize =
                new Size(600, 760);

            paletteForm.MinimumSize =
                new Size(620, 800);

            TableLayoutPanel paletteGrid =
                new TableLayoutPanel();

            paletteGrid.Dock =
                DockStyle.Fill;

            paletteGrid.ColumnCount = 8;
            paletteGrid.RowCount = 16;
            paletteGrid.Padding =
                new Padding(10);

            for (int index = 0; index < 8; index++)
            {
                paletteGrid.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 12.5f));
            }

            for (int index = 0; index < 16; index++)
            {
                paletteGrid.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 6.25f));
            }

            for (int index = 0; index < PadColorPalette.Length; index++)
            {
                (byte value, Color color) =
                    PadColorPalette[index];

                Button colorButton =
                    new Button();

                colorButton.FlatStyle =
                    FlatStyle.Flat;

                colorButton.FlatAppearance.BorderColor =
                    currentValue == value
                    ? Color.Black
                    : Color.DarkGray;

                colorButton.FlatAppearance.BorderSize =
                    currentValue == value
                    ? 3
                    : 1;

                colorButton.BackColor =
                    color;

                colorButton.ForeColor =
                    Color.White;

                colorButton.Margin =
                    new Padding(3);

                colorButton.Dock =
                    DockStyle.Fill;

                colorButton.AutoSize =
                    false;

                colorButton.TextAlign =
                    ContentAlignment.MiddleCenter;

                colorButton.UseCompatibleTextRendering =
                    true;

                colorButton.Font =
                    new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

                colorButton.Text =
                    value.ToString();

                colorButton.Click +=
                    (_, _) =>
                    {
                        selectedValue =
                            value;

                        paletteForm.DialogResult =
                            DialogResult.OK;

                        paletteForm.Close();
                    };

                int column =
                    index % 8;

                int row =
                    index / 8;

                paletteGrid.Controls.Add(
                    colorButton,
                    column,
                    row);
            }

            paletteForm.Controls.Add(
                paletteGrid);

            return
                paletteForm.ShowDialog(this) == DialogResult.OK
                ? selectedValue
                : null;
        }


        private static Color GetColorForPaletteValue(byte value)
        {
            (byte Value, Color Color) closest =
                PadColorPalette[0];

            foreach ((byte paletteValue, Color paletteColor) in PadColorPalette)
            {
                if (Math.Abs(paletteValue - value) < Math.Abs(closest.Value - value))
                {
                    closest =
                        (paletteValue, paletteColor);
                }
            }

            return closest.Color;
        }


        private static Color GetReadableTextColor(Color background)
        {
            // Choose a readable label colour for both bright and dark pad colours.
            double luminance = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B);
            return luminance >= 145 ? Color.Black : Color.White;
        }


        private void ApplyProfileVisualState()
        {
            foreach (Control control in padGrid.Controls)
            {
                if (control is not Button button
                    || control.Tag is not PadPosition position)
                {
                    continue;
                }

                int padIndex =
                    (position.Row * 8) + position.Column;

                UpdatePadButtonVisualState(
                    button,
                    padIndex);
            }
        }


        private void ClearAssignmentsButton_Click(
            object? sender,
            EventArgs e)
        {
            padActions.Clear();
            manualPadLights.Clear();
            pendingSoundFilePath = null;
            ApplyProfileVisualState();

            statusLabel.Text =
                "All pad assignments cleared.";
        }


        private void UpdatePadButtonVisualState(
            Button button,
            int padIndex)
        {
            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && (!string.IsNullOrWhiteSpace(assignedAction.SoundFilePath)
                    || !string.IsNullOrWhiteSpace(assignedAction.DisplayName)
                    || assignedAction.LightColorValue.HasValue))
            {
                string displayName = !string.IsNullOrWhiteSpace(assignedAction.DisplayName)
                    ? assignedAction.DisplayName!
                    : (!string.IsNullOrWhiteSpace(assignedAction.SoundFilePath)
                        ? Path.GetFileNameWithoutExtension(assignedAction.SoundFilePath)
                        : string.Empty);
                if (displayName.Length > 12) displayName = displayName.Substring(0, 12) + "...";
                button.Text = string.IsNullOrWhiteSpace(displayName)
                    ? (padIndex + 1).ToString()
                    : $"{padIndex + 1}\n{displayName}";

                bool isLit = !disabledPads.Contains(padIndex)
                    && (assignedAction.LightColorValue.HasValue || manualPadLights.ContainsKey(padIndex)
                        || !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath));
                if (isLit)
                {
                    byte assignedColorValue = assignedAction.LightColorValue ?? DimWhiteLightValue;
                    button.BackColor = GetColorForPaletteValue(assignedColorValue);
                    button.ForeColor = GetReadableTextColor(button.BackColor);
                }
                else
                {
                    button.BackColor = Color.FromArgb(31, 31, 31);
                    button.ForeColor = Color.FromArgb(92, 92, 92);
                }
                button.FlatAppearance.MouseOverBackColor = button.BackColor;
                button.FlatAppearance.MouseDownBackColor = button.BackColor;
                padButtonToolTip.SetToolTip(button, assignedAction.SoundFilePath ?? assignedAction.DisplayName ?? string.Empty);
                return;
            }

            button.Text =
                (padIndex + 1).ToString();

            if (manualPadLights.ContainsKey(padIndex) && !disabledPads.Contains(padIndex))
            {
                // Use a neutral, visible grey in the virtual UI to represent
                // the Launchpad's subdued white test light. The hardware's
                // low-velocity palette value is not a reliable RGB match.
                button.BackColor = Color.FromArgb(125, 125, 125);
                button.ForeColor = Color.FromArgb(20, 20, 20);
            }
            else
            {
                // Unused pads stay visually inert until first interacted with.
                button.BackColor = Color.FromArgb(31, 31, 31);
                button.ForeColor = Color.FromArgb(92, 92, 92);
            }

            // Keep WinForms hover, press and focus styling from making the
            // number appear lit when the pad's light state is actually off.
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.FlatAppearance.MouseDownBackColor = button.BackColor;

            padButtonToolTip.SetToolTip(
                button,
                string.Empty);
        }


        private bool OpenMidiInput(UIntPtr deviceId, string deviceName)
        {
            if (TryOpenMidiInput(deviceId))
            {
                statusLabel.Text =
                    $"Connected: {deviceName} (listening for Launchpad MIDI input)";

                return true;
            }

            uint inputDeviceCount =
                midiInGetNumDevs();

            bool foundLaunchpadInput = false;

            for (uint i = 0; i < inputDeviceCount; i++)
            {
                MIDIINCAPS capabilities;

                uint result =
                    midiInGetDevCapsW(
                        new UIntPtr(i),
                        out capabilities,
                        (uint)Marshal.SizeOf<MIDIINCAPS>());

                if (result != 0)
                {
                    continue;
                }

                string inputName =
                    capabilities.productName?
                    .Trim()
                    ?? "";

                if (!IsLaunchpadDeviceName(inputName))
                {
                    continue;
                }

                foundLaunchpadInput = true;

                if (TryOpenMidiInput(new UIntPtr(i)))
                {
                    statusLabel.Text =
                        $"Connected: {deviceName} (listening for {inputName})";

                    return true;
                }
            }

            if (foundLaunchpadInput)
            {
                statusLabel.Text =
                    $"Connected: {deviceName} (Launchpad input was detected, but Windows refused to open it)";
            }
            else
            {
                statusLabel.Text =
                    $"Connected: {deviceName} (no Launchpad MIDI input device was reported by Windows)";
            }

            return false;
        }


        private static bool IsLaunchpadDeviceName(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            return deviceName.Contains(
                "Launchpad",
                StringComparison.OrdinalIgnoreCase);
        }


        private bool TryOpenMidiInput(UIntPtr deviceId)
        {
            midiInCallbackHandle =
                GCHandle.Alloc(this);

            uint result =
                midiInOpen(
                    out midiInHandle,
                    deviceId,
                    HandleMidiInCallback,
                    GCHandle.ToIntPtr(midiInCallbackHandle),
                    0x00090000);   // CALLBACK_FUNCTION: call HandleMidiInCallback directly

            if (result != 0)
            {
                if (midiInCallbackHandle.IsAllocated)
                {
                    midiInCallbackHandle.Free();
                }

                midiInHandle =
                    IntPtr.Zero;

                return false;
            }

            result =
                midiInStart(midiInHandle);

            if (result != 0)
            {
                midiInClose(midiInHandle);
                midiInHandle = IntPtr.Zero;

                if (midiInCallbackHandle.IsAllocated)
                {
                    midiInCallbackHandle.Free();
                }

                return false;
            }

            return true;
        }


        private static void HandleMidiInCallback(
            IntPtr midiDeviceHandle,
            uint message,
            IntPtr instance,
            IntPtr parameter1,
            IntPtr parameter2)
        {
            if (instance == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle =
                GCHandle.FromIntPtr(instance);

            if (handle.Target is MainForm form)
            {
                if (message == MIM_DATA)
                {
                    form.ProcessMidiInput(parameter1);
                }
            }
        }


        private void ProcessMidiInput(IntPtr parameter1)
        {
            uint rawMessage =
                unchecked((uint)parameter1.ToInt64());

            byte status =
                (byte)(rawMessage & 0xFF);

            byte note =
                (byte)((rawMessage >> 8) & 0xFF);

            byte velocity =
                (byte)((rawMessage >> 16) & 0xFF);

            // Only react to MIDI note-on messages with non-zero velocity.
            if ((status & 0xF0) != 0x90 || velocity == 0)
            {
                return;
            }

            int padIndex = GetPadIndexFromMidiNote(note);
            if (padIndex < 0)
            {
                return;
            }

            // MIDI input arrives on a native callback thread. Marshal UI and
            // Launchpad feedback work onto the WinForms UI thread.
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() => HandlePhysicalPadPressed(padIndex)));
            }
            catch (InvalidOperationException)
            {
                // The form is closing; ignore late MIDI messages.
            }
        }


        private void HandlePhysicalPadPressed(int padIndex)
        {
            if (IsDisposed || padIndex < 0 || padIndex >= 64)
            {
                return;
            }

            selectedPadIndex = padIndex;
            RefreshSelectedPadEditor();
            Button? button = null;
            foreach (Control control in padGrid.Controls)
            {
                if (control is Button candidate
                    && candidate.Tag is PadPosition position
                    && (position.Row * 8 + position.Column) == padIndex)
                {
                    button = candidate;
                    break;
                }
            }

            if (disabledPads.Contains(padIndex))
            {
                SendPad(padIndex / 8, padIndex % 8, 0);
                statusLabel.Text = $"Pad {padIndex + 1} is disabled.";
                return;
            }

            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                byte assignedColor = assignedAction.LightColorValue ?? DimWhiteLightValue;
                int row = padIndex / 8;
                int column = padIndex % 8;

                // A physical press immediately restores the pad's assigned colour.
                SendPad(row, column, assignedColor);
                if (button != null)
                {
                    UpdatePadButtonVisualState(button, padIndex);
                }

                PlayPadSound(assignedAction.SoundFilePath!);
                if (lastSoundLabel != null)
                {
                    string name = !string.IsNullOrWhiteSpace(assignedAction.DisplayName)
                        ? assignedAction.DisplayName!
                        : Path.GetFileNameWithoutExtension(assignedAction.SoundFilePath);
                    lastSoundLabel.Text = $"Last sound: {name}";
                }

                statusLabel.Text = soundsMuted
                    ? $"Pad {padIndex + 1} colour activated; sound is muted."
                    : $"Playing sound for pad {padIndex + 1}.";
                return;
            }

            // Colour-only pads light in their chosen colour but do not play audio.
            if (padActions.TryGetValue(padIndex, out PadAction? colourOnlyAction)
                && colourOnlyAction.LightColorValue.HasValue)
            {
                byte chosenColour = colourOnlyAction.LightColorValue.Value;
                manualPadLights[padIndex] = chosenColour;
                SendPad(padIndex / 8, padIndex % 8, chosenColour);
                if (button != null) UpdatePadButtonVisualState(button, padIndex);
                RefreshSelectedPadEditor();
                statusLabel.Text = $"Pad {padIndex + 1} colour activated; no sound assigned.";
                return;
            }

            // Unassigned physical pads use the subdued white test light.
            manualPadLights[padIndex] = DimWhiteLightValue;
            SendPad(padIndex / 8, padIndex % 8, DimWhiteLightValue);
            if (button != null)
            {
                UpdatePadButtonVisualState(button, padIndex);
            }
            statusLabel.Text = $"Pad {padIndex + 1} dim white light activated.";
        }


        private int GetPadIndexFromMidiNote(byte note)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    byte expectedNote =
                        (byte)(81 + column - (row * 10));

                    if (expectedNote == note)
                    {
                        return (row * 8) + column;
                    }
                }
            }

            return -1;
        }


        private void PlayPadSound(string soundFilePath)
        {
            if (soundsMuted)
                return;

            if (activeAudioMode == "Off")
                return;

            if (!File.Exists(soundFilePath))
            {
                SetStatusText($"Sound file not found: {soundFilePath}");
                return;
            }

            int generation;
            lock (activeAudioPlayersLock)
            {
                // Ignore repeat presses for a sound that is already playing.
                // This prevents rapid pad presses from building a playback queue.
                if (activeSoundPaths.Contains(soundFilePath))
                    return;

                activeSoundPaths.Add(soundFilePath);
                generation = playbackGeneration;
            }

            // Playback runs off the UI/MIDI callback thread, so the Stop button
            // remains responsive even when pads are pressed rapidly.
            _ = Task.Run(() => PlayPadSoundWorker(soundFilePath, generation));
        }

        private void PlayPadSoundWorker(string soundFilePath, int generation)
        {
            AudioFileReader? reader = null;
            IWavePlayer? player = null;
            MMDevice? selectedOutputDevice = null;
            using ManualResetEventSlim playbackFinished = new(false);

            try
            {
                reader = new AudioFileReader(soundFilePath);

                string? outputName = selectedOutputDeviceName;
                if (!string.IsNullOrWhiteSpace(outputName))
                {
                    using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                    foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(
                        DataFlow.Render,
                        DeviceState.All))
                    {
                        if (device.FriendlyName == outputName)
                        {
                            selectedOutputDevice = device;
                            break;
                        }
                        device.Dispose();
                    }
                }

                if (selectedOutputDevice != null)
                {
                    player = new WasapiOut(
                        selectedOutputDevice,
                        AudioClientShareMode.Shared,
                        false,
                        100);
                }
                else
                {
                    player = new WaveOutEvent();
                }

                player.PlaybackStopped += (_, _) => playbackFinished.Set();
                player.Init(reader);

                lock (activeAudioPlayersLock)
                {
                    // Stop All may have been pressed while the player was being
                    // prepared. Do not let that sound start after the stop.
                    if (generation != playbackGeneration)
                        return;

                    activeAudioPlayers.Add(player);
                }

                player.Play();
                playbackFinished.Wait();
            }
            catch (Exception ex)
            {
                SetStatusText($"Could not play sound: {ex.Message}");
            }
            finally
            {
                if (player != null)
                {
                    lock (activeAudioPlayersLock)
                    {
                        activeAudioPlayers.Remove(player);
                        activeSoundPaths.Remove(soundFilePath);
                    }
                    try { player.Stop(); } catch { }
                    player.Dispose();
                }
                else
                {
                    lock (activeAudioPlayersLock)
                        activeSoundPaths.Remove(soundFilePath);
                }

                reader?.Dispose();
                selectedOutputDevice?.Dispose();
            }
        }

        private void StopAllSounds()
        {
            IWavePlayer[] playersToStop;
            lock (activeAudioPlayersLock)
            {
                // Invalidate any player still being prepared and clear duplicate
                // suppression so the next intentional press can play normally.
                playbackGeneration++;
                activeSoundPaths.Clear();
                playersToStop = activeAudioPlayers.ToArray();
            }

            int stoppedCount = 0;
            foreach (IWavePlayer player in playersToStop)
            {
                try
                {
                    player.Stop();
                    stoppedCount++;
                }
                catch
                {
                    // Continue stopping any other active players.
                }
            }

            SetStatusText(stoppedCount == 0
                ? "No sounds are currently playing."
                : $"Stopped {stoppedCount} active sound(s).");
        }

        private void SetStatusText(string message)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && !Disposing)
                            statusLabel.Text = message;
                    }));
                }
                catch (InvalidOperationException) { }
            }
            else
            {
                statusLabel.Text = message;
            }
        }

        private void LoadAudioSettings()
        {
            try
            {
                if (!File.Exists(AudioSettingsPath)) return;
                var settings = JsonSerializer.Deserialize<AudioSettingsData>(File.ReadAllText(AudioSettingsPath));
                if (settings == null) return;
                foreach (string mode in new[] { "Off", "TV", "Headset", "Broadcast" })
                    if (settings.Assignments.TryGetValue(mode, out string? deviceName))
                        audioModeAssignments[mode] = deviceName;
                if (settings.ActiveMode is string modeName && audioModeAssignments.ContainsKey(modeName))
                    activeAudioMode = modeName;
            }
            catch { /* Ignore invalid settings and start with defaults. */ }
        }

        private void SaveAudioSettings()
        {
            try
            {
                string? directory = Path.GetDirectoryName(AudioSettingsPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var settings = new AudioSettingsData
                {
                    ActiveMode = activeAudioMode,
                    Assignments = new Dictionary<string, string?>(audioModeAssignments)
                };
                File.WriteAllText(AudioSettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { statusLabel.Text = $"Could not save audio settings: {ex.Message}"; }
        }

        private void ApplyActiveAudioMode()
        {
            selectedOutputDeviceName = activeAudioMode == "Off" ? null : audioModeAssignments[activeAudioMode];
        }

        private void AudioSettingsButton_Click(object? sender, EventArgs e)
        {
            using Form dialog = new Form
            {
                Text = "Audio Settings",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(560, 330),
                MinimumSize = new Size(500, 330)
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            var choices = new Dictionary<string, ComboBox>();
            string[] modes = { "TV", "Headset", "Broadcast" };
            List<string> devices = new List<string>();
            using (var enumerator = new MMDeviceEnumerator())
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    if (!devices.Any(n => string.Equals(n, device.FriendlyName, StringComparison.OrdinalIgnoreCase))) devices.Add(device.FriendlyName);
            devices.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string mode in modes)
            {
                layout.Controls.Add(new Label { Text = mode, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, Array.IndexOf(modes, mode));
                var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.Add("(Not assigned)");
                foreach (string device in devices) combo.Items.Add(device);
                string? current = audioModeAssignments[mode];
                combo.SelectedItem = current != null && combo.Items.Contains(current) ? current : "(Not assigned)";
                choices[mode] = combo;
                layout.Controls.Add(combo, 1, Array.IndexOf(modes, mode));
            }
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0) };
            var ok = new Button { Text = "OK", Width = 90, Height = 30, DialogResult = DialogResult.OK, Margin = new Padding(4, 0, 0, 0) };
            var cancel = new Button { Text = "Cancel", Width = 90, Height = 30, DialogResult = DialogResult.Cancel, Margin = new Padding(4, 0, 0, 0) };
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 4); layout.SetColumnSpan(buttons, 2);
            dialog.Controls.Add(layout); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            foreach (string mode in modes)
            {
                string? chosen = choices[mode].SelectedItem as string;
                audioModeAssignments[mode] = chosen == "(Not assigned)" ? null : chosen;
            }
            ApplyActiveAudioMode();
            SaveAudioSettings();
            statusLabel.Text = "Audio mode assignments saved.";
        }

        private sealed class AudioSettingsData
        {
            public string ActiveMode { get; set; } = "TV";
            public Dictionary<string, string?> Assignments { get; set; } = new();
        }

        private void PopulateAudioOutputDevices()
        {
            audioOutputDevices.Clear();
            outputMenuListBox.Items.Clear();

            using MMDeviceEnumerator enumerator =
                new MMDeviceEnumerator();

            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active))
            {
                if (!audioOutputDevices.Any(name =>
                    string.Equals(name, device.FriendlyName, StringComparison.OrdinalIgnoreCase)))
                {
                    audioOutputDevices.Add(device.FriendlyName);
                }
            }

            foreach (string favoriteDeviceName in favoriteOutputDeviceNames
                .Where(deviceName => audioOutputDevices.Any(item =>
                    string.Equals(item, deviceName, StringComparison.OrdinalIgnoreCase))))
            {
                outputMenuListBox.Items.Add(favoriteDeviceName);
            }

            if (outputMenuListBox.Items.Count == 0)
            {
                outputMenuListBox.Items.Add("No outputs added yet");
                selectedOutputDeviceName = null;
            }
            else
            {
                int selectedIndex = selectedOutputDeviceName == null
                    ? -1
                    : outputMenuListBox.Items.IndexOf(selectedOutputDeviceName);

                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                    selectedOutputDeviceName = outputMenuListBox.Items[0]?.ToString();
                }

                outputMenuListBox.SelectedIndex = selectedIndex;
            }
        }

        private void AddOutputButton_Click(
            object? sender,
            EventArgs e)
        {
            using Form chooser = new Form();
            chooser.Text = "Add Audio Output";
            chooser.StartPosition = FormStartPosition.CenterParent;
            chooser.FormBorderStyle = FormBorderStyle.FixedDialog;
            chooser.MinimizeBox = false;
            chooser.MaximizeBox = false;
            chooser.ShowInTaskbar = false;
            chooser.ClientSize = new Size(540, 430);
            chooser.MinimumSize = new Size(420, 320);

            // Use explicit rows instead of stacking docked controls directly
            // on the form. This keeps the action buttons inside the dialog
            // even when Windows applies display scaling.
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            Label instruction = new Label
            {
                Text = "Choose an audio output to add to your favorites:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0),
                AutoEllipsis = true
            };

            ListBox availableOutputs = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One
            };

            List<string> availableNames = new List<string>();
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    if (!availableNames.Any(name =>
                        string.Equals(name, device.FriendlyName, StringComparison.OrdinalIgnoreCase)))
                    {
                        availableNames.Add(device.FriendlyName);
                    }
                }
            }

            foreach (string name in availableNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                availableOutputs.Items.Add(name);

            if (availableOutputs.Items.Count > 0)
                availableOutputs.SelectedIndex = 0;

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 7, 0, 0),
                Margin = new Padding(0)
            };

            Button add = new Button
            {
                Text = "Add Selected",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK,
                Margin = new Padding(6, 0, 0, 0)
            };
            Button cancel = new Button
            {
                Text = "Cancel",
                Width = 85,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(6, 0, 0, 0)
            };

            buttons.Controls.Add(add);
            buttons.Controls.Add(cancel);

            layout.Controls.Add(instruction, 0, 0);
            layout.Controls.Add(availableOutputs, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            chooser.Controls.Add(layout);

            chooser.AcceptButton = add;
            chooser.CancelButton = cancel;

            if (chooser.ShowDialog(this) != DialogResult.OK
                || availableOutputs.SelectedItem is not string selectedDeviceName)
            {
                return;
            }

            if (!favoriteOutputDeviceNames.Any(favorite =>
                string.Equals(favorite, selectedDeviceName, StringComparison.OrdinalIgnoreCase)))
            {
                favoriteOutputDeviceNames.Add(selectedDeviceName);
            }

            selectedOutputDeviceName = selectedDeviceName;
            if (activeAudioMode != "Off") audioModeAssignments[activeAudioMode] = selectedDeviceName;
            SaveAudioSettings();
            PopulateAudioOutputDevices();
            statusLabel.Text = $"Added output: {selectedDeviceName}";
        }


        private void RemoveOutputButton_Click(
            object? sender,
            EventArgs e)
        {
            if (outputMenuListBox.SelectedItem is not string selectedDeviceName
                || selectedDeviceName.StartsWith("No outputs", StringComparison.OrdinalIgnoreCase))
            {
                statusLabel.Text = "Select a favorite output to remove.";
                return;
            }

            favoriteOutputDeviceNames.RemoveAll(name =>
                string.Equals(name, selectedDeviceName, StringComparison.OrdinalIgnoreCase));

            selectedOutputDeviceName = null;
            PopulateAudioOutputDevices();
            statusLabel.Text = $"Removed output: {selectedDeviceName}";
        }

        private void SendPad(
            int row,
            int column,
            byte velocity)
        {
            if (!midiConnected)
            {
                statusLabel.Text =
                    "Not connected to a MIDI output.";

                return;
            }


            byte note =
                (byte)(
                    81
                    + column
                    - (row * 10));


            uint message =
                0x90u
                | ((uint)note << 8)
                | ((uint)velocity << 16);


            uint result =
                midiOutShortMsg(
                    midiHandle,
                    message);


            if (result != 0)
            {
                statusLabel.Text =
                    $"MIDI send error: 0x{result:X8}";
            }
        }


        // ============================================================
        // SHOW 0–63 PALETTE
        // ============================================================
        //
        // Physical Launchpad:
        //
        // 0   1   2   3   4   5   6   7
        // 8   9   10  11  12  13  14  15
        // ...
        // 56  57  58  59  60  61  62  63
        //
        // This lets us see what each velocity actually looks like
        // on the hardware.
        // ============================================================

private void ShowAllPadsDimWhite()
{
    if (!midiConnected)
    {
        statusLabel.Text = "Not connected to a MIDI output.";
        return;
    }

    for (int row = 0; row < 8; row++)
    {
        for (int column = 0; column < 8; column++)
        {
            SendPad(row, column, DimWhiteLightValue);
        }
    }

    statusLabel.Text = "Connected. Launchpad lights set to dim white.";
}


private void ShowPalette(int startValue)
{
    if (!midiConnected)
    {
        statusLabel.Text =
            "Not connected to a MIDI output.";

        return;
    }

    for (int row = 0; row < 8; row++)
    {
        for (int column = 0; column < 8; column++)
        {
            int value =
                startValue + (row * 8) + column;

            if (value <= 127)
            {
                SendPad(
                    row,
                    column,
                    (byte)value);
            }
            else
            {
                // Page 2, pad 64:
                // this pad is reserved for the palette scanner.
                SendPad(
                    row,
                    column,
                    0);
            }
        }
    }

    statusLabel.Text =
        $"Palette values {startValue}–{Math.Min(startValue + 63, 127)} displayed.";
}


// ============================================================
// PALETTE SCANNER
// ============================================================

private void InitializePaletteCycle()
{
    paletteCycleTimer =
        new System.Windows.Forms.Timer();

    paletteCycleTimer.Interval =
        75;

    paletteCycleTimer.Tick +=
        PaletteCycleTimer_Tick;
}


private void StartPaletteCycle()
{
    if (!midiConnected)
    {
        statusLabel.Text =
            "Not connected to a MIDI output.";

        return;
    }

    paletteCycleValue = 1;

    paletteCycleTimer.Start();

    statusLabel.Text =
        "Palette scanner running...";
}


private void StopPaletteCycle()
{
    if (paletteCycleTimer != null)
    {
        paletteCycleTimer.Stop();
    }
}


private void PaletteCycleTimer_Tick(
    object? sender,
    EventArgs e)
{
    if (!midiConnected)
    {
        StopPaletteCycle();
        return;
    }

    // Bottom-right physical Launchpad pad:
    // row 7, column 7 = MIDI note 18.
    SendPad(
        7,
        7,
        (byte)paletteCycleValue);

    paletteCycleValue++;

    if (paletteCycleValue > 127)
    {
        paletteCycleValue = 1;
    }
}

        

                // ============================================================
        // SHOW PALETTE BUTTON
        // ============================================================

    	private void Rainbow1Button_Click(
    object? sender,
    EventArgs e)
{
    StopPaletteCycle();

    rainbowPageTwo = false;

    ShowPalette(1);
}


private void Rainbow2Button_Click(
    object? sender,
    EventArgs e)
{
    StopPaletteCycle();

    rainbowPageTwo = true;

    ShowPalette(65);

    paletteCycleValue = 1;
}

        // ============================================================
        // ALL OFF
        // ============================================================

        private void AllOffButton_Click(
            object? sender,
            EventArgs e)
        {
            TurnEverythingOff();
        }


        private void TurnEverythingOff()
        {
            manualPadLights.Clear();
            if (!midiConnected)
            {
                statusLabel.Text =
                    "Not connected to a MIDI output.";

                return;
            }


            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    SendPad(
                        row,
                        column,
                        0);
                }
            }


            ResetPcGrid();


            statusLabel.Text =
                "All Launchpad pads turned off.";
        }


        // ============================================================
        // ALL ON
        // ============================================================

        private void AllOnButton_Click(
            object? sender,
            EventArgs e)
        {
            if (!midiConnected)
            {
                statusLabel.Text =
                    "Not connected to a MIDI output.";

                return;
            }


            byte velocity =
                GetSelectedPaletteValue();


            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    SendPad(
                        row,
                        column,
                        velocity);
                }
            }


            foreach (Control control in padGrid.Controls)
            {
                if (control is Button button)
                {
                    button.BackColor =
                        SystemColors.Highlight;

                    button.ForeColor =
                        SystemColors.HighlightText;
                }
            }


            statusLabel.Text =
                $"All Launchpad pads set to velocity {velocity}.";
        }


        // ============================================================
        // RESET PC GRID
        // ============================================================

        private void ResetPcGrid()
        {
            foreach (Control control in padGrid.Controls)
            {
                if (control is Button button)
                {
                    button.BackColor =
                        SystemColors.Control;

                    button.ForeColor =
                        SystemColors.ControlText;
                }
            }
        }


        // ============================================================
        // WINDOW CLOSING
        // ============================================================

        private void MainForm_FormClosing(
            object? sender,
            FormClosingEventArgs e)
        {
            StopAllSounds();
            CloseMidiDevice();
        }


        // ============================================================
        // PAD POSITION
        // ============================================================

        private class PadPosition
        {
            public int Row;
            public int Column;
            public int PaletteValue;
        }


        private class PadAction
        {
            public string? SoundFilePath;
            public string? DisplayName;
            public byte? LightColorValue;
        }


        private class ProfileData
        {
            public string? Name { get; set; }
            public byte? BasePaletteValue { get; set; }
            public List<ProfilePadAssignment> Pads { get; set; } = new();
        }


        private class ProfilePadAssignment
        {
            public int Index { get; set; }
            public string? SoundFilePath { get; set; }
            public string? DisplayName { get; set; }
            public byte? LightColorValue { get; set; }
        }


        // ============================================================
        // MIDI DEVICE
        // ============================================================

        private class MidiDevice
        {
            public UIntPtr Id;
            public string Name = "";


            public override string ToString()
            {
                return Name;
            }
        }
    }


    // ================================================================
    // PROGRAM STARTUP
    // ================================================================

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Application.Run(
                new MainForm());
        }
    }
}