﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2021 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using ShareX.Properties;
using ShareX.UploadersLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

#if WindowsStore
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
#endif

namespace ShareX
{
    internal static class Program
    {
        public const string Name = "ShareX";

        public const ShareXBuild Build =
#if RELEASE
            ShareXBuild.Release;
#elif STEAM
            ShareXBuild.Steam;
#elif WindowsStore
            ShareXBuild.MicrosoftStore;
#elif DEBUG
            ShareXBuild.Debug;
#else
            ShareXBuild.Unknown;
#endif

        public static string VersionText
        {
            get
            {
                StringBuilder sbVersionText = new StringBuilder();
                Version version = Version.Parse(Application.ProductVersion);
                sbVersionText.Append(version.Major + "." + version.Minor);
                if (version.Build > 0 || version.Revision > 0) sbVersionText.Append("." + version.Build);
                if (version.Revision > 0) sbVersionText.Append("." + version.Revision);
                if (Dev) sbVersionText.Append(" Dev");
                if (Portable) sbVersionText.Append(" Portable");
                return sbVersionText.ToString();
            }
        }

        public static string Title
        {
            get
            {
                string title = $"{Name} {VersionText}";

                if (Settings != null && Settings.DevMode)
                {
                    string info = Build.ToString();

                    if (IsAdmin)
                    {
                        info += ", Admin";
                    }

                    title += $" ({info})";
                }

                return title;
            }
        }

        public static string TitleShort
        {
            get
            {
                if (Settings != null && Settings.DevMode)
                {
                    return Title;
                }

                return Name;
            }
        }

        public static bool Dev { get; } = true;
        public static bool MultiInstance { get; private set; }
        public static bool Portable { get; private set; }
        public static bool SilentRun { get; private set; }
        public static bool Sandbox { get; private set; }
        public static bool IsAdmin { get; private set; }
        public static bool SteamFirstTimeConfig { get; private set; }
        public static bool IgnoreHotkeyWarning { get; private set; }
        public static bool PuushMode { get; private set; }

        internal static ApplicationConfig Settings { get; set; }
        internal static TaskSettings DefaultTaskSettings { get; set; }
        internal static UploadersConfig UploadersConfig { get; set; }
        internal static HotkeysConfig HotkeysConfig { get; set; }

        internal static MainForm MainForm { get; private set; }
        internal static Stopwatch StartTimer { get; private set; }
        internal static HotkeyManager HotkeyManager { get; set; }
        internal static WatchFolderManager WatchFolderManager { get; set; }
        internal static GitHubUpdateManager UpdateManager { get; private set; }
        internal static ShareXCLIManager CLI { get; private set; }

        #region Paths

        private const string PersonalPathConfigFileName = "PersonalPath.cfg";

        public static readonly string DefaultPersonalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Name);
        public static readonly string PortablePersonalFolder = Helpers.GetAbsolutePath(Name);

        private static string PersonalPathConfigFilePath
        {
            get
            {
                string relativePath = Helpers.GetAbsolutePath(PersonalPathConfigFileName);

                if (File.Exists(relativePath))
                {
                    return relativePath;
                }

                return CurrentPersonalPathConfigFilePath;
            }
        }

        private static readonly string CurrentPersonalPathConfigFilePath = Path.Combine(DefaultPersonalFolder, PersonalPathConfigFileName);

        private static readonly string PreviousPersonalPathConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Name, PersonalPathConfigFileName);

        private static readonly string PortableCheckFilePath = Helpers.GetAbsolutePath("Portable");
        public static readonly string NativeMessagingHostFilePath = Helpers.GetAbsolutePath("ShareX_NativeMessagingHost.exe");
        public static readonly string SteamInAppFilePath = Helpers.GetAbsolutePath("Steam");

        private static string CustomPersonalPath { get; set; }

        public static string PersonalFolder
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPersonalPath))
                {
                    return Helpers.ExpandFolderVariables(CustomPersonalPath);
                }

                return DefaultPersonalFolder;
            }
        }

        public const string HistoryFileName = "History.json";

        public static string HistoryFilePath
        {
            get
            {
                if (Sandbox) return null;

                return Path.Combine(PersonalFolder, HistoryFileName);
            }
        }

        public const string HistoryFileNameOld = "History.xml";

        public static string HistoryFilePathOld
        {
            get
            {
                if (Sandbox) return null;

                return Path.Combine(PersonalFolder, HistoryFileNameOld);
            }
        }

        public const string LogsFolderName = "Logs";

        public static string LogsFolder => Path.Combine(PersonalFolder, LogsFolderName);

        public static string LogsFilePath
        {
            get
            {
                string fileName = string.Format("ShareX-Log-{0:yyyy-MM}.txt", DateTime.Now);
                return Path.Combine(LogsFolder, fileName);
            }
        }

        public static string RequestLogsFilePath => Path.Combine(LogsFolder, "ShareX-Request-Logs.txt");

        public static string ScreenshotsParentFolder
        {
            get
            {
                if (Settings != null && Settings.UseCustomScreenshotsPath)
                {
                    string path = Settings.CustomScreenshotsPath;
                    string path2 = Settings.CustomScreenshotsPath2;

                    if (!string.IsNullOrEmpty(path))
                    {
                        path = Helpers.ExpandFolderVariables(path);

                        if (string.IsNullOrEmpty(path2) || Directory.Exists(path))
                        {
                            return path;
                        }
                    }

                    if (!string.IsNullOrEmpty(path2))
                    {
                        path2 = Helpers.ExpandFolderVariables(path2);

                        if (Directory.Exists(path2))
                        {
                            return path2;
                        }
                    }
                }

                return Path.Combine(PersonalFolder, "Screenshots");
            }
        }

        public static string ToolsFolder => Path.Combine(PersonalFolder, "Tools");
        public static string ImageEffectsFolder => Path.Combine(PersonalFolder, "ImageEffects");
        public static string ScreenRecorderCacheFilePath => Path.Combine(PersonalFolder, "ScreenRecorder.avi");
        public static string DefaultFFmpegFilePath => Path.Combine(ToolsFolder, "ffmpeg.exe");
        public static string ChromeHostManifestFilePath => Path.Combine(ToolsFolder, "Chrome-host-manifest.json");
        public static string FirefoxHostManifestFilePath => Path.Combine(ToolsFolder, "Firefox-host-manifest.json");

        private static string PersonalPathDetectionMethod;

        #endregion Paths

        private static bool closeSequenceStarted, restartRequested, restartAsAdmin;

        [STAThread]
        private static void Main(string[] args)
        {
            // Allow Visual Studio to break on exceptions in Debug builds
#if !DEBUG
            // Add the event handler for handling UI thread exceptions to the event
            Application.ThreadException += Application_ThreadException;

            // Set the unhandled exception mode to force all Windows Forms errors to go through our handler
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Add the event handler for handling non-UI thread exceptions to the event
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif

            StartTimer = Stopwatch.StartNew(); // For be able to show startup time

            CLI = new ShareXCLIManager(args);
            CLI.ParseCommands();

#if STEAM
            if (CheckUninstall()) return; // Steam will run ShareX with -Uninstall when uninstalling
#endif

            if (CheckAdminTasks()) return; // If ShareX opened just for be able to execute task as Admin

            SystemOptions.UpdateSystemOptions();
            UpdatePersonalPath();

            DebugHelper.Init(LogsFilePath);

            MultiInstance = CLI.IsCommandExist("multi", "m");

            using (ApplicationInstanceManager instanceManager = new ApplicationInstanceManager(!MultiInstance, args, SingleInstanceCallback))
            {
                Run();
            }

            if (restartRequested)
            {
                DebugHelper.WriteLine("ShareX restarting.");

                if (restartAsAdmin)
                {
                    TaskHelpers.RunShareXAsAdmin("-silent");
                }
                else
                {
                    Process.Start(Application.ExecutablePath);
                }
            }
        }

        private static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DebugHelper.WriteLine("ShareX starting.");
            DebugHelper.WriteLine("Version: " + VersionText);
            DebugHelper.WriteLine("Build: " + Build);
            DebugHelper.WriteLine("Command line: " + Environment.CommandLine);
            DebugHelper.WriteLine("Personal path: " + PersonalFolder);
            if (!string.IsNullOrEmpty(PersonalPathDetectionMethod))
            {
                DebugHelper.WriteLine("Personal path detection method: " + PersonalPathDetectionMethod);
            }
            DebugHelper.WriteLine("Operating system: " + Helpers.GetOperatingSystemProductName(true));
            IsAdmin = Helpers.IsAdministrator();
            DebugHelper.WriteLine("Running as elevated process: " + IsAdmin);

            SilentRun = CLI.IsCommandExist("silent", "s");
#if WindowsStore
            SilentRun = SilentRun || AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
#endif

#if STEAM
            SteamFirstTimeConfig = CLI.IsCommandExist("SteamConfig");
#endif

            IgnoreHotkeyWarning = CLI.IsCommandExist("NoHotkeys");

            CreateParentFolders();
            RegisterExtensions();
            CheckPuushMode();
            DebugWriteFlags();

            SettingManager.LoadInitialSettings();

            Uploader.UpdateServicePointManager();
            UpdateManager = new GitHubUpdateManager("ShareX", "ShareX", Dev, Portable);
            LanguageHelper.ChangeLanguage(Settings.Language);
            CleanupManager.CleanupAsync();
            Helpers.TryFixHandCursor();

            DebugHelper.WriteLine("MainForm init started.");
            MainForm = new MainForm();
            DebugHelper.WriteLine("MainForm init finished.");

            Application.Run(MainForm);

            CloseSequence();
        }

        public static void CloseSequence()
        {
            if (!closeSequenceStarted)
            {
                closeSequenceStarted = true;

                DebugHelper.Logger.AsyncWrite = false;
                DebugHelper.WriteLine("ShareX closing.");

                if (WatchFolderManager != null) WatchFolderManager.Dispose();
                SettingManager.SaveAllSettings();

                DebugHelper.WriteLine("ShareX closed.");
            }
        }

        public static void Restart(bool asAdmin = false)
        {
            restartRequested = true;
            restartAsAdmin = asAdmin;
            Application.Exit();
        }

        private static void SingleInstanceCallback(object sender, InstanceCallbackEventArgs args)
        {
            if (WaitFormLoad(5000))
            {
                MainForm.InvokeSafe(() => UseCommandLineArgs(args.CommandLineArgs));
            }
        }

        private static bool WaitFormLoad(int wait)
        {
            Stopwatch timer = Stopwatch.StartNew();

            while (timer.ElapsedMilliseconds < wait)
            {
                if (MainForm != null && MainForm.IsReady) return true;

                Thread.Sleep(10);
            }

            return false;
        }

        private static void UseCommandLineArgs(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                if (MainForm.niTray != null && MainForm.niTray.Visible)
                {
                    // Workaround for Windows startup tray icon bug
                    MainForm.niTray.Visible = false;
                    MainForm.niTray.Visible = true;
                }

                MainForm.ForceActivate();
            }
            else if (MainForm.Visible)
            {
                MainForm.ForceActivate();
            }

            CLIManager cli = new CLIManager(args);
            cli.ParseCommands();

            CLI.UseCommandLineArgs(cli.Commands);
        }

        private static void UpdatePersonalPath()
        {
            Sandbox = CLI.IsCommandExist("sandbox");

            if (!Sandbox)
            {
                if (CLI.IsCommandExist("portable", "p"))
                {
                    Portable = true;
                    CustomPersonalPath = PortablePersonalFolder;
                    PersonalPathDetectionMethod = "Portable CLI flag";
                }
                else if (File.Exists(PortableCheckFilePath))
                {
                    Portable = true;
                    CustomPersonalPath = PortablePersonalFolder;
                    PersonalPathDetectionMethod = $"Portable file ({PortableCheckFilePath})";
                }
                else if (!string.IsNullOrEmpty(SystemOptions.PersonalPath))
                {
                    CustomPersonalPath = SystemOptions.PersonalPath;
                    PersonalPathDetectionMethod = "Registry";
                }
                else
                {
#if !WindowsStore
                    MigratePersonalPathConfig();
#endif

                    string customPersonalPath = ReadPersonalPathConfig();

                    if (!string.IsNullOrEmpty(customPersonalPath))
                    {
                        CustomPersonalPath = Helpers.GetAbsolutePath(customPersonalPath);
                        PersonalPathDetectionMethod = $"PersonalPath.cfg file ({PersonalPathConfigFilePath})";
                    }
                }

                if (!Directory.Exists(PersonalFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(PersonalFolder);
                    }
                    catch (Exception e)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.AppendFormat("{0} \"{1}\"", Resources.Program_Run_Unable_to_create_folder_, PersonalFolder);
                        sb.AppendLine();

                        if (!string.IsNullOrEmpty(PersonalPathDetectionMethod))
                        {
                            sb.AppendLine("Personal path detection method: " + PersonalPathDetectionMethod);
                        }

                        sb.AppendLine();
                        sb.Append(e);

                        MessageBox.Show(sb.ToString(), "ShareX - " + Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        CustomPersonalPath = "";
                    }
                }
            }
        }

        private static void CreateParentFolders()
        {
            if (!Sandbox && Directory.Exists(PersonalFolder))
            {
                Helpers.CreateDirectory(SettingManager.BackupFolder);
                Helpers.CreateDirectory(ImageEffectsFolder);
                Helpers.CreateDirectory(LogsFolder);
                Helpers.CreateDirectory(ScreenshotsParentFolder);
                Helpers.CreateDirectory(ToolsFolder);
            }
        }

        private static void RegisterExtensions()
        {
#if !WindowsStore
            if (!Portable)
            {
                if (!IntegrationHelpers.CheckCustomUploaderExtension())
                {
                    IntegrationHelpers.CreateCustomUploaderExtension(true);
                }

                if (!IntegrationHelpers.CheckImageEffectExtension())
                {
                    IntegrationHelpers.CreateImageEffectExtension(true);
                }
            }
#endif
        }

        public static void UpdateHelpersSpecialFolders()
        {
            Dictionary<string, string> specialFolders = new Dictionary<string, string>();
            specialFolders.Add("ShareXImageEffects", ImageEffectsFolder);
            HelpersOptions.ShareXSpecialFolders = specialFolders;
        }

        private static void MigratePersonalPathConfig()
        {
            if (File.Exists(PreviousPersonalPathConfigFilePath))
            {
                try
                {
                    if (!File.Exists(CurrentPersonalPathConfigFilePath))
                    {
                        Helpers.CreateDirectoryFromFilePath(CurrentPersonalPathConfigFilePath);
                        File.Move(PreviousPersonalPathConfigFilePath, CurrentPersonalPathConfigFilePath);
                    }

                    File.Delete(PreviousPersonalPathConfigFilePath);
                    Directory.Delete(Path.GetDirectoryName(PreviousPersonalPathConfigFilePath));
                }
                catch (Exception e)
                {
                    e.ShowError();
                }
            }
        }

        public static string ReadPersonalPathConfig()
        {
            if (File.Exists(PersonalPathConfigFilePath))
            {
                return File.ReadAllText(PersonalPathConfigFilePath, Encoding.UTF8).Trim();
            }

            return "";
        }

        public static bool WritePersonalPathConfig(string path)
        {
            if (path == null)
            {
                path = "";
            }
            else
            {
                path = path.Trim();
            }

            bool isDefaultPath = string.IsNullOrEmpty(path) && !File.Exists(PersonalPathConfigFilePath);

            if (!isDefaultPath)
            {
                string currentPath = ReadPersonalPathConfig();

                if (!path.Equals(currentPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        Helpers.CreateDirectoryFromFilePath(PersonalPathConfigFilePath);
                        File.WriteAllText(PersonalPathConfigFilePath, path, Encoding.UTF8);
                        return true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        MessageBox.Show(string.Format(Resources.Program_WritePersonalPathConfig_Cant_access_to_file, PersonalPathConfigFilePath),
                            "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }

            return false;
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            OnError(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            OnError((Exception)e.ExceptionObject);
        }

        private static void OnError(Exception e)
        {
            using (ErrorForm errorForm = new ErrorForm(e.Message, $"{e}\r\n\r\n{Title}", LogsFilePath, Links.URL_ISSUES))
            {
                errorForm.ShowDialog();
            }
        }

        private static bool CheckAdminTasks()
        {
            if (CLI.IsCommandExist("dnschanger"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Helpers.TryFixHandCursor();
                Application.Run(new DNSChangerForm());
                return true;
            }

            return false;
        }

        private static bool CheckUninstall()
        {
            if (CLI.IsCommandExist("uninstall"))
            {
                try
                {
                    IntegrationHelpers.Uninstall();
                }
                catch
                {
                }

                return true;
            }

            return false;
        }

        private static bool CheckPuushMode()
        {
            string puushPath = Helpers.GetAbsolutePath("puush");
            PuushMode = File.Exists(puushPath);
            return PuushMode;
        }

        private static void DebugWriteFlags()
        {
            List<string> flags = new List<string>();

            if (Dev) flags.Add(nameof(Dev));
            if (MultiInstance) flags.Add(nameof(MultiInstance));
            if (Portable) flags.Add(nameof(Portable));
            if (SilentRun) flags.Add(nameof(SilentRun));
            if (Sandbox) flags.Add(nameof(Sandbox));
            if (SteamFirstTimeConfig) flags.Add(nameof(SteamFirstTimeConfig));
            if (IgnoreHotkeyWarning) flags.Add(nameof(IgnoreHotkeyWarning));
            if (SystemOptions.DisableUpdateCheck) flags.Add(nameof(SystemOptions.DisableUpdateCheck));
            if (SystemOptions.DisableUpload) flags.Add(nameof(SystemOptions.DisableUpload));
            if (PuushMode) flags.Add(nameof(PuushMode));

            string output = string.Join(", ", flags);

            if (!string.IsNullOrEmpty(output))
            {
                DebugHelper.WriteLine("Flags: " + output);
            }
        }
    }
}