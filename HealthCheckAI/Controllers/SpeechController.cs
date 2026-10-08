using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;

namespace HealthCheckAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpeechController : ControllerBase
    {
        [HttpPost("Synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] SpeechRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("請提供要轉換的文字。");
            }

            string speechKey = "3ITwPCMW2MOg63OBMUL6jPN7VvoSG9pW8nketjqztG3rhc1QtbaWJQQJ99CGACi0881XJ3w3AAAYACOGHRmE";
            string speechRegion = "japaneast"; 

            var config = SpeechConfig.FromSubscription(speechKey, speechRegion);

            // 💡 設定為台灣的自然語音 (HsiaoChen 是女生，YunJhe 是男生)
            config.SpeechSynthesisVoiceName = "zh-TW-HsiaoChenNeural";

            using var synthesizer = new SpeechSynthesizer(config, null);
            var result = await synthesizer.SpeakTextAsync(request.Text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                // 回傳產生的音檔資料
                return File(result.AudioData, "audio/mpeg");
            }
            else
            {
                return StatusCode(500, "Azure 語音合成失敗");
            }
        }
    }

    public class SpeechRequest
    {
        public string Text { get; set; }
    }
}