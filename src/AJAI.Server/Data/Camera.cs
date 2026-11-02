namespace AJAI_Server.Data
{
    public class Camera
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime LastPing { get; set; }
    }
}
