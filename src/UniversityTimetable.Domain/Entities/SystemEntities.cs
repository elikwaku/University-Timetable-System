using System;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class AISchedulingLog : BaseEntity
    {
        public int TimetableId { get; set; }
        public Timetable? Timetable { get; set; }

        public string StepName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ClashReport : BaseEntity
    {
        public int TimetableId { get; set; }
        public Timetable? Timetable { get; set; }

        public ConflictType ConflictType { get; set; }
        public ConflictSeverity Severity { get; set; }
        public string AffectedEntryIds { get; set; } = string.Empty; // Comma separated IDs
        public string Description { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public bool IsResolved { get; set; } = false;
    }

    public class AuditLog : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class Notification : BaseEntity
    {
        public string? UserId { get; set; } // Null if for all users in target role
        public string? TargetRole { get; set; } // Admin, Student, etc.
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
    }

    public class SystemSetting : BaseEntity
    {
        public string Key { get; set; } = string.Empty; // e.g., "BreakStartTime", "BreakEndTime"
        public string Value { get; set; } = string.Empty; // e.g., "12:30", "13:00"
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
    }
}
