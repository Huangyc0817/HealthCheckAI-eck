using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Helpers
{
    public class ReportParts
    {
        public string BeforeText { get; set; } = "";
        public List<PhysicalExamRow> TableRows { get; set; } = new();
        public string TableRawText { get; set; } = "";
        public string KeyPointsText { get; set; } = "";
        public string SuggestionsText { get; set; } = "";
        public ReportType ReportType { get; set; } = ReportType.Unknown;
    }

    public static class ReportRenderHelper
    {
        private static readonly string[] LabSections =
        {
            "血液檢查", "生化檢查", "肝功能檢查", "腎功能檢查", "血脂肪檢查",
            "糖尿病檢查", "痛風檢查", "胰臟功能檢查", "心臟血管功能檢查",
            "甲狀腺檢查", "肝炎標記", "血液腫瘤標誌", "其它檢查", "尿液檢查"
        };

        public static ReportParts Split(string? reportContent, string? category = null)
        {
            var parts = new ReportParts();

            var content = (reportContent ?? "")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            if (string.IsNullOrWhiteSpace(content))
                return parts;

            content = Regex.Replace(content, @"\*{0,2}內容摘要\*{0,2}\s*[:：]", "內容摘要：");
            content = Regex.Replace(content, @"\*{0,2}重點整理\*{0,2}\s*[:：]", "重點整理：");
            content = Regex.Replace(content, @"\*{0,2}健康建議\*{0,2}\s*[:：]", "健康建議：");

            var reportType = !string.IsNullOrWhiteSpace(category)
                ? ReportClassifier.FromCategory(category)
                : DetectTypeFromText(content);

            string header = "";
            string summary = "";
            string keyPoints = "";
            string suggestions = "";
            string tablePart = "";

            var mHeader = Regex.Match(content, @"^(.*?)(內容摘要：)", RegexOptions.Singleline);
            if (mHeader.Success)
                header = mHeader.Groups[1].Value.Trim();

            int summaryStart = content.IndexOf("內容摘要：", StringComparison.Ordinal);
            int keyIdx = content.IndexOf("重點整理：", StringComparison.Ordinal);
            int sugIdx = content.IndexOf("健康建議：", StringComparison.Ordinal);
            int tableIdx = FindTableStartIndex(content);

            if (summaryStart >= 0)
            {
                int start = summaryStart + "內容摘要：".Length;

                var candidates = new List<int>();
                if (tableIdx > start) candidates.Add(tableIdx);
                if (keyIdx > start) candidates.Add(keyIdx);
                if (sugIdx > start) candidates.Add(sugIdx);

                int end = candidates.Any() ? candidates.Min() : content.Length;
                summary = content.Substring(start, end - start).Trim();
            }

            if (tableIdx >= 0)
            {
                var candidates = new List<int>();
                if (keyIdx > tableIdx) candidates.Add(keyIdx);
                if (sugIdx > tableIdx) candidates.Add(sugIdx);

                int tableEnd = candidates.Any() ? candidates.Min() : content.Length;
                tablePart = content.Substring(tableIdx, tableEnd - tableIdx).Trim();
            }

            if (keyIdx >= 0)
            {
                int start = keyIdx + "重點整理：".Length;
                int end = (sugIdx > start) ? sugIdx : content.Length;
                keyPoints = content.Substring(start, end - start).Trim();
            }

            if (sugIdx >= 0)
            {
                int start = sugIdx + "健康建議：".Length;
                suggestions = content.Substring(start).Trim();
            }

            var rows = new List<PhysicalExamRow>();
            string tableRawText = "";

            switch (reportType)
            {
                case ReportType.SimplePhysical:
                    // 1. 先用原本的解析器抓出那 13 項標準資料
                    rows = SimplePhysicalParser.Parse(content);
                    tableRawText = content;

                    // 2. 手動捕捉文字中的「其他」與「建議」，強行塞進表格列中
                    if (content.Contains("其他"))
                    {
                        var matchOther = Regex.Match(content, @"其他.*?(?:Others)?\)?[,\t\s:]+(?<result>[^\n]+)", RegexOptions.IgnoreCase);
                        if (matchOther.Success)
                        {
                            rows.Add(new PhysicalExamRow { Item = "其他(Others)", Result = matchOther.Groups["result"].Value.Trim(), IsSection = false });
                        }
                    }

                    if (content.Contains("建議"))
                    {
                        var matchSug = Regex.Match(content, @"建議.*?(?:Suggestion)?\)?[,\t\s:]+(?<result>[^\n]+)", RegexOptions.IgnoreCase);
                        if (matchSug.Success)
                        {
                            rows.Add(new PhysicalExamRow { Item = "建議(Suggestion)", Result = matchSug.Groups["result"].Value.Trim(), IsSection = false });
                        }
                    }

                    // 💡 去重複機制：將 Item 相同的項目合併，只保留第一筆
                    rows = rows.GroupBy(r => r.Item).Select(g => g.First()).ToList();
                    break;

                case ReportType.PhysicalExam:
                    rows = PhysicalExamParser.Parse(content);
                    rows = rows.GroupBy(r => r.Item)
                               .Select(g => g.OrderByDescending(x => (x.Result ?? "").Length).First())
                               .ToList();
                    tableRawText = content;
                    break;

                case ReportType.Laboratory:
                    rows = ParseLaboratoryTable(tablePart);
                    tableRawText = tablePart;
                    break;

                case ReportType.Eye:
                    {
                        var eyeSource = content;
                        rows = ParseEyeTable(eyeSource);
                        tableRawText = ExtractEyeDiagnosisText(eyeSource);
                        break;
                    }

                case ReportType.ECG:
                    {
                        rows = new List<PhysicalExamRow>();
                        var ecgSource = !string.IsNullOrWhiteSpace(tablePart) ? tablePart : content;
                        var ecg = EcgReportParser.Parse(ecgSource);

                        tableRawText =
                            "【心電圖儀器參數】\n" +
                            $"Heart Rate：{Format(ecg.HeartRate)}\n" +
                            $"PR Interval：{Format(ecg.PRInterval)}\n" +
                            $"QRS Duration：{Format(ecg.QRSDuration)}\n" +
                            $"QT/QTc：{Format(ecg.QT_QTc)}\n" +
                            $"Axes：{Format(ecg.Axes)}\n" +
                            $"Machine Interpretation：{Format(ecg.MachineInterpretation)}\n\n" +
                            "【AI分析】\n" +
                            ecg.Summary + "\n\n";
                        break;
                    }

                case ReportType.Ultrasound:
                case ReportType.Unknown:
                default:
                    rows = new List<PhysicalExamRow>();
                    tableRawText = UltrasoundTextFormatter.Format(tablePart);
                    break;
            }

            parts.BeforeText = string.IsNullOrWhiteSpace(summary) ? header : summary;
            parts.TableRows = rows;
            parts.TableRawText = tableRawText;
            parts.KeyPointsText = keyPoints;
            parts.SuggestionsText = suggestions;
            parts.ReportType = reportType;

            return parts;
        }

        // 💡 補齊遺漏的 DetectTypeFromText
        private static ReportType DetectTypeFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ReportType.Unknown;

            if (text.Contains("理學檢查") || (text.Contains("Physical Examination") && text.Contains("無明顯異常")))
                return ReportType.SimplePhysical;

            if ((text.Contains("體格檢查") || text.Contains("Physical Examination")) && IsBodyCheckFormat(text))
                return ReportType.PhysicalExam;

            if (text.Contains("Laboratory Examination") || (text.Contains("檢查項目") && text.Contains("本次") && text.Contains("前次")))
                return ReportType.Laboratory;

            if (text.Contains("眼科檢查") || text.Contains("視力裸視") || text.Contains("電腦驗光") || text.Contains("辨色力"))
                return ReportType.Eye;

            if (text.Contains("心電圖") || text.Contains("ECG"))
                return ReportType.ECG;

            if (text.Contains("超音波") || text.Contains("儀器檢查") || text.Contains("精密儀器"))
                return ReportType.Ultrasound;

            return ReportType.Unknown;
        }

        // 💡 全新防呆版的 ParseEyeTable
        private static List<PhysicalExamRow> ParseEyeTable(string text)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(text))
                return rows;

            // 把所有空白、換行清空，解決 OCR 黏字問題
            string cleanText = Regex.Replace(text, @"\s+", "");

            // 暴力抓取數字區塊
            var matches = Regex.Matches(cleanText, @"(右眼|左眼)([0-9.]+)(---|[-0-9.]+)([0-9.]+)(-?[0-9.]+)(-?[0-9.]+)(無明顯異常|無異常|異常|正常|[^\s左右診]*)?");

            foreach (Match match in matches)
            {
                string eye = match.Groups[1].Value;

                // Reference 全部設為 "---" 避免 AbnormalHelper 發生轉換錯誤
                rows.Add(new PhysicalExamRow { Item = $"{eye} 視力裸視", Result = match.Groups[2].Value, Previous = "---", Reference = "---", IsSection = false });
                rows.Add(new PhysicalExamRow { Item = $"{eye} 矯正視力", Result = match.Groups[3].Value, Previous = "---", Reference = "---", IsSection = false });
                rows.Add(new PhysicalExamRow { Item = $"{eye} 眼壓", Result = match.Groups[4].Value, Previous = "---", Reference = "---", IsSection = false });
                rows.Add(new PhysicalExamRow { Item = $"{eye} 電腦驗光", Result = match.Groups[5].Value, Previous = "---", Reference = "---", IsSection = false });
                rows.Add(new PhysicalExamRow { Item = $"{eye} 散光", Result = match.Groups[6].Value, Previous = "---", Reference = "---", IsSection = false });

                string extra = match.Groups[7].Value;
                if (!string.IsNullOrWhiteSpace(extra) && !extra.StartsWith("診斷"))
                {
                    rows.Add(new PhysicalExamRow { Item = $"{eye} 辨色力", Result = extra, Previous = "---", Reference = "---", IsSection = false });
                }
            }

            return rows;
        }

        private static bool TryParseLabLine(string line, out PhysicalExamRow row)
        {
            row = new PhysicalExamRow();
            if (string.IsNullOrWhiteSpace(line)) return false;

            var cells = line.Split('\t').Select(x => x.Trim()).ToList();

            if (cells.All(string.IsNullOrWhiteSpace)) return false;
            if (cells.Count >= 4 && cells[0].Contains("檢查項目")) return false;

            cells = cells.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            if (cells.Count >= 4)
            {
                var item = cells[0];
                var result = cells[1];
                var previous = cells[2];
                var reference = cells[3];

                if (cells.Count >= 5 && LooksLikeItemPart(cells[1]) && LooksLikeResultValue(cells[2]))
                {
                    item = cells[0] + cells[1];
                    result = cells[2];
                    previous = cells[3];
                    reference = cells[4];
                }
                else if (LooksLikeItemPart(cells[1]) && LooksLikeResultValue(cells[2]))
                {
                    item = cells[0] + cells[1];
                    result = cells[2];
                    previous = "---";
                    reference = cells[3];
                }

                if (reference.StartsWith("---")) reference = reference.Substring(3).Trim();
                reference = reference.Replace(" --- ", " ").Trim();

                if (item.Contains("尿液顏色") && reference.Contains(" "))
                {
                    var refParts = reference.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (refParts.Length == 2 && refParts[0].Equals(refParts[1], StringComparison.OrdinalIgnoreCase))
                    {
                        result = result + " " + refParts[0];
                        reference = refParts[1];
                    }
                }

                var mixedResult = Regex.Match(item, @"^(?<item>.+\))\s+(?<result>Light|Dark|Yellow|Clear|Cloudy|NEGATIVE|Positive|Negative|\+|-|\d+(\.\d+)?(\-\d+(\.\d+)?)?)$", RegexOptions.IgnoreCase);
                if (mixedResult.Success)
                {
                    item = mixedResult.Groups["item"].Value.Trim();
                    result = mixedResult.Groups["result"].Value.Trim();
                }

                row = new PhysicalExamRow { Item = item, Result = result, Previous = previous, Reference = reference, IsSection = false };
                return true;
            }

            var m2 = Regex.Match(line, @"^(?<item>.+?)\s+(?<result>(?:<|>|≦|≧)?\s*[A-Za-z0-9\.\+\-/:\(\)]+)\s*---\s*(?<ref>.+)$", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                var item = CleanupText(m2.Groups["item"].Value).Replace(".(", "(");
                var result = CleanupText(m2.Groups["result"].Value);
                var reference = CleanupText(m2.Groups["ref"].Value);
                reference = Regex.Replace(reference, @"\s+(?=[\u4e00-\u9fffA-Za-z]+\(.+\)$)", " ");

                row = new PhysicalExamRow { Item = item, Result = result, Previous = "---", Reference = NormalizeReference(reference), IsSection = false };
                return true;
            }

            return false;
        }

        private static bool LooksLikeItemPart(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            return Regex.IsMatch(text, @"^\(?[A-Za-z0-9\-/ ]+\)?$");
        }

        private static bool LooksLikeResultValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            return Regex.IsMatch(text, @"^(<|>|≦|≧)?\s*\d+(\.\d+)?(\s*-\s*\d+(\.\d+)?)?$|^[A-Z]$|^Light$|^test\)?$|^[-+]$", RegexOptions.IgnoreCase);
        }

        private static int FindSectionStart(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            int best = -1;
            foreach (var s in LabSections)
            {
                int idx = text.IndexOf(s, StringComparison.Ordinal);
                if (idx >= 0 && (best == -1 || idx < best)) best = idx;
            }
            return best;
        }

        private static int FindTableStartIndex(string content)
        {
            var keys = new[] {
                "系統體格檢查表", "理學檢查", "Physical Examination", "Laboratory Examination",
                "眼科檢查", "靜態心電圖", "頸動脈超音波檢查", "甲狀腺超音波檢查",
                "檢查項目\t本次\t前次\t本次參考值", "檢查項目 本次 前次 本次參考值",
                "項目\t結果\t參考值", "項目 結果 參考值", "項目\t結果", "項目 結果",
                "眼別\t視力裸視", "眼別 視力裸視", "視力裸視", "矯正視力", "眼壓",
                "電腦驗光", "辨色力", "診斷(Diagnosis)", "診斷 (Diagnosis)"
            };

            int idx = -1;
            foreach (var key in keys)
            {
                int found = content.IndexOf(key, StringComparison.Ordinal);
                if (found >= 0 && (idx == -1 || found < idx)) idx = found;
            }
            return idx;
        }

        private static List<PhysicalExamRow> ParseLaboratoryTable(string tablePart)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(tablePart)) return rows;

            var rawLines = tablePart.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            foreach (var rawLine in rawLines)
            {
                var line = rawLine;
                if (line.Contains("if (") || line.Contains("continue;") || line.Contains("{") || line.Contains("}")) continue;
                if (line.Contains("檢查項目") && line.Contains("本次")) continue;

                if (IsRealSection(line))
                {
                    rows.Add(new PhysicalExamRow { Item = NormalizeSectionLine(line), Result = "", Previous = "", Reference = "", IsSection = true });
                    continue;
                }
                if (TryParseLabLine(line, out var row)) rows.Add(row);
            }

            bool hasDataRows = rows.Any(x => !x.IsSection);
            if (!hasDataRows)
            {
                rows.Clear();
                var text = NormalizeLabText(tablePart);
                foreach (var sec in LabSections) text = text.Replace(sec + " (", "\n" + sec + " (");

                var lines = text.Split('\n').Select(CleanupText).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (var line in lines)
                {
                    if (IsRealSection(line))
                    {
                        rows.Add(new PhysicalExamRow { Item = NormalizeSectionLine(line), Result = "", Previous = "", Reference = "", IsSection = true });
                        continue;
                    }
                    if (TryParseLabLine(line, out var row)) rows.Add(row);
                }
            }
            return rows;
        }

        private static string NormalizeLabText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = text.Replace("生化檢 查", "生化檢查").Replace("腎功能檢 查", "腎功能檢查").Replace("心臟血管功能檢 查", "心臟血管功能檢查").Replace("其它檢 查", "其它檢查").Replace("尿液檢 查", "尿液檢查").Replace("前 檢查項目 本次 本次參考值 次", "").Replace("檢查項目 本次 本次參考值 次", "").Replace("檢查項目 本次 前次 本次參考值", "").Replace("檢查項目 本次 前 次 本次參考值", "");
            text = text.Replace("紅血球.(RBC(B))", "紅血球(RBC(B))").Replace("Non- reactive", "Nonreactive").Replace("Alpha- Fetoprotein", "Alpha-Fetoprotein").Replace("EBV- ", "EBV-");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            text = Regex.Replace(text, @"\n?\s*---\s*\n?", " --- ");
            text = Regex.Replace(text, @"(血液檢查\s*\(.*?\)|生化檢查\s*\(.*?\)|肝功能檢查\s*\(.*?\)|腎功能檢查\s*\(.*?\)|血脂肪檢查\s*\(.*?\)|糖尿病檢查\s*\(.*?\)|痛風檢查\s*\(.*?\)|胰臟功能檢查\s*\(.*?\)|心臟血管功能檢查\s*\(.*?\)|甲狀腺檢查\s*\(.*?\)|肝炎標記\s*\(.*?\)|血液腫瘤標誌\s*\(.*?\)|其它檢查\s*\(.*?\)|尿液檢查\s*\(.*?\))", "\n$1\n");
            text = Regex.Replace(text, @"((?:<|>|≦|≧)?\s*[\d\.]+)\s*---\s*([^\n]+?)\s+([\u4e00-\u9fffA-Za-z\.\-\(\)/]+)\s*(10\^\d+/uL|10\^\d+/UL|/100WBC|/HPF|g/dL|mg/dL|pg|fL|IU/L|U/L|NG/DL|NG/ML|mIU/L|MG/DL|%)", "\n$1 --- $2 $3 $4\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"([\u4e00-\u9fffA-Za-z\.\-\(\)/ ]+?)\s+((?:<|>|≦|≧)?\s*[A-Za-z0-9\.\+\-/:\(\)]+)\s*---\s*([^\n]+?)(?=(?:\s+(?:[\u4e00-\u9fff][^\d].*?---)|\s+(?:血液檢查|生化檢查|肝功能檢查|腎功能檢查|血脂肪檢查|糖尿病檢查|痛風檢查|胰臟功能檢查|心臟血管功能檢查|甲狀腺檢查|肝炎標記|血液腫瘤標誌|其它檢查|尿液檢查)|$))", "\n$1 $2 --- $3\n", RegexOptions.IgnoreCase);
            text = text.Replace("鹼性磷酸酶(Alkaline 43 --- 34-104 IU/L Phosphatase)", "鹼性磷酸酶(Alkaline Phosphatase) 43 --- 34-104 IU/L").Replace("高密度脂蛋白膽固 59 --- 男>40；女>50 醇(HDL-C) mg/dL", "高密度脂蛋白膽固醇(HDL-C) 59 --- 男>40；女>50 mg/dL").Replace("尿膽素原 --- ≦1.5 ≦1.5 MG/DL (Urobilinogen)", "尿膽素原(Urobilinogen) ≦1.5 --- ≦1.5 MG/DL");
            return text;
        }

        private static void FlushBufferAsRows(List<string> buffer, List<PhysicalExamRow> rows)
        {
            if (buffer == null || buffer.Count == 0) return;
            var text = string.Join(" ", buffer);
            text = text.Replace("(Alkaline 43 --- 34-104 IU/L Phosphatase)", "(Alkaline Phosphatase) 43 --- 34-104 IU/L").Replace("高密度脂蛋白膽固 59 --- 男>40；女>50 醇(HDL-C) mg/dL", "高密度脂蛋白膽固醇(HDL-C) 59 --- 男>40；女>50 mg/dL").Replace("尿膽素原 --- ≦1.5 ≦1.5 MG/DL (Urobilinogen)", "尿膽素原(Urobilinogen) ≦1.5 --- ≦1.5 MG/DL");
            text = Regex.Replace(text, @"(?<result>(?:<|>|≦|≧)?\s*\d+(?:\.\d+)?)\s*---\s*(?<ref>\S+)\s+(?<item>[\u4e00-\u9fffA-Za-z\.\-\(\)/]+)\s*(?<unit>10\^\d+/uL|10\^\d+/UL|/100WBC|/HPF|g/dL|mg/dL|pg|fL|IU/L|U/L|NG/DL|NG/ML|mIU/L|MG/DL|%)", "\n${result} --- ${ref} ${item} ${unit}\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<item>[\u4e00-\u9fffA-Za-z\.\-\(\)/ ]+?)\s+(?<result>(?:<|>|≦|≧)?\s*[\dA-Za-z\.\+\-/:\(\)]+)\s*---\s*(?<ref>[^。\n]+?)(?=(?:[\u4e00-\u9fffA-Za-z].*?---)|$)", "\n${item} ${result} --- ${ref}\n", RegexOptions.IgnoreCase);
            var candidates = text.Split('\n').Select(CleanupText).Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains("---")).ToList();
            foreach (var c in candidates) if (TryParseLabCandidate(c, out var row)) rows.Add(row);
        }

        private static bool TryParseLabCandidate(string text, out PhysicalExamRow row)
        {
            row = new PhysicalExamRow();
            if (string.IsNullOrWhiteSpace(text) || !text.Contains("---")) return false;
            text = CleanupText(text);

            var m1 = Regex.Match(text, @"^(?<result>(?:<|>|≦|≧)?\s*[\dA-Za-z\.\+\-/:\(\)]+)\s*---\s*(?<reference>.+?)\s+(?<item>[\u4e00-\u9fffA-Za-z\.\-\(\)/]+)\s*(?<unit>10\^\d+/uL|10\^\d+/UL|/100WBC|/HPF|g/dL|mg/dL|pg|fL|IU/L|U/L|NG/DL|NG/ML|mIU/L|MG/DL|%)$", RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                var result = CleanupText(m1.Groups["result"].Value);
                var reference = CleanupText(m1.Groups["reference"].Value);
                var item = CleanupText(m1.Groups["item"].Value);
                var unit = CleanupText(m1.Groups["unit"].Value);
                if (!reference.EndsWith(unit, StringComparison.OrdinalIgnoreCase)) reference = $"{reference} {unit}".Trim();

                row = new PhysicalExamRow { Item = item.Replace(".(", "("), Result = result, Previous = "---", Reference = NormalizeReference(reference), IsSection = false };
                return true;
            }

            var m2 = Regex.Match(text, @"^(?<item>.+?)\s+(?<result>(?:<|>|≦|≧)?\s*[\dA-Za-z\.\+\-/:\(\)]+)\s*---\s*(?<reference>.+)$", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                row = new PhysicalExamRow { Item = CleanupText(m2.Groups["item"].Value).Replace(".(", "("), Result = CleanupText(m2.Groups["result"].Value), Previous = "---", Reference = NormalizeReference(CleanupText(m2.Groups["reference"].Value)), IsSection = false };
                return true;
            }
            return false;
        }

        private static string NormalizeReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "";
            reference = CleanupText(reference);
            reference = reference.Replace("MG/DL", "mg/dL").Replace("NG/DL", "ng/dL").Replace("NG/ML", "ng/mL").Replace("NOT FOUND /HPF", "NOT FOUND/HPF").Replace("NOT FOUND / HPF", "NOT FOUND/HPF").Replace("Non-reactive", "Nonreactive").Replace("Non reactive", "Nonreactive").Replace("Reactive (", "Reactive(").Replace(" - ", "-").Replace(" / ", "/");
            int secIdx = FindSectionStart(reference);
            if (secIdx > 0) reference = CleanupText(reference.Substring(0, secIdx));
            return Regex.Replace(reference, @"\s+", " ").Trim();
        }

        private static string NormalizeSectionLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "";
            line = CleanupText(line);
            line = line.Replace("血液檢查 ( Blood Cell Exam )", "血液檢查 (Blood Cell Exam)").Replace("生化檢查 ( Biochemistry Exam )", "生化檢查 (Biochemistry Exam)").Replace("肝功能檢查 ( Liver Function )", "肝功能檢查 (Liver Function)").Replace("腎功能檢查 ( Renal Function )", "腎功能檢查 (Renal Function)").Replace("血脂肪檢查 ( Lipid )", "血脂肪檢查 (Lipid)").Replace("糖尿病檢查 ( Plasma Glucose )", "糖尿病檢查 (Plasma Glucose)").Replace("痛風檢查 ( Gout )", "痛風檢查 (Gout)").Replace("胰臟功能檢查 ( Pancrease Function )", "胰臟功能檢查 (Pancrease Function)").Replace("胰臟功能檢查 ( Pancreas Function )", "胰臟功能檢查 (Pancreas Function)").Replace("心臟血管功能檢查 ( Cardiovascular Enzym Exam )", "心臟血管功能檢查 (Cardiovascular Enzym Exam)").Replace("甲狀腺檢查 ( Thyroid Function )", "甲狀腺檢查 (Thyroid Function)").Replace("肝炎標記 ( Hepatitis )", "肝炎標記 (Hepatitis)").Replace("血液腫瘤標誌 ( Tumor Markers )", "血液腫瘤標誌 (Tumor Markers)").Replace("其它檢查 ( Others )", "其它檢查 (Others)").Replace("尿液檢查 ( Urine Routine )", "尿液檢查 (Urine Routine)");
            return line;
        }

        private static bool IsRealSection(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = CleanupText(text);
            return LabSections.Any(s => text.StartsWith(s, StringComparison.Ordinal));
        }

        private static string CleanupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var t = text.Replace('\u00A0', ' ').Replace("（", "(").Replace("）", ")").Replace(" ", "").Replace("．", ".");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t;
        }

        private static bool IsBodyCheckFormat(string text)
        {
            return text.Contains("Body height") || text.Contains("Body weight") || text.Contains("Pulse rate") || text.Contains("Blood pressure") || text.Contains("Abdominal girth") || text.Contains("BMI");
        }

        private static string ExtractEyeDiagnosisText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var match = Regex.Match(text, @"診\s*斷.*?(?=建議|$)", RegexOptions.Singleline);
            if (!match.Success) return "";

            var diagnosisText = match.Value.Trim();
            diagnosisText = Regex.Replace(diagnosisText, @"\n{2,}", "\n");
            diagnosisText = Regex.Replace(diagnosisText, @"abnormal-text'>", "", RegexOptions.IgnoreCase);
            diagnosisText = Regex.Replace(diagnosisText, @"<[^>]+>", "");
            diagnosisText = diagnosisText.Replace("\t", " ").Replace("\u00A0", "").Replace("\u3000", "");
            diagnosisText = Regex.Replace(diagnosisText, @"^\s+", "");
            diagnosisText = diagnosisText.Trim();
            diagnosisText = diagnosisText.Replace("視力(Visual\nacuity)：", "視力(Visual acuity)：").Replace("眼壓(Intraocular\npressure)：", "眼壓(Intraocular pressure)：").Replace("正常範圍 (Within normal limits)", "正常範圍 (Within normal limits)");
            diagnosisText = Regex.Replace(diagnosisText, @"視力\(Visual\s+acuity\)：\s*\n?\s*", "視力(Visual acuity)：");
            diagnosisText = Regex.Replace(diagnosisText, @"眼壓\(Intraocular\s+pressure\)：\s*\n?\s*", "眼壓(Intraocular pressure)：");

            var lines = diagnosisText.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var result = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("診斷")) result.Add(line);
                else if (line.StartsWith("視力(") || line.StartsWith("眼壓(") || line.StartsWith("眼瞼(") || line.StartsWith("結膜(") || line.StartsWith("角膜(") || line.StartsWith("瞳孔(") || line.StartsWith("晶狀體(") || line.StartsWith("眼球肌(") || line.StartsWith("眼底(") || line.StartsWith("視網膜病變(") || line.StartsWith("玻璃體(") || line.StartsWith("淚腺(") || line.StartsWith("黃斑(") || line.StartsWith("虹膜(") || line.StartsWith("其他(")) result.Add(line);
                else if (result.Count > 0) result[result.Count - 1] += line;
            }
            return string.Join("\n", result);
        }

        private static string Format(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未抓到" : value;
        }
    }
}