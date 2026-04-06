namespace HealthCheckAI.Helpers
{
    public static class ReportParserFactory
    {
        public static object Parse(string category, string text)
        {
            var type = ReportClassifier.FromCategory(category);
            return Parse(type, text);
        }

        public static object Parse(ReportType type, string text)
        {
            return type switch
            {
                ReportType.PhysicalExam => PhysicalExamParser.Parse(text),
                ReportType.SimplePhysical => SimplePhysicalParser.Parse(text),

                // 這三個你之後再補專用 parser
                ReportType.Laboratory => TextBlockParser.SplitToBlocks(text),
                ReportType.Eye => TextBlockParser.SplitToBlocks(text),
                ReportType.ECG => TextBlockParser.SplitToBlocks(text),
                ReportType.Ultrasound => TextBlockParser.SplitToBlocks(text),

                _ => TextBlockParser.SplitToBlocks(text)
            };
        }
    }
}