using Godot;
using System.Collections.Generic;

namespace MergeRobotB;

[Tool]
public partial class MainMenu : Control
{
	private FactoryVisuals _factoryBg;
	private Label _titleLabel;
	private Label _bestScoreValueLabel;
	private Label _bestScoreCaptionLabel;
	private Button _continueButton;
	private Button _playButton;
	private Button _settingsButton;

	// Ayarlar paneli elemanları
	private ColorRect _settingsOverlay;
	private Panel _settingsCard;
	private Label _settingsTitle;
	private Button _langButton;
	private Button _themeButton;
	private Button _soundButton;
	private Button _musicButton;
	private Button _closeSettingsButton;

	// Alt kısımdaki fabrika bandında yürüyen parçalar
	private readonly List<Sprite2D> _conveyorPieceSprites = new();
	private Sprite2D _heroRobotSprite;
	private float _animTime = 0f;

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
		{
			GameData.EnsureLoaded();
			GameData.EnsureBgmPlayer(GetTree());
		}

		// Eski otomatik düğümler varsa temizle
		foreach (Node child in GetChildren())
		{
			child.QueueFree();
		}
		_conveyorPieceSprites.Clear();

		BuildMenuUI();
		ApplyThemeColors();
		RefreshTexts();

		// Editörde açıldığında sol taraftaki Scene ağacında tüm düğümlerin görünmesini sağlar
		if (Engine.IsEditorHint())
		{
			AssignOwnerRecursive(this, this);
		}
	}

	private static void AssignOwnerRecursive(Node current, Node rootOwner)
	{
		foreach (Node child in current.GetChildren())
		{
			child.Owner = rootOwner;
			AssignOwnerRecursive(child, rootOwner);
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;

		float dt = (float)delta;
		_animTime += dt;

		// Ortadaki ana robotun hafif nefes alma / sallanma hareketi
		if (_heroRobotSprite != null)
		{
			_heroRobotSprite.Position = new Vector2(360f, 340f + Mathf.Sin(_animTime * 2.2f) * 6f);
		}

		// Alt konveyör üzerindeki parçaları sağa doğru kaydır, ekrandan çıkınca sola sar
		foreach (var sp in _conveyorPieceSprites)
		{
			sp.Position += new Vector2(48f * dt, 0f);
			if (sp.Position.X > 655f)
			{
				sp.Position = new Vector2(65f, sp.Position.Y);
			}
		}
	}

	private void BuildMenuUI()
	{
		// 1. Fabrika arka planı (Karanlık / Aydınlık mod destekli)
		_factoryBg = new FactoryVisuals
		{
			Name = "FactoryBackground",
			Kind = FactoryVisuals.VisualKind.Background
		};
		AddChild(_factoryBg);

		// 2. Oyun Başlığı ("RoboMerge")
		_titleLabel = new Label
		{
			Name = "TitleLabel",
			Position = new Vector2(40, 95),
			Size = new Vector2(640, 110),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_titleLabel, 76, new Color(1.0f, 0.74f, 0.20f), new Color(0.14f, 0.10f, 0.22f), 18);
		AddChild(_titleLabel);

		// 3. Başlığın altında duran ana L8 Robot ve taçlı rekor göstergesi
		if (ResourceLoader.Exists("res://sprites/L8.png"))
		{
			_heroRobotSprite = new Sprite2D
			{
				Name = "HeroRobotSprite",
				Texture = GD.Load<Texture2D>("res://sprites/L8.png"),
				Position = new Vector2(360f, 340f),
				Scale = new Vector2(0.68f, 0.68f)
			};
			AddChild(_heroRobotSprite);
		}

		BuildCrownAndBestScore();

		// 4. Menü Butonları (Devam Et, Oyuna Başla, Ayarlar)
		_continueButton = CreateGlossyPillButton(
			new Vector2(150, 620), new Vector2(420, 82),
			new Color(0.24f, 0.82f, 0.44f), new Color(0.12f, 0.48f, 0.24f), 30
		);
		_continueButton.Name = "ContinueButton";
		_continueButton.Pressed += OnContinueGamePressed;
		AddChild(_continueButton);

		_playButton = CreateGlossyPillButton(
			new Vector2(135, 720), new Vector2(450, 96),
			new Color(1.0f, 0.76f, 0.14f), new Color(0.72f, 0.44f, 0.04f), 36
		);
		_playButton.Name = "PlayButton";
		_playButton.Pressed += OnPlayNewGamePressed;
		AddChild(_playButton);

		_settingsButton = CreateGlossyPillButton(
			new Vector2(200, 838), new Vector2(320, 72),
			new Color(0.32f, 0.64f, 0.96f), new Color(0.15f, 0.36f, 0.64f), 28
		);
		_settingsButton.Name = "SettingsButton";
		_settingsButton.Pressed += () => _settingsOverlay.Visible = true;
		AddChild(_settingsButton);

		// 5. Alt kısımda fabrika temasına uygun konveyör vitrini
		BuildFactoryConveyorShowcase();

		BuildSettingsModal();
	}

	private void BuildFactoryConveyorShowcase()
	{
		float beltY = 1095f;

		var showcaseBelt = new FactoryVisuals
		{
			Name = "BottomShowcaseConveyor",
			Kind = FactoryVisuals.VisualKind.CartoonWheelConveyor,
			Position = new Vector2(55f, beltY),
			BeltSize = new Vector2(610f, 52f),
			IsRunning = true,
			BeltSpeed = 48f
		};
		AddChild(showcaseBelt);

		var piecesNode = new Node2D { Name = "ShowcasePieces" };
		AddChild(piecesNode);

		for (int lvl = 1; lvl <= 7; lvl++)
		{
			string path = $"res://sprites/L{lvl}.png";
			if (!ResourceLoader.Exists(path)) continue;

			float scale = 0.22f + (lvl - 1) * 0.025f;
			float radius = 92f * scale;

			var sp = new Sprite2D
			{
				Name = $"ShowcasePiece_L{lvl}",
				Texture = GD.Load<Texture2D>(path),
				Position = new Vector2(75f + (lvl - 1) * 84f, beltY - radius + 4f),
				Scale = new Vector2(scale, scale)
			};
			piecesNode.AddChild(sp);
			_conveyorPieceSprites.Add(sp);
		}
	}

	private void BuildCrownAndBestScore()
	{
		var crownContainer = new Node2D
		{
			Name = "CrownIcon",
			Position = new Vector2(360, 470)
		};
		AddChild(crownContainer);

		var crownPts = new Vector2[]
		{
			new(-28, 14), new(-36, -16), new(-16, -2),
			new(0, -24), new(16, -2), new(36, -16), new(28, 14)
		};
		var crownOutline = new Polygon2D
		{
			Name = "CrownOutline",
			Polygon = new Vector2[]
			{
				new(-32, 18), new(-41, -21), new(-16, -7),
				new(0, -29), new(16, -7), new(41, -21), new(32, 18)
			},
			Color = new Color(0.55f, 0.36f, 0.08f)
		};
		crownContainer.AddChild(crownOutline);

		var crownFill = new Polygon2D
		{
			Name = "CrownFill",
			Polygon = crownPts,
			Color = new Color(1.0f, 0.90f, 0.45f)
		};
		crownContainer.AddChild(crownFill);

		_bestScoreValueLabel = new Label
		{
			Name = "BestScoreValueLabel",
			Position = new Vector2(210, 492),
			Size = new Vector2(300, 65),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_bestScoreValueLabel, 48, Colors.White, new Color(0.52f, 0.34f, 0.08f), 12);
		AddChild(_bestScoreValueLabel);

		_bestScoreCaptionLabel = new Label
		{
			Name = "BestScoreCaptionLabel",
			Position = new Vector2(180, 548),
			Size = new Vector2(360, 45),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_bestScoreCaptionLabel, 26, Colors.White, new Color(0.52f, 0.34f, 0.08f), 9);
		AddChild(_bestScoreCaptionLabel);
	}

	// Yuvarlak hatlı, parlak şeritli buton oluşturucu
	public static Button CreateGlossyPillButton(Vector2 pos, Vector2 size, Color mainColor, Color borderColor, int fontSize)
	{
		var btn = new Button
		{
			Position = pos,
			Size = size,
			FocusMode = FocusModeEnum.None
		};
		GameData.ApplySoftButtonFont(btn, fontSize, Colors.White, borderColor.Darkened(0.25f), 9);

		int radius = (int)(size.Y * 0.5f);
		var normalStyle = new StyleBoxFlat
		{
			BgColor = mainColor,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 9,
			BorderColor = borderColor,
			ShadowColor = new Color(0, 0, 0, 0.25f),
			ShadowSize = 6,
			ShadowOffset = new Vector2(0, 4)
		};
		var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
		hoverStyle.BgColor = mainColor.Lightened(0.08f);

		var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
		pressedStyle.BgColor = mainColor.Darkened(0.08f);
		pressedStyle.BorderWidthBottom = 5;

		btn.AddThemeStyleboxOverride("normal", normalStyle);
		btn.AddThemeStyleboxOverride("hover", hoverStyle);
		btn.AddThemeStyleboxOverride("pressed", pressedStyle);

		var gloss = new Panel
		{
			Name = "GlossHighlight",
			Position = new Vector2(22, 8),
			Size = new Vector2(size.X - 44, size.Y * 0.26f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		var glossStyle = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.28f),
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomLeft = 14,
			CornerRadiusBottomRight = 14
		};
		gloss.AddThemeStyleboxOverride("panel", glossStyle);
		btn.AddChild(gloss);

		return btn;
	}

	private void BuildSettingsModal()
	{
		_settingsOverlay = new ColorRect
		{
			Name = "SettingsPanel",
			Color = new Color(0, 0, 0, 0.72f),
			Size = new Vector2(720, 1280),
			Visible = false
		};
		AddChild(_settingsOverlay);

		_settingsCard = new Panel
		{
			Name = "SettingsCard",
			Position = new Vector2(85, 250),
			Size = new Vector2(550, 740)
		};
		var cardStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.16f, 0.20f, 0.28f),
			CornerRadiusTopLeft = 36,
			CornerRadiusTopRight = 36,
			CornerRadiusBottomLeft = 36,
			CornerRadiusBottomRight = 36,
			BorderWidthLeft = 5,
			BorderWidthTop = 5,
			BorderWidthRight = 5,
			BorderWidthBottom = 10,
			BorderColor = new Color(0.09f, 0.12f, 0.18f)
		};
		_settingsCard.AddThemeStyleboxOverride("panel", cardStyle);
		_settingsOverlay.AddChild(_settingsCard);

		_settingsTitle = new Label
		{
			Name = "SettingsTitle",
			Position = new Vector2(0, 32),
			Size = new Vector2(550, 65),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_settingsTitle, 44, new Color(1.0f, 0.84f, 0.25f), new Color(0.35f, 0.18f, 0.05f), 11);
		_settingsCard.AddChild(_settingsTitle);

		_themeButton = CreateGlossyPillButton(
			new Vector2(65, 125), new Vector2(420, 80),
			new Color(0.52f, 0.42f, 0.88f), new Color(0.30f, 0.22f, 0.58f), 26
		);
		_themeButton.Name = "ThemeButton";
		_themeButton.Pressed += () =>
		{
			GameData.SetLightTheme(!GameData.IsLightTheme);
			ApplyThemeColors();
			RefreshTexts();
		};
		_settingsCard.AddChild(_themeButton);

		_langButton = CreateGlossyPillButton(
			new Vector2(65, 225), new Vector2(420, 80),
			new Color(0.24f, 0.62f, 0.92f), new Color(0.12f, 0.36f, 0.60f), 28
		);
		_langButton.Name = "LanguageButton";
		_langButton.Pressed += () =>
		{
			GameData.SetLanguage(GameData.Language == "TR" ? "EN" : "TR");
			RefreshTexts();
		};
		_settingsCard.AddChild(_langButton);

		_soundButton = CreateGlossyPillButton(
			new Vector2(65, 325), new Vector2(420, 80),
			new Color(0.24f, 0.76f, 0.52f), new Color(0.12f, 0.46f, 0.30f), 28
		);
		_soundButton.Name = "SoundButton";
		_soundButton.Pressed += () =>
		{
			GameData.SetSound(!GameData.SoundEnabled);
			RefreshTexts();
		};
		_settingsCard.AddChild(_soundButton);

		_musicButton = CreateGlossyPillButton(
			new Vector2(65, 425), new Vector2(420, 80),
			new Color(0.24f, 0.76f, 0.52f), new Color(0.12f, 0.46f, 0.30f), 28
		);
		_musicButton.Name = "MusicButton";
		_musicButton.Pressed += () =>
		{
			GameData.SetMusic(!GameData.MusicEnabled);
			RefreshTexts();
		};
		_settingsCard.AddChild(_musicButton);

		_closeSettingsButton = CreateGlossyPillButton(
			new Vector2(115, 580), new Vector2(320, 82),
			new Color(0.92f, 0.34f, 0.34f), new Color(0.58f, 0.16f, 0.16f), 30
		);
		_closeSettingsButton.Name = "CloseSettingsButton";
		_closeSettingsButton.Pressed += () => _settingsOverlay.Visible = false;
		_settingsCard.AddChild(_closeSettingsButton);
	}

	private void ApplyThemeColors()
	{
		_factoryBg?.QueueRedraw();
	}

	private void RefreshTexts()
	{
		_titleLabel.Text = GameData.Tr("TITLE");
		_bestScoreValueLabel.Text = GameData.BestScore.ToString();
		_bestScoreCaptionLabel.Text = GameData.Tr("BEST_SCORE");

		bool hasSession = GameData.HasSavedSession;
		_continueButton.Visible = hasSession;
		_continueButton.Text = $"{GameData.Tr("CONTINUE_GAME")} ({GameData.SavedScore})";

		_playButton.Text = hasSession ? GameData.Tr("NEW_GAME") : GameData.Tr("PLAY");
		_playButton.Position = hasSession ? new Vector2(135, 720) : new Vector2(135, 640);
		_settingsButton.Position = hasSession ? new Vector2(200, 838) : new Vector2(200, 760);

		_settingsButton.Text = GameData.Tr("SETTINGS");
		_settingsTitle.Text = GameData.Tr("SETTINGS");
		_themeButton.Text = GameData.IsLightTheme ? GameData.Tr("THEME_LIGHT") : GameData.Tr("THEME_DARK");
		_langButton.Text = GameData.Tr("LANGUAGE");
		_soundButton.Text = GameData.SoundEnabled ? GameData.Tr("SOUND_ON") : GameData.Tr("SOUND_OFF");
		_musicButton.Text = GameData.MusicEnabled ? GameData.Tr("MUSIC_ON") : GameData.Tr("MUSIC_OFF");
		_closeSettingsButton.Text = GameData.Tr("CLOSE");
	}

	private void OnContinueGamePressed()
	{
		if (Engine.IsEditorHint()) return;
		GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
	}

	private void OnPlayNewGamePressed()
	{
		if (Engine.IsEditorHint()) return;
		GameData.ClearSessionState();
		GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
	}
}
