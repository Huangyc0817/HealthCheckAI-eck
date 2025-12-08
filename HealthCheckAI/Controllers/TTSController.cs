using Microsoft.AspNetCore.Mvc;
using System.Speech.Synthesis;
using System.IO;

namespace HealthCheckAI.Controllers
{
    public class TTSController : Controller
    {
        // ✅ 顯示有按鈕的播放頁面
        public IActionResult Index()
        {
            return View();
        }

        // ✅ 將文字轉語音，回傳給前端播放
        [HttpGet]
        public IActionResult Speak(string text = "您好，這是AI健康檢查報告。")
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.wav");

            using (SpeechSynthesizer synth = new SpeechSynthesizer())
            {
                synth.Volume = 100;
                synth.Rate = 0;

                var voices = synth.GetInstalledVoices();
                if (voices.Count > 0)
                {
                    synth.SelectVoice(voices[0].VoiceInfo.Name);
                }

                // ✅ 先設定輸出檔
                synth.SetOutputToWaveFile(tempFile);

                // ✅ 執行語音
                synth.Speak(text);

                // ✅ 關閉輸出通道（釋放檔案鎖定）
                synth.SetOutputToNull();
            }

            // ✅ 現在安全地讀取音訊檔案
            byte[] audioBytes = System.IO.File.ReadAllBytes(tempFile);

            // ✅ 回傳給瀏覽器
            return File(audioBytes, "audio/wav");
        }
    }
}