using System.Security.AccessControl;
using System.Security.Principal;

namespace WasmSharp.TestSuiteRunner.Tests.Corpus;

internal sealed class DirectoryReadRestriction : IDisposable
{
    private readonly DirectoryInfo directory_;
    private readonly byte[]? originalSecurity_;
    private readonly UnixFileMode originalMode_;

    internal DirectoryReadRestriction(string path)
    {
        directory_ = new DirectoryInfo(path);
        if (OperatingSystem.IsWindows())
        {
            var security = directory_.GetAccessControl(AccessControlSections.Access);
            originalSecurity_ = security.GetSecurityDescriptorBinaryForm();
            security.AddAccessRule(
                new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.ListDirectory,
                    AccessControlType.Deny
                )
            );
            directory_.SetAccessControl(security);
        }
        else
        {
            originalMode_ = File.GetUnixFileMode(path);
            File.SetUnixFileMode(
                path,
                originalMode_ & ~(UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead)
            );
        }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetSecurityDescriptorBinaryForm(originalSecurity_!, AccessControlSections.Access);
            directory_.SetAccessControl(security);
        }
        else
        {
            File.SetUnixFileMode(directory_.FullName, originalMode_);
        }
    }
}
