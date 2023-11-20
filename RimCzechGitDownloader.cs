// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private readonly string _rimBase;
		private readonly string _name;
		private readonly string _checkPart;
		private readonly string _baseDir;
		private readonly string _languageDir;
		private readonly bool _exists;

		public Expansion(string rimBase, string name)
		{
			_rimBase = rimBase;
			_name = name;
			_checkPart = $"/{name}/";
			_baseDir = Path.Combine(_rimBase, "Data", name, "Languages");
			_languageDir = Path.Combine(_baseDir, "Czech - git");
			_exists = Directory.Exists(_baseDir);
		}

		public bool Exists => _exists;

		public string LanguageDir => _languageDir;

		public bool IsForExpansion(string fullName) => fullName.Contains(_checkPart, StringComparison.OrdinalIgnoreCase);

		public string GetPathFor(string[] parts)
		{
			return Path.Combine(new[] { _languageDir }.Concat(parts.SkipWhile(s => !s.Equals(_name, StringComparison.OrdinalIgnoreCase)).Skip(1)).ToArray());
		}

		public static Expansion[] GetExpansions(string rimBase, out Expansion core)
		{
			core = new Expansion(rimBase, "Core");
			return new Expansion[] {
					core,
					new Expansion(rimBase, "Royalty"),
					new Expansion(rimBase, "Ideology"),
					new Expansion(rimBase, "Biotech"),
				};
		}
	}

	private static string Version => "1.4";

	private static void Main(string[] args)
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		Console.WriteLine($"Rim Czech Git Downloader - v {Version}");
		Console.WriteLine("Hledám instalaci RimWorldu...");
		if (args?.Length > 0)
		{
			SafeExecute(() => CheckForRimworldFolder(args[0]));
		}
		SafeExecute(() => CheckForRimworldFolder(@".\"));
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
					CheckForRimworldFolder(rimFolder);
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

		void CheckForRimworldFolder(string rimFolder)
		{
			if (!CheckRimworldFolder(rimFolder, out var expansions)) return;

			Console.WriteLine("Nalezena instalace RimWorldu.");
			Console.WriteLine(rimFolder);
			ProcessRimworld(expansions);
		}

		bool CheckRimworldFolder(string rimFolder, [NotNullWhen(true)] out Expansion[] expansions)
		{
			expansions = null!;
			if (!Directory.Exists(rimFolder)) return false;

			expansions = Expansion.GetExpansions(rimFolder, out var core);
			if (!core.Exists) return false;
			return true;
		}

		void ProcessRimworld(Expansion[] expansions)
		{
			try
			{
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

			void CopyToFile(ZipArchiveEntry entry, string path)
			{
				var directory = Path.GetDirectoryName(path);
				if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
				entry.ExtractToFile(path);
			}
		}

		Stream GetGitZip()
		{
			using var httpClient = new HttpClient();
			var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, @"https://github.com/Ludeon/RimWorld-Czech/archive/refs/heads/master.zip"));
			using var data = response.Content.ReadAsStream();
			var ms = new MemoryStream();
			data.CopyTo(ms);
			ms.Position = 0;
			return ms;
		}
	}
}