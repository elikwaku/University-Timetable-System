using System.Collections.Generic;

namespace UniversityTimetable.Domain.Entities
{
    public class Lecturer : BaseEntity
    {
        public string? UserId { get; set; } // Identity User FK
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }

        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int MaxWeeklyWorkloadHours { get; set; } = 18;
        public string AvailableDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
        public string PreferredTimeSlots { get; set; } = "Morning,Afternoon";

        public ICollection<Course> AssignedCourses { get; set; } = new List<Course>();
    }
}
