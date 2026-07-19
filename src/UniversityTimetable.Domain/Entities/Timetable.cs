using System;
using System.Collections.Generic;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class Timetable : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
        public AcademicYear? AcademicYear { get; set; }

        public int SemesterId { get; set; }
        public Semester? Semester { get; set; }

        public TimetableStatus Status { get; set; } = TimetableStatus.Draft;
        public DateTime? PublishedAt { get; set; }
        public string? CreatedByUserId { get; set; }

        public ICollection<TimetableEntry> Entries { get; set; } = new List<TimetableEntry>();
        public ICollection<ClashReport> ClashReports { get; set; } = new List<ClashReport>();
        public ICollection<AISchedulingLog> AISchedulingLogs { get; set; } = new List<AISchedulingLog>();
    }

    public class TimetableEntry : BaseEntity
    {
        public int TimetableId { get; set; }
        public Timetable? Timetable { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int LecturerId { get; set; }
        public Lecturer? Lecturer { get; set; }

        public int ClassroomId { get; set; }
        public Classroom? Classroom { get; set; }

        public int? StudentGroupId { get; set; } // Nullable if Combined Class across all groups
        public StudentGroup? StudentGroup { get; set; }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int DurationHours { get; set; } // 1 or 2 hours ONLY per prompt requirements

        public SessionType SessionType { get; set; } = SessionType.Lecture;
        public bool IsLocked { get; set; } = false;
        public string? Remarks { get; set; }
    }
}
