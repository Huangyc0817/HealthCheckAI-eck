using System.ComponentModel.DataAnnotations;

namespace HealthCheckAI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }


        [Required(ErrorMessage = "請輸入帳號")]
       
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "請選擇角色")]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入姓名")]

        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
