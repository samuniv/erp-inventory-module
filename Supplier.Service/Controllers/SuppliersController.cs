using MediatR;
using Microsoft.AspNetCore.Mvc;
using Supplier.Service.Application.Commands;
using Supplier.Service.Application.Queries;
using Supplier.Service.DTOs;

namespace Supplier.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(IMediator mediator, ILogger<SuppliersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all suppliers with optional filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GetAllSuppliersResponse>> GetSuppliers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetAllSuppliersQuery(searchTerm, isActive, page, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers");
            return StatusCode(500, new { message = "An error occurred while retrieving suppliers" });
        }
    }

    /// <summary>
    /// Get a specific supplier by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDto>> GetSupplier(int id)
    {
        try
        {
            var query = new GetSupplierByIdQuery(id);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return NotFound(new { message = $"Supplier with ID {id} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier with ID {SupplierId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the supplier" });
        }
    }

    /// <summary>
    /// Create a new supplier
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SupplierDto>> CreateSupplier([FromBody] CreateSupplierDto createSupplierDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateSupplierCommand(
                createSupplierDto.Name,
                createSupplierDto.ContactPerson,
                createSupplierDto.Email,
                createSupplierDto.Phone,
                createSupplierDto.Address,
                createSupplierDto.City,
                createSupplierDto.State,
                createSupplierDto.PostalCode,
                createSupplierDto.Country,
                "API" // TODO: Get from authenticated user context
            );

            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetSupplier), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier");
            return StatusCode(500, new { message = "An error occurred while creating the supplier" });
        }
    }

    /// <summary>
    /// Update an existing supplier
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(int id, [FromBody] UpdateSupplierDto updateSupplierDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateSupplierCommand(
                id,
                updateSupplierDto.Name,
                updateSupplierDto.ContactPerson,
                updateSupplierDto.Email,
                updateSupplierDto.Phone,
                updateSupplierDto.Address,
                updateSupplierDto.City,
                updateSupplierDto.State,
                updateSupplierDto.PostalCode,
                updateSupplierDto.Country,
                updateSupplierDto.IsActive,
                "API" // TODO: Get from authenticated user context
            );

            var result = await _mediator.Send(command);

            if (result == null)
            {
                return NotFound(new { message = $"Supplier with ID {id} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier with ID {SupplierId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the supplier" });
        }
    }

    /// <summary>
    /// Delete a supplier
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteSupplier(int id)
    {
        try
        {
            var command = new DeleteSupplierCommand(id);
            var result = await _mediator.Send(command);

            if (!result)
            {
                return NotFound(new { message = $"Supplier with ID {id} not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier with ID {SupplierId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the supplier" });
        }
    }
}
