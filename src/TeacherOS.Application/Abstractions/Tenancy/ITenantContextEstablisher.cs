using System;
using System.Collections.Generic;
using System.Text;

namespace TeacherOS.Application.Abstractions.Tenancy;

public interface ITenantContextEstablisher
{
    void Establish(Guid tenantId);
}
