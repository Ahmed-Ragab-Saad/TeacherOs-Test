using System;
using System.Collections.Generic;
using System.Text;

namespace TeacherOS.Domain.Students;

public enum StudentStatus
{
    Active = 1,
    SuspendedAdministrative = 2,
    SuspendedNonPayment = 3,
    Graduated = 4,
}
