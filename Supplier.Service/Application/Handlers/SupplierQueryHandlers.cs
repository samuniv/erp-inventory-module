using MediatR;
using Microsoft.EntityFrameworkCore;
using Supplier.Service.Application.Queries;
using Supplier.Service.Data;
using Supplier.Service.DTOs;

namespace Supplier.Service.Application.Handlers;

public class GetAllSuppliersQueryHandler : IRequestHandler<GetAllSuppliersQuery, GetAllSuppliersResponse>
{
    private readonly SupplierDbContext _context;

    public GetAllSuppliersQueryHandler(SupplierDbContext context)
    {
        _context = context;
    }

    public async Task<GetAllSuppliersResponse> Handle(GetAllSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Suppliers.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchTerm) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchTerm)) ||
                (s.Email != null && s.Email.ToLower().Contains(searchTerm)) ||
                (s.City != null && s.City.ToLower().Contains(searchTerm)) ||
                (s.Country != null && s.Country.ToLower().Contains(searchTerm))
            );
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Address,
                s.City,
                s.State,
                s.PostalCode,
                s.Country,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt,
                s.CreatedBy,
                s.UpdatedBy
            ))
            .ToListAsync(cancellationToken);

        return new GetAllSuppliersResponse(suppliers, totalCount, request.Page, request.PageSize);
    }
}

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly SupplierDbContext _context;

    public GetSupplierByIdQueryHandler(SupplierDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .Where(s => s.Id == request.Id)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Address,
                s.City,
                s.State,
                s.PostalCode,
                s.Country,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt,
                s.CreatedBy,
                s.UpdatedBy
            ))
            .FirstOrDefaultAsync(cancellationToken);

        return supplier;
    }
}
