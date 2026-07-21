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
            try
            {
                ModelState.Remove("Building");

                if (room.BuildingId == null || room.BuildingId == 0)
                {
                    var defaultBuilding = await _context.Buildings.FirstOrDefaultAsync();
                    if (defaultBuilding == null)
                    {
                        defaultBuilding = new Building { Code = "MAIN", Name = "Main Campus" };
                        _context.Buildings.Add(defaultBuilding);
                        await _context.SaveChangesAsync();
                    }
                    room.BuildingId = defaultBuilding.Id;
                }

                _context.Classrooms.Add(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Classroom '{room.RoomNumber}' created successfully.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Failed to create classroom: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(Classroom room)
        {
            try
            {
                ModelState.Remove("Building");

                var existing = await _context.Classrooms.FindAsync(room.Id);
                if (existing != null)
                {
                    existing.RoomNumber = room.RoomNumber;
                    if (room.BuildingId != null && room.BuildingId != 0)
                    {
                        existing.BuildingId = room.BuildingId;
                    }
                    existing.Capacity = room.Capacity;
                    existing.RoomType = room.RoomType;
                    existing.IsActive = room.IsActive;
                    existing.UpdatedAt = System.DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Classroom '{room.RoomNumber}' updated successfully.";
                }
                else
                {
                    TempData["Error"] = "Classroom not found.";
                }
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Failed to update classroom: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _context.Classrooms.FindAsync(id);
                if (existing != null)
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = System.DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Classroom deleted successfully.";
                }
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Failed to delete classroom: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
