﻿using System;
using System.Collections.Generic;
using System.Drawing.IconLib;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HeliosPlus.Resources;
using HeliosPlus.Shared;
using HeliosPlus.Steam;
using NvAPIWrapper.Native.GPU;

namespace HeliosPlus.UIForms
{
    public partial class ShortcutForm : Form
    {
        public ShortcutForm()
        {
            InitializeComponent();
        }

        public ShortcutForm(Profile profile) : this()
        {
            Profile = profile;
        }


        public static string IconCache
        {
            get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Assembly.GetExecutingAssembly().GetName().Name, @"IconCache");
        }

        public string ProcessNameToMonitor
        {
            get
            {
                if (rb_switch_temp.Checked && rb_standalone.Checked) {
                    if (rb_wait_executable.Checked)
                    {
                        return txt_process_name.Text;
                    } 
                }
                return string.Empty;
            }
            set
            {
                // We we're setting this entry, then we want to set it to a particular entry
                txt_process_name.Text = value;
                rb_wait_executable.Checked = true;
            }
        }

        public Profile Profile
        {
            get => dv_profile.Profile;
            set
            {
                // Check the profile is valid
                // Create an array of display profiles we have
                var profiles = Profile.GetAllProfiles().ToArray();
                // Check if the user supplied a --profile option using the profiles' ID
                var profileIndex = profiles.Length > 0 ? Array.FindIndex(profiles, p => p.Id.Equals(value.Id, StringComparison.InvariantCultureIgnoreCase)) : -1;
                // If the profileID wasn't there, maybe they used the profile name?
                if (profileIndex == -1)
                {
                    // Try and lookup the profile in the profiles' Name fields
                    profileIndex = profiles.Length > 0 ? Array.FindIndex(profiles, p => p.Name.StartsWith(value.Name, StringComparison.InvariantCultureIgnoreCase)) : -1;
                }
                // If the profileID still isn't there, then raise the alarm
                if (profileIndex == -1)
                {
                    MessageBox.Show(
                        $"ShortcutForm: Couldn't find Profile Name or ID supplied to Profile propoerty.",
                        Language.Executable,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
                dv_profile.Profile = value;
            }
        }


        public string ExecutableNameAndPath
        {
            get => rb_switch_temp.Checked && rb_launcher.Checked ? txt_executable.Text : string.Empty;
            set
            {
                if (File.Exists(txt_executable.Text))
                {
                    rb_switch_temp.Checked = true;
                    rb_launcher.Checked = true;
                    txt_executable.Text = value;
                }
            }
        }

        public uint ExecutableTimeout
        {
            get
            {

                if (rb_wait_executable.Checked)
                {
                    return (uint)nud_timeout_executable.Value;
                }

                return 0;
            }
            set
            {
                nud_timeout_executable.Value = value;
            }
        }

        public string ExecutableArguments
        {
            get => cb_args_executable.Checked ? txt_args_executable.Text : string.Empty;
            set
            {
                txt_args_executable.Text = value;
                cb_args_executable.Checked = true;
            }
        }


        public uint GameAppId
        {
            get => rb_switch_temp.Checked && rb_launcher.Checked ? (uint) nud_game_appid.Value : 0;
            set
            {
                rb_switch_temp.Checked = true;
                rb_launcher.Checked = true;
                nud_game_appid.Value = value;
            }
        }

        public string GameName
        {
            get => rb_switch_temp.Checked && rb_launcher.Checked ? txt_game_name.Text : string.Empty;
            set
            {
                rb_switch_temp.Checked = true;
                rb_launcher.Checked = true;
                txt_game_name.Text = value;
            }
        }

        public SupportedGameLibrary GameLibrary
        {
            get => rb_switch_temp.Checked && rb_launcher.Checked ? (SupportedGameLibrary) nud_game_appid.Value : SupportedGameLibrary.Unknown;
            set
            {
                rb_switch_temp.Checked = true;
                rb_launcher.Checked = true;
                switch (value)
                {
                    case SupportedGameLibrary.Steam:
                        break;

                    case SupportedGameLibrary.Uplay:
                        break;

                }
                // TODO - If SupportedGameLibrary.Unknown; then we need to show an error message.

            }
        }


        public uint GameTimeout
        {
            get
            {
                if (rb_switch_temp.Checked && rb_launcher.Checked)
                {
                    return (uint)nud_timeout_game.Value;
                }
                return 0;
            }
            set
            {
                nud_timeout_game.Value = value;
            }
        }

        public string GameArguments
        {
            get => cb_args_game.Checked ? txt_args_game.Text : string.Empty;
            set
            {
                txt_args_game.Text = value;
                cb_args_game.Checked = true;
            }
        }


        private void btn_app_executable_Click(object sender, EventArgs e)
        {
            if (dialog_open.ShowDialog(this) == DialogResult.OK)
            {
                if (File.Exists(dialog_open.FileName) && Path.GetExtension(dialog_open.FileName) == @".exe")
                {
                    txt_executable.Text = dialog_open.FileName;
                    dialog_open.FileName = string.Empty;
                }
                else
                {
                    MessageBox.Show(
                        Language.Selected_file_is_not_a_valid_executable_file,
                        Language.Executable,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
            }
        }

        private void btn_save_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.None;

            try
            {
                if (dialog_save.ShowDialog(this) == DialogResult.OK)
                {
                    if (CreateShortcut(dialog_save.FileName))
                    {
                        MessageBox.Show(
                            Language.Shortcut_place_successfully,
                            Language.Shortcut,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            Language.Failed_to_create_the_shortcut_Unexpected_exception_occurred,
                            Language.Shortcut,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
                    }

                    dialog_save.FileName = string.Empty;
                    DialogResult = DialogResult.OK;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Language.Shortcut, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ReSharper disable once FunctionComplexityOverflow
        // ReSharper disable once CyclomaticComplexity
        private bool CreateShortcut(string fileName)
        {
            var programName = Path.GetFileNameWithoutExtension(txt_executable.Text);
            var description = string.Empty;
            var icon = string.Empty;

            var args = new List<string>
            {
                // Add the SwitchProfile command as the first argument to start to switch to another profile
                $"{HeliosStartupAction.SwitchProfile}",
                // Add the Profile Name as the second argument (use that rather than ID - though ID still will work!)
                $"--profile \"{dv_profile.Profile.Name}\""
            };

            if (!Directory.Exists(IconCache))
            {
                try
                {
                    Directory.CreateDirectory(IconCache);
                }
                catch
                {
                    // ignored
                }
            }

            // Only add the rest of the options if the temporary switch radio button is set
            if (rb_switch_temp.Checked)
            {
                // Only add this set of options if the standalone programme radio button is set
                if (rb_standalone.Checked)
                {
                    // Doublecheck the Executable text field is filled in
                    if (string.IsNullOrWhiteSpace(txt_executable.Text))
                    {
                        throw new Exception(Language.Executable_address_can_not_be_empty);
                    }

                    // Doublecheck the Executable text field is a path to a real file
                    if (!File.Exists(txt_executable.Text))
                    {
                        throw new Exception(Language.Executable_file_not_found);
                    }

                    // Add the executable command and the executable name to the shortcut arguments
                    args.Add($"execute \"{txt_executable.Text.Trim()}\"");

                    // Check that the wait for executable radiobutton is on
                    if (rb_wait_executable.Checked)
                    {
                        // Doublecheck the process name has text in it
                        if (!string.IsNullOrWhiteSpace(txt_process_name.Text))
                        {
                            // Add the waitfor argument and the process name to the shortcut arguments
                            args.Add($"--waitfor \"{txt_process_name.Text.Trim()}\"");
                        }
                    }

                    // Add the timeout argument and the timeout duration in seconds to the shortcut arguments
                    args.Add($"--timeout {(int)nud_timeout_executable.Value}");

                    if (cb_args_executable.Checked && !string.IsNullOrWhiteSpace(txt_args_executable.Text))
                    {
                        args.Add($"--arguments \"{txt_args_executable.Text.Trim()}\"");
                    }

                    // Prepare text for the shortcut description field
                    description = string.Format(Language.Executing_application_with_profile, programName, Profile.Name);

                    // Grab an icon for the selected executable
                    try
                    {
                        icon = Path.Combine(IconCache, Guid.NewGuid() + ".ico");
                        new ProfileIcon(Profile).ToIconOverly(txt_executable.Text)
                            .Save(icon, MultiIconFormat.ICO);
                    }
                    catch (Exception)
                    {
                        icon = $"{txt_executable.Text.Trim()},0";
                    }
                }
                // Only add the rest of the options if the temporary switch radio button is set
                // and if the game launching radio button is set
                else if (rb_launcher.Checked)
                {
                    // TODO need to make this work so at least one game library is installed
                    // i.e. if (!SteamGame.SteamInstalled && !UplayGame.UplayInstalled )
                    if (!SteamGame.SteamInstalled)
                    {
                        throw new Exception(Language.Steam_is_not_installed);
                    }

                    // TODO - Add in Uplay game as well depending on which one was requested
                    // Add the Steam Game ID to the shortcut arguments
                    var steamGame = new SteamGame((uint) nud_game_appid.Value);
                    args.Add($"--steam {(int) nud_game_appid.Value}");

                    // Add the game timeout argument and the timeout duration in seconds to the shortcut arguments
                    args.Add($"--timeout {(int) nud_timeout_game.Value}");

                    if (cb_args_game.Checked && !string.IsNullOrWhiteSpace(txt_args_game.Text))
                    {
                        args.Add($"--arguments \"{txt_args_game.Text.Trim()}\"");
                    }

                    // Prepare text for the shortcut description field
                    description = string.Format(Language.Executing_application_with_profile, steamGame.Name,
                        Profile.Name);
                    var steamIcon = steamGame.GetIcon().Result;

                    // Grab an icon for the selected game
                    if (!string.IsNullOrWhiteSpace(steamIcon))
                    {
                        try
                        {
                            icon = Path.Combine(IconCache, Guid.NewGuid() + ".ico");
                            new ProfileIcon(Profile).ToIconOverly(steamIcon)
                                .Save(icon, MultiIconFormat.ICO);
                        }
                        catch (Exception)
                        {
                            icon = steamIcon;
                        }
                    }
                    else
                    {
                        icon = $"{SteamGame.SteamAddress},0";
                    }
                }

            }
            // Only add the rest of the options if the permanent switch radio button is set
            else
            {
                // Prepare text for the shortcut description field
                description = string.Format(Language.Switching_display_profile_to_profile, Profile.Name);

                // Grab an icon for the selected profile
                try
                {
                    icon = Path.Combine(IconCache, Guid.NewGuid() + ".ico");
                    new ProfileIcon(Profile).ToIcon().Save(icon, MultiIconFormat.ICO);
                }
                catch
                {
                    icon = string.Empty;
                }
            }

            // TODO - Make this semi-automatic if its a game to be launched. Maybe still show
            // a save prompt, but suggest the name of the game extracted from the game launcher
            // or the executable itself.

            // Now we are ready to create a shortcut based on the filename the user gave us
            fileName = Path.ChangeExtension(fileName, @"lnk");

            // If the user supplied a file
            if (fileName != null)
            {
                try
                {
                    // Remove the old file to replace it
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }

                    // Actually create the shortcut!
                    var wshShellType = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
                    dynamic wshShell = Activator.CreateInstance(wshShellType);

                    try
                    {
                        var shortcut = wshShell.CreateShortcut(fileName);

                        try
                        {
                            shortcut.TargetPath = Application.ExecutablePath;
                            shortcut.Arguments = string.Join(" ", args);
                            shortcut.Description = description;
                            shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) ??
                                                        string.Empty;

                            if (!string.IsNullOrWhiteSpace(icon))
                            {
                                shortcut.IconLocation = icon;
                            }

                            shortcut.Save();
                        }
                        finally
                        {
                            Marshal.FinalReleaseComObject(shortcut);
                        }
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(wshShell);
                    }
                }
                catch
                {
                    // Clean up a failed attempt
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }
            }

            // Return a status on how it went
            // true if it was a success or false if it was not
            return fileName != null && File.Exists(fileName);
        }

        private void nud_game_appid_ValueChanged(object sender, EventArgs e)
        {
            //lbl_steamname.Text = new SteamGame((uint) nud_game_appid.Value).ToString();
        }

        /*private void nud_steamapps_Click(object sender, EventArgs e)
        {
            var steamGamesForm = new SteamGamesForm();

            if (steamGamesForm.ShowDialog(this) == DialogResult.OK && steamGamesForm.SteamGame != null)
            {
                nud_game_appid.Value = steamGamesForm.SteamGame.AppId;
            }
        }*/

/*        private void rb_wait_process_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_wait_process.Checked)
            {
                // Enable the Process Name Text field
                txt_process_name.Enabled = true;
            }
        }
*/

        private void txt_executable_TextChanged(object sender, EventArgs e)
        {
            try
            {
                txt_process_name.Text = Path.GetFileNameWithoutExtension(txt_executable.Text)?.ToLower() ?? txt_process_name.Text;

            }
            catch
            {
                // ignored
            }
        }

        private void rb_switch_perm_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_switch_perm.Checked)
            {
                // Disable the Temporary Group
                g_temporary.Enabled = false;
            }

        }
        private void rb_switch_temp_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_switch_temp.Checked)
            {
                // Enable the Temporary Group
                g_temporary.Enabled = true;
            }

            /*g_temporary.Enabled = rb_switch_temp.Checked;

            p_standalone.Enabled = rb_standalone.Checked;
            txt_process_name.Enabled = cb_process.Checked;
            nud_timeout.Enabled = cb_process.Checked;

            p_game.Enabled = rb_launcher.Checked;

            txt_args_executable.Enabled = cb_args.Checked;

            if (rb_launcher.Checked)
            {
                nud_steamappid_ValueChanged(rb_launcher, e);
            }*/
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void rb_standalone_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_standalone.Checked)
            {
                // Enable the Standalone Panel
                p_standalone.Enabled = true;
                // Disable the Game Panel
                p_game.Enabled = false;
            }
        }

        private void rb_launcher_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_launcher.Checked)
            {
                // Enable the Game Panel
                p_game.Enabled = true;
                // Disable the Standalone Panel
                p_standalone.Enabled = false;
            }
        }


        private void cb_args_executable_CheckedChanged(object sender, EventArgs e)
        {
            // Disable the Process Name Text field
            if (cb_args_executable.Checked)
            {
                // Enable the Executable Arguments Text field
                txt_args_executable.Enabled = true;
            }
            else
            {
                // Disable the Executable Arguments Text field
                txt_args_executable.Enabled = false;
            }

        }

        private async void ShortcutForm_Load(object sender, EventArgs e)
        {

            // Make the game launcher selector read only.
            cmb_game_launcher.DropDownStyle = ComboBoxStyle.DropDownList;
            // Fill the list of supported game libraries
            foreach (var gameLibrary in Enum.GetNames(typeof(SupportedGameLibrary))) {
                if (gameLibrary != "Unknown")
                {
                    cmb_game_launcher.Items.Add(gameLibrary);
                }
            }


            // Set the Profile name
            lbl_profile.Text = $"Selected Profile: {dv_profile.Profile?.Name ?? Language.None}";

            // Start finding the games and loading the tree_games
            foreach (var game in SteamGame.GetAllOwnedGames().OrderByDescending(game => game.IsInstalled).ThenBy(game => game.Name))
                {
                var iconAddress = await game.GetIcon();

                if (!string.IsNullOrWhiteSpace(iconAddress))
                {
                    try
                    {
                        using (var fileReader = File.OpenRead(iconAddress))
                        {
                            var icon = new Icon(fileReader, il_games.ImageSize);
                            il_games.Images.Add(icon);
                        }
                    }
                    catch
                    {
                        il_games.Images.Add(Properties.Resources.SteamIcon);
                    }
                }
                else
                {
                    il_games.Images.Add(Properties.Resources.SteamIcon);
                }

                if (!Visible)
                {
                    return;
                }

                lv_games.Items.Add(new ListViewItem
                {
                    Text = game.Name,
                    Tag = game,
                    ImageIndex = il_games.Images.Count - 1
                });
            }
        }

        private void rb_wait_process_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_wait_process.Checked)
            {
                // Enable the Process Name Text field
                txt_process_name.Enabled = true;
            } else
            {
                txt_process_name.Enabled = false;
            }
        }

        private void rb_wait_executable_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_wait_executable.Checked)
            {
                // Disable the Process Name Text field
                txt_process_name.Enabled = false;
            } else
            {
                txt_process_name.Enabled = true;
            }

        }


        private void btn_app_process_Click(object sender, EventArgs e)
        {
            if (dialog_open.ShowDialog(this) == DialogResult.OK)
            {
                if (File.Exists(dialog_open.FileName) && Path.GetExtension(dialog_open.FileName) == @".exe")
                {
                    txt_process_name.Text = dialog_open.FileName;
                    dialog_open.FileName = string.Empty;
                }
                else
                {
                    MessageBox.Show(
                        Language.Selected_file_is_not_a_valid_executable_file,
                        Language.Executable,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
            }
        }

        private void cb_args_game_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_args_game.Checked)
            {
                txt_args_game.Enabled = true;
            } else
            {
                txt_args_game.Enabled = false;
            }
        }
    }
}