namespace EnterpriseDataManager.Models
{
    using Microsoft.AspNetCore.Identity;

    public class Users : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }
}
