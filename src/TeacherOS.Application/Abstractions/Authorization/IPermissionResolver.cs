using System;
using System.Collections.Generic;
using System.Text;

namespace TeacherOS.Application.Abstractions.Authorization;

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken);
}
