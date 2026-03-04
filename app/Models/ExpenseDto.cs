using System.ComponentModel.DataAnnotations;

namespace ExpenseApp.Models;

public class ExpenseDto
{
    [Required]
    public int UserId { get; set; } = 1; // Default to first user for demo

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [Range(0.01, 100000, ErrorMessage = "Amount must be between £0.01 and £100,000")]
    public decimal AmountGBP { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? ReceiptFile { get; set; }

    /// <summary>Amount in minor units (pence)</summary>
    public int AmountMinor => (int)(AmountGBP * 100);
}

public class ApproveRejectDto
{
    public int ReviewedBy { get; set; } = 2; // Default to manager user for demo
    public string? Comments { get; set; }
}
