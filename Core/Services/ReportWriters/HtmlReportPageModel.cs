using System.Collections.Generic;

namespace WebLoadTester.Core.Services.ReportWriters;

internal sealed record HtmlReportPageModel(
    string DocumentTitle,
    HtmlReportHeroModel Hero,
    string ExecutiveSummary,
    IReadOnlyList<HtmlReportFactModel> RunOverview,
    IReadOnlyList<HtmlReportCardModel> SummaryCards,
    IReadOnlyList<HtmlReportFactModel> WhatWasChecked,
    IReadOnlyList<HtmlReportFactModel> RunParameters,
    IReadOnlyList<HtmlReportCardModel> OutcomeCards,
    IReadOnlyList<HtmlReportProblemModel> Problems,
    IReadOnlyList<HtmlReportMaterialModel> Materials,
    IReadOnlyList<string> Recommendations,
    string FooterNote);

internal sealed record HtmlReportHeroModel(
    string Eyebrow,
    string Headline,
    string Summary,
    string StatusBadge,
    string StatusBadgeCssClass,
    string RiskBadge,
    string RiskBadgeCssClass,
    string RunId,
    double SuccessPercent,
    int PassedItems,
    int FailedItems,
    IReadOnlyList<HtmlReportFactModel> Facts);

internal sealed record HtmlReportFactModel(string Label, string Value, string? Note = null);

internal sealed record HtmlReportCardModel(string Title, string Value, string Note, string ToneCssClass = "tone-neutral");

internal sealed record HtmlReportProblemModel(
    string Severity,
    string Where,
    string WhatFailed,
    string Occurrences,
    string Impact,
    string Message);

internal sealed record HtmlReportMaterialModel(string Label, string Path, string Note);
