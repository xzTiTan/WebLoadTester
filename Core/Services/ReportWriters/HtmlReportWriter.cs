using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public class HtmlReportWriter
{
    private readonly IArtifactStore _artifactStore;

    public HtmlReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    public Task<string> WriteAsync(TestReport report, string runId)
    {
        var html = BuildHtml(report);
        return _artifactStore.SaveHtmlReportAsync(runId, html);
    }

    private static string BuildHtml(TestReport report)
    {
        var failed = report.Results.Where(x => !x.Success).ToList();
        var recommendations = BuildRecommendations(report, failed);
        var page = BuildPageModel(report, failed, recommendations);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='ru'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
        sb.AppendLine($"<title>{Escape(page.DocumentTitle)} - {Escape(report.ModuleName)}</title>");
        sb.AppendLine("""
<style>
:root{
  --bg:#eef3f9;
  --panel:#ffffff;
  --panel-soft:#f7f9fc;
  --panel-strong:#f2f6fb;
  --line:#d8e0eb;
  --text:#152033;
  --muted:#607086;
  --ok:#1f7a49;
  --ok-soft:#e9f6ee;
  --fail:#b42318;
  --fail-soft:#fdecec;
  --warn:#a86212;
  --warn-soft:#fff1df;
  --accent:#0f6cbd;
  --accent-soft:#e8f1fb;
  --shadow:0 14px 36px rgba(15,32,58,.08)
}
*{box-sizing:border-box}
body{
  margin:0;
  background:linear-gradient(180deg,#f5f8fc 0%,#eef3f9 100%);
  color:var(--text);
  font:14px/1.55 Segoe UI,Arial,sans-serif;
  padding:24px
}
.page{max-width:1280px;margin:0 auto}
.hero,.sec,.card,.summary-band{background:var(--panel);border:1px solid var(--line);border-radius:20px;box-shadow:var(--shadow)}
.hero{padding:28px;margin-bottom:18px;overflow:hidden;position:relative}
.hero::before{
  content:"";
  position:absolute;
  inset:0 0 auto 0;
  height:160px;
  background:
    radial-gradient(circle at top right,rgba(15,108,189,.16),transparent 55%),
    radial-gradient(circle at top left,rgba(31,122,73,.12),transparent 45%);
  pointer-events:none
}
.eyebrow{
  display:inline-flex;
  padding:6px 12px;
  border-radius:999px;
  border:1px solid var(--line);
  background:var(--accent-soft);
  color:var(--accent);
  font-size:12px;
  font-weight:700;
  text-transform:uppercase;
  letter-spacing:.04em
}
.hero-grid,.section-head,.hero-facts,.cards,.facts,.artifacts,.gallery,.diag,.summary-grid,.inline-cards,.problem-grid,.timeline{display:grid;gap:14px}
.hero-grid{grid-template-columns:minmax(0,1.7fr) minmax(300px,.95fr);align-items:start}
.hero-grid>*,
.section-head>*,
.hero-facts>*,
.cards>*,
.facts>*,
.artifacts>*,
.gallery>*,
.diag>*,
.summary-grid>*,
.inline-cards>*,
.problem-grid>*,
.timeline>*{min-width:0}
.hero h1{margin:14px 0 10px;font-size:40px;line-height:1.05}
.lead{max-width:820px;font-size:16px;color:#32445d}
.badge-row,.pill-row,.meta-row{display:flex;gap:8px;flex-wrap:wrap;margin-top:14px}
.badge,.pill,.status-chip{
  display:inline-flex;
  align-items:center;
  padding:6px 12px;
  border-radius:999px;
  border:1px solid var(--line);
  background:var(--panel-soft);
  font-weight:700;
  max-width:100%;
  white-space:normal;
  overflow-wrap:anywhere;
  word-break:break-word
}
.status-chip{padding:5px 10px;font-size:12px}
.b-ok,.severity-pass{color:var(--ok);background:var(--ok-soft)}
.b-fail,.severity-fail{color:var(--fail);background:var(--fail-soft)}
.b-warn,.severity-warn{color:var(--warn);background:var(--warn-soft)}
.b-low,.severity-na{color:var(--accent);background:var(--accent-soft)}
.b-mid{color:var(--warn);background:var(--warn-soft)}
.b-high{color:var(--fail);background:var(--fail-soft)}
.hero-side{
  padding:18px;
  border-radius:18px;
  background:rgba(255,255,255,.82);
  border:1px solid rgba(216,224,235,.9);
  backdrop-filter:blur(4px)
}
.hero-side h2{margin:0 0 12px;font-size:18px}
.bar{height:14px;border-radius:999px;background:#e4ebf4;overflow:hidden}
.bar>span{display:block;height:100%;background:linear-gradient(90deg,var(--accent),#66a3e5)}
.bar.ok>span{background:linear-gradient(90deg,#1f7a49,#65c18c)}
.bar.fail>span{background:linear-gradient(90deg,#b42318,#ec776f)}
.bar.warn>span{background:linear-gradient(90deg,#a86212,#f1b05f)}
.cap{display:flex;justify-content:space-between;font-size:12px;color:var(--muted);margin-top:8px;gap:10px;flex-wrap:wrap}
.hero-facts{grid-template-columns:repeat(auto-fit,minmax(180px,1fr));margin-top:18px}
.summary-band{padding:20px 22px;margin-bottom:18px;background:linear-gradient(180deg,#ffffff 0%,#f8fbff 100%)}
.summary-grid{grid-template-columns:minmax(0,1.3fr) minmax(260px,.7fr);align-items:start}
.summary-band h2{margin:0 0 8px;font-size:24px}
.summary-band p{margin:0;font-size:15px;color:#32445d}
.summary-note{padding:16px;border-radius:16px;background:var(--panel-soft);border:1px solid var(--line)}
.summary-note strong{display:block;margin-bottom:6px}
.sec{padding:20px 22px;margin-bottom:18px}
.section-head{grid-template-columns:minmax(0,1fr) auto;align-items:end;margin-bottom:14px}
.section-head h2{margin:0;font-size:24px}
.section-head .small{max-width:420px;text-align:right}
.cards,.inline-cards{grid-template-columns:repeat(auto-fit,minmax(210px,1fr))}
.facts,.diag,.problem-grid{grid-template-columns:repeat(auto-fit,minmax(240px,1fr))}
.card,.fact{padding:16px}
.card{border-radius:18px;background:var(--panel)}
.card .n{font-size:30px;font-weight:800;line-height:1.1;margin-bottom:6px;overflow-wrap:anywhere;word-break:break-word}
.card h3{margin:0 0 8px;font-size:17px}
.fact{border:1px solid var(--line);border-radius:16px;background:var(--panel-soft)}
.fact strong{display:block;margin-bottom:6px}
.k{display:block;color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.04em}
.v{display:block;font-weight:700;font-size:16px;word-break:break-word;margin-top:4px}
.tone-success{background:linear-gradient(180deg,#f4fbf6 0%,#ffffff 100%)}
.tone-warning{background:linear-gradient(180deg,#fff8ef 0%,#ffffff 100%)}
.tone-danger{background:linear-gradient(180deg,#fff4f3 0%,#ffffff 100%)}
.tone-neutral{background:linear-gradient(180deg,#f8fafc 0%,#ffffff 100%)}
.tone-accent{background:linear-gradient(180deg,#eef5ff 0%,#ffffff 100%)}
table{width:100%;border-collapse:collapse;table-layout:fixed}
th,td{padding:11px 12px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left;overflow-wrap:anywhere;word-break:break-word}
th{background:var(--panel-soft);color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.04em}
tr:last-child td{border-bottom:none}
.ok{color:var(--ok);font-weight:700}
.fail{color:var(--fail);font-weight:700}
.warn{color:var(--warn);font-weight:700}
.artifacts{grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}
.artifacts a{
  display:block;
  padding:14px 16px;
  border:1px solid var(--line);
  border-radius:16px;
  background:var(--panel-soft);
  text-decoration:none;
  color:inherit
}
.artifacts a strong{display:block;margin-bottom:4px}
.gallery{grid-template-columns:repeat(auto-fit,minmax(250px,1fr))}
.shot{
  border:1px solid var(--line);
  border-radius:16px;
  overflow:hidden;
  background:var(--panel-soft);
  display:flex;
  flex-direction:column
}
.shot img{display:block;width:100%;height:200px;object-fit:cover;background:#dfe6f1}
.shot .body{padding:12px 14px}
.shot .body strong{display:block;margin-bottom:6px}
.shot.empty-shot{justify-content:center;min-height:240px}
.shot.empty-shot .body{padding:18px}
.timeline{grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}
.timeline-step{
  padding:16px;
  border-radius:18px;
  border:1px solid var(--line);
  background:var(--panel-soft)
}
.timeline-step h3{margin:0 0 8px;font-size:18px}
.group-panel{
  border:1px solid var(--line);
  border-radius:18px;
  padding:16px;
  background:linear-gradient(180deg,#ffffff 0%,#f8fafc 100%)
}
.group-panel h3{margin:0 0 8px;font-size:20px}
.callout{
  padding:16px;
  border-radius:16px;
  border:1px solid var(--line);
  background:var(--panel-soft)
}
.callout strong{display:block;margin-bottom:6px}
.small{font-size:12px;color:var(--muted)}
.lead,.small,.summary-band p,.summary-note,.callout,.timeline-step,.group-panel,.hero-side,.card,.fact,.empty,h1,h2,h3,p,li,strong,a{overflow-wrap:anywhere;word-break:break-word}
.empty{
  padding:16px;
  border:1px dashed var(--line);
  border-radius:16px;
  background:var(--panel-soft);
  color:var(--muted)
}
.mono{font-family:Consolas,'Courier New',monospace;white-space:normal;overflow-wrap:anywhere;word-break:break-all}
.list{margin:0;padding-left:20px}
.footer{margin-top:8px;text-align:right}
.section-stack{display:grid;gap:14px}
.divider{height:1px;background:var(--line);margin:14px 0}
.status-line{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
.status-line>*{min-width:0}
.status-line strong{font-size:15px}
@media (max-width:900px){
  body{padding:14px}
  .hero-grid,.summary-grid{grid-template-columns:1fr}
  .section-head{grid-template-columns:1fr}
  .section-head .small{text-align:left}
  .hero h1{font-size:30px}
}
@media print{
  body{padding:0;background:#fff}
  .hero,.sec,.card,.summary-band{box-shadow:none}
  .hero::before{display:none}
  a{text-decoration:none;color:inherit}
}
</style>
""");
        sb.AppendLine("</head><body><div class='page'>");
        RenderHero(page.Hero, sb);
        RenderExecutiveSummary(page.ExecutiveSummary, report, sb);
        RenderFactsSection("Общая информация о запуске", "Базовые сведения о текущем запуске, чтобы быстро понять контекст результата.", page.RunOverview, sb);
        RenderCardsSection("Ключевые показатели", "Главные показатели запуска, которые стоит увидеть в первые секунды.", page.SummaryCards, sb);
        RenderFactsSection("Что проверялось", "Коротко о типе проверки, её цели и объекте запуска.", page.WhatWasChecked, sb);
        RenderFactsSection("Параметры запуска", "Параметры профиля и ключевые настройки модуля для этого прогона.", page.RunParameters, sb);
        RenderCardsSection("Что получилось", "Итог запуска, сводка по ошибкам и основным метрикам.", page.OutcomeCards, sb);
        RenderProblemsSection(page.Problems, sb);
        RenderMaterialsSection(page.Materials, sb);
        AppendHttpPerformanceSection(report, sb); AppendSecurityChecksSection(report, sb); AppendHttpFunctionalSection(report, sb); AppendAssetsSection(report, sb); AppendDiagnosticsSection(report, sb); AppendAvailabilitySection(report, sb); AppendPreflightSection(report, sb); AppendSnapshotSection(report, sb); AppendTimingSection(report, sb); AppendScenarioSection(report, sb);
        if (report.Metrics.TopSlow.Count > 0)
        {
            var maxSlow = Math.Max(1d, report.Metrics.TopSlow.Max(x => x.DurationMs));
            AppendSectionHeader("Самые медленные элементы", "Подсказка, какие проверки заняли больше всего времени.", sb);
            sb.AppendLine("<table><tr><th>Элемент</th><th>Тип</th><th>Статус</th><th>Длительность</th><th>Нагрузка</th></tr>");
            foreach (var item in report.Metrics.TopSlow)
            {
                sb.AppendLine($"<tr><td>{Escape(GetResultName(item))}</td><td>{Escape(TranslateResultKind(item.Kind))}</td><td class='{(item.Success ? "ok" : "fail")}'>{(item.Success ? "Успешно" : "Ошибка")}</td><td>{Escape(FormatDuration(item.DurationMs))}</td><td>{RenderBar(item.DurationMs, maxSlow, FormatDuration(item.DurationMs), item.Success ? "ok" : "fail")}</td></tr>");
            }
            sb.AppendLine("</table></section>");
        }
        AppendSectionHeader("Технические детали запуска", "Полная таблица результатов для диагностики и трассировки.", sb);
        sb.AppendLine("<table><tr><th>Тип</th><th>Элемент</th><th>Статус</th><th>Длительность</th><th>Воркер / итерация</th><th>Подробности</th></tr>");
        foreach (var item in report.Results)
        {
            sb.AppendLine($"<tr><td>{Escape(TranslateResultKind(item.Kind))}</td><td>{Escape(GetResultName(item))}</td><td class='{(item.Success ? "ok" : "fail")}'>{(item.Success ? "Успешно" : "Ошибка")}</td><td>{Escape(FormatDuration(item.DurationMs))}</td><td>{item.WorkerId} / {item.IterationIndex}</td><td>{Escape(DescribeResult(item))}</td></tr>");
        }
        sb.AppendLine("</table></section>");
        RenderRecommendationsSection(page.Recommendations, sb);
        sb.AppendLine($"<div class='small footer'>{Escape(page.FooterNote)} <span class='mono'>{Escape(report.RunId)}</span></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static HtmlReportPageModel BuildPageModel(TestReport report, IReadOnlyList<ResultBase> failed, IReadOnlyList<string> recommendations)
    {
        var title = string.IsNullOrWhiteSpace(report.FinalName) ? report.TestName : report.FinalName;
        var passed = Math.Max(0, report.Metrics.TotalItems - report.Metrics.FailedItems);
        var successPercent = report.Metrics.TotalItems > 0
            ? passed * 100d / report.Metrics.TotalItems
            : report.Status == TestStatus.Success ? 100d : 0d;
        var problemLevel = GetSeverity(report.Metrics.FailedItems, report.Metrics.TotalItems);
        var problems = BuildProblemModels(report, failed);

        var summaryCards = BuildSummaryCards(report, failed, successPercent);
        var outcomeCards = BuildOutcomeCards(report, failed, passed);
        var hero = new HtmlReportHeroModel(
            "HTML-отчёт одного запуска",
            GetHeroHeadline(report.Status),
            BuildHeroSummary(report, title, failed),
            FormatStatus(report.Status),
            GetStatusCssClass(report.Status),
            problemLevel.Label,
            problemLevel.CssClass,
            report.RunId,
            successPercent,
            passed,
            failed.Count,
            new List<HtmlReportFactModel>
            {
                new("Запуск", title, "Имя конфигурации или сценария запуска."),
                new("Объект", DescribeTarget(report), "Основная цель, URL, host или сценарий этого прогона."),
                new("Начало", FormatDate(report.StartedAt), "Время старта по локальному часовому поясу."),
                new("Длительность", FormatDuration(report.Metrics.TotalDurationMs), "Суммарная длительность зафиксированных результатов.")
            });

        return new HtmlReportPageModel(
            title,
            hero,
            BuildExecutiveSummary(report, title, failed, passed),
            BuildRunOverviewFacts(report),
            summaryCards,
            BuildWhatWasCheckedFacts(report),
            BuildRunParameterFacts(report),
            outcomeCards,
            problems,
            BuildMaterialModels(report),
            recommendations.ToList(),
            "HTML-страница построена из текущих данных запуска и связана с report.json. RunId:");
    }

    private static void RenderHero(HtmlReportHeroModel hero, StringBuilder sb)
    {
        sb.AppendLine("<section class='hero'>");
        sb.AppendLine("<div class='hero-grid'>");
        sb.AppendLine("<div>");
        sb.AppendLine($"<div class='eyebrow'>{Escape(hero.Eyebrow)}</div>");
        sb.AppendLine($"<h1>{Escape(hero.Headline)}</h1>");
        sb.AppendLine($"<div class='lead'>{Escape(hero.Summary)}</div>");
        sb.AppendLine($"<div class='badge-row'><span class='badge {hero.StatusBadgeCssClass}'>{Escape(hero.StatusBadge)}</span><span class='badge {hero.RiskBadgeCssClass}'>{Escape(hero.RiskBadge)}</span><span class='badge mono'>RunId: {Escape(hero.RunId)}</span></div>");
        sb.AppendLine("<div class='hero-facts'>");
        foreach (var fact in hero.Facts)
        {
            AppendFact(fact, sb);
        }
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<aside class='hero-side'>");
        sb.AppendLine("<h2>Краткий итог</h2>");
        sb.AppendLine($"<div class='bar {(hero.FailedItems == 0 ? "ok" : "warn")}'><span style='width:{ClampPercent(hero.SuccessPercent):F1}%'></span></div>");
        sb.AppendLine($"<div class='cap'><span>Успешно: {hero.PassedItems}</span><span>Ошибки: {hero.FailedItems}</span><span>{hero.SuccessPercent:F1}% без ошибок</span></div>");
        sb.AppendLine("<div class='section-stack' style='margin-top:16px'>");
        AppendFact(new HtmlReportFactModel("Статус", hero.StatusBadge), sb);
        AppendFact(new HtmlReportFactModel("Оценка риска", hero.RiskBadge), sb);
        AppendFact(new HtmlReportFactModel("Элементов результата", (hero.PassedItems + hero.FailedItems).ToString(CultureInfo.InvariantCulture), "Количество записей, попавших в итоговый отчёт."), sb);
        sb.AppendLine("</div>");
        sb.AppendLine("</aside>");
        sb.AppendLine("</div>");
        sb.AppendLine("</section>");
    }

    private static void RenderExecutiveSummary(string summary, TestReport report, StringBuilder sb)
    {
        sb.AppendLine("<section class='summary-band'>");
        sb.AppendLine("<div class='summary-grid'>");
        sb.AppendLine("<div>");
        sb.AppendLine("<h2>Краткий вывод</h2>");
        sb.AppendLine($"<p>{Escape(summary)}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='summary-note'>");
        sb.AppendLine("<strong>На что смотреть дальше</strong>");
        sb.AppendLine($"<div class='small'>{Escape(GetFollowUpHint(report))}</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</section>");
    }

    private static void RenderCardsSection(string title, string note, IReadOnlyList<HtmlReportCardModel> cards, StringBuilder sb)
    {
        AppendSectionHeader(title, note, sb);
        sb.AppendLine("<div class='cards'>");
        foreach (var card in cards)
        {
            sb.AppendLine($"<div class='card {Escape(card.ToneCssClass)}'><div class='n'>{Escape(card.Value)}</div><h3>{Escape(card.Title)}</h3><div class='small'>{Escape(card.Note)}</div></div>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void RenderFactsSection(string title, string note, IReadOnlyList<HtmlReportFactModel> facts, StringBuilder sb)
    {
        AppendSectionHeader(title, note, sb);
        if (facts.Count == 0)
        {
            sb.AppendLine("<div class='empty'>Для этого запуска не удалось собрать дополнительные данные.</div></section>");
            return;
        }

        sb.AppendLine("<div class='facts'>");
        foreach (var fact in facts)
        {
            AppendFact(fact, sb);
        }
        sb.AppendLine("</div></section>");
    }

    private static void RenderProblemsSection(IReadOnlyList<HtmlReportProblemModel> problems, StringBuilder sb)
    {
        AppendSectionHeader("Ошибки и проблемные места", "Здесь собраны наиболее важные сбои и проблемные элементы текущего запуска.", sb);
        if (problems.Count == 0)
        {
            sb.AppendLine("<div class='empty'>Проблемных элементов не зафиксировано. Запуск прошёл без ошибок на уровне итогового отчёта.</div></section>");
            return;
        }

        sb.AppendLine("<table><tr><th>Серьёзность</th><th>Где возникло</th><th>Что не получилось</th><th>Повторяемость</th><th>Почему это важно</th><th>Детали</th></tr>");
        foreach (var problem in problems)
        {
            sb.AppendLine($"<tr><td>{Escape(problem.Severity)}</td><td>{Escape(problem.Where)}</td><td>{Escape(problem.WhatFailed)}</td><td>{Escape(problem.Occurrences)}</td><td>{Escape(problem.Impact)}</td><td>{Escape(problem.Message)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void RenderMaterialsSection(IReadOnlyList<HtmlReportMaterialModel> materials, StringBuilder sb)
    {
        AppendSectionHeader("Материалы запуска", "Файлы и артефакты, которые можно открыть после завершения прогона.", sb);
        if (materials.Count == 0)
        {
            sb.AppendLine("<div class='empty'>Материалы запуска не были зарегистрированы.</div></section>");
            return;
        }

        sb.AppendLine("<div class='artifacts'>");
        foreach (var material in materials)
        {
            sb.AppendLine($"<a href='{Escape(material.Path)}'><strong>{Escape(material.Label)}</strong><div class='small'>{Escape(material.Note)}</div><div class='small mono'>{Escape(material.Path)}</div></a>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void RenderRecommendationsSection(IReadOnlyList<string> recommendations, StringBuilder sb)
    {
        var normalized = recommendations
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return;
        }

        AppendSectionHeader("Рекомендации и следующие шаги", "Куда смотреть дальше и какие действия имеют смысл после этого запуска.", sb);
        sb.AppendLine("<ul class='list'>");
        foreach (var recommendation in normalized)
        {
            sb.AppendLine($"<li>{Escape(recommendation)}</li>");
        }
        sb.AppendLine("</ul></section>");
    }

    private static void AppendFact(HtmlReportFactModel fact, StringBuilder sb)
    {
        sb.AppendLine("<div class='fact'>");
        sb.AppendLine($"<span class='k'>{Escape(fact.Label)}</span>");
        sb.AppendLine($"<span class='v'>{Escape(fact.Value)}</span>");
        if (!string.IsNullOrWhiteSpace(fact.Note))
        {
            sb.AppendLine($"<div class='small'>{Escape(fact.Note)}</div>");
        }
        sb.AppendLine("</div>");
    }

    private static IReadOnlyList<HtmlReportCardModel> BuildSummaryCards(TestReport report, IReadOnlyList<ResultBase> failed, double successPercent)
    {
        var errorBreakdown = report.Metrics.ErrorBreakdown.Count == 0
            ? "Классы ошибок не зафиксированы."
            : string.Join(", ", report.Metrics.ErrorBreakdown.OrderByDescending(x => x.Value).Select(x => $"{TranslateErrorKind(x.Key)}: {x.Value}"));

        return new List<HtmlReportCardModel>
        {
            new("Статус запуска", FormatStatus(report.Status), "Итоговое состояние по завершении модуля.", GetToneCss(report.Status, report.Metrics.FailedItems)),
            new("Объект проверки", DescribeTarget(report), "Куда именно был направлен этот запуск.", "tone-accent"),
            new("Успешность", $"{successPercent:F1}%", "Доля элементов результата без ошибок.", failed.Count == 0 ? "tone-success" : "tone-warning"),
            new("Проблемы", report.Metrics.FailedItems.ToString(CultureInfo.InvariantCulture), errorBreakdown, failed.Count == 0 ? "tone-success" : "tone-danger")
        };
    }

    private static IReadOnlyList<HtmlReportCardModel> BuildOutcomeCards(TestReport report, IReadOnlyList<ResultBase> failed, int passed)
    {
        var failureComment = failed.Count == 0
            ? "Ни один зафиксированный элемент результата не завершился ошибкой."
            : $"Зафиксировано {failed.Count} проблемных элементов. Подробности см. в блоке ошибок ниже.";

        return new List<HtmlReportCardModel>
        {
            new("Проверено элементов", report.Metrics.TotalItems.ToString(CultureInfo.InvariantCulture), "Общее число результатов, попавших в отчёт.", "tone-neutral"),
            new("Успешных элементов", passed.ToString(CultureInfo.InvariantCulture), "Количество результатов без ошибок.", failed.Count == 0 ? "tone-success" : "tone-neutral"),
            new("Проблемных элементов", report.Metrics.FailedItems.ToString(CultureInfo.InvariantCulture), failureComment, failed.Count == 0 ? "tone-success" : "tone-danger"),
            new("Средняя / P95", $"{FormatDuration(report.Metrics.AverageMs)} / {FormatDuration(report.Metrics.P95Ms)}", DescribeOutcomeFocus(report), report.Metrics.P95Ms > 0 ? "tone-warning" : "tone-neutral")
        };
    }

    private static IReadOnlyList<HtmlReportFactModel> BuildRunOverviewFacts(TestReport report)
    {
        return new List<HtmlReportFactModel>
        {
            new("Вид тестирования", report.ModuleName, "Конкретный модуль, сформировавший HTML-отчёт."),
            new("Название запуска", string.IsNullOrWhiteSpace(report.FinalName) ? report.TestName : report.FinalName, "Имя конфигурации или сценария, под которым выполнялся прогон."),
            new("Семейство", FormatFamily(report.Family), "К какой группе проверок относится модуль."),
            new("Объект проверки", DescribeTarget(report), "Главный URL, host, endpoint или сценарий текущего запуска."),
            new("Начало", FormatDate(report.StartedAt), "Время старта по локальному часовому поясу."),
            new("Завершение", FormatDate(report.FinishedAt), "Время окончания, если оно было зафиксировано."),
            new("Длительность", FormatDuration(report.Metrics.TotalDurationMs), "Полная длительность зафиксированных результатов."),
            new("Короткий ID запуска", ShortRunId(report.RunId), "Сокращённый идентификатор для удобной ссылки на конкретный прогон.")
        };
    }

    private static IReadOnlyList<HtmlReportFactModel> BuildWhatWasCheckedFacts(TestReport report)
    {
        var facts = new List<HtmlReportFactModel>
        {
            new("Назначение запуска", DescribeModulePurpose(report), "Зачем выполнялся этот тип проверки и что он должен подтвердить."),
            new("Что именно проверялось", DescribeTarget(report), DescribeModuleFocus(report)),
            new("Что входило в охват", DescribeScope(report), "Сколько шагов, endpoint-ов, портов, профилей или ресурсов было включено в запуск."),
            new("Что считается результатом", DescribeSuccessCriteria(report), "Критерий, по которому этот запуск читается как успешный или проблемный.")
        };

        facts.Add(new HtmlReportFactModel("Среда запуска", string.IsNullOrWhiteSpace(report.OsDescription) ? "Не определена" : report.OsDescription, $"Версия приложения: {FormatOrFallback(report.AppVersion)}."));
        return facts;
    }

    private static IReadOnlyList<HtmlReportFactModel> BuildRunParameterFacts(TestReport report)
    {
        var facts = new List<HtmlReportFactModel>
        {
            new("Режим запуска", FormatRunMode(report.ProfileSnapshot.Mode), "Как был ограничен запуск: по итерациям или по времени."),
            new("Параллельность", report.ProfileSnapshot.Parallelism.ToString(CultureInfo.InvariantCulture), "Количество одновременно работающих воркеров."),
            new("Итерации", FormatNumericOrDash(report.ProfileSnapshot.Iterations), "Используется в режиме запуска по числу повторений."),
            new("Длительность", report.ProfileSnapshot.DurationSeconds > 0 ? $"{report.ProfileSnapshot.DurationSeconds} с" : "не задана", "Используется в режиме запуска по времени."),
            new("Таймаут операции", $"{report.ProfileSnapshot.TimeoutSeconds} с", "Ограничение на одну итерацию или операцию."),
            new("Пауза между итерациями", $"{report.ProfileSnapshot.PauseBetweenIterationsMs} мс", "Помогает регулировать интенсивность запуска."),
            new("Headless", FormatBoolRu(report.ProfileSnapshot.Headless), "Показывались ли окна браузера при UI-проверках."),
            new("Политика скриншотов", FormatScreenshotsPolicy(report.ProfileSnapshot.ScreenshotsPolicy), "Когда программа сохраняла скриншоты."),
            new("JSON-отчёт", "Да", "Обязательный машинно-читаемый отчёт этого запуска."),
            new("HTML-отчёт", FormatBoolRu(report.ProfileSnapshot.HtmlReportEnabled), "Для новых запусков HTML-отчёт обязателен; значение сохраняется для совместимости с историей."),
            new("Telegram", FormatBoolRu(report.ProfileSnapshot.TelegramEnabled), "Были ли разрешены уведомления для этого запуска."),
            new("Preflight", FormatBoolRu(report.ProfileSnapshot.PreflightEnabled), "Выполнялись ли предварительные проверки окружения.")
        };

        foreach (var pair in DescribeModuleSettings(report))
        {
            facts.Add(new HtmlReportFactModel(pair.Key, pair.Value));
        }

        return facts.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
    }

    private static IReadOnlyList<HtmlReportMaterialModel> BuildMaterialModels(TestReport report)
    {
        var materials = BuildArtifactPaths(report)
            .Select(path => new HtmlReportMaterialModel(GuessArtifactLabel(path), path, GetArtifactNote(path)))
            .ToList();
        materials.Insert(0, new HtmlReportMaterialModel("Каталог запуска", "./", "Папка текущего запуска со всеми сохранёнными материалами."));
        return materials;
    }

    private static string BuildHeroSummary(TestReport report, string title, IReadOnlyList<ResultBase> failed)
    {
        var outcome = failed.Count == 0
            ? $"Запуск завершился без ошибок. Зафиксировано {report.Metrics.TotalItems} элементов результата."
            : $"Запуск завершился с {failed.Count} проблемными элементами из {report.Metrics.TotalItems}.";

        var parts = new List<string>
        {
            $"Выполнен запуск «{title}».",
            $"Проверка: {report.ModuleName}.",
            $"Объект: {DescribeTarget(report)}.",
            outcome
        };

        var scope = DescribeScope(report);
        if (!string.IsNullOrWhiteSpace(scope))
        {
            parts.Add($"Охват запуска: {scope}.");
        }

        return string.Join(" ", parts);
    }

    private static string BuildExecutiveSummary(TestReport report, string title, IReadOnlyList<ResultBase> failed, int passed)
    {
        var outcome = report.Status switch
        {
            TestStatus.Success => "завершился успешно",
            TestStatus.Failed => "завершился с ошибками",
            TestStatus.Partial => "завершился частично",
            TestStatus.Canceled => "был отменён",
            TestStatus.Stopped => "был остановлен",
            _ => "завершился"
        };

        var scope = DescribeScope(report);
        var problemText = failed.Count == 0
            ? "Критичных проблем в итоговых результатах не зафиксировано."
            : $"Зафиксировано {failed.Count} проблемных результатов из {Math.Max(report.Metrics.TotalItems, failed.Count)}.";

        var scopeText = string.IsNullOrWhiteSpace(scope)
            ? string.Empty
            : $" В охват входило: {scope}.";

        return $"Запуск «{title}» ({report.ModuleName}) {outcome}. Проверка была направлена на {DescribeTarget(report)}.{scopeText} Успешно завершено {passed} результатов. {problemText}";
    }

    private static string GetHeroHeadline(TestStatus status) => status switch
    {
        TestStatus.Success => "Тест пройден успешно",
        TestStatus.Failed => "Тест завершился с ошибкой",
        TestStatus.Canceled => "Тест был отменён",
        TestStatus.Stopped => "Тест был остановлен",
        TestStatus.Partial => "Тест завершился частично",
        _ => "Тест завершён"
    };

    private static IReadOnlyList<HtmlReportProblemModel> BuildProblemModels(TestReport report, IReadOnlyList<ResultBase> failed)
    {
        return failed
            .GroupBy(item => new
            {
                item.Kind,
                Name = GetResultName(item),
                ErrorType = item.ErrorType ?? string.Empty,
                ErrorMessage = item.ErrorMessage ?? string.Empty
            })
            .OrderByDescending(group => GetProblemPriority(group.First()))
            .ThenByDescending(group => group.Count())
            .Take(10)
            .Select(group =>
            {
                var sample = group.First();
                return new HtmlReportProblemModel(
                    ResolveItemSeverity(sample.ErrorType, sample.ErrorMessage),
                    BuildProblemWhere(group),
                    string.IsNullOrWhiteSpace(group.Key.Name)
                        ? TranslateResultKind(group.Key.Kind)
                        : $"{TranslateResultKind(group.Key.Kind)}: {group.Key.Name}",
                    FormatOccurrences(group.Count()),
                    BuildProblemImpact(report.ModuleId, sample),
                    SummarizeProblemMessage(group));
            })
            .ToList();
    }

    private static string GetFollowUpHint(TestReport report)
    {
        return report.ModuleId switch
        {
            "ui.scenario" => "Сначала откройте блоки по воркеру и итерации, затем сравните проблемные шаги с секцией регрессионного сравнения.",
            "ui.snapshot" => "Сначала просмотрите неудачные карточки галереи, затем откройте успешные снимки для визуального сравнения.",
            "http.performance" => "Обратите внимание на endpoint-ы с высоким P95/P99 и на точки, где появились ошибки.",
            "http.functional" => "Сравните колонку «Ожидалось» с «Получено», затем проверьте повторяемость ошибок по endpoint-ам.",
            "net.diagnostics" => "Идите по цепочке сверху вниз: DNS, затем TCP, затем TLS, чтобы увидеть место разрыва.",
            "net.security" => "Начните с карточек уровня Fail и Warn: они дают самый быстрый путь к исправлениям.",
            _ => "Сначала смотрите общий итог и проблемные места, затем переходите к артефактам и модульной секции."
        };
    }

    private static string DescribeModuleFocus(TestReport report)
    {
        return report.ModuleId switch
        {
            "ui.scenario" => "Проверялась цепочка шагов сценария, корректность действий и итоговые скриншоты.",
            "ui.snapshot" => "Проверялись целевые страницы или элементы, которые нужно было снять и сохранить в виде снимков.",
            "http.performance" => "Проверялась скорость отклика endpoint-ов и наличие деградации под выбранным режимом запуска.",
            "http.functional" => "Проверялись ожидаемые HTTP-статусы, заголовки, содержимое ответа и JSON-условия.",
            "net.diagnostics" => "Проверялся путь до сервиса через DNS, TCP и TLS, чтобы локализовать точку сбоя.",
            "net.security" => "Проверялись безопасные заголовки и базовые настройки без атакующих действий.",
            "http.assets" => "Проверялись Web-ресурсы, их тип, размер и задержка загрузки.",
            "net.preflight" => "Проверялась готовность окружения и доступность вспомогательных зависимостей.",
            "net.availability" => "Проверялась базовая доступность HTTP или TCP-цели.",
            "ui.timing" => "Сравнивались профили загрузки страницы в Chromium и их тайминги.",
            _ => "Проверялся объект, указанный в настройках этого запуска."
        };
    }

    private static string DescribeSuccessCriteria(TestReport report)
    {
        return report.ModuleId switch
        {
            "ui.scenario" => "Успех означает, что сценарий проходит шаги без ошибок, а регрессионное сравнение не добавляет новых сбоев.",
            "ui.snapshot" => "Успех означает, что целевые снимки получены и сохранены без ошибок съёмки.",
            "http.performance" => "Успех означает отсутствие ошибок запросов и приемлемую задержку по основным endpoint-ам.",
            "http.functional" => "Успех означает, что endpoint-ы вернули ожидаемые ответы и проверки не дали ошибок.",
            "net.diagnostics" => "Успех означает, что DNS, TCP и TLS завершились без разрыва цепочки.",
            "net.security" => "Успех означает, что критичных и предупреждающих baseline-замечаний нет или они минимальны.",
            "http.assets" => "Успех означает, что ресурсы уложились в ограничения по типу, размеру и времени ответа.",
            "net.preflight" => "Успех означает, что окружение и базовые зависимости готовы к основному запуску.",
            "net.availability" => "Успех означает, что цель была доступна в рамках выбранной проверки.",
            "ui.timing" => "Успех означает, что страница открылась в выбранных профилях Chromium, а тайминги navigation, DOMContentLoaded и load удалось собрать.",
            _ => "Успех определяется отсутствием проблемных результатов в итоговом отчёте."
        };
    }

    private static string DescribeOutcomeFocus(TestReport report)
    {
        return report.ModuleId switch
        {
            "http.performance" => "Подходит для быстрой оценки средней скорости и хвоста медленных ответов.",
            "http.functional" => "Помогает увидеть, где контракт endpoint-а сработал нестабильно при повторах.",
            "net.diagnostics" => "Позволяет быстро оценить, на каком этапе диагностической цепочки появились потери.",
            "ui.scenario" => "Помогает понять, есть ли системные ошибки в шагах сценария и регрессия относительно baseline.",
            "ui.snapshot" => "Дает быстрый сигнал, удалось ли собрать визуальные материалы по целям.",
            "net.security" => "Показывает, насколько критичны найденные baseline-замечания.",
            _ => "Подходит для быстрой оценки стабильности прогона."
        };
    }

    private static string ShortRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return "n/a";
        }

        return runId.Length <= 8 ? runId : runId[..8];
    }

    private static string DescribeTarget(TestReport report)
    {
        return report.ModuleId switch
        {
            "http.performance" or "http.functional" => ReadString(report.ModuleSettingsSnapshot, "BaseUrl") ?? "HTTP-ресурс не указан",
            "http.assets" => $"{ReadArray(report.ModuleSettingsSnapshot, "Assets").Count} настроенных ресурса(ов)",
            "net.security" => ReadString(report.ModuleSettingsSnapshot, "Url") ?? "Целевой URL не указан",
            "net.diagnostics" => ReadString(report.ModuleSettingsSnapshot, "Hostname") ?? "Хост не указан",
            "net.availability" => ReadString(report.ModuleSettingsSnapshot, "Url")
                ?? (ReadString(report.ModuleSettingsSnapshot, "Host") is { } host ? $"{host}:{ReadInt(report.ModuleSettingsSnapshot, "Port")?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}" : "Цель не указана"),
            "net.preflight" => ReadString(report.ModuleSettingsSnapshot, "Target") ?? "Окружение и целевая точка запуска",
            "ui.snapshot" or "ui.timing" => DescribeUiTargets(report),
            "ui.scenario" => ReadString(report.ModuleSettingsSnapshot, "TargetUrl") ?? "UI-сценарий без явного TargetUrl",
            _ => "Объект проверки не определён"
        };
    }

    private static string DescribeScope(TestReport report)
    {
        return report.ModuleId switch
        {
            "http.performance" or "http.functional" => $"{ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Count} настроенных endpoint(ов)",
            "http.assets" => $"{ReadArray(report.ModuleSettingsSnapshot, "Assets").Count} ресурса(ов) для проверки",
            "net.diagnostics" => $"{ReadArray(report.ModuleSettingsSnapshot, "Ports").Count} порта(ов) и включённые DNS/TCP/TLS-проверки",
            "ui.snapshot" or "ui.timing" => $"{ReadArray(report.ModuleSettingsSnapshot, "Targets").Count} целевых профиля(ей)/страниц",
            "ui.scenario" => $"{ReadArray(report.ModuleSettingsSnapshot, "Steps").Count} шага(ов) сценария",
            _ => string.Empty
        };
    }

    private static string DescribeModulePurpose(TestReport report)
    {
        return report.ModuleId switch
        {
            "http.performance" => "Проверка скорости отклика и стабильности HTTP endpoint-ов.",
            "http.functional" => "Проверка контрактов endpoint-ов: статусы, заголовки, содержимое и JSON-условия.",
            "http.assets" => "Проверка доступности, размера, типа и задержки Web-ресурсов.",
            "net.diagnostics" => "Безопасная диагностика сетевых проблем на уровнях DNS, TCP и TLS.",
            "net.availability" => "Проверка, доступен ли целевой сервис по HTTP или TCP.",
            "net.preflight" => "Проверка готовности окружения и базовой сетевой доступности перед основным запуском.",
            "net.security" => "Проверка базовых безопасных настроек без атакующих действий.",
            "ui.snapshot" => "Снятие и фиксация визуальных состояний интерфейса.",
            "ui.timing" => "Сравнение профилей Chromium и таймингов загрузки страницы.",
            "ui.scenario" => "Пошаговое выполнение UI-сценария с фиксацией результатов и артефактов.",
            _ => "Назначение проверки не уточнено."
        };
    }

    private static string DescribeUiTargets(TestReport report)
    {
        var targets = ReadArray(report.ModuleSettingsSnapshot, "Targets");
        if (targets.Count == 0)
        {
            return "Целевые страницы не указаны";
        }

        var urls = targets
            .Select(x => ReadString(x, "Url"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2)
            .ToList();

        return urls.Count == 0
            ? $"{targets.Count} настроенных цели"
            : $"{targets.Count} цели, например: {string.Join(", ", urls)}";
    }

    private static string GetArtifactNote(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized is "." or "./") return "Папка текущего запуска со всеми отчётами, логами и вложенными артефактами.";
        if (normalized.EndsWith("report.json", StringComparison.OrdinalIgnoreCase)) return "Машиночитаемый источник отчётных данных текущего запуска.";
        if (normalized.EndsWith("report.html", StringComparison.OrdinalIgnoreCase)) return "Человекочитаемая HTML-страница этого запуска.";
        if (normalized.Contains("logs/run.log", StringComparison.OrdinalIgnoreCase)) return "Журнал выполнения с диагностическими сообщениями.";
        if (normalized.Contains("screenshots/", StringComparison.OrdinalIgnoreCase)) return "Визуальный артефакт, сохранённый во время выполнения.";
        return "Дополнительный артефакт, созданный модулем во время запуска.";
    }

    private static string GetToneCss(TestStatus status, int failedItems)
    {
        if (status == TestStatus.Success && failedItems == 0) return "tone-success";
        if (status == TestStatus.Failed || failedItems > 0) return "tone-danger";
        if (status is TestStatus.Canceled or TestStatus.Stopped or TestStatus.Partial) return "tone-warning";
        return "tone-neutral";
    }

    private static void AppendHttpPerformanceSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.performance", StringComparison.OrdinalIgnoreCase)) return;
        var groups = report.Results.OfType<EndpointResult>().GroupBy(x => string.IsNullOrWhiteSpace(x.Name) ? "(endpoint)" : x.Name).Select(g =>
        {
            var samples = g.Select(x => x.LatencyMs > 0 ? x.LatencyMs : x.DurationMs).OrderBy(x => x).ToList();
            return new
            {
                g.Key,
                Requests = g.Count(),
                Success = g.Count(x => x.Success),
                Failures = g.Count(x => !x.Success),
                Avg = samples.Count == 0 ? 0 : samples.Average(),
                Min = samples.Count == 0 ? 0 : samples.First(),
                Max = samples.Count == 0 ? 0 : samples.Last(),
                P95 = Percentile(samples, 0.95),
                P99 = Percentile(samples, 0.99),
                Message = g.FirstOrDefault(x => !x.Success)?.ErrorMessage
            };
        }).OrderByDescending(x => x.Failures).ThenByDescending(x => x.P95).ToList();
        if (groups.Count == 0) return;
        var maxLatency = Math.Max(1d, groups.Max(x => x.Max));
        var slowest = groups.OrderByDescending(x => x.P95 > 0 ? x.P95 : x.Max).First();

        AppendSectionHeader("Результаты по endpoint-ам", "Показывает, какие точки выдержали нагрузку, а где появилась деградация по скорости или ошибкам.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{groups.Count}</div><h3>Endpoint-ов в отчёте</h3><div class='small'>Сколько отдельных точек попало в итоговую сводку.</div></div>");
        sb.AppendLine($"<div class='card {(groups.All(x => x.Failures == 0) ? "tone-success" : "tone-danger")}'><div class='n'>{groups.Sum(x => x.Requests)}</div><h3>Всего запросов</h3><div class='small'>Сумма всех измерений по endpoint-ам в этом запуске.</div></div>");
        sb.AppendLine($"<div class='card {(slowest.Failures > 0 ? "tone-danger" : "tone-warning")}'><div class='n'>{Escape(FormatDuration(slowest.P95 > 0 ? slowest.P95 : slowest.Max))}</div><h3>Самая проблемная точка</h3><div class='small'>{Escape($"{slowest.Key}: P95/Max выше остальных endpoint-ов.")}</div></div>");
        sb.AppendLine($"<div class='card {(groups.Any(x => x.Failures > 0) ? "tone-danger" : "tone-success")}'><div class='n'>{groups.Sum(x => x.Failures)}</div><h3>Ошибочных ответов</h3><div class='small'>Сколько запросов завершилось неуспешно по всем endpoint-ам.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table><tr><th>Endpoint</th><th>Запросов</th><th>Успешно / ошибки</th><th>Min / Avg / Max</th><th>P95 / P99</th><th>Профиль задержки</th><th>Что это значит</th></tr>");
        foreach (var row in groups)
        {
            var interpretation = row.Failures > 0
                ? $"Есть ошибки: {row.Failures} из {row.Requests}. {(string.IsNullOrWhiteSpace(row.Message) ? "Нужна проверка контракта endpoint-а и логов." : row.Message)}"
                : row.P95 > row.Avg * 1.8d && row.Avg > 0
                    ? "Ошибок нет, но есть заметный хвост медленных ответов."
                    : "Точка выглядит стабильной по текущему запуску.";
            sb.AppendLine($"<tr><td><strong>{Escape(row.Key)}</strong></td><td>{row.Requests}</td><td class='{(row.Failures == 0 ? "ok" : "fail")}'>{row.Success} / {row.Failures}</td><td>{Escape($"{FormatDuration(row.Min)} / {FormatDuration(row.Avg)} / {FormatDuration(row.Max)}")}</td><td>{Escape($"{FormatDuration(row.P95)} / {FormatDuration(row.P99)}")}</td><td>{RenderBar(row.Avg, maxLatency, FormatDuration(row.Avg), row.Failures == 0 ? "ok" : "warn")}</td><td>{Escape(interpretation)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendSecurityChecksSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.security", StringComparison.OrdinalIgnoreCase)) return;
        var checks = report.Results.OfType<CheckResult>().ToList();
        if (checks.Count == 0) return;
        var failCount = checks.Count(x => string.Equals(x.Severity, "Fail", StringComparison.OrdinalIgnoreCase));
        var warnCount = checks.Count(x => string.Equals(x.Severity, "Warn", StringComparison.OrdinalIgnoreCase));
        var passCount = checks.Count(x => string.Equals(x.Severity, "Pass", StringComparison.OrdinalIgnoreCase));

        AppendSectionHeader("Результаты проверки безопасности", "Показывает найденные замечания, их серьёзность и то, что стоит исправить в первую очередь.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card {(failCount > 0 ? "tone-danger" : "tone-success")}'><div class='n'>{failCount}</div><h3>Критичных замечаний</h3><div class='small'>Замечания уровня Fail требуют внимания в первую очередь.</div></div>");
        sb.AppendLine($"<div class='card {(warnCount > 0 ? "tone-warning" : "tone-success")}'><div class='n'>{warnCount}</div><h3>Предупреждений</h3><div class='small'>Проблемы уровня Warn не блокируют всё, но ухудшают базовую безопасность.</div></div>");
        sb.AppendLine($"<div class='card tone-success'><div class='n'>{passCount}</div><h3>Успешных проверок</h3><div class='small'>Какие базовые настройки уже выглядят корректно.</div></div>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{checks.Count}</div><h3>Всего проверок</h3><div class='small'>Полный объём security baseline этого запуска.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='problem-grid'>");
        foreach (var check in checks.OrderByDescending(GetSecurityPriority).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var severity = TranslateSecuritySeverity(check.Severity);
            var css = GetSecuritySeverityCss(check.Severity);
            var recommendation = ReadString(check.Metrics, "recommendation") ?? "Рекомендация не приложена.";
            var detail = ReadString(check.Metrics, "header") ?? ReadString(check.Metrics, "value") ?? ReadString(check.Metrics, "location");
            sb.AppendLine($"<div class='card {(css == "severity-fail" ? "tone-danger" : css == "severity-warn" ? "tone-warning" : css == "severity-pass" ? "tone-success" : "tone-accent")}'><div class='status-line'><span class='status-chip {css}'>{Escape(severity)}</span><strong>{Escape(check.Name)}</strong></div><div style='margin-top:10px'>{Escape(string.IsNullOrWhiteSpace(check.ErrorMessage) ? "Проблемы не зафиксированы." : check.ErrorMessage)}</div><div class='small' style='margin-top:8px'>Почему это важно: {Escape(ExplainSecurityWhyImportant(check.Name))}</div><div class='small' style='margin-top:8px'>Рекомендация: {Escape(recommendation)}</div>{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $"<div class='small' style='margin-top:8px'>Деталь: {Escape(detail)}</div>")}</div>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void AppendHttpFunctionalSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.functional", StringComparison.OrdinalIgnoreCase)) return;
        var specs = ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Select(x => new
        {
            Name = ReadString(x, "Name") ?? "(endpoint)",
            Method = ReadString(x, "Method") ?? "GET",
            Path = ReadString(x, "Path") ?? "/",
            Expected = ReadInt(x, "ExpectedStatusCode"),
            Headers = ReadStringList(x, "RequiredHeaders"),
            Body = ReadString(x, "BodyContains"),
            JsonChecks = ReadStringList(x, "JsonFieldEquals")
        }).ToList();
        var results = report.Results
            .OfType<EndpointResult>()
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Name) ? "(endpoint)" : x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IterationIndex).ThenByDescending(x => x.WorkerId).ToList(), StringComparer.OrdinalIgnoreCase);
        if (specs.Count == 0 && results.Count == 0) return;
        var allNames = specs.Select(x => x.Name)
            .Concat(results.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AppendSectionHeader("Функциональные проверки endpoint-ов", "Слева показаны ожидаемые условия, справа — фактический результат выполнения.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{allNames.Count}</div><h3>Endpoint-ов в отчёте</h3><div class='small'>Сколько функциональных точек удалось сопоставить с результатами.</div></div>");
        sb.AppendLine($"<div class='card {(results.Values.All(x => x.All(y => y.Success)) ? "tone-success" : "tone-danger")}'><div class='n'>{results.Values.Sum(x => x.Count)}</div><h3>Всего выполнений</h3><div class='small'>Все попытки по endpoint-ам с учётом повторов и итераций.</div></div>");
        sb.AppendLine($"<div class='card {(results.Values.Any(x => x.Any(y => !y.Success)) ? "tone-danger" : "tone-success")}'><div class='n'>{results.Values.Sum(x => x.Count(y => !y.Success))}</div><h3>Неуспешных выполнений</h3><div class='small'>Повторяющиеся ошибки сразу видны на уровне секции.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table><tr><th>Endpoint</th><th>Ожидалось</th><th>Получено</th><th>Повторы</th><th>Средняя задержка</th><th>Итог</th><th>Комментарий</th></tr>");
        foreach (var name in allNames)
        {
            var spec = specs.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            results.TryGetValue(name, out var resultGroup);
            resultGroup ??= new List<EndpointResult>();
            var latest = resultGroup.FirstOrDefault();
            var successCount = resultGroup.Count(x => x.Success);
            var failureCount = resultGroup.Count - successCount;
            var averageLatency = resultGroup.Count == 0 ? 0d : resultGroup.Average(x => x.LatencyMs > 0 ? x.LatencyMs : x.DurationMs);
            var checks = new List<string>();
            if (spec?.Expected.HasValue == true) checks.Add($"HTTP {spec.Expected}");
            if (spec?.Headers.Count > 0) checks.Add("Headers: " + string.Join(", ", spec.Headers));
            if (!string.IsNullOrWhiteSpace(spec?.Body)) checks.Add("Body contains: " + spec.Body);
            if (spec?.JsonChecks.Count > 0) checks.Add("JSON: " + string.Join(", ", spec.JsonChecks));
            var actual = latest == null
                ? "Нет результата"
                : $"Последний HTTP {latest.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}";
            var latency = latest == null
                ? "n/a"
                : resultGroup.Count == 1
                    ? FormatDuration(latest.LatencyMs > 0 ? latest.LatencyMs : latest.DurationMs)
                    : $"{FormatDuration(averageLatency)} в среднем";
            var statusText = latest == null
                ? "Не выполнено"
                : failureCount == 0
                    ? $"Успешно {successCount} из {resultGroup.Count}"
                    : $"Есть ошибки: {failureCount} из {resultGroup.Count}";
            var note = latest == null
                ? "Для настроенного endpoint-а не найдено ни одного результата."
                : failureCount == 0
                    ? $"Все {resultGroup.Count} зафиксированных прогона прошли успешно."
                    : latest.ErrorMessage ?? "Зафиксированы ошибки выполнения.";
            var endpointMeta = spec == null
                ? "Результат без сохранённой настройки"
                : $"{spec.Method} {spec.Path}";
            sb.AppendLine($"<tr><td><strong>{Escape(name)}</strong><div class='small'>{Escape(endpointMeta)}</div></td><td>{Escape(checks.Count == 0 ? "Только проверка HTTP-ответа" : string.Join(" | ", checks))}</td><td>{Escape(actual)}</td><td>{Escape(resultGroup.Count == 0 ? "0 попыток" : $"Успешно {successCount}, с ошибкой {failureCount}")}</td><td>{Escape(latency)}</td><td class='{(latest != null && failureCount == 0 ? "ok" : "fail")}'>{Escape(statusText)}</td><td>{Escape(note)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendAssetsSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.assets", StringComparison.OrdinalIgnoreCase)) return;
        var specs = ReadArray(report.ModuleSettingsSnapshot, "Assets").Select(x => new
        {
            Name = ReadString(x, "Name"),
            Url = ReadString(x, "Url") ?? string.Empty,
            ExpectedType = ReadString(x, "ExpectedContentType"),
            MaxKb = ReadInt(x, "MaxSizeKb"),
            MaxMs = ReadInt(x, "MaxLatencyMs")
        }).ToList();
        var assets = report.Results
            .OfType<AssetResult>()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IterationIndex).ThenByDescending(x => x.WorkerId).ToList(), StringComparer.OrdinalIgnoreCase);
        if (specs.Count == 0 && assets.Count == 0) return;
        var maxLatency = Math.Max(1d, report.Results.OfType<AssetResult>().Select(x => x.LatencyMs).DefaultIfEmpty(1d).Max());
        AppendSectionHeader("Проверка Web-ресурсов", "Сопоставление ожидаемых ограничений по размеру, типу и задержке с фактическим ответом.", sb);
        sb.AppendLine("<table><tr><th>Ресурс</th><th>Ожидаемый content type</th><th>Фактический content type</th><th>Лимит KB / факт</th><th>Лимит ms / факт</th><th>Итог</th><th>Комментарий</th></tr>");
        foreach (var spec in specs)
        {
            var key = string.IsNullOrWhiteSpace(spec.Name) ? spec.Url : spec.Name!;
            assets.TryGetValue(key, out var assetGroup);
            assetGroup ??= new List<AssetResult>();
            var latest = assetGroup.FirstOrDefault();
            var failures = assetGroup.Count(x => !x.Success);
            var actualKb = latest == null ? "n/a" : FormatKilobytes(latest.Bytes);
            var actualMs = latest == null ? "n/a" : FormatDuration(latest.LatencyMs);
            var note = latest == null
                ? "Для настроенного ресурса не найдено ни одного результата."
                : failures == 0
                    ? $"Зафиксировано {assetGroup.Count} успешных результатов."
                    : latest.ErrorMessage ?? $"Есть ошибки: {failures} из {assetGroup.Count}.";
            sb.AppendLine($"<tr><td><strong>{Escape(key)}</strong><div class='small'>{Escape(spec.Url)}</div></td><td>{Escape(spec.ExpectedType ?? "n/a")}</td><td>{Escape(latest?.ContentType ?? "n/a")}</td><td>{Escape($"{(spec.MaxKb.HasValue ? spec.MaxKb + " KB" : "n/a")} / {actualKb}")}</td><td>{(latest == null ? "n/a" : RenderBar(latest.LatencyMs, Math.Max(maxLatency, spec.MaxMs ?? 0), $"{(spec.MaxMs.HasValue ? spec.MaxMs + " ms" : "n/a")} / {actualMs}", spec.MaxMs.HasValue && latest.LatencyMs > spec.MaxMs ? "fail" : "warn"))}</td><td class='{(latest != null && failures == 0 ? "ok" : "fail")}'>{Escape(latest == null ? "Не выполнено" : failures == 0 ? $"Успешно {assetGroup.Count} из {assetGroup.Count}" : $"Есть ошибки: {failures} из {assetGroup.Count}")}</td><td>{Escape(note)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }
    private static void AppendDiagnosticsSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.diagnostics", StringComparison.OrdinalIgnoreCase)) return;
        var parsed = report.Results.OfType<CheckResult>().Select(x => new
        {
            Check = x,
            Stage = x.Name.StartsWith("DNS", StringComparison.OrdinalIgnoreCase) ? "DNS" : x.Name.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) ? "TCP" : x.Name.StartsWith("TLS", StringComparison.OrdinalIgnoreCase) ? "TLS" : "Check",
            Port = ReadInt(x.Metrics, "port") ?? ParsePort(x.Name),
            ResolvedIps = string.Join(", ", ReadArray(x.Metrics, "resolvedIps").Select(y => y.GetString()).Where(y => !string.IsNullOrWhiteSpace(y))),
            Latency = ReadDouble(x.Metrics, "latencyMs") ?? x.DurationMs
        }).ToList();
        if (parsed.Count == 0) return;
        var dnsChecks = parsed.Where(x => x.Stage == "DNS").ToList();
        var ports = parsed.Where(x => x.Port.HasValue).Select(x => x.Port!.Value).Distinct().OrderBy(x => x).ToList();
        AppendSectionHeader("Сетевая диагностика", "Диагностическая цепочка показывает, где именно оборвался путь от DNS до TCP и TLS.", sb);
        sb.AppendLine($"<div class='callout'><strong>Ключевой вывод</strong><div>{Escape(DescribeDiagnosticFailurePoint(parsed, dnsChecks, ports))}</div></div>");
        sb.AppendLine("<div class='timeline' style='margin-top:14px'>");
        if (dnsChecks.Count == 0)
        {
            sb.AppendLine("<div class='timeline-step'><h3>DNS</h3><div class='small'>DNS-этап не был сохранён в результатах этого запуска.</div></div>");
        }
        else
        {
            foreach (var check in dnsChecks)
            {
                sb.AppendLine($"<div class='timeline-step'><h3>DNS</h3><div class='status-line'><span class='status-chip {(check.Check.Success ? "b-ok" : "b-fail")}'>{(check.Check.Success ? "Успешно" : "Ошибка")}</span><span class='small'>{Escape(FormatDuration(check.Latency))}</span></div><div style='margin-top:8px'>{Escape(string.IsNullOrWhiteSpace(check.ResolvedIps) ? "Список IP-адресов не был сохранён." : "Получены IP: " + check.ResolvedIps)}</div><div class='small' style='margin-top:8px'>{Escape(string.IsNullOrWhiteSpace(check.Check.ErrorMessage) ? "DNS-разрешение завершилось без дополнительных замечаний." : check.Check.ErrorMessage)}</div></div>");
            }
        }
        sb.AppendLine("</div>");

        foreach (var port in ports)
        {
            var tcp = parsed.FirstOrDefault(x => x.Stage == "TCP" && x.Port == port);
            var tls = parsed.FirstOrDefault(x => x.Stage == "TLS" && x.Port == port);
            sb.AppendLine($"<div class='group-panel' style='margin-top:14px'><h3>Порт {port}</h3><div class='timeline'>");
            sb.AppendLine(RenderDiagnosticStageCard("TCP", tcp));
            sb.AppendLine(RenderDiagnosticStageCard("TLS", tls));
            sb.AppendLine("</div></div>");
        }
        sb.AppendLine("</section>");
    }

    private static void AppendAvailabilitySection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.availability", StringComparison.OrdinalIgnoreCase)) return;
        var rows = report.Results.OfType<CheckResult>().ToList();
        if (rows.Count == 0) return;
        var maxLatency = Math.Max(1d, rows.Select(x => ReadDouble(x.Metrics, "latencyMs") ?? x.DurationMs).Max());
        AppendSectionHeader("Проверка доступности", "Простая картина доступности целевого сервиса для этого запуска.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card {(rows.All(x => x.Success) ? "tone-success" : "tone-danger")}'><div class='n'>{rows.Count(x => x.Success)}</div><h3>Успешных проверок</h3><div class='small'>Сколько попыток подтвердили доступность целевой точки.</div></div>");
        sb.AppendLine($"<div class='card {(rows.Any(x => !x.Success) ? "tone-danger" : "tone-success")}'><div class='n'>{rows.Count(x => !x.Success)}</div><h3>Неуспешных проверок</h3><div class='small'>Сколько раз доступность не подтвердилась.</div></div>");
        sb.AppendLine($"<div class='card tone-warning'><div class='n'>{Escape(FormatDuration(rows.Average(x => ReadDouble(x.Metrics, "latencyMs") ?? x.DurationMs)))}</div><h3>Средняя задержка</h3><div class='small'>Средняя скорость ответа по всем проверкам доступности.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table><tr><th>Проверка</th><th>Цель</th><th>Статус</th><th>Задержка</th><th>Комментарий</th></tr>");
        foreach (var row in rows)
        {
            var target = ReadString(row.Metrics, "endpoint") ?? (ReadString(row.Metrics, "host") is { } host ? $"{host}:{ReadInt(row.Metrics, "port")?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}" : "n/a");
            var latency = ReadDouble(row.Metrics, "latencyMs") ?? row.DurationMs;
            sb.AppendLine($"<tr><td><strong>{Escape(row.Name)}</strong></td><td>{Escape(target)}</td><td class='{(row.Success ? "ok" : "fail")}'>{(row.Success ? "Доступен" : "Недоступен")}</td><td>{RenderBar(latency, maxLatency, FormatDuration(latency), row.Success ? "ok" : "fail")}</td><td>{Escape(string.IsNullOrWhiteSpace(row.ErrorMessage) ? "Проверка доступности завершена успешно." : row.ErrorMessage)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendPreflightSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.preflight", StringComparison.OrdinalIgnoreCase)) return;
        var checks = report.Results.OfType<PreflightResult>().ToList();
        if (checks.Count == 0) return;
        AppendSectionHeader("Предварительные проверки", "Готовность окружения и базовой сетевой доступности перед основным запуском.", sb);
        sb.AppendLine("<div class='diag'>");
        foreach (var check in checks)
        {
            var details = new List<string>();
            if (ReadString(check.Metrics, "path") is { } path) details.Add("Путь: " + path);
            if (ReadString(check.Metrics, "database") is { } db) details.Add("База данных: " + db);
            if (ReadString(check.Metrics, "browsersPath") is { } bp) details.Add("Путь к браузерам: " + bp);
            if (ReadBool(check.Metrics, "installed") is { } installed) details.Add("Установлено: " + FormatBoolRu(installed));
            if (ReadString(check.Metrics, "host") is { } host) details.Add("Цель: " + host + (ReadInt(check.Metrics, "port") is { } port ? ":" + port : string.Empty));
            if (ReadString(check.Metrics, "endpoint") is { } endpoint) details.Add("Endpoint: " + endpoint);
            if (ReadInt(check.Metrics, "statusCode") is { } code) details.Add("HTTP-статус: " + code);
            if (!string.IsNullOrWhiteSpace(check.Details)) details.Add(check.Details!);
            sb.AppendLine($"<div class='card {(check.Success ? "tone-success" : "tone-danger")}'><h3>{Escape(check.Name)}</h3><div class='status-line'><span class='status-chip {(check.Success ? "b-ok" : "b-fail")}'>{(check.Success ? "Готово" : "Есть проблема")}</span>{(ReadDouble(check.Metrics, "latencyMs") is { } latency ? $"<span class='small'>Задержка: {Escape(FormatDuration(latency))}</span>" : string.Empty)}</div><ul>{string.Concat(details.Select(x => $"<li>{Escape(x)}</li>"))}</ul></div>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void AppendSnapshotSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.snapshot", StringComparison.OrdinalIgnoreCase)) return;
        var items = report.Results
            .OfType<RunResult>()
            .Where(x => x.Name.StartsWith("Target:", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(x.ScreenshotPath))
            .Select(item =>
            {
                var path = NormalizeArtifactPath(item.ScreenshotPath) ?? item.ScreenshotPath ?? string.Empty;
                var subtitle = "Снимок сделан во время текущего запуска.";
                var pills = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.DetailsJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(item.DetailsJson);
                        var root = doc.RootElement;
                        if (ReadString(root, "url") is { } url) subtitle = url;
                        if (ReadBool(root, "hasSelector") == true)
                        {
                            pills.Add((ReadBool(root, "selectorFound") ?? false) ? "Селектор найден" : "Селектор не найден");
                        }

                        if (ReadBool(root, "fullPage") is { } fullPage)
                        {
                            pills.Add(fullPage ? "Снимок всей страницы" : "Только viewport");
                        }

                        if (ReadString(root, "waitUntil") is { } waitUntil)
                        {
                            pills.Add("Ожидание: " + waitUntil);
                        }

                        if (ReadDouble(root, "elapsedMs") is { } elapsed)
                        {
                            pills.Add("Сделано за " + FormatDuration(elapsed));
                        }

                        if (ReadDouble(root, "bytes") is { } bytes && bytes > 0)
                        {
                            pills.Add("Размер: " + FormatKilobytes((long)bytes));
                        }

                        if (TryGetProperty(root, "viewport", out var viewport))
                        {
                            pills.Add($"{ReadInt(viewport, "width")}x{ReadInt(viewport, "height")}");
                        }
                    }
                    catch
                    {
                        subtitle = Trim(item.DetailsJson);
                    }
                }

                if (item.WorkerId > 0) pills.Add($"Воркер {item.WorkerId}");
                if (item.IterationIndex > 0) pills.Add($"Итерация {item.IterationIndex}");

                return new
                {
                    Title = item.Name.StartsWith("Target:", StringComparison.OrdinalIgnoreCase) ? item.Name["Target:".Length..].Trim() : item.Name,
                    item.Success,
                    Path = path,
                    Subtitle = subtitle,
                    Pills = pills,
                    Message = item.Success ? "Снимок сохранён." : item.ErrorMessage ?? "Цель не удалось снять."
                };
            })
            .ToList();
        if (items.Count == 0) return;
        AppendSectionHeader("Галерея снимков", "Визуальные артефакты текущего запуска встроены прямо в отчёт.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{items.Count}</div><h3>Целей в секции</h3><div class='small'>Сколько отдельных снимков или попыток попало в отчёт.</div></div>");
        sb.AppendLine($"<div class='card {(items.All(x => x.Success) ? "tone-success" : "tone-danger")}'><div class='n'>{items.Count(x => x.Success)}</div><h3>Успешных снимков</h3><div class='small'>Цели, по которым удалось сохранить изображение.</div></div>");
        sb.AppendLine($"<div class='card {(items.Any(x => !x.Success) ? "tone-danger" : "tone-success")}'><div class='n'>{items.Count(x => !x.Success)}</div><h3>Неудачных попыток</h3><div class='small'>Цели, где снимок не был получен или завершился ошибкой.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class='gallery'>");
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Path))
            {
                sb.AppendLine($"<div class='shot'><a href='{Escape(item.Path)}'><img loading='lazy' src='{Escape(item.Path)}' alt='{Escape(item.Title)}'></a><div class='body'><div class='status-line'><strong>{Escape(item.Title)}</strong><span class='status-chip {(item.Success ? "b-ok" : "b-fail")}'>{(item.Success ? "Снимок получен" : "Снимок не получен")}</span></div><div class='small'>{Escape(item.Subtitle)}</div><div class='pill-row'>{string.Concat(item.Pills.Select(x => $"<span class='pill'>{Escape(x)}</span>"))}</div><div class='small' style='margin-top:8px'>{Escape(item.Message)}</div></div></div>");
            }
            else
            {
                sb.AppendLine($"<div class='shot empty-shot'><div class='body'><div class='status-line'><strong>{Escape(item.Title)}</strong><span class='status-chip b-fail'>Не получилось</span></div><div class='small' style='margin-top:8px'>{Escape(item.Subtitle)}</div><div class='pill-row'>{string.Concat(item.Pills.Select(x => $"<span class='pill'>{Escape(x)}</span>"))}</div><div style='margin-top:10px'>{Escape(item.Message)}</div></div></div>");
            }
        }
        sb.AppendLine("</div></section>");
    }

    private static void AppendTimingSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.timing", StringComparison.OrdinalIgnoreCase)) return;
        var rows = report.Results.OfType<TimingResult>().Select(x =>
        {
            var url = x.Url ?? string.Empty;
            var profile = "n/a";
            var navigation = 0d;
            var dom = 0d;
            var load = 0d;
            var screenshot = string.Empty;
            if (!string.IsNullOrWhiteSpace(x.DetailsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(x.DetailsJson);
                    var root = doc.RootElement;
                    url = ReadString(root, "url") ?? url;
                    if (TryGetProperty(root, "metrics", out var metrics))
                    {
                        navigation = ReadDouble(metrics, "navigationMs") ?? 0;
                        dom = ReadDouble(metrics, "domContentLoadedMs") ?? 0;
                        load = ReadDouble(metrics, "loadEventMs") ?? 0;
                    }
                    if (TryGetProperty(root, "profile", out var profileElement)) profile = BuildTimingProfileLabel(profileElement);
                    screenshot = NormalizeArtifactPath(ReadString(root, "screenshot")) ?? string.Empty;
                }
                catch { }
            }
            return new { x.Name, Url = url, Profile = profile, Navigation = navigation, Dom = dom, Load = load, x.Success, x.ErrorMessage, Screenshot = screenshot, x.WorkerId, x.IterationIndex };
        }).ToList();
        if (rows.Count == 0) return;
        var maxMetric = Math.Max(1d, rows.SelectMany(x => new[] { x.Navigation, x.Dom, x.Load }).Max());
        AppendSectionHeader("Профили загрузки Chromium", "Сопоставление таймингов navigation, DOMContentLoaded и load по целям и профилям Chromium.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{rows.Count}</div><h3>Профилей в отчёте</h3><div class='small'>Сколько профилей Chromium реально попало в секцию сравнения загрузки.</div></div>");
        sb.AppendLine($"<div class='card {(rows.All(x => x.Success) ? "tone-success" : "tone-danger")}'><div class='n'>{rows.Count(x => x.Success)}</div><h3>Успешных профилей</h3><div class='small'>Сколько профилей завершились без ошибок загрузки.</div></div>");
        sb.AppendLine($"<div class='card tone-warning'><div class='n'>{Escape(FormatDuration(rows.Max(x => x.Load > 0 ? x.Load : x.Navigation)))}</div><h3>Самый долгий профиль</h3><div class='small'>Худшее измерение по load/navigation среди профилей.</div></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table><tr><th>Цель</th><th>Профиль</th><th>Навигация</th><th>DOMContentLoaded</th><th>Load event</th><th>Статус</th><th>Артефакты</th></tr>");
        foreach (var row in rows) sb.AppendLine($"<tr><td><strong>{Escape(row.Name)}</strong><div class='small'>{Escape(row.Url)}</div><div class='small'>Воркер {row.WorkerId} / итерация {row.IterationIndex}</div></td><td>{Escape(row.Profile)}</td><td>{RenderBar(row.Navigation, maxMetric, FormatDuration(row.Navigation), row.Success ? "ok" : "warn")}</td><td>{RenderBar(row.Dom, maxMetric, FormatDuration(row.Dom), "warn")}</td><td>{RenderBar(row.Load, maxMetric, FormatDuration(row.Load), "warn")}</td><td class='{(row.Success ? "ok" : "fail")}'>{(row.Success ? "Успешно" : "Ошибка")}{(string.IsNullOrWhiteSpace(row.ErrorMessage) ? string.Empty : $"<div class='small'>{Escape(row.ErrorMessage)}</div>")}</td><td>{(string.IsNullOrWhiteSpace(row.Screenshot) ? "<span class='small'>n/a</span>" : $"<a href='{Escape(row.Screenshot)}'>Скриншот</a>")}</td></tr>");
        sb.AppendLine("</table></section>");
    }

    private static string BuildTimingProfileLabel(JsonElement profileElement)
    {
        var browser = ReadString(profileElement, "browser") ?? "chromium";
        var viewportText = "n/a";
        if (TryGetProperty(profileElement, "viewport", out var viewport))
        {
            viewportText = viewport.ValueKind switch
            {
                JsonValueKind.String => viewport.GetString() ?? "n/a",
                JsonValueKind.Object => $"{ReadInt(viewport, "width")}x{ReadInt(viewport, "height")}",
                _ => "n/a"
            };
        }

        return $"{browser} · {viewportText}";
    }

    private static void AppendScenarioSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.scenario", StringComparison.OrdinalIgnoreCase)) return;
        var steps = report.Results.OfType<StepResult>().ToList();
        var screenshots = report.Results.OfType<RunResult>()
            .Where(x => !string.Equals(x.Name, "Регрессионное сравнение", StringComparison.OrdinalIgnoreCase) &&
                        (!string.IsNullOrWhiteSpace(x.ScreenshotPath) || x.Name.StartsWith("Scenario:", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        AppendSectionHeader("Выполнение UI-сценария", "Пошаговое представление выполнения сценария и связанных артефактов.", sb);
        sb.AppendLine("<div class='inline-cards'>");
        sb.AppendLine($"<div class='card tone-accent'><div class='n'>{steps.Count}</div><h3>Шагов в отчёте</h3><div class='small'>Сколько шагов сценария было зафиксировано в результатах.</div></div>");
        sb.AppendLine($"<div class='card {(steps.All(x => x.Success) ? "tone-success" : "tone-danger")}'><div class='n'>{steps.Count(x => x.Success)}</div><h3>Успешных шагов</h3><div class='small'>Количество шагов без ошибок по всем итерациям.</div></div>");
        sb.AppendLine($"<div class='card {(steps.Any(x => !x.Success) ? "tone-danger" : "tone-success")}'><div class='n'>{steps.Count(x => !x.Success)}</div><h3>Проблемных шагов</h3><div class='small'>Шаги, которые завершились ошибкой или прервали сценарий.</div></div>");
        sb.AppendLine("</div>");
        if (steps.Count == 0)
        {
            sb.AppendLine("<div class='empty'>Пошаговые результаты не были зафиксированы. Сценарий мог завершиться раньше, чем началась детальная запись шагов.</div>");
        }
        else
        {
            var groups = steps
                .GroupBy(step => new { step.WorkerId, step.IterationIndex })
                .OrderBy(g => g.Key.WorkerId)
                .ThenBy(g => g.Key.IterationIndex);

            foreach (var group in groups)
            {
                var orderedSteps = group.OrderBy(GetScenarioStepIndex).ToList();
                var failedCount = orderedSteps.Count(x => !x.Success);
                sb.AppendLine("<div class='group-panel' style='margin-top:14px'>");
                sb.AppendLine($"<h3>Воркер {group.Key.WorkerId}, итерация {group.Key.IterationIndex}</h3>");
                sb.AppendLine($"<div class='small'>Шагов: {orderedSteps.Count}. Успешно: {orderedSteps.Count - failedCount}. Ошибок: {failedCount}.</div>");
                sb.AppendLine("<table style='margin-top:12px'><tr><th>Шаг</th><th>Действие</th><th>Что делали</th><th>Длительность</th><th>Статус</th><th>Скриншот</th></tr>");
                foreach (var step in orderedSteps)
                {
                    var target = DescribeScenarioStepTarget(step);
                    var screenshot = string.IsNullOrWhiteSpace(step.ScreenshotPath)
                        ? "<span class='small'>нет</span>"
                        : $"<a href='{Escape(NormalizeArtifactPath(step.ScreenshotPath) ?? step.ScreenshotPath!)}'>Открыть</a>";
                    sb.AppendLine($"<tr><td><strong>{GetScenarioStepIndex(step)}</strong></td><td>{Escape(TranslateScenarioAction(step.Action))}</td><td>{Escape(target)}</td><td>{Escape(FormatDuration(step.DurationMs))}</td><td class='{(step.Success ? "ok" : "fail")}'>{(step.Success ? "Успешно" : "Ошибка")}{(string.IsNullOrWhiteSpace(step.ErrorMessage) ? string.Empty : $"<div class='small'>{Escape(step.ErrorMessage)}</div>")}</td><td>{screenshot}</td></tr>");
                }

                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }
        }
        if (screenshots.Count > 0)
        {
            sb.AppendLine("<div class='divider'></div>");
            sb.AppendLine("<h3 style='margin:0 0 12px'>Скриншоты и события сценария</h3>");
            sb.AppendLine("<div class='gallery' style='margin-top:14px'>");
            foreach (var shot in screenshots)
            {
                var path = NormalizeArtifactPath(shot.ScreenshotPath) ?? shot.ScreenshotPath ?? string.Empty;
                var note = DescribeScenarioArtifact(shot);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    sb.AppendLine($"<div class='shot'><a href='{Escape(path)}'><img loading='lazy' src='{Escape(path)}' alt='{Escape(shot.Name)}'></a><div class='body'><div class='status-line'><strong>{Escape(shot.Name)}</strong><span class='status-chip {(shot.Success ? "b-ok" : "b-fail")}'>{(shot.Success ? "Артефакт сохранён" : "Есть проблема")}</span></div><div class='small'>{Escape(note)}</div></div></div>");
                }
                else
                {
                    sb.AppendLine($"<div class='shot empty-shot'><div class='body'><div class='status-line'><strong>{Escape(shot.Name)}</strong><span class='status-chip b-fail'>Без скриншота</span></div><div class='small' style='margin-top:8px'>{Escape(note)}</div></div></div>");
                }
            }
            sb.AppendLine("</div>");
        }
        var comparison = ExtractRegressionComparison(report);
        if (comparison != null)
        {
            sb.AppendLine("<div class='divider'></div>");
            sb.AppendLine("<div class='callout'>");
            sb.AppendLine("<strong>Регрессионное сравнение</strong>");
            sb.AppendLine($"<div>{Escape(DescribeRegressionComparison(comparison))}</div>");
            sb.AppendLine("<div class='facts' style='margin-top:12px'>");
            AppendKv("Базовый прогон", comparison.BaselineRunId ?? "n/a", sb);
            AppendKv("Сравнено шагов", comparison.ComparedSteps.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Изменившихся шагов", comparison.ChangedSteps.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Новых ошибок", comparison.NewErrors.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Исправленных ошибок", comparison.ResolvedErrors.ToString(CultureInfo.InvariantCulture), sb);
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</section>");
    }

    private static void AppendSectionHeader(string title, string? note, StringBuilder sb)
    {
        sb.AppendLine($"<section class='sec'><div class='section-head'><h2>{Escape(title)}</h2>{(string.IsNullOrWhiteSpace(note) ? string.Empty : $"<div class='small'>{Escape(note)}</div>")}</div>");
    }

    private static void AppendKeyMetric(string key, string value, StringBuilder sb)
    {
        sb.AppendLine($"<div class='fact'><span class='k'>{Escape(key)}</span><span class='v'>{Escape(value)}</span></div>");
    }

    private static void AppendSummaryCard(string title, string value, string note, StringBuilder sb)
    {
        sb.AppendLine($"<div class='card'><div class='n'>{Escape(value)}</div><h3>{Escape(title)}</h3><div class='small'>{Escape(note)}</div></div>");
    }

    private static void AppendKv(string key, string value, StringBuilder sb)
    {
        sb.AppendLine($"<div class='fact'><span class='k'>{Escape(key)}</span><span class='v'>{Escape(value)}</span></div>");
    }

    private static string RenderBar(double value, double max, string label, string css)
    {
        return $"<div class='bar {css}'><span style='width:{ClampPercent(max <= 0 ? 0 : value * 100d / max):F1}%'></span></div><div class='cap'><span>{Escape(label)}</span></div>";
    }

    private static string RenderDiagnosticCell(dynamic? value)
    {
        if (value == null) return "<span class='small'>n/a</span>";
        return $"<div class='{(value.Check.Success ? "ok" : "fail")}'>{(value.Check.Success ? "Успешно" : "Ошибка")}</div><div class='small'>{Escape(FormatDuration((double)value.Latency))}</div><div class='small'>{Escape(string.IsNullOrWhiteSpace((string)value.Check.ErrorMessage) ? "Проверка завершена." : (string)value.Check.ErrorMessage)}</div>";
    }

    private static string RenderDiagnosticStageCard(string stage, dynamic? value)
    {
        if (value == null)
        {
            return $"<div class='timeline-step'><h3>{Escape(stage)}</h3><div class='small'>Этап не был выполнен или не попал в результаты.</div></div>";
        }

        var detail = string.IsNullOrWhiteSpace((string)value.Check.ErrorMessage)
            ? $"{stage}-этап завершился без дополнительных замечаний."
            : (string)value.Check.ErrorMessage;
        return $"<div class='timeline-step'><h3>{Escape(stage)}</h3><div class='status-line'><span class='status-chip {(value.Check.Success ? "b-ok" : "b-fail")}'>{(value.Check.Success ? "Успешно" : "Ошибка")}</span><span class='small'>{Escape(FormatDuration((double)value.Latency))}</span></div>{(!string.IsNullOrWhiteSpace((string?)value.ResolvedIps) ? $"<div class='small' style='margin-top:8px'>{Escape("IP: " + (string)value.ResolvedIps)}</div>" : string.Empty)}<div class='small' style='margin-top:8px'>{Escape(detail)}</div></div>";
    }

    private static string DescribeDiagnosticFailurePoint(IEnumerable<dynamic> parsed, IEnumerable<dynamic> dnsChecks, IReadOnlyList<int> ports)
    {
        var dnsFailure = dnsChecks.FirstOrDefault(x => !x.Check.Success);
        if (dnsFailure != null)
        {
            return "Проблема начинается на этапе DNS: хост не был надёжно разрешён до перехода к TCP/TLS.";
        }

        foreach (var port in ports)
        {
            var tcp = parsed.FirstOrDefault(x => x.Stage == "TCP" && x.Port == port);
            if (tcp is { } tcpStage && !tcpStage.Check.Success)
            {
                return $"Проблема начинается на этапе TCP для порта {port}: сетевое подключение не установилось.";
            }

            var tls = parsed.FirstOrDefault(x => x.Stage == "TLS" && x.Port == port);
            if (tls is { } tlsStage && !tlsStage.Check.Success)
            {
                return $"DNS и TCP отработали, но на этапе TLS для порта {port} произошло отклонение.";
            }
        }

        return "Цепочка DNS → TCP → TLS выглядит целостной: явного места разрыва в этом запуске не зафиксировано.";
    }

    private static string TranslateSecuritySeverity(string? severity) => severity switch
    {
        "Fail" => "Критично",
        "Warn" => "Нужно внимание",
        "Pass" => "Норма",
        "NA" => "Неприменимо",
        _ => "Не указано"
    };

    private static string GetSecuritySeverityCss(string? severity) => severity switch
    {
        "Fail" => "severity-fail",
        "Warn" => "severity-warn",
        "Pass" => "severity-pass",
        "NA" => "severity-na",
        _ => "severity-na"
    };

    private static int GetSecurityPriority(CheckResult check) => check.Severity switch
    {
        "Fail" => 3,
        "Warn" => 2,
        "Pass" => 1,
        _ => 0
    };

    private static string ExplainSecurityWhyImportant(string checkName) => checkName switch
    {
        "HSTS" => "Снижает риск downgrade-сценариев и заставляет браузер использовать HTTPS.",
        "X-Content-Type-Options" => "Защищает от небезопасного MIME-sniffing в браузере.",
        "Frame protection" => "Помогает защититься от clickjacking через встраивание страницы во frame.",
        "Content-Security-Policy" => "Ограничивает выполнение нежелательных скриптов и источников контента.",
        "Referrer-Policy" => "Сокращает лишнюю утечку URL и контекста перехода.",
        "Permissions-Policy" => "Ограничивает доступ к чувствительным браузерным API.",
        "HTTP→HTTPS redirect" => "Не даёт пользователю остаться на незашифрованном HTTP-канале.",
        "Cookie flags" => "Уменьшает риск кражи или небезопасной передачи cookies.",
        _ => "Влияет на базовую поверхность безопасности целевого ресурса."
    };

    private static string DescribeScenarioStepTarget(StepResult step)
    {
        var fallback = !string.IsNullOrWhiteSpace(step.Selector) ? step.Selector! : step.Name;
        if (string.IsNullOrWhiteSpace(step.DetailsJson))
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(step.DetailsJson);
            return ReadString(doc.RootElement, "input")
                ?? ReadString(doc.RootElement, "url")
                ?? ReadString(doc.RootElement, "value")
                ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string TranslateScenarioAction(string? action)
    {
        return action switch
        {
            null or "" => "Действие не указано",
            "Navigate" => "Переход",
            "Click" => "Клик",
            "Type" => "Ввод текста",
            "WaitForSelector" => "Ожидание элемента",
            "WaitForTimeout" => "Пауза",
            "Screenshot" => "Скриншот",
            "AssertText" => "Проверка текста",
            "AssertVisible" => "Проверка видимости",
            _ => action
        };
    }

    private static string DescribeScenarioArtifact(RunResult shot)
    {
        if (string.IsNullOrWhiteSpace(shot.DetailsJson))
        {
            return shot.ErrorMessage ?? "Артефакт сценария сохранён без дополнительных деталей.";
        }

        try
        {
            using var doc = JsonDocument.Parse(shot.DetailsJson);
            var root = doc.RootElement;
            var stage = ReadString(root, "stage");
            var targetUrl = ReadString(root, "targetUrl");
            var message = ReadString(root, "message");
            return stage switch
            {
                "startup-navigation" => $"{(string.IsNullOrWhiteSpace(targetUrl) ? "Стартовая навигация" : "Стартовая навигация к " + targetUrl)}. {(shot.Success ? "Этап завершился." : shot.ErrorMessage ?? message ?? "Есть проблема.")}",
                "interrupted" => shot.ErrorMessage ?? message ?? "Сценарий был прерван до завершения всех шагов.",
                "iteration-final" => "Итоговый скриншот после выполнения сценария.",
                _ => shot.ErrorMessage ?? message ?? Trim(shot.DetailsJson)
            };
        }
        catch
        {
            return shot.ErrorMessage ?? Trim(shot.DetailsJson);
        }
    }

    private static string DescribeRegressionComparison(RegressionComparisonView comparison)
    {
        if (string.IsNullOrWhiteSpace(comparison.BaselineRunId))
        {
            return string.IsNullOrWhiteSpace(comparison.Message)
                ? "Базовый успешный прогон не найден, поэтому регрессионное сравнение не было выполнено."
                : comparison.Message;
        }

        return $"Сравнение выполнено с базовым прогоном {comparison.BaselineRunId}. Сравнено шагов: {comparison.ComparedSteps}, изменилось: {comparison.ChangedSteps}, новых ошибок: {comparison.NewErrors}, исправленных ошибок: {comparison.ResolvedErrors}.";
    }

    private static int GetProblemPriority(ResultBase item)
    {
        var severity = ResolveItemSeverity(item.ErrorType, item.ErrorMessage);
        return severity switch
        {
            "Высокая" => 3,
            "Средняя" => 2,
            _ => 1
        };
    }

    private static string FormatOccurrences(int count)
    {
        return count switch
        {
            <= 0 => "не повторялось",
            1 => "1 раз",
            _ => $"{count} повторений"
        };
    }

    private static string BuildProblemWhere(IEnumerable<ResultBase> group)
    {
        var items = group.ToList();
        var first = items.First();
        var workers = items.Select(x => x.WorkerId).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
        var iterations = items.Select(x => x.IterationIndex).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
        var workerText = workers.Count == 0 ? string.Empty : "воркер " + string.Join(", ", workers);
        var iterationText = iterations.Count == 0 ? string.Empty : "итерация " + string.Join(", ", iterations);
        var scope = string.Join(" / ", new[] { workerText, iterationText }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(scope)
            ? TranslateResultKind(first.Kind)
            : $"{TranslateResultKind(first.Kind)} ({scope})";
    }

    private static string BuildProblemImpact(string moduleId, ResultBase item)
    {
        return moduleId switch
        {
            "ui.scenario" => "Может ломать выполнение сценария целиком или искажать картину регрессии.",
            "ui.snapshot" => "Из-за этого снимок не удалось получить или он потерял диагностическую ценность.",
            "http.performance" => "Влияет на восприятие скорости и стабильности endpoint-а под выбранной нагрузкой.",
            "http.functional" => "Означает, что контракт endpoint-а подтверждён не полностью или нестабилен.",
            "net.diagnostics" => "Показывает, на каком этапе сетевой цепочки теряется доступность сервиса.",
            "net.security" => "Повышает риск по базовой безопасности ресурса и требует исправления настройки.",
            _ => item.DurationMs > 0
                ? "Влияет на итоговый статус запуска и требует внимания в результатах."
                : "Отражается на итоговой оценке запуска."
        };
    }

    private static string SummarizeProblemMessage(IEnumerable<ResultBase> group)
    {
        var messages = group
            .Select(item => string.IsNullOrWhiteSpace(item.ErrorMessage) ? item.ErrorType : item.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return messages.Count == 0
            ? "Ошибка зафиксирована без текстового пояснения."
            : string.Join(" ", messages);
    }

    private static List<KeyValuePair<string, string>> DescribeModuleSettings(TestReport report)
    {
        var list = new List<KeyValuePair<string, string>>();
        switch (report.ModuleId)
        {
            case "http.performance":
                list.Add(new("Базовый URL", ReadString(report.ModuleSettingsSnapshot, "BaseUrl") ?? "n/a"));
                list.Add(new("Настроенные endpoint-ы", ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Таймаут модуля", (ReadInt(report.ModuleSettingsSnapshot, "TimeoutSeconds")?.ToString(CultureInfo.InvariantCulture) ?? "n/a") + " с"));
                break;
            case "net.security":
                list.Add(new("URL", ReadString(report.ModuleSettingsSnapshot, "Url") ?? "n/a"));
                break;
            case "http.functional":
                list.Add(new("Базовый URL", ReadString(report.ModuleSettingsSnapshot, "BaseUrl") ?? "n/a"));
                list.Add(new("Настроенные endpoint-ы", ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Count.ToString(CultureInfo.InvariantCulture)));
                break;
            case "http.assets":
                list.Add(new("Настроенные ресурсы", ReadArray(report.ModuleSettingsSnapshot, "Assets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Таймаут модуля", (ReadInt(report.ModuleSettingsSnapshot, "TimeoutSeconds")?.ToString(CultureInfo.InvariantCulture) ?? "n/a") + " с"));
                break;
            case "net.diagnostics":
                list.Add(new("Хост", ReadString(report.ModuleSettingsSnapshot, "Hostname") ?? "n/a"));
                list.Add(new("Порты", string.Join(", ", ReadArray(report.ModuleSettingsSnapshot, "Ports").Select(x => ReadInt(x, "Port")).Where(x => x.HasValue).Select(x => x!.Value.ToString(CultureInfo.InvariantCulture)))));
                break;
            case "net.availability":
                list.Add(new("Тип проверки", ReadString(report.ModuleSettingsSnapshot, "CheckType") ?? "n/a"));
                list.Add(new("Цель", ReadString(report.ModuleSettingsSnapshot, "Url") ?? (ReadString(report.ModuleSettingsSnapshot, "Host") is { } host ? $"{host}:{ReadInt(report.ModuleSettingsSnapshot, "Port")?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}" : "n/a")));
                break;
            case "net.preflight":
                list.Add(new("Цель", ReadString(report.ModuleSettingsSnapshot, "Target") ?? "n/a"));
                break;
            case "ui.snapshot":
                list.Add(new("Цели", ReadArray(report.ModuleSettingsSnapshot, "Targets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Режим ожидания страницы", ReadString(report.ModuleSettingsSnapshot, "WaitUntil") ?? "n/a"));
                list.Add(new("Формат", ReadString(report.ModuleSettingsSnapshot, "ScreenshotFormat") ?? "n/a"));
                list.Add(new("Вся страница", FormatBoolRu(ReadBool(report.ModuleSettingsSnapshot, "FullPage") ?? false)));
                break;
            case "ui.timing":
                list.Add(new("Цели", ReadArray(report.ModuleSettingsSnapshot, "Targets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Режим ожидания страницы", ReadString(report.ModuleSettingsSnapshot, "WaitUntil") ?? "n/a"));
                break;
            case "ui.scenario":
                list.Add(new("Целевой URL", ReadString(report.ModuleSettingsSnapshot, "TargetUrl") ?? "n/a"));
                list.Add(new("Шаги", ReadArray(report.ModuleSettingsSnapshot, "Steps").Count.ToString(CultureInfo.InvariantCulture)));
                break;
        }
        return list.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
    }

    private static IReadOnlyList<string> BuildArtifactPaths(TestReport report)
    {
        var paths = new List<string>();
        AddArtifactPath(paths, report.Artifacts.JsonPath, "report.json");
        AddArtifactPath(paths, report.Artifacts.HtmlPath, "report.html");
        AddArtifactPath(paths, report.Artifacts.LogPath, "logs/run.log");
        foreach (var screenshotPath in report.Results.Select(result => result switch { RunResult run => run.ScreenshotPath, StepResult step => step.ScreenshotPath, _ => null }).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)) AddArtifactPath(paths, screenshotPath, null);
        foreach (var artifact in report.ModuleArtifacts.Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))) AddArtifactPath(paths, artifact.RelativePath, null);
        return paths;
    }

    private static void AddArtifactPath(List<string> paths, string? path, string? fallback)
    {
        var normalized = NormalizeArtifactPath(path, fallback);
        if (!string.IsNullOrWhiteSpace(normalized) && !paths.Contains(normalized, StringComparer.OrdinalIgnoreCase)) paths.Add(normalized);
    }

    private static string GuessArtifactLabel(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized is "." or "./") return "Каталог запуска";
        if (normalized.EndsWith("report.json", StringComparison.OrdinalIgnoreCase)) return "JSON-отчёт";
        if (normalized.EndsWith("report.html", StringComparison.OrdinalIgnoreCase)) return "HTML-отчёт";
        if (normalized.Contains("logs/run.log", StringComparison.OrdinalIgnoreCase)) return "Журнал запуска";
        if (normalized.Contains("screenshots/", StringComparison.OrdinalIgnoreCase)) return "Скриншот";
        return "Артефакт";
    }

    private static string? NormalizeArtifactPath(string? path, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return fallback?.Replace('\\', '/');
        var normalized = path.Replace('\\', '/');
        if (!Path.IsPathRooted(normalized)) return normalized;
        foreach (var marker in new[] { "report.json", "report.html", "logs/run.log", "screenshots/" })
        {
            var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return normalized[index..];
        }
        return fallback?.Replace('\\', '/') ?? normalized;
    }
    private static string DescribeResult(ResultBase item) => item switch
    {
        RunResult run => string.IsNullOrWhiteSpace(run.DetailsJson) ? run.ScreenshotPath ?? "Артефакт запуска." : Trim(run.DetailsJson),
        StepResult step => string.IsNullOrWhiteSpace(step.DetailsJson) ? $"{step.Action} {step.Selector}".Trim() : Trim(step.DetailsJson),
        TimingResult timing => string.IsNullOrWhiteSpace(timing.DetailsJson) ? timing.Url ?? "Результат таймингов." : Trim(timing.DetailsJson),
        CheckResult check => $"{(check.StatusCode.HasValue ? "HTTP " + check.StatusCode.Value + " · " : string.Empty)}{ReadString(check.Metrics, "endpoint") ?? ReadString(check.Metrics, "host") ?? check.ErrorMessage ?? "Дополнительные детали не указаны."}",
        EndpointResult endpoint => $"HTTP {endpoint.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, {FormatDuration(endpoint.LatencyMs > 0 ? endpoint.LatencyMs : endpoint.DurationMs)}",
        AssetResult asset => $"{asset.ContentType ?? "n/a"}, {FormatKilobytes(asset.Bytes)}, {FormatDuration(asset.LatencyMs)}",
        ProbeResult probe => string.IsNullOrWhiteSpace(probe.Details) ? "Подробности отсутствуют." : probe.Details!,
        PreflightResult preflight => string.IsNullOrWhiteSpace(preflight.Details) ? "Предварительная проверка." : preflight.Details!,
        _ => item.ErrorMessage ?? "Подробности отсутствуют."
    };

    private static RegressionComparisonView? ExtractRegressionComparison(TestReport report)
    {
        var compareResult = report.Results.OfType<RunResult>().FirstOrDefault(x => string.Equals(x.Name, "Регрессионное сравнение", StringComparison.OrdinalIgnoreCase));
        if (compareResult == null || string.IsNullOrWhiteSpace(compareResult.DetailsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(compareResult.DetailsJson);
            var root = doc.RootElement;
            return new RegressionComparisonView(ReadString(root, "baselineRunId"), ReadInt(root, "comparedSteps") ?? 0, ReadInt(root, "changedSteps") ?? 0, ReadInt(root, "newErrors") ?? 0, ReadInt(root, "resolvedErrors") ?? 0, ReadString(root, "message") ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private static (string Label, string CssClass) GetSeverity(int failed, int total)
    {
        if (failed <= 0) return ("Уровень риска: низкий", "b-low");
        var ratio = total <= 0 ? 1d : (double)failed / total;
        return ratio >= 0.5d ? ("Уровень риска: высокий", "b-high") : ("Уровень риска: средний", "b-mid");
    }

    private static List<string> BuildRecommendations(TestReport report, IReadOnlyList<ResultBase> failed)
    {
        var list = new List<string>();
        if (failed.Count == 0)
        {
            list.Add("Запуск выглядит стабильным: его можно использовать как базовую точку для следующих сравнений и повторных прогонов.");
            if (report.ModuleId == "ui.snapshot") list.Add("Сохранённые снимки можно использовать как визуальный ориентир для следующих UI-проверок.");
            if (report.ModuleId == "http.performance") list.Add("Сохраните этот результат как ориентир по задержкам и сравнивайте с ним следующие прогоны при той же нагрузке.");
            return list;
        }
        if (failed.Any(x => (x.ErrorType ?? string.Empty).Contains("Timeout", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Перед повторным запуском проверьте таймауты и внешнюю стабильность сети или окружения: часть ошибок похожа на истечение времени ожидания.");
        }

        if (failed.Any(x => (x.ErrorMessage ?? string.Empty).Contains("selector", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Для UI-проверок перепроверьте селекторы и изменения DOM-структуры: ошибки указывают на нестабильную привязку к элементам страницы.");
        }

        if (report.ModuleId == "ui.timing")
        {
            list.Add("Сравните самые медленные профили Chromium и выровняйте viewport, user-agent и режим headless, прежде чем делать выводы о поведении загрузки.");
        }

        if (report.ModuleId == "ui.scenario")
        {
            list.Add("После исправлений повторите сценарий и сопоставьте проблемные шаги с последним успешным базовым прогоном.");
        }

        if (report.ModuleId.StartsWith("http.", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Сверьте контракт endpoint-ов: HTTP-статусы, обязательные заголовки, ограничения по размеру ответа и ожидаемое содержимое.");
        }

        if (report.ModuleId == "net.security")
        {
            list.Add("Сначала исправьте замечания со статусом «Критично» и «Нужно внимание», затем подтвердите изменения повторным прогоном.");
        }

        if (list.Count == 0)
        {
            list.Add("Откройте проблемные строки и приложенные артефакты, затем повторите запуск после исправления входных данных или окружения.");
        }

        return list;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = Math.Clamp((int)Math.Ceiling(Math.Clamp(percentile, 0, 1) * sorted.Count) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static string ResolveItemSeverity(string? errorType, string? errorMessage)
    {
        var source = $"{errorType} {errorMessage}";
        if (source.Contains("timeout", StringComparison.OrdinalIgnoreCase) || source.Contains("dns", StringComparison.OrdinalIgnoreCase)) return "Высокая";
        if (source.Contains("assert", StringComparison.OrdinalIgnoreCase) || source.Contains("selector", StringComparison.OrdinalIgnoreCase)) return "Средняя";
        return "Низкая";
    }

    private static string GetResultName(ResultBase result) => result switch
    {
        RunResult run => run.Name,
        StepResult step => step.Name,
        CheckResult check => check.Name,
        EndpointResult endpoint => endpoint.Name,
        AssetResult asset => asset.Name,
        PreflightResult preflight => preflight.Name,
        ProbeResult probe => probe.Name,
        TimingResult timing => string.IsNullOrWhiteSpace(timing.Name) ? timing.Url ?? string.Empty : timing.Name,
        _ => string.Empty
    };

    private static string FormatStatus(TestStatus status) => status switch
    {
        TestStatus.Success => "Пройден успешно",
        TestStatus.Failed => "Завершился с ошибкой",
        TestStatus.Canceled => "Отменён",
        TestStatus.Stopped => "Остановлен",
        TestStatus.Partial => "Завершён частично",
        _ => status.ToString()
    };

    private static string GetStatusCssClass(TestStatus status) => status switch
    {
        TestStatus.Success => "b-ok",
        TestStatus.Failed => "b-fail",
        TestStatus.Canceled or TestStatus.Stopped or TestStatus.Partial => "b-warn",
        _ => "b-warn"
    };

    private static string FormatDate(DateTimeOffset value) => value == default ? "n/a" : value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
    private static string FormatDuration(double value) => value <= 0 ? "0 ms" : value >= 1000 ? $"{value / 1000d:F2} s" : $"{value:F0} ms";
    private static string FormatKilobytes(long bytes) => bytes <= 0 ? "0 KB" : $"{bytes / 1024d:F1} KB";
    private static string FormatBool(bool value) => value ? "Yes" : "No";
    private static string FormatBoolRu(bool value) => value ? "Да" : "Нет";
    private static string FormatRunMode(RunMode mode) => mode switch
    {
        RunMode.Iterations => "По количеству итераций",
        RunMode.Duration => "По времени",
        _ => mode.ToString()
    };
    private static string FormatFamily(TestFamily family) => family switch
    {
        TestFamily.UiTesting => "UI-тестирование",
        TestFamily.HttpTesting => "HTTP-тестирование",
        TestFamily.NetSec => "Сеть и безопасность",
        _ => family.ToString()
    };
    private static string FormatOrFallback(string? value) => string.IsNullOrWhiteSpace(value) ? "не указана" : value;
    private static string FormatNumericOrDash(int value) => value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "не используется";
    private static string FormatScreenshotsPolicy(object policy)
    {
        var text = policy.ToString() ?? string.Empty;
        return text switch
        {
            "Always" => "Всегда",
            "OnError" => "Только при ошибке",
            "Off" => "Выключены",
            _ => text
        };
    }
    private static string TranslateResultKind(string kind) => kind switch
    {
        "Run" => "Запуск",
        "Step" => "Шаг",
        "Check" => "Проверка",
        "Endpoint" => "HTTP endpoint",
        "Asset" => "Ресурс",
        "Probe" => "Проба",
        "Timing" => "Тайминг",
        "PreflightCheck" => "Предварительная проверка",
        _ => kind
    };
    private static string TranslateErrorKind(string kind) => kind switch
    {
        "Timeout" => "Таймаут",
        "AssertFailed" => "Провал условия",
        "Network" => "Сетевая ошибка",
        "Http" => "HTTP-ошибка",
        "Exception" => "Исключение",
        "Playwright" => "Playwright",
        _ => kind
    };
    private static double ClampPercent(double value) => Math.Clamp(value, 0, 100);
    private static string Trim(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Length > 220 ? value[..220] + "..." : value;
    private static string Escape(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    private static int GetScenarioStepIndex(StepResult step)
    {
        if (string.IsNullOrWhiteSpace(step.DetailsJson)) return step.ItemIndex ?? 0;
        try
        {
            using var doc = JsonDocument.Parse(step.DetailsJson);
            return ReadInt(doc.RootElement, "stepIndex") ?? step.ItemIndex ?? 0;
        }
        catch
        {
            return step.ItemIndex ?? 0;
        }
    }

    private static int? ParsePort(string name) => name.LastIndexOf(':') is var index && index >= 0 && int.TryParse(name[(index + 1)..].Trim(), out var port) ? port : null;
    private static string? ReadString(JsonElement? element, string propertyName) => element is not { } value ? null : ReadString(value, propertyName);
    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(), _ => null };
    }
    private static int? ReadInt(JsonElement? element, string propertyName) => element is not { } value ? null : ReadInt(value, propertyName);
    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    }
    private static double? ReadDouble(JsonElement? element, string propertyName) => element is not { } value ? null : ReadDouble(value, propertyName);
    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }
    private static bool? ReadBool(JsonElement? element, string propertyName) => element is not { } value ? null : ReadBool(value, propertyName);
    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }
    private static List<string> ReadStringList(JsonElement element, string propertyName) => ReadArray(element, propertyName).Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    private static List<JsonElement> ReadArray(JsonElement? element, string propertyName) => element is not { } value ? new List<JsonElement>() : ReadArray(value, propertyName);
    private static List<JsonElement> ReadArray(JsonElement element, string propertyName) => !TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array ? new List<JsonElement>() : value.EnumerateArray().ToList();
    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty(propertyName, out value)) return true;
        foreach (var property in element.EnumerateObject()) if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        return false;
    }

    private sealed record RegressionComparisonView(string? BaselineRunId, int ComparedSteps, int ChangedSteps, int NewErrors, int ResolvedErrors, string Message);
}
