namespace EnterpriseDataManager.Data
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

    public class EnterpriseDataManagerDbContext : IdentityDbContext
    {
        public EnterpriseDataManagerDbContext(DbContextOptions<EnterpriseDataManagerDbContext> options)
            : base(options) { }
    }
}
