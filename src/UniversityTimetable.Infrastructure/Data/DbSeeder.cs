using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;
using UniversityTimetable.Infrastructure.Identity;

namespace UniversityTimetable.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            await context.Database.EnsureCreatedAsync();

            // 1. Roles
            string[] roles = new[] { "SuperAdmin", "TimetableAdmin", "DepartmentAdmin", "Student" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. System Settings
            if (!await context.SystemSettings.AnyAsync())
            {
                context.SystemSettings.AddRange(new List<SystemSetting>
                {
                    new SystemSetting { Key = "BreakStartTime", Value = "12:30", Category = "Scheduling", Description = "Official university daily break start time" },
                    new SystemSetting { Key = "BreakEndTime", Value = "13:00", Category = "Scheduling", Description = "Official university daily break end time" },
                    new SystemSetting { Key = "MinWeeklyContactHours", Value = "4", Category = "Academic", Description = "Minimum required weekly contact hours per course" },
                    new SystemSetting { Key = "MaxDailyLecturerHours", Value = "6", Category = "Scheduling", Description = "Maximum teaching hours allowed per day for a lecturer" }
                });
                await context.SaveChangesAsync();
            }

            // 3. Departments & Programmes
            if (!await context.Departments.AnyAsync())
            {
                var csDept = new Department
                {
                    Code = "CS",
                    Name = "Computer Science & Software Engineering",
                    Faculty = "School of Technology & Engineering"
                };

                var busDept = new Department
                {
                    Code = "BUS",
                    Name = "Business & Finance",
                    Faculty = "School of Business"
                };

                var sciDept = new Department
                {
                    Code = "BIO",
                    Name = "Biological & Environmental Sciences",
                    Faculty = "School of Science"
                };

                context.Departments.AddRange(csDept, busDept, sciDept);
                await context.SaveChangesAsync();

                var csProg = new Programme
                {
                    Code = "BSC-CS",
                    Name = "BSc Computer Science",
                    DepartmentId = csDept.Id
                };

                var busProg = new Programme
                {
                    Code = "BSC-BA",
                    Name = "BSc Business Administration",
                    DepartmentId = busDept.Id
                };

                context.Programmes.AddRange(csProg, busProg);
                await context.SaveChangesAsync();
            }

            // 4. Academic Year & Semester
            if (!await context.AcademicYears.AnyAsync())
            {
                var year = new AcademicYear
                {
                    YearName = "2025/2026",
                    IsCurrent = true,
                    StartDate = new DateTime(2025, 9, 1),
                    EndDate = new DateTime(2026, 6, 30)
                };
                context.AcademicYears.Add(year);
                await context.SaveChangesAsync();

                var semester = new Semester
                {
                    AcademicYearId = year.Id,
                    SemesterNumber = 1,
                    IsActive = true
                };
                context.Semesters.Add(semester);
                await context.SaveChangesAsync();
            }

            // 5. Buildings & Classrooms
            if (!await context.Buildings.AnyAsync())
            {
                var bldgA = new Building { Code = "ENG-A", Name = "Engineering Building Block A" };
                var bldgB = new Building { Code = "SCI-B", Name = "Science Complex Block B" };
                var bldgC = new Building { Code = "BUS-C", Name = "Business Hall Block C" };

                context.Buildings.AddRange(bldgA, bldgB, bldgC);
                await context.SaveChangesAsync();

                context.Classrooms.AddRange(new List<Classroom>
                {
                    new Classroom { BuildingId = bldgA.Id, RoomNumber = "ENG-101", Capacity = 80, RoomType = RoomType.LectureHall },
                    new Classroom { BuildingId = bldgA.Id, RoomNumber = "COMP-LAB-1", Capacity = 60, RoomType = RoomType.ComputerLab },
                    new Classroom { BuildingId = bldgA.Id, RoomNumber = "ENG-103", Capacity = 40, RoomType = RoomType.Lab },
                    new Classroom { BuildingId = bldgB.Id, RoomNumber = "AUDITORIUM-1", Capacity = 150, RoomType = RoomType.LectureHall },
                    new Classroom { BuildingId = bldgB.Id, RoomNumber = "SCI-LAB-1", Capacity = 40, RoomType = RoomType.Lab },
                    new Classroom { BuildingId = bldgB.Id, RoomNumber = "SCI-201", Capacity = 50, RoomType = RoomType.SeminarRoom },
                    new Classroom { BuildingId = bldgC.Id, RoomNumber = "HALL-C1", Capacity = 100, RoomType = RoomType.LectureHall },
                    new Classroom { BuildingId = bldgC.Id, RoomNumber = "HALL-C2", Capacity = 70, RoomType = RoomType.LectureHall }
                });
                await context.SaveChangesAsync();
            }

            // 6. Lecturers
            if (!await context.Lecturers.AnyAsync())
            {
                var csDept = await context.Departments.FirstAsync(d => d.Code == "CS");
                var busDept = await context.Departments.FirstAsync(d => d.Code == "BUS");
                var sciDept = await context.Departments.FirstAsync(d => d.Code == "BIO");

                var lec1 = new Lecturer
                {
                    EmployeeId = "LEC-001",
                    FullName = "Dr. Alan Turing",
                    Email = "alan.turing@university.edu",
                    DepartmentId = csDept.Id,
                    MaxWeeklyWorkloadHours = 18,
                    AvailableDays = "Monday,Tuesday,Wednesday,Thursday,Friday"
                };

                var lec2 = new Lecturer
                {
                    EmployeeId = "LEC-002",
                    FullName = "Dr. Grace Hopper",
                    Email = "grace.hopper@university.edu",
                    DepartmentId = csDept.Id,
                    MaxWeeklyWorkloadHours = 18,
                    AvailableDays = "Monday,Tuesday,Wednesday,Thursday,Friday"
                };

                var lec3 = new Lecturer
                {
                    EmployeeId = "LEC-003",
                    FullName = "Prof. Peter Drucker",
                    Email = "peter.drucker@university.edu",
                    DepartmentId = busDept.Id,
                    MaxWeeklyWorkloadHours = 16,
                    AvailableDays = "Monday,Tuesday,Wednesday,Thursday,Friday"
                };

                var lec4 = new Lecturer
                {
                    EmployeeId = "LEC-004",
                    FullName = "Dr. Rosalind Franklin",
                    Email = "rosalind.franklin@university.edu",
                    DepartmentId = sciDept.Id,
                    MaxWeeklyWorkloadHours = 18,
                    AvailableDays = "Monday,Tuesday,Wednesday,Thursday,Friday"
                };

                context.Lecturers.AddRange(lec1, lec2, lec3, lec4);
                await context.SaveChangesAsync();
            }

            // 7. Student Groups & Students
            var csProgObj = await context.Programmes.FirstAsync(p => p.Code == "BSC-CS");
            var busProgObj = await context.Programmes.FirstAsync(p => p.Code == "BSC-BA");

            if (!await context.StudentGroups.AnyAsync())
            {
                // Level 200 CS has 120 students -> Auto split into Group A (60) & Group B (60)
                var grpA = new StudentGroup
                {
                    Name = "Group A",
                    ProgrammeId = csProgObj.Id,
                    Level = DegreeLevel.Level200,
                    StudentCount = 60
                };
                var grpB = new StudentGroup
                {
                    Name = "Group B",
                    ProgrammeId = csProgObj.Id,
                    Level = DegreeLevel.Level200,
                    StudentCount = 60
                };
                var grp100 = new StudentGroup
                {
                    Name = "Level 100 Main",
                    ProgrammeId = csProgObj.Id,
                    Level = DegreeLevel.Level100,
                    StudentCount = 50
                };

                context.StudentGroups.AddRange(grpA, grpB, grp100);
                await context.SaveChangesAsync();
            }

            // 8. Courses
            if (!await context.Courses.AnyAsync())
            {
                var csDept = await context.Departments.FirstAsync(d => d.Code == "CS");
                var busDept = await context.Departments.FirstAsync(d => d.Code == "BUS");
                var sciDept = await context.Departments.FirstAsync(d => d.Code == "BIO");

                var turing = await context.Lecturers.FirstAsync(l => l.EmployeeId == "LEC-001");
                var hopper = await context.Lecturers.FirstAsync(l => l.EmployeeId == "LEC-002");
                var drucker = await context.Lecturers.FirstAsync(l => l.EmployeeId == "LEC-003");
                var franklin = await context.Lecturers.FirstAsync(l => l.EmployeeId == "LEC-004");

                context.Courses.AddRange(new List<Course>
                {
                    new Course
                    {
                        Code = "CS101",
                        ShortForm = "Intro to CS",
                        Title = "Introduction to Computer Science",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = csDept.Id,
                        ProgrammeId = csProgObj.Id,
                        Level = DegreeLevel.Level100,
                        ComputerLabRequired = true,
                        AssignedLecturerId = hopper.Id
                    },
                    new Course
                    {
                        Code = "CS201",
                        ShortForm = "OOP",
                        Title = "Object-Oriented Programming with C#",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = csDept.Id,
                        ProgrammeId = csProgObj.Id,
                        Level = DegreeLevel.Level200,
                        ComputerLabRequired = true,
                        AssignedLecturerId = turing.Id
                    },
                    new Course
                    {
                        Code = "CS202",
                        ShortForm = "DSA",
                        Title = "Data Structures & Algorithms",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = csDept.Id,
                        ProgrammeId = csProgObj.Id,
                        Level = DegreeLevel.Level200,
                        PracticalRequired = true,
                        AssignedLecturerId = hopper.Id
                    },
                    new Course
                    {
                        Code = "CS302",
                        ShortForm = "Software Eng",
                        Title = "Software Engineering & Field Practicum",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = csDept.Id,
                        ProgrammeId = csProgObj.Id,
                        Level = DegreeLevel.Level300,
                        FieldWorkRequired = true,
                        AssignedLecturerId = turing.Id
                    },
                    new Course
                    {
                        Code = "BUS101",
                        ShortForm = "Intro to Bus",
                        Title = "Principles of Management & Business",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = busDept.Id,
                        ProgrammeId = busProgObj.Id,
                        Level = DegreeLevel.Level100,
                        AssignedLecturerId = drucker.Id
                    },
                    new Course
                    {
                        Code = "BIO101",
                        ShortForm = "Gen Biology",
                        Title = "General Biology & Lab Experiments",
                        CreditHours = 3,
                        WeeklyContactHours = 4,
                        DepartmentId = sciDept.Id,
                        ProgrammeId = csProgObj.Id,
                        Level = DegreeLevel.Level200,
                        LabRequired = true,
                        AssignedLecturerId = franklin.Id
                    }
                });
                await context.SaveChangesAsync();
            }

            // 9. Users & Authentication Account Setup
            var adminEmail = "admin@university.edu";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    UserType = "Admin",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(adminUser, "Admin@123456");
                await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                await userManager.AddToRoleAsync(adminUser, "TimetableAdmin");
            }

            var studentEmail = "student@university.edu";
            var studentUser = await userManager.FindByEmailAsync(studentEmail);
            if (studentUser == null)
            {
                var grpA = await context.StudentGroups.FirstAsync(g => g.Name == "Group A" && g.Level == DegreeLevel.Level200);

                var studentEntity = new Student
                {
                    IndexNumber = "CS/2025/0001",
                    FullName = "John Doe",
                    Email = studentEmail,
                    ProgrammeId = csProgObj.Id,
                    Level = DegreeLevel.Level200,
                    StudentGroupId = grpA.Id
                };
                context.Students.Add(studentEntity);
                await context.SaveChangesAsync();

                studentUser = new ApplicationUser
                {
                    UserName = studentEmail,
                    Email = studentEmail,
                    FullName = "John Doe",
                    UserType = "Student",
                    AssociatedStudentId = studentEntity.Id,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(studentUser, "Student@123456");
                await userManager.AddToRoleAsync(studentUser, "Student");

                studentEntity.UserId = studentUser.Id;
                await context.SaveChangesAsync();
            }
        }
    }
}
