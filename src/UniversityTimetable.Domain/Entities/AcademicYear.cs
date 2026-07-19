using System;
using System.Collections.Generic;

namespace UniversityTimetable.Domain.Entities
{
    public class AcademicYear : BaseEntity
    {
        public string YearName { get; set; } = string.Empty; // e.g., "2025/2026"
        public bool IsCurrent { get; set; } = true;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public ICollection<Semester> Semesters { get; set; } = new List<Semester>();
    }

    public class Semester : BaseEntity
    {
        public int AcademicYearId { get; set; }
        public AcademicYear? AcademicYear { get; set; }
        public int SemesterNumber { get; set; } // 1 or 2
        public string Name => $"Semester {SemesterNumber}";
        public bool IsActive { get; set; } = true;
    }
}
