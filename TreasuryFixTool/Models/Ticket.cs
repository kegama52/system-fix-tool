using System;

namespace TreasuryFixTool.Models;

public class Ticket
{
    public string TicketId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StepsTaken { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string DetectedIssues { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}