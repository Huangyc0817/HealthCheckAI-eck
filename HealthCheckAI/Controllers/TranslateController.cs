using HealthCheckAI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthCheckAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslateController : ControllerBase
    {
        private readonly TranslationService _translator;

        public TranslateController(TranslationService translator)
        {
            _translator = translator;
        }

        public class TranslateRequest
        {
            public string Text { get; set; } = "";
            public string Source { get; set; } = "auto";
            public string Target { get; set; } = "en";
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] TranslateRequest req)
        {
            var translated = await _translator.TranslateAsync(req.Text, req.Source, req.Target);
            return Ok(new { translatedText = translated });
        }
    }
}
