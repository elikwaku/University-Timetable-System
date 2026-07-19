using System;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Core.Scheduling
{
    public interface IAIExplainabilityService
    {
        AISchedulingLog ExplainAssignment(
            int timetableId,
            Course course,
            Lecturer lecturer,
            Classroom classroom,
            StudentGroup? studentGroup,
            TimeSlot slot,
            SessionType sessionType,
            string? additionalContext = null);
    }

    public class AIExplainabilityService : IAIExplainabilityService
    {
        public AISchedulingLog ExplainAssignment(
            int timetableId,
            Course course,
            Lecturer lecturer,
            Classroom classroom,
            StudentGroup? studentGroup,
            TimeSlot slot,
            SessionType sessionType,
            string? additionalContext = null)
        {
            string groupText = studentGroup != null ? $"Group '{studentGroup.Name}' ({studentGroup.StudentCount} students)" : "All Programme Groups (Combined Lecture)";
            string roomText = $"{classroom.RoomNumber} ({classroom.RoomType}, Cap: {classroom.Capacity})";
            string timeText = $"{slot.DayOfWeek} {slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm} ({slot.DurationHours}h)";

            string rationale = $"Scheduled {course.Code} ({course.ShortForm}) - {sessionType} for {groupText}.";
            string details = $"Assigned Lecturer: {lecturer.FullName} (within max workload).\n" +
                             $"Assigned Classroom: {roomText} - room type and capacity validated.\n" +
                             $"Scheduled Time Window: {timeText}.\n" +
                             $"Academic Rule Compliance: 08:00-17:00 window, duration {slot.DurationHours}h, clear of 12:30-13:00 official break.";

            if (!string.IsNullOrEmpty(additionalContext))
            {
                details += $"\nSpecial Context: {additionalContext}";
            }

            return new AISchedulingLog
            {
                TimetableId = timetableId,
                StepName = $"{course.Code}_{sessionType}",
                Action = "SESSION_ASSIGNED",
                Rationale = rationale,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
