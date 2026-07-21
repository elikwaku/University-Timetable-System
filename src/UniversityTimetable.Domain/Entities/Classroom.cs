using System.Collections.Generic;
using UniversityTimetable.Domain.Enums;

namespace UniversityTimetable.Domain.Entities
{
    public class Building : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();
    }

    public class Classroom : BaseEntity
    {
        public string RoomNumber { get; set; } = string.Empty;
        public int? BuildingId { get; set; }
        public Building? Building { get; set; }

        public int Capacity { get; set; }
        public RoomType RoomType { get; set; } = RoomType.LectureHall;
        public bool IsActive { get; set; } = true;
    }
}
