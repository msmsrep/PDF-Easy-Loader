using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using IComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using IDataObject = System.Windows.IDataObject;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// ドロップ／貼り付けされたデータからファイルのパスを取り出す
/// </summary>
public interface IAttachmentExtractor
{
    /// <summary>ドロップされたデータからファイルを取り出す</summary>
    IReadOnlyList<string> Extract(IDataObject data);

    /// <summary>クリップボードの内容からファイルを取り出す</summary>
    IReadOnlyList<string> ExtractFromClipboard();
}

/// <summary>
/// Outlook(Classic)のメール添付はディスク上に実体が無く、
/// FileDrop(CF_HDROP)ではなく「仮想ファイル」
/// (CFSTR_FILEDESCRIPTORW / CFSTR_FILECONTENTS)として渡される。
/// そのままではドロップも貼り付けも受け取れないため、
/// COMのIDataObjectから中身を読み出して一時フォルダへ書き出し、
/// 以降は普通のファイルと同じように扱えるようにする。
/// </summary>
public sealed class AttachmentExtractor(ITempWorkspace workspace) : IAttachmentExtractor
{
    private const string FileDescriptorFormat = "FileGroupDescriptorW";
    private const string FileContentsFormat = "FileContents";

    private const uint FileAttributeDirectory = 0x10;

    /// <summary>パス長制限に当たらないよう、書き出すファイル名を切り詰める長さ</summary>
    private const int MaxFileNameLength = 100;

    /// <summary>
    /// 受け取れるファイルを含むデータか。ドラッグ中のカーソル表示に使う
    /// </summary>
    public static bool HasFiles(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(FileDescriptorFormat);

    public IReadOnlyList<string> Extract(IDataObject data)
    {
        // エクスプローラーなど、実体のあるファイルはそのままパスが取れる
        if (data.GetDataPresent(DataFormats.FileDrop) &&
            data.GetData(DataFormats.FileDrop) is string[] paths &&
            paths.Length > 0)
        {
            return paths;
        }

        return ExtractVirtualFiles(data);
    }

    public IReadOnlyList<string> ExtractFromClipboard()
    {
        try
        {
            var data = Clipboard.GetDataObject();

            return data is null ? [] : Extract(data);
        }
        catch (ExternalException)
        {
            // 他プロセスがクリップボードをロックしている場合がある
            return [];
        }
    }

    /// <summary>
    /// 仮想ファイルを一時フォルダへ書き出す。
    /// 書き出したファイルは復号済みPDFと同じく、起動時と終了時にまとめて消える。
    /// </summary>
    private IReadOnlyList<string> ExtractVirtualFiles(IDataObject data)
    {
        // WPFのDataObjectは、他アプリ由来のデータではCOMのIDataObjectへそのまま橋渡ししてくれる
        if (data is not IComDataObject com) return [];

        var descriptors = ReadDescriptors(com);

        if (descriptors.Count == 0) return [];

        string directory = workspace.CreateWorkDirectory();
        var results = new List<string>(descriptors.Count);

        foreach (var (index, fileName) in descriptors)
        {
            string? saved = TrySaveContents(com, index, directory, fileName);

            if (saved is not null) results.Add(saved);
        }

        return results;
    }

    /// <summary>
    /// 添付の一覧（ファイル名）を読む。
    /// 中身の取得は一覧の並び順(lindex)で指定するため、元の位置も一緒に返す。
    /// </summary>
    private static List<(int Index, string FileName)> ReadDescriptors(IComDataObject com)
    {
        var format = CreateFormat(FileDescriptorFormat, lindex: -1, TYMED.TYMED_HGLOBAL);

        STGMEDIUM medium;

        try
        {
            com.GetData(ref format, out medium);
        }
        catch (Exception)
        {
            // 添付を持たないデータ（テキストのコピーなど）
            return [];
        }

        try
        {
            if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.unionmember == IntPtr.Zero) return [];

            IntPtr locked = GlobalLock(medium.unionmember);

            if (locked == IntPtr.Zero) return [];

            try
            {
                // FILEGROUPDESCRIPTORW = 件数(4バイト) + FILEDESCRIPTORW の並び
                int count = Marshal.ReadInt32(locked);
                var descriptors = new List<(int, string)>(count);

                int size = Marshal.SizeOf<FileDescriptorW>();

                for (int i = 0; i < count; i++)
                {
                    var descriptor = Marshal.PtrToStructure<FileDescriptorW>(locked + sizeof(int) + (i * size));

                    // フォルダーごとドラッグされた場合、中身を持たない項目が混ざる
                    if ((descriptor.dwFileAttributes & FileAttributeDirectory) != 0) continue;

                    descriptors.Add((i, descriptor.cFileName));
                }

                return descriptors;
            }
            finally
            {
                GlobalUnlock(medium.unionmember);
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    /// <summary>添付1件の中身をファイルへ書き出す。失敗したらnull</summary>
    private static string? TrySaveContents(IComDataObject com, int index, string directory, string fileName)
    {
        var format = CreateFormat(FileContentsFormat, index, TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL);

        STGMEDIUM medium;

        try
        {
            com.GetData(ref format, out medium);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            string path = UniquePath(directory, fileName);

            switch (medium.tymed)
            {
                case TYMED.TYMED_ISTREAM:
                    WriteStream(medium.unionmember, path);
                    break;

                case TYMED.TYMED_HGLOBAL:
                    WriteMemory(medium.unionmember, path);
                    break;

                default:
                    // メール本体(.msg)などはTYMED_ISTORAGEで渡される。PDFではないので拾わない
                    return null;
            }

            return path;
        }
        catch (Exception)
        {
            // 1件書き出せなくても残りの添付は処理する
            return null;
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static void WriteStream(IntPtr handle, string path)
    {
        if (handle == IntPtr.Zero) throw new IOException("添付の中身を取得できませんでした。");

        var stream = (IStream)Marshal.GetObjectForIUnknown(handle);

        try
        {
            using var file = File.Create(path);

            byte[] buffer = new byte[81920];
            IntPtr read = Marshal.AllocCoTaskMem(sizeof(int));

            try
            {
                while (true)
                {
                    stream.Read(buffer, buffer.Length, read);

                    int count = Marshal.ReadInt32(read);

                    if (count <= 0) break;

                    file.Write(buffer, 0, count);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(read);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    private static void WriteMemory(IntPtr handle, string path)
    {
        if (handle == IntPtr.Zero) throw new IOException("添付の中身を取得できませんでした。");

        IntPtr locked = GlobalLock(handle);

        if (locked == IntPtr.Zero) throw new IOException("添付の中身を取得できませんでした。");

        try
        {
            int size = (int)GlobalSize(handle);
            byte[] bytes = new byte[size];

            Marshal.Copy(locked, bytes, 0, size);
            File.WriteAllBytes(path, bytes);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    /// <summary>同じ名前の添付が複数あっても上書きしないようにする</summary>
    private static string UniquePath(string directory, string fileName)
    {
        string safe = string.Join("_", Path.GetFileName(fileName).Split(Path.GetInvalidFileNameChars()));

        if (string.IsNullOrWhiteSpace(safe)) safe = "attachment.pdf";

        if (safe.Length > MaxFileNameLength)
        {
            string extension = Path.GetExtension(safe);
            safe = string.Concat(safe.AsSpan(0, MaxFileNameLength - extension.Length), extension);
        }

        string path = Path.Combine(directory, safe);

        for (int i = 2; File.Exists(path); i++)
        {
            string name = $"{Path.GetFileNameWithoutExtension(safe)}({i}){Path.GetExtension(safe)}";
            path = Path.Combine(directory, name);
        }

        return path;
    }

    private static FORMATETC CreateFormat(string name, int lindex, TYMED tymed) => new()
    {
        cfFormat = (short)DataFormats.GetDataFormat(name).Id,
        ptd = IntPtr.Zero,
        dwAspect = DVASPECT.DVASPECT_CONTENT,
        lindex = lindex,
        tymed = tymed,
    };

    /// <summary>FILEDESCRIPTORW（Win32）</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct FileDescriptorW
    {
        public uint dwFlags;
        public Guid clsid;
        public int sizelCx;
        public int sizelCy;
        public int pointlX;
        public int pointlY;
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr handle);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
