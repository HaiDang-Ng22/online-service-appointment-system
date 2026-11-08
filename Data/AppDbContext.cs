using Appointment.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;
        public DbSet<WorkingHour> WorkingHours { get; set; } = null!;
        public DbSet<AppointmentModel> Appointments { get; set; } = null!;
        public DbSet<ProviderOffDay> ProviderOffDays { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Email Unique Index
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Service Relationships
            modelBuilder.Entity<Service>()
                .HasOne(s => s.Provider)
                .WithMany(u => u.ProvidedServices)
                .HasForeignKey(s => s.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Category)
                .WithMany(c => c.Services)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Appointment Relationships
            modelBuilder.Entity<AppointmentModel>()
                .HasOne(a => a.Customer)
                .WithMany(u => u.CustomerAppointments)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppointmentModel>()
                .HasOne(a => a.Provider)
                .WithMany(u => u.ProviderAppointments)
                .HasForeignKey(a => a.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppointmentModel>()
                .HasOne(a => a.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProviderOffDay Relationships
            modelBuilder.Entity<ProviderOffDay>()
                .HasOne(p => p.Provider)
                .WithMany(u => u.OffDays)
                .HasForeignKey(p => p.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            // US-26: Password reset tokens
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.TokenHash);

            // US-30: Reviews (mỗi lịch hẹn chỉ có 1 đánh giá)
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.AppointmentId)
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Appointment)
                .WithMany()
                .HasForeignKey(r => r.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Service)
                .WithMany()
                .HasForeignKey(r => r.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // US-28: Payments
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Appointment)
                .WithMany()
                .HasForeignKey(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Customer)
                .WithMany()
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.AppointmentId);
        }
    }
}
