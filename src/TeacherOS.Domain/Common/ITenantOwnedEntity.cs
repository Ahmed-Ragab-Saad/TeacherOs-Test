using System;
using System.Collections.Generic;
using System.Text;

namespace TeacherOS.Domain.Common;

public interface ITenantOwnedEntity
{
    Guid TenantId { get; }
}
