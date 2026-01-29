using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class SessionMappings
{
    public static SessionDto ToDto(this Session session) =>
        new(session.Id, session.PatientId, session.TherapistId,
            session.SessionDate, session.SessionType, session.Modality,
            session.DurationMinutes, session.SessionNumber,
            session.CreatedAt, session.UpdatedAt);

    public static Session ToEntity(this CreateSessionRequest request) =>
        new()
        {
            PatientId = request.PatientId,
            TherapistId = request.TherapistId,
            SessionDate = request.SessionDate,
            SessionType = request.SessionType,
            Modality = request.Modality,
            DurationMinutes = request.DurationMinutes,
            SessionNumber = request.SessionNumber
        };
}
