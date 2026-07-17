using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace NoCloudware.UI.Core.ShellExtension;

[ComVisible(true)]
[Guid("00000000-0000-0000-0000-000000000001")]
public abstract class BaseShellExtension
{
    private string[]? _selectedFiles;

    protected abstract IShellExtensionConfig GetConfig();

    public int Initialize(IntPtr pidlFolder, IntPtr pdtobj, IntPtr hkeyProgID)
    {
        if (pdtobj == IntPtr.Zero)
            return 1;

        try
        {
            var dataObject = (IDataObject)Marshal.GetObjectForIUnknown(pdtobj);
            var format = new System.Runtime.InteropServices.ComTypes.FORMATETC
            {
                cfFormat = (short)ClipboardFormats.CF_HDROP,
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
            };

            dataObject.GetData(ref format, out var medium);
            _selectedFiles = ExtractFilePaths(medium.unionmember);
            ReleaseStgMedium(ref medium);
        }
        catch
        {
            return 1;
        }

        return 0;
    }

    public int QueryContextMenu(IntPtr hMenu, uint iMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
    {
        var config = GetConfig();
        if (!config.IsEnabled || _selectedFiles == null || _selectedFiles.Length == 0)
            return 0;

        uint idCmd = idCmdFirst;
        int itemsAdded = 0;

        foreach (var item in config.MenuItems)
        {
            if (item.IsSeparator)
            {
                NativeMethods.InsertMenu(hMenu, iMenu + (uint)itemsAdded, MenuFlags.BY_POSITION | MenuFlags.SEPARATOR, IntPtr.Zero, null);
            }
            else
            {
                var flags = MenuFlags.BY_POSITION | MenuFlags.STRING;
                if (item.IsDefault)
                    flags |= MenuFlags.DEFAULT;

                NativeMethods.InsertMenu(hMenu, iMenu + (uint)itemsAdded, flags, new IntPtr(idCmd), item.Label);
                idCmd++;
            }
            itemsAdded++;
        }

        return itemsAdded;
    }

    public int InvokeCommand(IntPtr pici)
    {
        var config = GetConfig();
        if (!config.IsEnabled || _selectedFiles == null)
            return 1;

        try
        {
            var info = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
            if (info.lpVerb == IntPtr.Zero)
                return 1;

            var verb = Marshal.PtrToStringAnsi(info.lpVerb);
            if (!int.TryParse(verb, out var cmdIndex))
                return 1;

            if (cmdIndex < 0 || cmdIndex >= config.MenuItems.Count)
                return 1;

            var item = config.MenuItems[cmdIndex];
            var args = string.IsNullOrEmpty(item.Argument) ? "" : $" {item.Argument}";

            foreach (var file in _selectedFiles)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = config.ExecutablePath,
                    Arguments = $"\"{file}\"{args}",
                    UseShellExecute = false
                });
            }
        }
        catch
        {
            return 1;
        }

        return 0;
    }

    public int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax)
    {
        return unchecked((int)0x80004001);
    }

    private static string[]? ExtractFilePaths(IntPtr hDrop)
    {
        try
        {
            var fileCount = NativeMethods.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            var files = new List<string>();

            for (uint i = 0; i < fileCount; i++)
            {
                var sb = new StringBuilder(260);
                NativeMethods.DragQueryFile(hDrop, i, sb, 260);
                files.Add(sb.ToString());
            }

            return files.ToArray();
        }
        catch
        {
            return null;
        }
    }

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM pmedium);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public int fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool InsertMenu(IntPtr hMenu, uint uPosition, MenuFlags uFlags, IntPtr uIDNewItem, string? lpNewItem);
    }

    [Flags]
    private enum MenuFlags : uint
    {
        BY_POSITION = 0x00000400,
        STRING = 0x00000000,
        SEPARATOR = 0x00000800,
        DEFAULT = 0x00001000
    }

    private static class ClipboardFormats
    {
        public const int CF_HDROP = 15;
    }
}
