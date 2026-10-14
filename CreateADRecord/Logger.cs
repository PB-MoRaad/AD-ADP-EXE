using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CreateADUser
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        None = 4
    }

    /// <summary>
    /// Minimal dependency-free file logger.
    ///
    /// Design constraints:
    ///  - Must never throw. A logging failure must not break provisioning.
    ///  - Must never lose the diagnostics: falls back through several directories.
    ///  - Must never write the service account password to disk.
    /// </summary>
    public static class Logger
    {
        private static readonly object Sync = new object();

        private static string _logFile;
        private static string _failureLog;
        private static string _auditLog;
        private static bool _initialised;

        /// <summary>Correlates every line of one run, and matches the RunId in the PowerShell logs.</summary>
        public static string RunId { get; private set; }

        public static string LogFilePath { get { return _logFile; } }

        public static LogLevel MinimumLevel { get; set; }

        /// <summary>
        /// Strings that must never reach disk. Add the service account password here
        /// at startup so it is masked even if an exception message happens to contain it.
        /// </summary>
        private static readonly List<string> Secrets = new List<string>();

        static Logger()
        {
            MinimumLevel = LogLevel.Debug;
            RunId = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static void AddSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret)) return;
            lock (Sync)
            {
                if (!Secrets.Contains(secret)) Secrets.Add(secret);
            }
        }

        /// <summary>
        /// Resolves a writable log directory and opens the run. Safe to call more than once.
        /// </summary>
        public static void Initialise()
        {
            lock (Sync)
            {
                if (_initialised) return;
                _initialised = true;

                try
                {
                    var configured = ConfigurationManager.AppSettings["logDirectory"];
                    var levelText = ConfigurationManager.AppSettings["logLevel"];

                    LogLevel parsed;
                    if (!string.IsNullOrWhiteSpace(levelText) &&
                        Enum.TryParse(levelText, true, out parsed))
                    {
                        MinimumLevel = parsed;
                    }

                    // Ordered by preference. AppDomain.BaseDirectory is last because it
                    // may sit under a user profile the service account cannot reach.
                    var candidates = new[]
                    {
                        configured,
                        @"C:\PS\Logs\App",
                        @"C:\Windows\Temp\CreateADUser",
                        Path.Combine(Path.GetTempPath(), "CreateADUser"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")
                    };

                    foreach (var dir in candidates.Where(d => !string.IsNullOrWhiteSpace(d)))
                    {
                        if (!TryPrepareDirectory(dir)) continue;

                        var stamp = DateTime.Now.ToString("yyyyMMdd");
                        _logFile = Path.Combine(dir, string.Format("CreateADUser_{0}.log", stamp));
                        _failureLog = Path.Combine(dir, "_Failures.log");
                        _auditLog = Path.Combine(dir, "_Audit.log");
                        return;
                    }
                }
                catch
                {
                    // Fall through to console-only logging.
                }
            }
        }

        /// <summary>
        /// Confirms the directory exists and is genuinely writable by this identity.
        /// Directory.Exists alone proves neither.
        /// </summary>
        private static bool TryPrepareDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var probe = Path.Combine(dir, string.Format(".probe_{0}.tmp",
                    Process.GetCurrentProcess().Id));
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Debug(string message) { Write(LogLevel.Debug, message, null); }
        public static void Info(string message) { Write(LogLevel.Info, message, null); }
        public static void Warn(string message) { Write(LogLevel.Warn, message, null); }
        public static void Error(string message) { Write(LogLevel.Error, message, null); }
        public static void Error(string message, Exception ex) { Write(LogLevel.Error, message, ex); }

        private static void Write(LogLevel level, string message, Exception ex)
        {
            if (level < MinimumLevel) return;

            try
            {
                Initialise();

                var line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1,-5}] [{2}] [T{3}] {4}",
                    DateTime.Now,
                    level.ToString().ToUpperInvariant(),
                    RunId,
                    Thread.CurrentThread.ManagedThreadId,
                    Redact(message));

                var sb = new StringBuilder(line);

                if (ex != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("    --- Exception ---");
                    sb.AppendLine(Indent(Redact(DescribeException(ex))));
                    sb.Append("    -----------------");
                }

                var text = sb.ToString();

                Append(_logFile, text);

                // Errors additionally go to a rolling file that survives log rotation,
                // so failures across many runs can be reviewed in one place.
                if (level == LogLevel.Error) Append(_failureLog, text);

                // stderr for Error, stdout otherwise, so the calling process still
                // sees what it saw before.
                if (level == LogLevel.Error) Console.Error.WriteLine(line);
                else Console.WriteLine(line);
            }
            catch
            {
                // Logging must never break the caller.
            }
        }

        /// <summary>One tab-delimited row per provisioning attempt, for review in Excel.</summary>
        public static void Audit(string outcome, string samAccountName, string homeDirectory,
                                 int exitCode, long elapsedMs, string detail)
        {
            try
            {
                Initialise();

                var fields = new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    RunId,
                    outcome ?? string.Empty,
                    exitCode.ToString(),
                    samAccountName ?? string.Empty,
                    homeDirectory ?? string.Empty,
                    Environment.UserDomainName + "\\" + Environment.UserName,
                    Environment.MachineName,
                    elapsedMs + "ms",
                    Redact(Flatten(detail))
                };

                Append(_auditLog, string.Join("\t", fields));
            }
            catch
            {
            }
        }

        /// <summary>Writes the header block that opens every run's log section.</summary>
        public static void LogStartupBanner(string[] args)
        {
            Info("==================================================");
            Info(string.Format("CreateADUser starting - RunId {0}", RunId));
            Info("==================================================");
            Info(string.Format("Version:    {0}",
                FileVersionInfo.GetVersionInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion));
            Info(string.Format("Machine:    {0}", Environment.MachineName));
            Info(string.Format("Running as: {0}\\{1}", Environment.UserDomainName, Environment.UserName));
            Info(string.Format("OS:         {0}", Environment.OSVersion));
            Info(string.Format("CLR:        {0}", Environment.Version));
            Info(string.Format("Directory:  {0}", AppDomain.CurrentDomain.BaseDirectory));
            Info(string.Format("Log file:   {0}", _logFile ?? "<console only>"));

            // Arguments are logged through Redact, so the -password value is masked.
            Info(string.Format("Arguments:  {0}",
                args == null ? "<none>" : string.Join(" ", args.Select(MaskArgument))));
        }

        /// <summary>
        /// Masks the value following a password-like switch. Argument arrays are the
        /// most common accidental route for a credential to reach a log file.
        /// </summary>
        private static string MaskArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return arg;

            var lower = arg.ToLowerInvariant();
            if (lower.StartsWith("-password") || lower.StartsWith("/password") ||
                lower.StartsWith("--password") || lower.Contains("password="))
            {
                var eq = arg.IndexOf('=');
                return eq >= 0 ? arg.Substring(0, eq + 1) + "***REDACTED***" : arg;
            }

            return Redact(arg);
        }

        private static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            lock (Sync)
            {
                foreach (var secret in Secrets)
                {
                    if (!string.IsNullOrEmpty(secret))
                        text = text.Replace(secret, "***REDACTED***");
                }
            }

            return text;
        }

        /// <summary>Unwraps the full exception chain, which ToString() truncates awkwardly.</summary>
        private static string DescribeException(Exception ex)
        {
            var sb = new StringBuilder();
            var depth = 0;

            for (var current = ex; current != null; current = current.InnerException, depth++)
            {
                if (depth > 0) sb.AppendLine(new string('-', 40));

                sb.AppendLine(string.Format("Type:    {0}", current.GetType().FullName));
                sb.AppendLine(string.Format("Message: {0}", current.Message));

                var comEx = current as System.DirectoryServices.DirectoryServicesCOMException;
                if (comEx != null)
                {
                    sb.AppendLine(string.Format("LDAP:    ErrorCode={0} ExtendedError={1} ExtendedMessage={2}",
                        comEx.ErrorCode, comEx.ExtendedError, comEx.ExtendedErrorMessage));
                }

                var win32Ex = current as System.ComponentModel.Win32Exception;
                if (win32Ex != null)
                {
                    sb.AppendLine(string.Format("Win32:   NativeErrorCode={0} (0x{0:X8})", win32Ex.NativeErrorCode));
                }

                if (!string.IsNullOrEmpty(current.StackTrace))
                {
                    sb.AppendLine("Stack:");
                    sb.AppendLine(current.StackTrace);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string Indent(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return string.Join(Environment.NewLine,
                text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(l => "    " + l));
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", " >> ").Replace("\n", " >> ").Replace("\t", " ");
        }

        /// <summary>
        /// Appends with retry. Concurrent provisioning runs share the daily file, so
        /// sharing violations are expected rather than exceptional.
        /// </summary>
        private static void Append(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    lock (Sync)
                    {
                        File.AppendAllText(path, text + Environment.NewLine, Encoding.UTF8);
                    }
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(50 * attempt);
                }
                catch
                {
                    return; // Permissions or path problem: nothing more to try.
                }
            }
        }
    }
}