namespace CostTracker.Application.Exceptions;

public class DomainValidationException(string message) : Exception(message);
