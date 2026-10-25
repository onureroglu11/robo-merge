using Godot;
using System.Collections.Generic;

namespace MergeRobotB;

[Tool]
public partial class Main : Node2D
{
	[Export]
	public PackedScene PieceScene;

	// Oyun kutusu sınırları (720x1280 dikey ekran)
	private const float BoxLeftX = 60f;
	private const float BoxRightX = 660f;
	private const float BoxTopY = 260f;
	private const float BoxBottomY = 1170f;
	private const float DropHeightY = 195f;
	private const float DangerLineY = 335f;
	private const float Fill85LineY = BoxBottomY - (BoxBottomY - DangerLineY) * 0.85f;
	private const float DangerTimeLimit = 3.0f;

	// Konveyör koordinatları
	private const float NextConveyorSurfaceY = 118f;
	private const float NextPreviewTargetX = 115f;
	private const float NextPreviewStartX = -70f;
	private const float RobotConveyorY = 1205f;

	private Piece _currentPiece;
	private int _nextLevel = 1;
	private bool _isHolding = false;
	private bool _canSpawn = false;
	private bool _isGameOver = false;
	private bool _hasUsedContinue = false;

	private int _currentScore = 0;

	// Tehlike çizgisi sayaçları
	private float _dangerTimer = 0f;
	private float _warningBeepCooldown = 0f;

	// Sahne elemanları
	private FactoryVisuals _factoryBg;
	private FactoryVisuals _topConveyor;
	private ColorRect _boxBg;
	private Line2D _boxBorder;
	private Node2D _piecesContainer;
	private Line2D _aimGuideLine;
	private readonly List<Line2D> _dangerDashLines = new();
	private Label _dangerTimerLabel;

	// Arayüz elemanları
	private Panel _scorePanel;
	private Panel _bestPanel;
	private Label _scoreTitleLabel;
	private Label _scoreValueLabel;
	private Label _bestTitleLabel;
	private Label _bestValueLabel;
	private Label _nextTitleLabel;
	private Sprite2D _nextPreviewSprite;
	private Button _pauseButton;

	// Pause ekranı elemanları
	private ColorRect _pauseOverlay;
	private Label _pauseTitleLabel;
	private Button _resumeButton;
	private Button _pauseThemeButton;
	private Button _pauseSoundButton;
	private Button _pauseMusicButton;
	private Button _pauseMainMenuButton;

	// Oyun Sonu (Game Over) ekranı elemanları
	private ColorRect _gameOverOverlay;
	private Label _gameOverTitleLabel;
	private Label _goCurrentScoreLabel;
	private Label _goBestScoreLabel;
	private Button _continueAdButton;
	private Button _doneButton;
	private Button _replayButton;
	private Button _goMainMenuButton;
	private Label _adWatchingLabel;

	private readonly RandomNumberGenerator _rng = new();

	// Ses dosyaları
	private AudioStream _dropSound;
	private AudioStream _hitSound;
	private AudioStream _mergeSound;
	private AudioStream _chainSound;
	private AudioStream _robotSound;
	private AudioStream _warningSound;
	private AudioStream _gameOverSound;

	public override void _Ready()
	{
		_rng.Randomize();

		if (!Engine.IsEditorHint())
		{
			GameData.EnsureLoaded();
			GameData.EnsureBgmPlayer(GetTree());
		}

		if (PieceScene == null && ResourceLoader.Exists("res://scenes/Piece.tscn"))
		{
			PieceScene = GD.Load<PackedScene>("res://scenes/Piece.tscn");
		}

		foreach (Node child in GetChildren())
		{
			child.QueueFree();
		}
		_dangerDashLines.Clear();

		if (!Engine.IsEditorHint())
		{
			LoadAudioAssets();
		}

		BuildGameWorld();
		BuildHUD();
		BuildPauseModal();
		BuildGameOverModal();
		ApplyThemeColors();

		if (!Engine.IsEditorHint())
		{
			if (GameData.HasSavedSession)
			{
				RestoreSavedSession();
			}
			else
			{
				_currentScore = 0;
				_nextLevel = RollSpawnLevel();
				SpawnNextPieceFromConveyor();
			}
		}
		else if (_nextPreviewSprite != null && ResourceLoader.Exists("res://sprites/L1.png"))
		{
			_nextPreviewSprite.Texture = GD.Load<Texture2D>("res://sprites/L1.png");
			_nextPreviewSprite.Scale = new Vector2(0.22f, 0.22f);
		}

		UpdateScoreUI();

		// Godot Editöründe açıldığında sol taraftaki Scene ağacında tüm düğümlerin görünmesini sağlar
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

	private void LoadAudioAssets()
	{
		if (ResourceLoader.Exists("res://audio/drop.wav")) _dropSound = GD.Load<AudioStream>("res://audio/drop.wav");
		if (ResourceLoader.Exists("res://audio/hit.wav")) _hitSound = GD.Load<AudioStream>("res://audio/hit.wav");
		if (ResourceLoader.Exists("res://audio/merge.wav")) _mergeSound = GD.Load<AudioStream>("res://audio/merge.wav");
		if (ResourceLoader.Exists("res://audio/chain.wav")) _chainSound = GD.Load<AudioStream>("res://audio/chain.wav");
		if (ResourceLoader.Exists("res://audio/robot.wav")) _robotSound = GD.Load<AudioStream>("res://audio/robot.wav");
		if (ResourceLoader.Exists("res://audio/warning.wav")) _warningSound = GD.Load<AudioStream>("res://audio/warning.wav");
		if (ResourceLoader.Exists("res://audio/gameover.wav")) _gameOverSound = GD.Load<AudioStream>("res://audio/gameover.wav");
	}

	private void BuildGameWorld()
	{
		// 1. Fabrika arka planı
		_factoryBg = new FactoryVisuals
		{
			Name = "FactoryBackground",
			Kind = FactoryVisuals.VisualKind.Background
		};
		AddChild(_factoryBg);

		// 2. Oyun kutusu zemini ve nişan çizgisi
		_boxBg = new ColorRect
		{
			Name = "GameBoxBackground",
			Position = new Vector2(BoxLeftX, BoxTopY),
			Size = new Vector2(BoxRightX - BoxLeftX, BoxBottomY - BoxTopY),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		AddChild(_boxBg);

		_aimGuideLine = new Line2D
		{
			Name = "AimGuideLine",
			Points = new Vector2[] { new(360, DropHeightY), new(360, BoxBottomY) },
			Width = 3.5f,
			DefaultColor = new Color(0.45f, 0.85f, 1.0f, 0.28f),
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round
		};
		AddChild(_aimGuideLine);

		// 3. Kutunun fiziksel duvarları (Sol, Alt, Sağ)
		var gameBoxBody = new StaticBody2D { Name = "GameBoxBody" };
		var boxPhysMat = new PhysicsMaterial { Friction = 0.2f, Bounce = 0.05f };
		gameBoxBody.PhysicsMaterialOverride = boxPhysMat;

		var wallPoly = new CollisionPolygon2D
		{
			Name = "WallsCollisionPolygon",
			Polygon = new Vector2[]
			{
				new(BoxLeftX, BoxTopY - 60f),
				new(BoxLeftX, BoxBottomY),
				new(BoxRightX, BoxBottomY),
				new(BoxRightX, BoxTopY - 60f),
				new(BoxRightX + 45f, BoxTopY - 60f),
				new(BoxRightX + 45f, BoxBottomY + 45f),
				new(BoxLeftX - 45f, BoxBottomY + 45f),
				new(BoxLeftX - 45f, BoxTopY - 60f)
			}
		};
		gameBoxBody.AddChild(wallPoly);
		AddChild(gameBoxBody);

		_boxBorder = new Line2D
		{
			Name = "GameBoxBorder",
			Points = new Vector2[]
			{
				new(BoxLeftX - 6f, BoxTopY),
				new(BoxLeftX - 6f, BoxBottomY + 6f),
				new(BoxRightX + 6f, BoxBottomY + 6f),
				new(BoxRightX + 6f, BoxTopY)
			},
			Width = 14.0f,
			JointMode = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round
		};
		AddChild(_boxBorder);

		// 4. Kesikli tehlike çizgisi (Danger Line)
		var dangerHolder = new Node2D { Name = "DashedDangerLine" };
		AddChild(dangerHolder);

		float dashLen = 22f;
		float gapLen = 16f;
		int dashIdx = 1;
		for (float x = BoxLeftX + 12f; x < BoxRightX - 10f; x += dashLen + gapLen)
		{
			float xEnd = Mathf.Min(x + dashLen, BoxRightX - 10f);
			var dash = new Line2D
			{
				Name = $"Dash_{dashIdx++}",
				Points = new Vector2[] { new(x, DangerLineY), new(xEnd, DangerLineY) },
				Width = 5.0f,
				DefaultColor = new Color(1f, 1f, 1f, 0.45f),
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round
			};
			dangerHolder.AddChild(dash);
			_dangerDashLines.Add(dash);
		}

		_dangerTimerLabel = new Label
		{
			Name = "DangerTimerLabel",
			Position = new Vector2(BoxLeftX, DangerLineY - 46f),
			Size = new Vector2(600, 42),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
			ZIndex = 30
		};
		GameData.ApplySoftFont(_dangerTimerLabel, 28, new Color(1.0f, 0.35f, 0.35f), new Color(0.25f, 0.04f, 0.04f), 9);
		AddChild(_dangerTimerLabel);

		// 5. Sol üstteki Next Conveyor (Sadece yeni parça gelirken çalışır, parça beklerken durur)
		_topConveyor = new FactoryVisuals
		{
			Name = "NextConveyor",
			Kind = FactoryVisuals.VisualKind.CartoonWheelConveyor,
			Position = new Vector2(18f, NextConveyorSurfaceY),
			BeltSize = new Vector2(198f, 46f),
			IsRunning = false
		};
		AddChild(_topConveyor);

		_nextPreviewSprite = new Sprite2D
		{
			Name = "NextPiecePreview",
			Position = new Vector2(NextPreviewTargetX, NextConveyorSurfaceY - 28f),
			ZIndex = 10
		};
		AddChild(_nextPreviewSprite);

		// 6. Sağ alttaki Robot Çıkış Konveyörü
		BuildBottomRobotExitConveyor();

		_piecesContainer = new Node2D { Name = "PiecesContainer" };
		AddChild(_piecesContainer);
	}

	private void BuildBottomRobotExitConveyor()
	{
		var botConveyor = new FactoryVisuals
		{
			Name = "RobotExitConveyor",
			Kind = FactoryVisuals.VisualKind.RedRailConveyor,
			Position = new Vector2(455f, RobotConveyorY),
			BeltSize = new Vector2(265f, 42f),
			ShowRobotExitHint = true
		};
		AddChild(botConveyor);

		// Robotların çıkacağı sağ alt kapı ve görsel robot sembolü
		var gateFrame = new Panel
		{
			Name = "RobotExitGate",
			Position = new Vector2(655f, RobotConveyorY - 56f),
			Size = new Vector2(65f, 56f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var gateStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.12f, 0.16f, 0.85f),
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 0,
			CornerRadiusBottomLeft = 0,
			CornerRadiusBottomRight = 0,
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderColor = new Color(0.82f, 0.14f, 0.14f)
		};
		gateFrame.AddThemeStyleboxOverride("panel", gateStyle);
		AddChild(gateFrame);

		if (ResourceLoader.Exists("res://sprites/L8.png"))
		{
			var roboBadge = new Sprite2D
			{
				Name = "RobotExitSymbol",
				Texture = GD.Load<Texture2D>("res://sprites/L8.png"),
				Position = new Vector2(490f, RobotConveyorY - 20f),
				Scale = new Vector2(0.18f, 0.18f),
				Modulate = new Color(1.0f, 1.0f, 1.0f, 0.88f),
				ZIndex = 12
			};
			AddChild(roboBadge);
		}
	}

	private void BuildHUD()
	{
		var hud = new CanvasLayer { Name = "HUD" };
		AddChild(hud);

		_nextTitleLabel = new Label
		{
			Name = "NextLabel",
			Position = new Vector2(22, 16),
			Size = new Vector2(200, 36)
		};
		GameData.ApplySoftFont(_nextTitleLabel, 22, new Color(1.0f, 0.86f, 0.32f), new Color(0.25f, 0.14f, 0.04f), 8);
		hud.AddChild(_nextTitleLabel);

		_scorePanel = CreateHudBadgePanel(new Vector2(236, 18), new Vector2(168, 94));
		_scorePanel.Name = "ScorePanel";
		hud.AddChild(_scorePanel);

		_scoreTitleLabel = new Label
		{
			Name = "ScoreTitleLabel",
			Position = new Vector2(0, 6),
			Size = new Vector2(168, 30),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_scoreTitleLabel, 19, new Color(0.68f, 0.88f, 1.0f), new Color(0.10f, 0.18f, 0.30f), 7);
		_scorePanel.AddChild(_scoreTitleLabel);

		_scoreValueLabel = new Label
		{
			Name = "ScoreValueLabel",
			Position = new Vector2(0, 34),
			Size = new Vector2(168, 52),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_scoreValueLabel, 34, Colors.White, new Color(0.10f, 0.16f, 0.26f), 9);
		_scorePanel.AddChild(_scoreValueLabel);

		_bestPanel = CreateHudBadgePanel(new Vector2(416, 18), new Vector2(168, 94));
		_bestPanel.Name = "BestScorePanel";
		hud.AddChild(_bestPanel);

		_bestTitleLabel = new Label
		{
			Name = "BestTitleLabel",
			Position = new Vector2(0, 6),
			Size = new Vector2(168, 30),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_bestTitleLabel, 19, new Color(1.0f, 0.84f, 0.28f), new Color(0.32f, 0.18f, 0.04f), 7);
		_bestPanel.AddChild(_bestTitleLabel);

		_bestValueLabel = new Label
		{
			Name = "BestValueLabel",
			Position = new Vector2(0, 34),
			Size = new Vector2(168, 52),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_bestValueLabel, 34, new Color(1.0f, 0.90f, 0.42f), new Color(0.32f, 0.18f, 0.04f), 9);
		_bestPanel.AddChild(_bestValueLabel);

		_pauseButton = MainMenu.CreateGlossyPillButton(
			new Vector2(598, 22), new Vector2(96, 86),
			new Color(0.32f, 0.64f, 0.96f), new Color(0.15f, 0.36f, 0.64f), 30
		);
		_pauseButton.Name = "PauseButton";
		_pauseButton.Text = "II";
		_pauseButton.Pressed += OnPauseButtonPressed;
		hud.AddChild(_pauseButton);

		BuildLevelLegend(hud);
	}

	private static Panel CreateHudBadgePanel(Vector2 pos, Vector2 size)
	{
		var panel = new Panel
		{
			Position = pos,
			Size = size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.16f, 0.21f, 0.30f, 0.92f),
			CornerRadiusTopLeft = 22,
			CornerRadiusTopRight = 22,
			CornerRadiusBottomLeft = 22,
			CornerRadiusBottomRight = 22,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 6,
			BorderColor = new Color(0.09f, 0.12f, 0.18f)
		};
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	// Sol altta hangi parçanın hangisine dönüştüğünü gösteren sıralama barı
	private void BuildLevelLegend(CanvasLayer hud)
	{
		var legendBg = new Panel
		{
			Name = "LevelEvolutionBar",
			Position = new Vector2(45, 1188),
			Size = new Vector2(392, 64),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.19f, 0.27f, 0.88f),
			CornerRadiusTopLeft = 22,
			CornerRadiusTopRight = 22,
			CornerRadiusBottomLeft = 22,
			CornerRadiusBottomRight = 22,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 5,
			BorderColor = new Color(0.09f, 0.12f, 0.18f)
		};
		legendBg.AddThemeStyleboxOverride("panel", style);
		hud.AddChild(legendBg);

		for (int lvl = 1; lvl <= 8; lvl++)
		{
			string path = $"res://sprites/L{lvl}.png";
			if (ResourceLoader.Exists(path))
			{
				var sp = new Sprite2D
				{
					Name = $"LegendIcon_L{lvl}",
					Texture = GD.Load<Texture2D>(path),
					Position = new Vector2(26f + (lvl - 1) * 48f, 32f),
					Scale = new Vector2(0.16f, 0.16f)
				};
				legendBg.AddChild(sp);
			}
		}
	}

	private void BuildPauseModal()
	{
		var layer = new CanvasLayer { Name = "PauseLayer", Layer = 10, ProcessMode = ProcessModeEnum.Always };
		AddChild(layer);

		_pauseOverlay = new ColorRect
		{
			Name = "PauseOverlay",
			Color = new Color(0, 0, 0, 0.76f),
			Size = new Vector2(720, 1280),
			Visible = false
		};
		layer.AddChild(_pauseOverlay);

		var card = new Panel
		{
			Name = "PauseCard",
			Position = new Vector2(90, 240),
			Size = new Vector2(540, 740)
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
		card.AddThemeStyleboxOverride("panel", cardStyle);
		_pauseOverlay.AddChild(card);

		_pauseTitleLabel = new Label
		{
			Name = "PauseTitleLabel",
			Position = new Vector2(0, 32),
			Size = new Vector2(540, 65),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_pauseTitleLabel, 44, new Color(0.38f, 0.88f, 1.0f), new Color(0.08f, 0.20f, 0.34f), 11);
		card.AddChild(_pauseTitleLabel);

		_resumeButton = MainMenu.CreateGlossyPillButton(
			new Vector2(60, 125), new Vector2(420, 82),
			new Color(0.24f, 0.82f, 0.44f), new Color(0.12f, 0.48f, 0.24f), 30
		);
		_resumeButton.Name = "ResumeButton";
		_resumeButton.Pressed += OnResumePressed;
		card.AddChild(_resumeButton);

		_pauseThemeButton = MainMenu.CreateGlossyPillButton(
			new Vector2(60, 228), new Vector2(420, 78),
			new Color(0.52f, 0.42f, 0.88f), new Color(0.30f, 0.22f, 0.58f), 26
		);
		_pauseThemeButton.Name = "ThemeButton";
		_pauseThemeButton.Pressed += () =>
		{
			GameData.SetLightTheme(!GameData.IsLightTheme);
			ApplyThemeColors();
			RefreshModalTexts();
		};
		card.AddChild(_pauseThemeButton);

		_pauseSoundButton = MainMenu.CreateGlossyPillButton(
			new Vector2(60, 326), new Vector2(420, 78),
			new Color(0.24f, 0.62f, 0.92f), new Color(0.12f, 0.36f, 0.60f), 28
		);
		_pauseSoundButton.Name = "SoundButton";
		_pauseSoundButton.Pressed += () =>
		{
			GameData.SetSound(!GameData.SoundEnabled);
			RefreshModalTexts();
		};
		card.AddChild(_pauseSoundButton);

		_pauseMusicButton = MainMenu.CreateGlossyPillButton(
			new Vector2(60, 424), new Vector2(420, 78),
			new Color(0.24f, 0.62f, 0.92f), new Color(0.12f, 0.36f, 0.60f), 28
		);
		_pauseMusicButton.Name = "MusicButton";
		_pauseMusicButton.Pressed += () =>
		{
			GameData.SetMusic(!GameData.MusicEnabled);
			RefreshModalTexts();
		};
		card.AddChild(_pauseMusicButton);

		_pauseMainMenuButton = MainMenu.CreateGlossyPillButton(
			new Vector2(60, 560), new Vector2(420, 82),
			new Color(0.92f, 0.36f, 0.32f), new Color(0.58f, 0.16f, 0.14f), 30
		);
		_pauseMainMenuButton.Name = "MainMenuButton";
		_pauseMainMenuButton.Pressed += OnPauseMainMenuPressed;
		card.AddChild(_pauseMainMenuButton);
	}

	private void BuildGameOverModal()
	{
		var layer = new CanvasLayer { Name = "GameOverLayer", Layer = 12, ProcessMode = ProcessModeEnum.Always };
		AddChild(layer);

		_gameOverOverlay = new ColorRect
		{
			Name = "GameOverOverlay",
			Color = new Color(0, 0, 0, 0.82f),
			Size = new Vector2(720, 1280),
			Visible = false
		};
		layer.AddChild(_gameOverOverlay);

		var card = new Panel
		{
			Name = "GameOverCard",
			Position = new Vector2(85, 280),
			Size = new Vector2(550, 670)
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
		card.AddThemeStyleboxOverride("panel", cardStyle);
		_gameOverOverlay.AddChild(card);

		_gameOverTitleLabel = new Label
		{
			Name = "GameOverTitleLabel",
			Position = new Vector2(0, 34),
			Size = new Vector2(550, 65),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_gameOverTitleLabel, 48, new Color(1.0f, 0.38f, 0.38f), new Color(0.32f, 0.06f, 0.06f), 12);
		card.AddChild(_gameOverTitleLabel);

		_goCurrentScoreLabel = new Label
		{
			Name = "CurrentScoreLabel",
			Position = new Vector2(0, 125),
			Size = new Vector2(550, 52),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_goCurrentScoreLabel, 34, Colors.White, new Color(0.10f, 0.16f, 0.26f), 9);
		card.AddChild(_goCurrentScoreLabel);

		_goBestScoreLabel = new Label
		{
			Name = "BestScoreLabel",
			Position = new Vector2(0, 185),
			Size = new Vector2(550, 52),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		GameData.ApplySoftFont(_goBestScoreLabel, 30, new Color(1.0f, 0.86f, 0.32f), new Color(0.32f, 0.18f, 0.04f), 9);
		card.AddChild(_goBestScoreLabel);

		_adWatchingLabel = new Label
		{
			Name = "AdWatchingLabel",
			Position = new Vector2(0, 250),
			Size = new Vector2(550, 45),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		GameData.ApplySoftFont(_adWatchingLabel, 24, new Color(0.42f, 0.94f, 0.58f), new Color(0.08f, 0.28f, 0.14f), 8);
		card.AddChild(_adWatchingLabel);

		// 1. Aşama Butonları: Devam Et (Sağında Reklam Sembolü ile) ve kırmızı Pes Et
		_continueAdButton = MainMenu.CreateGlossyPillButton(
			new Vector2(65, 315), new Vector2(420, 86),
			new Color(0.24f, 0.82f, 0.44f), new Color(0.12f, 0.48f, 0.24f), 28
		);
		_continueAdButton.Name = "ContinueAdButton";
		_continueAdButton.Pressed += OnContinueWithAdPressed;
		AddAdSymbolToButton(_continueAdButton);
		card.AddChild(_continueAdButton);

		_doneButton = MainMenu.CreateGlossyPillButton(
			new Vector2(65, 430), new Vector2(420, 80),
			new Color(0.92f, 0.34f, 0.32f), new Color(0.58f, 0.16f, 0.14f), 28
		);
		_doneButton.Name = "GiveUpButton";
		_doneButton.Pressed += OnDoneGameOverPressed;
		card.AddChild(_doneButton);

		// 2. Aşama Butonları (Pes Et seçildikten sonra gelen çerçeve): Tekrar Oyna ve Ana Menüye Dön
		_replayButton = MainMenu.CreateGlossyPillButton(
			new Vector2(65, 315), new Vector2(420, 86),
			new Color(1.0f, 0.76f, 0.14f), new Color(0.72f, 0.44f, 0.04f), 30
		);
		_replayButton.Name = "ReplayButton";
		_replayButton.Visible = false;
		_replayButton.Pressed += OnReplayPressed;
		card.AddChild(_replayButton);

		_goMainMenuButton = MainMenu.CreateGlossyPillButton(
			new Vector2(65, 430), new Vector2(420, 80),
			new Color(0.32f, 0.64f, 0.96f), new Color(0.15f, 0.36f, 0.64f), 28
		);
		_goMainMenuButton.Name = "GameOverMainMenuButton";
		_goMainMenuButton.Visible = false;
		_goMainMenuButton.Pressed += OnGoMainMenuFromGameOverPressed;
		card.AddChild(_goMainMenuButton);
	}

	// Devam Et butonunun sağına şık bir video/reklam (Play + AD) sembolü çizer
	private static void AddAdSymbolToButton(Button btn)
	{
		var badge = new Panel
		{
			Position = new Vector2(btn.Size.X - 92f, 16f),
			Size = new Vector2(68, 48),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var badgeStyle = new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 0.76f, 0.14f),
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 4,
			BorderColor = new Color(0.55f, 0.32f, 0.04f)
		};
		badge.AddThemeStyleboxOverride("panel", badgeStyle);

		// Küçük beyaz Play üçgeni
		var playTri = new Polygon2D
		{
			Polygon = new Vector2[] { new(14, 12), new(14, 34), new(31, 23) },
			Color = Colors.White
		};
		badge.AddChild(playTri);

		var adLbl = new Label
		{
			Text = "AD",
			Position = new Vector2(31, 9),
			Size = new Vector2(34, 30),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		GameData.ApplySoftFont(adLbl, 15, Colors.White, new Color(0.45f, 0.24f, 0.02f), 5);
		badge.AddChild(adLbl);

		btn.AddChild(badge);
	}

	private void ApplyThemeColors()
	{
		_factoryBg?.QueueRedraw();
		bool light = GameData.IsLightTheme;
		if (light)
		{
			_boxBg.Color = new Color(0.16f, 0.21f, 0.22f, 0.55f);
			_boxBorder.DefaultColor = new Color(0.22f, 0.26f, 0.28f);
			_aimGuideLine.DefaultColor = new Color(0.85f, 0.96f, 1.0f, 0.35f);
		}
		else
		{
			_boxBg.Color = new Color(0.06f, 0.08f, 0.11f, 0.62f);
			_boxBorder.DefaultColor = new Color(0.36f, 0.44f, 0.54f);
			_aimGuideLine.DefaultColor = new Color(0.45f, 0.85f, 1.0f, 0.28f);
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _isGameOver || GetTree().Paused) return;

		float dt = (float)delta;

		// Oyuncu basılı tutarken parçayı sadece yatay (X) eksende kutu içinde gezdir
		if (_isHolding && _currentPiece != null && IsInstanceValid(_currentPiece))
		{
			Vector2 mousePos = GetGlobalMousePosition();
			float radius = _currentPiece.GetEffectiveRadius();
			float minX = BoxLeftX + radius + 4f;
			float maxX = BoxRightX - radius - 4f;
			float clampedX = Mathf.Clamp(mousePos.X, minX, maxX);
			_currentPiece.GlobalPosition = new Vector2(clampedX, DropHeightY);
		}

		if (_currentPiece != null && IsInstanceValid(_currentPiece) && _canSpawn)
		{
			_aimGuideLine.Visible = true;
			float x = _currentPiece.GlobalPosition.X;
			_aimGuideLine.Points = new Vector2[] { new(x, DropHeightY), new(x, BoxBottomY) };
		}
		else
		{
			_aimGuideLine.Visible = false;
		}

		UpdateDangerState(dt);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || _isGameOver || GetTree().Paused) return;

		if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			// Sağ üstteki Pause butonuna tıklanıyorsa parçayı düşürme
			if (mouseBtn.Position.Y < 115f && mouseBtn.Position.X > 580f)
				return;

			if (mouseBtn.Pressed)
			{
				if (_currentPiece != null && IsInstanceValid(_currentPiece) && _canSpawn)
				{
					_isHolding = true;
					float radius = _currentPiece.GetEffectiveRadius();
					float clampedX = Mathf.Clamp(mouseBtn.Position.X, BoxLeftX + radius + 4f, BoxRightX - radius - 4f);
					_currentPiece.GlobalPosition = new Vector2(clampedX, DropHeightY);
				}
			}
			else
			{
				if (_isHolding && _currentPiece != null && IsInstanceValid(_currentPiece))
				{
					DropActivePiece();
				}
			}
		}
	}

	// Konveyördeki parçayı sahneye alır, yeni parçayı da konveyör bandıyla getirir
	private void SpawnNextPieceFromConveyor(int? forcedCurrentLevel = null)
	{
		if (PieceScene == null || _isGameOver) return;

		int levelToSpawn = forcedCurrentLevel ?? _nextLevel;
		_nextLevel = RollSpawnLevel();

		_currentPiece = PieceScene.Instantiate<Piece>();
		_currentPiece.Freeze = true;
		_piecesContainer.AddChild(_currentPiece);
		_currentPiece.SetLevel(levelToSpawn);

		_currentPiece.GlobalPosition = new Vector2(NextPreviewTargetX, NextConveyorSurfaceY - 30f);
		var moveTween = CreateTween();
		moveTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		moveTween.TweenProperty(_currentPiece, "global_position", new Vector2(360f, DropHeightY), 0.25f);

		AnimateNextConveyorPreview();

		_canSpawn = true;
	}

	// Sıradaki parçayı konveyörle uyumlu hızda getirir; parça yerine varınca konveyör de durur
	private void AnimateNextConveyorPreview()
	{
		if (_nextPreviewSprite == null) return;

		string path = $"res://sprites/L{_nextLevel}.png";
		if (ResourceLoader.Exists(path))
		{
			_nextPreviewSprite.Texture = GD.Load<Texture2D>(path);
		}

		float scale = 0.22f + (_nextLevel - 1) * 0.04f;
		_nextPreviewSprite.Scale = new Vector2(scale, scale);

		float visualRadius = 85f * scale;
		float targetY = NextConveyorSurfaceY - visualRadius + 4f;
		_nextPreviewSprite.Position = new Vector2(NextPreviewStartX, targetY);

		float travelDistance = NextPreviewTargetX - NextPreviewStartX; // 185px
		float duration = 0.45f;

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		// Parçayı konveyör üzerinde hedef noktaya taşı
		tween.TweenProperty(_nextPreviewSprite, "position:x", NextPreviewTargetX, duration);

		// Üst konveyörün dişlerini ve çarklarını parçanın aldığı mesafeyle birebir aynı hızda döndür;
		// parça durduğunda tween bittiği için konveyör de tam o anda durur
		if (_topConveyor != null)
		{
			float startOffset = _topConveyor.AnimOffset;
			float endOffset = startOffset + travelDistance;
			tween.TweenMethod(Callable.From<float>(val =>
			{
				_topConveyor.AnimOffset = val % 1000f;
				_topConveyor.QueueRedraw();
			}), startOffset, endOffset, duration);
		}
	}

	private async void DropActivePiece()
	{
		if (_currentPiece == null || !IsInstanceValid(_currentPiece)) return;

		PlaySfx(_dropSound);

		_isHolding = false;
		_canSpawn = false;

		var droppedPiece = _currentPiece;
		_currentPiece = null;

		droppedPiece.Freeze = false;

		GetTree().CreateTimer(0.35f).Timeout += () =>
		{
			if (IsInstanceValid(droppedPiece) && !droppedPiece.IsQueuedForDeletion())
			{
				droppedPiece.HasDropped = true;
			}
		};

		await ToSignal(GetTree().CreateTimer(0.50f), SceneTreeTimer.SignalName.Timeout);

		if (!_isGameOver)
		{
			SpawnNextPieceFromConveyor();
		}
	}

	// İki aynı seviye parça çarpıştığında yeni seviye üretir ve puan ekler
	public void MergePieces(int resultingLevel, Vector2 spawnPosition, int chainStep)
	{
		if (PieceScene == null || _isGameOver) return;

		int mergedLevel = resultingLevel - 1;
		int basePoints = GetPointsForMerge(mergedLevel);

		// Zincir birleşmesi varsa ekstra puan ekle (2. merge +10, 3. merge +20...)
		int chainBonus = (chainStep >= 2) ? (chainStep - 1) * 10 : 0;
		int totalPoints = basePoints + chainBonus;

		AddScore(totalPoints);

		if (chainStep >= 2)
		{
			PlaySfx(_chainSound, 0.95f + chainStep * 0.06f);
			ShowFloatingText($"{GameData.Tr("CHAIN")} x{chainStep}! +{totalPoints}", spawnPosition, resultingLevel, new Color(0.35f, 0.95f, 1.0f));
		}
		else
		{
			float pitch = 0.92f + (resultingLevel * 0.05f);
			PlaySfx(_mergeSound, pitch);
			ShowFloatingText($"+{totalPoints}", spawnPosition, resultingLevel, new Color(1.0f, 0.85f, 0.22f));
		}

		CreateMergeParticles(spawnPosition, resultingLevel);

		// İki L8 Robot birleştiğinde üretilen robotu sağ alt konveyörden tahliye et
		if (resultingLevel > 8)
		{
			PlaySfx(_robotSound);
			ShowFloatingText($"{GameData.Tr("ROBOT_BUILT")} +{totalPoints}", spawnPosition + new Vector2(0, -35f), 9, new Color(0.35f, 1.0f, 0.55f));
			ProduceAndEvacuateRobot(spawnPosition);
			return;
		}

		var newPiece = PieceScene.Instantiate<Piece>();
		newPiece.GlobalPosition = spawnPosition;
		newPiece.Freeze = false;
		newPiece.HasDropped = true;
		newPiece.ChainCount = chainStep;

		_piecesContainer.CallDeferred(Node.MethodName.AddChild, newPiece);
		newPiece.CallDeferred(nameof(Piece.SetLevel), resultingLevel);
		newPiece.CallDeferred(nameof(Piece.PlaySpawnAnimation));
	}

	private void ProduceAndEvacuateRobot(Vector2 spawnPosition)
	{
		var robotSprite = new Sprite2D
		{
			GlobalPosition = spawnPosition,
			Scale = new Vector2(0.85f, 0.85f),
			ZIndex = 25
		};
		if (ResourceLoader.Exists("res://sprites/L8.png"))
		{
			robotSprite.Texture = GD.Load<Texture2D>("res://sprites/L8.png");
		}
		AddChild(robotSprite);

		var tween = CreateTween();
		Vector2 conveyorEntry = new Vector2(510f, RobotConveyorY - 56f);
		tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(robotSprite, "global_position", conveyorEntry, 0.65f);

		tween.Chain().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tween.TweenProperty(robotSprite, "global_position:x", 820f, 0.80f);

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(robotSprite))
				robotSprite.QueueFree();
		}));
	}

	private void SetDangerDashesAppearance(Color color, float width)
	{
		foreach (var dash in _dangerDashLines)
		{
			dash.DefaultColor = color;
			dash.Width = width;
		}
	}

	// Tehlike çizgisi ve 3 saniyelik Game Over sayacı kontrolü
	private void UpdateDangerState(float dt)
	{
		bool is85PercentFull = false;
		bool anyPieceOverDanger = false;

		foreach (Node child in _piecesContainer.GetChildren())
		{
			if (child is not Piece piece || !IsInstanceValid(piece) || piece.IsQueuedForDeletion())
				continue;

			if (piece == _currentPiece || !piece.HasDropped)
			{
				piece.SetDangerWarning(false);
				continue;
			}

			float topEdgeY = piece.GlobalPosition.Y - piece.GetEffectiveRadius();

			if (topEdgeY <= Fill85LineY)
			{
				is85PercentFull = true;
			}

			if (topEdgeY <= DangerLineY && piece.LinearVelocity.Length() < 85f)
			{
				anyPieceOverDanger = true;
				piece.SetDangerWarning(true);
			}
			else
			{
				piece.SetDangerWarning(false);
			}
		}

		if (anyPieceOverDanger)
		{
			_dangerTimer += dt;
			float remaining = Mathf.Max(0f, DangerTimeLimit - _dangerTimer);

			SetDangerDashesAppearance(new Color(1.0f, 0.22f, 0.22f, 0.95f), 6.5f);

			_dangerTimerLabel.Visible = true;
			_dangerTimerLabel.Text = $"⚠ {GameData.Tr("DANGER")} {remaining:0.0}s";

			_warningBeepCooldown -= dt;
			if (_warningBeepCooldown <= 0f)
			{
				_warningBeepCooldown = 0.65f;
				PlaySfx(_warningSound, 1.0f, -6f);
			}

			if (_dangerTimer >= DangerTimeLimit)
			{
				TriggerGameOver();
			}
		}
		else
		{
			_dangerTimer = 0f;
			_warningBeepCooldown = 0f;
			_dangerTimerLabel.Visible = false;

			if (is85PercentFull)
			{
				SetDangerDashesAppearance(new Color(1.0f, 0.45f, 0.22f, 0.80f), 5.5f);
			}
			else
			{
				Color idleColor = GameData.IsLightTheme
					? new Color(1.0f, 1.0f, 1.0f, 0.72f)
					: new Color(1.0f, 1.0f, 1.0f, 0.42f);
				SetDangerDashesAppearance(idleColor, 4.5f);
			}
		}
	}

	private void TriggerGameOver()
	{
		_isGameOver = true;
		_dangerTimerLabel.Visible = false;
		GameData.ClearSessionState();

		PlaySfx(_gameOverSound);

		RefreshModalTexts();
		_adWatchingLabel.Visible = false;
		_continueAdButton.Visible = !_hasUsedContinue;
		_doneButton.Visible = !_hasUsedContinue;
		_replayButton.Visible = _hasUsedContinue;
		_goMainMenuButton.Visible = _hasUsedContinue;

		_gameOverOverlay.Visible = true;
	}

	// Ödüllü reklam sonrası üstteki parçaları temizleyip oyuna devam ettirir
	private async void OnContinueWithAdPressed()
	{
		_continueAdButton.Disabled = true;
		_doneButton.Disabled = true;
		_adWatchingLabel.Text = GameData.Tr("AD_WATCHING");
		_adWatchingLabel.Visible = true;

		await ToSignal(GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);

		_hasUsedContinue = true;
		_adWatchingLabel.Visible = false;
		_continueAdButton.Disabled = false;
		_doneButton.Disabled = false;
		_gameOverOverlay.Visible = false;

		float safeClearThresholdY = BoxTopY + (BoxBottomY - BoxTopY) * 0.52f;
		foreach (Node child in _piecesContainer.GetChildren())
		{
			if (child is Piece piece && IsInstanceValid(piece) && piece != _currentPiece)
			{
				if (piece.GlobalPosition.Y < safeClearThresholdY)
				{
					CreateMergeParticles(piece.GlobalPosition, piece.Level);
					piece.QueueFree();
				}
				else
				{
					piece.SetDangerWarning(false);
				}
			}
		}

		_dangerTimer = 0f;
		_isGameOver = false;

		if (_currentPiece == null || !IsInstanceValid(_currentPiece))
		{
			SpawnNextPieceFromConveyor();
		}
	}

	// Pes Et seçildiğinde 2. çerçeveye geçer (Tekrar Oyna ve Ana Menüye Dön butonları görünür)
	private void OnDoneGameOverPressed()
	{
		_continueAdButton.Visible = false;
		_doneButton.Visible = false;
		_replayButton.Visible = true;
		_goMainMenuButton.Visible = true;
	}

	private void OnReplayPressed()
	{
		GameData.ClearSessionState();
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	private void OnGoMainMenuFromGameOverPressed()
	{
		GameData.ClearSessionState();
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}

	private void OnPauseButtonPressed()
	{
		if (_isGameOver) return;
		RefreshModalTexts();
		_pauseOverlay.Visible = true;
		GetTree().Paused = true;
	}

	private void OnResumePressed()
	{
		_pauseOverlay.Visible = false;
		GetTree().Paused = false;
	}

	private void OnPauseMainMenuPressed()
	{
		SaveCurrentSessionState();
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}

	private void SaveCurrentSessionState()
	{
		var pieceList = new List<SavedPieceData>();
		foreach (Node child in _piecesContainer.GetChildren())
		{
			if (child is Piece p && IsInstanceValid(p) && p != _currentPiece && !p.IsQueuedForDeletion())
			{
				pieceList.Add(new SavedPieceData
				{
					Level = p.Level,
					X = p.GlobalPosition.X,
					Y = p.GlobalPosition.Y,
					Rotation = p.Rotation
				});
			}
		}

		int currentHoldLevel = (_currentPiece != null && IsInstanceValid(_currentPiece)) ? _currentPiece.Level : 1;
		GameData.SaveSessionState(_currentScore, currentHoldLevel, _nextLevel, pieceList);
	}

	private void RestoreSavedSession()
	{
		_currentScore = GameData.SavedScore;
		_nextLevel = GameData.SavedNextPieceLevel;

		foreach (var data in GameData.SavedPieces)
		{
			var p = PieceScene.Instantiate<Piece>();
			p.GlobalPosition = new Vector2(data.X, data.Y);
			p.Rotation = data.Rotation;
			p.Freeze = false;
			p.HasDropped = true;
			_piecesContainer.AddChild(p);
			p.SetLevel(data.Level);
		}

		SpawnNextPieceFromConveyor(GameData.SavedCurrentPieceLevel);
	}

	// Rastgele parça ağırlıkları (L1: %50, L2: %30, L3: %15, L4: %5)
	private int RollSpawnLevel()
	{
		int roll = _rng.RandiRange(1, 100);
		if (roll <= 50) return 1;
		if (roll <= 80) return 2;
		if (roll <= 95) return 3;
		return 4;
	}

	private static int GetPointsForMerge(int mergedLevel)
	{
		return mergedLevel switch
		{
			1 => 10,
			2 => 20,
			3 => 40,
			4 => 80,
			5 => 150,
			6 => 220,
			7 => 300,
			8 => 500,
			_ => 300
		};
	}

	private void AddScore(int points)
	{
		_currentScore += points;
		GameData.SetBestScore(_currentScore);
		UpdateScoreUI();
	}

	private void UpdateScoreUI()
	{
		_scoreTitleLabel.Text = GameData.Tr("SCORE");
		_scoreValueLabel.Text = _currentScore.ToString();
		_bestTitleLabel.Text = GameData.Tr("BEST");
		_bestValueLabel.Text = GameData.BestScore.ToString();
		_nextTitleLabel.Text = GameData.Tr("NEXT");
	}

	private void RefreshModalTexts()
	{
		UpdateScoreUI();

		_pauseTitleLabel.Text = GameData.Tr("PAUSE_TITLE");
		_resumeButton.Text = GameData.Tr("RESUME");
		_pauseThemeButton.Text = GameData.IsLightTheme ? GameData.Tr("THEME_LIGHT") : GameData.Tr("THEME_DARK");
		_pauseSoundButton.Text = GameData.SoundEnabled ? GameData.Tr("SOUND_ON") : GameData.Tr("SOUND_OFF");
		_pauseMusicButton.Text = GameData.MusicEnabled ? GameData.Tr("MUSIC_ON") : GameData.Tr("MUSIC_OFF");
		_pauseMainMenuButton.Text = GameData.Tr("MAIN_MENU");

		_gameOverTitleLabel.Text = GameData.Tr("GAME_OVER");
		_goCurrentScoreLabel.Text = $"{GameData.Tr("CURRENT_SCORE")}: {_currentScore}";
		_goBestScoreLabel.Text = $"★ {GameData.Tr("BEST_SCORE")}: {GameData.BestScore}";
		_continueAdButton.Text = GameData.Tr("CONTINUE_AD");
		_doneButton.Text = GameData.Tr("GIVE_UP");
		_replayButton.Text = GameData.Tr("REPLAY");
		_goMainMenuButton.Text = GameData.Tr("GO_MAIN_MENU");
	}

	private void ShowFloatingText(string text, Vector2 position, int level, Color color)
	{
		var label = new Label
		{
			Text = text,
			ZIndex = 30,
			GlobalPosition = position - new Vector2(45f, 32f + level * 4f)
		};
		GameData.ApplySoftFont(label, 22 + level * 2, color, new Color(0.15f, 0.08f, 0.02f), 8);
		AddChild(label);

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position:y", label.Position.Y - (50f + level * 6f), 0.70f)
			 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "modulate:a", 0f, 0.70f);
		tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
	}

	private void CreateMergeParticles(Vector2 position, int level)
	{
		var particles = new CpuParticles2D
		{
			GlobalPosition = position,
			ZIndex = 20,
			Emitting = false,
			OneShot = true,
			Explosiveness = 0.95f,
			Amount = 14 + level * 2,
			Lifetime = 0.42f + level * 0.03f,
			Direction = Vector2.Zero,
			Spread = 180f,
			InitialVelocityMin = 75f + level * 18f,
			InitialVelocityMax = 150f + level * 32f,
			Gravity = new Vector2(0f, 260f),
			ScaleAmountMin = 2.5f + level * 0.4f,
			ScaleAmountMax = 5.0f + level * 0.5f,
			Color = new Color(1.0f, 0.78f, 0.25f)
		};
		AddChild(particles);
		particles.Emitting = true;

		GetTree().CreateTimer(0.85f).Timeout += () =>
		{
			if (IsInstanceValid(particles))
				particles.QueueFree();
		};
	}

	public void PlayCollisionSfx(float pitchScale, float volumeDb)
	{
		PlaySfx(_hitSound, pitchScale, volumeDb);
	}

	public void PlaySfx(AudioStream stream, float pitchScale = 1.0f, float volumeDb = 0.0f)
	{
		if (stream == null || !GameData.SoundEnabled) return;

		var player = new AudioStreamPlayer
		{
			Stream = stream,
			PitchScale = pitchScale,
			VolumeDb = volumeDb
		};
		AddChild(player);
		player.Play();
		player.Finished += () => player.QueueFree();
	}
}
