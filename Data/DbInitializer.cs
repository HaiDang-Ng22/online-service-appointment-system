using Appointment.Helpers;
using Appointment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Appointment.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            EnsureDatabaseSchemaCreated(context);
            SeedData(context);
        }

        public static void EnsureDatabaseSchemaCreated(AppDbContext context)
        {
            try
            {
                var dbCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                if (dbCreator != null)
                {
                    if (!dbCreator.Exists())
                    {
                        dbCreator.Create();
                    }
                    if (!dbCreator.HasTables())
                    {
                        dbCreator.CreateTables();
                    }
                }
                else
                {
                    context.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi kiểm tra/tạo bảng cơ sở dữ liệu: {ex.Message}");
                try { context.Database.EnsureCreated(); } catch { }
            }
        }

        public static void SeedData(AppDbContext context)
        {
            // Seed Admin if no user exists or if admin doesn't exist
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                var admin = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = "Quản trị viên Hệ thống",
                    Email = "admin@appointx.com",
                    PasswordHash = PasswordHasher.HashPassword("Admin@123456"),
                    PhoneNumber = "0900000000",
                    Address = "Hà Nội, Việt Nam",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(admin);
                context.SaveChanges();
            }

            // Seed Categories if empty (NO EMOJIS OR ICONS)
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Bác sĩ / Phòng khám", Description = "Dịch vụ y tế, khám sức khỏe, tư vấn bác sĩ" },
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Salon / Spa", Description = "Chăm sóc tóc, thẩm mỹ, làm đẹp, massage" },
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Tư vấn viên", Description = "Tư vấn tài chính, pháp lý, tâm lý, hướng nghiệp" },
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Trung tâm thể thao", Description = "Huấn luyện viên cá nhân, Gym, Yoga" },
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Trung tâm giáo dục", Description = "Gia sư, luyện thi, khóa học ngắn hạn" },
                    new Category { Id = Guid.NewGuid().ToString("N"), Name = "Trung tâm dịch vụ", Description = "Sửa chữa thiết bị, bảo dưỡng xe, dịch vụ tận nơi" }
                };
                context.Categories.AddRange(categories);
                context.SaveChanges();
            }

            // Seed Sample Provider & Services if empty
            if (!context.Users.Any(u => u.Role == "Provider"))
            {
                var doctorProvider = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = "Phòng Khám Đa Khoa AppointCare",
                    Email = "doctor@appointx.com",
                    PasswordHash = PasswordHasher.HashPassword("Provider@123456"),
                    PhoneNumber = "0912345678",
                    Address = "123 Nguyễn Trãi, Thanh Xuân, Hà Nội",
                    Role = "Provider",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var spaProvider = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = "Beauty & Spa Sakura",
                    Email = "spa@appointx.com",
                    PasswordHash = PasswordHasher.HashPassword("Provider@123456"),
                    PhoneNumber = "0987654321",
                    Address = "45 Lê Lợi, Quận 1, TP. Hồ Chí Minh",
                    Role = "Provider",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.AddRange(doctorProvider, spaProvider);
                context.SaveChanges();

                var healthCat = context.Categories.FirstOrDefault(c => c.Name.Contains("Bác sĩ"));
                var spaCat = context.Categories.FirstOrDefault(c => c.Name.Contains("Salon"));

                if (healthCat != null)
                {
                    context.Services.Add(new Service
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ProviderId = doctorProvider.Id,
                        CategoryId = healthCat.Id,
                        Name = "Khám Sức Khỏe Tổng Quát",
                        Description = "Khám lâm sàng, xét nghiệm tổng quát và tư vấn chế độ dinh dưỡng, sinh hoạt.",
                        Price = 500000,
                        DurationMinutes = 60,
                        Location = doctorProvider.Address,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (spaCat != null)
                {
                    context.Services.Add(new Service
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ProviderId = spaProvider.Id,
                        CategoryId = spaCat.Id,
                        Name = "Chăm Sóc Da Mặt Chuyên Sâu",
                        Description = "Liệu trình tẩy tế bào chết, massage nâng cơ, đắp mặt nạ dưỡng chất sinh học.",
                        Price = 350000,
                        DurationMinutes = 45,
                        Location = spaProvider.Address,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Working hours for providers
                foreach (var day in Enum.GetValues<DayOfWeek>())
                {
                    if (day != DayOfWeek.Sunday)
                    {
                        context.WorkingHours.Add(new WorkingHour
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ProviderId = doctorProvider.Id,
                            DayOfWeek = day,
                            StartTime = new TimeSpan(8, 0, 0),
                            EndTime = new TimeSpan(17, 0, 0),
                            IsAvailable = true
                        });

                        context.WorkingHours.Add(new WorkingHour
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ProviderId = spaProvider.Id,
                            DayOfWeek = day,
                            StartTime = new TimeSpan(9, 0, 0),
                            EndTime = new TimeSpan(18, 0, 0),
                            IsAvailable = true
                        });
                    }
                }

                context.SaveChanges();
            }

            // Seed Staff if not exist
            if (!context.Users.Any(u => u.Role == "Staff"))
            {
                var staff = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = "Nhân Viên Hỗ Trợ - Nguyễn Văn A",
                    Email = "staff@appointx.com",
                    PasswordHash = PasswordHasher.HashPassword("Staff@123456"),
                    PhoneNumber = "0933333333",
                    Address = "Hà Nội, Việt Nam",
                    Role = "Staff",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(staff);
                context.SaveChanges();
            }

            // Seed Demo Customer if not exist
            if (!context.Users.Any(u => u.Role == "Customer"))
            {
                var customer = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FullName = "Khách Hàng Trải Nghiệm",
                    Email = "customer@appointx.com",
                    PasswordHash = PasswordHasher.HashPassword("Customer@123456"),
                    PhoneNumber = "0944444444",
                    Address = "Đà Nẵng, Việt Nam",
                    Role = "Customer",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(customer);
                context.SaveChanges();
            }
        }
    }
}
