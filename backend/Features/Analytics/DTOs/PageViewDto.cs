using System.ComponentModel.DataAnnotations;

namespace backend.Features.Analytics.DTOs
{
    public class PageViewDto
    {
        [Required]
        [MaxLength(PageView.RouteMaxLength)]
        public string? Route { get; set; }
    }
}
