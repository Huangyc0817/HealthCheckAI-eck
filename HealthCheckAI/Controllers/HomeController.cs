using Microsoft.AspNetCore.Mvc;
using HealthCheckAI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace HealthCheckAI.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // 顯示註冊頁面
        public IActionResult Register()
        {
            return View();
        }

        // 接收註冊表單資料
        [HttpPost]
        public IActionResult Register(User user)
        {
            Console.WriteLine("HealthCheckAI_DB"+_context.Database.GetDbConnection().ConnectionString); //測試
            if (ModelState.IsValid)
            {
                user.Role = "Public";


                var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
                if (existingUser != null)
                {
                    ViewBag.Message = "此帳號已存在";
                    return View();
                }

                _context.Users.Add(user);
                _context.SaveChanges();

                ViewBag.Message = "註冊成功！";
                return RedirectToAction("Index");
            }

            ViewBag.Message = "註冊失敗";
            return View();
        }

        // 顯示登入頁面
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PrivacyNotice()
        {
           
            return View();
        }

        // 接收登入表單資料
        [HttpPost]
        public IActionResult Index(string username, string password)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                HttpContext.Session.SetString("UserName", user.Username);  // ?? DiagnosisA 讀的就是這個
                HttpContext.Session.SetString("UserRole", user.Role);      // "Public" 或 "Doctor"
                HttpContext.Session.SetInt32("UserId", user.Id);

                // 如果你有真實姓名，也可以順便存
                HttpContext.Session.SetString("Name", user.Name);

                if (user.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Doctor");
                }
                else if (user.Role.Equals("Public", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("PrivacyNotice", "Public");
                }
            }

            ViewBag.Message = "帳號或密碼錯誤";
            return View();
        }

    }
}