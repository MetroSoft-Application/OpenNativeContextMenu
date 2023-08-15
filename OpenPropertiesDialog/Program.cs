using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OpenPropertiesDialog
{
    class OpenPropertiesDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        internal struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public int fMask;
            public IntPtr hWnd;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpClass;
            public IntPtr hkeyClass;
            public int dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        internal const int SEE_MASK_INVOKEIDLIST = 0x000C;
        internal const int SEE_MASK_NOCLOSEPROCESS = 0x0040;
        internal const int SEE_MASK_FLAG_NO_UI = 0x0400;

        [DllImport("shell32.dll", EntryPoint = "ShellExecuteEx", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal extern static bool ShellExecuteEx(ref SHELLEXECUTEINFO sei);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const string DIALOG_CLASS_NAME = "#32770";

        private static string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static List<IntPtr> FindWindowsWithTitleContaining(string targetTitlePart)
        {
            var windowHandles = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                var length = GetWindowTextLength(hWnd);
                if (length == 0)
                {
                    return true;
                }

                var builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);
                var className = GetWindowClassName(hWnd);
                if (builder.ToString().Contains(targetTitlePart) && className == DIALOG_CLASS_NAME)
                {
                    windowHandles.Add(hWnd);
                }

                return true;
            }, IntPtr.Zero);

            return windowHandles;
        }

        private static string GetFileNameOrDirectoryName(string path)
        {
            return Path.GetFileName(path) ?? new DirectoryInfo(path).Name;
        }

        [STAThread]
        static void Main(string[] args)
        {
            SHELLEXECUTEINFO info;
            info = new SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_INVOKEIDLIST | SEE_MASK_FLAG_NO_UI;
            info.hWnd = IntPtr.Zero;
            info.lpFile = args[0];
            info.lpVerb = "properties";
            info.lpParameters = "\0";
            info.lpDirectory = "\0";
            info.nShow = 0;
            info.hInstApp = IntPtr.Zero;
            info.lpIDList = IntPtr.Zero;
            info.lpClass = "\0";
            info.hkeyClass = IntPtr.Zero;
            info.dwHotKey = 0;
            info.hIcon = IntPtr.Zero;
            info.hProcess = IntPtr.Zero;
            // プロパティダイアログ表示
            if (!ShellExecuteEx(ref info))
            {
                Console.WriteLine("Failed");
                return;
            };

            // プロパティダイアログが表示されるまで待つ
            var hdlList = new List<IntPtr>();
            var path = GetFileNameOrDirectoryName(args[0]);
            while (true)
            {
                hdlList = FindWindowsWithTitleContaining(path);
                if (hdlList.Count > 0)
                {
                    break;
                }
            }

            //プロパティダイアログを閉じるまで待つ
            var anyWindowClosed = false;
            while (true)
            {
                foreach (var handle in hdlList)
                {
                    if (!IsWindow(handle))
                    {
                        anyWindowClosed = true;
                        break;
                    }
                }

                if (anyWindowClosed)
                {
                    break;
                }
                // ディレイ
                Thread.Sleep(500);
            }
            CloseHandle(info.hProcess);
        }
    }
}
