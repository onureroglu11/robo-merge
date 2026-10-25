using Godot;

namespace MergeRobotB;

public partial class Piece : RigidBody2D
{
	[Export]
	public int Level = 1;

	public bool IsMerged = false;
	public bool HasDropped = false;
	public int ChainCount = 0; // Zincirleme birleşmeleri takip eder
	public double SpawnTimeSec = 0.0;

	private double _lastHitSoundTime = 0.0;
	private bool _isInDangerWarning = false;
	private float _warningPulseTime = 0f;

	// Seviyelere göre parça yarıçapları (L1 -> L8)
	public static readonly float[] LevelRadii = new float[]
	{
		34f,   // L1: Hurda Metal
		46f,   // L2: Vida / Somun
		62f,   // L3: Dişli
		80f,   // L4: Mekanizma
		102f,  // L5: Motor
		125f,  // L6: Enerji Çekirdeği
		150f,  // L7: Robot Gövdesi
		176f   // L8: Robot
	};

	// Görsellerdeki şeffaf boşluklara göre ayarlanmış collider çarpanları
	private static readonly float[] ColliderMultipliers = new float[]
	{
		0.78f, // L1
		0.66f, // L2
		0.73f, // L3
		0.78f, // L4
		0.88f, // L5
		0.78f, // L6
		0.71f, // L7
		0.91f  // L8
	};

	private static readonly Vector2[] SpriteOffsets = new Vector2[]
	{
		new Vector2(1.5f, 0f),
		new Vector2(0.5f, 1.5f),
		new Vector2(2.0f, 0f),
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		new Vector2(2.5f, 1.5f),
		new Vector2(0.5f, 0f)
	};

	// L8 Robotun kafa, omuz, kol ve bacaklarını saran özel poligon noktaları
	//
	private static readonly Vector2[] L8RobotContourPolygon = new Vector2[]
	{
		new(-15f, -97f), new(0f, -99f), new(15f, -97f),
		new(30f, -86f), new(48f, -72f),
		new(72f, -68f), new(92f, -57f), new(100f, -38f),
		new(108f, -12f), new(116f, 12f), new(118f, 36f), new(115f, 58f),
		new(107f, 75f), new(90f, 79f), new(75f, 72f),
		new(67f, 62f), new(72f, 84f), new(72f, 99f), new(50f, 102f), new(22f, 98f),
		new(16f, 68f), new(0f, 62f), new(-16f, 68f),
		new(-22f, 98f), new(-50f, 102f), new(-72f, 99f), new(-72f, 84f), new(-67f, 62f),
		new(-75f, 72f), new(-90f, 79f), new(-107f, 75f),
		new(-115f, 58f), new(-118f, 36f), new(-116f, 12f), new(-108f, -12f),
		new(-100f, -38f), new(-92f, -57f), new(-72f, -68f),
		new(-48f, -72f), new(-30f, -86f)
	};

	public override void _Ready()
	{
		SpawnTimeSec = Time.GetTicksMsec() / 1000.0;
		ContactMonitor = true;
		MaxContactsReported = 8;

		// Parçaların birbirinin üzerinden rahat kayması için
		var mat = new PhysicsMaterial
		{
			Friction = 0.25f,
			Bounce = 0.12f
		};
		PhysicsMaterialOverride = mat;

		BodyEntered += OnBodyEntered;

		var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collisionShape != null && collisionShape.Shape == null)
		{
			collisionShape.Shape = new CircleShape2D();
		}

		var polyCol = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
		if (polyCol == null)
		{
			polyCol = new CollisionPolygon2D
			{
				Name = "CollisionPolygon2D",
				Polygon = L8RobotContourPolygon,
				Disabled = true,
				Visible = false
			};
			AddChild(polyCol);
		}
		else
		{
			polyCol.Polygon = L8RobotContourPolygon;
		}

		UpdateAppearance();
	}

	public override void _Process(double delta)
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null) return;

		// Tehlike çizgisini geçen parçada kırmızı ısınma efekti
		if (_isInDangerWarning)
		{
			_warningPulseTime += (float)delta * 10f;
			float pulse = (Mathf.Sin(_warningPulseTime) + 1f) * 0.5f;
			sprite.Modulate = new Color(1.0f, Mathf.Lerp(0.25f, 0.85f, pulse), Mathf.Lerp(0.25f, 0.85f, pulse));
		}
		else if (sprite.Modulate != Colors.White)
		{
			sprite.Modulate = Colors.White;
		}
	}

	public void SetDangerWarning(bool warning)
	{
		_isInDangerWarning = warning;
		if (!warning)
		{
			_warningPulseTime = 0f;
		}
	}

	public float GetEffectiveRadius()
	{
		int idx = Mathf.Clamp(Level, 1, 8) - 1;
		return LevelRadii[idx] * ColliderMultipliers[idx];
	}

	public void SetLevel(int newLevel)
	{
		Level = Mathf.Clamp(newLevel, 1, 8);
		Mass = 1.0f + (Level - 1) * 0.65f;
		UpdateAppearance();
	}

	// Seviyeye göre sprite boyutunu ve çarpışma şeklini günceller
	private void UpdateAppearance()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		var circleCol = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		var polyCol = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

		float targetRadius = LevelRadii[Level - 1];
		float scale = (targetRadius * 2f) / 256f;

		if (Level == 8 && polyCol != null)
		{
			// L8 için daireyi kapatıp robot poligonunu açıyoruz
			if (circleCol != null)
			{
				circleCol.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
				circleCol.Visible = false;
			}

			polyCol.Polygon = L8RobotContourPolygon;
			polyCol.Scale = new Vector2(scale, scale);
			polyCol.SetDeferred(CollisionPolygon2D.PropertyName.Disabled, false);
			polyCol.Visible = true;
		}
		else
		{
			// L1 - L7 arası parçalar için dairesel collider kullanıyoruz
			if (polyCol != null)
			{
				polyCol.SetDeferred(CollisionPolygon2D.PropertyName.Disabled, true);
				polyCol.Visible = false;
			}

			if (circleCol != null)
			{
				circleCol.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
				circleCol.Visible = true;

				if (circleCol.Shape is not CircleShape2D circle)
				{
					circle = new CircleShape2D();
					circleCol.Shape = circle;
				}
				else
				{
					circle = (CircleShape2D)circle.Duplicate();
					circleCol.Shape = circle;
				}
				circle.Radius = targetRadius * ColliderMultipliers[Level - 1];
			}
		}

		if (sprite != null)
		{
			string path = $"res://sprites/L{Level}.png";
			if (ResourceLoader.Exists(path))
			{
				sprite.Texture = GD.Load<Texture2D>(path);
			}
			sprite.Position = SpriteOffsets[Level - 1];
			sprite.Scale = new Vector2(scale, scale);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (IsMerged) return;

		double now = Time.GetTicksMsec() / 1000.0;
		float speed = LinearVelocity.Length();

		// Yeterli hızla çarptığında çarpışma sesi çal
		if (HasDropped && speed > 65f && (now - _lastHitSoundTime > 0.14))
		{
			_lastHitSoundTime = now;
			if (GetTree().CurrentScene is Main mainScene)
			{
				float pitch = (float)GD.RandRange(0.88, 1.14);
				float volDb = Mathf.Lerp(-15f, -2f, Mathf.Clamp((speed - 65f) / 320f, 0f, 1f));
				mainScene.PlayCollisionSfx(pitch, volDb);
			}
		}

		// Aynı seviyedeki iki parça temas ettiğinde birleştir
		if (body is Piece otherPiece)
		{
			if (otherPiece.IsMerged || Level != otherPiece.Level)
				return;

			if (Freeze || otherPiece.Freeze)
				return;

			// İki parçadan sadece biri birleşmeyi tetiklesin
			if (GetInstanceId() > otherPiece.GetInstanceId())
				return;

			IsMerged = true;
			otherPiece.IsMerged = true;

			Vector2 spawnPos = (GlobalPosition + otherPiece.GlobalPosition) * 0.5f;

			// Kısa süre içinde peş peşe birleşme olduysa zincir sayacını artır
			int prevChain = Mathf.Max(ChainCount, otherPiece.ChainCount);
			bool isRecentMerge = (now - SpawnTimeSec < 2.2 && ChainCount > 0) ||
								 (now - otherPiece.SpawnTimeSec < 2.2 && otherPiece.ChainCount > 0);
			int newChain = isRecentMerge ? (prevChain + 1) : 1;

			if (GetTree().CurrentScene is Main main)
			{
				main.MergePieces(Level + 1, spawnPos, newChain);
			}

			QueueFree();
			otherPiece.QueueFree();
		}
	}

	// Birleşmeden doğan yeni parçanın büyüme animasyonu
	public void PlaySpawnAnimation()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null) return;

		float targetRadius = LevelRadii[Level - 1];
		float scale = (targetRadius * 2f) / 256f;
		Vector2 targetScale = new Vector2(scale, scale);

		sprite.Scale = Vector2.Zero;
		var tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(sprite, "scale", targetScale, 0.22f);
	}
}
