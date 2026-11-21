using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal sealed class ReadAccessRestriction : IDisposable
{
    private readonly Action _restore;

    internal ReadAccessRestriction(DirectoryInfo directory)
    {
        _restore = OperatingSystem.IsWindows()
            ? DenyWindowsRead(
                directory.GetAccessControl(AccessControlSections.Access),
                directory.SetAccessControl,
                FileSystemRights.ListDirectory
            )
            : DenyUnixRead(directory.FullName, () => directory.GetFileSystemInfos());
    }

    internal ReadAccessRestriction(FileInfo file)
    {
        _restore = OperatingSystem.IsWindows()
            ? DenyWindowsRead(
                file.GetAccessControl(AccessControlSections.Access),
                file.SetAccessControl,
                FileSystemRights.ReadData
            )
            : DenyUnixRead(file.FullName, () => file.OpenRead().Dispose());
    }

    public void Dispose()
    {
        _restore();
    }

    [SupportedOSPlatform("windows")]
    private static Action DenyWindowsRead<TSecurity>(TSecurity security, Action<TSecurity> apply, FileSystemRights rights)
        where TSecurity : FileSystemSecurity
    {
        var original = security.GetSecurityDescriptorBinaryForm();
        security.AddAccessRule(
            new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), rights, AccessControlType.Deny)
        );
        apply(security);
        return () =>
        {
            security.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            apply(security);
        };
    }

    [UnsupportedOSPlatform("windows")]
    private static Action DenyUnixRead(string path, Action read)
    {
        var original = File.GetUnixFileMode(path);
        void Restore()
        {
            File.SetUnixFileMode(path, original);
        }

        File.SetUnixFileMode(path, original & ~(UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead));
        try
        {
            SkipIfReadable(read);
            return Restore;
        }
        catch
        {
            // コンストラクターが完了しない場合は、呼び出し元のusingでは権限を復元できない。
            Restore();
            throw;
        }
    }

    private static void SkipIfReadable(Action read)
    {
        try
        {
            read();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        Skip.Test("現在の実行環境ではUnixの読み取り権限を外してもデータを読み取れます。");
    }
}
