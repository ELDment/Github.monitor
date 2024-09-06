using System.Text;
using System.Net.Http.Headers;
using System.Data.SQLite;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Data.SqlClient;
using System.Xml.Linq;

namespace ELDment
{
	public partial class GithubManager // -> var | 自行更改
	{
		protected static readonly string githubName = "ELDment";
		protected static readonly string githubToken = "ghp_";
		protected static readonly string githubRepo = "CS2_Railcannon.RE";
		protected static readonly string watchedRepo = "CS2_Railcannon";
		protected SQLiteConnection? dbConnection = null;
	}

	public partial class GithubManager // -> MAIN
	{
		static async Task Main(string[] args)
		{
			GithubManager gManager = new GithubManager();
			try
			{
				gManager.ConsoleOutput($"正在监控，{githubName}的项目：{watchedRepo}");
				Console.Title = "GithubManager";
				await gManager.OpenConnection();
				await gManager.CreateTable();
				while (true)
				{
					long delta = 0;
					try
					{
						string? starList = await gManager.ListStargazers();
						string? newlyList = string.Empty, failureList = string.Empty, expiresList = string.Empty;
						long timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
						if (!string.IsNullOrWhiteSpace(starList))
						{
							string ticks = DateTime.Now.Ticks.ToString();
							List<string> list = new List<string>();
							list.Clear();
							foreach (JObject? githubStardata in JArray.Parse(starList))
							{
								if (githubStardata is null) continue;
								string name = githubStardata["login"]!.ToString();
								if (string.IsNullOrWhiteSpace(name) || name == githubName) continue;
								if (!await gManager.ExistsCollaborator(name))
								{
									if (!string.IsNullOrWhiteSpace(await gManager.AddCollaborator(name)))
									{
										await gManager.InsertCollaborator(name, ticks);
										newlyList = (string.IsNullOrWhiteSpace(newlyList)) ? name : $"{newlyList}，{name}";
									}

								}
								else
								{
									if (!await gManager.UpdateCollaboratorData(name, ticks))
									{
										failureList = (string.IsNullOrWhiteSpace(failureList)) ? name : $"{failureList}，{name}";
										list.Add(name);
									}
								}
							}
							string?[]? buffer = await gManager.CheckCollaboratorData(ticks);
							if (buffer is not null)
							{
								foreach (string? name in buffer)
								{
									if (string.IsNullOrWhiteSpace(name) || list.Contains(name)) continue;
									if (!string.IsNullOrWhiteSpace(await gManager.RemoveCollaborator(name)))
									{
										await gManager.DeleteCollaboratorData(name); //从数据库删除
										expiresList = (string.IsNullOrWhiteSpace(expiresList)) ? name : $"{expiresList}，{name}";
									}
								}
							}
							delta = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timeStamp;
							gManager.ConsoleOutput($"耗时{delta}毫秒 - {(string.IsNullOrWhiteSpace(newlyList) ? "暂无新增" : $"[+]新增：{newlyList}")}，{(string.IsNullOrWhiteSpace(expiresList) ? "暂无过期" : $"[-]过期：{expiresList}")}，{(string.IsNullOrWhiteSpace(failureList) ? "全部Data更新完毕" : $"未成功更新：{failureList}")}");
						}
					}
					catch (Exception error)
					{
						gManager.ConsoleOutput($"AT Main[Loop] -> {error.Message}");
					}
					//gManager.ConsoleOutput($"{60 * 1000 - (int)delta}");
					Thread.Sleep(60 * 1000 - (int)delta);
					continue;
				}
				//gManager.ConsoleOutput("等待资源释放...");
				//gManager.dbConnection!.Close();
			}
			catch (Exception error)
			{
				gManager.ConsoleOutput($"AT Main -> {error.Message}");
			}
			return;
		}
	}

	public partial class GithubManager // -> requestApi
	{
		protected async Task<string?> ListStargazers()
		{
			try
			{
				string? responseBody = string.Empty;
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(20);
					string requestUrl = $"https://api.github.com/repos/{githubName}/{watchedRepo}/stargazers";
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
					request.Headers.Add("User-Agent", "Awesome-Octocat-App");
					request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
					request.Headers.Add("Accept", "application/vnd.github+json");
					HttpResponseMessage response = await client.SendAsync(request);
					responseBody = await response.Content.ReadAsStringAsync();
					if ((int)response.StatusCode != 200) responseBody = string.Empty;
				}
				return responseBody;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT ListStargazers -> {error.Message}");
			}
			return string.Empty;
		}

		protected async Task<string?> AddCollaborator(string collaboratorName)
		{
			try
			{
				string? responseBody = string.Empty;
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(20);
					string requestUrl = $"https://api.github.com/repos/{githubName}/{githubRepo}/collaborators/{collaboratorName}";
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
					request.Headers.Add("User-Agent", "Awesome-Octocat-App");
					request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
					request.Headers.Add("Accept", "application/vnd.github+json");
					request.Content = new StringContent("{\"permission\":\"Read\"}", Encoding.UTF8, "application/json");
					HttpResponseMessage response = await client.SendAsync(request);
					responseBody = await response.Content.ReadAsStringAsync();
					if ((int)response.StatusCode != 201) responseBody = string.Empty;
				}
				return responseBody;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT AddCollaborator -> {error.Message}");
			}
			return string.Empty;
		}

		protected async Task<string?> RemoveCollaborator(string name)
		{
			try
			{
				string? responseBody = string.Empty;
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(20);
					string requestUrl = $"https://api.github.com/repos/{githubName}/{githubRepo}/collaborators/{name}";
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
					request.Headers.Add("User-Agent", "Awesome-Octocat-App");
					request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
					request.Headers.Add("Accept", "application/vnd.github+json");
					HttpResponseMessage response = await client.SendAsync(request);
					responseBody = "success";
					if ((int)response.StatusCode != 204) responseBody = string.Empty;
				}
				return responseBody;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT RemoveCollaborator -> {error.Message}");
			}
			return string.Empty;
		}
	}

	public partial class GithubManager // -> sqlite
	{
		protected async Task<bool> OpenConnection()
		{
			try
			{
				if (dbConnection is not null)
				{
					if (dbConnection.State != System.Data.ConnectionState.Closed)
					{
						await dbConnection.CloseAsync();
						dbConnection = null;
					}
				}
				dbConnection = new SQLiteConnection("Data Source=GithubManager.db;Version=3;");
				await dbConnection.OpenAsync();
				return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT OpenConnection -> {error.Message}");
			}
			return false;
		}

		protected async Task<bool> CreateTable()
		{
			try
			{
				using (var command = new SQLiteCommand("CREATE TABLE IF NOT EXISTS `github` (`githubName` varchar(64) NOT NULL, `activateDate` text NOT NULL , PRIMARY KEY (`githubName`));", dbConnection))
				{
					await command.ExecuteNonQueryAsync();
				}
				return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT CreateTable -> {error.Message}");
			}
			return false;
		}

		protected async Task<bool> ExistsCollaborator(string collaboratorName)
		{
			try
			{
				Int64 count = 0;
				using (SQLiteCommand command = new SQLiteCommand($"SELECT COUNT(*) FROM `github` WHERE `githubName` = '{collaboratorName}';", dbConnection))
				{
					count = (Int64)((await command.ExecuteScalarAsync()) ?? 0);
				}
				if (count >= 1) return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT ExistsCollaborator -> {error.Message}");
			}
			return false;
		}

		protected async Task<bool> InsertCollaborator(string collaboratorName, string ticks)
		{
			try
			{
				using (SQLiteCommand command = new SQLiteCommand($"INSERT INTO `github` (`githubName`, `activateDate`) VALUES ('{collaboratorName}', '{ticks}');", dbConnection))
				{
					await command.ExecuteNonQueryAsync();
				}
				return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT InsertCollaborator -> {error.Message}");
			}
			return false;
		}

		protected async Task<bool> UpdateCollaboratorData(string collaboratorName, string ticks)
		{
			try
			{
				using (SQLiteCommand command = new SQLiteCommand($"UPDATE `github` SET `activateDate`='{ticks}' WHERE `githubName`='{collaboratorName}';", dbConnection))
				{
					await command.ExecuteNonQueryAsync();
				}
				return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT UpdateCollaboratorData -> {error.Message}");
			}
			return false;
		}

		protected async Task<string?[]?> CheckCollaboratorData(string ticks)
		{
			try
			{
				List<string?> list = new List<string?> { };
				list.Clear();
				using (SQLiteCommand command = new SQLiteCommand($"SELECT `githubName` FROM `github` WHERE `activateDate` != '{ticks}';", dbConnection))
				{
					using (DbDataReader reader = await command.ExecuteReaderAsync())
					{
						while (reader.Read())
						{
							if (string.IsNullOrWhiteSpace(reader["githubName"].ToString())) continue;
							list.Add(reader["githubName"].ToString());
						}
					}
				}
				return list.ToArray();
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT CheckCollaboratorData -> {error.Message}");
			}
			return new string[1];
		}

		protected async Task<bool> DeleteCollaboratorData(string githubName)
		{
			try
			{
				using (SQLiteCommand command = new SQLiteCommand($"DELETE FROM `github` WHERE `githubName` = '{githubName}';", dbConnection))
				{
					await command.ExecuteNonQueryAsync();
				}
				return true;
			}
			catch (Exception error)
			{
				ConsoleOutput($"AT DeleteCollaboratorData -> {error.Message}");
			}
			return false;
		}
	}

	public partial class GithubManager // -> utilities
	{
		protected void ConsoleOutput(string? content)
		{
			try
			{
				DateTime currentTime = DateTime.Now;
				Console.ResetColor();
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.Write($"{currentTime.ToString("HH:mm:ss")}");
				Console.ResetColor();
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write($" [ GManager ]");
				Console.ResetColor();
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine($" {content}");
				Console.ResetColor();
			}
			catch (Exception error)
			{
				Console.WriteLine($"AT ConsoleOutput -> {error.Message}");
			}
			return;
		}
	}
}
