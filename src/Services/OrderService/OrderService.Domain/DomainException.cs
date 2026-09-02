namespace OrderService.Domain.Entities;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
