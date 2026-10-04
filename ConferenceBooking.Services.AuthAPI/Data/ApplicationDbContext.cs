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
        public DbSet<UserProfile> Tbl_UserProfile { get; set; }
        public DbSet<RegistrationRequest> Tbl_RegistrationRequest { get; set; }
        public DbSet<Department> Tbl_Department { get; set; }
        public DbSet<UserLoginSessions> Tbl_UserLoginSessions { get; set; }
        public DbSet<PasswordReset> Tbl_PasswordReset { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Department>()
                .ToTable("Tbl_Department");

            modelBuilder.Entity<UserProfile>()
                .ToTable("Tbl_UserProfile");

            modelBuilder.Entity<RegistrationRequest>()
                .ToTable("Tbl_RegistrationRequest");

            modelBuilder.Entity<UserLoginSessions>()
                .ToTable("Tbl_UserLoginSessions");

            modelBuilder.Entity<PasswordReset>()
                .ToTable("Tbl_PasswordReset");
        }

    }
}
