using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class StationDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationId { get; set; }
    public virtual Station Station { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = null!; // "DISCOM Sanction Letter", "Land Lease / Ownership", "Fire Safety NOC", "CEIG Clearance / SLD", "Site Photographs"

    [StringLength(100)]
    public string? DocumentNumber { get; set; } // Sanction ID / Reference / Consumer No.

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = null!; // "/uploads/station-documents/guid.pdf"

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string FileExtension { get; set; } = null!; // .pdf, .jpg, .png

    public long FileSizeBytes { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Pending"; // "Pending", "Verified", "Rejected"

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedByAdminId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
