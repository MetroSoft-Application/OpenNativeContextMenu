using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Globalization;

namespace OpenNativeContextMenu
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CommandOpen
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("fff3af42-25a1-4fdb-9142-b6e0c21ab32c");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandOpen"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private CommandOpen(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CommandOpen Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in CommandOpen's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CommandOpen(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                //DTEを取得し、nullチェック
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                Assumes.Present(dte);

                //選択ファイル取得
                var solutionExplorer = dte.ToolWindows.SolutionExplorer;
                var selectedItems = (Array)solutionExplorer.SelectedItems;
                if (selectedItems.Length != 1)
                {
                    return;
                }
                var firstSelectedItem = selectedItems.GetValue(0) as UIHierarchyItem;
                var projectItem = firstSelectedItem?.Object as ProjectItem;
                //選択ファイルのパスを取得
                var path = projectItem?.FileNames[1];
                if (!string.IsNullOrWhiteSpace(path))
                {
                    OpenContexMenu(path);
                    return;
                }

                var project = firstSelectedItem?.Object as Project;
                //選択プロジェクトファイルのパスを取得
                path = project?.FullName;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    OpenContexMenu(path);
                    return;
                }

                var solution = firstSelectedItem?.Object as Solution;
                //選択ソリューションファイルのパスを取得
                path = solution?.FullName;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    OpenContexMenu(path);
                    return;
                }
            }
            catch (Exception ex)
            {
                ActivityLog.LogError("OpenContextMenu", ex.ToString());
            }
        }

        /// <summary>
        /// コンテキストメニューを開く
        /// </summary>
        /// <param name="path">開く対象のファイルパス</param>
        private void OpenContexMenu(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            const string exeName = "OpenContextMenu.exe";
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var targetDirectory = Path.Combine(baseDir, @"Exec");
            var exe = Directory.GetFiles(targetDirectory, exeName, SearchOption.AllDirectories).First();
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = path
                };
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    process.WaitForExit();
                }
            }
        }
    }
}
