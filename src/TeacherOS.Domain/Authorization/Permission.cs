using System;
using System.Collections.Generic;
using System.Text;

namespace TeacherOS.Domain.Authorization;

public static class Permission
{
    public const string AttendanceRecord = "attendance.record";
    public const string PaymentRecord = "payment.record";
    public const string PaymentAdjust = "payment.adjust";
    public const string SessionClose = "session.close";
    public const string ShiftClose = "shift.close";
    public const string ContentPublish = "content.publish";
    public const string MembersManage = "members.manage";

    public static readonly IReadOnlyCollection<string> All =
    [
        AttendanceRecord,
        PaymentRecord,
        PaymentAdjust,
        SessionClose,
        ShiftClose,
        ContentPublish,
        MembersManage,
    ];
}
