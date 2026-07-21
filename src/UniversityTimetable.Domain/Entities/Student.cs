using System.Collections.Generic;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class StudentGroup : BaseEntity
    {
        public string Name { get; set; } = string.Empty; // e.g. "Group A"
        public int ProgrammeId { get; set; }
        public Programme? Programme { get; set; }

        public DegreeLevel Level { get; set; } = DegreeLevel.Level100;
        public int StudentCount { get; set; }
        public bool IgnoreSplit { get; set; } = false;

        public ICollection<Student> Students { get; set; } = new List<Student>();
    }

    public class Student : BaseEntity
    {
        public string? UserId { get; set; } // Identity User FK
        public string IndexNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public int ProgrammeId { get; set; }
        public Programme? Programme { get; set; }

        public DegreeLevel Level { get; set; } = DegreeLevel.Level100;

        public int? StudentGroupId { get; set; }
        public StudentGroup? StudentGroup { get; set; }
    }
}
