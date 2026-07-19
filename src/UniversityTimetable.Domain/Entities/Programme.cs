using System.Collections.Generic;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class Programme : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public ICollection<Course> Courses { get; set; } = new List<Course>();
        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<StudentGroup> StudentGroups { get; set; } = new List<StudentGroup>();
    }
}
