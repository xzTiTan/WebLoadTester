using System.Collections.Generic;

namespace WebLoadTester.Domain;

public class Scenario
{
    public List<ScenarioStep> Steps { get; set; } = new();
}

public class ScenarioStep
{
    public string Selector { get; set; } = string.Empty;
}
