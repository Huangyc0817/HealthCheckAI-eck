using Microsoft.AspNetCore.Mvc;
using HealthCheckAI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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

            // 👉 取得所有已發布報告
            var reports = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .ToList();

            // 👉 首頁上方三個卡片
            ViewBag.ReportCount = reports.Count;

            ViewBag.HighRiskCount = reports
                .Count(p => p.AiSeverity != null && p.AiSeverity.Contains("高"));

            var lastDate = reports.Any()
                ? reports.Max(p => p.PublishedAt ?? p.UploadedAt)
                : null;

            ViewBag.LastUpdated = lastDate?.ToString("yyyy/MM/dd") ?? "--";

            // 👉 Summary 用資料（直接沿用你原本的）
            string GetSeverity(string dept)
            {
                return reports
                    .Where(f => f.Department == dept)
                    .OrderByDescending(f => f.PublishedAt ?? f.UploadedAt)
                    .Select(f => f.AiSeverity)
                    .FirstOrDefault() ?? "";
            }

            int GetScore(string dept)
            {
                var file = reports
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

            // 👉 六大分類（完全照你原本 Summary）
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

            // 👉 ⭐重點：首頁直接帶風險資料
            return View(sorted);
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
            var voiceSections = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(username) || !IsPublicUser())
            {
                return RedirectToAction("Index", "Home");
            }
            var reports = _context.PatientFiles
                .Where(p => p.PatientName == username &&
                p.IsPublishedToPublic &&
                !string.IsNullOrEmpty(p.AiSummary))
                .OrderBy(p => p.Department)
                .ToList();

            ViewBag.AbnormalVoiceSummary = BuildAbnormalVoiceSummary(reports);

            var latestFile = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .OrderByDescending(p => p.PublishedAt ?? p.UploadedAt)
                .FirstOrDefault();

            ViewBag.HasReport = latestFile != null;
            ViewBag.Department = latestFile?.Department;
            ViewBag.AiSeverity = latestFile?.AiSeverity;
            ViewBag.AiSummary = latestFile?.AiSummary;
            ViewBag.PublishedAt = latestFile?.PublishedAt ?? latestFile?.UploadedAt;
            ViewBag.VoiceSections = voiceSections;

            var allFiles = _context.PatientFiles
                .Where(p => p.PatientName == username && p.IsPublishedToPublic)
                .ToList();

            var trendList = allFiles
    .Where(f => !string.IsNullOrEmpty(f.Department))
    .GroupBy(f => f.Department)
    .Select(g =>
    {
        var ordered = g
            .OrderByDescending(f => f.PublishedAt ?? f.UploadedAt)
            .ToList();

        if (ordered.Count < 2)
            return null;

        var current = ordered[0];
        var previous = ordered[1];

        int currentScore = CalculateScore(current);
        int previousScore = CalculateScore(previous);

        int diff = currentScore - previousScore;

        return new HealthTrendViewModel
        {
            Department = current.Department,
            CurrentScore = currentScore,
            PreviousScore = previousScore,
            Difference = diff,
            TrendIcon = diff > 0 ? "⬆️" : diff < 0 ? "⬇️" : "➡️",
            TrendText = diff > 0
                ? "風險上升，建議優先追蹤"
                : diff < 0
                    ? "風險下降，狀況改善"
                    : "風險持平"
        };
    })
    .Where(x => x != null)
    .ToList();

            ViewBag.HealthTrends = trendList;

            foreach (var file in allFiles)
            {
                if (string.IsNullOrEmpty(file.AiSummary))
                    continue;

                var singleReport = new List<PatientFile> { file };

                voiceSections[file.Department] =
                    BuildAbnormalVoiceSummary(singleReport);
            }

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

        private int CalculateScore(PatientFile file)
        {
            if (file == null) return 0;

            string text = file.ExtractedText ?? "";
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

            if (score > 100)
                score = 100;

            return score;
        }

        private string BuildAbnormalVoiceSummary(List<PatientFile> reports)
        {
            var keywords = new[]
            {
        "異常", "偏高", "偏低", "過高", "過低",
        "近視", "散光", "陽性", "貧血",
        "血壓", "BMI", "潛血", "糖化血色素",
        "高血糖", "高血脂"
    };

            var ignoreKeywords = new[]
            {
        "無明顯異常",
        "未見明顯異常",
        "健康建議",
        "建議(Suggestion)",
        "信心度",
        "相對風險"
    };

            var article = new List<string>();

            foreach (var report in reports)
            {
                var text = report.AiSummary ?? "";

                var lines = text
                    .Split(new[] { '\n', '。', '；' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x =>
                        keywords.Any(k => x.Contains(k)) &&
                        !ignoreKeywords.Any(i => x.Contains(i)))
                    .Distinct()
                    .ToList();

                if (report.Department == "眼科檢查")
                {
                    lines = lines.Where(x =>
                        !x.Contains("電腦驗光") &&
                        !x.Contains("辨色力") &&
                        !x.Contains("---") &&
                        !Regex.IsMatch(x, @"\d+\.\d+\s+---"))
                        .ToList();
                }

                if (!lines.Any())
                    continue;

                var deptText = $"【{report.Department}】。";

                foreach (var line in lines)
                {
                    deptText += line + "。";
                }

                

                article.Add(deptText);
            }

            return string.Join("\n\n", article);
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