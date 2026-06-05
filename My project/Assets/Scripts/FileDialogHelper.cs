using System;
using System.Runtime.InteropServices;

/// <summary>
/// Win32 GetOpenFileName wrapper. Unity runtime (Windows) で動作する。
/// </summary>
public static class FileDialogHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    /// <summary>
    /// Opens a file dialog for audio files. Returns the selected full path, or null if cancelled.
    /// </summary>
    public static string OpenAudioFileDialog()
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "Audio Files (*.wav;*.ogg)\0*.wav;*.ogg\0All Files (*.*)\0*.*\0\0";
        ofn.nFilterIndex = 1;
        ofn.lpstrFile = new string('\0', 260);
        ofn.nMaxFile = 260;
        ofn.lpstrTitle = "Select Audio File";
        ofn.Flags = 0x00000008 /*OFN_NOCHANGEDIR*/ | 0x00001000 /*OFN_FILEMUSTEXIST*/;

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile;
        return null;
    }
}
