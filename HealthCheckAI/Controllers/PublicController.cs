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
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            var reports = _context.PatientFiles
    .Where(p => p.PatientName == displayName && p.IsPublishedToPublic)
    .ToList();

            ViewBag.ReportCount = reports.Count;

            ViewBag.HighRiskCount = reports
                .Count(p => p.AiSeverity != null && p.AiSeverity.Contains("高"));

            var lastDate = reports
                .Max(p => p.PublishedAt ?? p.UploadedAt);

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

            // 簡單版：用 Session 記錄已同意
            HttpContext.Session.SetString("PrivacyAccepted", "true");

            // 同意之後進入來賓主頁
            return RedirectToAction("Index");
        }


        public IActionResult Summary()
        {
            // 1. 取登入的來賓顯示名稱（跟 DiagnosisA/B/C 用的一樣）
            var displayName = HttpContext.Session.GetString("Name");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(displayName) || role != "Public")
            {
                return RedirectToAction("Index", "Home");
            }

            // 2. 抓這個來賓最新一份「已上傳給來賓」的報告（上面那個藍框 card 用）
            var latestFile = _context.PatientFiles
                .Where(p => p.PatientName == displayName && p.IsPublishedToPublic)
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();

            ViewBag.HasReport = latestFile != null;
            ViewBag.Department = latestFile?.Department;
            ViewBag.AiSeverity = latestFile?.AiSeverity;
            ViewBag.AiSummary = latestFile?.AiSummary;
            ViewBag.PublishedAt = latestFile?.PublishedAt ?? latestFile?.UploadedAt;

            // 3. 把這個來賓所有「已上傳給來賓」的檔案抓出來
            var allFiles = _context.PatientFiles
                .Where(p => p.PatientName == displayName && p.IsPublishedToPublic)
                .ToList();

            // 讀文字嚴重程度 (高/中/低)
            string GetSeverity(string dept)
            {
                return allFiles
                    .Where(f => f.Department == dept)
                    .OrderByDescending(f => f.PublishedAt ?? f.UploadedAt)
                    .Select(f => f.AiSeverity)
                    .FirstOrDefault();
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

            // 4. 六個科別
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

            // 5. 排序
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

            // A：體格檢查表
            var file = GetLatestPublishedReport(displayName, "體格檢查表");

            ViewBag.Department = "體格檢查表";
            ViewBag.PublishedAt = file?.PublishedAt ?? file?.UploadedAt;
            ViewBag.AiSeverity = file?.AiSeverity;
            ViewBag.DiagnosisA = file?.AiSummary ?? "目前尚無此科別醫師上傳的 AI 健檢報告。";

            ViewBag.ExtractedText = file?.ExtractedText ?? "";

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

