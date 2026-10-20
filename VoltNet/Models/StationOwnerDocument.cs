using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class StationOwnerDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationOwnerId { get; set; }
    public virtual StationOwner StationOwner { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = null!; // "PAN Card", "Aadhaar / ID Proof", "Business Registration / MSME", "GST Certificate", "Bank Cancelled Cheque"

    [StringLength(100)]
    public string? DocumentNumber { get; set; } // PAN No, GSTIN, Aadhaar last 4, etc.

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = null!; // "/uploads/owner-kyc/guid.pdf"

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string FileExtension { get; set; } = null!;

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
