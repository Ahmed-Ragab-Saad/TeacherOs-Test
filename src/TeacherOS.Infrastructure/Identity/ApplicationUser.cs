using System;
using Microsoft.AspNetCore.Identity;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }
}
