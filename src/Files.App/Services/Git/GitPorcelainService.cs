// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Dialogs;
using GitPorcelain;
using Microsoft.Extensions.Logging;
using Sentry;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Files.App.Services.Git;

internal sealed class GitPorcelainService : IVersionControlService
{
	private const string GIT_RESOURCE_NAME = "Files:https://github.com";
	private const string GIT_RESOURCE_USERNAME = "Personal Access Token";
	private const string CLIENT_ID_SECRET = Constants.AutomatedWorkflowInjectionKeys.GitHubClientId;
	private const int MAX_NUMBER_OF_BRANCHES = 30;

	private static readonly string _clientId = AppLifecycleHelper.AppEnvironment is AppEnvironment.Dev
		? string.Empty
		: CLIENT_ID_SECRET;

	private readonly StatusCenterViewModel statusCenterViewModel = Ioc.Default.GetRequiredService<StatusCenterViewModel>();
	private readonly ILogger logger = Ioc.Default.GetRequiredService<ILogger<App>>();
	private readonly IDialogService dialogService = Ioc.Default.GetRequiredService<IDialogService>();
	private readonly object availabilityLock = new();

	private Task<GitVersion>? availabilityTask;
	private int activeOperationCount;
	private bool isExecutingGitAction;

	public bool IsExecutingGitAction
	{
		get => isExecutingGitAction;
		private set
		{
			if (isExecutingGitAction == value)
				return;

			isExecutingGitAction = value;
			IsExecutingGitActionChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecutingGitAction)));
		}
	}

	public event PropertyChangedEventHandler? IsExecutingGitActionChanged;
	public event EventHandler? GitFetchCompleted;

	public async Task<string?> GetGitRepositoryPathAsync(
		string? path,
		string? root = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(path) ||
			path.Equals("Home", StringComparison.OrdinalIgnoreCase) ||
			ShellStorageFolder.IsShellPath(path))
		{
			return null;
		}

		try
		{
			await EnsureGitAvailableAsync(cancellationToken);
			var repository = await CreateClient().DiscoverAsync(path, cancellationToken);
			var workingTreePath = repository?.Info.WorkingTreePath;
			if (string.IsNullOrWhiteSpace(workingTreePath))
				return null;

			if (!string.IsNullOrWhiteSpace(root))
			{
				var normalizedRoot = Path.GetFullPath(root);
				var relativePath = Path.GetRelativePath(normalizedRoot, workingTreePath);
				if (relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relativePath == "..")
					return null;
			}

			return workingTreePath;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogDebug(ex, "Git repository discovery failed for {Path}", LogPathHelper.RedactPath(path));
			return null;
		}
	}

	public async Task<string> GetOriginRepositoryNameAsync(
		string? path,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(path))
			return string.Empty;

		try
		{
			var repository = await CreateClient().OpenAsync(path, cancellationToken);
			return (await repository.GetRemotesAsync(cancellationToken))
				.FirstOrDefault()?.RepositoryName ?? string.Empty;
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogDebug(ex, "Could not read Git remotes for {Path}", LogPathHelper.RedactPath(path));
			return string.Empty;
		}
	}

	public async Task<BranchItem[]> GetBranchNames(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return [];

		try
		{
			var repository = await CreateClient().OpenAsync(path);
			var branches = await repository.GetBranchesAsync(new GitBranchQueryOptions
			{
				MaximumPerCategory = MAX_NUMBER_OF_BRANCHES,
			});

			return branches.Select(static branch => new BranchItem(
				branch.Name,
				branch.IsCurrent,
				branch.IsRemote,
				branch.AheadBy,
				branch.BehindBy)).ToArray();
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Could not enumerate Git branches for {Path}", LogPathHelper.RedactPath(path));
			return [];
		}
	}

	public async Task<BranchItem?> GetRepositoryHead(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		try
		{
			var repository = await CreateClient().OpenAsync(path);
			var head = await repository.GetHeadAsync();
			if (head.IsUnborn)
				return null;

			var displayName = head.Name ?? head.ObjectId?[..Math.Min(7, head.ObjectId.Length)];
			return string.IsNullOrWhiteSpace(displayName)
				? null
				: new BranchItem(displayName, true, false, head.AheadBy, head.BehindBy);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogDebug(ex, "Could not read Git HEAD for {Path}", LogPathHelper.RedactPath(path));
			return null;
		}
	}

	public async Task<string?> GetRepositoryHeadName(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		try
		{
			var repository = await CreateClient().OpenAsync(path);
			var head = await repository.GetHeadAsync();
			return head.IsUnborn ? null : head.Name ?? head.ObjectId;
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogDebug(ex, "Could not read Git HEAD for {Path}", LogPathHelper.RedactPath(path));
			return null;
		}
	}

	public async Task<bool> Checkout(string? repositoryPath, string? branch)
	{
		SentrySdk.Metrics.EmitCounter("Triggered git checkout", 1);

		if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(branch))
			return false;

		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			var branches = await repository.GetBranchesAsync();
			var targetBranch = branches.FirstOrDefault(candidate =>
				candidate.Name.Equals(branch, StringComparison.OrdinalIgnoreCase));
			if (targetBranch is null)
				return false;

			var head = await repository.GetHeadAsync();
			var status = await repository.GetStatusAsync();
			var switchOptions = new GitSwitchOptions();
			var shouldPopStash = false;

			if (status.Entries.Any(static entry => entry.Kind is GitStatusEntryKind.Unmerged))
			{
				var dialog = DynamicDialogFactory.GetFor_GitMergeConflicts(targetBranch.Name, head.Name ?? string.Empty);
				await dialog.ShowAsync();

				var selectedOption = dialog.ViewModel.AdditionalData is GitCheckoutOptions option
					? option
					: GitCheckoutOptions.None;
				if (selectedOption is GitCheckoutOptions.None)
					return false;
				if (selectedOption is GitCheckoutOptions.AbortMerge)
					await repository.ResetAsync(mode: GitResetMode.Hard);
			}
			else if (status.Entries.Any(static entry => entry.Kind is not GitStatusEntryKind.Ignored))
			{
				var dialog = DynamicDialogFactory.GetFor_GitCheckoutConflicts(targetBranch.Name, head.Name ?? string.Empty);
				await dialog.ShowAsync();

				var selectedOption = dialog.ViewModel.AdditionalData is GitCheckoutOptions option
					? option
					: GitCheckoutOptions.None;
				switch (selectedOption)
				{
					case GitCheckoutOptions.None:
						return false;
					case GitCheckoutOptions.DiscardChanges:
						switchOptions.DiscardChanges = true;
						break;
					case GitCheckoutOptions.BringChanges:
					case GitCheckoutOptions.StashChanges:
						await repository.PushStashAsync(new GitStashPushOptions { IncludeUntracked = true });
						shouldPopStash = selectedOption is GitCheckoutOptions.BringChanges;
						break;
				}
			}

			var targetName = targetBranch.IsRemote
				? targetBranch.Name[(targetBranch.Name.IndexOf('/') + 1)..]
				: targetBranch.Name;
			await repository.SwitchAsync(targetName, switchOptions);
			if (shouldPopStash)
				await repository.PopStashAsync();

			return true;
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Could not switch Git branch in {Path}", LogPathHelper.RedactPath(repositoryPath));
			return false;
		}
		finally
		{
			await EndOperationAsync();
		}
	}

	public async Task CreateNewBranchAsync(string repositoryPath, string activeBranch)
	{
		SentrySdk.Metrics.EmitCounter("Triggered create git branch", 1);

		var viewModel = new AddBranchDialogViewModel(repositoryPath, activeBranch);
		var loadBranchesTask = viewModel.LoadBranches();
		var dialog = dialogService.GetDialog(viewModel);
		await loadBranchesTask;
		if (await dialog.TryShowAsync() is not DialogResult.Primary)
			return;

		var head = await GetRepositoryHead(repositoryPath);
		if (head?.Name != viewModel.BasedOn && !await Checkout(repositoryPath, viewModel.BasedOn))
			return;

		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			await repository.CreateBranchAsync(viewModel.NewBranchName, new GitCreateBranchOptions
			{
				Switch = viewModel.Checkout,
			});
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Could not create a Git branch in {Path}", LogPathHelper.RedactPath(repositoryPath));
		}
		finally
		{
			await EndOperationAsync();
		}
	}

	public async Task DeleteBranchAsync(string? repositoryPath, string? activeBranch, string? branchToDelete)
	{
		SentrySdk.Metrics.EmitCounter("Triggered delete git branch", 1);

		if (string.IsNullOrWhiteSpace(repositoryPath) ||
			string.IsNullOrWhiteSpace(activeBranch) ||
			string.IsNullOrWhiteSpace(branchToDelete) ||
			activeBranch.Equals(branchToDelete, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var dialog = DynamicDialogFactory.GetFor_DeleteGitBranchConfirmation(branchToDelete);
		await dialog.TryShowAsync();
		if (!(dialog.ViewModel.AdditionalData as bool? ?? false))
			return;

		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			await repository.DeleteBranchAsync(branchToDelete);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Could not delete Git branch {Branch} in {Path}", branchToDelete, LogPathHelper.RedactPath(repositoryPath));
		}
		finally
		{
			await EndOperationAsync();
		}
	}

	public async Task<bool> ValidateBranchNameForRepositoryAsync(string branchName, string repositoryPath)
	{
		if (string.IsNullOrWhiteSpace(branchName) || string.IsNullOrWhiteSpace(repositoryPath))
			return false;

		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			return await repository.IsBranchNameValidAsync(branchName);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException or ArgumentException)
		{
			logger.LogDebug(ex, "Could not validate a Git branch name in {Path}", LogPathHelper.RedactPath(repositoryPath));
			return false;
		}
	}

	public async Task FetchOriginAsync(string? repositoryPath, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(repositoryPath))
			return;

		var completed = false;
		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath, cancellationToken);
			var remotes = await repository.GetRemotesAsync(cancellationToken);
			foreach (var remote in remotes)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					await repository.FetchAsync(
						new GitFetchOptions { Remote = remote.Name, Prune = true },
						cancellationToken: cancellationToken);
				}
				catch (GitException ex)
				{
					logger.LogWarning(ex, "Failed to fetch remote {RemoteName} in {Path}", remote.Name, LogPathHelper.RedactPath(repositoryPath));
				}
			}

			completed = true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Failed to fetch repository {Path}", LogPathHelper.RedactPath(repositoryPath));
		}
		finally
		{
			await EndOperationAsync();
			if (completed && !cancellationToken.IsCancellationRequested)
			{
				await MainWindow.Instance.DispatcherQueue.EnqueueOrInvokeAsync(
					() => GitFetchCompleted?.Invoke(this, EventArgs.Empty));
			}
		}
	}

	public async Task PullOriginAsync(string? repositoryPath)
	{
		if (string.IsNullOrWhiteSpace(repositoryPath))
			return;

		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			await repository.PullAsync(new GitPullOptions { Prune = true });
		}
		catch (GitCommandException ex) when (ex.FailureKind is GitFailureKind.Authentication or GitFailureKind.Authorization)
		{
			logger.LogWarning(ex, "Git authentication failed while pulling {Path}", LogPathHelper.RedactPath(repositoryPath));
			if (await IsGitHubRepositoryAsync(repositoryPath))
				await RequireGitAuthenticationAsync();
			else
				await ShowGitErrorAsync(ex.Message);
		}
		catch (GitCommandException ex) when (ex.FailureKind is GitFailureKind.Conflict or GitFailureKind.DirtyWorkingTree)
		{
			await ShowGitErrorAsync(Strings.PullUncommittedChangesError.GetLocalizedResource());
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Failed to pull repository {Path}", LogPathHelper.RedactPath(repositoryPath));
			await ShowGitErrorAsync(ex.Message);
		}
		finally
		{
			await EndOperationAsync();
		}
	}

	public async Task PushToOriginAsync(string? repositoryPath, string? branchName)
	{
		if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(branchName))
			return;

		await BeginOperationAsync();
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			var branches = await repository.GetBranchesAsync(new GitBranchQueryOptions { IncludeRemote = false });
			var branch = branches.FirstOrDefault(candidate => candidate.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
			if (branch is null)
				return;

			var remotes = await repository.GetRemotesAsync();
			var remoteName = branch.UpstreamName?.Split('/', 2)[0]
				?? remotes.FirstOrDefault(remote => remote.Name.Equals("origin", StringComparison.OrdinalIgnoreCase))?.Name
				?? remotes.FirstOrDefault()?.Name;
			if (remoteName is null)
				return;

			await repository.PushAsync(new GitPushOptions
			{
				Remote = remoteName,
				RefSpec = branch.Name,
				SetUpstream = string.IsNullOrWhiteSpace(branch.UpstreamName),
			});
		}
		catch (GitCommandException ex) when (ex.FailureKind is GitFailureKind.Authentication or GitFailureKind.Authorization)
		{
			logger.LogWarning(ex, "Git authentication failed while pushing {Path}", LogPathHelper.RedactPath(repositoryPath));
			if (await IsGitHubRepositoryAsync(repositoryPath))
				await RequireGitAuthenticationAsync();
			else
				await ShowGitErrorAsync(ex.Message);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Failed to push repository {Path}", LogPathHelper.RedactPath(repositoryPath));
			await ShowGitErrorAsync(ex.Message);
		}
		finally
		{
			await EndOperationAsync();
		}
	}

	public async Task RequireGitAuthenticationAsync()
	{
		var pending = true;
		using var client = new HttpClient();
		client.DefaultRequestHeaders.Add("Accept", "application/json");
		client.DefaultRequestHeaders.Add("User-Agent", "Files App");

		JsonDocument codeJsonContent;
		try
		{
			using var codeResponse = await client.PostAsync(
				$"https://github.com/login/device/code?client_id={_clientId}&scope=repo",
				new StringContent(string.Empty));
			if (!codeResponse.IsSuccessStatusCode)
			{
				await DynamicDialogFactory.GetFor_GitHubConnectionError().TryShowAsync();
				return;
			}

			await using var codeStream = await codeResponse.Content.ReadAsStreamAsync();
			codeJsonContent = await JsonDocument.ParseAsync(codeStream);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not start GitHub device authentication");
			await DynamicDialogFactory.GetFor_GitHubConnectionError().TryShowAsync();
			return;
		}

		using (codeJsonContent)
		{
			var userCode = codeJsonContent.RootElement.GetProperty("user_code").GetString() ?? string.Empty;
			var deviceCode = codeJsonContent.RootElement.GetProperty("device_code").GetString() ?? string.Empty;
			var interval = codeJsonContent.RootElement.GetProperty("interval").GetInt32();
			var expiresIn = codeJsonContent.RootElement.GetProperty("expires_in").GetInt32();

			using var loginCTS = new CancellationTokenSource();
			var viewModel = new GitHubLoginDialogViewModel(userCode, Strings.ConnectGitHubDescription.GetLocalizedResource(), loginCTS);
			var dialog = dialogService.GetDialog(viewModel);
			var loginDialogTask = dialog.TryShowAsync();

			while (!loginCTS.Token.IsCancellationRequested && pending && expiresIn > 0)
			{
				try
				{
					using var loginResponse = await client.PostAsync(
						$"https://github.com/login/oauth/access_token?client_id={_clientId}&device_code={deviceCode}&grant_type=urn:ietf:params:oauth:grant-type:device_code",
						new StringContent(string.Empty),
						loginCTS.Token);
					expiresIn -= interval;

					if (!loginResponse.IsSuccessStatusCode)
					{
						dialog.Hide();
						break;
					}

					await using var loginStream = await loginResponse.Content.ReadAsStreamAsync(loginCTS.Token);
					using var loginJsonContent = await JsonDocument.ParseAsync(loginStream, cancellationToken: loginCTS.Token);
					if (loginJsonContent.RootElement.TryGetProperty("error", out var error))
					{
						if (error.GetString() == "authorization_pending")
						{
							await Task.Delay(TimeSpan.FromSeconds(interval), loginCTS.Token);
							continue;
						}

						dialog.Hide();
						break;
					}

					var token = loginJsonContent.RootElement.GetProperty("access_token").GetString();
					if (string.IsNullOrWhiteSpace(token))
						continue;

					pending = false;
					CredentialsHelpers.SavePassword(GIT_RESOURCE_NAME, GIT_RESOURCE_USERNAME, token);
					viewModel.Subtitle = Strings.AuthorizationSucceded.GetLocalizedResource();
					viewModel.LoginConfirmed = true;
				}
				catch (OperationCanceledException) when (loginCTS.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "GitHub device authentication failed");
					dialog.Hide();
					break;
				}
			}

			await loginDialogTask;
		}
	}

	public async Task<IReadOnlyList<GitItemModel>> GetGitInformationForItemsAsync(
		string repositoryPath,
		IReadOnlyList<string> paths,
		bool getStatus = true,
		bool getCommit = true,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(repositoryPath) || paths.Count == 0)
			return [];

		var repository = await CreateClient().OpenAsync(repositoryPath, cancellationToken);
		var workingTreePath = repository.Info.WorkingTreePath
			?? throw new InvalidOperationException("Git item metadata requires a working tree.");
		var relativePaths = paths.Select(path =>
			Path.GetRelativePath(workingTreePath, path).Replace('\\', '/')).ToArray();
		var pathInfo = await repository.GetPathInfoAsync(relativePaths, new GitPathInfoOptions
		{
			IncludeStatus = getStatus,
			IncludeLastCommit = getCommit,
		}, cancellationToken);
		var infoByPath = pathInfo.ToDictionary(info => info.Path, StringComparer.Ordinal);

		var result = new List<GitItemModel>(paths.Count);
		for (var index = 0; index < paths.Count; index++)
		{
			infoByPath.TryGetValue(relativePaths[index], out var info);
			GitItemStatus? status = getStatus ? GetItemStatus(info?.StatusEntries ?? []) : null;
			var commit = info?.LastCommit;
			result.Add(new GitItemModel
			{
				Status = status,
				StatusHumanized = status switch
				{
					GitItemStatus.Added => Strings.Added.GetLocalizedResource(),
					GitItemStatus.Deleted => Strings.Deleted.GetLocalizedResource(),
					GitItemStatus.Modified => Strings.Modified.GetLocalizedResource(),
					GitItemStatus.Untracked => Strings.Untracked.GetLocalizedResource(),
					_ => null,
				},
				LastCommitDate = commit?.Author.When,
				LastCommitMessage = commit?.Subject,
				LastCommitAuthor = commit?.Author.Name,
				LastCommitSha = commit?.ObjectId,
				Path = paths[index],
			});
		}

		return result;
	}

	public void RemoveSavedCredentials()
		=> CredentialsHelpers.DeleteSavedPassword(GIT_RESOURCE_NAME, GIT_RESOURCE_USERNAME);

	public string GetSavedCredentials()
		=> CredentialsHelpers.GetPassword(GIT_RESOURCE_NAME, GIT_RESOURCE_USERNAME);

	public async Task InitializeRepositoryAsync(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return;

		try
		{
			await EnsureGitAvailableAsync();
			await CreateClient().InitializeAsync(path);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Could not initialize a Git repository in {Path}", LogPathHelper.RedactPath(path));
			await DynamicDialogFactory.GetFor_GitCannotInitializeqRepositoryHere().TryShowAsync();
		}
	}

	public (string RepoUrl, string RepoName) GetRepoInfo(string url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
			(uri.Scheme is "http" or "https") &&
			(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
			 uri.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase)))
		{
			var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
			if (segments.Length >= 2)
			{
				var repositoryName = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
					? segments[1][..^4]
					: segments[1];
				return ($"{uri.Scheme}://{uri.Authority}/{segments[0]}/{repositoryName}", repositoryName);
			}
		}

		return GitRemoteUrl.TryParse(url, out var result) && result is not null && IsSupportedCloneAddress(result)
			? (result.Value, result.RepositoryName)
			: (string.Empty, string.Empty);
	}

	public bool IsValidRepoUrl(string url)
		=> GitRemoteUrl.TryParse(url, out var result) && result is not null && IsSupportedCloneAddress(result);

	public async Task CloneRepoAsync(string repoUrl, string repoName, string targetDirectory)
	{
		var banner = StatusCenterHelper.AddCard_GitClone(repoName.CreateEnumerable(), targetDirectory.CreateEnumerable(), ReturnResult.InProgress);
		var progressModel = new StatusCenterItemProgressModel(banner.ProgressEventSource, enumerationCompleted: true, FileSystemStatusCode.InProgress);
		var errorMessage = string.Empty;
		var isSuccess = false;
		var progress = new Progress<GitProgress>(update =>
		{
			if (update.Total is long total)
				progressModel.ItemsCount = total;
			if (update.Percentage is int percentage)
				progressModel.Report(percentage);
		});

		try
		{
			await EnsureGitAvailableAsync(banner.CancellationToken);
			await CreateClient().CloneAsync(repoUrl, targetDirectory, progress: progress, cancellationToken: banner.CancellationToken);
			isSuccess = true;
		}
		catch (OperationCanceledException) when (banner.CancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			errorMessage = ex.Message;
			logger.LogWarning(ex, "Could not clone {RepositoryUrl} to {Path}", repoUrl, LogPathHelper.RedactPath(targetDirectory));
		}

		if (!string.IsNullOrEmpty(errorMessage))
		{
			UIHelpers.CloseAllDialogs();
			await Task.Delay(500);
			await DynamicDialogFactory.ShowFor_CannotCloneRepo(errorMessage);
		}

		statusCenterViewModel.RemoveItem(banner);
		StatusCenterHelper.AddCard_GitClone(
			repoName.CreateEnumerable(),
			targetDirectory.CreateEnumerable(),
			isSuccess ? ReturnResult.Success :
				banner.CancellationToken.IsCancellationRequested ? ReturnResult.Cancelled : ReturnResult.Failed);
	}

	private GitClient CreateClient()
	{
		var options = new GitClientOptions
		{
			PromptMode = GitPromptMode.Disabled,
			CommandTimeout = TimeSpan.FromMinutes(10),
		};

		var token = GetSavedCredentials();
		if (!string.IsNullOrWhiteSpace(token))
		{
			var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{token}"));
			options.EnvironmentVariables["GIT_CONFIG_COUNT"] = "1";
			options.EnvironmentVariables["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraHeader";
			options.EnvironmentVariables["GIT_CONFIG_VALUE_0"] = $"Authorization: Basic {credentials}";
		}

		return new GitClient(options);
	}

	private Task EnsureGitAvailableAsync(CancellationToken cancellationToken = default)
	{
		Task<GitVersion> task;
		lock (availabilityLock)
			task = availabilityTask ??= new GitClient().EnsureMinimumVersionAsync();

		return task.WaitAsync(cancellationToken);
	}

	private async Task BeginOperationAsync()
	{
		Interlocked.Increment(ref activeOperationCount);
		await MainWindow.Instance.DispatcherQueue.EnqueueOrInvokeAsync(
			() => IsExecutingGitAction = Volatile.Read(ref activeOperationCount) > 0);
	}

	private async Task EndOperationAsync()
	{
		Interlocked.Decrement(ref activeOperationCount);
		await MainWindow.Instance.DispatcherQueue.EnqueueOrInvokeAsync(
			() => IsExecutingGitAction = Volatile.Read(ref activeOperationCount) > 0);
	}

	private static GitItemStatus GetItemStatus(IReadOnlyList<GitStatusEntry> entries)
	{
		var bestStatus = GitFileStatus.Unmodified;
		var bestPriority = 0;
		foreach (var entry in entries)
		{
			foreach (var status in new[] { entry.IndexStatus, entry.WorkTreeStatus })
			{
				var priority = GetStatusPriority(status);
				if (priority > bestPriority)
				{
					bestStatus = status;
					bestPriority = priority;
				}
			}
		}

		return bestStatus switch
		{
			GitFileStatus.Added => GitItemStatus.Added,
			GitFileStatus.Deleted => GitItemStatus.Deleted,
			GitFileStatus.Untracked => GitItemStatus.Untracked,
			GitFileStatus.Unmodified or GitFileStatus.Ignored => GitItemStatus.Unmodified,
			_ => GitItemStatus.Modified,
		};
	}

	private static int GetStatusPriority(GitFileStatus status) => status switch
	{
		GitFileStatus.Unmerged => 6,
		GitFileStatus.Deleted => 5,
		GitFileStatus.Added => 4,
		GitFileStatus.Renamed or GitFileStatus.Copied or GitFileStatus.TypeChanged or GitFileStatus.Modified => 3,
		GitFileStatus.Untracked => 2,
		GitFileStatus.Unknown => 1,
		_ => 0,
	};

	private static bool IsSupportedCloneAddress(GitRemoteUrl address)
	{
		if (address.IsLocal && Path.IsPathFullyQualified(address.Value))
			return true;

		if (Uri.TryCreate(address.Value, UriKind.Absolute, out var uri))
		{
			return uri.Scheme is "http" or "https" or "ssh" or "git" or "file";
		}

		return !address.IsLocal;
	}

	private async Task<bool> IsGitHubRepositoryAsync(string repositoryPath)
	{
		try
		{
			var repository = await CreateClient().OpenAsync(repositoryPath);
			var remotes = await repository.GetRemotesAsync();
			return remotes.SelectMany(static remote => remote.FetchUrls)
				.Any(static url => GitRemoteUrl.TryParse(url, out var parsed) &&
					parsed?.Host?.Equals("github.com", StringComparison.OrdinalIgnoreCase) is true);
		}
		catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
		{
			logger.LogDebug(ex, "Could not identify Git remotes for {Path}", LogPathHelper.RedactPath(repositoryPath));
			return false;
		}
	}

	private static async Task ShowGitErrorAsync(string? message)
	{
		var viewModel = new DynamicDialogViewModel
		{
			TitleText = Strings.GitError.GetLocalizedResource(),
			SubtitleText = message,
			CloseButtonText = Strings.Close.GetLocalizedResource(),
			DynamicButtons = DynamicDialogButtons.Cancel,
		};
		await new DynamicDialog(viewModel).TryShowAsync();
	}
}
