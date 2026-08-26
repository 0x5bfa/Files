// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Utils.Git
{
	internal static class GitHelpers
	{
		private static readonly IVersionControlService implementation =
			Ioc.Default.GetRequiredService<IVersionControlService>();

		public static Task<string?> GetGitRepositoryPathAsync(
			string? path,
			string? root = null,
			CancellationToken cancellationToken = default)
			=> implementation.GetGitRepositoryPathAsync(path, root, cancellationToken);

		public static Task<string> GetOriginRepositoryNameAsync(
			string? path,
			CancellationToken cancellationToken = default)
			=> implementation.GetOriginRepositoryNameAsync(path, cancellationToken);

		public static Task<BranchItem[]> GetBranchNames(string? path)
			=> implementation.GetBranchNames(path);

		public static Task<BranchItem?> GetRepositoryHead(string? path)
			=> implementation.GetRepositoryHead(path);

		public static Task<string?> GetRepositoryHeadName(string? path)
			=> implementation.GetRepositoryHeadName(path);

		public static Task<bool> Checkout(string? repositoryPath, string? branch)
			=> implementation.Checkout(repositoryPath, branch);

		public static Task CreateNewBranchAsync(string repositoryPath, string activeBranch)
			=> implementation.CreateNewBranchAsync(repositoryPath, activeBranch);

		public static Task DeleteBranchAsync(string? repositoryPath, string? activeBranch, string? branchToDelete)
			=> implementation.DeleteBranchAsync(repositoryPath, activeBranch, branchToDelete);

		public static Task<bool> ValidateBranchNameForRepositoryAsync(string branchName, string repositoryPath)
			=> implementation.ValidateBranchNameForRepositoryAsync(branchName, repositoryPath);

		public static Task FetchOriginAsync(string? repositoryPath, CancellationToken cancellationToken = default)
			=> implementation.FetchOriginAsync(repositoryPath, cancellationToken);

		public static Task PullOriginAsync(string? repositoryPath)
			=> implementation.PullOriginAsync(repositoryPath);

		public static Task PushToOriginAsync(string? repositoryPath, string? branchName)
			=> implementation.PushToOriginAsync(repositoryPath, branchName);

		public static Task RequireGitAuthenticationAsync()
			=> implementation.RequireGitAuthenticationAsync();

		public static Task<IReadOnlyList<GitItemModel>> GetGitInformationForItemsAsync(
			string repositoryPath,
			IReadOnlyList<string> paths,
			bool getStatus = true,
			bool getCommit = true,
			CancellationToken cancellationToken = default)
			=> implementation.GetGitInformationForItemsAsync(
				repositoryPath,
				paths,
				getStatus,
				getCommit,
				cancellationToken);

		public static void RemoveSavedCredentials()
			=> implementation.RemoveSavedCredentials();

		public static string GetSavedCredentials()
			=> implementation.GetSavedCredentials();

		public static Task InitializeRepositoryAsync(string? path)
			=> implementation.InitializeRepositoryAsync(path);

		public static (string RepoUrl, string RepoName) GetRepoInfo(string url)
			=> implementation.GetRepoInfo(url);

		public static bool IsValidRepoUrl(string url)
			=> implementation.IsValidRepoUrl(url);

		public static Task CloneRepoAsync(string repoUrl, string repoName, string targetDirectory)
			=> implementation.CloneRepoAsync(repoUrl, repoName, targetDirectory);

		public static bool IsExecutingGitAction => implementation.IsExecutingGitAction;

		public static event PropertyChangedEventHandler? IsExecutingGitActionChanged
		{
			add => implementation.IsExecutingGitActionChanged += value;
			remove => implementation.IsExecutingGitActionChanged -= value;
		}

		public static event EventHandler? GitFetchCompleted
		{
			add => implementation.GitFetchCompleted += value;
			remove => implementation.GitFetchCompleted -= value;
		}
	}
}
