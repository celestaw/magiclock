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

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetSaveFileName(ref OPENFILENAME ofn);

    /// <summary>
    /// Opens a file dialog for audio files. Returns the selected full path, or null if cancelled.
    /// </summary>
    public static string OpenAudioFileDialog()
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "Audio Files (*.wav;*.ogg;*.mp3;*.m4a)\0*.wav;*.ogg;*.mp3;*.m4a\0All Files (*.*)\0*.*\0\0";
        ofn.nFilterIndex = 1;
        ofn.lpstrFile = new string('\0', 260);
        ofn.nMaxFile = 260;
        ofn.lpstrTitle = "Select Audio File";
        ofn.Flags = 0x00000008 | 0x00001000;

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile;
        return null;
    }

    /// <summary>譜面JSONを開くダイアログ。</summary>
    public static string OpenChartFileDialog()
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "Chart JSON (*.json)\0*.json\0All Files (*.*)\0*.*\0\0";
        ofn.nFilterIndex = 1;
        ofn.lpstrFile = new string('\0', 260);
        ofn.nMaxFile = 260;
        ofn.lpstrTitle = "Open Chart";
        ofn.Flags = 0x00000008 | 0x00001000;

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile;
        return null;
    }

    /// <summary>譜面JSONの保存先を選ぶダイアログ。</summary>
    public static string SaveChartFileDialog(string defaultName = "chart")
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = "Chart JSON (*.json)\0*.json\0All Files (*.*)\0*.*\0\0";
        ofn.nFilterIndex = 1;
        ofn.lpstrFile = defaultName + ".json" + new string('\0', 260 - defaultName.Length - 5);
        ofn.nMaxFile = 260;
        ofn.lpstrTitle = "Save Chart";
        ofn.lpstrDefExt = "json";
        ofn.Flags = 0x00000008 | 0x00000002; // OFN_NOCHANGEDIR | OFN_OVERWRITEPROMPT

        if (GetSaveFileName(ref ofn))
            return ofn.lpstrFile;
        return null;
    }
}
