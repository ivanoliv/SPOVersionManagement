using System;
using System.IO;
using System.Windows.Forms;
using SPOVersionManagement.Forms;

namespace SPOVersionManagement
{
    public static class Program
    {
        /// <summary>
        /// Entry point when called from PowerShell via:
        ///   Add-Type -Path "SPOVersionManagement.dll"
        ///   [SPOVersionManagement.Program]::Main("C:\path\to\root")
        /// </summary>
        public static void Main(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                MessageBox.Show(
                    $"Root path not found: {rootPath}\n\nPlease provide a valid application root path.",
                    "SPO Version Management",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            LaunchUI(rootPath);
        }

        /// <summary>
        /// Entry point for standalone EXE execution.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            string rootPath = null;
            string navigateTo = null;

            // Parse arguments
            if (args != null)
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("--navigate=", StringComparison.OrdinalIgnoreCase))
                        navigateTo = arg.Substring("--navigate=".Length);
                    else if (rootPath == null && Directory.Exists(arg))
                        rootPath = arg;
                }
            }

            if (rootPath == null)
            {
                // Auto-detect: assume DLL is in src\...\bin\ — walk up to find AppPaths.json
                rootPath = FindRootPath();
            }

            if (rootPath == null)
            {
                MessageBox.Show(
                    "Could not locate the application root folder.\n\n" +
                    "Pass the root path as argument or run from the application directory.",
                    "SPO Version Management",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            LaunchUI(rootPath, navigateTo);
        }

        private static void LaunchUI(string rootPath, string navigateTo = null)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var form = new MainForm(rootPath, navigateTo);
                Application.Run(form);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fatal error:\n{ex.Message}\n\n{ex.StackTrace}",
                    "SPO Version Management - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string FindRootPath()
        {
            // Try current directory first (BaseDirectory)
            string current = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            if (File.Exists(Path.Combine(current, "config", "AppPaths.json")))
                return current;

            // If exe is in an "app" subfolder, check parent directly
            string dirName = Path.GetFileName(current);
            if (string.Equals(dirName, "app", StringComparison.OrdinalIgnoreCase))
            {
                string parent = Path.GetDirectoryName(current);
                if (parent != null && File.Exists(Path.Combine(parent, "config", "AppPaths.json")))
                    return parent;
            }

            // Try Environment.CurrentDirectory (where user launched from)
            string cwd = Environment.CurrentDirectory;
            if (File.Exists(Path.Combine(cwd, "config", "AppPaths.json")))
                return cwd;

            // Walk up from executable location
            string dir = current;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (dir == null) break;
                if (File.Exists(Path.Combine(dir, "config", "AppPaths.json")))
                    return dir;
            }

            return null;
        }
    }
}
