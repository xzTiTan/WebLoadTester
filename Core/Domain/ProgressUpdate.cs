namespace WebLoadTester.Core.Domain;

public record ProgressUpdate(int Current, int Total, string? Message = null)
{
    public double Percentage => Total <= 0 ? 0 : (double)Current / Total * 100;
}
