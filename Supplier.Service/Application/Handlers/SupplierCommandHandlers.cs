using MediatR;
using Microsoft.EntityFrameworkCore;
using Supplier.Service.Application.Commands;
using Supplier.Service.Data;
using Supplier.Service.DTOs;

namespace Supplier.Service.Application.Handlers;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly SupplierDbContext _context;

    public CreateSupplierCommandHandler(SupplierDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = new Entities.Supplier
        {
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy ?? "System",
            UpdatedBy = request.CreatedBy ?? "System"
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.City,
            supplier.State,
            supplier.PostalCode,
            supplier.Country,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.UpdatedAt,
            supplier.CreatedBy,
            supplier.UpdatedBy
        );
    }
}

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto?>
{
    private readonly SupplierDbContext _context;

    public UpdateSupplierCommandHandler(SupplierDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierDto?> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supplier == null)
        {
            return null;
        }

        supplier.Name = request.Name;
        supplier.ContactPerson = request.ContactPerson;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.Address = request.Address;
        supplier.City = request.City;
        supplier.State = request.State;
        supplier.PostalCode = request.PostalCode;
        supplier.Country = request.Country;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = request.UpdatedBy ?? "System";

        await _context.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.City,
            supplier.State,
            supplier.PostalCode,
            supplier.Country,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.UpdatedAt,
            supplier.CreatedBy,
            supplier.UpdatedBy
        );
    }
}

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, bool>
{
    private readonly SupplierDbContext _context;

    public DeleteSupplierCommandHandler(SupplierDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supplier == null)
        {
            return false;
        }

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
