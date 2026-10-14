using Newtonsoft.Json;
using ParametersParser;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Security.Principal;

namespace CreateADUser
{
    /// <summary>
    /// Central point for the service account credentials.
    ///
    /// These were previously hardcoded in four separate places. Reading them from
    /// config is a first step, not a solution: the value should be encrypted, or
    /// the account replaced with a group Managed Service Account so no password
    /// exists to leak.
    /// </summary>
    internal static class Credentials
    {
        public static string Domain
        {
            get { return ConfigurationManager.AppSettings["serviceAccountDomain"] ?? "PROSPERITY"; }
        }

        public static string ServiceAccountUser
        {
            get { return ConfigurationManager.AppSettings["serviceAccountUser"] ?? "proc-ADWriter"; }
        }

        public static string ServiceAccountPassword
        {
            get
            {
                var value = ConfigurationManager.AppSettings["serviceAccountPassword"];
                if (string.IsNullOrEmpty(value))
                {
                    throw new ConfigurationErrorsException(
                        "The 'serviceAccountPassword' app setting is missing.");
                }
                return value;
            }
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            string samAccountName = string.Empty;
            string homeDirectory = string.Empty;

            Logger.Initialise();

            // Registered before anything else so the password is masked everywhere,
            // including inside exception messages that may echo it back.
            try { Logger.AddSecret(Credentials.ServiceAccountPassword); } catch { }

            Logger.LogStartupBanner(args);

            ParameterParser parser = new ParameterParser("Create AD User Account Utility",
                "This command line tool creates Active Directory User Account");
            parser.Parameter("ldap", ParameterParser.AllowType.Required, "LDAP Connection String");
            parser.Parameter("json", ParameterParser.AllowType.Required, "JSON containing all AD Atributes");
            parser.Parameter("templateprofile", ParameterParser.AllowType.Required, "Template profile to copy group memberships from");
            parser.Parameter("password", ParameterParser.AllowType.Required, "New user default password");
            parser.Parameter("UPNFlag", ParameterParser.AllowType.Required, "UPN Flag");

            try
            {
                parser.Parse(args);

                // The new user's password is also kept out of the logs.
                Logger.AddSecret(parser["password"]);

                var originalDictionary = JsonConvert
                    .DeserializeObject<List<Dictionary<string, string>>>(parser["json"])
                    .FirstOrDefault();

                if (originalDictionary == null)
                {
                    throw new InvalidOperationException("The supplied JSON contained no attribute object.");
                }

                var ADAttributes = new Dictionary<string, string>(originalDictionary);
                string[] fieldsToRemove = { "NewHireID", "ProfileID", "cn", "userPrincipalName" };
                foreach (string field in fieldsToRemove)
                {
                    ADAttributes.Remove(field);
                }

                samAccountName = ADAttributes["saMAccountName"];
                homeDirectory = ADAttributes["homeDirectory"];

                Logger.Info(string.Format("Provisioning: sam='{0}' cn='{1}' homeDir='{2}'",
                    samAccountName, originalDictionary["cn"], homeDirectory));
                Logger.Debug(string.Format("Attributes to set: {0}",
                    string.Join(", ", ADAttributes.Keys)));

                // sAMAccountName has a hard 20-character limit. AD truncates silently,
                // producing an account whose name no longer matches the home directory
                // built from the untruncated value.
                if (!string.IsNullOrEmpty(samAccountName) && samAccountName.Length > 20)
                {
                    Logger.Error(string.Format(
                        "sAMAccountName '{0}' is {1} characters; AD will truncate it to 20. Aborting to avoid a mismatch with the home directory.",
                        samAccountName, samAccountName.Length));

                    Console.Error.WriteLine(string.Format(
                        "Error: sAMAccountName '{0}' exceeds the 20 character limit.", samAccountName));

                    Logger.Audit("REJECTED", samAccountName, homeDirectory, -1,
                        sw.ElapsedMilliseconds, "sAMAccountName too long");
                    return -1;
                }

                string connection = "LDAP://" + parser["ldap"];
                Logger.Debug(string.Format("LDAP connection: {0}", connection));

                DirectoryEntry newUser;
                using (DirectoryEntry dEntry = new DirectoryEntry(connection,
                    Credentials.ServiceAccountUser, Credentials.ServiceAccountPassword))
                {
                    if (DoesUserExist(dEntry, samAccountName))
                    {
                        Logger.Error(string.Format("Account '{0}' already exists; aborting", samAccountName));
                        Console.Error.WriteLine(
                            string.Format("Error: User with samaccount name {0} already exists", samAccountName));

                        Logger.Audit("REJECTED", samAccountName, homeDirectory, -1,
                            sw.ElapsedMilliseconds, "account already exists");
                        return -1;
                    }

                    Logger.Info(string.Format("Creating directory object CN={0}", originalDictionary["cn"]));
                    newUser = dEntry.Children.Add("CN=" + originalDictionary["cn"], "User");

                    foreach (var Attribute in ADAttributes)
                    {
                        AD.SetProperty(newUser, Attribute.Key, Attribute.Value);
                    }

                    if (parser["UPNFlag"] == "Yes")
                    {
                        string userPrincipalName = originalDictionary["userPrincipalName"];
                        if (!IsValidEmail(userPrincipalName))
                        {
                            Logger.Error(string.Format("UPN '{0}' is not a valid address", userPrincipalName));
                            Console.Error.WriteLine("Error: UPN is invalid");

                            Logger.Audit("REJECTED", samAccountName, homeDirectory, -1,
                                sw.ElapsedMilliseconds, "invalid UPN");
                            return -1;
                        }

                        AD.SetProperty(newUser, "userPrincipalName", userPrincipalName);
                    }

                    newUser.CommitChanges();
                    Logger.Info("Account object committed");

                    newUser.Properties["userAccountControl"].Value = 0x2;

                    newUser.Invoke("SetPassword", new object[] { parser["password"] });
                    Logger.Info("Initial password set");

                    newUser.Properties["pwdLastSet"].Value = 0;
                    newUser.CommitChanges();
                    Logger.Info("Password flagged for change at next logon");

                    // ------------------------------------------------------------------
                    // Capture the account's SID directly from the object just created.
                    //
                    // Looking the SID up by name instead races against AD replication:
                    // the account exists on the DC that created it, but the SID lookup
                    // goes to whichever DC this server happens to query, which may not
                    // have received it yet. That produced the intermittent
                    // "Unable to resolve ... to a SID" failure.
                    //
                    // Reading objectSid from the live directory entry is authoritative
                    // and involves no lookup at all.
                    // ------------------------------------------------------------------
                    SecurityIdentifier newUserSid = null;
                    try
                    {
                        newUser.RefreshCache(new[] { "objectSid" });
                        var sidBytes = newUser.Properties["objectSid"].Value as byte[];

                        if (sidBytes != null && sidBytes.Length > 0)
                        {
                            newUserSid = new SecurityIdentifier(sidBytes, 0);
                            Logger.Info(string.Format("Captured objectSid for '{0}': {1}",
                                samAccountName, newUserSid.Value));
                        }
                        else
                        {
                            Logger.Warn(string.Format(
                                "objectSid was empty for '{0}'; the provisioner will fall back to a name lookup with retries.",
                                samAccountName));
                        }
                    }
                    catch (Exception sidEx)
                    {
                        // Not fatal: the provisioner retries a name lookup when no SID
                        // is supplied.
                        Logger.Warn(string.Format(
                            "Could not read objectSid for '{0}' ({1}); falling back to a name lookup.",
                            samAccountName, sidEx.Message));
                    }

                    if (!string.IsNullOrEmpty(parser["templateprofile"]))
                    {
                        Logger.Info(string.Format("Copying group memberships from template '{0}'",
                            parser["templateprofile"]));

                        var memberships = new List<string>();
                        using (DirectoryEntry tEntry = new DirectoryEntry(
                            "LDAP://HW3-DC01.prosperitybank.com",
                            Credentials.ServiceAccountUser, Credentials.ServiceAccountPassword))
                        {
                            memberships = AD.GetGroups(tEntry, parser["templateprofile"]);
                        }

                        if (memberships != null)
                        {
                            foreach (var membership in memberships)
                            {
                                AD.AddToGroup(newUser, membership);
                            }
                            Logger.Info(string.Format("Applied {0} group membership(s)", memberships.Count));
                        }
                    }

                    newUser.Close();

                    // ------------------------------------------------------------------
                    // Home directory.
                    //
                    // This replaces AD.CreateProfileLocation, which launched powershell.exe
                    // under alternate credentials via CreateProcessWithLogonW. That call
                    // needs a window station, a desktop, and non-interactive desktop heap,
                    // and was failing on this server with 0xC0000142 STATUS_DLL_INIT_FAILED
                    // before PowerShell executed a single line.
                    //
                    // Provision does the same work in-process under impersonation, so there
                    // is no child process and no launch to fail. It throws on any failure
                    // and verifies the ACL before returning.
                    //
                    // Rollback is handled here rather than inside the provisioner: the
                    // provisioner reports what went wrong, the caller decides what to undo.
                    // ------------------------------------------------------------------
                    try
                    {
                        HomeDirectoryProvisioner.Provision(
                            homeDirectory,
                            samAccountName,
                            Credentials.Domain,
                            Credentials.ServiceAccountUser,
                            Credentials.ServiceAccountPassword,
                            newUserSid);
                    }
                    catch (Exception provisionEx)
                    {
                        Logger.Error(string.Format(
                            "Home directory provisioning failed for '{0}'. Rolling back the AD account.",
                            samAccountName), provisionEx);

                        try
                        {
                            AD.RemoveADProfile(dEntry, samAccountName);
                        }
                        catch (Exception rollbackEx)
                        {
                            // A rollback failure must not replace the original cause, or
                            // the real reason for the failure is lost.
                            Logger.Error(string.Format(
                                "ROLLBACK FAILED for '{0}'. The AD account may still exist and require manual removal.",
                                samAccountName), rollbackEx);
                        }

                        throw;
                    }

                    // No Directory.Exists check here.
                    //
                    // The old check ran as THIS process's identity, not as the service
                    // account that created the folder, so it could report false for a
                    // directory that exists perfectly well - and then delete a good AD
                    // account. Provision already verifies both the directory and the ACL
                    // while impersonating, and throws if either is wrong.

                    Logger.Info(string.Format("Home directory ready: {0}", homeDirectory));
                }

                sw.Stop();
                Logger.Info(string.Format("SUCCESS: '{0}' provisioned in {1}ms",
                    samAccountName, sw.ElapsedMilliseconds));
                Logger.Audit("SUCCESS", samAccountName, homeDirectory, 0, sw.ElapsedMilliseconds, string.Empty);

                return 0;
            }
            catch (JsonReaderException jex)
            {
                Logger.Error("Invalid JSON supplied on the command line", jex);
                Console.Error.WriteLine("Error: Invalid JSON format");
                Logger.Audit("FAILURE", samAccountName, homeDirectory, -1, sw.ElapsedMilliseconds, "invalid JSON");
                return -1;
            }
            catch (DirectoryServicesCOMException dex)
            {
                // ExtendedErrorMessage carries the actual LDAP reason and is not in ToString().
                Logger.Error(string.Format(
                    "AD operation failed for '{0}'. LDAP ErrorCode={1} ExtendedError={2} ExtendedMessage={3}",
                    samAccountName, dex.ErrorCode, dex.ExtendedError, dex.ExtendedErrorMessage), dex);

                Console.Error.WriteLine(string.Format(
                    "Error: User with samaccount name {0} AD creation failed. Additional Technical Error: {1}",
                    samAccountName, dex));

                Logger.Audit("FAILURE", samAccountName, homeDirectory, -1, sw.ElapsedMilliseconds,
                    "LDAP " + dex.ExtendedErrorMessage);
                return -1;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Provisioning failed for '{0}'", samAccountName), ex);

                Console.Error.WriteLine(string.Format(
                    "Error: User with samaccount name {0} AD creation failed. Additional Technical Error: {1}",
                    samAccountName, ex));

                // Points whoever receives the notification at the log rather than at a
                // stack trace they cannot act on.
                Console.Error.WriteLine(string.Format(
                    "Diagnostic log: {0} (RunId {1})", Logger.LogFilePath, Logger.RunId));

                Logger.Audit("FAILURE", samAccountName, homeDirectory, -1, sw.ElapsedMilliseconds,
                    ex.GetType().Name + ": " + ex.Message);
                return -1;
            }
        }

        public static bool DoesUserExist(DirectoryEntry de, string user)
        {
            DirectorySearcher searcher = new DirectorySearcher(de);
            searcher.Filter = "(&(objectClass=user)(sAMAccountName=" + user + "))";
            var result = searcher.FindOne();

            Logger.Debug(string.Format("DoesUserExist('{0}') -> {1}", user, result != null));
            return result != null;
        }

        public static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}