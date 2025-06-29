using MediatR;
using Supplier.Service.DTOs;

namespace Supplier.Service.Application.Commands;

public record CreateSupplierCommand(
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? CreatedBy = null
) : IRequest<SupplierDto>;

public record UpdateSupplierCommand(
    int Id,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    bool IsActive,
    string? UpdatedBy = null
) : IRequest<SupplierDto?>;

public record DeleteSupplierCommand(int Id) : IRequest<bool>;
