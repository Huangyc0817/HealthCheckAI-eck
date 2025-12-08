using Microsoft.AspNetCore.Mvc;
using HealthCheckAI.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthCheckAI.Controllers
{
    public class PublicController : Controller
    {

        // 共用方法：抓這個人、指定科別清單中，最新的一筆已上傳報告
        private PatientFile? GetLatestPublishedReport(string displayName, params string[] departments)
        {
            var query = _context.PatientFiles
                .Where(p => p.PatientName == displayName && p.IsPublishedToPublic);

            if (departments != null && departments.Length > 0)
            {
                query = query.Where(p => departments.Contains(p.Department));
            }

            return query
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();
        }


        private readonly AppDbContext _context;

        public PublicController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {


            // ✅ 這裡改成跟 DoctorController 一樣，用 "UserName"
            var loginName = HttpContext.Session.GetString("Name") ?? "";

            // 如果你想顯示真實姓名，可以另外從 Users 查
            //var user = _context.Users.FirstOrDefault(u => u.Username == loginName);
            //ViewBag.UserName = user?.Name ?? loginName;

            ViewBag.UserName = loginName;
            return View();
        }

        [HttpGet]
        public IActionResult PrivacyNotice()
        {
          
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PrivacyConfirm(bool agree)
        {
            if (!agree)
            {
                ModelState.AddModelError("", "請先滑到最底並勾選同意後再繼續。");
                return View("PrivacyNotice");
            }

            // 簡單版：用 Session 記錄已同意
            HttpContext.Session.SetString("PrivacyAccepted", "true");

            // 同意之後進入民眾主頁
            return RedirectToAction("Index");
        }


        public IActionResult Summary()
        {
            // 1. 取得目前登入的帳號（登入時請有 SetString("Username", user.Username)）
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                // 沒登入就踢回登入頁
                return RedirectToAction("Index", "Home");
            }

            // 2. 抓這個帳號最新一份「已上傳給民眾」的報告
            var file = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();

            ViewBag.HasReport = file != null;
            ViewBag.Department = file?.Department;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.AiSummary = file?.AiSummary;
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;

            return View();
        }

        // 📌 這一段目前還是你原本舊的 Report 表，可先留著或之後改成用 PatientFiles
        public IActionResult Diagnosis()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var report = _context.Reports.FirstOrDefault(r => r.UserId == userId);

            if (report == null)
            {
                report = new Report
                {
                    DiagnosisA = "血壓正常，建議維持運動習慣。",
                    DiagnosisB = "血糖略高，建議減少含糖飲料。",
                    DiagnosisC = "體重略高，建議每週運動三次。",
                    DiagnosisD = "心率穩定，無明顯異常。",
                    DiagnosisE = "心率穩定，無明顯異常。",
                    DiagnosisF = "心率穩定，無明顯異常。",

                };
                ViewBag.DiagnosisA = "目前尚無AI健康報告。";
                ViewBag.DiagnosisB = "";
                ViewBag.DiagnosisC = "";
                ViewBag.DiagnosisD = "";
                ViewBag.DiagnosisE = "";
                ViewBag.DiagnosisF = "";
            }
            else
            {
                ViewBag.DiagnosisA = report.DiagnosisA;
                ViewBag.DiagnosisB = report.DiagnosisB;
                ViewBag.DiagnosisC = report.DiagnosisC;
                ViewBag.DiagnosisD = report.DiagnosisD;
                ViewBag.DiagnosisE = report.DiagnosisE;
                ViewBag.DiagnosisF = report.DiagnosisF;
            }
            return View();
        }

        public IActionResult DiagnosisA()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // A：系統體格檢查表
            var file = GetLatestPublishedReport(displayName, "系統體格檢查表");

            ViewBag.Department = "系統體格檢查表";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisA = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }

        public IActionResult DiagnosisB()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // B：理學檢查
            var file = GetLatestPublishedReport(displayName, "理學檢查");

            ViewBag.Department = "理學檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisB = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }
        public IActionResult DiagnosisC()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // C：眼科檢查
            var file = GetLatestPublishedReport(displayName, "眼科檢查");

            ViewBag.Department = "眼科檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisC = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }
        public IActionResult DiagnosisD()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // D：靜態心電圖
            var file = GetLatestPublishedReport(displayName, "靜態心電圖");

            ViewBag.Department = "靜態心電圖";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisD = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }
        public IActionResult DiagnosisE()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // E：實驗室檢查
            var file = GetLatestPublishedReport(displayName, "實驗室檢查");

            ViewBag.Department = "實驗室檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisE = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }

        public IActionResult DiagnosisF()
        {
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // F：精密儀器檢查
            var file = GetLatestPublishedReport(displayName, "精密儀器檢查");

            ViewBag.Department = "精密儀器檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisF = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            return View();
        }



    }
}

