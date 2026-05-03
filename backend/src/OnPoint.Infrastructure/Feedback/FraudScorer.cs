using Microsoft.EntityFrameworkCore;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Feedback;

public class FraudScorer
{
    private readonly AppDbContext _db;

    public FraudScorer(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> ScoreAsync(Guid sessionId, string? honeypot)
    {
        if (!string.IsNullOrEmpty(honeypot))
            return 100;

        int score = 0;

        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        bool recentEntry = await _db.PointsLedgers
            .AnyAsync(p =>
                p.SessionId == sessionId &&
                p.Reason == "feedback_submitted" &&
                p.CreatedAt >= cutoff);

        if (recentEntry)
            score += 40;

        return Math.Min(score, 100);
    }
}
