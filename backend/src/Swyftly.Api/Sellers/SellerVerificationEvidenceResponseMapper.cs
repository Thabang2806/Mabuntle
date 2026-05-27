using Swyftly.Application.Sellers;
using Swyftly.Domain.Sellers;

namespace Swyftly.Api.Sellers;

public static class SellerVerificationEvidenceResponseMapper
{
    public static SellerVerificationEvidenceResponse Map(SellerVerificationEvidence evidence) =>
        new(
            evidence.Id,
            evidence.EvidenceType.ToString(),
            evidence.OriginalFileName,
            evidence.ContentType,
            evidence.ByteSize,
            evidence.Sha256Hash,
            evidence.Note,
            evidence.UploadedAtUtc,
            evidence.RemovedAtUtc);
}
