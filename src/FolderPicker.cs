// The Windows Vista+ folder picker (the Explorer-style one: address bar, Quick access, search) through its COM
// interface IFileOpenDialog with FOS_PICKFOLDERS. .NET Framework's FolderBrowserDialog only shows the old tree.
// Pick() returns the chosen folder, or null when cancelled; if the COM dialog isn't available it falls back to the old one.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class FolderPicker
{
    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")] class FileOpenDialogRCW { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);                    // IModalWindow
        void SetFileTypes(uint count, IntPtr types);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint fos);
        void GetOptions(out uint fos);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int where);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string ext);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

    const uint FOS_PICKFOLDERS = 0x20, FOS_FORCEFILESYSTEM = 0x40, FOS_PATHMUSTEXIST = 0x800, FOS_NOCHANGEDIR = 0x8;
    const uint SIGDN_FILESYSPATH = 0x80058000;
    const int ERROR_CANCELLED = unchecked((int)0x800704C7);

    public static string Pick(IWin32Window owner, string title, string startFolder)
    {
        try
        {
            IFileDialog d = (IFileDialog)new FileOpenDialogRCW();
            uint o; d.GetOptions(out o);
            d.SetOptions(o | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR);
            if (!string.IsNullOrEmpty(title)) d.SetTitle(title);
            d.SetOkButtonLabel("Select folder");
            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
                try { IShellItem si; SHCreateItemFromParsingName(startFolder, IntPtr.Zero, typeof(IShellItem).GUID, out si); d.SetFolder(si); } catch { }
            int hr = d.Show(owner != null ? owner.Handle : IntPtr.Zero);
            if (hr == ERROR_CANCELLED) return null;
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            IShellItem result; d.GetResult(out result);
            string path; result.GetDisplayName(SIGDN_FILESYSPATH, out path);
            return path;
        }
        catch (Exception)
        {   // no COM dialog (very old Windows / odd shell): the old tree dialog
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = title ?? "";
                if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder)) fb.SelectedPath = startFolder;
                return fb.ShowDialog(owner) == DialogResult.OK ? fb.SelectedPath : null;
            }
        }
    }
}
