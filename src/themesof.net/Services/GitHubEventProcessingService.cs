using System.Collections.Concurrent;

using Azure;
using Azure.Storage.Blobs;

using Terrajobst.GitHubEvents;

namespace ThemesOfDotNet.Services;

public sealed class GitHubEventProcessingService : IGitHubEventProcessor, IHostedService
{
    private readonly ILogger<GitHubEventProcessingService> _logger;
    private readonly GitHubCrawlerService _crawlerService;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<GitHubEventMessage> _messages = new();
    private readonly AutoResetEvent _dataAvailable = new(false);
    private readonly Processor _processor;

    private Task? _workerTask;

    public GitHubEventProcessingService(ILogger<GitHubEventProcessingService> logger,
                                        IConfiguration configuration,
                                        GitHubCrawlerService crawlerService)
    {
        _logger = logger;
        _crawlerService = crawlerService;
        _processor = new Processor(logger, configuration, crawlerService);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _workerTask = Task.Run(async () =>
        {
            _logger.LogInformation("GitHub event processing started");
            try
            {
                await RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GitHub event processing was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GitHub event processing");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return _workerTask ?? Task.CompletedTask;
    }

    public void Process(GitHubEventMessage message)
    {
        _messages.Enqueue(message);
        _dataAvailable.Set();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var waitHandles = new[]
        {
            _dataAvailable,
            cancellationToken.WaitHandle
        };

        while (true)
        {
            WaitHandle.WaitAny(waitHandles);
            cancellationToken.ThrowIfCancellationRequested();

            while (_messages.TryDequeue(out var message))
                await _processor.ProcessAsync(message);

            await _crawlerService.Crawler.StoreAsync();
        }
    }

    private sealed class Processor
    {
        private readonly ILogger _logger;
        private readonly GitHubCrawlerService _crawlerService;
        private readonly string _connectionString;

        public Processor(ILogger logger, IConfiguration configuration, GitHubCrawlerService crawlerService)
        {
            _logger = logger;
            _crawlerService = crawlerService;
            _connectionString = configuration["BlobConnectionString"];
        }

        public async Task ProcessAsync(GitHubEventMessage message)
        {
            _logger.LogInformation("Processing message {message}", message);
            try
            {
                // await StoreMessageAsync(message);
                if (!await DispatchAsync(message))
                    _logger.LogInformation("Unhandled message {message}", message);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {message}", message);
            }
        }

        private Task<bool> DispatchAsync(GitHubEventMessage message)
        {
            if (message.Kind.IsEvent(GitHubEventMessageKind.EventRepository))
            {
                var owner = message.Body.Organization.Login;
                var repoName = message.Body.Repository.Name;

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.RepositoryCreated:
                        // Don't care
                        break;
                    case GitHubEventMessageKind.RepositoryDeleted:
                        return _crawlerService.Crawler.DeleteRepoAsync(owner, repoName);
                    case GitHubEventMessageKind.RepositoryArchived:
                    case GitHubEventMessageKind.RepositoryUnarchived:
                    case GitHubEventMessageKind.RepositoryPublicized:
                    case GitHubEventMessageKind.RepositoryPrivatized:
                        return _crawlerService.Crawler.UpdateRepoAsync(owner, repoName);
                    case GitHubEventMessageKind.RepositoryRenamed:
                        var fromRepoName = message.Body.Changes.AdditionalData["repository"]!["name"]!.Value<string>("from")!;
                        return _crawlerService.Crawler.UpdateRepoAsync(owner, fromRepoName);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventLabel))
            {
                var owner = message.Body.Organization.Login;
                var repoName = message.Body.Repository.Name;
                var labelId = message.Body.Label.Id;
                var labelName = message.Body.Label.Name;

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.LabelCreated:
                    case GitHubEventMessageKind.LabelEdited:
                        return _crawlerService.Crawler.UpdateLabelAsync(owner, repoName, labelId, labelName);
                    case GitHubEventMessageKind.LabelDeleted:
                        return _crawlerService.Crawler.DeleteLabelAsync(owner, repoName, labelId);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventMilestone))
            {
                var owner = message.Body.Organization.Login;
                var repoName = message.Body.Repository.Name;
                var milestoneId = message.Body.Milestone.Id;
                var milestoneNumber = message.Body.Milestone.Number;

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.MilestoneCreated:
                    case GitHubEventMessageKind.MilestoneEdited:
                    case GitHubEventMessageKind.MilestoneClosed:
                    case GitHubEventMessageKind.MilestoneOpened:
                        return _crawlerService.Crawler.UpdateMilestoneAsync(owner, repoName, milestoneId, milestoneNumber);
                    case GitHubEventMessageKind.MilestoneDeleted:
                        return _crawlerService.Crawler.DeleteMilestoneAsync(owner, repoName, milestoneId);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventIssue))
            {
                var owner = message.Body.Organization.Login;
                var repoName = message.Body.Repository.Name;
                var issueNumber = message.Body.Issue.Number;
                var labels = message.Body.Issue.Labels.Select(l => l.Name);

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.IssueOpened:
                    case GitHubEventMessageKind.IssueReopened:
                    case GitHubEventMessageKind.IssueClosed:
                    case GitHubEventMessageKind.IssueEdited:
                    case GitHubEventMessageKind.IssueAssigned:
                    case GitHubEventMessageKind.IssueUnassigned:
                    case GitHubEventMessageKind.IssueLabeled:
                    case GitHubEventMessageKind.IssueUnlabeled:
                    case GitHubEventMessageKind.IssueMilestoned:
                    case GitHubEventMessageKind.IssueDemilestoned:
                    case GitHubEventMessageKind.IssueLocked:
                    case GitHubEventMessageKind.IssueUnlocked:
                        return _crawlerService.Crawler.UpdateIssueAsync(owner, repoName, issueNumber, labels);
                    case GitHubEventMessageKind.IssueTransferred:
                        var newOwner = message.Body.Changes.NewRepository.Owner.Login;
                        var newRepoName = message.Body.Changes.NewRepository.Name;
                        var newIssueNumber = message.Body.Changes.NewIssue.Number;
                        return _crawlerService.Crawler.TransferIssueAsync(owner, repoName, issueNumber,
                                                                          newOwner, newRepoName, newIssueNumber);
                    case GitHubEventMessageKind.IssueDeleted:
                        return _crawlerService.Crawler.DeleteIssueAsync(owner, repoName, issueNumber);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventPullRequest))
            {
                var owner = message.Body.Organization.Login;
                var repoName = message.Body.Repository.Name;
                var issueNumber = message.Body.PullRequest.Number;
                var labels = message.Body.PullRequest.Labels.Select(l => l.Name);

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.PullRequestOpened:
                    case GitHubEventMessageKind.PullRequestReopened:
                    case GitHubEventMessageKind.PullRequestClosed:
                    case GitHubEventMessageKind.PullRequestEdited:
                    case GitHubEventMessageKind.PullRequestAssigned:
                    case GitHubEventMessageKind.PullRequestUnassigned:
                    case GitHubEventMessageKind.PullRequestLabeled:
                    case GitHubEventMessageKind.PullRequestUnlabeled:
                    case GitHubEventMessageKind.PullRequestLocked:
                    case GitHubEventMessageKind.PullRequestUnlocked:
                    case GitHubEventMessageKind.PullRequestReadyForReview:
                    case GitHubEventMessageKind.PullRequestConvertedToDraft:
                        return _crawlerService.Crawler.UpdateIssueAsync(owner, repoName, issueNumber, labels);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventProject))
            {
                var projectId = message.Body.Project.Id;

                switch (message.Kind)
                {
                    case GitHubEventMessageKind.ProjectCreated:
                    case GitHubEventMessageKind.ProjectEdited:
                    case GitHubEventMessageKind.ProjectReopened:
                        return _crawlerService.Crawler.UpdateProjectAsync(projectId);
                    case GitHubEventMessageKind.ProjectDeleted:
                        return _crawlerService.Crawler.DeleteProjectAsync(projectId);
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventProjectColumn))
            {
                var projectId = GetProjectIdFromUrl(message.Body.ProjectColumn.ProjectUrl);
                var columnId = message.Body.ProjectColumn.Id;

                if (projectId is null)
                {
                    _logger.LogWarning("Couldn't extract project ID from {messsage}", message);
                }
                else
                {
                    switch (message.Kind)
                    {
                        case GitHubEventMessageKind.ProjectColumnCreated:
                        case GitHubEventMessageKind.ProjectColumnEdited:
                            return _crawlerService.Crawler.UpdateProjectColumnAsync(projectId.Value, columnId);
                        case GitHubEventMessageKind.ProjectColumnDeleted:
                            return _crawlerService.Crawler.DeleteProjectColumnAsync(projectId.Value, columnId);
                    }
                }
            }

            if (message.Kind.IsEvent(GitHubEventMessageKind.EventProjectCard))
            {
                var projectId = GetProjectIdFromUrl(message.Body.ProjectCard.ProjectUrl);
                var columnId = message.Body.ProjectCard.ColumnId;
                var cardId = message.Body.ProjectCard.Id;

                if (projectId is null)
                {
                    _logger.LogWarning("Couldn't extract project ID from {messsage}", message);
                }
                else
                {
                    switch (message.Kind)
                    {
                        case GitHubEventMessageKind.ProjectCardCreated:
                        case GitHubEventMessageKind.ProjectCardMoved:
                            return _crawlerService.Crawler.UpdateProjectCardAsync(projectId.Value, columnId, cardId);
                        case GitHubEventMessageKind.ProjectCardDeleted:
                            return _crawlerService.Crawler.DeleteProjectCardAsync(projectId.Value, columnId, cardId);
                    }
                }
            }

            _logger.LogInformation("Ignored message {message}", message);
            return Task.FromResult(false);
        }

        private async Task StoreMessageAsync(GitHubEventMessage message)
        {
            var blobName = $"{message.Kind.GetEvent()}/{message.Kind.GetAction()}/{message.Delivery}.txt";
            var client = new BlobClient(_connectionString, "events", blobName);

            try
            {
                await client.UploadAsync(BinaryData.FromString(message.FormatMessage()));
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "BlobAlreadyExists")
            {
                // Ignore
            }
        }

        private static long? GetProjectIdFromUrl(string? projectUrl)
        {
            if (projectUrl is null)
                return null;

            var prefix = @"https://api.github.com/projects/";
            if (!projectUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            var remainder = projectUrl.Substring(prefix.Length);
            if (!long.TryParse(remainder, out var result))
                return null;

            return result;
        }
    }
}
