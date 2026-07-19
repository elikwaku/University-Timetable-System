namespace UniversityTimetable.Domain.Enums
{
    public enum RoomType
    {
        LectureHall = 1,
        Lab = 2,
        ComputerLab = 3,
        SeminarRoom = 4
    }

    public enum SessionType
    {
        Lecture = 1,
        Lab = 2,
        ComputerLab = 3,
        Practical = 4,
        FieldWork = 5,
        CombinedClass = 6
    }

    public enum DegreeLevel
    {
        Level100 = 100,
        Level200 = 200,
        Level300 = 300,
        Level400 = 400,
        Postgraduate = 500
    }

    public enum TimetableStatus
    {
        Draft = 1,
        Validated = 2,
        Published = 3,
        Archived = 4
    }

    public enum ConflictType
    {
        LecturerClash = 1,
        ClassroomClash = 2,
        StudentGroupClash = 3,
        CapacityViolation = 4,
        RoomTypeMismatch = 5,
        BreakTimeViolation = 6,
        InvalidSessionDuration = 7,
        MissingCombinedLecture = 8,
        UnfulfilledContactHours = 9
    }

    public enum ConflictSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
