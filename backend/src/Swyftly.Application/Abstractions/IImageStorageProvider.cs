namespace Swyftly.Application.Abstractions;

public interface IImageStorageProvider
{
    Task<ImageStorageReference> CreateReferenceAsync(
        CreateImageReferenceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateImageReferenceRequest(
    string StorageKey,
    string? Url);

public sealed record ImageStorageReference(
    string StorageKey,
    string Url);
