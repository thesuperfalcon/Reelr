using backend.Features.Paging;

namespace backend.Features.Reviews.DTOs
{
    public class MovieReviewPageDto : PageDto<ReviewDto>
    {
        // Every review of the film the caller can see, not only this page.
        public int TotalCount { get; set; }
    }
}
