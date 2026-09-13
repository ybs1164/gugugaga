using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 에디터와 스탠드얼론 빌드(Windows) 양쪽에서 동작하는 파일 대화상자 헬퍼.
    /// 에디터에서는 EditorUtility를 사용하고, 빌드본(Windows)에서는 comdlg32.dll(Win32)을 통해
    /// 네이티브 파일 열기/저장 대화상자를 호출한다.
    /// </summary>
    public static class StandaloneFileDialog
    {
        public static string OpenFilePanel(string title, string directory, string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title, directory, extension);
#elif UNITY_STANDALONE_WIN
            return OpenFilePanelWin32(title, directory, extension);
#else
            Debug.LogWarning("[StandaloneFileDialog] 이 플랫폼에서는 파일 대화상자를 지원하지 않습니다.");
            return string.Empty;
#endif
        }

        public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.SaveFilePanel(title, directory, defaultName, extension);
#elif UNITY_STANDALONE_WIN
            return SaveFilePanelWin32(title, directory, defaultName, extension);
#else
            Debug.LogWarning("[StandaloneFileDialog] 이 플랫폼에서는 파일 대화상자를 지원하지 않습니다.");
            return string.Empty;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int MaxPathLength = 2048;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public IntPtr filter = IntPtr.Zero;
            public IntPtr customFilter = IntPtr.Zero;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public IntPtr file = IntPtr.Zero;
            public int maxFile = 0;
            public IntPtr fileTitle = IntPtr.Zero;
            public int maxFileTitle = 0;
            public string initialDir = null;
            public string title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public static string OpenFilePanelWin32(string title, string directory, string extension)
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.dlgOwner = GetActiveWindow();
            ofn.title = string.IsNullOrEmpty(title) ? "파일 열기" : title;

            if (string.IsNullOrEmpty(directory))
                directory = Directory.GetCurrentDirectory();
            ofn.initialDir = Path.GetFullPath(directory);

            string extFilter = string.IsNullOrEmpty(extension) ? "*.*" : $"*.{extension}";
            string filterText = $"{extension?.ToUpperInvariant() ?? "All"} Files ({extFilter})\0{extFilter}\0All Files (*.*)\0*.*\0\0";
            byte[] filterBytes = Encoding.Unicode.GetBytes(filterText);
            IntPtr filterPtr = Marshal.AllocHGlobal(filterBytes.Length);
            Marshal.Copy(filterBytes, 0, filterPtr, filterBytes.Length);
            ofn.filter = filterPtr;

            IntPtr filePtr = Marshal.AllocHGlobal(MaxPathLength * sizeof(char));
            for (int i = 0; i < MaxPathLength * sizeof(char); i += sizeof(char))
                Marshal.WriteInt16(filePtr, i, 0);

            ofn.file = filePtr;
            ofn.maxFile = MaxPathLength;
            ofn.defExt = extension;
            ofn.flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;

            try
            {
                if (GetOpenFileName(ofn))
                {
                    string path = Marshal.PtrToStringUni(filePtr);
                    return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
                }
                return string.Empty;
            }
            finally
            {
                if (filterPtr != IntPtr.Zero) Marshal.FreeHGlobal(filterPtr);
                if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            }
        }

        public static string SaveFilePanelWin32(string title, string directory, string defaultName, string extension)
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.dlgOwner = GetActiveWindow();
            ofn.title = string.IsNullOrEmpty(title) ? "파일 저장" : title;

            if (string.IsNullOrEmpty(directory))
                directory = Directory.GetCurrentDirectory();
            ofn.initialDir = Path.GetFullPath(directory);

            string extFilter = string.IsNullOrEmpty(extension) ? "*.*" : $"*.{extension}";
            string filterText = $"{extension?.ToUpperInvariant() ?? "All"} Files ({extFilter})\0{extFilter}\0All Files (*.*)\0*.*\0\0";
            byte[] filterBytes = Encoding.Unicode.GetBytes(filterText);
            IntPtr filterPtr = Marshal.AllocHGlobal(filterBytes.Length);
            Marshal.Copy(filterBytes, 0, filterPtr, filterBytes.Length);
            ofn.filter = filterPtr;

            IntPtr filePtr = Marshal.AllocHGlobal(MaxPathLength * sizeof(char));
            for (int i = 0; i < MaxPathLength * sizeof(char); i += sizeof(char))
                Marshal.WriteInt16(filePtr, i, 0);

            if (!string.IsNullOrEmpty(defaultName))
            {
                if (!string.IsNullOrEmpty(extension) && !defaultName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
                    defaultName += "." + extension;
                byte[] nameBytes = Encoding.Unicode.GetBytes(defaultName + "\0");
                Marshal.Copy(nameBytes, 0, filePtr, Math.Min(nameBytes.Length, MaxPathLength * sizeof(char)));
            }

            ofn.file = filePtr;
            ofn.maxFile = MaxPathLength;
            ofn.defExt = extension;
            ofn.flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR;

            try
            {
                if (GetSaveFileName(ofn))
                {
                    string path = Marshal.PtrToStringUni(filePtr);
                    return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
                }
                return string.Empty;
            }
            finally
            {
                if (filterPtr != IntPtr.Zero) Marshal.FreeHGlobal(filterPtr);
                if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            }
        }
#endif
    }
}
