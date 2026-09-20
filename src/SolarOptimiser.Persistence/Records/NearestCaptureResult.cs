namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// SOL-T-1003's result: a <see cref="CaptureDetail"/> plus the signed time difference between the requested
    /// instant and the capture's <c>RequestedAtUTC</c> (positive = capture after the requested instant).
    /// </summary>
    public sealed record NearestCaptureResult(CaptureDetail Capture, TimeSpan SignedTimeDifference);
}
