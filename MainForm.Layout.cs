using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Soundboard
{
    partial class MainForm
    {
        private void BuildWindow()
        {
            Text = "Vicious Mockery";
            Width = 1580;
            Height = 900;
            MinimumSize = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormClosing += MainForm_FormClosing;
        }

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

            foreach ((byte Value, Color Color) item in PadColorPalette)
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
    }
}