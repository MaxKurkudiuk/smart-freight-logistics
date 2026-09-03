using System.ComponentModel.DataAnnotations;
using OrderService.Domain.Enums;

namespace OrderService.Application.DTOs;

public sealed record UpdateStatusRequest
{
    [Required]
    public OrderStatus NewStatus { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}
