using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal sealed class FileSystemAccessRestriction : IDisposable
{
    private const UnixFileMode ReadPermissions = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const UnixFileMode WritePermissions = UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    private readonly Action _restore;

    private FileSystemAccessRestriction(Action restore)
    {
        _restore = restore;
    }

    internal static FileSystemAccessRestriction DenyListing(DirectoryInfo directory)
    {
        return OperatingSystem.IsWindows()
            ? DenyWindowsAccess(
                directory.GetAccessControl(AccessControlSections.Access),
                directory.SetAccessControl,
                FileSystemRights.ListDirectory
            )
            : DenyUnixAccess(directory.FullName, ReadPermissions, () => directory.GetFileSystemInfos());
    }

    internal static FileSystemAccessRestriction DenyRead(FileInfo file)
    {
        return OperatingSystem.IsWindows()
            ? DenyWindowsAccess(
                file.GetAccessControl(AccessControlSections.Access),
                file.SetAccessControl,
                FileSystemRights.ReadData
            )
            : DenyUnixAccess(file.FullName, ReadPermissions, () => file.OpenRead().Dispose());
    }

    internal static FileSystemAccessRestriction DenyCreatingSubdirectories(DirectoryInfo directory)
    {
        // Unixでは親ディレクトリの書き込み権限を外すため、ファイルの作成と削除も制限される。
        return OperatingSystem.IsWindows()
            ? DenyWindowsAccess(
                directory.GetAccessControl(AccessControlSections.Access),
                directory.SetAccessControl,
                FileSystemRights.CreateDirectories
            )
            : DenyUnixAccess(directory.FullName, WritePermissions, () =>
                directory.CreateSubdirectory(Guid.NewGuid().ToString("N")).Delete()
            );
    }

    public void Dispose()
    {
        _restore();
    }

    [SupportedOSPlatform("windows")]
    private static FileSystemAccessRestriction DenyWindowsAccess<TSecurity>(
        TSecurity security,
        Action<TSecurity> apply,
        FileSystemRights rights
    )
        where TSecurity : FileSystemSecurity
    {
        var original = security.GetSecurityDescriptorBinaryForm();
        security.AddAccessRule(
            new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), rights, AccessControlType.Deny)
        );
        apply(security);
        return new FileSystemAccessRestriction(() =>
        {
            security.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            apply(security);
        });
    }

    [UnsupportedOSPlatform("windows")]
    private static FileSystemAccessRestriction DenyUnixAccess(string path, UnixFileMode permissions, Action probe)
    {
        var original = File.GetUnixFileMode(path);
        void Restore()
        {
            File.SetUnixFileMode(path, original);
        }

        File.SetUnixFileMode(path, original & ~permissions);
        try
        {
            SkipIfAllowed(probe);
            return new FileSystemAccessRestriction(Restore);
        }
        catch
        {
            // 操作を制限できずスキップする場合も、一時パスの元の権限を復元する。
            Restore();
            throw;
        }
    }

    private static void SkipIfAllowed(Action probe)
    {
        try
        {
            probe();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        Skip.Test("現在の実行環境ではUnixの権限を外しても対象の操作が成功します。");
    }
}
