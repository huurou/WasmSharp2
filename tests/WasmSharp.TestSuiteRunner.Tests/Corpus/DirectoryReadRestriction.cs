using System.Security.AccessControl;
using System.Security.Principal;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal sealed class DirectoryReadRestriction : IDisposable
{
    private readonly DirectoryInfo _directory;
    private readonly byte[]? _originalSecurity;
    private readonly UnixFileMode _originalMode;

    internal DirectoryReadRestriction(string path)
    {
        _directory = new DirectoryInfo(path);
        if (OperatingSystem.IsWindows())
        {
            var security = _directory.GetAccessControl(AccessControlSections.Access);
            _originalSecurity = security.GetSecurityDescriptorBinaryForm();
            security.AddAccessRule(
                new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.ListDirectory,
                    AccessControlType.Deny
                )
            );
            _directory.SetAccessControl(security);
        }
        else
        {
            _originalMode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(
                path,
                _originalMode & ~(UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead)
            );
        }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetSecurityDescriptorBinaryForm(_originalSecurity!, AccessControlSections.Access);
            _directory.SetAccessControl(security);
        }
        else
        {
            File.SetUnixFileMode(_directory.FullName, _originalMode);
        }
    }
}
