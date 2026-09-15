using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Soundboard
{
    public partial class MainForm : Form
    {
        private UIntPtr selectedMidiDeviceId = UIntPtr.Zero;
        private IntPtr midiHandle = IntPtr.Zero;
        private IntPtr midiInHandle = IntPtr.Zero;
        private GCHandle midiInCallbackHandle;
        private bool midiConnected = false;

        private ComboBox midiDevices = null!;
        private Button connectButton = null!;
        private ComboBox paletteSelector = null!;
        private ComboBox audioOutputSelector = null!;
        private ListBox outputMenuListBox = null!;
        private Button addOutputButton = null!;
        private ComboBox profileSelector = null!;
        private Button newProfileButton = null!;
        private Button allOffButton = null!;
        private Button allOnButton = null!;
        private Button rainbow1Button = null!;
        private Button rainbow2Button = null!;
        private Button assignSoundButton = null!;
        private Button saveProfileButton = null!;
        private Button loadProfileButton = null!;
        private Button clearAssignmentsButton = null!;
        private TableLayoutPanel sideLayout = null!;

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
        private readonly Dictionary<string, byte> profileBasePaletteValues = new();
        private string currentProfileName = "Untitled Profile";

        private static readonly (byte Value, Color Color)[] PadColorPalette =
        {
            (1, Color.FromArgb(255, 70, 70)),
            (8, Color.FromArgb(255, 120, 70)),
            (16, Color.FromArgb(255, 180, 70)),
            (24, Color.FromArgb(230, 220, 90)),
            (32, Color.FromArgb(160, 230, 90)),
            (40, Color.FromArgb(95, 220, 145)),
            (48, Color.FromArgb(90, 205, 200)),
            (56, Color.FromArgb(90, 150, 220)),
            (64, Color.FromArgb(120, 110, 240)),
            (72, Color.FromArgb(180, 90, 220)),
            (80, Color.FromArgb(235, 110, 180)),
            (96, Color.FromArgb(255, 150, 150)),
            (104, Color.FromArgb(120, 240, 230)),
            (112, Color.FromArgb(250, 210, 120)),
            (120, Color.FromArgb(210, 240, 250)),
            (127, Color.FromArgb(255, 255, 255))
        };

        private Label statusLabel = null!;
        private Label deviceCountLabel = null!;
        private TableLayoutPanel padGrid = null!;
        private readonly ToolTip padButtonToolTip = new();

        public MainForm()
        {
            BuildWindow();
            InitializePaletteCycle();
            FindMidiDevices();
        }

        // ============================================================
        // FIND MIDI DEVICES
        // ============================================================

        private void FindMidiDevices()
        {
            midiDevices.Items.Clear();

            uint deviceCount = NativeMethods.midiOutGetNumDevs();

            string selectedOutputName = midiDevices.Items.Count > 0
                && midiDevices.SelectedItem is MidiDevice selectedDevice
                ? selectedDevice.Name
                : "No MIDI output selected";

            deviceCountLabel.Text = $"MIDI: {selectedOutputName} ({deviceCount} outputs)";

            if (deviceCount == 0)
            {
                statusLabel.Text = "Windows reports no MIDI outputs.";
                return;
            }

            for (uint deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                NativeMethods.MIDIOUTCAPS capabilities;
                uint result = NativeMethods.midiOutGetDevCapsW(
                    new UIntPtr(deviceId),
                    out capabilities,
                    (uint)Marshal.SizeOf<NativeMethods.MIDIOUTCAPS>());

                if (result == 0)
                {
                    string name = capabilities.ProductName?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"Unnamed MIDI Output {deviceId}";

                    midiDevices.Items.Add(new MidiDevice
                    {
                        Id = new UIntPtr(deviceId),
                        Name = name
                    });
                }
                else
                {
                    midiDevices.Items.Add(new MidiDevice
                    {
                        Id = new UIntPtr(deviceId),
                        Name = $"MIDI Output {deviceId} (GetCaps error 0x{result:X8})"
                    });
                }
            }

            if (midiDevices.Items.Count > 0)
            {
                midiDevices.SelectedIndex = 0;

                for (int i = 0; i < midiDevices.Items.Count; i++)
                {
                    if (midiDevices.Items[i] is not MidiDevice device)
                        continue;

                    if (device.Name.Contains("Launchpad", StringComparison.OrdinalIgnoreCase))
                    {
                        midiDevices.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        // ============================================================
        // CONNECT / DISCONNECT
        // ============================================================

        private void ConnectButton_Click(object? sender, EventArgs e)
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

            MidiDevice device = (MidiDevice)midiDevices.SelectedItem;
            IntPtr newHandle;

            uint result = NativeMethods.midiOutOpen(
                out newHandle,
                device.Id,
                UIntPtr.Zero,
                UIntPtr.Zero,
                0);

            if (result != 0)
            {
                statusLabel.Text = $"Could not open {device.Name}.";
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

            midiHandle = newHandle;
            selectedMidiDeviceId = device.Id;

            if (OpenMidiInput(device.Id, device.Name))
            {
                midiConnected = true;
            }
            else
            {
                midiConnected = false;
                UpdateConnectionToggleAppearance();
                return;
            }

            UpdateConnectionToggleAppearance();
            ShowPalette(1);
            statusLabel.Text = $"Connected: {device.Name}";
        }

        private void CloseMidiDevice()
        {
            if (!midiConnected)
                return;

            TurnEverythingOff();

            NativeMethods.midiInStop(midiInHandle);
            NativeMethods.midiInReset(midiInHandle);
            NativeMethods.midiInClose(midiInHandle);
            midiInHandle = IntPtr.Zero;

            if (midiInCallbackHandle.IsAllocated)
                midiInCallbackHandle.Free();

            NativeMethods.midiOutReset(midiHandle);
            NativeMethods.midiOutClose(midiHandle);
            midiHandle = IntPtr.Zero;

            midiConnected = false;
            UpdateConnectionToggleAppearance();
            statusLabel.Text = "Disconnected.";
        }

        private void MidiSelectorButton_Click(object? sender, EventArgs e)
        {
            using ContextMenuStrip menu = new ContextMenuStrip();

            foreach (MidiDevice device in midiDevices.Items)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(device.Name);
                item.Tag = device;
                item.Click += (_, _) =>
                {
                    midiDevices.SelectedItem = device;
                    if (midiConnected)
                    {
                        CloseMidiDevice();
                        ConnectButton_Click(this, EventArgs.Empty);
                    }
                };
                menu.Items.Add(item);
            }

            menu.Show(deviceCountLabel, new Point(0, deviceCountLabel.Height));
        }

        private void UpdateConnectionToggleAppearance()
        {
            connectButton.BackColor = midiConnected ? Color.ForestGreen : Color.Gray;
            connectButton.ForeColor = Color.White;
            connectButton.FlatAppearance.BorderColor = midiConnected ? Color.ForestGreen : Color.Gray;
            connectButton.FlatAppearance.MouseOverBackColor = midiConnected ? Color.ForestGreen : Color.Gray;
            connectButton.FlatAppearance.MouseDownBackColor = midiConnected ? Color.ForestGreen : Color.Gray;

            using GraphicsPath path = new GraphicsPath();
            path.AddEllipse(new Rectangle(0, 0, connectButton.Width, connectButton.Height));
            connectButton.Region = new Region(path);
        }

        // ============================================================
        // PAD CLICK
        // ============================================================

        private void PadButton_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (sender is not Button clickedButton)
                return;

            if (clickedButton.Tag is not PadPosition position)
                return;

            int padIndex = (position.Row * 8) + position.Column;

            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                string defaultName = !string.IsNullOrWhiteSpace(assignedAction.DisplayName)
                    ? assignedAction.DisplayName
                    : Path.GetFileNameWithoutExtension(assignedAction.SoundFilePath);

                (string? Name, byte? SelectedColorValue)? promptResult = PromptForDisplayName(
                    defaultName,
                    assignedAction.LightColorValue ?? GetSelectedPaletteValue());

                if (promptResult == null)
                {
                    statusLabel.Text = "Pad rename cancelled.";
                    return;
                }

                assignedAction.DisplayName = string.IsNullOrWhiteSpace(promptResult.Value.Name)
                    ? defaultName
                    : promptResult.Value.Name.Trim();
                assignedAction.LightColorValue = promptResult.Value.SelectedColorValue;

                UpdatePadButtonVisualState(clickedButton, padIndex);

                if (midiConnected && promptResult.Value.SelectedColorValue.HasValue)
                    SendPad(position.Row, position.Column, promptResult.Value.SelectedColorValue.Value);

                statusLabel.Text = $"Pad {padIndex + 1} renamed.";
                return;
            }

            statusLabel.Text = $"Pad {padIndex + 1} has no sound assigned.";
        }

        private void PadButton_Click(object? sender, EventArgs e)
        {
            if (sender is not Button clickedButton)
                return;

            if (clickedButton.Tag is not PadPosition position)
                return;

            int padIndex = (position.Row * 8) + position.Column;

            if (rainbowPageTwo && position.PaletteValue == 63)
            {
                if (paletteCycleTimer.Enabled)
                    StopPaletteCycle();
                else
                    StartPaletteCycle();
                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingSoundFilePath))
            {
                padActions[padIndex] = new PadAction
                {
                    SoundFilePath = pendingSoundFilePath,
                    DisplayName = pendingDisplayName,
                    LightColorValue = pendingLightColorValue
                };

                pendingSoundFilePath = null;
                pendingDisplayName = null;
                pendingLightColorValue = null;

                UpdatePadButtonVisualState(clickedButton, padIndex);
                statusLabel.Text = $"Sound assigned to pad {padIndex + 1}.";
                return;
            }

            byte velocity = GetSelectedPaletteValue();

            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                if (assignedAction.LightColorValue.HasValue)
                    velocity = assignedAction.LightColorValue.Value;

                SendPad(position.Row, position.Column, velocity);
                clickedButton.BackColor = SystemColors.Highlight;
                clickedButton.ForeColor = SystemColors.HighlightText;
                PlayPadSound(assignedAction.SoundFilePath!);
                statusLabel.Text = $"Playing sound for pad {padIndex + 1}.";
                return;
            }

            SendPad(position.Row, position.Column, velocity);
            clickedButton.BackColor = SystemColors.Highlight;
            clickedButton.ForeColor = SystemColors.HighlightText;
            statusLabel.Text = $"Physical pad {position.PaletteValue} tested with velocity {velocity}.";
        }

        // ============================================================
        // PALETTE VALUE
        // ============================================================

        private byte GetSelectedPaletteValue()
        {
            if (paletteSelector.SelectedItem == null)
                return 0;

            if (byte.TryParse(paletteSelector.SelectedItem.ToString(), out byte value))
                return value;

            return 0;
        }

        // ============================================================
        // SOUND ASSIGNMENT
        // ============================================================

        private void AssignSoundButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*";
            dialog.Title = "Choose a sound for the next pad you click";

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text = "Sound assignment cancelled.";
                return;
            }

            string defaultName = Path.GetFileNameWithoutExtension(dialog.FileName);

            (string? Name, byte? SelectedColorValue)? promptResult = PromptForDisplayName(
                defaultName,
                pendingLightColorValue ?? GetSelectedPaletteValue());

            if (promptResult == null)
            {
                pendingSoundFilePath = null;
                pendingDisplayName = null;
                pendingLightColorValue = null;
                statusLabel.Text = "Sound assignment cancelled.";
                return;
            }

            pendingSoundFilePath = dialog.FileName;
            pendingDisplayName = string.IsNullOrWhiteSpace(promptResult.Value.Name)
                ? defaultName
                : promptResult.Value.Name.Trim();
            pendingLightColorValue = promptResult.Value.SelectedColorValue;

            statusLabel.Text = "Choose a Launchpad pad to assign that sound to.";
        }

        private (string? Name, byte? SelectedColorValue)? PromptForDisplayName(
            string defaultName,
            byte? initialLightColorValue)
        {
            byte? selectedColorValue = initialLightColorValue ?? GetSelectedPaletteValue();

            using Form promptForm = new Form();
            promptForm.Text = "Name this sound";
            promptForm.StartPosition = FormStartPosition.CenterParent;
            promptForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            promptForm.MinimizeBox = false;
            promptForm.MaximizeBox = false;
            promptForm.AutoSize = true;
            promptForm.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 3;
            layout.Padding = new Padding(12);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label promptLabel = new Label();
            promptLabel.Text = "What would you like to name this?";
            promptLabel.TextAlign = ContentAlignment.MiddleLeft;
            promptLabel.Dock = DockStyle.Fill;
            promptLabel.AutoSize = true;
            layout.Controls.Add(promptLabel);
            layout.SetColumnSpan(promptLabel, 2);

            TextBox textBox = new TextBox();
            textBox.Text = defaultName;
            textBox.Dock = DockStyle.Fill;
            textBox.Width = 260;
            layout.Controls.Add(textBox);

            Button colorSwatchButton = new Button();
            colorSwatchButton.FlatStyle = FlatStyle.Flat;
            colorSwatchButton.FlatAppearance.BorderColor = Color.Black;
            colorSwatchButton.FlatAppearance.BorderSize = 1;
            colorSwatchButton.Size = new Size(30, 26);
            colorSwatchButton.Dock = DockStyle.Fill;
            colorSwatchButton.Anchor = AnchorStyles.None;

            GraphicsPath swatchPath = new GraphicsPath();
            swatchPath.AddEllipse(0, 0, 30, 26);
            colorSwatchButton.Region = new Region(swatchPath);
            colorSwatchButton.BackColor = GetColorForPaletteValue(selectedColorValue.Value);
            colorSwatchButton.CausesValidation = false;
            colorSwatchButton.Click += (_, _) =>
            {
                byte? newColorValue = ShowColorPalettePicker(
                    selectedColorValue ?? GetSelectedPaletteValue());
                if (!newColorValue.HasValue)
                    return;
                selectedColorValue = newColorValue.Value;
                colorSwatchButton.BackColor = GetColorForPaletteValue(selectedColorValue.Value);
            };
            layout.Controls.Add(colorSwatchButton);

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.AutoSize = true;
            buttonPanel.Padding = new Padding(0, 8, 0, 0);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(90, 30);

            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Size = new Size(90, 30);

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);
            layout.Controls.Add(buttonPanel);
            layout.SetColumnSpan(buttonPanel, 2);

            promptForm.Controls.Add(layout);
            promptForm.AcceptButton = okButton;
            promptForm.CancelButton = cancelButton;

            DialogResult result = promptForm.ShowDialog(this);
            if (result != DialogResult.OK)
                return null;

            return (textBox.Text, selectedColorValue);
        }

        private byte? ShowColorPalettePicker(byte currentValue)
        {
            byte? selectedValue = null;

            using Form paletteForm = new Form();
            paletteForm.Text = "Choose a pad color";
            paletteForm.StartPosition = FormStartPosition.CenterParent;
            paletteForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            paletteForm.MinimizeBox = false;
            paletteForm.MaximizeBox = false;
            paletteForm.AutoSize = true;
            paletteForm.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel paletteGrid = new TableLayoutPanel();
            paletteGrid.Dock = DockStyle.Fill;
            paletteGrid.ColumnCount = 4;
            paletteGrid.RowCount = 4;
            paletteGrid.Padding = new Padding(10);

            for (int index = 0; index < 4; index++)
                paletteGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int index = 0; index < 4; index++)
                paletteGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            for (int index = 0; index < PadColorPalette.Length; index++)
            {
                (byte value, Color color) = PadColorPalette[index];

                Button colorButton = new Button();
                colorButton.FlatStyle = FlatStyle.Flat;
                colorButton.FlatAppearance.BorderColor = currentValue == value ? Color.Black : Color.DarkGray;
                colorButton.FlatAppearance.BorderSize = currentValue == value ? 3 : 1;
                colorButton.BackColor = color;
                colorButton.ForeColor = Color.White;
                colorButton.Margin = new Padding(4);
                colorButton.Text = value.ToString();
                colorButton.Click += (_, _) =>
                {
                    selectedValue = value;
                    paletteForm.DialogResult = DialogResult.OK;
                    paletteForm.Close();
                };

                paletteGrid.Controls.Add(colorButton, index % 4, index / 4);
            }

            paletteForm.Controls.Add(paletteGrid);

            return paletteForm.ShowDialog(this) == DialogResult.OK ? selectedValue : null;
        }

        private static Color GetColorForPaletteValue(byte value)
        {
            (byte Value, Color Color) closest = PadColorPalette[0];
            foreach ((byte paletteValue, Color paletteColor) in PadColorPalette)
            {
                if (Math.Abs(paletteValue - value) < Math.Abs(closest.Value - value))
                    closest = (paletteValue, paletteColor);
            }
            return closest.Color;
        }

        // ============================================================
        // PROFILES
        // ============================================================

        private void NewProfileButton_Click(object? sender, EventArgs e)
        {
            string? profileName = PromptForText("Create profile", "What would you like to name this profile?");
            if (string.IsNullOrWhiteSpace(profileName))
            {
                statusLabel.Text = "Profile creation cancelled.";
                return;
            }

            string trimmedName = profileName.Trim();
            byte basePaletteValue = GetSelectedPaletteValue();
            profileBasePaletteValues[trimmedName] = basePaletteValue;

            if (!profileSelector.Items.Contains(trimmedName))
                profileSelector.Items.Add(trimmedName);

            profileSelector.SelectedItem = trimmedName;
            currentProfileName = trimmedName;
            ApplyProfilePalette(trimmedName);
            statusLabel.Text = $"Profile '{trimmedName}' created.";
        }

        private void ApplyProfilePalette(string profileName)
        {
            if (profileBasePaletteValues.TryGetValue(profileName, out byte basePaletteValue))
                paletteSelector.SelectedIndex = basePaletteValue;
        }

        private string? PromptForText(string title, string promptText)
        {
            using Form promptForm = new Form();
            promptForm.Text = title;
            promptForm.StartPosition = FormStartPosition.CenterParent;
            promptForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            promptForm.MinimizeBox = false;
            promptForm.MaximizeBox = false;
            promptForm.AutoSize = true;
            promptForm.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.Padding = new Padding(12);

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label promptLabel = new Label();
            promptLabel.Text = promptText;
            promptLabel.AutoSize = true;
            promptLabel.Dock = DockStyle.Fill;
            layout.Controls.Add(promptLabel);

            TextBox textBox = new TextBox();
            textBox.Width = 280;
            textBox.Dock = DockStyle.Fill;
            layout.Controls.Add(textBox);

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.AutoSize = true;
            buttonPanel.Padding = new Padding(0, 8, 0, 0);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(80, 30);

            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Size = new Size(80, 30);

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);
            layout.Controls.Add(buttonPanel);

            promptForm.Controls.Add(layout);
            promptForm.AcceptButton = okButton;
            promptForm.CancelButton = cancelButton;

            if (promptForm.ShowDialog(this) != DialogResult.OK)
                return null;

            return textBox.Text;
        }

        private void SaveProfileButton_Click(object? sender, EventArgs e)
        {
            using SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Launchpad profile (*.json)|*.json";
            dialog.Title = "Save Launchpad profile";

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text = "Profile save cancelled.";
                return;
            }

            try
            {
                ProfileData profile = new ProfileData
                {
                    Name = currentProfileName,
                    BasePaletteValue = GetSelectedPaletteValue()
                };

                for (int i = 0; i < 64; i++)
                {
                    if (padActions.TryGetValue(i, out PadAction? assignedAction)
                        && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
                    {
                        profile.Pads.Add(new ProfilePadAssignment
                        {
                            Index = i,
                            SoundFilePath = assignedAction.SoundFilePath,
                            DisplayName = assignedAction.DisplayName,
                            LightColorValue = assignedAction.LightColorValue
                        });
                    }
                }

                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                statusLabel.Text = $"Profile saved: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Could not save profile: {ex.Message}";
            }
        }

        private void LoadProfileButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Launchpad profile (*.json)|*.json";
            dialog.Title = "Load Launchpad profile";

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text = "Profile load cancelled.";
                return;
            }

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                ProfileData? profile = JsonSerializer.Deserialize<ProfileData>(json);

                if (profile == null)
                {
                    statusLabel.Text = "Profile file could not be loaded.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(profile.Name))
                {
                    currentProfileName = profile.Name;
                    if (!profileSelector.Items.Contains(profile.Name))
                        profileSelector.Items.Add(profile.Name);
                    profileSelector.SelectedItem = profile.Name;
                }

                if (profile.BasePaletteValue.HasValue)
                    paletteSelector.SelectedIndex = profile.BasePaletteValue.Value;

                padActions.Clear();

                foreach (ProfilePadAssignment assignment in profile.Pads)
                {
                    if (assignment.Index < 0 || assignment.Index >= 64)
                        continue;
                    if (string.IsNullOrWhiteSpace(assignment.SoundFilePath))
                        continue;

                    padActions[assignment.Index] = new PadAction
                    {
                        SoundFilePath = assignment.SoundFilePath,
                        DisplayName = assignment.DisplayName,
                        LightColorValue = assignment.LightColorValue
                    };
                }

                ApplyProfileVisualState();
                statusLabel.Text = $"Profile loaded: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Could not load profile: {ex.Message}";
            }
        }

        private void ApplyProfileVisualState()
        {
            foreach (Control control in padGrid.Controls)
            {
                if (control is not Button button || control.Tag is not PadPosition position)
                    continue;

                int padIndex = (position.Row * 8) + position.Column;
                UpdatePadButtonVisualState(button, padIndex);
            }
        }

        private void ClearAssignmentsButton_Click(object? sender, EventArgs e)
        {
            padActions.Clear();
            pendingSoundFilePath = null;
            ApplyProfileVisualState();
            statusLabel.Text = "All pad assignments cleared.";
        }

        private void UpdatePadButtonVisualState(Button button, int padIndex)
        {
            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                string displayName = !string.IsNullOrWhiteSpace(assignedAction.DisplayName)
                    ? assignedAction.DisplayName
                    : Path.GetFileNameWithoutExtension(assignedAction.SoundFilePath);

                if (displayName.Length > 12)
                    displayName = displayName.Substring(0, 12) + "...";

                button.Text = $"{padIndex + 1}\n{displayName}";
                button.BackColor = SystemColors.Highlight;
                button.ForeColor = SystemColors.HighlightText;
                padButtonToolTip.SetToolTip(button, assignedAction.SoundFilePath);
                return;
            }

            button.Text = (padIndex + 1).ToString();
            button.BackColor = SystemColors.Control;
            button.ForeColor = SystemColors.ControlText;
            padButtonToolTip.SetToolTip(button, string.Empty);
        }

        // ============================================================
        // MIDI INPUT
        // ============================================================

        private bool OpenMidiInput(UIntPtr deviceId, string deviceName)
        {
            if (TryOpenMidiInput(deviceId))
            {
                statusLabel.Text = $"Connected: {deviceName} (listening for Launchpad MIDI input)";
                return true;
            }

            uint inputDeviceCount = NativeMethods.midiInGetNumDevs();
            bool foundLaunchpadInput = false;

            for (uint i = 0; i < inputDeviceCount; i++)
            {
                NativeMethods.MIDIINCAPS capabilities;
                uint result = NativeMethods.midiInGetDevCapsW(
                    new UIntPtr(i),
                    out capabilities,
                    (uint)Marshal.SizeOf<NativeMethods.MIDIINCAPS>());

                if (result != 0)
                    continue;

                string inputName = capabilities.ProductName?.Trim() ?? "";

                if (!IsLaunchpadDeviceName(inputName))
                    continue;

                foundLaunchpadInput = true;

                if (TryOpenMidiInput(new UIntPtr(i)))
                {
                    statusLabel.Text = $"Connected: {deviceName} (listening for {inputName})";
                    return true;
                }
            }

            statusLabel.Text = foundLaunchpadInput
                ? $"Connected: {deviceName} (Launchpad input was detected, but Windows refused to open it)"
                : $"Connected: {deviceName} (no Launchpad MIDI input device was reported by Windows)";

            return false;
        }

        private static bool IsLaunchpadDeviceName(string? deviceName)
        {
            return !string.IsNullOrWhiteSpace(deviceName)
                && deviceName.Contains("Launchpad", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryOpenMidiInput(UIntPtr deviceId)
        {
            midiInCallbackHandle = GCHandle.Alloc(this);

            uint result = NativeMethods.midiInOpen(
                out midiInHandle,
                deviceId,
                HandleMidiInCallback,
                GCHandle.ToIntPtr(midiInCallbackHandle),
                0x00030000);

            if (result != 0)
            {
                if (midiInCallbackHandle.IsAllocated)
                    midiInCallbackHandle.Free();
                midiInHandle = IntPtr.Zero;
                return false;
            }

            result = NativeMethods.midiInStart(midiInHandle);

            if (result != 0)
            {
                NativeMethods.midiInClose(midiInHandle);
                midiInHandle = IntPtr.Zero;
                if (midiInCallbackHandle.IsAllocated)
                    midiInCallbackHandle.Free();
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
                return;

            GCHandle handle = GCHandle.FromIntPtr(instance);
            if (handle.Target is MainForm form && message == NativeMethods.MIM_DATA)
                form.ProcessMidiInput(parameter1);
        }

        private void ProcessMidiInput(IntPtr parameter1)
        {
            uint rawMessage = unchecked((uint)parameter1.ToInt64());
            byte status = (byte)(rawMessage & 0xFF);
            byte note = (byte)((rawMessage >> 8) & 0xFF);
            byte velocity = (byte)((rawMessage >> 16) & 0xFF);

            if ((status & 0xF0) != 0x90 || velocity == 0)
                return;

            int padIndex = GetPadIndexFromMidiNote(note);
            if (padIndex < 0)
                return;

            if (padActions.TryGetValue(padIndex, out PadAction? assignedAction)
                && !string.IsNullOrWhiteSpace(assignedAction.SoundFilePath))
            {
                PlayPadSound(assignedAction.SoundFilePath!);
            }
        }

        private int GetPadIndexFromMidiNote(byte note)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    byte expectedNote = (byte)(81 + column - (row * 10));
                    if (expectedNote == note)
                        return (row * 8) + column;
                }
            }
            return -1;
        }

        // ============================================================
        // AUDIO PLAYBACK
        // ============================================================

        private void PlayPadSound(string soundFilePath)
        {
            if (!File.Exists(soundFilePath))
            {
                statusLabel.Text = $"Sound file not found: {soundFilePath}";
                return;
            }

            try
            {
                using AudioFileReader reader = new AudioFileReader(soundFilePath);
                MMDevice? selectedOutputDevice = null;

                if (!string.IsNullOrWhiteSpace(selectedOutputDeviceName))
                {
                    using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                    foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.All))
                    {
                        if (device.FriendlyName == selectedOutputDeviceName)
                        {
                            selectedOutputDevice = device;
                            break;
                        }
                    }
                }

                if (selectedOutputDevice != null)
                {
                    using WasapiOut wasapiOutput = new WasapiOut(
                        selectedOutputDevice,
                        AudioClientShareMode.Shared,
                        false,
                        200);
                    wasapiOutput.Init(reader);
                    wasapiOutput.Play();

                    while (wasapiOutput.PlaybackState == PlaybackState.Playing)
                    {
                        Application.DoEvents();
                        Thread.Sleep(10);
                    }
                    return;
                }

                using WaveOutEvent waveOutput = new WaveOutEvent();
                waveOutput.Init(reader);
                waveOutput.Play();

                while (waveOutput.PlaybackState == PlaybackState.Playing)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Could not play sound: {ex.Message}";
            }
        }

        // ============================================================
        // AUDIO OUTPUT DEVICES
        // ============================================================

        private void PopulateAudioOutputDevices()
        {
            audioOutputDevices.Clear();
            audioOutputSelector.Items.Clear();
            outputMenuListBox.Items.Clear();

            List<string> discoveredDevices = new List<string>();

            using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.All))
                discoveredDevices.Add(device.FriendlyName);

            foreach (string deviceName in discoveredDevices
                .OrderByDescending(GetAudioOutputPriority)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                audioOutputDevices.Add(deviceName);
                audioOutputSelector.Items.Add(deviceName);
            }

            if (audioOutputDevices.Count == 0)
            {
                audioOutputSelector.Items.Add("No audio outputs found");
                audioOutputSelector.SelectedIndex = 0;
                selectedOutputDeviceName = null;
                outputMenuListBox.Items.Add("No outputs added yet");
                return;
            }

            foreach (string favoriteDeviceName in favoriteOutputDeviceNames
                .Where(deviceName => audioOutputDevices.Any(item =>
                    string.Equals(item, deviceName, StringComparison.OrdinalIgnoreCase))))
            {
                outputMenuListBox.Items.Add(favoriteDeviceName);
            }

            if (outputMenuListBox.Items.Count == 0)
                outputMenuListBox.Items.Add("No outputs added yet");

            string preferredDeviceName =
                favoriteOutputDeviceNames.FirstOrDefault(deviceName => audioOutputDevices.Any(item =>
                    string.Equals(item, deviceName, StringComparison.OrdinalIgnoreCase)))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("VoiceMeeter AUX Output", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("VoiceMeeter Output", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("VB-Audio VoiceMeeter", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices.FirstOrDefault(deviceName => deviceName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                ?? audioOutputDevices[0];

            int selectedIndex = audioOutputSelector.Items.IndexOf(preferredDeviceName);
            audioOutputSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            selectedOutputDeviceName = preferredDeviceName;

            if (outputMenuListBox.Items.Count > 0
                && outputMenuListBox.Items[0] is string firstItem
                && firstItem.StartsWith("No outputs", StringComparison.OrdinalIgnoreCase))
            {
                outputMenuListBox.SelectedIndex = -1;
            }
            else if (outputMenuListBox.Items.Count > 0)
            {
                outputMenuListBox.SelectedIndex = 0;
            }
        }

        private void AddOutputButton_Click(object? sender, EventArgs e)
        {
            if (audioOutputSelector.SelectedItem is not string selectedDeviceName
                || string.IsNullOrWhiteSpace(selectedDeviceName))
            {
                statusLabel.Text = "Select an output to add.";
                return;
            }

            if (selectedDeviceName.StartsWith("No audio outputs", StringComparison.OrdinalIgnoreCase))
            {
                statusLabel.Text = "No outputs available to add.";
                return;
            }

            if (!favoriteOutputDeviceNames.Any(favorite =>
                string.Equals(favorite, selectedDeviceName, StringComparison.OrdinalIgnoreCase)))
            {
                favoriteOutputDeviceNames.Add(selectedDeviceName);
            }

            selectedOutputDeviceName = selectedDeviceName;
            PopulateAudioOutputDevices();
            statusLabel.Text = $"Added output: {selectedDeviceName}";
        }

        private static int GetAudioOutputPriority(string deviceName)
        {
            if (deviceName.Contains("VoiceMeeter AUX Output", StringComparison.OrdinalIgnoreCase))
                return 5;
            if (deviceName.Contains("VoiceMeeter Output", StringComparison.OrdinalIgnoreCase))
                return 4;
            if (deviceName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (deviceName.Contains("VB-Audio VoiceMeeter", StringComparison.OrdinalIgnoreCase)
                || deviceName.Contains("VoiceMeeter VAIO", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (deviceName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (deviceName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (deviceName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                return -1;
            return -2;
        }

        // ============================================================
        // PALETTE CYCLE
        // ============================================================

        private void InitializePaletteCycle()
        {
            paletteCycleTimer = new System.Windows.Forms.Timer();
            paletteCycleTimer.Interval = 75;
            paletteCycleTimer.Tick += PaletteCycleTimer_Tick;
        }

        private void StartPaletteCycle()
        {
            if (!midiConnected)
            {
                statusLabel.Text = "Not connected to a MIDI output.";
                return;
            }

            paletteCycleValue = 1;
            paletteCycleTimer.Start();
            statusLabel.Text = "Palette scanner running...";
        }

        private void StopPaletteCycle()
        {
            paletteCycleTimer?.Stop();
        }

        private void PaletteCycleTimer_Tick(object? sender, EventArgs e)
        {
            if (!midiConnected)
            {
                StopPaletteCycle();
                return;
            }

            SendPad(7, 7, (byte)paletteCycleValue);
            paletteCycleValue++;
            if (paletteCycleValue > 127)
                paletteCycleValue = 1;
        }

        // ============================================================
        // SEND PAD / SHOW PALETTE
        // ============================================================
        //
        // LAUNCHPAD MK2 SESSION LAYOUT
        //
        // Row 0: 81 82 83 84 85 86 87 88
        // Row 1: 71 72 73 74 75 76 77 78
        // Row 2: 61 62 63 64 65 66 67 68
        // Row 3: 51 52 53 54 55 56 57 58
        // Row 4: 41 42 43 44 45 46 47 48
        // Row 5: 31 32 33 34 35 36 37 38
        // Row 6: 21 22 23 24 25 26 27 28
        // Row 7: 11 12 13 14 15 16 17 18
        //
        // note = 81 + column - (row * 10)
        // Channel 1 Note On = 0x90
        //
        // DO NOT CHANGE THIS MAPPING.
        // ============================================================

        private void SendPad(int row, int column, byte velocity)
        {
            if (!midiConnected)
            {
                statusLabel.Text = "Not connected to a MIDI output.";
                return;
            }

            byte note = (byte)(81 + column - (row * 10));
            uint message = 0x90u | ((uint)note << 8) | ((uint)velocity << 16);

            uint result = NativeMethods.midiOutShortMsg(midiHandle, message);
            if (result != 0)
                statusLabel.Text = $"MIDI send error: 0x{result:X8}";
        }

        private void ShowPalette(int startValue)
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
                    int value = startValue + (row * 8) + column;
                    if (value <= 127)
                        SendPad(row, column, (byte)value);
                    else
                        SendPad(row, column, 0);
                }
            }

            statusLabel.Text = $"Palette values {startValue}\u2013{Math.Min(startValue + 63, 127)} displayed.";
        }

        private void Rainbow1Button_Click(object? sender, EventArgs e)
        {
            StopPaletteCycle();
            rainbowPageTwo = false;
            ShowPalette(1);
        }

        private void Rainbow2Button_Click(object? sender, EventArgs e)
        {
            StopPaletteCycle();
            rainbowPageTwo = true;
            ShowPalette(65);
            paletteCycleValue = 1;
        }

        // ============================================================
        // ALL OFF / ALL ON
        // ============================================================

        private void AllOffButton_Click(object? sender, EventArgs e) => TurnEverythingOff();

        private void TurnEverythingOff()
        {
            if (!midiConnected)
            {
                statusLabel.Text = "Not connected to a MIDI output.";
                return;
            }

            for (int row = 0; row < 8; row++)
                for (int column = 0; column < 8; column++)
                    SendPad(row, column, 0);

            ResetPadGrid();
            statusLabel.Text = "All Launchpad pads turned off.";
        }

        private void AllOnButton_Click(object? sender, EventArgs e)
        {
            if (!midiConnected)
            {
                statusLabel.Text = "Not connected to a MIDI output.";
                return;
            }

            byte velocity = GetSelectedPaletteValue();

            for (int row = 0; row < 8; row++)
                for (int column = 0; column < 8; column++)
                    SendPad(row, column, velocity);

            foreach (Control control in padGrid.Controls)
            {
                if (control is Button button)
                {
                    button.BackColor = SystemColors.Highlight;
                    button.ForeColor = SystemColors.HighlightText;
                }
            }

            statusLabel.Text = $"All Launchpad pads set to velocity {velocity}.";
        }

        private void ResetPadGrid()
        {
            foreach (Control control in padGrid.Controls)
            {
                if (control is Button button)
                {
                    button.BackColor = SystemColors.Control;
                    button.ForeColor = SystemColors.ControlText;
                }
            }
        }

        // ============================================================
        // WINDOW CLOSING
        // ============================================================

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e) => CloseMidiDevice();
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}