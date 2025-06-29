using MediatR;
using Supplier.Service.DTOs;

namespace Supplier.Service.Application.Queries;

public record GetAllSuppliersQuery(
    string? SearchTerm = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<GetAllSuppliersResponse>;

public record GetAllSuppliersResponse(
    IEnumerable<SupplierDto> Suppliers,
    int TotalCount,
    int Page,
    int PageSize
);

public record GetSupplierByIdQuery(int Id) : IRequest<SupplierDto?>;
