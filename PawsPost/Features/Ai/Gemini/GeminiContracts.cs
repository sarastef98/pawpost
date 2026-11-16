using System.Text.Json.Serialization;

namespace PawsPost.Features.Ai.Gemini;

// The Gemini JSON wire format. Only used inside this folder.
internal record GeminiPart([property: JsonPropertyName("text")] string? Text);

internal record GeminiContent(
    [property: JsonPropertyName("parts")] GeminiPart[]? Parts,
    [property: JsonPropertyName("role")] string? Role = null);

internal record GeminiEmbedRequest(
    [property: JsonPropertyName("content")] GeminiContent Content,
    [property: JsonPropertyName("taskType")] string TaskType);

internal record GeminiEmbedding([property: JsonPropertyName("values")] float[]? Values);

internal record GeminiEmbedResponse([property: JsonPropertyName("embedding")] GeminiEmbedding? Embedding);

internal record GeminiGenerateRequest(
    [property: JsonPropertyName("systemInstruction")] GeminiContent SystemInstruction,
    [property: JsonPropertyName("contents")] GeminiContent[] Contents);

internal record GeminiCandidate([property: JsonPropertyName("content")] GeminiContent? Content);

internal record GeminiGenerateResponse([property: JsonPropertyName("candidates")] GeminiCandidate[]? Candidates);
