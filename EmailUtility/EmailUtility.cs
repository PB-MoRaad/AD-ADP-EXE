using System;
using System.Net.Mail;
using ParametersParser;


namespace EmailUtility
{
    class EmailUtility
    {
        public static void SendEmail(ParameterParser param)
        {
            try
            {
                using (MailMessage mail = new MailMessage())
                {
                    foreach (var recipient in param["emailto"].Split(';', ','))
                    {
                        mail.To.Add(recipient);
                    }

                    if (!string.IsNullOrEmpty(param["emailcc"]))
                    {
                        foreach (var recipient in param["emailcc"].Split(';', ','))
                        {
                            mail.CC.Add(recipient);
                        }
                    }

                    if (!string.IsNullOrEmpty(param["emailbcc"]))
                    {
                        foreach (var recipient in param["emailbcc"].Split(';', ','))
                        {
                            mail.Bcc.Add(recipient);
                        }
                    }

                    mail.Subject = param["subject"];

                    if (param["isbodyhtml"].ToUpper() == "TRUE")
                    {
                        mail.IsBodyHtml = true;

                    }
                    else mail.IsBodyHtml = false;

                    mail.Body = param["body"];
                    mail.From = new MailAddress(param["emailfrom"]);

                    using (var smtpClient = new SmtpClient())
                    {
                        smtpClient.Host = "smtp.prosperitybank.com";
                        smtpClient.UseDefaultCredentials = true;
                        smtpClient.Send(mail);
                    }
                }
            }
            catch (SmtpException)
            {

            }
        }
    }
}
