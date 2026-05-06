namespace Alza.HttpExtensions;

/// <summary>
/// Represents a communication error between microservices
/// </summary>
/// <param name="ErrorCode">A short error code that could be use by API consumer's systems</param>
/// <param name="UserFriendlyErrorDescription">
/// Short error message that is relevant for the external API user and DOES NOT include any details of local infrastructure
/// </param>
public sealed record SharedErrorModel(string ErrorCode, string UserFriendlyErrorDescription);
