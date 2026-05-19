namespace SmartDocScan.Api.Models;

public sealed class LoginResponse
{
    public bool MfaRequired { get; set; }
    public Guid? ChallengeId { get; set; }
    public UserDto? User { get; set; }
}

public sealed class VerifyOtpRequest
{
    public Guid ChallengeId { get; set; }
    public string? Code { get; set; }
}

public sealed class ChangePasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class CurrentUserDto
{
    public UserDto? User { get; set; }
    public bool Authenticated { get; set; }
    public string? AuthProvider { get; set; }
}
