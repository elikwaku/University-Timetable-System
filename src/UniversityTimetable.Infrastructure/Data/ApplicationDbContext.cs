using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Identity;

namespace UniversityTimetable.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Programme> Programmes { get; set; } = null!;
        public DbSet<AcademicYear> AcademicYears { get; set; } = null!;
        public DbSet<Semester> Semesters { get; set; } = null!;
        public DbSet<Building> Buildings { get; set; } = null!;
        public DbSet<Classroom> Classrooms { get; set; } = null!;
        public DbSet<Lecturer> Lecturers { get; set; } = null!;
        public DbSet<StudentGroup> StudentGroups { get; set; } = null!;
        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<Timetable> Timetables { get; set; } = null!;
        public DbSet<TimetableEntry> TimetableEntries { get; set; } = null!;
        public DbSet<AISchedulingLog> AISchedulingLogs { get; set; } = null!;
        public DbSet<ClashReport> ClashReports { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Global Query Filter for Soft Delete
            builder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Programme>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<AcademicYear>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Semester>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Building>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Classroom>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Lecturer>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<StudentGroup>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Student>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Course>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<Timetable>().HasQueryFilter(e => !e.IsDeleted);
            builder.Entity<TimetableEntry>().HasQueryFilter(e => !e.IsDeleted);

            // Relationships and Indexes
            builder.Entity<Department>()
                .HasIndex(d => d.Code).IsUnique();

            builder.Entity<Programme>()
                .HasIndex(p => p.Code).IsUnique();

            builder.Entity<Course>()
                .HasIndex(c => c.Code).IsUnique();

            builder.Entity<Lecturer>()
                .HasIndex(l => l.EmployeeId).IsUnique();

            builder.Entity<Student>()
                .HasIndex(s => s.IndexNumber).IsUnique();

            builder.Entity<TimetableEntry>()
                .HasOne(e => e.Timetable)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.TimetableId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TimetableEntry>()
                .HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TimetableEntry>()
                .HasOne(e => e.Lecturer)
                .WithMany()
                .HasForeignKey(e => e.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TimetableEntry>()
                .HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TimetableEntry>()
                .HasOne(e => e.StudentGroup)
                .WithMany()
                .HasForeignKey(e => e.StudentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
