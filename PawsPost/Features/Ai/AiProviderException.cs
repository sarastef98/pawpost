namespace PawsPost.Features.Ai;

/// <summary>
/// An AI provider call failed. The message is always safe to log: it never holds provider response
/// bodies, ad text, questions or keys.
/// </summary>
public class AiProviderException : Exception
{
    public AiProviderException(string message)
        : base(message)
    {
    }

    public AiProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
