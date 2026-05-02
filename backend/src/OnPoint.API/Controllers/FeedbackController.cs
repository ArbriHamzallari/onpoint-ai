using Microsoft.AspNetCore.Mvc;
using OnPoint.Infrastructure.Persistence;
using OnPoint.Domain;

namespace OnPoint.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeedbackController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(Feedback feedback)
        {
            feedback.Id = Guid.NewGuid();
            feedback.CreatedAt = DateTime.UtcNow;

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(feedback);
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var data = _context.Feedbacks.ToList();
            return Ok(data);
        }
    }
}