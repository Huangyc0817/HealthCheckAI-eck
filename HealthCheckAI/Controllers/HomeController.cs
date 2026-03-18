using System.Security.Cryptography;
using System.Text;
using HealthCheckAI.Models;
using HealthCheckAI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HealthCheckAI.Helpers;
using System.Linq;

namespace HealthCheckAI.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;

        // ✅ 只保留一個建構子（DI 才會正常）
        public HomeController(AppDbContext context, IEmailService email)
        {
            _context = context;
            _email = email;
        }

        // 顯示註冊頁面
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(User user)
        {
            user.Role = "Public";

            // 驗證身分證字號是否合法
            if (!TwIdValidator.IsValidTaiwanId(user.Username))
            {
                ViewBag.Message = "請輸入有效的身分證字號";
                return View(user);
            }

            // 檢查帳號是否已存在
            var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
            if (existingUser != null)
            {
                ViewBag.Message = "此身分證字號已註冊";
                return View(user);
            }

            if (ModelState.IsValid)
            {
                _context.Users.Add(user);
                _context.SaveChanges();

                return RedirectToAction("Index");
            }

            ViewBag.Message = "註冊失敗";
            return View(user);
        }

        // 顯示登入頁面
        public IActionResult Index()
        {
            return View();
        }

        // ✅ 接收登入表單資料（真寄信，所以要 async）

        [HttpPost]
        public async Task<IActionResult> Index(string username, string password)
        {
            username = username?.Trim();
            password = password?.Trim();

            var user = _context.Users
                .AsEnumerable() 
                .FirstOrDefault(u =>
                    (u.Username ?? "").Trim() == username &&
                    (u.Password ?? "").Trim() == password
                );

            if (user != null)
            {
                HttpContext.Session.SetInt32("PendingUserId", user.Id);

                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    ViewBag.Message = "此帳號未設定 Email，無法進行 OTP 驗證。請先補上 Email。";
                    HttpContext.Session.Remove("PendingUserId");
                    return View();
                }

                await CreateStoreAndSendOtpAsync(user);
                return RedirectToAction("VerifyOtp");
            }

            ViewBag.Message = "帳號或密碼錯誤";
            return View();
        }

        // ====== OTP 驗證頁 ======
        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");
            if (pendingUserId == null) return RedirectToAction("Index");

            return View(); // Views/Home/VerifyOtp.cshtml
        }

        [HttpPost]
        public IActionResult VerifyOtp(string otp)
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");
            if (pendingUserId == null) return RedirectToAction("Index");

            var now = DateTime.UtcNow;

            // 找最新一筆未使用 OTP
            var record = _context.MfaOtps
                .Where(x => x.UserId == pendingUserId.Value && x.UsedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (record == null)
            {
                ViewBag.Message = "驗證碼不存在，請重新登入取得。";
                return View();
            }

            if (record.LockedUntil != null && record.LockedUntil > now)
            {
                ViewBag.Message = "錯誤次數過多，請稍後再試。";
                return View();
            }

            if (record.ExpireAt <= now)
            {
                ViewBag.Message = "驗證碼已過期，請重新登入取得。";
                return View();
            }

            var inputHash = HashOtp(otp, pendingUserId.Value);

            if (!string.Equals(inputHash, record.OtpHash, StringComparison.OrdinalIgnoreCase))
            {
                record.FailCount += 1;

                // 5 次鎖 10 分鐘
                if (record.FailCount >= 5)
                {
                    record.LockedUntil = now.AddMinutes(10);
                }

                _context.SaveChanges();
                ViewBag.Message = "驗證碼錯誤";
                return View();
            }

            // 成功：標記已使用
            record.UsedAt = now;
            _context.SaveChanges();

            // ✅ OTP 成功後才真正登入（寫入你原本的 Session）
            var user = _context.Users.FirstOrDefault(u => u.Id == pendingUserId.Value);
            if (user == null) return RedirectToAction("Index");

            HttpContext.Session.SetString("UserName", user.Username);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Name", user.Name ?? "");

            // 清 Pending
            HttpContext.Session.Remove("PendingUserId");

            // ✅ 角色導頁（保留你原本邏輯）
            if (user.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Doctor");
            else
                return RedirectToAction("PrivacyNotice", "Public");
        }

        // ✅ 重新寄送 OTP（要 async 才能 await）
        [HttpPost]
        public async Task<IActionResult> ResendOtp()
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");
            if (pendingUserId == null) return RedirectToAction("Index");

            var user = _context.Users.FirstOrDefault(u => u.Id == pendingUserId.Value);
            if (user == null) return RedirectToAction("Index");

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ViewBag.Message = "此帳號未設定 Email，無法寄送驗證碼。";
                return View("VerifyOtp");
            }

            await CreateStoreAndSendOtpAsync(user);
            ViewBag.Message = "已重新寄送驗證碼";
            return View("VerifyOtp");
        }

        // ====== OTP 工具 ======
        private async Task CreateStoreAndSendOtpAsync(User user)
        {
            var otp = Generate6DigitOtp();
            var otpHash = HashOtp(otp, user.Id);

            var record = new MfaOtp
            {
                UserId = user.Id,
                OtpHash = otpHash,
                ExpireAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            };

            _context.MfaOtps.Add(record);
            _context.SaveChanges();

            await _email.SendOtpAsync(user.Email!, otp);
        }

        private static string Generate6DigitOtp()
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var value = BitConverter.ToUInt32(bytes, 0) % 1000000;
            return value.ToString("D6");
        }

        private static string HashOtp(string otp, int userId)
        {
            var raw = $"{userId}:{otp}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }
    }
}