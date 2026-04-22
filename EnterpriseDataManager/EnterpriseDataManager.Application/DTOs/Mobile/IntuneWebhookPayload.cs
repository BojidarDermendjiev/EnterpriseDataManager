namespace EnterpriseDataManager.Application.DTOs.Mobile;

public record IntuneWebhookPayload(
    string DeviceId,
    string ComplianceStatus,
    string UserId);
