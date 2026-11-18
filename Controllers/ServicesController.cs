using Appointment.Data;
using Appointment.Infrastructure;
using Appointment.Models;
using Appointment.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Controllers
{
    public class ServicesController : Controller
    {
        private readonly AppDbContext _context;

        public ServicesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Services
        public async Task<IActionResult> Index(
            string? search,
            string? providerId,
            string? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? location,
            int? minRating,
            double? maxDistance,
            string? sortBy,
            double? userLat,
            double? userLng)
        {
            var query = _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.IsActive);

            // 1. Tìm kiếm theo từ khóa (tên dịch vụ, mô tả, tên nhà cung cấp, phòng khám, cơ sở, địa điểm)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term)
                                      || (s.Description != null && s.Description.ToLower().Contains(term))
                                      || (s.Provider != null && s.Provider.FullName.ToLower().Contains(term))
                                      || (s.Provider != null && s.Provider.Address != null && s.Provider.Address.ToLower().Contains(term))
                                      || (s.Location != null && s.Location.ToLower().Contains(term)));
            }

            // 2. Lọc theo nhà cung cấp cụ thể
            if (!string.IsNullOrWhiteSpace(providerId))
            {
                query = query.Where(s => s.ProviderId == providerId);
            }

            // 3. Lọc theo danh mục
            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                query = query.Where(s => s.CategoryId == categoryId);
            }

            // 4. Lọc theo khoảng giá
            if (minPrice.HasValue)
            {
                query = query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(s => s.Price <= maxPrice.Value);
            }

            // 5. Lọc theo địa điểm
            if (!string.IsNullOrWhiteSpace(location))
            {
                var loc = location.Trim().ToLower();
                query = query.Where(s => (s.Location != null && s.Location.ToLower().Contains(loc))
                                      || (s.Provider != null && s.Provider.Address != null && s.Provider.Address.ToLower().Contains(loc)));
            }

            var services = await query.ToListAsync();
            // Đảm bảo kết quả hiển thị chính xác và không bị trùng lặp
            services = services.GroupBy(s => s.Id).Select(g => g.First()).ToList();

            // Tính điểm đánh giá của từng dịch vụ
            var serviceIds = services.Select(s => s.Id).ToList();
            var ratingRows = await _context.Reviews.AsNoTracking()
                .Where(r => serviceIds.Contains(r.ServiceId))
                .Select(r => new { r.ServiceId, r.Rating })
                .ToListAsync();

            var ratingsMap = ratingRows
                .GroupBy(r => r.ServiceId)
                .ToDictionary(g => g.Key, g => new RatingInfo { Average = g.Average(x => x.Rating), Count = g.Count() });
            ViewBag.Ratings = ratingsMap;

            // 6. Lọc theo đánh giá tối thiểu của khách hàng
            if (minRating.HasValue && minRating.Value > 0)
            {
                services = services.Where(s => ratingsMap.TryGetValue(s.Id, out var r) && r.Average >= minRating.Value).ToList();
            }

            // 7. Tính khoảng cách ước tính và lọc theo khoảng cách
            var distanceMap = new Dictionary<string, double>();
            foreach (var s in services)
            {
                distanceMap[s.Id] = EstimateDistanceKm(s.Location, s.Provider?.Address, userLat, userLng, location);
            }
            ViewBag.Distances = distanceMap;

            if (maxDistance.HasValue && maxDistance.Value > 0)
            {
                services = services.Where(s => distanceMap.TryGetValue(s.Id, out var d) && d <= maxDistance.Value).ToList();
            }

            // 8. Sắp xếp kết quả tìm kiếm
            switch (sortBy?.ToLower())
            {
                case "price_asc":
                    services = services.OrderBy(s => s.Price).ToList();
                    break;
                case "price_desc":
                    services = services.OrderByDescending(s => s.Price).ToList();
                    break;
                case "rating_desc":
                    services = services.OrderByDescending(s => ratingsMap.TryGetValue(s.Id, out var r) ? r.Average : 0)
                                       .ThenByDescending(s => ratingsMap.TryGetValue(s.Id, out var r) ? r.Count : 0)
                                       .ToList();
                    break;
                case "name_asc":
                    services = services.OrderBy(s => s.Name).ToList();
                    break;
                case "distance":
                    services = services.OrderBy(s => distanceMap.TryGetValue(s.Id, out var d) ? d : double.MaxValue).ToList();
                    break;
                default:
                    services = services.OrderByDescending(s => s.CreatedAt).ToList();
                    break;
            }

            // Danh sách toàn bộ nhà cung cấp để lọc dropdown
            var allProviders = await _context.Users
                .Where(u => u.Role == "Provider" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();
            ViewBag.Providers = allProviders;

            // Thông tin nhà cung cấp đang được chọn (nếu có)
            ProviderSummaryViewModel? selectedProviderSummary = null;
            if (!string.IsNullOrEmpty(providerId))
            {
                var prov = allProviders.FirstOrDefault(p => p.Id == providerId) ?? await _context.Users.FindAsync(providerId);
                if (prov != null)
                {
                    var provServiceIds = await _context.Services.Where(s => s.ProviderId == prov.Id && s.IsActive).Select(s => s.Id).ToListAsync();
                    var provReviews = await _context.Reviews.Where(r => provServiceIds.Contains(r.ServiceId)).Select(r => r.Rating).ToListAsync();
                    var provCategories = await _context.Services.Where(s => s.ProviderId == prov.Id && s.IsActive && s.Category != null)
                        .Select(s => s.Category!.Name).Distinct().ToListAsync();

                    selectedProviderSummary = new ProviderSummaryViewModel
                    {
                        Id = prov.Id,
                        FullName = prov.FullName,
                        Address = prov.Address,
                        PhoneNumber = prov.PhoneNumber,
                        Email = prov.Email,
                        AvatarUrl = prov.AvatarUrl,
                        ServiceCount = provServiceIds.Count,
                        ReviewCount = provReviews.Count,
                        RatingAverage = provReviews.Any() ? provReviews.Average() : 0,
                        CategoryNames = provCategories
                    };
                }
            }
            ViewBag.SelectedProvider = selectedProviderSummary;

            // Danh sách nhà cung cấp khớp với từ khóa tìm kiếm (để gợi ý thẻ nhà cung cấp)
            var matchedProviders = new List<ProviderSummaryViewModel>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                var matchedUsers = allProviders
                    .Where(p => p.FullName.ToLower().Contains(searchLower)
                             || (p.Address != null && p.Address.ToLower().Contains(searchLower)))
                    .ToList();

                foreach (var p in matchedUsers)
                {
                    var pServiceIds = await _context.Services.Where(s => s.ProviderId == p.Id && s.IsActive).Select(s => s.Id).ToListAsync();
                    var pReviews = await _context.Reviews.Where(r => pServiceIds.Contains(r.ServiceId)).Select(r => r.Rating).ToListAsync();
                    matchedProviders.Add(new ProviderSummaryViewModel
                    {
                        Id = p.Id,
                        FullName = p.FullName,
                        Address = p.Address,
                        PhoneNumber = p.PhoneNumber,
                        Email = p.Email,
                        AvatarUrl = p.AvatarUrl,
                        ServiceCount = pServiceIds.Count,
                        ReviewCount = pReviews.Count,
                        RatingAverage = pReviews.Any() ? pReviews.Average() : 0
                    });
                }
            }
            ViewBag.MatchedProviders = matchedProviders;

            // US-27: Top 5 dịch vụ được đặt nhiều nhất trong 30 ngày qua
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var topBookedServiceIds = await _context.Appointments
                .Where(a => a.CreatedAt >= thirtyDaysAgo && a.Status != AppointmentRules.Cancelled)
                .GroupBy(a => a.ServiceId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToListAsync();

            if (topBookedServiceIds.Count < 5)
            {
                var additionalIds = await _context.Appointments
                    .Where(a => a.Status != AppointmentRules.Cancelled && !topBookedServiceIds.Contains(a.ServiceId))
                    .GroupBy(a => a.ServiceId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .Take(5 - topBookedServiceIds.Count)
                    .ToListAsync();
                topBookedServiceIds.AddRange(additionalIds);
            }
            ViewBag.TopBookedServiceIds = new HashSet<string>(topBookedServiceIds);

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SearchTerm = search;
            ViewBag.SelectedProviderId = providerId;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Location = location;
            ViewBag.MinRating = minRating;
            ViewBag.MaxDistance = maxDistance;
            ViewBag.SortBy = sortBy;
            ViewBag.UserLat = userLat;
            ViewBag.UserLng = userLng;

            return View(services);
        }

        // GET: /Services/Provider/abc
        [HttpGet]
        public IActionResult Provider(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            return RedirectToAction(nameof(Index), new { providerId = id });
        }

        private static double EstimateDistanceKm(string? serviceLocation, string? providerAddress, double? userLat, double? userLng, string? searchLoc)
        {
            var combinedLoc = $"{serviceLocation} {providerAddress}".ToLower();

            var cityCoords = new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
            {
                { "thanh xuân", (20.9937, 105.8048) },
                { "cầu giấy", (21.0362, 105.7906) },
                { "đống đa", (21.0181, 105.8277) },
                { "hoàn kiếm", (21.0285, 105.8542) },
                { "ba đình", (21.0341, 105.8235) },
                { "hai bà trưng", (21.0069, 105.8524) },
                { "hà nội", (21.0285, 105.8542) },
                { "quận 1", (10.7756, 106.7004) },
                { "quận 3", (10.7844, 106.6843) },
                { "quận 5", (10.7556, 106.6667) },
                { "quận 7", (10.7340, 106.7218) },
                { "quận 10", (10.7719, 106.6677) },
                { "bình thạnh", (10.8106, 106.7091) },
                { "thủ đức", (10.8494, 106.7537) },
                { "hồ chí minh", (10.7769, 106.7009) },
                { "tp.hcm", (10.7769, 106.7009) },
                { "tphcm", (10.7769, 106.7009) },
                { "đà nẵng", (16.0544, 108.2022) }
            };

            if (userLat.HasValue && userLng.HasValue)
            {
                foreach (var entry in cityCoords)
                {
                    if (combinedLoc.Contains(entry.Key))
                    {
                        return CalculateHaversine(userLat.Value, userLng.Value, entry.Value.Lat, entry.Value.Lng);
                    }
                }
                return 4.2;
            }

            if (!string.IsNullOrWhiteSpace(searchLoc))
            {
                var sLoc = searchLoc.Trim().ToLower();
                if (combinedLoc.Contains(sLoc)) return 1.5;

                foreach (var entry in cityCoords)
                {
                    if (sLoc.Contains(entry.Key) && combinedLoc.Contains(entry.Key))
                    {
                        return 2.8;
                    }
                }
                return 7.5;
            }

            return 3.0;
        }

        private static double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 1);
        }

        // GET: /Services/Details/abc
        public async Task<IActionResult> Details(string id, DateTime? selectedDate)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var service = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

            if (service == null) return NotFound();

            DateTime date = selectedDate ?? DateTime.Today;
            if (date < DateTime.Today) date = DateTime.Today;

            // Check if provider has set an OffDay on this date
            var offDay = await _context.ProviderOffDays
                .FirstOrDefaultAsync(p => p.ProviderId == service.ProviderId && p.OffDate.Date == date.Date);

            bool isOffDay = (offDay != null);

            // Fetch working hours
            var workingHoursList = await _context.WorkingHours
                .Where(w => w.ProviderId == service.ProviderId && w.IsAvailable)
                .ToListAsync();

            var workingHours = workingHoursList.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);

            TimeSpan startTime = workingHours?.StartTime ?? new TimeSpan(8, 0, 0);
            TimeSpan endTime = workingHours?.EndTime ?? new TimeSpan(17, 0, 0);

            // US-14: nhà cung cấp đã cấu hình lịch tuần mà ngày này không làm việc -> không có khung giờ nào
            bool hasWeeklySchedule = await _context.WorkingHours.AnyAsync(w => w.ProviderId == service.ProviderId);
            if (hasWeeklySchedule && workingHours == null)
            {
                endTime = startTime;
            }

            // Fetch existing booked appointments
            var allAppointments = await _context.Appointments
                .Where(a => a.ProviderId == service.ProviderId && a.Status != "Cancelled")
                .ToListAsync();

            var bookedAppointments = allAppointments
                .Where(a => a.AppointmentDate.Date == date.Date)
                .ToList();

            var slots = new List<TimeSlotStatus>();
            TimeSpan current = startTime;
            int stepMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : 30;

            while (current.Add(TimeSpan.FromMinutes(stepMinutes)) <= endTime)
            {
                TimeSpan slotEnd = current.Add(TimeSpan.FromMinutes(stepMinutes));
                bool isBooked = bookedAppointments.Any(a => a.StartTime < slotEnd && a.EndTime > current);
                bool isPast = (date.Date == DateTime.Today && current < DateTime.Now.TimeOfDay);

                // If store is off on this date, no slots are available!
                bool available = !isOffDay && !isBooked && !isPast;

                slots.Add(new TimeSlotStatus
                {
                    Time = current,
                    IsAvailable = available
                });

                current = slotEnd;
            }

            var model = new BookingViewModel
            {
                ServiceId = service.Id,
                ServiceName = service.Name,
                ProviderName = service.Provider?.FullName ?? "N/A",
                Price = service.Price,
                DurationMinutes = service.DurationMinutes,
                CategoryName = service.Category?.Name ?? "Chưa phân loại",
                SelectedDate = date,
                IsProviderOffDay = isOffDay,
                OffDayReason = offDay?.Reason,
                AvailableSlots = slots
            };

            // US-30: đánh giá của khách hàng
            var reviews = await _context.Reviews.AsNoTracking()
                .Include(r => r.Customer)
                .Where(r => r.ServiceId == service.Id)
                .ToListAsync();
            ViewBag.Reviews = reviews.OrderByDescending(r => r.CreatedAt).ToList();
            ViewBag.Rating = new RatingInfo
            {
                Average = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
                Count = reviews.Count
            };

            // US-27: Khối "Có thể bạn cũng quan tâm" - Dịch vụ cùng danh mục liên quan
            var relatedServices = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Provider)
                .Where(s => s.CategoryId == service.CategoryId && s.Id != service.Id && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .Take(4)
                .ToListAsync();
            ViewBag.RelatedServices = relatedServices;

            ViewBag.Service = service;
            return View(model);
        }
    }
}
