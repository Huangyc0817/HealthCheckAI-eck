using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace HealthCheckAI.Controllers
{
    public class TTSController : Controller
    {
        // 替換成你在 Azure 申請的免費金鑰與區域
        private readonly string subscriptionKey = "3ITwPCMW2MOg63OBMUL6jPN7VvoSG9pW8nketjqztG3rhc1QtbaWJQQJ99CGACi0881XJ3w3AAAYACOGHRmE";
        private readonly string serviceRegion = "japaneast"; 

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Speak(string text = "您好，這是您的AI健康檢查報告。")
        {
            if (string.IsNullOrEmpty(text)) return BadRequest("請提供文字");

            // 1. 設定 Azure 語音設定
            var speechConfig = SpeechConfig.FromSubscription(subscriptionKey, serviceRegion);

            // 🔥 關鍵：指定「台灣神經網路女聲 (曉臻)」- 聲音非常溫柔專業，很適合醫療情境！
            speechConfig.SpeechSynthesisVoiceName = "zh-TW-HsiaoChenNeural";

            // 2. 設定輸出為記憶體串流 (不用再存成實體 temp 檔案了，效能更好)
            using var audioOutputStream = AudioOutputStream.CreatePullStream();
            using var audioConfig = AudioConfig.FromStreamOutput(audioOutputStream);

            using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);

            // 3. 執行 AI 語音合成
            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                // 4. 將合成好的音訊直接轉成 Byte Array 回傳
                byte[] audioBytes = result.AudioData;
                return File(audioBytes, "audio/wav");
            }
            else
            {
                // 處理失敗狀況（例如金鑰沒填或網路斷線）
                return StatusCode(500, $"語音合成失敗: {result.Reason}");
            }
        }
    }
}