using FluentAssertions;
using OnPoint.Domain;
using Xunit;

namespace OnPoint.UnitTests.Domain;

// Pins each domain enum to the exact string values used by the Postgres ENUM
// types in 0001_initial.sql. If a value drifts, EF will fail to round-trip
// rows the moment a real DB connects — this test catches that at build time.
public class EnumValuesTests
{
    [Fact]
    public void IssueStatus_HasExpectedValues() =>
        Enum.GetNames<IssueStatus>().Should().BeEquivalentTo(
            new[] { "open", "assigned", "in_progress", "resolved", "cancelled" });

    [Fact]
    public void IssuePriority_HasExpectedValues() =>
        Enum.GetNames<IssuePriority>().Should().BeEquivalentTo(
            new[] { "low", "medium", "high", "urgent" });

    [Fact]
    public void LocationType_HasExpectedValues() =>
        Enum.GetNames<LocationType>().Should().BeEquivalentTo(
            new[] { "room", "table", "public_area", "department", "service_point", "other" });

    [Fact]
    public void PointsEntryStatus_HasExpectedValues() =>
        Enum.GetNames<PointsEntryStatus>().Should().BeEquivalentTo(
            new[] { "confirmed", "pending_review", "reversed", "expired" });

    [Fact]
    public void BusinessType_HasExpectedValues() =>
        Enum.GetNames<BusinessType>().Should().BeEquivalentTo(
            new[] { "hotel", "restaurant", "retail", "service", "healthcare", "other" });

    [Fact]
    public void BusinessPlan_HasExpectedValues() =>
        Enum.GetNames<BusinessPlan>().Should().BeEquivalentTo(
            new[] { "trial", "starter", "growth", "enterprise" });

    [Fact]
    public void UserRole_HasExpectedValues() =>
        Enum.GetNames<UserRole>().Should().BeEquivalentTo(
            new[] { "platform_admin", "owner", "manager", "staff" });

    [Fact]
    public void FeedbackSentiment_HasExpectedValues() =>
        Enum.GetNames<FeedbackSentiment>().Should().BeEquivalentTo(
            new[] { "positive", "neutral", "negative", "unknown" });

    [Fact]
    public void FeedbackSeverity_HasExpectedValues() =>
        Enum.GetNames<FeedbackSeverity>().Should().BeEquivalentTo(
            new[] { "low", "medium", "high", "urgent", "unknown" });

    [Fact]
    public void AiStage_HasExpectedValues() =>
        Enum.GetNames<AiStage>().Should().BeEquivalentTo(
            new[]
            {
                "transcription", "sentiment", "classifier", "priority",
                "router", "matcher", "recommender", "satisfaction",
                "chatbot", "learning"
            });

    [Fact]
    public void AiProvider_HasExpectedValues() =>
        Enum.GetNames<AiProvider>().Should().BeEquivalentTo(
            new[] { "openai", "anthropic", "rule_based", "custom" });
}
