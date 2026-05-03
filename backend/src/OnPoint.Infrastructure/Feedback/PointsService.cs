using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Feedback;

public class PointsService
{
    private readonly AppDbContext _db;

    public PointsService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Inserts one immutable points_ledger row. Never updates.
    /// Returns the amount earned (may be 0 if high fraud — no row inserted).
    /// </summary>
    public int Award(
        Guid sessionId,
        Guid businessId,
        Guid feedbackId,
        string? comment,
        int fraudScore)
    {
        int amount = fraudScore >= 70 ? 0 : 10;

        if (amount > 0 && !string.IsNullOrEmpty(comment) && comment.Length >= 30)
            amount += 15;

        if (amount == 0)
            return 0;

        var entry = new PointsLedger
        {
            Id = Guid.NewGuid(),
            GuestUserId = null,
            SessionId = sessionId,
            BusinessId = businessId,
            FeedbackId = feedbackId,
            Amount = amount,
            Reason = "feedback_submitted",
            Status = PointsEntryStatus.confirmed,
            FraudScore = fraudScore,
            Flagged = fraudScore >= 30,
            CreatedAt = DateTime.UtcNow,
            ConfirmedAt = DateTime.UtcNow
        };

        _db.PointsLedgers.Add(entry);
        return amount;
    }
}
