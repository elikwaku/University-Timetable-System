using System;
using System.Collections.Generic;
using System.Linq;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Core.Scheduling
{
    public class ClashDetectionResult
    {
        public bool HasCriticalClashes => Reports.Any(r => r.Severity == ConflictSeverity.Critical || r.Severity == ConflictSeverity.High);
        public List<ClashReport> Reports { get; set; } = new List<ClashReport>();
    }

    public static class ClashDetectionEngine
    {
        public static ClashDetectionResult DetectAllClashes(
            int timetableId,
            List<TimetableEntry> entries,
            List<Course> courses,
            List<Classroom> classrooms,
            List<StudentGroup> studentGroups,
            TimeSpan breakStart,
            TimeSpan breakEnd)
        {
            var result = new ClashDetectionResult();
            var reports = new List<ClashReport>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry1 = entries[i];
                var course1 = courses.FirstOrDefault(c => c.Id == entry1.CourseId);
                var room1 = classrooms.FirstOrDefault(r => r.Id == entry1.ClassroomId);
                var group1 = studentGroups.FirstOrDefault(g => g.Id == entry1.StudentGroupId);

                // 1. Duration Validation (1h or 2h ONLY)
                if (entry1.DurationHours != 1 && entry1.DurationHours != 2)
                {
                    reports.Add(new ClashReport
                    {
                        TimetableId = timetableId,
                        ConflictType = ConflictType.InvalidSessionDuration,
                        Severity = ConflictSeverity.High,
                        AffectedEntryIds = entry1.Id.ToString(),
                        Description = $"Entry for {course1?.Code ?? "Course"} has unsupported duration of {entry1.DurationHours} hours. Only 1-hour or 2-hour sessions are allowed.",
                        Recommendation = "Split or adjust session duration to 1 hour or 2 hours."
                    });
                }

                // 2. Break Time Violation
                if (TimeSlotGenerator.OverlapsWithBreak(entry1.StartTime, entry1.EndTime, breakStart, breakEnd))
                {
                    reports.Add(new ClashReport
                    {
                        TimetableId = timetableId,
                        ConflictType = ConflictType.BreakTimeViolation,
                        Severity = ConflictSeverity.Critical,
                        AffectedEntryIds = entry1.Id.ToString(),
                        Description = $"Entry for {course1?.Code ?? "Course"} ({entry1.DayOfWeek} {entry1.StartTime:hh\\:mm}-{entry1.EndTime:hh\\:mm}) overlaps with official daily break ({breakStart:hh\\:mm}-{breakEnd:hh\\:mm}).",
                        Recommendation = "Shift class outside the official break window."
                    });
                }

                // 3. Room Type Mismatch
                if (course1 != null && room1 != null)
                {
                    bool mismatch = false;
                    string requiredType = "LectureHall";

                    if (course1.ComputerLabRequired && room1.RoomType != RoomType.ComputerLab)
                    {
                        mismatch = true;
                        requiredType = "ComputerLab";
                    }
                    else if (course1.LabRequired && room1.RoomType != RoomType.Lab)
                    {
                        mismatch = true;
                        requiredType = "Lab";
                    }

                    if (mismatch)
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.RoomTypeMismatch,
                            Severity = ConflictSeverity.High,
                            AffectedEntryIds = entry1.Id.ToString(),
                            Description = $"Course {course1.Code} requires a {requiredType}, but room {room1.RoomNumber} is a {room1.RoomType}.",
                            Recommendation = $"Reassign session to a valid {requiredType}."
                        });
                    }

                    // 4. Capacity Violation
                    int enrolledCount = group1?.StudentCount ?? (course1.Programme?.StudentGroups.Sum(g => g.StudentCount) ?? 0);
                    if (enrolledCount > room1.Capacity && room1.Capacity > 0)
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.CapacityViolation,
                            Severity = ConflictSeverity.High,
                            AffectedEntryIds = entry1.Id.ToString(),
                            Description = $"Room {room1.RoomNumber} capacity ({room1.Capacity}) is insufficient for {enrolledCount} enrolled students in {course1.Code}.",
                            Recommendation = "Assign a larger auditorium or split the class into smaller student groups."
                        });
                    }
                }

                // Overlap checks against other entries
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var entry2 = entries[j];
                    if (entry1.DayOfWeek != entry2.DayOfWeek) continue;

                    // Check time overlap
                    bool overlaps = entry1.StartTime < entry2.EndTime && entry1.EndTime > entry2.StartTime;
                    if (!overlaps) continue;

                    var course2 = courses.FirstOrDefault(c => c.Id == entry2.CourseId);

                    // 5. Lecturer Clash
                    if (entry1.LecturerId == entry2.LecturerId)
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.LecturerClash,
                            Severity = ConflictSeverity.Critical,
                            AffectedEntryIds = $"{entry1.Id},{entry2.Id}",
                            Description = $"Lecturer clash: Assigned lecturer is scheduled for multiple classes simultaneously on {entry1.DayOfWeek} ({entry1.StartTime:hh\\:mm}-{entry1.EndTime:hh\\:mm}) for {course1?.Code} and {course2?.Code}.",
                            Recommendation = "Move one session to a different time slot or reassign lecturer."
                        });
                    }

                    // 6. Classroom Clash
                    if (entry1.ClassroomId == entry2.ClassroomId)
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.ClassroomClash,
                            Severity = ConflictSeverity.Critical,
                            AffectedEntryIds = $"{entry1.Id},{entry2.Id}",
                            Description = $"Classroom clash: Room is double-booked on {entry1.DayOfWeek} ({entry1.StartTime:hh\\:mm}-{entry1.EndTime:hh\\:mm}) for {course1?.Code} and {course2?.Code}.",
                            Recommendation = "Move one session to a different room or time slot."
                        });
                    }

                    // 7. Student Group Clash
                    bool sameGroup = (entry1.StudentGroupId.HasValue && entry1.StudentGroupId == entry2.StudentGroupId);
                    bool sameProgrammeLevel = (course1 != null && course2 != null &&
                                               course1.ProgrammeId == course2.ProgrammeId &&
                                               course1.Level == course2.Level);

                    if (sameGroup || (sameProgrammeLevel && (!entry1.StudentGroupId.HasValue || !entry2.StudentGroupId.HasValue)))
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.StudentGroupClash,
                            Severity = ConflictSeverity.Critical,
                            AffectedEntryIds = $"{entry1.Id},{entry2.Id}",
                            Description = $"Student clash: Students in {course1?.Programme?.Name ?? "Programme"} Level {(int)(course1?.Level ?? 0)} have overlapping classes on {entry1.DayOfWeek} ({entry1.StartTime:hh\\:mm}-{entry1.EndTime:hh\\:mm}).",
                            Recommendation = "Shift one of the conflicting sessions."
                        });
                    }
                }
            }

            // 8. Contact Hours Validation
            foreach (var course in courses)
            {
                var courseEntries = entries.Where(e => e.CourseId == course.Id).ToList();
                var groupsForCourse = studentGroups.Where(g => g.ProgrammeId == course.ProgrammeId && g.Level == course.Level).ToList();

                if (groupsForCourse.Any())
                {
                    foreach (var group in groupsForCourse)
                    {
                        int groupHours = courseEntries
                            .Where(e => e.StudentGroupId == null || e.StudentGroupId == group.Id)
                            .Sum(e => e.DurationHours);

                        if (groupHours < course.WeeklyContactHours)
                        {
                            reports.Add(new ClashReport
                            {
                                TimetableId = timetableId,
                                ConflictType = ConflictType.UnfulfilledContactHours,
                                Severity = ConflictSeverity.High,
                                AffectedEntryIds = string.Join(",", courseEntries.Select(e => e.Id)),
                                Description = $"Course {course.Code} ({group.Name}) requires {course.WeeklyContactHours} weekly contact hours, but only {groupHours} hours are scheduled for this group.",
                                Recommendation = "Schedule additional sessions for this group to fulfill required contact hours."
                            });
                        }
                    }
                }
                else
                {
                    int scheduledHours = courseEntries.Sum(e => e.DurationHours);
                    if (scheduledHours < course.WeeklyContactHours)
                    {
                        reports.Add(new ClashReport
                        {
                            TimetableId = timetableId,
                            ConflictType = ConflictType.UnfulfilledContactHours,
                            Severity = ConflictSeverity.High,
                            AffectedEntryIds = string.Join(",", courseEntries.Select(e => e.Id)),
                            Description = $"Course {course.Code} requires {course.WeeklyContactHours} weekly contact hours, but only {scheduledHours} hours are scheduled.",
                            Recommendation = "Add additional 1-hour or 2-hour sessions."
                        });
                    }
                }
            }

            result.Reports = reports;
            return result;
        }
    }
}
