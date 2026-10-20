using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EIP.Data.DataLake.Infrastructure;
using FluentAssertions;

namespace EIP.Host.IntegrationTests;

public sealed class RawStorageUsageReaderTests
{
    [Fact]
    public async Task ReadUsage_PaginatesAndUsesHalfOpenUtcPeriod_WithTenantPrefix()
    {
        var tenantId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        using var s3 = new PagedS3Client(from, to);
        using var store = new S3RawObjectStore(s3, "usage-tests");
        var usage = await store.ReadUsageAsync(tenantId, from, to, CancellationToken.None);
        usage.Bytes.Should().Be(8);
        usage.Objects.Should().Be(2);
        s3.Prefixes.Should().OnlyContain(p => p == $"{tenantId:D}/");
        s3.Tokens.Should().Equal(null, "next");
    }

    private sealed class PagedS3Client(DateTimeOffset from, DateTimeOffset to)
        : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost:9000", ForcePathStyle = true })
    {
        public List<string> Prefixes { get; } = [];
        public List<string?> Tokens { get; } = [];

        public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
        {
            Prefixes.Add(request.Prefix);
            Tokens.Add(request.ContinuationToken);
            return Task.FromResult(request.ContinuationToken is null
                ? new ListObjectsV2Response
                {
                    IsTruncated = true,
                    NextContinuationToken = "next",
                    S3Objects = [new S3Object { Key = "start", Size = 3, LastModified = from.UtcDateTime },
                        new S3Object { Key = "before", Size = 100, LastModified = from.AddSeconds(-1).UtcDateTime }],
                }
                : new ListObjectsV2Response
                {
                    IsTruncated = false,
                    S3Objects = [new S3Object { Key = "inside", Size = 5, LastModified = from.AddHours(1).UtcDateTime },
                        new S3Object { Key = "end", Size = 200, LastModified = to.UtcDateTime }],
                });
        }
    }
}
