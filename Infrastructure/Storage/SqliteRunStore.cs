using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

/// <summary>
/// SQLite-хранилище тестов, профилей и прогонов.
/// </summary>
public class SqliteRunStore : IRunStore, ITestCaseRepository, IRunProfileRepository, ITestRunRepository, IRunItemRepository, IArtifactRepository
{
    private readonly string _dbPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteRunStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = CreateConnection();
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS TestCases (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                ModuleType TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CurrentVersion INTEGER NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS TestCaseVersions (
                Id TEXT PRIMARY KEY,
                TestCaseId TEXT NOT NULL,
                VersionNumber INTEGER NOT NULL,
                ChangedAt TEXT NOT NULL,
                ChangeNote TEXT,
                PayloadJson TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS RunProfiles (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Parallelism INTEGER NOT NULL,
                Mode TEXT NOT NULL,
                Iterations INTEGER NOT NULL,
                DurationSeconds INTEGER NOT NULL,
                TimeoutSeconds INTEGER NOT NULL,
                Headless INTEGER NOT NULL,
                ScreenshotsPolicy TEXT NOT NULL,
                HtmlReportEnabled INTEGER NOT NULL,
                TelegramEnabled INTEGER NOT NULL,
                PreflightEnabled INTEGER NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS TestRuns (
                RunId TEXT PRIMARY KEY,
                TestCaseId TEXT NOT NULL,
                TestCaseVersion INTEGER NOT NULL,
                TestName TEXT NOT NULL,
                ModuleType TEXT NOT NULL,
                ModuleName TEXT NOT NULL,
                ProfileSnapshotJson TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT,
                Status TEXT NOT NULL,
                SummaryJson TEXT
            );",
            @"CREATE TABLE IF NOT EXISTS RunItems (
                Id TEXT PRIMARY KEY,
                RunId TEXT NOT NULL,
                ItemType TEXT NOT NULL,
                ItemKey TEXT NOT NULL,
                Status TEXT NOT NULL,
                DurationMs REAL NOT NULL,
                ErrorMessage TEXT,
                ExtraJson TEXT
            );",
            @"CREATE TABLE IF NOT EXISTS Artifacts (
                Id TEXT PRIMARY KEY,
                RunId TEXT NOT NULL,
                ArtifactType TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS TelegramNotifications (
                Id TEXT PRIMARY KEY,
                RunId TEXT NOT NULL,
                SentAt TEXT NOT NULL,
                Status TEXT NOT NULL,
                ErrorMessage TEXT
            );"
        };

        foreach (var sql in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<TestCase>> GetTestCasesAsync(string moduleType, CancellationToken ct)
    {
        var result = new List<TestCase>();
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Name, Description, ModuleType, CreatedAt, UpdatedAt, CurrentVersion
                                FROM TestCases WHERE ModuleType = $moduleType ORDER BY UpdatedAt DESC";
        command.Parameters.AddWithValue("$moduleType", moduleType);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new TestCase
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ModuleType = reader.GetString(3),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                CurrentVersion = reader.GetInt32(6)
            });
        }

        return result;
    }

    public async Task<TestCaseVersion?> GetTestCaseVersionAsync(Guid testCaseId, int version, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, TestCaseId, VersionNumber, ChangedAt, ChangeNote, PayloadJson
                                FROM TestCaseVersions WHERE TestCaseId = $testCaseId AND VersionNumber = $version";
        command.Parameters.AddWithValue("$testCaseId", testCaseId.ToString());
        command.Parameters.AddWithValue("$version", version);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new TestCaseVersion
            {
                Id = Guid.Parse(reader.GetString(0)),
                TestCaseId = Guid.Parse(reader.GetString(1)),
                VersionNumber = reader.GetInt32(2),
                ChangedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                ChangeNote = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                PayloadJson = reader.GetString(5)
            };
        }

        return null;
    }

    public async Task<TestCase?> GetTestCaseAsync(Guid testCaseId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Name, Description, ModuleType, CreatedAt, UpdatedAt, CurrentVersion
                                FROM TestCases WHERE Id = $id";
        command.Parameters.AddWithValue("$id", testCaseId.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new TestCase
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ModuleType = reader.GetString(3),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                CurrentVersion = reader.GetInt32(6)
            };
        }

        return null;
    }

    public Task<IReadOnlyList<TestCase>> ListAsync(string moduleType, CancellationToken ct)
        => GetTestCasesAsync(moduleType, ct);

    Task<TestCase?> ITestCaseRepository.GetAsync(Guid testCaseId, CancellationToken ct)
        => GetTestCaseAsync(testCaseId, ct);

    public Task<TestCaseVersion?> GetVersionAsync(Guid testCaseId, int version, CancellationToken ct)
        => GetTestCaseVersionAsync(testCaseId, version, ct);

    public Task<TestCase> SaveVersionAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct)
        => SaveTestCaseAsync(name, description, moduleType, payloadJson, changeNote, ct);

    public async Task SetCurrentVersionAsync(Guid testCaseId, int version, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE TestCases SET CurrentVersion = $version, UpdatedAt = $updatedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", testCaseId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<TestCase> SaveTestCaseAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var lookupCommand = connection.CreateCommand();
        lookupCommand.CommandText = @"SELECT Id, CurrentVersion FROM TestCases WHERE Name = $name AND ModuleType = $moduleType";
        lookupCommand.Parameters.AddWithValue("$name", name);
        lookupCommand.Parameters.AddWithValue("$moduleType", moduleType);
        lookupCommand.Transaction = transaction;
        Guid? existingId = null;
        var currentVersion = 0;
        await using (var reader = await lookupCommand.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                existingId = Guid.Parse(reader.GetString(0));
                currentVersion = reader.GetInt32(1);
            }
        }

        var now = DateTimeOffset.UtcNow;
        TestCase testCase;
        if (existingId == null)
        {
            var newId = Guid.NewGuid();
            var insertCase = connection.CreateCommand();
            insertCase.CommandText = @"INSERT INTO TestCases (Id, Name, Description, ModuleType, CreatedAt, UpdatedAt, CurrentVersion)
                                       VALUES ($id, $name, $description, $moduleType, $createdAt, $updatedAt, $version)";
            insertCase.Parameters.AddWithValue("$id", newId.ToString());
            insertCase.Parameters.AddWithValue("$name", name);
            insertCase.Parameters.AddWithValue("$description", description);
            insertCase.Parameters.AddWithValue("$moduleType", moduleType);
            insertCase.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            insertCase.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            insertCase.Parameters.AddWithValue("$version", 1);
            insertCase.Transaction = transaction;
            await insertCase.ExecuteNonQueryAsync(ct);

            existingId = newId;
            currentVersion = 0;
            testCase = new TestCase
            {
                Id = newId,
                Name = name,
                Description = description,
                ModuleType = moduleType,
                CreatedAt = now,
                UpdatedAt = now,
                CurrentVersion = 1
            };
        }
        else
        {
            var updateCase = connection.CreateCommand();
            updateCase.CommandText = @"UPDATE TestCases SET Description = $description, UpdatedAt = $updatedAt, CurrentVersion = $version
                                       WHERE Id = $id";
            updateCase.Parameters.AddWithValue("$description", description);
            updateCase.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            updateCase.Parameters.AddWithValue("$version", currentVersion + 1);
            updateCase.Parameters.AddWithValue("$id", existingId.Value.ToString());
            updateCase.Transaction = transaction;
            await updateCase.ExecuteNonQueryAsync(ct);

            testCase = new TestCase
            {
                Id = existingId.Value,
                Name = name,
                Description = description,
                ModuleType = moduleType,
                CreatedAt = now,
                UpdatedAt = now,
                CurrentVersion = currentVersion + 1
            };
        }

        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = @"INSERT INTO TestCaseVersions (Id, TestCaseId, VersionNumber, ChangedAt, ChangeNote, PayloadJson)
                                       VALUES ($id, $testCaseId, $version, $changedAt, $changeNote, $payload)";
        versionCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        versionCommand.Parameters.AddWithValue("$testCaseId", existingId.Value.ToString());
        versionCommand.Parameters.AddWithValue("$version", currentVersion + 1);
        versionCommand.Parameters.AddWithValue("$changedAt", now.ToString("O"));
        versionCommand.Parameters.AddWithValue("$changeNote", changeNote);
        versionCommand.Parameters.AddWithValue("$payload", payloadJson);
        versionCommand.Transaction = transaction;
        await versionCommand.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        return testCase;
    }

    public async Task DeleteTestCaseAsync(Guid testCaseId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var deleteVersions = connection.CreateCommand();
        deleteVersions.CommandText = @"DELETE FROM TestCaseVersions WHERE TestCaseId = $id";
        deleteVersions.Parameters.AddWithValue("$id", testCaseId.ToString());
        deleteVersions.Transaction = transaction;
        await deleteVersions.ExecuteNonQueryAsync(ct);

        var deleteCase = connection.CreateCommand();
        deleteCase.CommandText = @"DELETE FROM TestCases WHERE Id = $id";
        deleteCase.Parameters.AddWithValue("$id", testCaseId.ToString());
        deleteCase.Transaction = transaction;
        await deleteCase.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
    }

    Task ITestCaseRepository.DeleteAsync(Guid testCaseId, CancellationToken ct)
        => DeleteTestCaseAsync(testCaseId, ct);

    public async Task<IReadOnlyList<RunProfile>> GetRunProfilesAsync(CancellationToken ct)
    {
        var result = new List<RunProfile>();
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Name, Parallelism, Mode, Iterations, DurationSeconds, TimeoutSeconds,
                                       Headless, ScreenshotsPolicy, HtmlReportEnabled, TelegramEnabled, PreflightEnabled
                                FROM RunProfiles ORDER BY Name";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new RunProfile
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Parallelism = reader.GetInt32(2),
                Mode = Enum.Parse<RunMode>(reader.GetString(3)),
                Iterations = reader.GetInt32(4),
                DurationSeconds = reader.GetInt32(5),
                TimeoutSeconds = reader.GetInt32(6),
                Headless = reader.GetInt32(7) == 1,
                ScreenshotsPolicy = Enum.Parse<ScreenshotsPolicy>(reader.GetString(8)),
                HtmlReportEnabled = reader.GetInt32(9) == 1,
                TelegramEnabled = reader.GetInt32(10) == 1,
                PreflightEnabled = reader.GetInt32(11) == 1
            });
        }

        return result;
    }

    public async Task<RunProfile?> GetRunProfileAsync(Guid profileId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, Name, Parallelism, Mode, Iterations, DurationSeconds, TimeoutSeconds,
                                       Headless, ScreenshotsPolicy, HtmlReportEnabled, TelegramEnabled, PreflightEnabled
                                FROM RunProfiles WHERE Id = $id";
        command.Parameters.AddWithValue("$id", profileId.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new RunProfile
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Parallelism = reader.GetInt32(2),
                Mode = Enum.Parse<RunMode>(reader.GetString(3)),
                Iterations = reader.GetInt32(4),
                DurationSeconds = reader.GetInt32(5),
                TimeoutSeconds = reader.GetInt32(6),
                Headless = reader.GetInt32(7) == 1,
                ScreenshotsPolicy = Enum.Parse<ScreenshotsPolicy>(reader.GetString(8)),
                HtmlReportEnabled = reader.GetInt32(9) == 1,
                TelegramEnabled = reader.GetInt32(10) == 1,
                PreflightEnabled = reader.GetInt32(11) == 1
            };
        }

        return null;
    }

    public Task<IReadOnlyList<RunProfile>> ListAsync(CancellationToken ct)
        => GetRunProfilesAsync(ct);

    Task<RunProfile?> IRunProfileRepository.GetAsync(Guid profileId, CancellationToken ct)
        => GetRunProfileAsync(profileId, ct);

    public Task<RunProfile> SaveAsync(RunProfile profile, CancellationToken ct)
        => SaveRunProfileAsync(profile, ct);

    public async Task<RunProfile> SaveRunProfileAsync(RunProfile profile, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        var storedId = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id;
        if (profile.Id == Guid.Empty)
        {
            command.CommandText = @"INSERT INTO RunProfiles (Id, Name, Parallelism, Mode, Iterations, DurationSeconds, TimeoutSeconds,
                                       Headless, ScreenshotsPolicy, HtmlReportEnabled, TelegramEnabled, PreflightEnabled)
                                VALUES ($id, $name, $parallelism, $mode, $iterations, $duration, $timeout,
                                        $headless, $screenshotsPolicy, $html, $telegram, $preflight)";
        }
        else
        {
            command.CommandText = @"UPDATE RunProfiles
                                    SET Name = $name, Parallelism = $parallelism, Mode = $mode, Iterations = $iterations,
                                        DurationSeconds = $duration, TimeoutSeconds = $timeout, Headless = $headless,
                                        ScreenshotsPolicy = $screenshotsPolicy, HtmlReportEnabled = $html,
                                        TelegramEnabled = $telegram, PreflightEnabled = $preflight
                                    WHERE Id = $id";
        }

        command.Parameters.AddWithValue("$id", storedId.ToString());
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$parallelism", profile.Parallelism);
        command.Parameters.AddWithValue("$mode", profile.Mode.ToString());
        command.Parameters.AddWithValue("$iterations", profile.Iterations);
        command.Parameters.AddWithValue("$duration", profile.DurationSeconds);
        command.Parameters.AddWithValue("$timeout", profile.TimeoutSeconds);
        command.Parameters.AddWithValue("$headless", profile.Headless ? 1 : 0);
        command.Parameters.AddWithValue("$screenshotsPolicy", profile.ScreenshotsPolicy.ToString());
        command.Parameters.AddWithValue("$html", profile.HtmlReportEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$telegram", profile.TelegramEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$preflight", profile.PreflightEnabled ? 1 : 0);
        await command.ExecuteNonQueryAsync(ct);

        return new RunProfile
        {
            Id = storedId,
            Name = profile.Name,
            Parallelism = profile.Parallelism,
            Mode = profile.Mode,
            Iterations = profile.Iterations,
            DurationSeconds = profile.DurationSeconds,
            TimeoutSeconds = profile.TimeoutSeconds,
            Headless = profile.Headless,
            ScreenshotsPolicy = profile.ScreenshotsPolicy,
            HtmlReportEnabled = profile.HtmlReportEnabled,
            TelegramEnabled = profile.TelegramEnabled,
            PreflightEnabled = profile.PreflightEnabled
        };
    }

    public async Task DeleteRunProfileAsync(Guid profileId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"DELETE FROM RunProfiles WHERE Id = $id";
        command.Parameters.AddWithValue("$id", profileId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    Task IRunProfileRepository.DeleteAsync(Guid profileId, CancellationToken ct)
        => DeleteRunProfileAsync(profileId, ct);

    public async Task CreateRunAsync(TestRun run, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO TestRuns (RunId, TestCaseId, TestCaseVersion, TestName, ModuleType, ModuleName,
                                     ProfileSnapshotJson, StartedAt, FinishedAt, Status, SummaryJson)
                                VALUES ($runId, $testCaseId, $testCaseVersion, $testName, $moduleType, $moduleName,
                                        $profileSnapshot, $startedAt, $finishedAt, $status, $summaryJson)";
        command.Parameters.AddWithValue("$runId", run.RunId);
        command.Parameters.AddWithValue("$testCaseId", run.TestCaseId.ToString());
        command.Parameters.AddWithValue("$testCaseVersion", run.TestCaseVersion);
        command.Parameters.AddWithValue("$testName", run.TestName);
        command.Parameters.AddWithValue("$moduleType", run.ModuleType);
        command.Parameters.AddWithValue("$moduleName", run.ModuleName);
        command.Parameters.AddWithValue("$profileSnapshot", run.ProfileSnapshotJson);
        command.Parameters.AddWithValue("$startedAt", run.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$finishedAt", run.FinishedAt?.ToString("O"));
        command.Parameters.AddWithValue("$status", run.Status);
        command.Parameters.AddWithValue("$summaryJson", string.IsNullOrWhiteSpace(run.SummaryJson) ? DBNull.Value : run.SummaryJson);
        await command.ExecuteNonQueryAsync(ct);
    }

    public Task CreateAsync(TestRun run, CancellationToken ct)
        => CreateRunAsync(run, ct);

    public async Task UpdateRunAsync(TestRun run, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE TestRuns
                                SET FinishedAt = $finishedAt, Status = $status, SummaryJson = $summaryJson
                                WHERE RunId = $runId";
        command.Parameters.AddWithValue("$finishedAt", run.FinishedAt?.ToString("O"));
        command.Parameters.AddWithValue("$status", run.Status);
        command.Parameters.AddWithValue("$summaryJson", string.IsNullOrWhiteSpace(run.SummaryJson) ? DBNull.Value : run.SummaryJson);
        command.Parameters.AddWithValue("$runId", run.RunId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateStatusAsync(string runId, string status, DateTimeOffset? finishedAt, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE TestRuns SET Status = $status, FinishedAt = $finishedAt WHERE RunId = $runId";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$finishedAt", finishedAt?.ToString("O"));
        command.Parameters.AddWithValue("$runId", runId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateSummaryAsync(string runId, string summaryJson, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE TestRuns SET SummaryJson = $summary WHERE RunId = $runId";
        command.Parameters.AddWithValue("$summary", string.IsNullOrWhiteSpace(summaryJson) ? DBNull.Value : summaryJson);
        command.Parameters.AddWithValue("$runId", runId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AddRunItemsAsync(IEnumerable<RunItem> items, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        foreach (var item in items)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO RunItems (Id, RunId, ItemType, ItemKey, Status, DurationMs, ErrorMessage, ExtraJson)
                                    VALUES ($id, $runId, $itemType, $itemKey, $status, $duration, $errorMessage, $extra)";
            command.Parameters.AddWithValue("$id", item.Id.ToString());
            command.Parameters.AddWithValue("$runId", item.RunId);
            command.Parameters.AddWithValue("$itemType", item.ItemType);
            command.Parameters.AddWithValue("$itemKey", item.ItemKey);
            command.Parameters.AddWithValue("$status", item.Status);
            command.Parameters.AddWithValue("$duration", item.DurationMs);
            command.Parameters.AddWithValue("$errorMessage", item.ErrorMessage ?? string.Empty);
            command.Parameters.AddWithValue("$extra", item.ExtraJson ?? string.Empty);
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public Task AppendAsync(IEnumerable<RunItem> items, CancellationToken ct)
        => AddRunItemsAsync(items, ct);

    async Task<IReadOnlyList<RunItem>> IRunItemRepository.ListByRunAsync(string runId, CancellationToken ct)
    {
        var items = new List<RunItem>();
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, RunId, ItemType, ItemKey, Status, DurationMs, ErrorMessage, ExtraJson
                                FROM RunItems WHERE RunId = $runId";
        command.Parameters.AddWithValue("$runId", runId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new RunItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                RunId = reader.GetString(1),
                ItemType = reader.GetString(2),
                ItemKey = reader.GetString(3),
                Status = reader.GetString(4),
                DurationMs = reader.GetDouble(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                ExtraJson = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return items;
    }

    public async Task AddArtifactsAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        foreach (var artifact in artifacts)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO Artifacts (Id, RunId, ArtifactType, RelativePath, CreatedAt)
                                    VALUES ($id, $runId, $type, $path, $createdAt)";
            command.Parameters.AddWithValue("$id", artifact.Id.ToString());
            command.Parameters.AddWithValue("$runId", artifact.RunId);
            command.Parameters.AddWithValue("$type", artifact.ArtifactType);
            command.Parameters.AddWithValue("$path", artifact.RelativePath);
            command.Parameters.AddWithValue("$createdAt", artifact.CreatedAt.ToString("O"));
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public Task AddAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct)
        => AddArtifactsAsync(artifacts, ct);

    async Task<IReadOnlyList<ArtifactRecord>> IArtifactRepository.ListByRunAsync(string runId, CancellationToken ct)
    {
        var artifacts = new List<ArtifactRecord>();
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id, RunId, ArtifactType, RelativePath, CreatedAt
                                FROM Artifacts WHERE RunId = $runId";
        command.Parameters.AddWithValue("$runId", runId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            artifacts.Add(new ArtifactRecord
            {
                Id = Guid.Parse(reader.GetString(0)),
                RunId = reader.GetString(1),
                ArtifactType = reader.GetString(2),
                RelativePath = reader.GetString(3),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
            });
        }

        return artifacts;
    }

    public async Task AddTelegramNotificationAsync(TelegramNotification notification, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO TelegramNotifications (Id, RunId, SentAt, Status, ErrorMessage)
                                VALUES ($id, $runId, $sentAt, $status, $error)";
        command.Parameters.AddWithValue("$id", notification.Id.ToString());
        command.Parameters.AddWithValue("$runId", notification.RunId);
        command.Parameters.AddWithValue("$sentAt", notification.SentAt.ToString("O"));
        command.Parameters.AddWithValue("$status", notification.Status);
        command.Parameters.AddWithValue("$error", notification.ErrorMessage ?? string.Empty);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TestRunSummary>> QueryRunsAsync(RunQuery query, CancellationToken ct)
    {
        var result = new List<TestRunSummary>();
        await using var connection = CreateConnection();
        await using var command = connection.CreateCommand();
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.ModuleType))
        {
            filters.Add("ModuleType = $moduleType");
            command.Parameters.AddWithValue("$moduleType", query.ModuleType);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add("Status = $status");
            command.Parameters.AddWithValue("$status", query.Status);
        }
        if (query.From.HasValue)
        {
            filters.Add("StartedAt >= $from");
            command.Parameters.AddWithValue("$from", query.From.Value.ToString("O"));
        }
        if (query.To.HasValue)
        {
            filters.Add("StartedAt <= $to");
            command.Parameters.AddWithValue("$to", query.To.Value.ToString("O"));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filters.Add("(TestName LIKE $search OR RunId LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{query.Search}%");
        }

        var whereClause = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;
        command.CommandText = $@"SELECT RunId, StartedAt, TestName, ModuleName, Status, SummaryJson, ModuleType
                                 FROM TestRuns {whereClause} ORDER BY StartedAt DESC";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var summaryJson = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            var summary = ParseSummary(summaryJson);
            result.Add(new TestRunSummary
            {
                RunId = reader.GetString(0),
                StartedAt = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                TestName = reader.GetString(2),
                ModuleName = reader.GetString(3),
                Status = reader.GetString(4),
                DurationMs = summary.TotalDurationMs,
                FailedItems = summary.FailedItems,
                KeyMetrics = summary.KeyMetrics,
                ModuleType = reader.GetString(6)
            });
        }

        return result;
    }

    public Task<IReadOnlyList<TestRunSummary>> ListAsync(RunQuery query, CancellationToken ct)
        => QueryRunsAsync(query, ct);

    public async Task<TestRunDetail?> GetRunDetailAsync(string runId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        TestRun? run = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT RunId, TestCaseId, TestCaseVersion, TestName, ModuleType, ModuleName,
                                           ProfileSnapshotJson, StartedAt, FinishedAt, Status, SummaryJson
                                    FROM TestRuns WHERE RunId = $runId";
            command.Parameters.AddWithValue("$runId", runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                run = new TestRun
                {
                    RunId = reader.GetString(0),
                    TestCaseId = Guid.Parse(reader.GetString(1)),
                    TestCaseVersion = reader.GetInt32(2),
                    TestName = reader.GetString(3),
                    ModuleType = reader.GetString(4),
                    ModuleName = reader.GetString(5),
                    ProfileSnapshotJson = reader.GetString(6),
                    StartedAt = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                    FinishedAt = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
                    Status = reader.GetString(9),
                    SummaryJson = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                };
            }
        }

        if (run == null)
        {
            return null;
        }

        var items = new List<RunItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT Id, RunId, ItemType, ItemKey, Status, DurationMs, ErrorMessage, ExtraJson
                                    FROM RunItems WHERE RunId = $runId";
            command.Parameters.AddWithValue("$runId", runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new RunItem
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    RunId = reader.GetString(1),
                    ItemType = reader.GetString(2),
                    ItemKey = reader.GetString(3),
                    Status = reader.GetString(4),
                    DurationMs = reader.GetDouble(5),
                    ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ExtraJson = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }

        var artifacts = new List<ArtifactRecord>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT Id, RunId, ArtifactType, RelativePath, CreatedAt
                                    FROM Artifacts WHERE RunId = $runId";
            command.Parameters.AddWithValue("$runId", runId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                artifacts.Add(new ArtifactRecord
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    RunId = reader.GetString(1),
                    ArtifactType = reader.GetString(2),
                    RelativePath = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
                });
            }
        }

        return new TestRunDetail
        {
            Run = run,
            Items = items,
            Artifacts = artifacts
        };
    }

    public Task<TestRunDetail?> GetByIdAsync(string runId, CancellationToken ct)
        => GetRunDetailAsync(runId, ct);

    public async Task DeleteRunAsync(string runId, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var deleteItems = connection.CreateCommand();
        deleteItems.CommandText = @"DELETE FROM RunItems WHERE RunId = $runId";
        deleteItems.Parameters.AddWithValue("$runId", runId);
        deleteItems.Transaction = transaction;
        await deleteItems.ExecuteNonQueryAsync(ct);

        var deleteArtifacts = connection.CreateCommand();
        deleteArtifacts.CommandText = @"DELETE FROM Artifacts WHERE RunId = $runId";
        deleteArtifacts.Parameters.AddWithValue("$runId", runId);
        deleteArtifacts.Transaction = transaction;
        await deleteArtifacts.ExecuteNonQueryAsync(ct);

        var deleteNotifications = connection.CreateCommand();
        deleteNotifications.CommandText = @"DELETE FROM TelegramNotifications WHERE RunId = $runId";
        deleteNotifications.Parameters.AddWithValue("$runId", runId);
        deleteNotifications.Transaction = transaction;
        await deleteNotifications.ExecuteNonQueryAsync(ct);

        var deleteRun = connection.CreateCommand();
        deleteRun.CommandText = @"DELETE FROM TestRuns WHERE RunId = $runId";
        deleteRun.Parameters.AddWithValue("$runId", runId);
        deleteRun.Transaction = transaction;
        await deleteRun.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }

    private SummaryPayload ParseSummary(string summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
        {
            return new SummaryPayload();
        }

        try
        {
            var summary = JsonSerializer.Deserialize<SummaryPayload>(summaryJson, _jsonOptions);
            return summary ?? new SummaryPayload();
        }
        catch (JsonException)
        {
            return new SummaryPayload();
        }
    }

    private class SummaryPayload
    {
        public double TotalDurationMs { get; set; }
        public int FailedItems { get; set; }
        public double AverageMs { get; set; }
        public double P95Ms { get; set; }
        public double P99Ms { get; set; }

        public string KeyMetrics =>
            AverageMs > 0
                ? $"avg {AverageMs:F1} ms, p95 {P95Ms:F1} ms"
                : string.Empty;
    }
}
