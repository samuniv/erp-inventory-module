using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Supplier.Service.Observability;

/// <summary>
/// Custom metrics for Supplier Service
/// </summary>
public class SupplierMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _suppliersCreatedCounter;
    private readonly Counter<long> _suppliersUpdatedCounter;
    private readonly Counter<long> _suppliersDeletedCounter;
    private readonly Counter<long> _supplierContactUpdatesCounter;
    private readonly Counter<long> _supplierRatingUpdatesCounter;
    private readonly Histogram<double> _supplierRatingHistogram;
    private readonly Histogram<double> _supplierResponseTimeHistogram;
    private readonly UpDownCounter<long> _activeSupplierGauge;
    private readonly UpDownCounter<long> _preferredSupplierGauge;

    public SupplierMetrics()
    {
        _meter = new Meter("Supplier.Service", "1.0.0");

        // Supplier lifecycle counters
        _suppliersCreatedCounter = _meter.CreateCounter<long>(
            "suppliers_created_total",
            description: "Total number of suppliers created");

        _suppliersUpdatedCounter = _meter.CreateCounter<long>(
            "suppliers_updated_total",
            description: "Total number of suppliers updated");

        _suppliersDeletedCounter = _meter.CreateCounter<long>(
            "suppliers_deleted_total",
            description: "Total number of suppliers deleted");

        _supplierContactUpdatesCounter = _meter.CreateCounter<long>(
            "supplier_contact_updates_total",
            description: "Total number of supplier contact information updates");

        _supplierRatingUpdatesCounter = _meter.CreateCounter<long>(
            "supplier_rating_updates_total",
            description: "Total number of supplier rating updates");

        // Histograms for distribution metrics
        _supplierRatingHistogram = _meter.CreateHistogram<double>(
            "supplier_rating_distribution",
            unit: "rating",
            description: "Distribution of supplier ratings");

        _supplierResponseTimeHistogram = _meter.CreateHistogram<double>(
            "supplier_response_time_seconds",
            unit: "s",
            description: "Response time for supplier operations");

        // Gauges for current state
        _activeSupplierGauge = _meter.CreateUpDownCounter<long>(
            "active_suppliers_current",
            description: "Current number of active suppliers");

        _preferredSupplierGauge = _meter.CreateUpDownCounter<long>(
            "preferred_suppliers_current",
            description: "Current number of preferred suppliers");
    }

    /// <summary>
    /// Records a new supplier creation
    /// </summary>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="supplierType">Type of supplier</param>
    /// <param name="country">Supplier country</param>
    /// <param name="rating">Initial rating</param>
    /// <param name="isPreferred">Whether supplier is preferred</param>
    public void RecordSupplierCreated(Guid supplierId, string supplierType, string country, double? rating, bool isPreferred)
    {
        var tags = new TagList
        {
            {"supplier_id", supplierId.ToString()},
            {"supplier_type", supplierType},
            {"country", country},
            {"is_preferred", isPreferred.ToString().ToLower()},
            {"has_rating", (rating.HasValue).ToString().ToLower()}
        };

        if (rating.HasValue)
        {
            tags.Add("rating_range", GetRatingRange(rating.Value));
        }

        _suppliersCreatedCounter.Add(1, tags);

        if (rating.HasValue)
        {
            _supplierRatingHistogram.Record(rating.Value, tags);
        }

        _activeSupplierGauge.Add(1);

        if (isPreferred)
        {
            _preferredSupplierGauge.Add(1);
        }
    }

    /// <summary>
    /// Records supplier update
    /// </summary>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="supplierType">Type of supplier</param>
    /// <param name="updateType">Type of update (contact, rating, status, etc.)</param>
    /// <param name="oldRating">Previous rating</param>
    /// <param name="newRating">New rating</param>
    /// <param name="wasPreferred">Previous preferred status</param>
    /// <param name="isPreferred">Current preferred status</param>
    /// <param name="responseTime">Time taken for the update operation</param>
    public void RecordSupplierUpdated(Guid supplierId, string supplierType, string updateType,
        double? oldRating, double? newRating, bool wasPreferred, bool isPreferred, TimeSpan responseTime)
    {
        var tags = new TagList
        {
            {"supplier_id", supplierId.ToString()},
            {"supplier_type", supplierType},
            {"update_type", updateType},
            {"rating_changed", (oldRating != newRating).ToString().ToLower()},
            {"preference_changed", (wasPreferred != isPreferred).ToString().ToLower()},
            {"response_time_range", GetResponseTimeRange(responseTime)}
        };

        if (newRating.HasValue)
        {
            tags.Add("new_rating_range", GetRatingRange(newRating.Value));
        }

        _suppliersUpdatedCounter.Add(1, tags);
        _supplierResponseTimeHistogram.Record(responseTime.TotalSeconds, tags);

        // Track specific update types
        if (updateType.Contains("contact"))
        {
            _supplierContactUpdatesCounter.Add(1, tags);
        }

        if (oldRating != newRating && newRating.HasValue)
        {
            _supplierRatingUpdatesCounter.Add(1, tags);
            _supplierRatingHistogram.Record(newRating.Value, tags);
        }

        // Update preferred supplier gauge
        if (!wasPreferred && isPreferred)
        {
            _preferredSupplierGauge.Add(1);
        }
        else if (wasPreferred && !isPreferred)
        {
            _preferredSupplierGauge.Add(-1);
        }
    }

    /// <summary>
    /// Records supplier deletion
    /// </summary>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="supplierType">Type of supplier</param>
    /// <param name="finalRating">Final rating before deletion</param>
    /// <param name="wasPreferred">Whether supplier was preferred</param>
    /// <param name="deletionReason">Reason for deletion</param>
    public void RecordSupplierDeleted(Guid supplierId, string supplierType, double? finalRating, bool wasPreferred, string deletionReason)
    {
        var tags = new TagList
        {
            {"supplier_id", supplierId.ToString()},
            {"supplier_type", supplierType},
            {"was_preferred", wasPreferred.ToString().ToLower()},
            {"deletion_reason", deletionReason}
        };

        if (finalRating.HasValue)
        {
            tags.Add("final_rating_range", GetRatingRange(finalRating.Value));
        }

        _suppliersDeletedCounter.Add(1, tags);
        _activeSupplierGauge.Add(-1);

        if (wasPreferred)
        {
            _preferredSupplierGauge.Add(-1);
        }
    }

    /// <summary>
    /// Records supplier search operation
    /// </summary>
    /// <param name="searchType">Type of search</param>
    /// <param name="resultCount">Number of results</param>
    /// <param name="searchDuration">Time taken for search</param>
    /// <param name="filters">Applied filters</param>
    public void RecordSupplierSearch(string searchType, int resultCount, TimeSpan searchDuration, string[]? filters)
    {
        var searchCounter = _meter.CreateCounter<long>(
            "supplier_searches_total",
            description: "Total number of supplier search operations");

        var searchDurationHistogram = _meter.CreateHistogram<double>(
            "supplier_search_duration_seconds",
            unit: "s",
            description: "Duration of supplier search operations");

        var tags = new TagList
        {
            {"search_type", searchType},
            {"result_count_range", GetResultCountRange(resultCount)},
            {"has_filters", (filters?.Length > 0).ToString().ToLower()},
            {"filter_count", filters?.Length ?? 0}
        };

        searchCounter.Add(1, tags);
        searchDurationHistogram.Record(searchDuration.TotalSeconds, tags);
    }

    /// <summary>
    /// Records supplier performance evaluation
    /// </summary>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="evaluationType">Type of evaluation</param>
    /// <param name="score">Performance score</param>
    /// <param name="previousScore">Previous score for comparison</param>
    /// <param name="evaluationCriteria">Criteria used for evaluation</param>
    public void RecordSupplierPerformanceEvaluation(Guid supplierId, string evaluationType, double score,
        double? previousScore, string[] evaluationCriteria)
    {
        var evaluationCounter = _meter.CreateCounter<long>(
            "supplier_evaluations_total",
            description: "Total number of supplier performance evaluations");

        var performanceScoreHistogram = _meter.CreateHistogram<double>(
            "supplier_performance_score_distribution",
            unit: "score",
            description: "Distribution of supplier performance scores");

        var tags = new TagList
        {
            {"supplier_id", supplierId.ToString()},
            {"evaluation_type", evaluationType},
            {"score_range", GetScoreRange(score)},
            {"criteria_count", evaluationCriteria.Length}
        };

        if (previousScore.HasValue)
        {
            tags.Add("score_trend", GetScoreTrend(previousScore.Value, score));
        }

        evaluationCounter.Add(1, tags);
        performanceScoreHistogram.Record(score, tags);
    }

    /// <summary>
    /// Records supplier contract event
    /// </summary>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="contractType">Type of contract</param>
    /// <param name="eventType">Contract event (created, renewed, terminated, etc.)</param>
    /// <param name="contractValue">Value of the contract</param>
    /// <param name="duration">Contract duration</param>
    public void RecordSupplierContractEvent(Guid supplierId, string contractType, string eventType,
        decimal? contractValue, TimeSpan? duration)
    {
        var contractEventCounter = _meter.CreateCounter<long>(
            "supplier_contract_events_total",
            description: "Total number of supplier contract events");

        var contractValueHistogram = _meter.CreateHistogram<double>(
            "supplier_contract_value_distribution",
            unit: "USD",
            description: "Distribution of supplier contract values");

        var tags = new TagList
        {
            {"supplier_id", supplierId.ToString()},
            {"contract_type", contractType},
            {"event_type", eventType}
        };

        if (contractValue.HasValue)
        {
            tags.Add("value_range", GetContractValueRange(contractValue.Value));
        }

        if (duration.HasValue)
        {
            tags.Add("duration_range", GetContractDurationRange(duration.Value));
        }

        contractEventCounter.Add(1, tags);

        if (contractValue.HasValue)
        {
            contractValueHistogram.Record((double)contractValue.Value, tags);
        }
    }

    /// <summary>
    /// Categorizes supplier ratings into ranges
    /// </summary>
    private static string GetRatingRange(double rating)
    {
        return rating switch
        {
            >= 4.5 => "excellent",
            >= 4.0 => "very_good",
            >= 3.5 => "good",
            >= 3.0 => "average",
            >= 2.0 => "below_average",
            _ => "poor"
        };
    }

    /// <summary>
    /// Categorizes response times into ranges
    /// </summary>
    private static string GetResponseTimeRange(TimeSpan responseTime)
    {
        return responseTime.TotalMilliseconds switch
        {
            <= 100 => "very_fast",
            <= 500 => "fast",
            <= 1000 => "normal",
            <= 2000 => "slow",
            _ => "very_slow"
        };
    }

    /// <summary>
    /// Categorizes search result counts
    /// </summary>
    private static string GetResultCountRange(int count)
    {
        return count switch
        {
            0 => "no_results",
            <= 5 => "few",
            <= 20 => "moderate",
            <= 50 => "many",
            _ => "extensive"
        };
    }

    /// <summary>
    /// Categorizes performance scores
    /// </summary>
    private static string GetScoreRange(double score)
    {
        return score switch
        {
            >= 90 => "excellent",
            >= 80 => "very_good",
            >= 70 => "good",
            >= 60 => "average",
            >= 50 => "below_average",
            _ => "poor"
        };
    }

    /// <summary>
    /// Determines score trend
    /// </summary>
    private static string GetScoreTrend(double previousScore, double currentScore)
    {
        var difference = currentScore - previousScore;
        return difference switch
        {
            > 10 => "significant_improvement",
            > 5 => "improvement",
            > 1 => "slight_improvement",
            < -10 => "significant_decline",
            < -5 => "decline",
            < -1 => "slight_decline",
            _ => "stable"
        };
    }

    /// <summary>
    /// Categorizes contract values
    /// </summary>
    private static string GetContractValueRange(decimal value)
    {
        return value switch
        {
            <= 10000 => "small",
            <= 50000 => "medium",
            <= 100000 => "large",
            <= 500000 => "very_large",
            _ => "enterprise"
        };
    }

    /// <summary>
    /// Categorizes contract durations
    /// </summary>
    private static string GetContractDurationRange(TimeSpan duration)
    {
        return duration.TotalDays switch
        {
            <= 30 => "short_term",
            <= 90 => "quarterly",
            <= 365 => "annual",
            <= 1095 => "multi_year",
            _ => "long_term"
        };
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
