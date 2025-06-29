using System.ComponentModel.DataAnnotations;

namespace Supplier.Service.DTOs;

public record SupplierDto(
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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy
);

public record CreateSupplierDto(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,

    [StringLength(100)]
    string? ContactPerson,

    [EmailAddress]
    [StringLength(100)]
    string? Email,

    [Phone]
    [StringLength(20)]
    string? Phone,

    [StringLength(500)]
    string? Address,

    [StringLength(100)]
    string? City,

    [StringLength(100)]
    string? State,

    [StringLength(20)]
    string? PostalCode,

    [StringLength(100)]
    string? Country
);

public record UpdateSupplierDto(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,

    [StringLength(100)]
    string? ContactPerson,

    [EmailAddress]
    [StringLength(100)]
    string? Email,

    [Phone]
    [StringLength(20)]
    string? Phone,

    [StringLength(500)]
    string? Address,

    [StringLength(100)]
    string? City,

    [StringLength(100)]
    string? State,

    [StringLength(20)]
    string? PostalCode,

    [StringLength(100)]
    string? Country,

    bool IsActive
);
