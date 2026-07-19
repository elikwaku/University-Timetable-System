using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Domain.Entities;
using UniversityTimetable.Infrastructure.Data;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/[controller]")]
    public class ClassroomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassroomController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Buildings = await _context.Buildings.ToListAsync();
            var rooms = await _context.Classrooms
                .Include(r => r.Building)
                .ToListAsync();
            return View("~/Views/Classroom/Index.cshtml", rooms);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(Classroom room)
        {
            _context.Classrooms.Add(room);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Classroom '{room.RoomNumber}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Classroom room)
        {
            var existing = await _context.Classrooms.FindAsync(room.Id);
            if (existing != null)
            {
                existing.RoomNumber = room.RoomNumber;
                existing.BuildingId = room.BuildingId;
                existing.Capacity = room.Capacity;
                existing.RoomType = room.RoomType;
                existing.IsActive = room.IsActive;
                existing.UpdatedAt = System.DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Classroom '{room.RoomNumber}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.Classrooms.FindAsync(id);
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Classroom deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
