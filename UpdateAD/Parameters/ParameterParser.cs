using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;

namespace ParametersParser
{
    class ParameterParser
    {
        private StringDictionary Parameters;
        private SortedDictionary<string, ParameterDefintion> NeededParameters;
        private string CommandLineName, CommandLineDesc;

        public ParameterParser(string CommandLineName = null, string CommandLineDesc = null)
        {
            Parameters = new StringDictionary();
            NeededParameters = new SortedDictionary<string, ParameterDefintion>();
            this.CommandLineName = CommandLineName;
            this.CommandLineDesc = CommandLineDesc;
        }

        private class ParameterDefintion
        {
            public ParameterDefintion(string Parameter, AllowType ParamAllowType, string Help)
            {
                this.Parameter = Parameter;
                this.ParamAllowType = ParamAllowType;
                this.Help = Help;
            }

            public string Parameter { get; set; }
            public AllowType ParamAllowType { get; set; }
            public string Help { get; set; }
        }

        public enum AllowType { Optional, Required }

        public string this[string Param]
        {
            get
            {
                if (Parameters == null) return null;
                else return (Parameters[Param]);
            }
        }

        public void Parameter(string ParameterName, AllowType ParamAllowType, string ParameterHelp)
        {
            ParameterDefintion Param = new ParameterDefintion(ParameterName, ParamAllowType, ParameterHelp);
            NeededParameters.Add(Param.Parameter, Param);
        }

        public void Parse(string[] Arguments)
        {
            string _Args = "";
            foreach (string s in Arguments)
                _Args += s + " ";

            Parse(_Args);
        }

        private void Parse(string Arguments)
        {
            if (string.IsNullOrEmpty(Arguments) || IsHelp(Arguments))
            {
                GetHelp();
                return;
            }

            //var cmdLineRegex = new Regex(@"(?:\-)(?<name>\w*)[:= ]'(?<value>\w.*?)'", 
            //    RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var cmdLineRegex = new Regex(@"(?:\-)(?<name>\w*)[:= ]'(?<value>\w.*?|[\\].*?)'",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match cmdLineMatch = cmdLineRegex.Match(Arguments.ToString());

            if (cmdLineMatch.Success)
            {
                while (cmdLineMatch.Success)
                {
                    Parameters.Add(cmdLineMatch.Groups["name"].Value,
                            cmdLineMatch.Groups["value"].Value);
                    cmdLineMatch = cmdLineMatch.NextMatch();
                }

                CheckRequiredParameters();
            }

            else throw new ExceptionSyntaxError("Argument Syntax Error Occured");
        }

        private bool IsHelp(string arg)
        {
            var helpValues = new[] { "-help", "-h" };
            return helpValues.Any(arg.Contains);
        }

        private void CheckRequiredParameters()
        {
            foreach (KeyValuePair<string, ParameterDefintion> Param in NeededParameters)
            {
                if (Param.Value.ParamAllowType == AllowType.Required)
                    if (Parameters.ContainsKey(Param.Key) == false)
                        throw new ParameterException(Param.Key + " is required");
            }
        }

        public void GetHelp()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("{0}: {1}\r\n", CommandLineName, CommandLineDesc));
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Usage:\n");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("command [options] [arguments]\r\n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Options:\n");

            var OrderedParameter = NeededParameters.OrderByDescending(p => p.Value.ParamAllowType == AllowType.Required)
                .ThenBy(s => s.Value.ParamAllowType == AllowType.Optional)
                .ThenBy(t => t.Key.Length)
                .ToList();

            var LongestParameter = NeededParameters.OrderByDescending(p => p.Value.Parameter.Length).FirstOrDefault();

            string HelpText = string.Empty;

            foreach (var Param in OrderedParameter)
            {
                HelpText += string.Format("[{0}] -{1}: {2} {3}\r\n", Param.Value.ParamAllowType, Param.Key,
                    new string(' ', LongestParameter.Key.Length - Param.Key.Length), Param.Value.Help) ;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(HelpText);
            Console.ReadLine();
        }

        public class ParameterException : Exception
        {
            public ParameterException() { HResult = -3; }
            public ParameterException(string message) : base(message) { HResult = -4; }
        }

        public class ExceptionSyntaxError : ParameterException
        {
            public ExceptionSyntaxError() { HResult = -4; }
            public ExceptionSyntaxError(string message) : base(message) { HResult = -5; }
        }

    }

}
