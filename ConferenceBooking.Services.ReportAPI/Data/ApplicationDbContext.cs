using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.ReportAPI.Data
{
    /// <summary>
    /// BookingAPI data context.
    ///
    /// This context maps BookingAPI-owned tables and maps the existing shared
    /// AuthAPI tables without including them in BookingAPI migrations.
    /// It deliberately does not inherit from IdentityDbContext and does not
    /// create or manage ASP.NET Core Identity tables.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
    }
}
