using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace Order.Service.Observability;

/// <summary>
/// Custom metrics for Order Service
/// </summary>
public class OrderMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _ordersCreatedCounter;
    private readonly Counter<long> _ordersCompletedCounter;
    private readonly Counter<long> _ordersCancelledCounter;
    private readonly Histogram<double> _orderProcessingDuration;
    private readonly Histogram<double> _orderValueHistogram;
    private readonly UpDownCounter<long> _activeOrdersGauge;

    public OrderMetrics()
    {
        _meter = new Meter("Order.Service", "1.0.0");
        
        // Counters for order lifecycle events
        _ordersCreatedCounter = _meter.CreateCounter<long>(
            "orders_created_total",
            description: "Total number of orders created");
            
        _ordersCompletedCounter = _meter.CreateCounter<long>(
            "orders_completed_total", 
            description: "Total number of orders completed");
            
        _ordersCancelledCounter = _meter.CreateCounter<long>(
            "orders_cancelled_total",
            description: "Total number of orders cancelled");

        // Histograms for distribution metrics
        _orderProcessingDuration = _meter.CreateHistogram<double>(
            "order_processing_duration_seconds",
            unit: "s",
            description: "Duration of order processing in seconds");
            
        _orderValueHistogram = _meter.CreateHistogram<double>(
            "order_value_distribution",
            unit: "USD",
            description: "Distribution of order values");

        // Gauge for current state
        _activeOrdersGauge = _meter.CreateUpDownCounter<long>(
            "active_orders_current",
            description: "Current number of active orders being processed");
    }

    /// <summary>
    /// Records a new order creation
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="orderValue">Order value in USD</param>
    public void RecordOrderCreated(Guid customerId, decimal orderValue)
    {
        var tags = new TagList
        {
            {"customer_id", customerId.ToString()},
            {"order_value_range", GetValueRange(orderValue)}
        };
        
        _ordersCreatedCounter.Add(1, tags);
        _orderValueHistogram.Record((double)orderValue, tags);
        _activeOrdersGauge.Add(1);
    }

    /// <summary>
    /// Records order completion
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="processingDuration">Time taken to process the order</param>
    /// <param name="orderValue">Order value in USD</param>
    public void RecordOrderCompleted(Guid customerId, TimeSpan processingDuration, decimal orderValue)
    {
        var tags = new TagList
        {
            {"customer_id", customerId.ToString()},
            {"order_value_range", GetValueRange(orderValue)},
            {"processing_time_range", GetDurationRange(processingDuration)}
        };
        
        _ordersCompletedCounter.Add(1, tags);
        _orderProcessingDuration.Record(processingDuration.TotalSeconds, tags);
        _activeOrdersGauge.Add(-1);
    }

    /// <summary>
    /// Records order cancellation
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="reason">Cancellation reason</param>
    public void RecordOrderCancelled(Guid customerId, string reason)
    {
        var tags = new TagList
        {
            {"customer_id", customerId.ToString()},
            {"cancellation_reason", reason}
        };
        
        _ordersCancelledCounter.Add(1, tags);
        _activeOrdersGauge.Add(-1);
    }

    /// <summary>
    /// Records inventory alert handling
    /// </summary>
    /// <param name="alertType">Type of inventory alert</param>
    /// <param name="productId">Product ID affected</param>
    public void RecordInventoryAlert(string alertType, Guid productId)
    {
        var alertCounter = _meter.CreateCounter<long>(
            "inventory_alerts_handled_total",
            description: "Total number of inventory alerts handled");
            
        var tags = new TagList
        {
            {"alert_type", alertType},
            {"product_id", productId.ToString()}
        };
        
        alertCounter.Add(1, tags);
    }

    /// <summary>
    /// Records business rule evaluation
    /// </summary>
    /// <param name="ruleName">Name of the business rule</param>
    /// <param name="result">Result of the evaluation</param>
    /// <param name="executionTime">Time taken to evaluate</param>
    public void RecordBusinessRuleEvaluation(string ruleName, bool result, TimeSpan executionTime)
    {
        var ruleCounter = _meter.CreateCounter<long>(
            "business_rules_evaluated_total",
            description: "Total number of business rule evaluations");
            
        var ruleHistogram = _meter.CreateHistogram<double>(
            "business_rule_execution_duration_seconds",
            unit: "s",
            description: "Duration of business rule execution");
            
        var tags = new TagList
        {
            {"rule_name", ruleName},
            {"result", result.ToString().ToLower()}
        };
        
        ruleCounter.Add(1, tags);
        ruleHistogram.Record(executionTime.TotalSeconds, tags);
    }

    /// <summary>
    /// Categorizes order values into ranges for better metrics grouping
    /// </summary>
    private static string GetValueRange(decimal value)
    {
        return value switch
        {
            <= 50 => "0-50",
            <= 100 => "51-100",
            <= 500 => "101-500",
            <= 1000 => "501-1000",
            <= 5000 => "1001-5000",
            _ => "5000+"
        };
    }

    /// <summary>
    /// Categorizes processing durations into ranges
    /// </summary>
    private static string GetDurationRange(TimeSpan duration)
    {
        return duration.TotalSeconds switch
        {
            <= 1 => "0-1s",
            <= 5 => "1-5s",
            <= 10 => "5-10s",
            <= 30 => "10-30s",
            <= 60 => "30-60s",
            _ => "60s+"
        };
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
