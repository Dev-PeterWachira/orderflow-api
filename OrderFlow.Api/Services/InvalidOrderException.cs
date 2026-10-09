namespace OrderFlow.Api.Services;

public class InvalidOrderException : Exception
{
    public InvalidOrderException(string message) : base(message)
    {
    }
}