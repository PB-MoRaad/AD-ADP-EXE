using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace CreateADUser
{
    /// <summary>
    /// Creates a user's home directory and applies its ACL in-process, under
    /// impersonation, instead of spawning powershell.exe under alternate credentials.
    ///
    /// Why this exists:
    ///   CreateProcessWithLogonW (what ProcessStartInfo.UserName/Password uses) needs
    ///   a window station and desktop the new token can attach to, plus non-interactive
    ///   desktop heap. On servers it frequently fails with 0xC0000142
    ///   STATUS_DLL_INIT_FAILED before the target process executes a single
    ///   instruction - which is why both output streams come back empty.
    ///
    ///   Impersonating a token in the current process avoids all of it.
    ///
    /// Logon type:
    ///   LOGON32_LOGON_NEW_CREDENTIALS means "keep running locally as the current
    ///   identity, but present these credentials for network resources". That suits a
    ///   UNC home directory and needs no "Log on as a batch job" right.
    ///
    ///   Consequence: for a LOCAL path this impersonation has no effect, because local
    ///   access still uses the original identity. Home directories here are UNC, so
    ///   this is correct - but do not reuse this for local paths without switching to
    ///   LOGON32_LOGON_INTERACTIVE (2).
    /// </summary>
    public static class HomeDirectoryProvisioner
    {
        private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
        private const int LOGON32_PROVIDER_WINNT50 = 3;

        /// <summary>Attempts made when a SID has to be resolved by name.</summary>
        private const int SidLookupAttempts = 6;

        /// <summary>Delay between those attempts.</summary>
        private const int SidLookupDelayMs = 2000;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out SafeAccessTokenHandle phToken);

        /// <summary>
        /// Creates the directory if absent and grants Full Control to the target user,
        /// BUILTIN\Administrators, and the impersonated service account.
        /// Throws on failure; returns normally on success.
        /// </summary>
        /// <param name="knownUserSid">
        /// The account's SID, read from the directory object that was just created.
        /// Strongly preferred: a freshly created account often has not replicated to
        /// the domain controller this machine happens to query, so resolving the name
        /// races against replication and intermittently fails. Passing the SID removes
        /// the lookup entirely. When null, the name is resolved with retries instead.
        /// </param>
        public static void Provision(string homeDirectory, string samAccountName,
            string domain, string serviceAccountUser, string serviceAccountPassword,
            SecurityIdentifier knownUserSid = null, bool protectAcl = false)
        {
            var sw = Stopwatch.StartNew();

            Logger.Info("--------------------------------------------------");
            Logger.Info(string.Format("Provision: sam='{0}' homeDir='{1}'", samAccountName, homeDirectory));
            Logger.Debug(string.Format("Provision: impersonating {0}\\{1} (network credentials only)",
                domain, serviceAccountUser));

            if (knownUserSid != null)
                Logger.Debug(string.Format("Provision: using supplied SID {0} (no name lookup needed)",
                    knownUserSid.Value));

            SafeAccessTokenHandle token;

            if (!LogonUser(serviceAccountUser, domain, serviceAccountPassword,
                    LOGON32_LOGON_NEW_CREDENTIALS, LOGON32_PROVIDER_WINNT50, out token))
            {
                var err = Marshal.GetLastWin32Error();
                Logger.Error(string.Format(
                    "Provision: LogonUser failed for {0}\\{1}. Win32 error {2} (0x{2:X8}).",
                    domain, serviceAccountUser, err));
                throw new Win32Exception(err,
                    string.Format("LogonUser failed for {0}\\{1} (error {2}).", domain, serviceAccountUser, err));
            }

            using (token)
            {
                // Everything inside this delegate runs with the service account's
                // credentials presented to the file server.
                WindowsIdentity.RunImpersonated(token, () =>
                {
                    Logger.Debug(string.Format("Provision: impersonation active as {0}",
                        WindowsIdentity.GetCurrent().Name));

                    CreateAndSecure(homeDirectory, samAccountName, domain, knownUserSid, protectAcl);
                });
            }

            sw.Stop();
            Logger.Info(string.Format("Provision: completed for '{0}' in {1}ms",
                samAccountName, sw.ElapsedMilliseconds));
        }

        private static void CreateAndSecure(string homeDirectory, string samAccountName,
            string domain, SecurityIdentifier knownUserSid, bool protectAcl)
        {
            // --- Validate the parent -------------------------------------------------

            var parent = Path.GetDirectoryName(homeDirectory);

            if (string.IsNullOrWhiteSpace(parent))
                throw new ArgumentException(
                    string.Format("Could not determine a parent directory from '{0}'.", homeDirectory));

            if (!Directory.Exists(parent))
                throw new DirectoryNotFoundException(string.Format(
                    "Parent directory does not exist or is not reachable: {0}. Confirm the share exists and the service account has at least Read on both the share and the NTFS path.",
                    parent));

            Logger.Debug(string.Format("Provision: parent '{0}' is reachable", parent));

            // --- Establish the user's SID ----------------------------------------------

            // Resolved before creating anything, so a bad account name does not leave an
            // orphaned directory behind.
            var userSid = knownUserSid ?? ResolveSidWithRetry(samAccountName, domain);

            var identity = SafeTranslateToName(userSid, domain, samAccountName);

            // --- Create ---------------------------------------------------------------

            if (Directory.Exists(homeDirectory))
            {
                Logger.Warn(string.Format("Provision: directory already exists: {0}", homeDirectory));
            }
            else
            {
                Logger.Info(string.Format("Provision: creating {0}", homeDirectory));
                Directory.CreateDirectory(homeDirectory);
            }

            var dirInfo = new DirectoryInfo(homeDirectory);

            // --- Read the DACL ---------------------------------------------------------

            // Reading only the Access section keeps this to the DACL. A full read also
            // carries owner data, and writing that back fails on many file servers with
            // "The security identifier is not allowed to be the owner of this object".
            var acl = dirInfo.GetAccessControl(AccessControlSections.Access);

            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(NTAccount)))
            {
                Logger.Debug(string.Format("  before: {0} {1} {2} (inherited={3})",
                    rule.IdentityReference, rule.AccessControlType, rule.FileSystemRights, rule.IsInherited));
            }

            // --- Build the rules --------------------------------------------------------

            const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            const PropagationFlags prop = PropagationFlags.None;
            const FileSystemRights full = FileSystemRights.FullControl;

            Logger.Info(string.Format("Provision: granting Full Control to {0}", identity));
            acl.AddAccessRule(new FileSystemAccessRule(userSid, full, inherit, prop, AccessControlType.Allow));

            // Well-known SID, so it resolves identically regardless of machine or locale.
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Logger.Info("Provision: granting Full Control to BUILTIN\\Administrators");
            acl.AddAccessRule(new FileSystemAccessRule(adminSid, full, inherit, prop, AccessControlType.Allow));

            // Keep the service account in the DACL. Without this, protecting the ACL
            // locks it out of a folder it just created, so later cleanup and rollback
            // fail with access denied.
            try
            {
                var selfSid = WindowsIdentity.GetCurrent().User;
                Logger.Info(string.Format("Provision: granting Full Control to the executing account ({0})",
                    WindowsIdentity.GetCurrent().Name));
                acl.AddAccessRule(new FileSystemAccessRule(selfSid, full, inherit, prop, AccessControlType.Allow));
            }
            catch (Exception ex)
            {
                Logger.Warn("Provision: could not add an ACE for the executing account: " + ex.Message);
            }

            if (protectAcl)
            {
                Logger.Warn("Provision: disabling inheritance (explicit permissions only)");
                acl.SetAccessRuleProtection(true, false);
            }

            // --- Apply ------------------------------------------------------------------

            try
            {
                dirInfo.SetAccessControl(acl);
                Logger.Info("Provision: ACL applied");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(string.Format(
                    "Could not apply the ACL to '{0}'. Changing permissions requires WRITE_DAC - the service account typically needs Full Control on the parent, or ownership of the new folder.",
                    homeDirectory), ex);
            }

            // --- Verify -------------------------------------------------------------------

            var verify = new DirectoryInfo(homeDirectory).GetAccessControl(AccessControlSections.Access);
            var granted = false;

            foreach (FileSystemAccessRule rule in verify.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                Logger.Debug(string.Format("  after:  {0} {1} {2} (inherited={3})",
                    rule.IdentityReference, rule.AccessControlType, rule.FileSystemRights, rule.IsInherited));

                // FileSystemRights is a flags enum, so HasFlag is correct where a string
                // match would miss combined or numeric representations.
                if (rule.IdentityReference.Value == userSid.Value &&
                    rule.AccessControlType == AccessControlType.Allow &&
                    rule.FileSystemRights.HasFlag(full))
                {
                    granted = true;
                }
            }

            if (!granted)
                throw new InvalidOperationException(string.Format(
                    "ACL applied but verification failed: no Allow/FullControl entry for {0} was found on '{1}'.",
                    identity, homeDirectory));

            Logger.Info("Provision: permissions verified");
        }

        /// <summary>
        /// Resolves a sAMAccountName to a SID, retrying to absorb AD replication delay.
        ///
        /// Only used when the caller could not supply the SID. An account created
        /// moments ago on one domain controller may not yet exist on the one this
        /// machine queries, so the first attempt frequently fails on an account that
        /// is perfectly valid.
        /// </summary>
        private static SecurityIdentifier ResolveSidWithRetry(string samAccountName, string domain)
        {
            Logger.Warn(string.Format(
                "Provision: no SID supplied for '{0}'; falling back to a name lookup with retries. Passing the SID from the created directory object avoids this race.",
                samAccountName));

            for (var attempt = 1; attempt <= SidLookupAttempts; attempt++)
            {
                foreach (var candidate in new[] { domain + "\\" + samAccountName, samAccountName })
                {
                    try
                    {
                        var sid = (SecurityIdentifier)new NTAccount(candidate)
                            .Translate(typeof(SecurityIdentifier));

                        Logger.Debug(string.Format("Provision: resolved '{0}' to {1} on attempt {2}",
                            candidate, sid.Value, attempt));
                        return sid;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(string.Format("Provision: attempt {0} could not translate '{1}': {2}",
                            attempt, candidate, ex.Message));
                    }
                }

                if (attempt < SidLookupAttempts)
                {
                    Logger.Debug(string.Format("Provision: waiting {0}ms for replication before attempt {1}",
                        SidLookupDelayMs, attempt + 1));
                    Thread.Sleep(SidLookupDelayMs);
                }
            }

            throw new InvalidOperationException(string.Format(
                "Unable to resolve '{0}' to a SID in domain '{1}' after {2} attempts over {3} seconds. The account may not have replicated to the domain controller this server queries.",
                samAccountName, domain, SidLookupAttempts,
                (SidLookupAttempts - 1) * SidLookupDelayMs / 1000));
        }

        /// <summary>
        /// Turns a SID back into a display name. Purely cosmetic for logging, so a
        /// failure here (again, usually replication) must not fail the provisioning.
        /// </summary>
        private static string SafeTranslateToName(SecurityIdentifier sid, string domain, string samAccountName)
        {
            try
            {
                return sid.Translate(typeof(NTAccount)).Value;
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format(
                    "Provision: could not translate {0} back to a name ({1}); using '{2}\\{3}' for display only.",
                    sid.Value, ex.Message, domain, samAccountName));

                return domain + "\\" + samAccountName;
            }
        }
    }
}