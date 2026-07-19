using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniversityTimetable.Infrastructure.Identity;

namespace UniversityTimetable.Web.Controllers
{
    [Route("Student/Account")]
    public class StudentAccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentAccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View("~/Views/StudentAccount/Login.cshtml");
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "StudentDashboard");
                }
            }

            ViewBag.Error = "Invalid student credentials.";
            return View("~/Views/StudentAccount/Login.cshtml");
        }

        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}
