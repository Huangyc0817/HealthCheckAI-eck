using Microsoft.AspNetCore.Mvc;
using HealthCheckAI.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthCheckAI.Controllers
{
    public class PublicController : Controller
    {
        private readonly AppDbContext _context;

        public PublicController(AppDbContext context)
        {
            _context = context;
        }

        // 共用方法：抓這個帳號、指定科別中，最新一筆已上傳報告
        private PatientFile? GetLatestPublishedReport(string username, params string[] departments)
        {
            var query = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic);

            if (departments != null && departments.Length > 0)
            {
                query = query.Where(p => departments.Contains(p.Department));
            }

            return query
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();
        }

        private string? GetLoginUsername()
        {
            return HttpContext.Session.GetString("Username");
        }

        private bool IsPublicUser()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == "Public";
        }

        public IActionResult Index()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var reports = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .ToList();

            ViewBag.ReportCount = reports.Count;

            ViewBag.HighRiskCount = reports
                .Count(p => p.AiSeverity != null && p.AiSeverity.Contains("高"));

            var lastDate = reports.Any()
                ? reports.Max(p => p.PublishedAt ?? p.UploadedAt)
                : null;

            ViewBag.LastUpdated = lastDate?.ToString("yyyy/MM/dd") ?? "--";

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

            HttpContext.Session.SetString("PrivacyAccepted", "true");

            return RedirectToAction("Index");
        }

        public IActionResult Summary()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var latestFile = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();

            ViewBag.HasReport = latestFile != null;
            ViewBag.Department = latestFile?.Department;
            ViewBag.AiSeverity = latestFile?.AiSeverity;
            ViewBag.AiSummary = latestFile?.AiSummary;
            ViewBag.PublishedAt = latestFile?.PublishedAt ?? latestFile?.UploadedAt;

            var allFiles = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .ToList();

            string GetSeverity(string dept)
            {
                return allFiles
                    .Where(f => f.Department == dept)
                    .OrderByDescending(f => f.PublishedAt ?? f.UploadedAt)
                    .Select(f => f.AiSeverity)
                    .FirstOrDefault() ?? "";
            }

            int GetScore(string dept)
            {
                var file = allFiles
                    .Where(f => f.Department == dept)
                    .OrderByDescending(f => f.PublishedAt ?? f.UploadedAt)
                    .FirstOrDefault();

                if (file == null) return 0;

                string text = file.ExtractedText ?? "";

                bool mostlyNormal =
                    CountKeyword(text, "無明顯異常") >= 5 ||
                    CountKeyword(text, "未見明顯異常") >= 5;

                string severity = file.AiSeverity ?? "";

                int score = 0;

                if (severity.Contains("高")) score += 60;
                else if (severity.Contains("中")) score += 35;
                else if (severity.Contains("低")) score += 10;

                int abnormal = 0;
                abnormal += CountKeyword(text, "偏高");
                abnormal += CountKeyword(text, "過高");
                abnormal += CountKeyword(text, "偏低");

                int abnormalOnly = CountKeyword(text, "異常")
                    - CountKeyword(text, "無明顯異常")
                    - CountKeyword(text, "未見明顯異常");

                if (abnormalOnly > 0)
                    abnormal += abnormalOnly;

                abnormal += CountKeyword(text, "+");
                abnormal += CountKeyword(text, "陽性");

                score += abnormal * 8;

                if (dept == "體格檢查表")
                {
                    if (text.Contains("BMI")) score += 10;
                    if (text.Contains("腹圍")) score += 10;
                    if (text.Contains("血壓")) score += 10;
                }

                if (dept == "實驗室檢查")
                {
                    if (text.Contains("HbA1c")) score += 10;
                    if (text.Contains("eGFR")) score += 10;
                    if (text.Contains("潛血")) score += 8;
                }

                if (mostlyNormal && dept == "理學檢查")
                {
                    score = Math.Min(score, 15);
                }

                if (score > 100) score = 100;

                return score;
            }

            var list = new List<DeptSummaryViewModel>
            {
                new DeptSummaryViewModel {
                    Order = 1,
                    Department = "體格檢查表",
                    EnglishName = "Systemic Physical Exam",
                    Severity = GetSeverity("體格檢查表"),
                    Score = GetScore("體格檢查表")
                },
                new DeptSummaryViewModel {
                    Order = 2,
                    Department = "理學檢查",
                    EnglishName = "Physical Examination",
                    Severity = GetSeverity("理學檢查"),
                    Score = GetScore("理學檢查")
                },
                new DeptSummaryViewModel {
                    Order = 3,
                    Department = "眼科檢查",
                    EnglishName = "Ophthalmologic Exam",
                    Severity = GetSeverity("眼科檢查"),
                    Score = GetScore("眼科檢查")
                },
                new DeptSummaryViewModel {
                    Order = 4,
                    Department = "靜態心電圖",
                    EnglishName = "Resting ECG",
                    Severity = GetSeverity("靜態心電圖"),
                    Score = GetScore("靜態心電圖")
                },
                new DeptSummaryViewModel {
                    Order = 5,
                    Department = "實驗室檢查",
                    EnglishName = "Laboratory Tests",
                    Severity = GetSeverity("實驗室檢查"),
                    Score = GetScore("實驗室檢查")
                },
                new DeptSummaryViewModel {
                    Order = 6,
                    Department = "精密儀器檢查",
                    EnglishName = "Advanced Diagnostic Tests",
                    Severity = GetSeverity("精密儀器檢查"),
                    Score = GetScore("精密儀器檢查")
                }
            };

            var sorted = list
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Severity))
                .ThenByDescending(x => x.Score)
                .ToList();

            return View(sorted);
        }

        private int CountKeyword(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            return System.Text.RegularExpressions.Regex.Matches(
                text,
                System.Text.RegularExpressions.Regex.Escape(keyword)
            ).Count;
        }

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
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "體格檢查表");

            ViewBag.Department = "體格檢查表";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisA = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }

        public IActionResult DiagnosisB()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "理學檢查");

            ViewBag.Department = "理學檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisB = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }

        public IActionResult DiagnosisC()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "眼科檢查");

            ViewBag.Department = "眼科檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisC = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }

        public IActionResult DiagnosisD()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "靜態心電圖");

            ViewBag.Department = "靜態心電圖";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisD = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }

        public IActionResult DiagnosisE()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "實驗室檢查");

            ViewBag.Department = "實驗室檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisE = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }

        public IActionResult DiagnosisF()
        {
            var username = GetLoginUsername();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }

            var file = GetLatestPublishedReport(username, "精密儀器檢查");

            ViewBag.Department = "精密儀器檢查";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisF = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";
            ViewBag.ExtractedText = file?.ExtractedText ?? "";

            return View();
        }
    }
}