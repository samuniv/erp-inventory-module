using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Service.DTOs;
using Order.Service.Models;
using Order.Service.Services;
using System.ComponentModel.DataAnnotations;

namespace Order.Service.Controllers;

/// <summary>
/// API controller for order management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    /// <param name="request">Order creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created order details</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Creating order for customer: {CustomerEmail}", request.CustomerEmail);

            var order = await _orderService.CreateOrderAsync(request, cancellationToken);

            _logger.LogInformation("Successfully created order {OrderId} for customer: {CustomerEmail}",
                order.Id, request.CustomerEmail);

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer: {CustomerEmail}", request.CustomerEmail);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the order." });
        }
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrderResponse>> GetOrder(
        int id,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderService.GetOrderAsync(id, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = $"Order {id} not found." });
        }

        return Ok(order);
    }

    /// <summary>
    /// Get paginated list of orders
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orders</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new { message = "Page must be greater than 0." });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "Page size must be between 1 and 100." });
        }

        var orders = await _orderService.GetOrdersAsync(page, pageSize, cancellationToken);
        return Ok(orders);
    }

    /// <summary>
    /// Update order status
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated order details</returns>
    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(
        int id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { message = "Invalid order status." });
        }

        try
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, status, cancellationToken);

            if (order == null)
            {
                return NotFound(new { message = $"Order {id} not found." });
            }

            _logger.LogInformation("Updated order {OrderId} status to {Status}", id, status);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid status transition for order {OrderId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while updating the order status." });
        }
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">Cancellation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CancelOrder(
        int id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _orderService.CancelOrderAsync(id, request.Reason, cancellationToken);

            if (!success)
            {
                return NotFound(new { message = $"Order {id} not found or already cancelled." });
            }

            _logger.LogInformation("Cancelled order {OrderId} with reason: {Reason}", id, request.Reason);
            return Ok(new { message = $"Order {id} has been cancelled successfully." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel order {OrderId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while cancelling the order." });
        }
    }

    /// <summary>
    /// Get order statistics
    /// </summary>
    /// <returns>Order statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult GetOrderStatistics()
    {
        // This would typically involve more complex statistics
        // For now, return a simple placeholder
        var stats = new
        {
            Message = "Order statistics endpoint - to be implemented",
            Timestamp = DateTime.UtcNow
        };

        return Ok(stats);
    }
}

/// <summary>
/// Request DTO for updating order status
/// </summary>
public class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for cancelling an order
/// </summary>
public class CancelOrderRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
