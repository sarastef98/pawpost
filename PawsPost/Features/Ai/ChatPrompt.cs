namespace PawsPost.Features.Ai;

/// <summary>A provider-neutral prompt: fixed instructions and the content built from the question and the ads.</summary>
public record ChatPrompt(string SystemInstruction, string UserContent);
