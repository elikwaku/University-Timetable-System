using Microsoft.AspNetCore.Identity;

namespace UniversityTimetable.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string UserType { get; set; } = "Student"; // "Admin" or "Student" or "Lecturer"
        public int? AssociatedStudentId { get; set; }
        public int? AssociatedLecturerId { get; set; }
    }
}
