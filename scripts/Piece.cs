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

	// Seviyelere göre parça yarıçapları (L1 -> L9, L10 oluştuğu an doğrudan tahliye olur)
	public static readonly float[] LevelRadii = new float[]
	{
		32f,   // L1: Hurda Metal
		42f,   // L2: Vida / Somun
		54f,   // L3: Dişli
		68f,   // L4: Mekanizma
		84f,   // L5: Motor
		100f,  // L6: Enerji Çekirdeği
		114f,  // L7: Yeşil Yapay Zeka Çipi
		132f,  // L8: Sarı Optik Sensör
		150f   // L9: Robot Gövdesi
	};

	// Her sprite'ın gerçek opak piksel sınırına göre birebir ölçülmüş collider çarpanları
	private static readonly float[] ColliderMultipliers = new float[]
	{
		0.78f, // L1 (eski tam oturan değer)
		0.66f, // L2 (eski tam oturan değer)
		0.73f, // L3 (eski tam oturan değer)
		0.78f, // L4 (eski tam oturan değer)
		0.88f, // L5 (eski tam oturan değer)
		0.78f, // L6 (eski tam oturan değer)
		0.89f, // L7 (Yeşil AI Çipi — 229/256 tam ölçü)
		0.78f, // L8 (Sarı Optik Sensör — 200/256 tam ölçü)
		0.71f  // L9 (Robot Gövdesi — eski tam oturan değer)
	};

	private static readonly Vector2[] SpriteOffsets = new Vector2[]
	{
		new Vector2(1.5f, 0f),
		new Vector2(0.5f, 1.5f),
		new Vector2(2.0f, 0f),
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		Vector2.Zero,
		new Vector2(0.5f, -0.5f),
		new Vector2(2.5f, 1.5f)
	};

	public override void _Ready()
	{
		SpawnTimeSec = Time.GetTicksMsec() / 1000.0;
		ContactMonitor = true;
		MaxContactsReported = 12;
		ContinuousCd = CcdMode.CastRay; // Küçük parçaların (L1-L3) hızlı düşerken çarpışmayı kaçırmasını engeller

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

		// Eski L10/L8 poligon collider kalıntısı varsa temizle (Final Robot oluştuğu an direkt banda gider)
		var oldPolyCol = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
		if (oldPolyCol != null)
		{
			oldPolyCol.QueueFree();
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
		int idx = Mathf.Clamp(Level, 1, 9) - 1;
		return LevelRadii[idx] * ColliderMultipliers[idx];
	}

	public void SetLevel(int newLevel)
	{
		Level = Mathf.Clamp(newLevel, 1, 9);
		Mass = 1.0f + (Level - 1) * 0.55f;
		UpdateAppearance();
	}

	// Seviyeye göre sprite boyutunu ve dairesel çarpışma şeklini günceller (L1 - L9)
	private void UpdateAppearance()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		var circleCol = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		int idx = Mathf.Clamp(Level, 1, 9) - 1;
		float targetRadius = LevelRadii[idx];
		float scale = (targetRadius * 2f) / 256f;

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
			circle.Radius = targetRadius * ColliderMultipliers[idx];
		}

		if (sprite != null)
		{
			string path = $"res://sprites/L{idx + 1}.png";
			if (ResourceLoader.Exists(path))
			{
				sprite.Texture = GD.Load<Texture2D>(path);
			}
			sprite.Position = SpriteOffsets[idx];
			sprite.Scale = new Vector2(scale, scale);
		}
	}

	// Her fizik karesinde aktif temas halindeki aynı seviye parçaları kontrol et
	// (Duvarla temas veya ilk kare gecikmesi yüzünden BodyEntered kaçsa bile anında birleşir)
	public override void _PhysicsProcess(double delta)
	{
		if (IsMerged || Freeze) return;

		var bodies = GetCollidingBodies();
		for (int i = 0; i < bodies.Count; i++)
		{
			if (bodies[i] is Piece otherPiece)
			{
				if (TryMergeWith(otherPiece))
					return;
			}
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
				float volDb = Mathf.Lerp(-15f, -4f, Mathf.Clamp((speed - 65f) / 320f, 0f, 1f));
				mainScene.PlayCollisionSfx(pitch, volDb);
			}
		}

		// Aynı seviyedeki iki parça temas ettiğinde birleştir
		if (body is Piece otherPiece)
		{
			TryMergeWith(otherPiece);
		}
	}

	private bool TryMergeWith(Piece otherPiece)
	{
		if (IsMerged || otherPiece == null || !IsInstanceValid(otherPiece) || otherPiece.IsMerged)
			return false;

		if (Level != otherPiece.Level || Freeze || otherPiece.Freeze)
			return false;

		// İki parçadan sadece biri birleşmeyi tetiklesin
		if (GetInstanceId() > otherPiece.GetInstanceId())
			return false;

		IsMerged = true;
		otherPiece.IsMerged = true;

		double now = Time.GetTicksMsec() / 1000.0;
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
		return true;
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
