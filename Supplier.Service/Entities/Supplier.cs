using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Supplier.Service.Entities;

[Table("SUPPLIERS")]
public class Supplier
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Required]
    [Column("NAME")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("CONTACT_PERSON")]
    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [Column("EMAIL")]
    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [Column("PHONE")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column("ADDRESS")]
    [StringLength(500)]
    public string? Address { get; set; }

    [Column("CITY")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("STATE")]
    [StringLength(100)]
    public string? State { get; set; }

    [Column("POSTAL_CODE")]
    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Column("COUNTRY")]
    [StringLength(100)]
    public string? Country { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("CREATED_BY")]
    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [Column("UPDATED_BY")]
    [StringLength(100)]
    public string? UpdatedBy { get; set; }
}
