using System.Collections.Generic;

namespace UniversityTimetable.Domain.Entities
{
    public class Department : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Faculty { get; set; } = string.Empty;

        public ICollection<Programme> Programmes { get; set; } = new List<Programme>();
        public ICollection<Lecturer> Lecturers { get; set; } = new List<Lecturer>();
        public ICollection<Course> Courses { get; set; } = new List<Course>();
    }
}
