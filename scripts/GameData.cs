using Godot;
using System.Collections.Generic;

namespace MergeRobotB;

public struct SavedPieceData
{
	public int Level;
	public float X;
	public float Y;
	public float Rotation;
}

// Skor, ayarlar ve yarım kalan oyun verilerini yöneten sınıf
public static class GameData
{
	private const string SavePath = "user://mergerobot_b.cfg";

	public static int BestScore { get; private set; } = 0;
	public static bool SoundEnabled { get; private set; } = true;
	public static bool MusicEnabled { get; private set; } = true;
	public static string Language { get; private set; } = "TR";
	public static bool IsLightTheme { get; private set; } = false;

	// Pause menüsünden ana menüye dönüldüğünde saklanan oyun durumu
	public static bool HasSavedSession { get; private set; } = false;
	public static int SavedScore { get; private set; } = 0;
	public static int SavedCurrentPieceLevel { get; private set; } = 1;
	public static int SavedNextPieceLevel { get; private set; } = 1;
	public static List<SavedPieceData> SavedPieces { get; private set; } = new();

	private static bool _loaded = false;
	private static AudioStreamPlayer _bgmPlayer;
	private static Font _roundedFont;

	public static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;
		Load();
	}

	// Tüm arayüzde kullanılan yumuşak ve kalın hatlı font
	public static Font GetRoundedFont()
	{
		if (_roundedFont != null) return _roundedFont;

		var sysFont = new SystemFont
		{
			FontNames = new string[] { "Comic Sans MS", "Trebuchet MS", "Segoe Print", "Verdana", "Arial Rounded MT Bold" },
			FontWeight = 800,
			Antialiasing = TextServer.FontAntialiasing.Lcd,
			GenerateMipmaps = true,
			MultichannelSignedDistanceField = false
		};

		var variation = new FontVariation
		{
			BaseFont = sysFont,
			VariationEmbolden = 0.35f,
			SpacingGlyph = 1
		};

		_roundedFont = variation;
		return _roundedFont;
	}

	public static void ApplySoftFont(Label label, int fontSize, Color fontColor, Color outlineColor, int outlineSize = 8)
	{
		label.AddThemeFontOverride("font", GetRoundedFont());
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		label.AddThemeColorOverride("font_outline_color", outlineColor);
		label.AddThemeConstantOverride("outline_size", outlineSize);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.35f));
		label.AddThemeConstantOverride("shadow_offset_x", 0);
		label.AddThemeConstantOverride("shadow_offset_y", 3);
	}

	public static void ApplySoftButtonFont(Button btn, int fontSize, Color fontColor, Color outlineColor, int outlineSize = 7)
	{
		btn.AddThemeFontOverride("font", GetRoundedFont());
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		btn.AddThemeColorOverride("font_color", fontColor);
		btn.AddThemeColorOverride("font_hover_color", fontColor);
		btn.AddThemeColorOverride("font_pressed_color", fontColor.Darkened(0.08f));
		btn.AddThemeColorOverride("font_focus_color", fontColor);
		btn.AddThemeColorOverride("font_outline_color", outlineColor);
		btn.AddThemeConstantOverride("outline_size", outlineSize);
	}

	// Arka plan müziğini sahneler arası kesilmeden çalar
	public static void EnsureBgmPlayer(SceneTree tree)
	{
		EnsureLoaded();
		if (_bgmPlayer != null && GodotObject.IsInstanceValid(_bgmPlayer))
		{
			UpdateBgmState();
			return;
		}

		if (!ResourceLoader.Exists("res://audio/bgm.wav")) return;

		_bgmPlayer = new AudioStreamPlayer
		{
			Name = "GlobalBgmPlayer",
			ProcessMode = Node.ProcessModeEnum.Always,
			VolumeDb = -10f
		};
		var stream = GD.Load<AudioStreamWav>("res://audio/bgm.wav");
		if (stream != null)
		{
			stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			stream.LoopBegin = 0;
			stream.LoopEnd = (int)(stream.MixRate * 8.0f);
			_bgmPlayer.Stream = stream;
		}
		tree.Root.CallDeferred(Node.MethodName.AddChild, _bgmPlayer);
		_bgmPlayer.Ready += UpdateBgmState;
	}

	public static void UpdateBgmState()
	{
		if (_bgmPlayer == null || !GodotObject.IsInstanceValid(_bgmPlayer)) return;
		if (MusicEnabled)
		{
			if (!_bgmPlayer.Playing)
				_bgmPlayer.Play();
		}
		else
		{
			_bgmPlayer.Stop();
		}
	}

	public static void SetBestScore(int score)
	{
		if (score > BestScore)
		{
			BestScore = score;
			Save();
		}
	}

	public static void SetSound(bool enabled)
	{
		SoundEnabled = enabled;
		Save();
	}

	public static void SetMusic(bool enabled)
	{
		MusicEnabled = enabled;
		UpdateBgmState();
		Save();
	}

	public static void SetLanguage(string lang)
	{
		Language = (lang == "EN") ? "EN" : "TR";
		Save();
	}

	public static void SetLightTheme(bool isLight)
	{
		IsLightTheme = isLight;
		Save();
	}

	public static void SaveSessionState(int currentScore, int currentPieceLvl, int nextPieceLvl, List<SavedPieceData> pieces)
	{
		HasSavedSession = true;
		SavedScore = currentScore;
		SavedCurrentPieceLevel = currentPieceLvl;
		SavedNextPieceLevel = nextPieceLvl;
		SavedPieces = new List<SavedPieceData>(pieces);
		Save();
	}

	public static void ClearSessionState()
	{
		HasSavedSession = false;
		SavedScore = 0;
		SavedPieces.Clear();
		Save();
	}

	public static void Save()
	{
		using var config = new ConfigFile();
		config.SetValue("Settings", "BestScore", BestScore);
		config.SetValue("Settings", "SoundEnabled", SoundEnabled);
		config.SetValue("Settings", "MusicEnabled", MusicEnabled);
		config.SetValue("Settings", "Language", Language);
		config.SetValue("Settings", "IsLightTheme", IsLightTheme);

		config.SetValue("Session", "HasSavedSession", HasSavedSession);
		config.SetValue("Session", "SavedScore", SavedScore);
		config.SetValue("Session", "SavedCurrentPieceLevel", SavedCurrentPieceLevel);
		config.SetValue("Session", "SavedNextPieceLevel", SavedNextPieceLevel);
		config.SetValue("Session", "PieceCount", SavedPieces.Count);

		for (int i = 0; i < SavedPieces.Count; i++)
		{
			var p = SavedPieces[i];
			config.SetValue("Session", $"P_{i}_L", p.Level);
			config.SetValue("Session", $"P_{i}_X", p.X);
			config.SetValue("Session", $"P_{i}_Y", p.Y);
			config.SetValue("Session", $"P_{i}_R", p.Rotation);
		}

		config.Save(SavePath);
	}

	public static void Load()
	{
		using var config = new ConfigFile();
		if (config.Load(SavePath) != Error.Ok) return;

		BestScore = (int)config.GetValue("Settings", "BestScore", 0);
		SoundEnabled = (bool)config.GetValue("Settings", "SoundEnabled", true);
		MusicEnabled = (bool)config.GetValue("Settings", "MusicEnabled", true);
		Language = (string)config.GetValue("Settings", "Language", "TR");
		IsLightTheme = (bool)config.GetValue("Settings", "IsLightTheme", false);

		HasSavedSession = (bool)config.GetValue("Session", "HasSavedSession", false);
		SavedScore = (int)config.GetValue("Session", "SavedScore", 0);
		SavedCurrentPieceLevel = (int)config.GetValue("Session", "SavedCurrentPieceLevel", 1);
		SavedNextPieceLevel = (int)config.GetValue("Session", "SavedNextPieceLevel", 1);
		int count = (int)config.GetValue("Session", "PieceCount", 0);

		SavedPieces.Clear();
		for (int i = 0; i < count; i++)
		{
			SavedPieces.Add(new SavedPieceData
			{
				Level = (int)config.GetValue("Session", $"P_{i}_L", 1),
				X = (float)config.GetValue("Session", $"P_{i}_X", 360f),
				Y = (float)config.GetValue("Session", $"P_{i}_Y", 800f),
				Rotation = (float)config.GetValue("Session", $"P_{i}_R", 0f)
			});
		}
	}

	// Türkçe ve İngilizce metin karşılıkları
	public static string Tr(string key)
	{
		bool en = Language == "EN";
		return key switch
		{
			"TITLE" => "RoboMerge",
			"SUBTITLE" => en ? "Cute Robot Factory Puzzle" : "Sevimli Robot Fabrikası",
			"PLAY" => en ? "START GAME!" : "OYUNA BAŞLA!",
			"NEW_GAME" => en ? "NEW GAME!" : "YENİ OYUN!",
			"CONTINUE_GAME" => en ? "CONTINUE" : "DEVAM ET",
			"SETTINGS" => en ? "SETTINGS" : "AYARLAR",
			"LANGUAGE" => en ? "Language: EN" : "Dil: Türkçe",
			"SOUND_ON" => en ? "Sound: ON" : "Ses: AÇIK",
			"SOUND_OFF" => en ? "Sound: OFF" : "Ses: KAPALI",
			"MUSIC_ON" => en ? "Music: ON" : "Müzik: AÇIK",
			"MUSIC_OFF" => en ? "Music: OFF" : "Müzik: KAPALI",
			"THEME_LIGHT" => en ? "Theme: Light Factory" : "Arka Plan: Aydınlık",
			"THEME_DARK" => en ? "Theme: Dark Factory" : "Arka Plan: Karanlık",
			"CLOSE" => en ? "CLOSE" : "KAPAT",
			"SCORE" => en ? "SCORE" : "SKOR",
			"BEST" => en ? "BEST" : "REKOR",
			"NEXT" => en ? "NEXT" : "SIRADAKİ",
			"ROBOT_OUT" => en ? "ROBOT EXIT" : "ROBOT ÇIKIŞI",
			"PAUSE_TITLE" => en ? "PAUSED" : "DURAKLATILDI",
			"RESUME" => en ? "RESUME" : "DEVAM ET",
			"MAIN_MENU" => en ? "MAIN MENU" : "ANA MENÜ",
			"GAME_OVER" => en ? "GAME OVER" : "OYUN BİTTİ",
			"CURRENT_SCORE" => en ? "Current Score" : "Mevcut Skor",
			"BEST_SCORE" => en ? "Best Score" : "En Yüksek Skor",
			"CONTINUE_AD" => en ? "CONTINUE      " : "DEVAM ET      ",
			"GIVE_UP" => en ? "GIVE UP" : "PES ET",
			"REPLAY" => en ? "PLAY AGAIN" : "TEKRAR OYNA",
			"GO_MAIN_MENU" => en ? "MAIN MENU" : "ANA MENÜYE DÖN",
			"AD_WATCHING" => en ? "Watching Rewarded Ad..." : "Ödüllü Reklam İzleniyor...",
			"CHAIN" => en ? "CHAIN" : "ZİNCİR",
			"ROBOT_BUILT" => en ? "ROBOT PRODUCED!" : "ROBOT ÜRETİLDİ!",
			"DANGER" => en ? "DANGER!" : "TEHLİKE!",
			_ => key
		};
	}
}
