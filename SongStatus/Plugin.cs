﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine.SceneManagement;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using BS_Utils.Gameplay;
using System.Text.RegularExpressions;
using System.Text;

namespace SongStatus
{

	[Plugin(RuntimeOptions.SingleStartInit)]
	public class Plugin
	{
		internal static Plugin instance { get; private set; }
		internal static string Name => "SongStatus";

		private string _statusPath = Path.Combine(Environment.CurrentDirectory, @"UserData/songStatus.txt");
		private readonly string _starCsvPath = Path.Combine(Environment.CurrentDirectory, @"UserData/star.csv");

		private readonly string _defaultTemplate = string.Join(
			Environment.NewLine,
			"Playing: {songName} {songSubName} - {authorName}",
			"{gamemode} {difficulty} | BPM: {beatsPerMinute}",
			"{[isNoFail]} {[modifiers]}");

		private readonly string _templatePath = Path.Combine(Environment.CurrentDirectory, @"UserData/songStatusTemplate.txt");


		[Init]
		/// <summary>
		/// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
		/// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
		/// Only use [Init] with one Constructor.
		/// </summary>
		public void Init(IPALogger logger)
		{
			instance = this;
			Logger.log = logger;
			Logger.log.Debug("Logger initialized.");

			BS_Utils.Utilities.BSEvents.gameSceneActive += GameCoreLoaded;
			BS_Utils.Utilities.BSEvents.menuSceneActive += ClearText;
		}

		#region BSIPA Config
		//Uncomment to use BSIPA's config
		/*
        [Init]
        public void InitWithConfig(Config conf)
        {
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Logger.log.Debug("Config loaded");
        }
        */
		#endregion

		[OnStart]
		public void OnApplicationStart()
		{
			Logger.log.Debug("OnApplicationStart");
			new GameObject("SongStatusController").AddComponent<SongStatusController>();

		}

		[OnExit]
		public void OnApplicationQuit()
		{
			Logger.log.Debug("OnApplicationQuit");

			BS_Utils.Utilities.BSEvents.gameSceneActive -= GameCoreLoaded;
			BS_Utils.Utilities.BSEvents.menuSceneActive -= ClearText;

			ClearText();
		}

		private async void GameCoreLoaded()
		{
			// OnApplicationStart() でやったほうがいいかもしれないが、プレイ開始前に変更できるメリットのほうがあると思うのでここで実施。
			// 1フレームごとに呼ばれる処理でもないのでさほど負荷はないはず。
			string templateText = _defaultTemplate;
			if (!File.Exists(_templatePath))
			{
				templateText = _defaultTemplate;
				File.WriteAllText(_templatePath, templateText);
			}
			else
			{
				templateText = File.ReadAllText(_templatePath);
			}

			GameplayCoreSceneSetupData gameplayCoreSceneSetupData = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData;
			IDifficultyBeatmap diff = gameplayCoreSceneSetupData.difficultyBeatmap;
			IBeatmapLevel song = diff.level;

			var isPracticeMode = (gameplayCoreSceneSetupData.practiceSettings != null && !BS_Utils.Gameplay.Gamemode.IsIsolatedLevel);
			Logger.log.Info($"levelID: {song.levelID} songName: {song.songName}");

			GameplayModifiers mods = gameplayCoreSceneSetupData.gameplayModifiers;
			var modsList = string.Empty;
			if (!mods.IsWithoutModifiers())
			{
				modsList += mods.instaFail ? "Instant Fail, " : string.Empty;
				modsList += (mods.energyType == GameplayModifiers.EnergyType.Battery) ? "Battery Energy, " : string.Empty;
				modsList += mods.disappearingArrows ? "Disappearing Arrows, " : string.Empty;
				modsList += mods.ghostNotes ? "Ghost Notes, " : string.Empty;
				modsList += mods.noBombs ? "No Bombs, " : string.Empty;
				modsList += (mods.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) ? "No Walls, " : string.Empty;
				modsList += (mods.enabledObstacleType == GameplayModifiers.EnabledObstacleType.FullHeightOnly) ? "No Walls(FullHeightOnly), " : string.Empty;
				modsList += mods.noArrows ? "No Arrows, " : string.Empty;
				modsList += mods.failOnSaberClash ? "Fail On Saber Clash, " : string.Empty;
				modsList += mods.strictAngles ? "Strict Angles, " : string.Empty;
				modsList += mods.fastNotes ? "Fast Notes" : string.Empty;
				modsList += mods.smallCubes ? "Small Cubes, " : string.Empty;
				modsList += mods.proMode ? "Pro Mode, " : string.Empty;
				modsList += mods.zenMode ? "Zen Mode, " : string.Empty;
				modsList += mods.songSpeedMul != 1.0f ? "Speed " + mods.songSpeedMul + "x" : string.Empty;

				modsList = modsList.Trim(new char[] { ' ', ',' });
			}

			//string gameplayModeText = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
			string gameplayModeText = diff.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
			if (isPracticeMode)
			{
				float practiceSpeed = gameplayCoreSceneSetupData.practiceSettings.songSpeedMul;
				if (practiceSpeed != 1.0f)
				{
					gameplayModeText += $" (Practice Mode:{practiceSpeed + "x"})";
				}
				else
				{
					gameplayModeText += " (Practice Mode)";
				}
			}
			//var keywords = templateText.Split('{', '}');

			string hash = song.levelID;
			if (hash.ToLower().StartsWith("custom_level_"))
			{
				hash = hash.Substring("custom_level_".Length);
			}
			else
            {
				hash = "";
            }
			templateText = ReplaceKeyword("hash", hash, templateText);
			templateText = ReplaceKeyword("songName", song.songName, templateText);
			templateText = ReplaceKeyword("songSubName", song.songSubName, templateText);
			templateText = ReplaceKeyword("authorName", song.songAuthorName, templateText);
			templateText = ReplaceKeyword("levelAuthorName", song.levelAuthorName, templateText);
			templateText = ReplaceKeyword("gamemode", gameplayModeText, templateText);
			templateText = ReplaceKeyword("difficulty", diff.difficulty.Name(), templateText);
			if (templateText.Contains("{star}"))
			{
				templateText = ReplaceKeyword("star", GetStarDifficulty(song.levelID, diff.difficulty.Name()), templateText);
			}
			templateText = ReplaceKeyword("isNoFail",
				mods.noFailOn0Energy ? "No Fail" : string.Empty, templateText);
			templateText = ReplaceKeyword("modifiers", modsList, templateText);
			templateText = ReplaceKeyword("beatsPerMinute",
				song.beatsPerMinute.ToString(CultureInfo.InvariantCulture), templateText);
			float njs = diff.noteJumpMovementSpeed;
			templateText = ReplaceKeyword("noteJumpSpeed", njs.ToString("0.0#"), templateText);

			PlayerSpecificSettings playerSettings = gameplayCoreSceneSetupData.playerSpecificSettings;

			var beatmapData = await diff.GetBeatmapDataAsync(diff.GetEnvironmentInfo(), playerSettings);
			templateText = ReplaceKeyword("notesCount",
				beatmapData.cuttableNotesCount.ToString(CultureInfo.InvariantCulture), templateText);

			templateText = ReplaceKeyword("obstaclesCount",
				beatmapData.obstaclesCount.ToString(CultureInfo.InvariantCulture), templateText);

			// Note Jump Offset
			string offsetText = "";
			var noteJumpDurationTypeSettings = playerSettings.noteJumpDurationTypeSettings;
			if (noteJumpDurationTypeSettings == NoteJumpDurationTypeSettings.Dynamic)
			{
				// Dynamic
				switch (playerSettings.noteJumpStartBeatOffset)
				{
					case -0.5f:
						offsetText = "Close";
						break;
					case -0.25f:
						offsetText = "Closer";
						break;
					case 0f:
						offsetText = "Default";
						break;
					case 0.25f:
						offsetText = "Further";
						break;
					case 0.5f:
						offsetText = "Far";
						break;
					default:
						offsetText = playerSettings.noteJumpStartBeatOffset.ToString();
						break;
				}
			}
			else
			{
				// Static
				var noteJumpFixedDuration = playerSettings.noteJumpFixedDuration;
				offsetText = noteJumpFixedDuration.ToString() + "s (Static)";
			}
			templateText = ReplaceKeyword("noteJumpStartBeatOffset", offsetText, templateText);

			Logger.log.Debug(templateText);
			using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(
				_statusPath, false, System.Text.Encoding.UTF8))
			{
				streamWriter.Write(templateText);
				streamWriter.Flush();
			}
		}

		private void ClearText()
		{
			File.WriteAllText(_statusPath, string.Empty);
		}

		private string GetStarDifficulty(string levelID, string difficulty)
		{
			if (!File.Exists(_starCsvPath))
			{
				Logger.log.Warn($"File Not found: {_starCsvPath}");
				return "-";
			}
			// 数KBだからOSのファイルキャッシュに入ると思うので Game 内にキャッシュしない。
			using (StreamReader sr = new StreamReader(_starCsvPath))
			{
				while (!sr.EndOfStream)
				{
					// CSVファイルの一行を読み込む
					string line = sr.ReadLine();
					string[] values = line.Split(',');

					if (values.Length < 3)
					{
						continue;
					}

					if ("custom_level_" + values[0] == levelID && values[1] == difficulty)
					{
						return values[2];
					}
				}
			}
			return "-";
		}

		private string ReplaceKeyword(string keyword, string replaceKeyword, string text)
		{
			// "{" で始まり "}" で終わる、かつ "{", "}", 改行を含まない文字列を抽出
			MatchCollection matches = Regex.Matches(text, @"{[^(\r\n{})]+}");

			// keyword を含むものだけさらに抽出
			bool found = false;
			var list = new List<Match>();
			foreach (Match match in matches)
			{
				if (match.Value.Contains(keyword))
				{
					found = true;
					list.Add(match);
				}
			}
			// 置換対象がなければそのまま返す
			if (!found) return text;

			list.Sort((a, b) =>
			{
				// 開始位置の逆順ソート
				return b.Index - a.Index;
			});

			var sb = new StringBuilder(text);
			foreach (var item in list)
			{
				string replacedValue = string.Empty;
				if (!string.IsNullOrEmpty(replaceKeyword))
                {
					// 抽出した文字列から先頭の "{" と末尾の "}" を除去して keyword 部分を置換
					replacedValue = item.Value.Substring(1, item.Value.Length - 2).Replace(keyword, replaceKeyword);
				}
				// text 置換
				sb.Remove(item.Index, item.Length).Insert(item.Index, replacedValue);
			}
			return sb.ToString();
		}

		/*
		private string ReplaceKeyword(string keyword, string replaceKeyword, string[] keywords, string text)
		{
			if (!keywords.Any(x => x.Contains(keyword))) return text;
			var containingKeywords = keywords.Where(x => x.Contains(keyword));

			if (string.IsNullOrEmpty(replaceKeyword))
			{
				//If the replacement word is null or empty, we want to remove the whole bracket.
				foreach (var containingKeyword in containingKeywords)
				{
					text = text.Replace("{" + containingKeyword + "}", string.Empty);
				}

				return text;
			}

			foreach (var containingKeyword in containingKeywords)
			{
				text = text.Replace("{" + containingKeyword + "}", containingKeyword);
			}

			text = text.Replace(keyword, replaceKeyword);

			return text;
		}
		*/
	}
}
