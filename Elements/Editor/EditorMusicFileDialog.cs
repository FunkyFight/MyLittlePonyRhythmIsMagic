using System;
using System.Runtime.InteropServices;

namespace MLP_RiM.Elements.Editor;

internal static class EditorMusicFileDialog
{
    private const int MaxPathLength = 4096;
    private const int OfnHideReadOnly = 0x00000004;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;

    public static bool TrySelectMp3(out string path, out string error)
    {
        return TrySelectFile(
            "Set beatmap song",
            "MP3 music (*.mp3)\0*.mp3\0All files (*.*)\0*.*\0\0",
            out path,
            out error);
    }

    public static bool TrySelectImage(out string path, out string error)
    {
        return TrySelectFile(
            "Import beatmap image",
            "Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0All files (*.*)\0*.*\0\0",
            out path,
            out error);
    }

    private static bool TrySelectFile(string title, string filter, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            error = "File dialog is only available on Windows";
            return false;
        }

        IntPtr filterPtr = IntPtr.Zero;
        IntPtr titlePtr = IntPtr.Zero;
        IntPtr filePtr = IntPtr.Zero;

        try
        {
            filterPtr = Marshal.StringToHGlobalUni(filter);
            titlePtr = Marshal.StringToHGlobalUni(title);
            filePtr = Marshal.AllocHGlobal(MaxPathLength * sizeof(char));
            Marshal.WriteInt16(filePtr, 0);

            OpenFileName openFileName = new()
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = filterPtr,
                lpstrFile = filePtr,
                nMaxFile = MaxPathLength,
                lpstrTitle = titlePtr,
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly
            };

            if (GetOpenFileName(ref openFileName))
            {
                path = Marshal.PtrToStringUni(filePtr) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(path);
            }

            int errorCode = CommDlgExtendedError();
            if (errorCode != 0)
                error = $"File dialog failed: 0x{errorCode:X}";

            return false;
        }
        finally
        {
            if (filterPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(filterPtr);

            if (titlePtr != IntPtr.Zero)
                Marshal.FreeHGlobal(titlePtr);

            if (filePtr != IntPtr.Zero)
                Marshal.FreeHGlobal(filePtr);
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
