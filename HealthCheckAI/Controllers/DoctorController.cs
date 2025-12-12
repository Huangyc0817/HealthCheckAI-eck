using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthCheckAI.Models;
using HealthCheckAI.Services;
using HealthCheckAI.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Http;
using HealthCheckAI.ML;





namespace HealthCheckAI.Controllers
{
    public class DoctorController : Controller
    {

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IAiPredictionService _ai;

        // ✅ 只保留這一個建構子
        public DoctorController(
            AppDbContext context,
            IWebHostEnvironment environment,
            IAiPredictionService ai)
        {
            _context = context;
            _environment = environment;
            _ai = ai;
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
            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);
            if (f == null) return NotFound();

            var path = GetStoredPathById(id);
            if (path == null || !System.IO.File.Exists(path))
                return NotFound("找不到實體檔案");

            var extractor = new FileTextExtractor();
            var text = extractor.Extract(
                path,
                string.IsNullOrWhiteSpace(f.ContentType)
                    ? MimeTypes.GetMimeType(path)
                    : f.ContentType
            );

            // ⭐⭐ 抽出來先丟進 TextFormatter 清理
            text = TextFormatter.FormatReportText(text);

            f.ExtractedText = text;
            f.UploadedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["Message"] = "📝 抽取完成！";
            return RedirectToAction("PatientFiles", new { name = f.PatientName });
        }



        [HttpPost]
        public IActionResult AnalyzeText(int id)
        {
            var file = _context.PatientFiles.FirstOrDefault(f => f.Id == id);
            if (file == null)
            {
                TempData["Message"] = "找不到檔案紀錄。";
                return RedirectToAction("PatientFiles", new { name = "" });
            }

            if (file.IsPublishedToPublic)
            {
                TempData["Message"] = "此檔案已上傳至民眾端，無法再次執行 AI 分析。";
                return RedirectToAction("PatientFiles", new { name = file.PatientName });
            }

            if (string.IsNullOrWhiteSpace(file.ExtractedText))
            {
                TempData["Message"] = "尚未抽取文字，請先點「抽取文字」。";
                return RedirectToAction("PatientFiles", new { name = file.PatientName });
            }

            // ✅ 1. 呼叫 AI 分析
            var result = _ai.Analyze(file.PatientName, file.Department, file.ExtractedText);
            // 假設：result.SeverityLevel = "高" / "中" / "低"

            string summaryText =
                "AI 綜合分析結果\n\n" +
                $"病人：{file.PatientName}\n" +
                $"科別：{file.Department}\n" +
                $"需追蹤程度：{result.SeverityLevel}（信心度 {result.Probability:P1}）\n\n" +
                "內容摘要：\n" +
                $"{result.Summary}\n\n" +
                "重點整理：\n" +
                $"{result.KeyPoints}\n\n" +
                "健康建議：\n" +
                $"{result.Suggestions}";

            // ✅ 2. 把 AI 結果寫回欄位
            file.AiSeverity = result.SeverityLevel;
            file.AiSummary = summaryText;

            // ✅ 3. 用嚴重程度算分數（0~100）
            var level = (result.SeverityLevel ?? "").Trim();   // 先保險處理 null & 空白
            int score = 0;
            if (level.Contains("高")) score = 90;
            else if (level.Contains("中")) score = 60;
            else if (level.Contains("低")) score = 30;

            file.AiScore = score;   // ⭐⭐ 這一行一定要存在，且在 SaveChanges 之前

            _context.PatientFiles.Update(file);
            _context.SaveChanges();

            TempData["Message"] = "AI 分析完成，請在左側點選病患查看與編輯。";

            return RedirectToAction("EditReports", new { fileId = file.Id });
        }







        [HttpPost]
        public IActionResult BulkAnalyze(string name, List<int> selectedIds)
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

            foreach (var file in files)
            {
                // 已經上傳到民眾端的不重跑 AI
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

                // ✅ 用新的 Analyze 一次拿到完整結果
                var result = _ai.Analyze(file.PatientName, file.Department, file.ExtractedText);

                string summaryText =
                    $"AI 綜合分析結果\n\n" +
                    $"病人：{file.PatientName}\n" +
                    $"科別：{file.Department}\n" +
                    $"需追蹤程度：{result.SeverityLevel}（相對風險：{result.Label}，信心度 {result.Probability:P1}）\n\n" +
                    $"內容摘要：\n{result.Summary}\n\n" +
                    $"重點整理：\n{result.KeyPoints}\n\n" +
                    $"健康建議：\n{result.Suggestions}";

                // 🔹 寫回 AI 結果
                file.AiSummary = summaryText;
                file.AiSeverity = result.SeverityLevel;

                // ⭐⭐⭐ 【最重要】計算 AiScore，批次分析也要寫！
                var level = (result.SeverityLevel ?? "").Trim();

                int score = 0;
                if (level.Contains("高")) score = 90;
                else if (level.Contains("中")) score = 60;
                else if (level.Contains("低")) score = 30;

                file.AiScore = score;   // ←← 批次分析少的就是這行

                success++;
            }

            if (success > 0)
            {
                _context.SaveChanges();
            }

            TempData["Message"] =
                $"已完成 {success} 筆 AI 分析" +
                (skippedNoText > 0 ? $"，略過 {skippedNoText} 筆（尚未抽取文字）" : "") +
                (skippedPublished > 0 ? $"，略過 {skippedPublished} 筆（已上傳民眾端）。" : "。");

            return RedirectToAction("PatientFiles", new { name });
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
            // name: 民眾姓名（你現有流程應該會帶過來）
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Message"] = "缺少民眾姓名。";
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

            // 1️⃣ 檢查有沒有帶病人名字
            if (string.IsNullOrWhiteSpace(patientName))
            {
                TempData["Message"] = "缺少民眾姓名。";
                return RedirectToAction(nameof(UploadReports), new { name = patientName });
            }

            // 2️⃣ 檢查有沒有選檔案
            if (files == null || !files.Any())
            {
                TempData["Message"] = "請選擇至少一個檔案。";
                return RedirectToAction(nameof(UploadReports), new { name = patientName });
            }

            // 3️⃣ 保存路徑：/wwwroot/uploads/{民眾姓名}/{yyyyMMdd}/
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

            // 回到這個病人的上傳清單頁（參數叫 name）
            return RedirectToAction(nameof(PatientFiles), new { name = patientName });
        }




        public IActionResult PatientList()
        {
            var patients = _context.PatientFiles
                .GroupBy(p => p.PatientName)
                .Select(g => new
                {
                    Name = g.Key,
                    CreatedAt = g.Min(x => x.UploadedAt == default ? x.UploadDate : x.UploadedAt) // 最早時間
                })
                .OrderBy(x => x.CreatedAt) // 最早的在前面（也可改成 OrderByDescending 看你喜歡）
                .ToList();

            ViewBag.Patients = patients;
            return View();
        }



        public IActionResult PatientFiles(string name)
        {
            var files = _context.PatientFiles
           .Where(x => x.PatientName == name)
           .OrderByDescending(x => x.UploadedAt == default ? x.UploadDate : x.UploadedAt)
           .ToList();

            var user = _context.Users.FirstOrDefault(u => u.Username == name);
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

            ViewBag.PatientName = displayName;
            ViewBag.PatientId = name;         // 👈 真正帳號
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
            // 撈出所有已經有 AiSummary 的檔案
            var aiFiles = _context.PatientFiles
                .Where(p => !string.IsNullOrEmpty(p.AiSummary))
                .OrderBy(p => p.PatientName)
                .ThenByDescending(p => p.UploadedAt == default ? p.UploadDate : p.UploadedAt)
                .ToList();

            // 分組：同一個病人放一起
            var groups = aiFiles
                .GroupBy(p => p.PatientName)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.AiGroups = groups;

            PatientFile? selected = null;
            if (fileId.HasValue)
            {
                selected = aiFiles.FirstOrDefault(p => p.Id == fileId.Value);
            }

            ViewBag.SelectedFileId = selected?.Id ?? 0;
            ViewBag.SelectedPatient = selected?.PatientName ?? "";
            ViewBag.ReportContent = selected?.AiSummary ?? "";

            return View();
        }


        [HttpPost]
        public IActionResult EditReports(int fileId, string selectedPatient, string reportContent, string actionType)
        {
            if (fileId == 0)
            {
                TempData["Message"] = "請先在左側選擇要編輯的報告。";
                return RedirectToAction("EditReports");
            }

            var file = _context.PatientFiles.FirstOrDefault(p => p.Id == fileId);
            if (file == null)
            {
                TempData["Message"] = "找不到這份報告。";
                return RedirectToAction("EditReports");
            }

            // 不管暫存或上傳，都先把目前編輯好的內容寫回 AiSummary
            file.AiSummary = reportContent;

            if (actionType == "save")
            {
                TempData["Message"] = $"{file.PatientName} 的報告已暫存修改。";
            }
            else if (actionType == "upload")
            {
                // ✅ 這裡就是「上傳到民眾端」
                file.IsPublishedToPublic = true;
                file.PublishedAt = DateTime.Now;

                TempData["Message"] = $"{file.PatientName} 的報告已上傳至民眾端，可供查閱。";
            }

            _context.SaveChanges();

            // 回到同一份報告，方便醫師確認
            return RedirectToAction("EditReports", new { fileId = file.Id });
        }

        [HttpPost]
        public IActionResult PublishAllForPatient(string patientName)
        {
            if (string.IsNullOrWhiteSpace(patientName))
            {
                TempData["Message"] = "⚠️ 缺少病人姓名，無法一鍵上傳。";
                return RedirectToAction("EditReports");
            }

            // 撈出這位病人所有「已有 AI 結果、尚未上傳民眾端」的檔案
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

            TempData["Message"] = $"✅ 已將 {aiFiles.Count} 份 {patientName} 的 AI 報告一鍵上傳至民眾端。";

            return RedirectToAction("EditReports");
        }






        [HttpPost]
        public IActionResult UploadFile(string patientName, string department, List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                TempData["Message"] = "⚠️ 請選擇檔案後再上傳。";
                return RedirectToAction("Index");
            }

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadRoot)) Directory.CreateDirectory(uploadRoot);

            // 先寫入 DB 取得 Id（不存 FileName）
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var entity = new PatientFile
                {
                    PatientName = patientName,
                    Department = department,
                    UploadDate = DateTime.Now,
                    UploadedAt = DateTime.Now,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                                ? null
                                : file.ContentType
                };
                _context.PatientFiles.Add(entity);
                _context.SaveChanges();   // 取得自動編號 Id

                // 以「Id.副檔名」存檔
                var ext = Path.GetExtension(file.FileName); // 例 .pdf
                var stored = $"{entity.Id}{ext}";
                var filePath = Path.Combine(uploadRoot, stored);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);

                // 若 ContentType 沒有值，就用副檔名推
                if (string.IsNullOrWhiteSpace(entity.ContentType))
                {
                    entity.ContentType = MimeTypes.GetMimeType(filePath);
                    _context.SaveChanges();
                }
            }

            TempData["Message"] = $"✅ 成功上傳 {files.Count} 份檔案！";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(int id, string name)
        {
            var f = _context.PatientFiles.FirstOrDefault(x => x.Id == id);
            if (f != null)
            {
                // 如果有實體檔案也要一起刪，可以順便處理
                var path = GetStoredPathById(id);
                if (path != null && System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }

                _context.PatientFiles.Remove(f);
                _context.SaveChanges();
            }

            TempData["Message"] = "✅ 已刪除一筆上傳紀錄。";

            // 🔁 回到剛剛那個病人的上傳清單頁
            return RedirectToAction("PatientFiles", new { name = name });
        }

       


    }
}
