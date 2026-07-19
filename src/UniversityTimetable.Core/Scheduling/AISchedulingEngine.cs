using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Core.Scheduling
{
    public class SchedulingOptions
    {
        public TimeSpan BreakStartTime { get; set; } = new TimeSpan(12, 30, 0);
        public TimeSpan BreakEndTime { get; set; } = new TimeSpan(13, 0, 0);
        public bool BalanceWorkload { get; set; } = true;
        public bool RegenerateOnlyUnlocked { get; set; } = false;
    }

    public class SchedulingResult
    {
        public bool Success { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int TotalCoursesScheduled { get; set; }
        public int TotalUnscheduledCourses { get; set; }
        public double ClassroomUtilizationPercentage { get; set; }
        public List<TimetableEntry> GeneratedEntries { get; set; } = new List<TimetableEntry>();
        public List<AISchedulingLog> ExplainabilityLogs { get; set; } = new List<AISchedulingLog>();
        public List<ClashReport> ClashReports { get; set; } = new List<ClashReport>();
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public interface ISchedulingEngine
    {
        Task<SchedulingResult> GenerateTimetableAsync(
            Timetable timetable,
            List<Course> courses,
            List<Lecturer> lecturers,
            List<Classroom> classrooms,
            List<StudentGroup> studentGroups,
            SchedulingOptions options);
    }

    public class AISchedulingEngine : ISchedulingEngine
    {
        private readonly IAIExplainabilityService _explainabilityService;

        public AISchedulingEngine(IAIExplainabilityService explainabilityService)
        {
            _explainabilityService = explainabilityService;
        }

        public async Task<SchedulingResult> GenerateTimetableAsync(
            Timetable timetable,
            List<Course> courses,
            List<Lecturer> lecturers,
            List<Classroom> classrooms,
            List<StudentGroup> studentGroups,
            SchedulingOptions options)
        {
            var startTime = DateTime.UtcNow;
            var result = new SchedulingResult();
            var logs = new List<AISchedulingLog>();
            var entries = new List<TimetableEntry>();

            // 1. Preserve Locked Entries if partial regeneration
            if (options.RegenerateOnlyUnlocked && timetable.Entries != null)
            {
                var locked = timetable.Entries.Where(e => e.IsLocked).ToList();
                entries.AddRange(locked);
                logs.Add(new AISchedulingLog
                {
                    TimetableId = timetable.Id,
                    StepName = "INITIALIZE_LOCKED_ENTRIES",
                    Action = "PRESERVE",
                    Rationale = $"Preserved {locked.Count} locked entries from overwrite.",
                    Details = $"Locked entries will remain fixed during partial re-scheduling."
                });
            }

            // 2. Generate Available Time Slots (1h & 2h windows, excluding break)
            var availableSlots = TimeSlotGenerator.GenerateAvailableTimeSlots(options.BreakStartTime, options.BreakEndTime);

            // 3. Build Demands for each Course
            var courseDemands = BuildCourseDemands(courses, studentGroups, classrooms);

            int scheduledCount = 0;
            int unscheduledCount = 0;

            foreach (var demand in courseDemands)
            {
                // Skip if course demands already satisfied by locked entries
                int existingHours = entries.Where(e => e.CourseId == demand.Course.Id &&
                                                      (demand.StudentGroup == null || e.StudentGroupId == demand.StudentGroup.Id)).Sum(e => e.DurationHours);
                int targetHours = demand.RequiredHours - existingHours;
                if (targetHours <= 0) continue;

                var combinations = TimeSlotGenerator.GetValidSessionCombinations(targetHours);
                var selectedCombination = combinations.FirstOrDefault() ?? new List<int> { 2, 2 };

                bool demandSuccess = true;

                foreach (var sessionDuration in selectedCombination)
                {
                    var validRoomTypes = GetRequiredRoomTypes(demand.Course, demand.SessionType);
                    var candidateRooms = classrooms.Where(r => r.IsActive && validRoomTypes.Contains(r.RoomType)).ToList();

                    // Sort rooms by capacity matching (smallest fit first to optimize large room utilization)
                    int demandSize = demand.StudentGroup?.StudentCount ?? demand.EstimatedCombinedSize;
                    candidateRooms = candidateRooms.Where(r => r.Capacity >= demandSize)
                        .OrderBy(r => r.Capacity).ToList();

                    if (!candidateRooms.Any())
                    {
                        // Fallback to any room if capacity constraint tight
                        candidateRooms = classrooms.Where(r => r.IsActive && validRoomTypes.Contains(r.RoomType)).OrderByDescending(r => r.Capacity).ToList();
                    }

                    var lecturer = demand.AssignedLecturer;
                    var candidateSlots = availableSlots.Where(s => s.DurationHours == sessionDuration).ToList();

                    bool assigned = false;

                    foreach (var slot in candidateSlots)
                    {
                        foreach (var room in candidateRooms)
                        {
                            // Check collisions against current placed entries
                            if (!HasCollision(slot, room.Id, lecturer.Id, demand.StudentGroup?.Id, demand.Course.ProgrammeId, demand.Course.Level, entries, options))
                            {
                                var newEntry = new TimetableEntry
                                {
                                    TimetableId = timetable.Id,
                                    CourseId = demand.Course.Id,
                                    LecturerId = lecturer.Id,
                                    ClassroomId = room.Id,
                                    StudentGroupId = demand.StudentGroup?.Id,
                                    DayOfWeek = slot.DayOfWeek,
                                    StartTime = slot.StartTime,
                                    EndTime = slot.EndTime,
                                    DurationHours = sessionDuration,
                                    SessionType = demand.SessionType,
                                    IsLocked = false
                                };

                                entries.Add(newEntry);
                                assigned = true;

                                string context = demand.SessionType == SessionType.CombinedClass
                                    ? $"Combined Class for split groups."
                                    : $"Group Session ({demand.StudentGroup?.Name ?? "General"}).";

                                logs.Add(_explainabilityService.ExplainAssignment(timetable.Id, demand.Course, lecturer, room, demand.StudentGroup, slot, demand.SessionType, context));
                                break;
                            }
                        }

                        if (assigned) break;
                    }

                    if (!assigned)
                    {
                        demandSuccess = false;
                        logs.Add(new AISchedulingLog
                        {
                            TimetableId = timetable.Id,
                            StepName = $"UNSCHEDULED_{demand.Course.Code}",
                            Action = "FAILED_PLACEMENT",
                            Rationale = $"Could not place {sessionDuration}h session for {demand.Course.Code}.",
                            Details = $"All candidate rooms and time slots resulted in lecturer/room/group clashes."
                        });
                    }
                }

                if (demandSuccess) scheduledCount++; else unscheduledCount++;
            }

            // 4. Calculate Utilization Metrics
            int totalSlotsPossible = classrooms.Count * availableSlots.Count;
            double utilization = totalSlotsPossible > 0 ? (double)entries.Count / totalSlotsPossible * 100.0 : 0.0;

            // 5. Run Clash Scanner for validation
            var clashResult = ClashDetectionEngine.DetectAllClashes(timetable.Id, entries, courses, classrooms, studentGroups, options.BreakStartTime, options.BreakEndTime);

            var endTime = DateTime.UtcNow;
            result.Success = !clashResult.HasCriticalClashes;
            result.ExecutionTime = endTime - startTime;
            result.TotalCoursesScheduled = scheduledCount;
            result.TotalUnscheduledCourses = unscheduledCount;
            result.ClassroomUtilizationPercentage = Math.Round(utilization, 2);
            result.GeneratedEntries = entries;
            result.ExplainabilityLogs = logs;
            result.ClashReports = clashResult.Reports;
            result.SummaryMessage = $"AI Timetable Generation completed in {result.ExecutionTime.TotalSeconds:F2}s. {scheduledCount} courses scheduled, {unscheduledCount} unscheduled. {clashResult.Reports.Count} total clashes detected.";

            return result;
        }

        private class SchedulingDemand
        {
            public Course Course { get; set; } = null!;
            public Lecturer AssignedLecturer { get; set; } = null!;
            public StudentGroup? StudentGroup { get; set; }
            public int RequiredHours { get; set; }
            public SessionType SessionType { get; set; }
            public int EstimatedCombinedSize { get; set; }
        }

        private List<SchedulingDemand> BuildCourseDemands(List<Course> courses, List<StudentGroup> studentGroups, List<Classroom> classrooms)
        {
            var demands = new List<SchedulingDemand>();

            foreach (var course in courses)
            {
                var lecturer = course.AssignedLecturer ?? new Lecturer { Id = 1, FullName = "Department Staff" };
                var progGroups = studentGroups.Where(g => g.ProgrammeId == course.ProgrammeId && g.Level == course.Level).ToList();

                int totalEnrolled = progGroups.Sum(g => g.StudentCount);
                if (totalEnrolled == 0) totalEnrolled = 60; // default assumption

                int largestRoomCap = classrooms.Any() ? classrooms.Max(c => c.Capacity) : 100;

                // Check if split needed
                if (progGroups.Count > 1 || totalEnrolled > largestRoomCap)
                {
                    // Demand 1: Mandatory Weekly Combined Class (2 hours)
                    demands.Add(new SchedulingDemand
                    {
                        Course = course,
                        AssignedLecturer = lecturer,
                        StudentGroup = null, // Combined for all
                        RequiredHours = 2,
                        SessionType = SessionType.CombinedClass,
                        EstimatedCombinedSize = totalEnrolled
                    });

                    // Demand 2: Group Specific Lab/Practical Sessions (2 hours each)
                    int remainingHours = Math.Max(2, course.WeeklyContactHours - 2);

                    foreach (var group in progGroups)
                    {
                        var sessionType = course.ComputerLabRequired ? SessionType.ComputerLab
                            : course.LabRequired ? SessionType.Lab
                            : course.FieldWorkRequired ? SessionType.FieldWork
                            : SessionType.Lecture;

                        demands.Add(new SchedulingDemand
                        {
                            Course = course,
                            AssignedLecturer = lecturer,
                            StudentGroup = group,
                            RequiredHours = remainingHours,
                            SessionType = sessionType,
                            EstimatedCombinedSize = group.StudentCount
                        });
                    }
                }
                else
                {
                    // Single Group course demand (4 contact hours split into sessions)
                    var group = progGroups.FirstOrDefault();
                    var sessionType = course.ComputerLabRequired ? SessionType.ComputerLab
                        : course.LabRequired ? SessionType.Lab
                        : course.FieldWorkRequired ? SessionType.FieldWork
                        : SessionType.Lecture;

                    demands.Add(new SchedulingDemand
                    {
                        Course = course,
                        AssignedLecturer = lecturer,
                        StudentGroup = group,
                        RequiredHours = course.WeeklyContactHours,
                        SessionType = sessionType,
                        EstimatedCombinedSize = totalEnrolled
                    });
                }
            }

            return demands;
        }

        private List<RoomType> GetRequiredRoomTypes(Course course, SessionType sessionType)
        {
            if (sessionType == SessionType.ComputerLab || course.ComputerLabRequired)
                return new List<RoomType> { RoomType.ComputerLab };
            if (sessionType == SessionType.Lab || course.LabRequired)
                return new List<RoomType> { RoomType.Lab };
            if (sessionType == SessionType.CombinedClass)
                return new List<RoomType> { RoomType.LectureHall };

            return new List<RoomType> { RoomType.LectureHall, RoomType.SeminarRoom };
        }

        private bool HasCollision(
            TimeSlot slot,
            int classroomId,
            int lecturerId,
            int? studentGroupId,
            int programmeId,
            DegreeLevel level,
            List<TimetableEntry> existingEntries,
            SchedulingOptions options)
        {
            // 1. Break overlap check
            if (TimeSlotGenerator.OverlapsWithBreak(slot.StartTime, slot.EndTime, options.BreakStartTime, options.BreakEndTime))
                return true;

            foreach (var entry in existingEntries)
            {
                if (entry.DayOfWeek != slot.DayOfWeek) continue;

                // Time overlap check
                bool overlap = slot.StartTime < entry.EndTime && slot.EndTime > entry.StartTime;
                if (!overlap) continue;

                // Lecturer clash
                if (entry.LecturerId == lecturerId) return true;

                // Classroom clash
                if (entry.ClassroomId == classroomId) return true;

                // Group clash
                if (studentGroupId.HasValue && entry.StudentGroupId.HasValue && entry.StudentGroupId == studentGroupId)
                    return true;

                // Combined class overlap for same programme & level
                if (!studentGroupId.HasValue || !entry.StudentGroupId.HasValue)
                {
                    if (entry.Course != null && entry.Course.ProgrammeId == programmeId && entry.Course.Level == level)
                        return true;
                }
            }

            return false;
        }
    }
}
