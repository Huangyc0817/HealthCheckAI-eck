namespace HealthCheckAI.Models
{
    public class EcgParameterResult
    {
        public string RawText { get; set; } = "";

        public string HeartRate { get; set; } = "";
        public string PRInterval { get; set; } = "";
        public string QRSDuration { get; set; } = "";
        public string QT_QTc { get; set; } = "";
        public string Axes { get; set; } = "";
        public string MachineInterpretation { get; set; } = "";

        public string Summary { get; set; } = "";
        public string Suggestion { get; set; } = "";
    }
}