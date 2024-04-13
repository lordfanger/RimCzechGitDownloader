// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;


namespace LordFanger;
public class RimCzechGitDownloader
{
	private class Expansion
	{
        private readonly string _name;
		private readonly string _checkPart;

        private Expansion(string rimBase, string name)
		{
            _name = name;
			_checkPart = $"/{name}/";
			var baseDir = Path.Combine(rimBase, "Data", name, "Languages");
			LanguageDir = Path.Combine(baseDir, LangugeDirName);
			Exists = Directory.Exists(baseDir);
		}

		public bool Exists { get; }

        public string LanguageDir { get; }

        public bool IsForExpansion(string fullName) => fullName.Contains(_checkPart, StringComparison.OrdinalIgnoreCase);

		public string GetPathFor(string[] parts)
		{
			return Path.Combine([LanguageDir, .. parts.SkipWhile(s => !s.Equals(_name, StringComparison.OrdinalIgnoreCase)).Skip(1)]);
		}

		public static Expansion[] GetExpansions(string rimBase, out Expansion core)
		{
			core = new Expansion(rimBase, "Core");
			return [
					core,
					new Expansion(rimBase, "Royalty"),
					new Expansion(rimBase, "Ideology"),
					new Expansion(rimBase, "Biotech"),
					new Expansion(rimBase, "Anomaly"),
				];
		}
	}

    private static string LangugeDirName = "Czech - git";

    private const string DEFAULT_GITHUB_URL = "https://github.com/Ludeon/rimworld-Czech/archive/refs/heads/master.zip";

    private static string Version => "1.8";

	private static void Main(string[] args)
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		Console.WriteLine($"Rim Czech Git Downloader - v {Version}");
		Console.WriteLine("Hledám instalaci RimWorldu...");
        var (rimWorldDirectory, githubUrl) = ((string?)null, DEFAULT_GITHUB_URL);

        while (args is not [])
        {
            if (args is ["-source", var source, .. var rest])
            {
				args = rest;
				githubUrl = source;
            }
			else if (args is ["-language", var language, .. var rest2])
            {
                args = rest2;
				LangugeDirName = language;
            }
            else if (args is [var directory, .. var rest3])
            {
				args = rest3;
				rimWorldDirectory = directory;
            }
        }

		if (!string.IsNullOrEmpty(rimWorldDirectory))
		{
			SafeExecute(() => CheckForRimWorldFolder(rimWorldDirectory));
		}
		SafeExecute(() => CheckForRimWorldFolder(@".\"));
		SafeExecute(() => CheckForSteamFolder(@"C:\Program Files\Steam\steamapps"));
		SafeExecute(() => CheckForSteamFolder(@"C:\Program Files (x86)\Steam\steamapps"));

		var drives = DriveInfo.GetDrives();
		foreach (var drive in drives)
		{
			Console.WriteLine(drive.RootDirectory);
			CheckFolder(drive.RootDirectory.FullName, 0);
		}

		void CheckFolder(string path, int level)
		{
			var directoryName = Path.GetFileName(path);
			if (directoryName == "steamapps")
			{
				CheckForSteamFolder(path);
			}

			if (level > 3) return;

			SafeExecute(
				() =>
				{
					var subDirectories = Directory.GetDirectories(path);
					foreach (var subDirectory in subDirectories)
					{
						SafeExecute(() => CheckFolder(subDirectory, level + 1));
					}
				});
		}

		void CheckForSteamFolder(string path)
		{
			SafeExecute(
				() =>
				{
					var rimFolder = Path.Combine(path, "common", "RimWorld");
					CheckForRimWorldFolder(rimFolder);
				});
		}

		void SafeExecute(Action action)
		{
			try
			{
				action();
			}
			catch { }
		}

		void CheckForRimWorldFolder(string rimFolder)
		{
			if (!CheckRimWorldFolder(rimFolder, out var expansions)) return;

			Console.WriteLine("Nalezena instalace RimWorldu.");
			Console.WriteLine(rimFolder);
			ProcessRimWorld(expansions);
		}

		bool CheckRimWorldFolder(string rimFolder, [NotNullWhen(true)] out Expansion[] expansions)
		{
			expansions = null!;
			if (!Directory.Exists(rimFolder)) return false;

			expansions = Expansion.GetExpansions(rimFolder, out var core);
			if (!core.Exists) return false;
			return true;
		}

		void ProcessRimWorld(Expansion[] expansions)
		{
			try
			{
				Console.WriteLine($"Použitá složka pro češtinu: '{LangugeDirName}'.");
				Console.WriteLine("Stahuji aktuální češtinu.");
				var zipStream = GetGitZip();
				using var zipArchive = new ZipArchive(zipStream);

				Console.WriteLine("Mažu původní překlad.");
				foreach (var expansion in expansions)
				{
					if (!expansion.Exists) continue;
					if (Directory.Exists(expansion.LanguageDir)) Directory.Delete(expansion.LanguageDir, true);
				}

				Console.WriteLine("Kopíruji aktuální překlad češtiny do hry.");
				foreach (var entry in zipArchive.Entries)
				{
					if (string.IsNullOrEmpty(entry.Name)) continue;
					var fullName = entry.FullName;
					var parts = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries);

					foreach (var expansion in expansions)
					{
						if (!expansion.IsForExpansion(fullName)) continue;
						var path = expansion.GetPathFor(parts);
						CopyToFile(entry, path);
						
						Console.WriteLine(path);
						break;
					}
				}
				
				Console.WriteLine("Aktuální překlad do češtiny nakopírován.");
				Environment.Exit(0);

			}
			catch (Exception ex)
			{
				Console.WriteLine($"{ex.GetType()}: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
				Environment.Exit(1);
			}

            return;

            void CopyToFile(ZipArchiveEntry entry, string path)
			{
				var directory = Path.GetDirectoryName(path);
				if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
				entry.ExtractToFile(path);
			}
		}

		Stream GetGitZip()
		{
            Console.WriteLine($"Stahuji z '{githubUrl}'.");
			using var httpClient = new HttpClient();
			//var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, @"https://github.com/Ludeon/rimworld-Czech/archive/refs/heads/master.zip"));
			//var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, @"https://github.com/lordfanger/rimworld-Czech/archive/refs/heads/biotech-1.zip"));
			var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, githubUrl));
			using var data = response.Content.ReadAsStream();
			var ms = new MemoryStream();
			data.CopyTo(ms);
			ms.Position = 0;
            Console.WriteLine("Staženo.");
            return ms;
		}
	}
}