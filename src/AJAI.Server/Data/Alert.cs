namespace AJAI_Server.Data
{
    public class Alert
    {
        public int Id { get; set; }
        public string CameraEmail { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Base64Image { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
