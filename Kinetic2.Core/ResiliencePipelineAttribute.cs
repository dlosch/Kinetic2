namespace Kinetic2;

public sealed class ResiliencePipelineAttribute : Attribute {
    public string PipelineName { get; }
    public string? ActivitySpanName { get; }
    public bool AddLogStatements { get; } = true;
    public bool AddActivitySpan => !string.IsNullOrEmpty(ActivitySpanName);

    public ResiliencePipelineAttribute(string pipelineName, bool addLogStatements = true, string? activitySpanName = null) {
        PipelineName = pipelineName;
        AddLogStatements = addLogStatements;
        ActivitySpanName = activitySpanName;
    }
}
