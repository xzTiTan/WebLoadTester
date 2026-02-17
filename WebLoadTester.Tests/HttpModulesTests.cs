using System.Collections.Generic;
using System.Text.Json;
using WebLoadTester.Modules.HttpAssets;
using WebLoadTester.Modules.HttpFunctional;
using WebLoadTester.Modules.HttpPerformance;
using Xunit;

namespace WebLoadTester.Tests;

public class HttpModulesTests
{
    [Fact]
    public void FunctionalValidate_ReturnsRussianErrorsAndRequiredRules()
    {
        var module = new HttpFunctionalModule();
        var settings = new HttpFunctionalSettings
        {
            BaseUrl = "",
            TimeoutSeconds = 0,
            Endpoints = new List<HttpFunctionalEndpoint>
            {
                new() { Name = "", Method = "", Path = "", ExpectedStatusCode = null },
                new() { Name = "dup", Method = "GET", Path = "/", ExpectedStatusCode = 700 },
                new() { Name = "dup", Method = "GET", Path = "/ok", ExpectedStatusCode = 200 }
            }
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("BaseUrl обязателен"));
        Assert.Contains(errors, e => e.Contains("TimeoutSeconds должен быть больше 0"));
        Assert.Contains(errors, e => e.Contains("Name обязателен"));
        Assert.Contains(errors, e => e.Contains("ExpectedStatusCode обязателен"));
        Assert.Contains(errors, e => e.Contains("100..599"));
        Assert.Contains(errors, e => e.Contains("уникальным"));
    }

    [Fact]
    public void PerformanceValidate_ReturnsRussianErrors()
    {
        var module = new HttpPerformanceModule();
        var settings = new HttpPerformanceSettings
        {
            BaseUrl = "invalid",
            TimeoutSeconds = 0,
            Endpoints = new List<HttpPerformanceEndpoint>
            {
                new() { Name = "dup", Method = "", Path = "" },
                new() { Name = "dup", Method = "GET", Path = "/", ExpectedStatusCode = 999 }
            }
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("BaseUrl обязателен"));
        Assert.Contains(errors, e => e.Contains("TimeoutSeconds должен быть больше 0"));
        Assert.Contains(errors, e => e.Contains("Method обязателен"));
        Assert.Contains(errors, e => e.Contains("Path обязателен"));
        Assert.Contains(errors, e => e.Contains("уникальным"));
        Assert.Contains(errors, e => e.Contains("100..599"));
    }

    [Fact]
    public void AssetsValidate_ReturnsRussianErrors()
    {
        var module = new HttpAssetsModule();
        var settings = new HttpAssetsSettings
        {
            TimeoutSeconds = 0,
            Assets = new List<AssetItem>
            {
                new() { Url = "not-url", MaxLatencyMs = 0, MaxSizeKb = 0 }
            }
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("TimeoutSeconds должен быть больше 0"));
        Assert.Contains(errors, e => e.Contains("Url обязателен"));
        Assert.Contains(errors, e => e.Contains("MaxLatencyMs должен быть больше 0"));
        Assert.Contains(errors, e => e.Contains("MaxSizeKb должен быть больше 0"));
    }

    [Fact]
    public void FunctionalJsonPath_SupportsBracketAndDotIndexSyntax()
    {
        const string json = """
                            {
                              "items": [
                                { "id": 42, "meta": { "name": "demo" } }
                              ]
                            }
                            """;

        var positive = HttpFunctionalModule.CheckJsonFieldEquals(json, new[] { "items[0].id=42", "items.0.meta.name=demo" });
        var negative = HttpFunctionalModule.CheckJsonFieldEquals(json, new[] { "items.0.id=100" });

        Assert.True(positive.Ok);
        Assert.False(negative.Ok);
        Assert.Contains("ожидали '100'", negative.Message);
    }

    [Fact]
    public void FunctionalLegacyMapping_MapsStatusCodeAndHeaders()
    {
        var endpoint = JsonSerializer.Deserialize<HttpFunctionalEndpoint>("""
            {
              "name": "legacy",
              "method": "GET",
              "path": "/",
              "statusCodeEquals": 201,
              "headers": {
                "X-Trace-Id": "abc"
              }
            }
            """)!;

        endpoint.NormalizeLegacy();

        Assert.Equal(201, endpoint.ExpectedStatusCode);
        Assert.Contains("X-Trace-Id:abc", endpoint.RequiredHeaders);
    }
}
