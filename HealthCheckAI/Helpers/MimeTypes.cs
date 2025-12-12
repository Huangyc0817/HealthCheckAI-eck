using System;
using System.Collections.Generic;
using System.IO;

namespace HealthCheckAI.Helpers
{
    public static class MimeTypes
    {
        private static readonly Dictionary<string, string> _map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { ".txt",  "text/plain" },
                { ".pdf",  "application/pdf" },
                { ".doc",  "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls",  "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".csv",  "text/csv" },
                { ".png",  "image/png" },
                { ".jpg",  "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".gif",  "image/gif" },
                { ".zip",  "application/zip" },
            };

        public static string GetMimeType(string pathOrFileName)
        {
            var ext = Path.GetExtension(pathOrFileName);
            if (!string.IsNullOrEmpty(ext) && _map.TryGetValue(ext, out var ct))
                return ct;

            return "application/octet-stream";
        }
    }
}
