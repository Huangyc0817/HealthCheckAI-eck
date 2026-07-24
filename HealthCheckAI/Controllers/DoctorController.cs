using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthCheckAI.Helpers;
using HealthCheckAI.ML;
using HealthCheckAI.Models;
using HealthCheckAI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HealthCheckAI.Controllers
{
    public class DoctorController : Controller
    {

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly OcrService _ocrService;
        // ✅ 只保留這一個建構子
        public DoctorController(
            AppDbContext context,
            IWebHostEnvironment environment,
            OcrService ocrService)
        {
            _context = context;
            _environment = environment;
            _ocrService = ocrService;
        }


        public IActionResult TrainAI()
        {
            var ai = new AIAnalysisService();
            string csvPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "patient_data.csv");
            ViewBag.Result = ai.TrainModel(csvPath);
            return View();
        }


        [HttpPost]
        public IActionResult ExtractText(int id)
        {
            Console.WriteLine("🔥🔥🔥 進到 ExtractText 🔥🔥🔥");

            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);
            if (f == null) return NotFound();

            var path = GetStoredPathById(id);
            if (path == null || !System.IO.File.Exists(path))
                return NotFound("找不到實體檔案");

            Console.WriteLine("科別：" + f.Department);
            Console.WriteLine("檔案路徑：" + path);

            string text = "";

            if ((f.Department ?? "").Contains("心電圖") && IsImageFile(path))
            {
                try
                {
                    text = _ocrService.ExtractTextFromImage(path);
                    Console.WriteLine("✅ 真正 OCR 結果：" + text);
                }
                catch (Exception ex)
                {
                    text = "OCR 錯誤：" + ex.Message;
                    Console.WriteLine(text);
                }
            }
            else
            {
                var extractor = new FileTextExtractor();

                text = extractor.Extract(
                    path,
                    string.IsNullOrWhiteSpace(f.ContentType)
                        ? MimeTypes.GetMimeType(path)
                        : f.ContentType,
                    f.Department,
                    _ocrService // 👈 B 方案：把 OCR 武器借給文字抽取器！
                );

                Console.WriteLine("✅ 一般抽取結果：" + text);
            }

            //text = TextFormatter.FormatReportText(text);
            //text = TextFormatter.RebuildPhysicalExamLines(text);

            f.ExtractedText = text;
            f.UploadedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["Message"] = "📝 抽取完成！";
            return RedirectToAction("PatientFiles", new { name = f.PatientName });
        }

        // 👉 記得在 DoctorController 頂端的建構子 (Constructor) 注入剛剛寫的 GeminiService
        // 為了不影響你其他的注入，你可以直接在 AnalyzeText 裡面「現場 new 出來」使用，最不容易改壞：

        [HttpPost]
        public async Task<IActionResult> AnalyzeText(int id)
        {
            var file = _context.PatientFiles.FirstOrDefault(f => f.Id == id);
            if (file == null) return NotFound();

            if (file.IsPublishedToPublic)
            {
                TempData["Message"] = "此檔案已上傳至來賓端，無法再次執行 AI 分析。";
                return RedirectToAction("PatientFiles", new { name = file.PatientName });
            }

            if (string.IsNullOrWhiteSpace(file.ExtractedText))
            {
                TempData["Message"] = "尚未抽取文字，請先點「抽取文字」。";
                return RedirectToAction("PatientFiles", new { name = file.PatientName });
            }

            // 1. 清理抽取文字
            var cleanedText = PreprocessExtractedText(file.Department, file.ExtractedText);

            // 2. 呼叫 Gemini 產生內容
            var gemini = new GeminiService(_context.GetService<IConfiguration>());
            string realAiSummary = await gemini.GenerateHealthSummaryAsync(file.Department, cleanedText);

            // 3. 自動解析風險程度 (預設為低)
            string severity = "低";
            var match = System.Text.RegularExpressions.Regex.Match(realAiSummary, @"需追蹤程度：\s*(高|中|低)");
            if (match.Success)
            {
                severity = match.Groups[1].Value;
            }

            // 4. 組合完整字串，讓 ReportRenderHelper 順利切割
            string summaryText =
                "AI 綜合分析結果\n\n" +
                $"科別：{file.Department}\n" +
                realAiSummary;

            // 5. 寫回資料庫
            file.AiSeverity = severity;
            file.AiSummary = summaryText;
            file.AiScore = ConvertSeverityToScore(severity);

            _context.PatientFiles.Update(file);
            _context.SaveChanges();

            TempData["Message"] = "🔮 AI 綜合分析與摘要完成！";

            return RedirectToAction("EditReports", new { fileId = file.Id });
        }

        private string PreprocessExtractedText(string department, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace("：", ":")
                       .Replace("（", "(")
                       .Replace("）", ")")
                       .Replace("\r\n", "\n")
                       .Trim();

            var lines = text.Split('\n')
                            .Select(x => x.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

            // 先針對體格檢查表做簡單清理
            if (department == "體格檢查表")
            {
                lines = lines
                    .Where(x => !x.Contains("理想體重範圍公式"))
                    .ToList();
            }

            return string.Join("\n", lines);
        }

        private double? ExtractBmi(string text)
        {
            var match = Regex.Match(text, @"BMI\s*[:：]?\s*(\d+(\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double bmi))
            {
                return bmi;
            }
            return null;
        }

        private string GetBmiLevel(double bmi)
        {
            if (bmi < 18.5) return "過輕";
            if (bmi < 24) return "正常";
            if (bmi < 27) return "過重";
            return "肥胖";
        }




        [HttpPost]
        public async Task<IActionResult> BulkAnalyze(string name, List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Message"] = "請先勾選要進行 AI 分析的報告。";
                return RedirectToAction("PatientFiles", new { name });
            }

            var files = _context.PatientFiles
                .Where(f => selectedIds.Contains(f.Id))
                .ToList();

            int success = 0;
            int skippedNoText = 0;
            int skippedPublished = 0;

            // 🌟 在迴圈外先準備好 Gemini 服務
            var gemini = new GeminiService(_context.GetService<IConfiguration>());

            foreach (var file in files)
            {
                // 已經上傳到來賓端的不重跑 AI
                if (file.IsPublishedToPublic)
                {
                    skippedPublished++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.ExtractedText))
                {
                    skippedNoText++;
                    continue;
                }

                // ✅ 1. 先清理抽取文字
                var cleanedText = PreprocessExtractedText(file.Department, file.ExtractedText);

                // ✅ 2. 呼叫 Gemini 取得摘要
                string realAiSummary = await gemini.GenerateHealthSummaryAsync(file.Department, cleanedText);

                // ✅ 3. 從 Gemini 的回答中，自動抓取「需追蹤程度」
                string severity = "低"; // 預設值
                var match = System.Text.RegularExpressions.Regex.Match(realAiSummary, @"需追蹤程度：\s*(高|中|低)");
                if (match.Success)
                {
                    severity = match.Groups[1].Value;
                }

                // ✅ 4. 組合最終字串
                string summaryText =
                    $"AI 綜合分析結果\n\n" +
                    $"來賓：{file.PatientName}\n" +
                    $"科別：{file.Department}\n" +
                    realAiSummary;

                // 🔹 5. 寫回屬性
                file.AiSummary = summaryText;
                file.AiSeverity = severity;
                file.AiScore = ConvertSeverityToScore(severity);

                success++;
            }

            if (success > 0)
            {
                _context.SaveChanges();
            }

            TempData["Message"] =
                $"已完成 {success} 筆 AI 分析" +
                (skippedNoText > 0 ? $"，略過 {skippedNoText} 筆（尚未抽取文字）" : "") +
                (skippedPublished > 0 ? $"，略過 {skippedPublished} 筆（已上傳來賓端）。" : "。");

            return RedirectToAction("EditReports", new { name });
        }

        public IActionResult Index()
        {
            // 從 Session 抓出登入者名字
            var name = HttpContext.Session.GetString("Name") ?? "醫師";

            ViewBag.UserName = name;
            return View();
        }


        [HttpGet]
        public IActionResult UploadReports(string name)
        {
            // name: 來賓姓名（你現有流程應該會帶過來）
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Message"] = "缺少來賓姓名。";
                return RedirectToAction("PatientList");
            }
            ViewBag.PatientName = name;
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> UploadReports(
     string patientName,
     List<IFormFile> files)
        {
            Console.WriteLine($"[UploadReports-POST] patientName = '{patientName}', files = {files?.Count ?? 0}");

            // 1️⃣ 檢查有沒有帶來賓名字
            if (string.IsNullOrWhiteSpace(patientName))
            {
                TempData["Message"] = "缺少來賓姓名。";
                return RedirectToAction(nameof(UploadReports), new { name = patientName });
            }

            // 2️⃣ 檢查有沒有選檔案
            if (files == null || !files.Any())
            {
                TempData["Message"] = "請選擇至少一個檔案。";
                return RedirectToAction(nameof(UploadReports), new { name = patientName });
            }

            // 3️⃣ 保存路徑：/wwwroot/uploads/{來賓姓名}/{yyyyMMdd}/
            var root = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                patientName,
                DateTime.Now.ToString("yyyyMMdd"));

            Directory.CreateDirectory(root);

            foreach (var f in files)
            {
                if (f.Length <= 0) continue;

                var originalName = Path.GetFileName(f.FileName);
                // 產生不重覆檔名（真的存的檔名）
                var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(originalName)}";
                var savePath = Path.Combine(root, safeName);

                // 🧾 把檔案寫進去
                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await f.CopyToAsync(stream);
                }

                // 🗄 寫入 ReportFiles
                var row = new ReportFile
                {
                    PatientName = patientName,      // 👈 這裡存「林小明」
                    OriginalName = originalName,    // 原始檔名
                    ContentType = string.IsNullOrWhiteSpace(f.ContentType)
                                    ? MimeTypes.GetMimeType(savePath)
                                    : f.ContentType,
                    UploadedAt = DateTime.Now
                };

                _context.ReportFiles.Add(row);
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = "上傳成功！";

            // 回到這個來賓的上傳清單頁（參數叫 name）
            return RedirectToAction(nameof(PatientFiles), new { name = patientName });
        }


        public IActionResult PatientList()
        {
            var files = _context.PatientFiles
                .OrderByDescending(x => x.UploadedAt)
                .ToList();

            return View(files);   // ✅ 一定要傳
        }



        public IActionResult PatientFiles(string name)
        {
            var files = _context.PatientFiles
                .Where(x => x.PatientName == name)
                .OrderByDescending(x => x.UploadedAt == default ?
                x.UploadDate : x.UploadedAt)
                .ToList();
            var user = _context.Users.FirstOrDefault(
                u => u.Username == name); 
            var displayName = user?.Name ?? name;

            // 計算有實體檔的 Id 清單（檔案以「Id.*」存在 wwwroot/uploads）
            var has = new HashSet<int>();
            var root = Path.Combine(_environment.WebRootPath, "uploads");
            if (Directory.Exists(root))
            {
                foreach (var f in files)
                {
                    var matched = Directory.GetFiles(root, f.Id + ".*");
                    if (matched.Any()) has.Add(f.Id);
                }
            }

            ViewBag.PatientName = displayName;// 顯示 Aaa
            ViewBag.PatientId = name;// 帳號 A123456789
            ViewBag.Name = name;
            ViewBag.HasFileIds = has;   // 傳給 View 用來啟/關按鈕

            return View(files);
        }

        private string? ResolveUploadedFilePath(string patientName, string department)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(folder)) return null;

            // 例如：王小明_心臟內科.pdf / 王小明_心臟內科.jpg ...
            var pattern = $"{patientName}_{department}*";
            var files = Directory.GetFiles(folder, pattern);
            if (files.Length == 0) return null;

            return files
             .Select(f => new FileInfo(f))
             .OrderByDescending(fi => fi.LastWriteTimeUtc) // 最新
             .First()
             .FullName;
        }

        private string? GetStoredPathById(int id)
        {
            var root = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(root)) return null;

            // 找 id.*；若多個，以最新修改時間為準
            var pattern = id.ToString() + ".*";
            var matches = Directory.GetFiles(root, pattern);
            return matches
                .OrderByDescending(p => System.IO.File.GetLastWriteTimeUtc(p))
                .FirstOrDefault();
        }

        public IActionResult Preview(int id)
        {
            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);
            if (f == null) return NotFound();

            var path = GetStoredPathById(id);
            if (path == null || !System.IO.File.Exists(path))
                return NotFound("找不到實體檔案");

            var contentType = string.IsNullOrWhiteSpace(f.ContentType)
                                ? MimeTypes.GetMimeType(path)
                                : f.ContentType;

            // inline 顯示
            return PhysicalFile(path, contentType, enableRangeProcessing: true);
        }

        public IActionResult Download(int id)
        {
            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);
            if (f == null) return NotFound();

            var path = GetStoredPathById(id);
            if (path == null || !System.IO.File.Exists(path))
                return NotFound("找不到實體檔案");

            var contentType = string.IsNullOrWhiteSpace(f.ContentType)
                                ? MimeTypes.GetMimeType(path)
                                : f.ContentType;

            var downloadName = $"{f.PatientName}_{f.Department}{Path.GetExtension(path)}";
            return PhysicalFile(path, contentType, fileDownloadName: downloadName);
        }

        [HttpPost]
        public IActionResult UploadSelected(string name, List<string> selectedDepts, IFormFile uploadedFile)
        {
            if (uploadedFile != null && uploadedFile.Length > 0)
            {

                string uploadPath = Path.Combine(_environment.WebRootPath, "uploads");

                using (var stream = new FileStream(uploadPath, FileMode.Create))
                {
                    uploadedFile.CopyTo(stream);
                }

                foreach (var dept in selectedDepts)
                {
                    var file = new PatientFile
                    {
                        PatientName = name,
                        Department = dept,
                        UploadDate = DateTime.Now,

                    };

                    _context.PatientFiles.Add(file);
                }

                _context.SaveChanges();
                TempData["Message"] = "上傳成功！";
            }

            return RedirectToAction("PatientFiles", new { name });
        }

        public IActionResult EditReports(int? fileId)
        {
            var aiFiles = _context.PatientFiles
                .Where(p => !string.IsNullOrEmpty(p.AiSummary))
                .OrderBy(p => p.PatientName)
                .ThenByDescending(p => p.UploadedAt == default ? p.UploadDate : p.UploadedAt)
                .ToList();



            var groups = aiFiles
                .GroupBy(p => p.PatientName)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.AiGroups = groups;

            PatientFile? selected = null;

            if (fileId.HasValue)
            {
                selected = aiFiles.FirstOrDefault(p => p.Id == fileId.Value);
            }

            ViewBag.OriginalExtractedText = selected?.ExtractedText ?? "";

            var aiContent = selected?.AiSummary ?? "";
            var extractedContent = selected?.ExtractedText ?? "";

            var tableParts = ReportRenderHelper.Split(extractedContent, selected?.Department);

            if ((tableParts.TableRows == null || !tableParts.TableRows.Any()) &&
                string.IsNullOrWhiteSpace(tableParts.TableRawText))
            {
                tableParts.TableRawText = extractedContent;
            }

            // 🔥 只有這行是我們剛剛新加的，確保把完整文字灌進去
            tableParts.TableRawText = extractedContent;

            var aiParts = ReportRenderHelper.Split(aiContent);

            var finalSuggestions = aiParts.SuggestionsText;

            if (!string.IsNullOrWhiteSpace(selected?.Department) && selected.Department.Contains("眼"))
            {
                var eyeSuggestion = EyeSuggestionHelper.GenerateVisionSuggestion(extractedContent);

                if (!string.IsNullOrWhiteSpace(eyeSuggestion))
                {
                    if (!string.IsNullOrWhiteSpace(finalSuggestions))
                        finalSuggestions = eyeSuggestion + Environment.NewLine + finalSuggestions;
                    else
                        finalSuggestions = eyeSuggestion;
                }
            }

            ViewBag.SelectedFileId = selected?.Id ?? 0;
            ViewBag.SelectedPatient = selected?.PatientName ?? "";
            ViewBag.ReportContent = aiContent;
            ViewBag.ReportParts = tableParts;
            ViewBag.AiBeforeText = aiParts.BeforeText;

            // 💡 自動判斷：如果「重點整理」是空的，就去抓「內容摘要 (BeforeText)」的內容
            string firstBoxText = !string.IsNullOrWhiteSpace(aiParts.KeyPointsText)
                ? aiParts.KeyPointsText
                : aiParts.BeforeText;

            // 把抓到的內容塞給前端，如果兩個都空，才真正觸發除錯模式
            ViewBag.AiKeyPointsPart = string.IsNullOrWhiteSpace(firstBoxText)
                ? "【除錯模式】未辨識到摘要標題，原始資料如下：\n" + aiContent
                : firstBoxText;

            ViewBag.AiSuggestionsPart = finalSuggestions;

            return View();
        }

        [HttpPost]
        public IActionResult EditReports(int fileId, string keyPoints, string suggestions, List<string> cellValues, string actionType)
        {
            // 1. 從資料庫抓出該筆報告
            var file = _context.PatientFiles.Find(fileId);
            if (file == null) return NotFound();

            // 2. 不管是暫存還是上傳，都必須先儲存醫師修改後的內容摘要與健康建議
            file.AiSummary = $"內容摘要：\n{keyPoints}\n\n健康建議：\n{suggestions}";

            // (如果你原本有寫更新表格 cellValues 的邏輯，請保留在這裡)

            // 3. 【核心修正】根據按下的按鈕類型 (actionType) 做不同處理
            if (actionType == "upload")
            {
                // 💡 如果點擊的是「上傳來賓端」，強制將發布狀態改為 true
                file.IsPublishedToPublic = true;
            }
            else if (actionType == "save")
            {
                // 如果是點擊「編輯(暫存)」，則維持原狀（不改變發布狀態）
                // file.IsPublishedToPublic = false; 
            }

            // 4. 儲存變更至資料庫
            _context.SaveChanges();

            // 5. 透過 PRG 模式重新導向（帶回最新的狀態，讓前端正確渲染鎖定畫面）
            return RedirectToAction("EditReports", new { fileId = fileId });
        }



        [HttpPost]
        public IActionResult PublishAllForPatient(string patientName)
        {
            if (string.IsNullOrWhiteSpace(patientName))
            {
                TempData["Message"] = "⚠️ 缺少來賓姓名，無法一鍵上傳。";
                return RedirectToAction("EditReports");
            }

            // 撈出這位來賓所有「已有 AI 結果、尚未上傳來賓端」的檔案
            var aiFiles = _context.PatientFiles
                .Where(p => p.PatientName == patientName &&
                            !string.IsNullOrEmpty(p.AiSummary) &&
                            !p.IsPublishedToPublic)
                .ToList();

            if (!aiFiles.Any())
            {
                TempData["Message"] = $"⚠️ {patientName} 目前沒有可一鍵上傳的 AI 報告。";
                return RedirectToAction("EditReports");
            }

            // 通通標記為已發布
            foreach (var f in aiFiles)
            {
                f.IsPublishedToPublic = true;
                f.PublishedAt = DateTime.Now;
            }

            _context.SaveChanges();

            TempData["Message"] = $"✅ 已將 {aiFiles.Count} 份 {patientName} 的 AI 報告一鍵上傳至來賓端。";

            return RedirectToAction("EditReports");
        }

        [HttpPost]
        public IActionResult UploadFile(string patientName, string department, List<IFormFile> files)
        {
            // patientName 現在代表「帳號 / Username」
            if (string.IsNullOrWhiteSpace(patientName))
            {
                TempData["Message"] = "⚠️ 請輸入來賓帳號。";
                return RedirectToAction("Index");
            }

            // ✅ 用帳號去 Users 表找人
            var user = _context.Users.FirstOrDefault(u => u.Username == patientName);

            if (user == null)
            {
                TempData["Message"] = $"⚠️ 找不到帳號為 {patientName} 的來賓。";
                return RedirectToAction("Index");
            }

            if (files == null || files.Count == 0)
            {
                TempData["Message"] = "⚠️ 請選擇檔案後再上傳。";
                return RedirectToAction("Index");
            }

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadRoot)) Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var entity = new PatientFile
                {
                    // ✅ 重點：PatientFiles.PatientName 存 Username，不存姓名
                    PatientName = user.Username,

                    Department = department,
                    UploadDate = DateTime.Now,
                    UploadedAt = DateTime.Now,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                                ? null
                                : file.ContentType
                };

                _context.PatientFiles.Add(entity);
                _context.SaveChanges();

                var ext = Path.GetExtension(file.FileName);
                var stored = $"{entity.Id}{ext}";
                var filePath = Path.Combine(uploadRoot, stored);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);

                if (string.IsNullOrWhiteSpace(entity.ContentType))
                {
                    entity.ContentType = MimeTypes.GetMimeType(filePath);
                    _context.SaveChanges();
                }
            }

            TempData["Message"] = $"✅ 成功上傳 {files.Count} 份檔案給 {user.Name}！";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);

            if (f != null)
            {
                // 先抓 username（用來 redirect）
                var username = f.PatientName; // ⚠️ 這裡其實就是帳號

                // 刪實體檔案
                var path = GetStoredPathById(id);
                if (path != null && System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }

                // 刪 DB
                _context.PatientFiles.Remove(f);
                _context.SaveChanges();

                TempData["Message"] = "✅ 已刪除一筆上傳紀錄。";

                // 🔁 用 username 回去
                return RedirectToAction("PatientFiles", new { name = username });
            }

            return RedirectToAction("PatientFiles");
        }

        private int ConvertSeverityToScore(string severity)
        {
            if (string.IsNullOrWhiteSpace(severity)) return 0;

            if (severity.Contains("高"))
                return 85 + Random.Shared.Next(0, 10);

            if (severity.Contains("中"))
                return 55 + Random.Shared.Next(0, 10);

            if (severity.Contains("低"))
                return 25 + Random.Shared.Next(0, 10);

            return 0;
        }
        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            return ext == ".png"
                || ext == ".jpg"
                || ext == ".jpeg"
                || ext == ".bmp"
                || ext == ".tif"
                || ext == ".tiff";
        }

        private string RebuildTableText(List<string> cellValues, int columnCount, ReportType reportType)
        {
            if (cellValues == null || !cellValues.Any() || columnCount <= 0)
                return "";

            var lines = new List<string>();

            for (int i = 0; i < cellValues.Count; i += columnCount)
            {
                var row = cellValues.Skip(i).Take(columnCount).ToList();

                while (row.Count < columnCount)
                    row.Add("");

                if (reportType == ReportType.Eye)
                {
                    // 眼別 視力裸視 矯正視力 眼壓(<21) 電腦驗光 散光 辨色力
                    var line = string.Join(" ", row.Where(x => !string.IsNullOrWhiteSpace(x)));
                    lines.Add(line);
                }
                else if (reportType == ReportType.Laboratory)
                {
                    // 項目 本次 前次 參考值
                    var item = row[0];
                    var result = row[1];
                    var previous = row[2];
                    var reference = row[3];
                    lines.Add($"{item} {result} {previous} {reference}".Trim());
                }
                else if (reportType == ReportType.PhysicalExam)
                {
                    // 項目 結果 參考值
                    var item = row[0];
                    var result = row[1];
                    var reference = row[2];
                    lines.Add($"{item} {result} {reference}".Trim());
                }
                else if (reportType == ReportType.SimplePhysical)
                {
                    // 項目 結果
                    var item = row[0];
                    var result = row[1];
                    lines.Add($"{item} {result}".Trim());
                }
                else
                {
                    var line = string.Join(" ", row.Where(x => !string.IsNullOrWhiteSpace(x)));
                    lines.Add(line);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

    }
}