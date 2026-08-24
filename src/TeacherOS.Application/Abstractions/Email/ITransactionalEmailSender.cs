using System.Threading;
using System.Threading.Tasks;

namespace TeacherOS.Application.Abstractions.Email;

public interface ITransactionalEmailSender
{
    Task<EmailDispatchResult> SendInvitationEmailAsync(
        InvitationEmailRequest request,
        CancellationToken cancellationToken = default);
}
