using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Soundboard
{
    partial class MainForm
    {
        private TableLayoutPanel BuildWindow()
        {
            Text = "Soundboard Slam";
            Width = 900;
            Height = 950;
            MinimumSize = new Size(850, 900);
            StartPosition = FormStartPosition.CenterScreen;
            FormClosing += MainForm_FormClosing;

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 2;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 390));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.Padding = new Padding(10);
            Controls.Add(mainLayout);

            TableLayoutPanel topLayout = BuildTopLayout();
            mainLayout.Controls.Add(topLayout, 0, 0);

            BuildPadGrid();
            BuildSideLayout();

            mainLayout.Controls.Add(padGrid, 0, 1);
            mainLayout.Controls.Add(sideLayout, 1, 1);

            return mainLayout;
        }

        private TableLayoutPanel BuildTopLayout()
        {
            TableLayoutPanel topLayout = new TableLayoutPanel();
            topLayout.Dock = DockStyle.Fill;
            topLayout.ColumnCount = 4;
            topLayout.RowCount = 9;

            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));

            for (int row = 0; row < 4; row++)
                topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            BuildMidiControls(topLayout);
            BuildPaletteControls(topLayout);
            BuildAudioOutputControls(topLayout);
            BuildProfileControls(topLayout);
            BuildActionButtons(topLayout);
            BuildStatusControl(topLayout);

            return topLayout;
        }

        private void BuildMidiControls(TableLayoutPanel topLayout)
        {
            Label midiLabel = new Label();
            midiLabel.Text = "MIDI Output:";
            midiLabel.Dock = DockStyle.Fill;
            midiLabel.TextAlign = ContentAlignment.MiddleLeft;
            topLayout.Controls.Add(midiLabel, 0, 0);

            midiDevices = new ComboBox();
            midiDevices.Dock = DockStyle.Fill;
            midiDevices.DropDownStyle = ComboBoxStyle.DropDownList;
            topLayout.Controls.Add(midiDevices, 1, 0);

            connectButton = new Button();
            connectButton.Text = "";
            connectButton.Dock = DockStyle.Fill;
            connectButton.BackColor = Color.Gray;
            connectButton.ForeColor = Color.White;
            connectButton.Click += ConnectButton_Click;
            connectButton.FlatStyle = FlatStyle.Flat;
            connectButton.FlatAppearance.BorderSize = 0;
            connectButton.Size = new Size(32, 32);
            connectButton.Margin = new Padding(0);
            connectButton.TextAlign = ContentAlignment.MiddleCenter;
            topLayout.Controls.Add(connectButton, 2, 0);
            topLayout.Controls.Add(new Label(), 3, 0);

            deviceCountLabel = new Label();
            deviceCountLabel.Text = "MIDI: Launchpad MK2 (0 outputs)";
            deviceCountLabel.Dock = DockStyle.Fill;
            deviceCountLabel.TextAlign = ContentAlignment.MiddleLeft;
            deviceCountLabel.Margin = new Padding(0);
            deviceCountLabel.BackColor = Color.FromArgb(243, 243, 243);
            deviceCountLabel.BorderStyle = BorderStyle.FixedSingle;
            deviceCountLabel.Padding = new Padding(6, 2, 6, 2);
            topLayout.Controls.Add(deviceCountLabel, 1, 1);
            topLayout.SetColumnSpan(deviceCountLabel, 3);
        }

        private void BuildPaletteControls(TableLayoutPanel topLayout)
        {
            Label paletteLabel = new Label();
            paletteLabel.Text = "Palette Value:";
            paletteLabel.Dock = DockStyle.Fill;
            paletteLabel.TextAlign = ContentAlignment.MiddleLeft;
            topLayout.Controls.Add(paletteLabel, 0, 2);

            paletteSelector = new ComboBox();
            paletteSelector.Dock = DockStyle.Fill;
            paletteSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            for (int value = 0; value <= 127; value++)
                paletteSelector.Items.Add(value.ToString());
            paletteSelector.SelectedIndex = Random.Shared.Next(34, 38);
            topLayout.Controls.Add(paletteSelector, 1, 2);

            allOffButton = new Button();
            allOffButton.Text = "ALL OFF";
            allOffButton.Dock = DockStyle.Fill;
            allOffButton.Click += AllOffButton_Click;
            topLayout.Controls.Add(allOffButton, 2, 2);

            allOnButton = new Button();
            allOnButton.Text = "ALL ON";
            allOnButton.Dock = DockStyle.Fill;
            allOnButton.Click += AllOnButton_Click;
            topLayout.Controls.Add(allOnButton, 3, 2);
        }

        private void BuildAudioOutputControls(TableLayoutPanel topLayout)
        {
            Label audioOutputLabel = new Label();
            audioOutputLabel.Text = "Audio Output:";
            audioOutputLabel.Dock = DockStyle.Fill;
            audioOutputLabel.TextAlign = ContentAlignment.MiddleLeft;
            topLayout.Controls.Add(audioOutputLabel, 0, 4);

            audioOutputSelector = new ComboBox();
            audioOutputSelector.Dock = DockStyle.Fill;
            audioOutputSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            audioOutputSelector.IntegralHeight = false;
            audioOutputSelector.SelectedIndexChanged += (_, _) =>
            {
                if (audioOutputSelector.SelectedItem is string selectedDeviceName)
                    selectedOutputDeviceName = selectedDeviceName;
            };
            topLayout.Controls.Add(audioOutputSelector, 1, 4);
            topLayout.SetColumnSpan(audioOutputSelector, 2);

            addOutputButton = new Button();
            addOutputButton.Text = "Add Output";
            addOutputButton.Dock = DockStyle.Fill;
            addOutputButton.Click += AddOutputButton_Click;
            topLayout.Controls.Add(addOutputButton, 3, 4);

            outputMenuListBox = new ListBox();
            outputMenuListBox.Dock = DockStyle.Fill;
            outputMenuListBox.IntegralHeight = false;
            outputMenuListBox.SelectionMode = SelectionMode.One;
            outputMenuListBox.SelectedIndexChanged += (_, _) =>
            {
                if (outputMenuListBox.SelectedItem is string selectedDeviceName)
                    selectedOutputDeviceName = selectedDeviceName;
            };

            Label outputFavoritesLabel = new Label();
            outputFavoritesLabel.Text = "Added Outputs:";
            outputFavoritesLabel.Dock = DockStyle.Fill;
            outputFavoritesLabel.TextAlign = ContentAlignment.MiddleLeft;
            outputFavoritesLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            topLayout.Controls.Add(outputFavoritesLabel, 0, 6);
            topLayout.SetColumnSpan(outputFavoritesLabel, 4);

            topLayout.Controls.Add(outputMenuListBox, 0, 7);
            topLayout.SetColumnSpan(outputMenuListBox, 4);

            PopulateAudioOutputDevices();
        }

        private void BuildProfileControls(TableLayoutPanel topLayout)
        {
            Label profileLabel = new Label();
            profileLabel.Text = "Profile:";
            profileLabel.Dock = DockStyle.Fill;
            profileLabel.TextAlign = ContentAlignment.MiddleLeft;
            topLayout.Controls.Add(profileLabel, 0, 3);

            profileSelector = new ComboBox();
            profileSelector.Dock = DockStyle.Fill;
            profileSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            profileBasePaletteValues["Janeway"] = 24;
            profileBasePaletteValues["Shorsey"] = 72;
            profileSelector.Items.Add("Janeway");
            profileSelector.Items.Add("Shorsey");
            profileSelector.SelectedIndex = 0;
            profileSelector.SelectedIndexChanged += (_, _) =>
            {
                if (profileSelector.SelectedItem is string profileName)
                {
                    currentProfileName = profileName;
                    ApplyProfilePalette(profileName);
                }
            };
            topLayout.Controls.Add(profileSelector, 1, 3);
            topLayout.SetColumnSpan(profileSelector, 2);

            newProfileButton = new Button();
            newProfileButton.Text = "New Profile";
            newProfileButton.Dock = DockStyle.Fill;
            newProfileButton.Click += NewProfileButton_Click;
            topLayout.Controls.Add(newProfileButton, 3, 3);
        }

        private void BuildActionButtons(TableLayoutPanel topLayout)
        {
            assignSoundButton = new Button();
            assignSoundButton.Text = "Assign Sound";
            assignSoundButton.Dock = DockStyle.Fill;
            assignSoundButton.Click += AssignSoundButton_Click;
            topLayout.Controls.Add(assignSoundButton, 0, 5);
            topLayout.SetColumnSpan(assignSoundButton, 2);

            saveProfileButton = new Button();
            saveProfileButton.Text = "Save Profile";
            saveProfileButton.Dock = DockStyle.Fill;
            saveProfileButton.Click += SaveProfileButton_Click;
            topLayout.Controls.Add(saveProfileButton, 2, 5);

            loadProfileButton = new Button();
            loadProfileButton.Text = "Load Profile";
            loadProfileButton.Dock = DockStyle.Fill;
            loadProfileButton.Click += LoadProfileButton_Click;
            topLayout.Controls.Add(loadProfileButton, 3, 5);
        }

        private void BuildStatusControl(TableLayoutPanel topLayout)
        {
            statusLabel = new Label();
            statusLabel.Text = "Not connected.";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Margin = new Padding(0, 4, 0, 0);
            statusLabel.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            statusLabel.BackColor = Color.FromArgb(243, 243, 243);
            statusLabel.BorderStyle = BorderStyle.FixedSingle;
            statusLabel.Padding = new Padding(6, 2, 6, 2);
            topLayout.Controls.Add(statusLabel, 0, 8);
            topLayout.SetColumnSpan(statusLabel, 4);
        }

        private void BuildPadGrid()
        {
            padGrid = new TableLayoutPanel();
            padGrid.Dock = DockStyle.Fill;
            padGrid.ColumnCount = 8;
            padGrid.RowCount = 8;
            padGrid.Margin = new Padding(5);
            padGrid.Padding = new Padding(5);

            for (int column = 0; column < 8; column++)
                padGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

            for (int row = 0; row < 8; row++)
                padGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5f));

            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Button padButton = new Button();
                    int paletteValue = (row * 8) + column;
                    padButton.Text = (paletteValue + 1).ToString();
                    padButton.Dock = DockStyle.Fill;
                    padButton.Margin = new Padding(3);
                    padButton.TextAlign = ContentAlignment.MiddleCenter;
                    padButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                    padButton.Tag = new PadPosition
                    {
                        Row = row,
                        Column = column,
                        PaletteValue = paletteValue
                    };
                    padButton.Click += PadButton_Click;
                    padButton.MouseDown += PadButton_MouseDown;
                    padGrid.Controls.Add(padButton, column, row);
                }
            }
        }

        private void BuildSideLayout()
        {
            sideLayout = new TableLayoutPanel();
            sideLayout.Dock = DockStyle.Fill;
            sideLayout.ColumnCount = 1;
            sideLayout.RowCount = 3;
            sideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            sideLayout.Margin = new Padding(5);
            sideLayout.Padding = new Padding(5);

            rainbow1Button = new Button();
            rainbow1Button.Text = "\U0001f308 1";
            rainbow1Button.Dock = DockStyle.Fill;
            rainbow1Button.Margin = new Padding(3);
            rainbow1Button.Font = new Font("Segoe UI Emoji", 20, FontStyle.Regular);
            rainbow1Button.BackColor = Color.FromArgb(255, 130, 120);
            rainbow1Button.ForeColor = Color.White;
            rainbow1Button.Click += Rainbow1Button_Click;
            sideLayout.Controls.Add(rainbow1Button, 0, 0);

            rainbow2Button = new Button();
            rainbow2Button.Text = "\U0001f308 2";
            rainbow2Button.Dock = DockStyle.Fill;
            rainbow2Button.Margin = new Padding(3);
            rainbow2Button.Font = new Font("Segoe UI Emoji", 20, FontStyle.Regular);
            rainbow2Button.BackColor = Color.FromArgb(120, 170, 255);
            rainbow2Button.ForeColor = Color.White;
            rainbow2Button.Click += Rainbow2Button_Click;
            sideLayout.Controls.Add(rainbow2Button, 0, 1);

            clearAssignmentsButton = new Button();
            clearAssignmentsButton.Text = "Clear";
            clearAssignmentsButton.Dock = DockStyle.Fill;
            clearAssignmentsButton.Margin = new Padding(3);
            clearAssignmentsButton.BackColor = Color.FromArgb(245, 245, 245);
            clearAssignmentsButton.ForeColor = Color.Black;
            clearAssignmentsButton.Click += ClearAssignmentsButton_Click;
            sideLayout.Controls.Add(clearAssignmentsButton, 0, 2);
        }
    }
}