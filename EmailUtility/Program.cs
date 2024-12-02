using ParametersParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailUtility
{
    class Program
    {
        static int Main(string[] args)
        {

            //string pattern = @"(?:\-)(?<name>\w*)[:= ]'(?<value>[^\']*?)'";
            // Instantiate ParameterParser and list all required & optional Parameters
            ParameterParser parser = new ParameterParser("Email Utility", "This command line tool sends email message via .NET SmtpClient Class");

            parser.Parameter("emailfrom", ParameterParser.AllowType.Required, "From address for the email message");
            parser.Parameter("emailto", ParameterParser.AllowType.Required, "Recipient address for the email message. For multiple recipients, separate each e-mail by a colon or semicolon");
            parser.Parameter("emailcc", ParameterParser.AllowType.Optional, "Carbon copy (CC) recipients for the email message. Separate each e-mail by a colon or semicolon");
            parser.Parameter("emailbcc", ParameterParser.AllowType.Optional, "Blind Carbon copy (BCC) recipients for the email message. Separate each e-mail by a colon or semicolon");
            parser.Parameter("subject", ParameterParser.AllowType.Required, "Subject line for the e-mail");
            parser.Parameter("body", ParameterParser.AllowType.Required, "Email message body");
            parser.Parameter("isbodyhtml", ParameterParser.AllowType.Optional, "Specifies whether the email message body is formatted in HTML. Set to true if the message body is in HTML format; otherwise, set to false. The default value is false.");

            try
            {
                parser.Parse(args);
                EmailUtility.SendEmail(parser);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                int errorCode = ex.HResult;             
                return (errorCode == 0 ? -1 : errorCode);
            }
        }
    }
}
