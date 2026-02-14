using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class ProcessingJobMappings
{
    public static ProcessingJobDto ToDto(this ProcessingJob job) =>
        new(job.Id, job.JobKey, job.Status, job.CreatedAt, job.CompletedAt);
}
