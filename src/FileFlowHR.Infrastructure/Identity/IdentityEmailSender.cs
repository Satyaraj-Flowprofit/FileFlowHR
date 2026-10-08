using System.Text.Encodings.Web;
using FileFlowHR.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;

namespace FileFlowHR.Infrastructure.Identity;

/// <summary>Adapter: the built-in account pages send their emails through our email sender.</summary>
public sealed class IdentityEmailSender(IEmailSender sender) : IEmailSender<UserAccount>
{
    public Task SendConfirmationLinkAsync(UserAccount user, string email, string confirmationLink) =>
        sender.SendAsync(email, "Confirm your email",
            $"Please confirm your FileFlowHR account by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(UserAccount user, string email, string resetLink) =>
        sender.SendAsync(email, "Reset your password",
            $"Reset your FileFlowHR password by <a href='{HtmlEncoder.Default.Encode(resetLink)}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(UserAccount user, string email, string resetCode) =>
        sender.SendAsync(email, "Reset your password", $"Your FileFlowHR reset code is: {HtmlEncoder.Default.Encode(resetCode)}");
}
