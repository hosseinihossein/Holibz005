using System;
using MailKit;
using MimeKit;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;

namespace Holibz005.Models
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string receiverUserName, string receiverEmail, string subject, string message);
    }

    public class EmailSender : IEmailSender
    {
        IConfiguration configuration;
        public EmailSender(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task SendEmailAsync(string receiverUserName, string receiverEmail, string subject, string message)
        {
            string? senderEmail = configuration["EmailSender:Address"];//save the email sender address in the appsettings.json
            string? senderGmailPassword = configuration["EmailSender:SenderGmailPassword"];//save the sender gmail password in the appsettings.json
            string senderName = "Admin";

            MimeMessage email = new MimeMessage();
            email.From.Add(new MailboxAddress(senderName, senderEmail));
            email.To.Add(new MailboxAddress(receiverUserName, receiverEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = message
            };

            using (SmtpClient smtp = new SmtpClient())
            {
                await smtp.ConnectAsync("smtp.gmail.com", 587, false);
                await smtp.AuthenticateAsync(senderEmail, senderGmailPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }

        }
    }
}