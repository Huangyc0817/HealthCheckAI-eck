using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HealthCheckAI.Helpers;
using HealthCheckAI.Models;
using HealthCheckAI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthCheckAI.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;

        public HomeController(AppDbContext context, IEmailService email)
        {
            _context = context;
            _email = email;
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(User user)
        {
            var conn = _context.Database.GetDbConnection();

            ViewBag.Message =
                "目前資料庫：" + conn.Database + "<br/>" +
                "目前連線字串：" + conn.ConnectionString;

            // 避免 Role 沒填造成 ModelState 驗證失敗
            ModelState.Remove("Role");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                ViewBag.Message = "錯誤：" + string.Join("、", errors);
                return View(user);
            }

            user.Username = user.Username?.Trim();

            if (string.IsNullOrWhiteSpace(user.Username))
            {
                ViewBag.Message = "請輸入帳號";
                return View(user);
            }

            // 判斷是不是數字開頭
            if (user.Username.All(char.IsDigit))
            {
                user.Role = "Doctor";
            }
            else
            {
                // 非數字開頭 → 必須是有效身分證字號
                if (!TwIdValidator.IsValid(user.Username))
                {
                    ViewBag.Message = "請輸入有效的身分證字號";
                    return View(user);
                }

                user.Role = "Public";
            }

            var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
            if (existingUser != null)
            {
                ViewBag.Message = "此帳號已註冊";
                return View(user);
            }

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        public IActionResult Index()
        {
            return View();
        }

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

            if (user == null)
            {
                ViewBag.Message = "帳號或密碼錯誤";
                return View();
            }

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

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");

            if (pendingUserId == null)
                return RedirectToAction("Index");

            return View();
        }

        [HttpPost]
        public IActionResult VerifyOtp(string otp)
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");

            if (pendingUserId == null)
                return RedirectToAction("Index");

            var now = DateTime.UtcNow;

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

                if (record.FailCount >= 5)
                {
                    record.LockedUntil = now.AddMinutes(10);
                }

                _context.SaveChanges();

                ViewBag.Message = "驗證碼錯誤";
                return View();
            }

            record.UsedAt = now;
            _context.SaveChanges();

            var user = _context.Users.FirstOrDefault(u => u.Id == pendingUserId.Value);

            if (user == null)
                return RedirectToAction("Index");

            // ✅ 這裡是重點：PublicController 會用 Username 抓報告
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username ?? "");
            HttpContext.Session.SetString("Name", user.Name ?? "");
            HttpContext.Session.SetString("UserRole", user.Role ?? "");

            HttpContext.Session.Remove("PendingUserId");

            if ((user.Role ?? "").Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Doctor");
            }

            return RedirectToAction("PrivacyNotice", "Public");
        }

        [HttpPost]
        public async Task<IActionResult> ResendOtp()
        {
            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");

            if (pendingUserId == null)
                return RedirectToAction("Index");

            var user = _context.Users.FirstOrDefault(u => u.Id == pendingUserId.Value);

            if (user == null)
                return RedirectToAction("Index");

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ViewBag.Message = "此帳號未設定 Email，無法寄送驗證碼。";
                return View("VerifyOtp");
            }

            await CreateStoreAndSendOtpAsync(user);

            ViewBag.Message = "已重新寄送驗證碼";
            return View("VerifyOtp");
        }

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