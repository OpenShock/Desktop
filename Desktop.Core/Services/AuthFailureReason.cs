namespace OpenShock.Desktop.Services;

/// <summary>
/// Why <see cref="AuthService.Authenticate"/> failed. Kept coarse on purpose - it exists so the UI
/// can tell the user what to do about it, not to describe the exception.
/// </summary>
public enum AuthFailureReason
{
    /// <summary>No failure, authentication has not failed since the last attempt.</summary>
    None,

    /// <summary>The API token was rejected, the user has to log in again.</summary>
    Unauthorized,

    /// <summary>The backend could not be reached at all.</summary>
    Unreachable,

    /// <summary>
    /// The backend sent something this build cannot read, e.g. a permission or enum value added
    /// after this version shipped. The user needs a newer version.
    /// </summary>
    IncompatibleServer,

    /// <summary>The backend answered with an error.</summary>
    Backend,

    /// <summary>Anything else.</summary>
    Unknown
}
