using Godot;
using System;

public partial class Main : Node2D
{
	[Export]
	public PackedScene PieceScene;

	private const float DropHeightY = 180f;
	private const float MinX = 100f;
	private const float MaxX = 620f;
	private const float DangerTimeLimit = 3.0f; // 3 saniye taşma kuralı

	private Piece _currentPiece;
	private int _nextLevel = 1;
	private bool _isHolding = false;
	private bool _canSpawn = true;
	private bool _isGameOver = false;

	private int _currentScore = 0;
	private int _bestScore = 0;

	// UI Elemanları
	private Label _scoreLabel;
	private Label _bestScoreLabel;
	private Sprite2D _nextPreview;
	private ColorRect _gameOverPanel;
	private Label _finalScoreLabel;
	private Button _restartButton;

	// Tehlike Çizgisi Alanı
	private Area2D _dangerArea;
	private float _dangerTimer = 0f;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();

		_scoreLabel = GetNode<Label>("UI/ScoreLabel");
		_bestScoreLabel = GetNode<Label>("UI/BestScoreLabel");
		_nextPreview = GetNode<Sprite2D>("UI/NextPreview");

		_gameOverPanel = GetNode<ColorRect>("UI/GameOverPanel");
		_finalScoreLabel = GetNode<Label>("UI/GameOverPanel/FinalScoreLabel");
		_restartButton = GetNode<Button>("UI/GameOverPanel/RestartButton");
		_restartButton.Pressed += OnRestartButtonPressed;

		_dangerArea = GetNode<Area2D>("DangerArea");

		LoadBestScore();
		UpdateUI();

		_nextLevel = GetRandomSpawnLevel();
		SpawnNewPiece();
	}

	public override void _Process(double delta)
	{
		if (_isGameOver) return;

		// Tutma hareketi
		if (_isHolding && _currentPiece != null)
		{
			Vector2 mousePos = GetGlobalMousePosition();
			float clampedX = Mathf.Clamp(mousePos.X, MinX, MaxX);
			_currentPiece.GlobalPosition = new Vector2(clampedX, DropHeightY);
		}

		// Tehlike süresi kontrolü
		CheckDangerTimer((float)delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		
		if (_isGameOver) return;
		
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			if (mouseBtn.Pressed)
			{
				if (_currentPiece != null && _canSpawn)
				{
					_isHolding = true;
				}
			}
			else
			{
				if (_isHolding && _currentPiece != null)
				{
					DropPiece();
				}
			}
		}
	}

	private void SpawnNewPiece()
	{
		if (PieceScene == null || _isGameOver) return;

		int currentLevelToSpawn = _nextLevel;
		_nextLevel = GetRandomSpawnLevel();
		UpdateNextPreview();

		_currentPiece = PieceScene.Instantiate<Piece>();
		_currentPiece.Freeze = true;
		_currentPiece.GlobalPosition = new Vector2(360f, DropHeightY);

		AddChild(_currentPiece);
		_currentPiece.SetLevel(currentLevelToSpawn);

		_canSpawn = true;
	}

	private async void DropPiece()
	{
		if (_currentPiece == null) return;

		_isHolding = false;
		_canSpawn = false;

		var pieceToDrop = _currentPiece;
		_currentPiece = null;

		pieceToDrop.Freeze = false;

		GetTree().CreateTimer(0.3f).Timeout += () =>
		{
			if (IsInstanceValid(pieceToDrop) && !pieceToDrop.IsQueuedForDeletion())
			{
				pieceToDrop.HasDropped = true;
			}
		};

		await ToSignal(GetTree().CreateTimer(0.7f), SceneTreeTimer.SignalName.Timeout);

		if (!_isGameOver)
		{
			SpawnNewPiece();
		}
	}

	public void MergePieces(int nextLevel, Vector2 spawnPosition)
	{
		if (PieceScene == null || _isGameOver) return;

		int points = GetPointsForLevel(nextLevel - 1);
		AddScore(points);

		if (nextLevel > 8)
		{
			AddScore(300);
			return;
		}

		var newPiece = PieceScene.Instantiate<Piece>();
		newPiece.GlobalPosition = spawnPosition;
		newPiece.Freeze = false;
		newPiece.HasDropped = true;

		CallDeferred(MethodName.AddChild, newPiece);
		newPiece.CallDeferred(nameof(Piece.SetLevel), nextLevel);
		newPiece.CallDeferred(nameof(Piece.PlaySpawnAnimation));
	}

	private void CheckDangerTimer(float delta)
	{
		if (_dangerArea == null) return;

		var overlappingBodies = _dangerArea.GetOverlappingBodies();
		bool anyDangerousPiece = false;

		foreach (var body in overlappingBodies)
		{
			// Oyuncunun elinde tutmakta olduğu parça tehlike sayılmaz
			if (body == _currentPiece) continue;

			if (body is Piece piece && IsInstanceValid(piece) && !piece.IsQueuedForDeletion())
			{
				try
				{
					if (piece.HasDropped && piece.LinearVelocity.Length() < 50f)
					{
						anyDangerousPiece = true;
						break;
					}
				}
				catch (ObjectDisposedException)
				{
				}
			}
		}

		if (anyDangerousPiece)
		{
			_dangerTimer += delta;
			GD.Print($"Tehlike Sayacı: {_dangerTimer:0.0} / {DangerTimeLimit}");

			if (_dangerTimer >= DangerTimeLimit)
			{
				TriggerGameOver();
			}
		}
		else
		{
			_dangerTimer = 0f;
		}
	}

	private void TriggerGameOver()
	{
		_isGameOver = true;
		_gameOverPanel.Visible = true;
		_finalScoreLabel.Text = $"Skor: {_currentScore}";
	}

	private void OnRestartButtonPressed()
	{
		GetTree().ReloadCurrentScene();
	}

	private int GetRandomSpawnLevel()
	{
		int roll = _rng.RandiRange(1, 100);
		if (roll <= 50) return 1;
		if (roll <= 80) return 2;
		if (roll <= 95) return 3;
		return 4;
	}

	private int GetPointsForLevel(int level)
	{
		return level switch
		{
			1 => 10,
			2 => 20,
			3 => 40,
			4 => 80,
			5 => 150,
			6 => 200,
			7 => 250,
			_ => 300
		};
	}

	private void AddScore(int points)
	{
		_currentScore += points;
		if (_currentScore > _bestScore)
		{
			_bestScore = _currentScore;
			SaveBestScore();
		}
		UpdateUI();
	}

	private void UpdateUI()
	{
		if (_scoreLabel != null)
			_scoreLabel.Text = $"SKOR: {_currentScore}";
		if (_bestScoreLabel != null)
			_bestScoreLabel.Text = $"EN YÜKSEK: {_bestScore}";
	}

	private void UpdateNextPreview()
{
	if (_nextPreview == null) return;

	// 1. Yeni seviye görselini yükle
	string path = $"res://sprites/L{_nextLevel}.png";
	if (ResourceLoader.Exists(path))
	{
		_nextPreview.Texture = GD.Load<Texture2D>(path);
	}

	// 2. Renk modülasyonunu sıfırla (orijinal renkler görünsün)
	_nextPreview.Modulate = Colors.White;

	// 3. Önizleme kutucuğu için kademeli ölçeklendirme
	float scale = 0.22f + (_nextLevel - 1) * 0.05f;
	_nextPreview.Scale = new Vector2(scale, scale);
}

	private void SaveBestScore()
	{
		using var config = new ConfigFile();
		config.SetValue("User", "BestScore", _bestScore);
		config.Save("user://robomerge.cfg");
	}

	private void LoadBestScore()
	{
		using var config = new ConfigFile();
		if (config.Load("user://robomerge.cfg") == Error.Ok)
		{
			_bestScore = (int)config.GetValue("User", "BestScore", 0);
		}
	}
}
