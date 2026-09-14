using System.ComponentModel.DataAnnotations;

namespace Appointment.Models.ViewModels
{
    public class BookingViewModel
    {
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt lịch")]
        [DataType(DataType.Date)]
        public DateTime SelectedDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn khung giờ")]
        public string SelectedTimeSlot { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public bool IsProviderOffDay { get; set; } = false;
        public string? OffDayReason { get; set; }

        public List<TimeSlotStatus> AvailableSlots { get; set; } = new List<TimeSlotStatus>();
    }

    public class TimeSlotStatus
    {
        public TimeSpan Time { get; set; }
        public string TimeFormatted => Time.ToString(@"hh\:mm");
        public bool IsAvailable { get; set; }
    }
}
