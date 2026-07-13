public record DiscoveredTest
{
    public required string FullyQualifiedName { get; init; }
    public required string Method { get; init; }
    public required string Site { get; init; }
    public required IReadOnlyList<string> Envs { get; init; }
    public required string Kind { get; init; }   
    public string? Module { get; init; }          
    public string? Category { get; init; }         
}
public interface IDiscovery
{
    IReadOnlyList<DiscoveredTest> Discover();
}