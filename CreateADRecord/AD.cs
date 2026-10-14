using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Security;
using System.Text;

namespace CreateADUser
{
    public static class AD
    {
        public static bool UserExists(DirectoryEntry dEntry, string UserName)
        {
            Logger.Debug(string.Format("UserExists: checking cn='{0}'", UserName));

            DirectorySearcher deSearch = new DirectorySearcher();
            deSearch.SearchRoot = dEntry;
            deSearch.Filter = "(&(objectClass=user) (cn=" + EscapeLdap(UserName) + "))";
            SearchResultCollection results = deSearch.FindAll();

            var exists = results.Count != 0;
            Logger.Debug(string.Format("UserExists: '{0}' -> {1}", UserName, exists));
            return exists;
        }

        public static void SetProperty(DirectoryEntry de, string PropertyName, string PropertyValue)
        {
            if (!string.IsNullOrEmpty(PropertyValue))
            {
                Logger.Debug(string.Format("SetProperty: {0} = '{1}'", PropertyName, PropertyValue));

                if (de.Properties.Contains(PropertyName))
                {
                    de.Properties[PropertyName][0] = PropertyValue;
                }
                else
                {
                    de.Properties[PropertyName].Add(PropertyValue);
                }
            }
            else
            {
                Logger.Debug(string.Format("SetProperty: {0} skipped (empty value)", PropertyName));
            }
        }

        public static List<String> GetGroups(DirectoryEntry tEntry, string username)
        {
            Logger.Debug(string.Format("GetGroups: reading memberOf for template '{0}'", username));

            DirectorySearcher dSearcher = new DirectorySearcher(tEntry)
            {
                Filter = "(&(objectClass=user)(sAMAccountName=" + EscapeLdap(username) + "))"
            };

            SearchResult result = dSearcher.FindOne();

            if (result != null)
            {
                DirectoryEntry user = result.GetDirectoryEntry();
                PropertyValueCollection memberOf = user.Properties["memberOf"];

                List<String> membersOf = new List<String>();
                foreach (object groupDN in memberOf)
                {
                    membersOf.Add(groupDN.ToString());
                }

                Logger.Info(string.Format("GetGroups: template '{0}' has {1} group membership(s)",
                    username, membersOf.Count));
                return membersOf;
            }

            Logger.Warn(string.Format("GetGroups: template profile '{0}' was not found", username));
            return null;
        }

        public static void AddToGroup(DirectoryEntry userDn, string groupDn)
        {
            Logger.Debug(string.Format("AddToGroup: adding to '{0}'", groupDn));

            try
            {
                DirectoryEntry GrpEntry = new DirectoryEntry(
                    "LDAP://HW3-DC01.prosperitybank.com/" + groupDn,
                    Credentials.ServiceAccountUser,
                    Credentials.ServiceAccountPassword);

                GrpEntry.Invoke("Add", new object[] { userDn.Path.ToString() });
                GrpEntry.CommitChanges();
                GrpEntry.Close();

                Logger.Debug(string.Format("AddToGroup: added to '{0}'", groupDn));
            }
            catch (Exception ex)
            {
                // Logged with the group name attached: the bare exception does not
                // say which of the many group additions failed.
                Logger.Error(string.Format("AddToGroup: failed adding to '{0}'", groupDn), ex);
                throw;
            }
        }

        public static void CreateProfileLocation(DirectoryEntry dEntry, string homeDir,
            string samAccountName, string userName, string password)
        {
            var sw = Stopwatch.StartNew();

            Logger.Info("--------------------------------------------------");
            Logger.Info(string.Format("CreateProfileLocation: sam='{0}' homeDir='{1}'",
                samAccountName, homeDir));

            try
            {
                var scriptPath = ConfigurationManager.AppSettings["scriptPath"];

                // Resolve relative paths against the assembly directory rather than the
                // process working directory, which differs between debugger and service hosts.
                if (!string.IsNullOrWhiteSpace(scriptPath) && !Path.IsPathRooted(scriptPath))
                {
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptPath);
                }

                Logger.Debug(string.Format("CreateProfileLocation: scriptPath='{0}'", scriptPath));

                // Checked up front so a missing file gives a clear message instead of
                // PowerShell's parameter-binding exit code (-196608).
                if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                {
                    throw new FileNotFoundException(
                        string.Format("PowerShell script not found at '{0}'. Check the scriptPath app setting.",
                            scriptPath), scriptPath ?? string.Empty);
                }

                SecureString securePassword = new SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }
                securePassword.MakeReadOnly();

                string arguments = string.Format(
                    "-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -userHomeDirectory \"{1}\" -userSamAccountName \"{2}\"",
                    scriptPath, homeDir, samAccountName);

                // powershell.exe is fully qualified: PATH resolution behaves differently
                // under the alternate-credential logon path.
                var powershellPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    @"WindowsPowerShell\v1.0\powershell.exe");

                var processInfo = new ProcessStartInfo
                {
                    FileName = powershellPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(powershellPath),
                    Domain = "PROSPERITY",
                    UserName = userName,
                    Password = securePassword,
                    LoadUserProfile = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Logger.Debug(string.Format("CreateProfileLocation: launching '{0}'", powershellPath));
                Logger.Debug(string.Format("CreateProfileLocation: as PROSPERITY\\{0}, workingDir='{1}'",
                    userName, processInfo.WorkingDirectory));
                Logger.Debug(string.Format("CreateProfileLocation: arguments {0}", arguments));

                using (var process = new Process { StartInfo = processInfo })
                {
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();

                    // Async reads: sequential ReadToEnd() deadlocks once either pipe
                    // buffer (~4KB) fills while the other is being drained.
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                    try
                    {
                        process.Start();
                    }
                    catch (Exception startEx)
                    {
                        // Failures here are logon or environment problems, not script
                        // problems: the script has not run at all.
                        Logger.Error(string.Format(
                            "CreateProfileLocation: process failed to start. Verify PROSPERITY\\{0} has 'Log on as a batch job' and that the profile is intact.",
                            userName), startEx);
                        throw;
                    }

                    Logger.Debug(string.Format("CreateProfileLocation: PID {0} started", process.Id));

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(120000))
                    {
                        Logger.Error("CreateProfileLocation: script exceeded the 120s timeout; killing the process");
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("PowerShell script timed out after 120 seconds.");
                    }

                    process.WaitForExit(); // Flushes the async output handlers.

                    var exitCode = process.ExitCode;
                    sw.Stop();

                    Logger.Info(string.Format("CreateProfileLocation: exit code {0} after {1}ms",
                        exitCode, sw.ElapsedMilliseconds));

                    // Both streams are logged on success as well as failure. On success
                    // they carry the resolved identity and the log path from the script.
                    if (stdout.Length > 0)
                        Logger.Debug("CreateProfileLocation: STDOUT" + Environment.NewLine + stdout.ToString().TrimEnd());

                    if (stderr.Length > 0)
                        Logger.Debug("CreateProfileLocation: STDERR" + Environment.NewLine + stderr.ToString().TrimEnd());

                    if (exitCode != 0)
                    {
                        var reason = DescribeExitCode(exitCode);

                        Logger.Error(string.Format(
                            "CreateProfileLocation: script failed. Exit {0} ({1}).{2}STDERR: {3}{2}STDOUT: {4}",
                            exitCode, reason, Environment.NewLine,
                            stderr.ToString().Trim(), stdout.ToString().Trim()));

                        throw new InvalidOperationException(string.Format(
                            "Script failed (exit {0} - {1}). Error: {2} Output: {3}",
                            exitCode, reason, stderr.ToString().Trim(), stdout.ToString().Trim()));
                    }

                    Logger.Info(string.Format("CreateProfileLocation: completed for '{0}'", samAccountName));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format(
                    "CreateProfileLocation: failed for '{0}'. Rolling back the AD account.", samAccountName), ex);

                try
                {
                    RemoveADProfile(dEntry, samAccountName);
                }
                catch (Exception rollbackEx)
                {
                    // The rollback failure must not replace the original cause, or the
                    // real reason for the failure is lost.
                    Logger.Error(string.Format(
                        "CreateProfileLocation: ROLLBACK FAILED for '{0}'. The AD account may still exist and require manual removal.",
                        samAccountName), rollbackEx);
                }

                throw; // 'throw;' not 'throw ex;' - preserves the original stack trace.
            }
        }

        /// <summary>
        /// Turns the numeric exit code into something searchable. PowerShell and Windows
        /// both return values here that mean nothing without translation.
        /// </summary>
        private static string DescribeExitCode(int exitCode)
        {
            switch (exitCode)
            {
                case 1: return "parameter validation failure";
                case 2: return "parent directory missing or inaccessible";
                case 3: return "directory creation failed";
                case 4: return "identity could not be resolved in AD";
                case 5: return "ACL read/apply/verification failed";
                case -196608: return "0xFFFD0000 PowerShell parameter binding failed - script path likely wrong";
                case -1073741502: return "0xC0000142 STATUS_DLL_INIT_FAILED - process could not initialise; check window station/desktop heap and 'Log on as a batch job'";
                case -1073741510: return "0xC000013A - process terminated by Ctrl+C or shutdown";
                default: return string.Format("0x{0:X8} unmapped", exitCode);
            }
        }

        public static SearchResult GetDirectoryEntry(DirectoryEntry dirEntry, string sAMAccountName)
        {
            // NOTE: the previous version overwrote the parameter with a hardcoded
            // "shelby.ross", so this method always returned the same account.
            Logger.Debug(string.Format("GetDirectoryEntry: looking up '{0}'", sAMAccountName));

            DirectorySearcher searcher = new DirectorySearcher(dirEntry);
            searcher.Filter = "(&(objectClass=user)(sAMAccountName=" + EscapeLdap(sAMAccountName) + "))";
            return searcher.FindOne();
        }

        public static void RemoveADProfile(DirectoryEntry de, string user)
        {
            Logger.Warn(string.Format("RemoveADProfile: rolling back AD account '{0}'", user));

            try
            {
                DirectoryEntries myEntries = de.Children;

                DirectorySearcher searcher = new DirectorySearcher(de);
                searcher.Filter = "(&(objectClass=user)(sAMAccountName=" + EscapeLdap(user) + "))";
                var result = searcher.FindOne();

                if (result == null)
                {
                    // Not an error: the account may never have been committed.
                    Logger.Warn(string.Format(
                        "RemoveADProfile: no account found for '{0}'; nothing to roll back", user));
                    return;
                }

                var userEntry = result.GetDirectoryEntry();
                Logger.Info(string.Format("RemoveADProfile: removing '{0}'", userEntry.Path));

                myEntries.Remove(userEntry);

                Logger.Info(string.Format("RemoveADProfile: '{0}' removed", user));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("RemoveADProfile: could not roll back '{0}'", user), ex);
                throw new Exception(
                    "ATTENTION: AD User Profile Processor could not roll back the unfinished AD account", ex);
            }
        }

        /// <summary>
        /// Escapes RFC 4515 special characters. Account names arrive from JSON, so an
        /// unescaped parenthesis or backslash otherwise breaks or distorts the filter.
        /// </summary>
        private static string EscapeLdap(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00")
                .Replace("/", "\\2f");
        }
    }
}