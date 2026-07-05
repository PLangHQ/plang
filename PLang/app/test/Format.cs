namespace app.test;

/// <summary>Test report file artefact format. Console output is written regardless.</summary>
public enum Format
{
    /// <summary>Structured JSON at <c>.test/results.json</c> (default).</summary>
    Json,
    /// <summary>JUnit XML at <c>.test/junit.xml</c> for CI ingestion.</summary>
    JUnit
}
