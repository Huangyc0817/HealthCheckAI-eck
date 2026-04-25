using Tesseract;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HealthCheckAI.Services
{
    public class OcrService
    {
        private readonly IWebHostEnvironment _env;

        public OcrService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public string ExtractTextFromImage(string imagePath)
        {
            var tessPath = Path.Combine(_env.WebRootPath, "tessdata");

            // 先裁上方文字區，避免心電圖波形干擾
            var croppedPath = CropTopArea(imagePath);

            using var engine = new TesseractEngine(tessPath, "eng", EngineMode.LstmOnly);
            using var img = Pix.LoadFromFile(croppedPath);
            using var page = engine.Process(img);

            return page.GetText();
        }

        private string CropTopArea(string imagePath)
        {
            var outputPath = Path.Combine(
                Path.GetDirectoryName(imagePath)!,
                Path.GetFileNameWithoutExtension(imagePath) + "_ecg_top.png"
            );

            using var image = Image.Load(imagePath);

            // 原始尺寸先記下來
            int originalWidth = image.Width;
            int originalHeight = image.Height;

            image.Mutate(x =>
            {
                // 放大 4 倍
                x.Resize(originalWidth * 4, originalHeight * 4);

                // Resize 後重新用 image.Width / image.Height
                int cropWidth = image.Width;
                int cropHeight = image.Height / 4;

                x.Crop(new Rectangle(0, 0, cropWidth, cropHeight));

                x.Grayscale();
                x.Contrast(1f);     // 原本2 → 拉高
                x.Brightness(1.2f); // 新增
            });

            image.Save(outputPath);

            return outputPath;
        }
    }
}