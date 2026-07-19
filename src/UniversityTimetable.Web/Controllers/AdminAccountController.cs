using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniversityTimetable.Infrastructure.Identity;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Admin/Account")]
    public class AdminAccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminAccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View("~/Views/AdminAccount/Login.cshtml");
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && (user.UserType == "Admin" || await _userManager.IsInRoleAsync(user, "SuperAdmin") || await _userManager.IsInRoleAsync(user, "TimetableAdmin")))
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "AdminDashboard");
                }
            }

            ViewBag.Error = "Invalid administrative credentials or insufficient permissions.";
            return View("~/Views/AdminAccount/Login.cshtml");
        }

        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}
