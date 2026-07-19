using System.Collections.Generic;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string Code { get; set; } = string.Empty; // e.g. "CS201"
        public string ShortForm { get; set; } = string.Empty; // e.g. "OOP"
        public string Title { get; set; } = string.Empty; // e.g. "Object-Oriented Programming"

        public int CreditHours { get; set; } = 3;
        public int WeeklyContactHours { get; set; } = 4; // minimum 4 contact hours per prompt requirements

        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int ProgrammeId { get; set; }
        public Programme? Programme { get; set; }

        public DegreeLevel Level { get; set; } = DegreeLevel.Level100;

        public bool PracticalRequired { get; set; } = false;
        public bool LabRequired { get; set; } = false;
        public bool ComputerLabRequired { get; set; } = false;
        public bool FieldWorkRequired { get; set; } = false;

        public int? AssignedLecturerId { get; set; }
        public Lecturer? AssignedLecturer { get; set; }
    }
}
