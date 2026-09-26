using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using ConferenceBooking.Services.AuthAPI.Models;

namespace ConferenceBooking.Services.AuthAPI.Data
{
    public class ApplicationDbContext:IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options):base(options)
        {
                
        }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<UserProfileRegistration> Tbl_User_Profile { get; set; }
        public DbSet<UserLoginSessions> Tbl_User_LoginSessions { get; set; }
    }
}
